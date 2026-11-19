using System.Collections.Generic;
using UnityEngine;

namespace Nox.Interactions {
	public interface IGrabber {
		Transform GrabPoint { get; }

		EntityRig Rig { get; }

		GrabSource Source { get; }

		GrabKind Kind { get; }

		GrabState State { get; }

		IGrabbable HeldGrabbable { get; }

		Vector3 HoldingPoint { get; }

		float ReachDistance { get; set; }

		bool CanGrab(IGrabbable grabbable)
			=> grabbable != null
				&& HeldGrabbable == null
				&& State == GrabState.Free
				&& grabbable.CanBeGrabbedBy(this);

		bool TryGrab(IGrabbable grabbable, in RaycastHit contact, float duration);

		bool TryRelease(IGrabbable grabbable, float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity);

		bool TryReleaseHeld(float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity);
	}

	public interface IGrabberTargeting {
		IGrabbable CurrentTarget { get; }

		LayerMask TargetLayers { get; set; }

		bool TryFindTarget(out IGrabbable grabbable, out RaycastHit contact);

		void SetTarget(IGrabbable grabbable, in RaycastHit contact);

		void ClearTarget(IGrabbable grabbable);
	}

	public interface IGrabberBody {
		IReadOnlyList<Collider> BodyColliders { get; }
	}

	public interface IPhysicsGrabber {
		bool PhysicsJoint { get; }
	}

	public interface IGrabberPalm {
		/// <summary>Volume of the palm: the part an object coming into the hand rests against.</summary>
		Collider PalmVolume { get; }

		/// <summary>
		/// Point of the palm skin facing <paramref name="worldPoint"/>, pushed out along that direction: an object brought
		/// into the hand rests against the palm there, instead of being centred inside the palm volume.
		/// </summary>
		bool TryGetPalmSurface(Vector3 worldPoint, out Vector3 point);
	}

	/// <summary>
	/// A grabber that brings the object to its grab point instead of holding it at the distance it was
	/// caught from (a pull, a far grab). The grabbable then blends the object onto the point, whatever its
	/// own <see cref="GrabType"/> is.
	/// </summary>
	public interface IGrabPull {
		bool PullsToPoint { get; }
	}

	/// <summary>
	/// A grabber whose own pose can be offset from its tracked pose, the way AutoHand holds a hand on an object another
	/// hand carries (its <c>grabPositionOffset</c>). An object held by several grabbers writes that offset, so it drags
	/// them onto itself instead of drifting away from hands that stayed on their controllers. An object carried by a servo
	/// on its own body writes it too: the world can stop such an object, so the hand has to stay on the grip instead of
	/// following its controller through it.
	/// </summary>
	public interface IGrabberOffset {
		/// <summary>Offset applied on top of the tracked pose: added in world space, rotated in the tracked frame.</summary>
		void SetOffset(Vector3 position, Quaternion rotation);

		void ClearOffset();
	}

	/// <summary>
	/// A grabber whose body is driven by a follower, the equivalent of AutoHand's <c>HandFollow</c> (see <c>XRFollow</c>):
	/// the body chases a follow point instead of being written to the tracked pose.
	/// <para>
	/// A grabber may or may not have one, and a grabbable has to tell them apart: a follower can be pulled onto the object
	/// through <see cref="IGrabberOffset"/>, where a grabber without a follower already is its grab point and has to be left
	/// alone. A grabbable held by several grabbers only offsets the followers, so the body carrying the object never chases
	/// itself.
	/// </para>
	/// </summary>
	public interface IGrabberFollower : IGrabberPose, IGrabberOffset {
		/// <summary>Whether the follower drives the grabber right now (a body that is only teleported cannot be offset).</summary>
		bool FollowsBody { get; }
	}
}
