using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private HoldFollower follower;

		private bool heldBodyCached;
		private bool heldWasKinematic;
		private bool heldWasGravity;
		private RigidbodyInterpolation heldWasInterpolation;
		private RigidbodyConstraints heldWasConstraints;
		private bool physicsHeld;

		private float blendDuration;
		private float blendElapsed;
		private Vector3 blendFromPosition;
		private Quaternion blendFromRotation;

		public bool HeldBodyIsKinematic
			=> body != null && body.isKinematic;

		/// <summary>
		/// Whether the object is still travelling into the grab point (the blend started by the grab has not
		/// finished). A grabber uses it to wait for the object before shaping fingers or moving the hold.
		/// </summary>
		public bool IsHoldBlending
			=> blendDuration > 0f && blendElapsed < blendDuration;

		private void BeginHold(float duration, IGrabber grabber) {
			var effective = HoldFor(grabber);
			blendDuration = effective.snap != GrabSnap.None
				? 0f
				: Mathf.Max(0f, duration);
			blendElapsed = 0f;
			blendFromPosition = transform.position;
			blendFromRotation = transform.rotation;
			holdPoint = grabber != null && grabber.GrabPoint != null
				? grabber.GrabPoint.position
				: transform.position;

			var physics = IsPhysicsHold && grabber is IPhysicsGrabber { PhysicsJoint: true };
			physicsHeld = physics;

			if (follower != null)
				follower.enabled = !physics;

			if (physics) {
				blendDuration = 0f;
				blendElapsed = 0f;

				ApplyHeldMaterial();

				if (body != null) {
					body.linearVelocity = Vector3.zero;
					body.angularVelocity = Vector3.zero;
					if (!body.isKinematic)
						body.WakeUp();
				}

				return;
			}

			if (hold.dynamic || body == null)
				return;

			heldBodyCached = true;
			heldWasKinematic = body.isKinematic;
			heldWasGravity = body.useGravity;
			heldWasInterpolation = body.interpolation;
			heldWasConstraints = body.constraints;

			if (!body.isKinematic) {
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}

			body.interpolation = RigidbodyInterpolation.None;
			body.constraints = RigidbodyConstraints.None;
			body.isKinematic = true;
		}

		private void EndHold() {
			blendDuration = 0f;
			blendElapsed = 0f;
			physicsHeld = false;

			RestoreHeldMaterial();

			if (follower != null)
				follower.enabled = false;

			if (!heldBodyCached || body == null)
				return;

			heldBodyCached = false;

			body.constraints = heldWasConstraints;
			body.interpolation = heldWasInterpolation;
			body.isKinematic = heldWasKinematic;
			body.useGravity = heldWasGravity;
		}
	}
}
