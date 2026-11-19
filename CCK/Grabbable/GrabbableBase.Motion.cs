using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		protected virtual void LateUpdate() {
			if (state != GrabState.Held || grabbers.Count == 0)
				return;

			RefreshBodies();
			CheckTear(grabbers[0]);

			if (!TryGetHoldPoint(out var point))
				return;

			motion.Track(point.position, point.rotation, Time.time);
		}
	}
}
