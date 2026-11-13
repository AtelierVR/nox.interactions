using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private const float HoldSettledTolerance = 0.005f;

		private Vector3 localPos;
		private Quaternion localRot = Quaternion.identity;
		private Vector3 holdPoint;

		/// <summary>
		/// World position the hold drives the object to (same maths as <see cref="FollowPoint"/>), so a
		/// grabber can tell whether the object is already where it will be held.
		/// </summary>
		public Vector3 HoldTarget {
			get {
				var grabber = grabbers.Count > 0 ? grabbers[0] : null;
				var point = grabber != null ? grabber.GrabPoint : null;

				if (point == null)
					return transform.position;

				switch (mode) {
					case GrabMode.Snap:
						return point.position - point.rotation * transform.InverseTransformPoint(GrabAnchor.position);
					case GrabMode.FixedPosition:
						return transform.position;
					default:
						return point.TransformPoint(localPos);
				}
			}
		}

		/// <summary>
		/// Whether the object sits on the position its hold drives it to. A grabber waits for that before
		/// shaping fingers: an object still travelling towards the hand (or lagging one frame behind it) is
		/// not a reference for the hand pose.
		/// </summary>
		public bool IsHoldSettled
			=> state == GrabState.Held
				&& !IsHoldBlending
				&& (HoldTarget - transform.position).sqrMagnitude <= HoldSettledTolerance * HoldSettledTolerance;

		public void FollowHeld(float deltaTime) {
			if (state != GrabState.Held) return;
			if (grabbers.Count == 0) return;
			if (physicsHeld) return;

			var primary = grabbers[0];
			if (primary == null || primary.GrabPoint == null) return;

			var point = primary.GrabPoint.position;
			var moved = Vector3.Distance(point, holdPoint);
			holdPoint = point;

			if (blendDuration > 0f && blendElapsed < blendDuration)
				blendElapsed += moved * InteractionConstants.GrabSpeedUp;

			FollowPoint(primary.GrabPoint, mode, deltaTime);
		}

		protected virtual void FollowPoint(Transform point, GrabMode followMode, float deltaTime) {
			Vector3 position;
			Quaternion rotation;

			var effective = HoldFor(grabbers.Count > 0 ? grabbers[0] : null);
			var snapPosition = (effective.snap & GrabSnap.Position) != 0;
			var snapRotation = (effective.snap & GrabSnap.Rotation) != 0;

			switch (followMode) {
				case GrabMode.Snap:
					rotation = point.rotation;
					position = point.position - rotation * transform.InverseTransformPoint(GrabAnchor.position);
					break;
				case GrabMode.FixedRotation:
					position = point.TransformPoint(localPos);
					rotation = transform.rotation;
					break;
				case GrabMode.FixedPosition:
					position = transform.position;
					rotation = point.rotation * localRot;
					break;
				default:
					position = point.TransformPoint(localPos);
					rotation = point.rotation * localRot;
					break;
			}

			if (blendDuration > 0f && blendElapsed < blendDuration) {
				blendElapsed += deltaTime;

				var blend = Mathf.Clamp01(blendElapsed / blendDuration);
				blend = 1f - (1f - blend) * (1f - blend);

				position = Vector3.Lerp(blendFromPosition, position, blend);
				rotation = Quaternion.Slerp(blendFromRotation, rotation, blend);
			}

			var current = transform.position;
			var weight = WeightScale(effective);
			var step = effective.followSpeed * weight * deltaTime;

			if (!snapPosition && step > 0f && Vector3.SqrMagnitude(position - current) > step * step)
				position = Vector3.MoveTowards(current, position, step);

			var degrees = effective.turnSpeed * weight * deltaTime;

			if (!snapRotation && degrees > 0f)
				rotation = Quaternion.RotateTowards(transform.rotation, rotation, degrees);

			if (position == current && rotation == transform.rotation)
				return;

			transform.SetPositionAndRotation(position, rotation);
		}

		private float WeightScale(in GrabHold effective) {
			if (body == null || effective.referenceMass <= 0f)
				return 1f;

			return Mathf.Sqrt(Mathf.Clamp01(effective.referenceMass / Mathf.Max(body.mass, 0.0001f)));
		}

		private void CaptureOffset(IGrabber grabber, EntityRig rig, in RaycastHit contact) {
			var effective = HoldFor(grabber);

			var point = grabber != null && grabber.GrabPoint != null
				? grabber.GrabPoint
				: transform;

			if (effective.snap == GrabSnap.Both) {
				localPos = Vector3.zero;
				localRot = Quaternion.identity;
				return;
			}

			if (CentersOnGrabPoint(grabber)) {
				localPos = contact.collider != null
					? point.InverseTransformDirection(transform.position - contact.point)
					: Vector3.zero;
				localRot = Quaternion.Inverse(point.rotation) * transform.rotation;
				return;
			}

			var snapPosition = (effective.snap & GrabSnap.Position) != 0;
			var snapRotation = (effective.snap & GrabSnap.Rotation) != 0;

			localPos = snapPosition
				? Vector3.zero
				: point.InverseTransformPoint(transform.position);
			localRot = snapRotation
				? Quaternion.identity
				: Quaternion.Inverse(point.rotation) * transform.rotation;

			if ((snapPosition && snapRotation) || !TryGetPose(rig, out var posePos, out var poseRot)) return;

			Quaternion inverse = Quaternion.Inverse(poseRot);

			if (!snapPosition)
				localPos = -(inverse * posePos);

			if (!snapRotation)
				localRot = inverse;
		}

		private void ClearOffset() {
			localPos = Vector3.zero;
			localRot = Quaternion.identity;
			holdPoint = transform.position;
		}

		/// <summary>
		/// Whether the hold targets the grab point itself: a <see cref="GrabKind.Direct"/> grabber (a hand)
		/// brings the object into the hand, and so does a <see cref="GrabType.GrabbableToHand"/> grab, whatever
		/// grabber performs it, and a pull grabber (<see cref="IGrabPull"/>). Any other grab keeps the object
		/// at the offset it was caught with.
		/// </summary>
		private bool CentersOnGrabPoint(IGrabber grabber)
			=> instantGrab
				|| grabType == GrabType.GrabbableToHand
				|| (grabber != null && (grabber.Kind == GrabKind.Direct || (grabber is IGrabPull pull && pull.PullsToPoint)));

		/// <summary>
		/// Hold actually used for this grabber. A <see cref="GrabKind.Direct"/> grabber (a hand, a held prop)
		/// ignores the snap and hold-distance hints of the grabbable and blends the object into the grab point.
		/// An <see cref="GrabKind.Indirect"/> grabber (ray, near/far, desktop) uses them as authored.
		/// </summary>
		private GrabHold HoldFor(IGrabber grabber) {
			var effective = hold;

			if (grabber != null && grabber.Kind == GrabKind.Direct) {
				effective.snap = GrabSnap.None;
				effective.atHoldDistance = false;
				effective.fixedDistance = false;
			}

			return effective;
		}
	}
}
