using System;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[Serializable]
	public struct GrabRay {
		public LayerMask layers;
		public float reach;
		public float offset;

		public static GrabRay Default
			=> new() {
				reach = InteractionConstants.DefaultReachDistance,
				offset = InteractionConstants.DefaultHoldingOffset
			};
	}

	[Serializable]
	public struct GrabHighlight {
		public bool enabled;
		public Material material;

		public static GrabHighlight Default
			=> new() {
				enabled = true
			};
	}
}
