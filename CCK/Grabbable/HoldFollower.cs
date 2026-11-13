using UnityEngine;

namespace Nox.CCK.Interactions {
	[DefaultExecutionOrder(10000)]
	[AddComponentMenu("")]
	public sealed class HoldFollower : MonoBehaviour {
		public GrabbableBase Target { get; set; }

		private void LateUpdate() {
			if (Target == null)
				return;

			Target.FollowHeld(Time.deltaTime);
		}
	}
}
