using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private IMotion provider;

		private Motion released {
			get {
				if (release.motion == null)
					release.motion = new Motion();

				return release.motion;
			}
		}

		public bool MotionEnabled {
			get => release.enabled;
			set => release.enabled = value;
		}

		public IMotion Motion {
			get => provider ?? released;
			set => provider = value;
		}

		protected virtual void LateUpdate() {
			if (!release.enabled || state != GrabState.Held) return;
			if (grabbers.Count == 0) return;

			var point = grabbers[0].GrabPoint;
			if (point == null) return;

			Motion.Track(point.position, point.rotation, Time.time);
		}

		private void Throw(float multiplier) {
			if (!release.enabled || multiplier == 0f) return;
			if (release.multiplier <= 0f) return;
			if (!Motion.TryGet(out var linear, out var angular)) return;

			ApplyThrow(linear, angular, multiplier * release.multiplier);
		}
	}
}
