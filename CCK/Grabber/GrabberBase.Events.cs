using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabberBase {
		[SerializeField] private GrabberGrabEvent onGrabbed = new();
		[SerializeField] private GrabberReleaseEvent onReleased = new();
		[SerializeField] private GrabberTargetEvent onTargetChanged = new();
		[SerializeField] private GrabberStateEvent onStateChanged = new();

		public GrabberGrabEvent OnGrabbedEvent => onGrabbed;

		public GrabberReleaseEvent OnReleasedEvent => onReleased;

		public GrabberTargetEvent OnTargetChangedEvent => onTargetChanged;

		public GrabberStateEvent OnStateChangedEvent => onStateChanged;

		private void RaiseGrabbed(IGrabbable grabbable) {
			if (onGrabbed != null)
				onGrabbed.Invoke(this, grabbable as GrabbableBase);
			RaiseStateChanged();
		}

		private void RaiseReleased(IGrabbable grabbable, float throwMultiplier) {
			if (onReleased != null)
				onReleased.Invoke(this, grabbable as GrabbableBase);
			RaiseStateChanged();
		}

		private void RaiseTargetChanged(IGrabbable grabbable) {
			if (onTargetChanged != null)
				onTargetChanged.Invoke(this, grabbable as GrabbableBase);
		}

		private void RaiseStateChanged() {
			if (onStateChanged != null)
				onStateChanged.Invoke(this, state);
		}
	}
}
