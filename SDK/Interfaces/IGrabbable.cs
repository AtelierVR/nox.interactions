using System.Collections.Generic;
using UnityEngine;

namespace Nox.Interactions {
	public interface IGrabbable {
		Transform Target { get; }

		GrabState State { get; }

		bool CanBeGrabbedBy(IGrabber grabber);

		bool TryAddGrabber(IGrabber grabber, in RaycastHit contact, float duration);

		bool TryRemoveGrabber(IGrabber grabber, float throwMultiplier);
	}

	public interface IGrabGroup {
		int HeldCount { get; }

		IReadOnlyList<IGrabber> Grabbers { get; }

		bool SingleGrabberOnly { get; set; }

		void RemoveAllGrabbers(float throwMultiplier);
	}

	public interface IGrabBody {
		Rigidbody Body { get; }
	}

	public interface IGrabColliders {
		IReadOnlyList<Collider> GrabColliders { get; }
	}

	public interface IGrabAccess {
		EntityRig Allowed { get; set; }
	}

	public interface IGrabAttachment {
		GrabMode Mode { get; set; }

		GrabPoseType PoseType { get; set; }
	}

	public interface IGrabHold {
		GrabHold Hold { get; set; }
	}

	public interface IGrabThrow {
		float ThrowMultiplier { get; set; }

		void ApplyThrow(Vector3 linear, Vector3 angular, float multiplier);
	}

	/// <summary>Preferred way a direct grab brings this object into the hand (see <see cref="GrabType"/>).</summary>
	public interface IGrabApproach {
		GrabType GrabType { get; }

		bool InstantGrab { get; }
	}

	public interface IGrabHighlight {
		bool IsHighlightedBy(IGrabber grabber);

		void SetHighlight(IGrabber grabber, bool enabled, Material material);
	}
}
