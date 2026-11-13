using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public abstract partial class CursorBase {
		[SerializeField] private CursorTargetEvent onEnter = new();
		[SerializeField] private CursorTargetEvent onExit = new();
		[SerializeField] private CursorTargetEvent onPress = new();
		[SerializeField] private CursorTargetEvent onRelease = new();

		public CursorTargetEvent OnEnterEvent => onEnter;

		public CursorTargetEvent OnExitEvent => onExit;

		public CursorTargetEvent OnPressEvent => onPress;

		public CursorTargetEvent OnReleaseEvent => onRelease;

		private void RaiseEnter(IInteractTarget value) {
			if (onEnter != null)
				onEnter.Invoke(this, value as InteractTarget);
		}

		private void RaiseExit(IInteractTarget value) {
			if (onExit != null)
				onExit.Invoke(this, value as InteractTarget);
		}

		private void RaisePress(IInteractTarget value) {
			if (onPress != null)
				onPress.Invoke(this, value as InteractTarget);
		}

		private void RaiseRelease(IInteractTarget value) {
			if (onRelease != null)
				onRelease.Invoke(this, value as InteractTarget);
		}
	}
}
