using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		[SerializeField] private GrabbableGrabEvent onGrabbed = new();
		[SerializeField] private GrabbableReleaseEvent onReleased = new();
		[SerializeField] private GrabbableHighlightEvent onHighlightChanged = new();
		[SerializeField] private GrabbableSqueezeEvent onSqueezed = new();
		[SerializeField] private GrabbableSqueezeEvent onUnsqueezed = new();
		[SerializeField] private GrabbableSqueezeEvent onTorn = new();

		public GrabbableGrabEvent OnGrabbedEvent => onGrabbed;

		public GrabbableReleaseEvent OnReleasedEvent => onReleased;

		public GrabbableHighlightEvent OnHighlightChangedEvent => onHighlightChanged;

		public GrabbableSqueezeEvent OnSqueezedEvent => onSqueezed;

		public GrabbableSqueezeEvent OnUnsqueezedEvent => onUnsqueezed;

		public GrabbableSqueezeEvent OnTornEvent => onTorn;

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

		private void RaiseSqueezed(IGrabber grabber, bool squeezed) {
			var target = squeezed ? onSqueezed : onUnsqueezed;

			if (target != null)
				target.Invoke(this, grabber as GrabberBase);
		}

		private void RaiseTorn(IGrabber grabber) {
			if (onTorn != null)
				onTorn.Invoke(this, grabber as GrabberBase);
		}
	}
}
