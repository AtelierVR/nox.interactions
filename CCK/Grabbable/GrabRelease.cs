using System;
using Nox.Interactions;

namespace Nox.CCK.Interactions {
	[Serializable]
	public struct GrabRelease {
		public bool enabled;
		public float multiplier;
		public Motion motion;

		public static GrabRelease Default
			=> new() {
				enabled = true,
				multiplier = InteractionConstants.DefaultThrowMultiplier
			};
	}
}
