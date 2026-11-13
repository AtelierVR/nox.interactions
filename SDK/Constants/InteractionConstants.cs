using UnityEngine;

namespace Nox.Interactions {
	public static class InteractionConstants {
		public const string GrabbableLayerName = "Grabbable";
		public const string GrabbingLayerName = "Grabbing";
		public const string HandLayerName = "Hand";

		public const float DefaultReachDistance = 0.2f;
		public const float DefaultHoldingOffset = 0.02f;
		public const float DefaultContactRadius = 0.03f;
		public const int DefaultConformSteps = 40;
		public const float DefaultTipRadius = 0.01f;
		public const float MinGrabDuration = 0.05f;
		public const float MaxGrabDuration = 0.25f;
		public const float GrabSpeedUp = 120f;
		public const float DefaultJointBreakForce = 3500f;
		public const float DefaultHeldVelocity = 10f;
		public const float DefaultThrowMultiplier = 1f;
		public const float DefaultIgnoreReleaseTime = 0.5f;
		public const float ContactEpsilon = 0.0001f;

		public static int GrabbableLayer
			=> LayerMask.NameToLayer(GrabbableLayerName);

		public static int GrabbingLayer
			=> LayerMask.NameToLayer(GrabbingLayerName);

		public static int HandLayer
			=> LayerMask.NameToLayer(HandLayerName);

		public static LayerMask GrabbableMask
			=> ToMask(GrabbableLayer);

		public static LayerMask GrabbingMask
			=> ToMask(GrabbingLayer);

		public static LayerMask ToMask(int layer)
			=> layer >= 0
				? 1 << layer
				: 0;
	}
}
