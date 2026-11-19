using System.Collections.Generic;
using UnityEngine;

namespace Nox.Interactions {
	public interface IGrabbable {
		Transform Target { get; }

		GrabState State { get; }

		bool IsGrabbable { get; set; }

		bool CanBeGrabbedBy(IGrabber grabber);

		bool TryAddGrabber(IGrabber grabber, in RaycastHit contact, float duration);

		bool TryRemoveGrabber(IGrabber grabber, float throwMultiplier);
	}

	public interface IGrabGroup {
		int HeldCount { get; }

		IReadOnlyList<IGrabber> Grabbers { get; }

		bool SingleGrabberOnly { get; set; }

		bool AllowGrabberSwap { get; set; }

		void RemoveAllGrabbers(float throwMultiplier);
	}

	public interface IGrabPriority {
		float PriorityWeight { get; }
	}

	public interface IGrabBody {
		Rigidbody Body { get; }
	}

	/// <summary>
	/// Grabbable that caps how fast its grabber may travel while carrying it, the way AutoHand's
	/// <c>maxHeldVelocity</c> does: the arm slows down on a heavy object instead of dragging it through the world.
	/// </summary>
	public interface IGrabVelocity {
		float MaxHeldVelocity { get; }
	}

	public interface IGrabColliders {
		IReadOnlyList<Collider> GrabColliders { get; }
	}

	public interface IGrabAccess {
		EntityRig Allowed { get; set; }
	}

	/// <summary>How a direct grab brings this object into the hand, and the pose it is held with.</summary>
	public interface IGrabAttachment {
		GrabType GrabType { get; }

		bool InstantGrab { get; }

		GrabPoseType PoseType { get; set; }
	}

	public interface IGrabThrow {
		float ThrowPower { get; set; }

		void ApplyThrow(Vector3 linear, Vector3 angular, float multiplier);
	}

	/// <summary>
	/// Grabber that knows where it is tracked, independently of its grab point: an object held by several grabbers asks
	/// every one of them where it is, so it can sit between them.
	/// </summary>
	public interface IGrabberPose {
		bool TryGetTrackedPose(out Vector3 position, out Quaternion rotation);
	}

	/// <summary>
	/// Grabbable that reacts to being crushed while it is held, the way AutoHand fires <c>onSqueeze</c> when the squeeze
	/// input of the hand is pressed on a held object: the object decides what crushing means (a shape key, a sound, a
	/// break), the grabber only reports the input.
	/// </summary>
	public interface IGrabSqueeze {
		void Squeeze(IGrabber grabber);

		void Unsqueeze(IGrabber grabber);
	}

	/// <summary>
	/// Grabbable attached to something else (a joint on the object): it can be torn off that attachment and taken, the way
	/// AutoHand breaks the joint of a held door or drawer. The grabber only reports the pull, the object decides when it
	/// gives up.
	/// </summary>
	public interface IGrabTear {
		bool CanTear { get; }

		bool TryTear(IGrabber grabber);
	}

	public interface IGrabHighlight {
		bool IsHighlightedBy(IGrabber grabber);

		void SetHighlight(IGrabber grabber, bool enabled, Material material);
	}
}
