using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		[SerializeField] private GrabbableGrabEvent onGrabbed = new();
		[SerializeField] private GrabbableReleaseEvent onReleased = new();
		[SerializeField] private GrabbableHighlightEvent onHighlightChanged = new();

		public GrabbableGrabEvent OnGrabbedEvent => onGrabbed;

		public GrabbableReleaseEvent OnReleasedEvent => onReleased;

		public GrabbableHighlightEvent OnHighlightChangedEvent => onHighlightChanged;

		private void RaiseGrabbed(IGrabber grabber) {
			if (onGrabbed != null)
				onGrabbed.Invoke(this, grabber as GrabberBase);
		}

		private void RaiseReleased(IGrabber grabber, float releaseMultiplier) {
			if (onReleased != null)
				onReleased.Invoke(this, grabber as GrabberBase);
		}

		private void RaiseHighlightChanged(IGrabber grabber, bool enabled) {
			if (onHighlightChanged != null)
				onHighlightChanged.Invoke(this, grabber as GrabberBase, enabled);
		}
	}
}
