using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabberBase {
		public bool TryGrab(IGrabbable grabbable, in RaycastHit contact, float duration) {
			if (grabbable == null || held != null) return false;
			if (!grabbable.CanBeGrabbedBy(this)) return false;

			ClearTarget(grabbable);

			if (!grabbable.TryAddGrabber(this, contact, duration)) return false;

			held = grabbable;
			state = GrabState.Held;

			RaiseGrabbed(grabbable);
			return true;
		}

		public bool TryGrabTarget(float duration) {
			if (target == null) return false;
			return TryGrab(target, hit, duration);
		}

		public bool TryRelease(IGrabbable grabbable, float throwMultiplier) {
			if (grabbable == null || held != grabbable) return false;
			if (!grabbable.TryRemoveGrabber(this, throwMultiplier)) return false;

			held = null;
			state = GrabState.Free;

			RaiseReleased(grabbable, throwMultiplier);
			return true;
		}

		public bool TryRelease(IGrabbable grabbable, float throwMultiplier, Vector3 linear, Vector3 angular) {
			if (!TryRelease(grabbable, throwMultiplier)) return false;

			if (grabbable is IGrabThrow thrower)
				thrower.ApplyThrow(linear, angular, throwMultiplier);

			return true;
		}

		public bool TryReleaseHeld(float throwMultiplier)
			=> TryRelease(held, throwMultiplier);

		public bool TryReleaseHeld(float throwMultiplier, Vector3 linear, Vector3 angular)
			=> TryRelease(held, throwMultiplier, linear, angular);
	}
}
