using System;
using Nox.Interactions;

namespace Nox.CCK.Interactions {
	[Serializable]
	public struct GrabLimits {
		public float drag;
		public float mass;

		public static GrabLimits Default
			=> new() {
				drag = 1.5f,
				mass = 0.1f
			};
	}
}
