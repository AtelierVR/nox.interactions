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
		bool TryGetPalmNormal(Vector3 worldPoint, out Vector3 normal);
	}

	/// <summary>
	/// A grabber that brings the object to its grab point instead of holding it at the distance it was
	/// caught from (a pull, a far grab). The grabbable then blends the object onto the point, whatever its
	/// own <see cref="GrabType"/> is.
	/// </summary>
	public interface IGrabPull {
		bool PullsToPoint { get; }
	}
}
