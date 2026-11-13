using System;

namespace Nox.Interactions {
	[Flags]
	public enum EntityRig {
		None = 0,
		Head = 1 << 0,
		LeftHand = 1 << 1,
		RightHand = 1 << 2,
		LeftFoot = 1 << 3,
		RightFoot = 1 << 4,
		Body = 1 << 5,
		Hands = LeftHand | RightHand,
		Feet = LeftFoot | RightFoot,
		All = Head | Hands | Feet | Body
	}

	public enum GrabState {
		Free = 0,
		Pending = 1,
		Held = 2,
		Releasing = 3
	}

	public enum GrabMode {
		Free = 0,
		Snap = 1,
		FixedRotation = 2,
		FixedPosition = 3
	}

	public enum GrabPoseType {
		Freeform = 0,
		Pinch = 1,
		Custom = 2
	}

	[Flags]
	public enum GrabSnap {
		None = 0,
		Position = 1 << 0,
		Rotation = 1 << 1,
		Both = Position | Rotation
	}

	public enum GrabSource {
		Direct = 0,
		Ray = 1,
		Script = 2,
		Remote = 3
	}

	/// <summary>
	/// How a grabber reaches its object. A <see cref="Direct"/> grabber holds it with a body (a hand, a
	/// grabbed prop), so the object is blended to it and stays where it was caught. An <see cref="Indirect"/>
	/// grabber points at it (ray, near/far, desktop), so the object's own hold hints (snap, hold distance)
	/// are honoured.
	/// </summary>
	public enum GrabKind {
		Direct = 0,
		Indirect = 1
	}

	/// <summary>
	/// How a direct grab brings the object into the hand, following AutoHand: either the object flies to
	/// the hand, or the hand reaches the object and comes back to its follow position with it.
	/// </summary>
	public enum GrabType {
		/// <summary>No preference: the grabber's own type is used.</summary>
		Default = 0,

		/// <summary>Attach immediately, no animation.</summary>
		InstantGrab = 1,

		/// <summary>The hand reaches the object, grabs it, then returns to its follow position with it.</summary>
		HandToGrabbable = 2,

		/// <summary>The object flies from where it is to the hand.</summary>
		GrabbableToHand = 3
	}

	public enum PointerMode {
		Near = 0,
		Far = 1
	}

	public enum CursorState {
		Idle = 0,
		Hover = 1,
		Press = 2
	}

	public enum GrabInputMode {
		Hold = 0,
		Toggle = 1
	}
}
