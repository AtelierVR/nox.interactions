using System;

namespace Nox.Interactions {
	[Serializable]
	public struct GrabHold {
		public bool dynamic;
		public float followSpeed;
		public float turnSpeed;
		public float referenceMass;
		public float physicsMass;
		public GrabSnap snap;
		public bool fixedDistance;
		public bool atHoldDistance;
		public float distance;

		public static GrabHold Default
			=> new() {
				followSpeed = InteractionConstants.DefaultHeldVelocity,
				turnSpeed = 720f
			};
	}
}
