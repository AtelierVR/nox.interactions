using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private const float HoldSettledTolerance = 0.005f;

		private HoldFollower follower;

		private bool physicsHeld;

		/// <summary>
		/// Whether the object is carried by a velocity servo on its own body, the AutoHand <c>WeightlessFollower</c>: the body
		/// stays <b>dynamic</b>, so the world can still block or rub it while it follows its grip.
		/// </summary>
		private bool dynamicHeld;

		/// <summary>
		/// Whether the world stopped the servo carried body: it was asked for a step and made less than half of it. A
		/// grabber then has to stay on its grip, since the object cannot come to it.
		/// </summary>
		private bool servoBlocked;

		/// <summary>Pose of the body when the servo last wrote its velocity, and the velocity it asked for.</summary>
		private Vector3 servoFrom;

		private Vector3 servoAsked;

		/// <summary>
		/// Whether a grabber is currently held on the object instead of on its controller: set while the world blocks the
		/// servo carried body, cleared once it caught up with its grip again, so the grabber is never snapped back.
		/// </summary>
		private bool gripLatched;

		private bool heldBodyCached;
		private bool heldWasKinematic;
		private bool heldWasGravity;
		private RigidbodyInterpolation heldWasInterpolation;
		private RigidbodyConstraints heldWasConstraints;

		private float physicsDrag;
		private float physicsAngleDrag;
		private float physicsMass;

		private float blendDuration;
		private float blendElapsed;
		private Vector3 blendFromPosition;
		private Quaternion blendFromRotation;

		public bool PhysicsHeld => physicsHeld;

		/// <summary>Whether the body of the object is driven by the hold servo instead of being frozen (see <c>DriveHeld</c>).</summary>
		public bool DynamicHeld => dynamicHeld;

		/// <summary>
		/// Whether a weightless hold may leave the body dynamic, so the world still constrains it (AutoHand's
		/// <c>WeightlessFollower</c>). An object that places itself while held (a mirror that centres on its grab point and
		/// faces its grabber) has to be written directly instead: it turns the option off.
		/// </summary>
		protected virtual bool KeepBodyDynamic
			=> true;

		public bool IsHoldBlending => blendDuration > 0f && blendElapsed < blendDuration;

		public bool IsHoldSettled {
			get {
				if (state != GrabState.Held || IsHoldBlending)
					return false;
				if (!TryGetHoldPose(out var position, out _))
					return false;

				return (position - transform.position).sqrMagnitude <= HoldSettledTolerance * HoldSettledTolerance;
			}
		}

		private void BeginHold(float duration, IGrabber grabber) {
			blendDuration = Mathf.Max(0f, duration);
			blendElapsed = 0f;
			blendFromPosition = transform.position;
			blendFromRotation = transform.rotation;

			// A physical hold leaves the body dynamic: the grabber binds a joint between the two, so the weight of the
			// object is felt by whatever holds it. A weightless hold is driven by this object instead, keeping the body
			// dynamic so the world can still constrain it (AutoHand's WeightlessFollower).
			physicsHeld = !held.ignoreWeight
				&& body != null
				&& grabber is IPhysicsGrabber { PhysicsJoint: true };

			dynamicHeld = !physicsHeld && held.ignoreWeight && body != null && KeepBodyDynamic;

			if (physicsHeld) {
				blendDuration = 0f;
				blendElapsed = 0f;

				ApplyHeldMaterial();
				CachePhysicsBody();

				if (!body.isKinematic) {
					body.linearVelocity = Vector3.zero;
					body.angularVelocity = Vector3.zero;
					body.WakeUp();
				}
			}
			else if (dynamicHeld) {
				blendDuration = 0f;
				blendElapsed = 0f;

				ApplyHeldMaterial();
				CachePhysicsBody();
				MakeDynamic();

				servoBlocked = false;
				servoAsked = Vector3.zero;
				servoFrom = body.position;
				gripLatched = false;
			}
			else {
				MakeKinematic();
			}

			if (follower != null)
				follower.enabled = !physicsHeld;
		}

		private void EndHold() {
			blendDuration = 0f;
			blendElapsed = 0f;

			servoBlocked = false;
			servoAsked = Vector3.zero;
			gripLatched = false;

			RestoreHeldMaterial();

			if (physicsHeld) {
				RestorePhysicsBody();
				physicsHeld = false;
			}

			if (dynamicHeld) {
				dynamicHeld = false;
				RestorePhysicsBody();

				// the throw is applied after this: the servo velocity of the hold must not survive the release
				if (body != null && !body.isKinematic) {
					body.linearVelocity = Vector3.zero;
					body.angularVelocity = Vector3.zero;
				}
			}

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

		private void MakeKinematic() {
			if (body == null || body.isKinematic)
				return;

			heldBodyCached = true;
			heldWasKinematic = body.isKinematic;
			heldWasGravity = body.useGravity;
			heldWasInterpolation = body.interpolation;
			heldWasConstraints = body.constraints;

			body.linearVelocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.interpolation = RigidbodyInterpolation.None;
			body.constraints = RigidbodyConstraints.None;
			body.isKinematic = true;
		}

		/// <summary>
		/// Leaves the body dynamic while it is carried, the way AutoHand's <c>WeightlessFollower</c> does: gravity is off
		/// and the hold drives the velocity, but the body still collides, is pushed back by a wall and can be constrained
		/// by anything it touches.
		/// </summary>
		private void MakeDynamic() {
			if (body == null)
				return;

			heldBodyCached = true;
			heldWasKinematic = body.isKinematic;
			heldWasGravity = body.useGravity;
			heldWasInterpolation = body.interpolation;
			heldWasConstraints = body.constraints;

			body.interpolation = RigidbodyInterpolation.Interpolate;
			body.constraints = RigidbodyConstraints.None;
			body.isKinematic = false;
			body.useGravity = false;
			body.linearVelocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.WakeUp();
		}

		private void CachePhysicsBody() {
			physicsDrag = body.linearDamping;
			physicsAngleDrag = body.angularDamping;
			physicsMass = body.mass;

			body.linearDamping = Mathf.Max(body.linearDamping, held.minDrag);
			body.angularDamping = Mathf.Max(body.angularDamping, held.minAngleDrag);
			body.mass = Mathf.Max(body.mass / Mathf.Max(held.massDivider, 1f), held.minMass);
		}

		private void RestorePhysicsBody() {
			if (body == null)
				return;

			body.linearDamping = physicsDrag;
			body.angularDamping = physicsAngleDrag;
			body.mass = physicsMass;
		}
	}
}
