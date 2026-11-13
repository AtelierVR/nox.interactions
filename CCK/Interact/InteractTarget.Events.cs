using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class InteractTarget {
		[SerializeField] private InteractTargetEvent onEnter = new();
		[SerializeField] private InteractTargetEvent onExit = new();
		[SerializeField] private InteractTargetEvent onPress = new();
		[SerializeField] private InteractTargetEvent onRelease = new();
		[SerializeField] private InteractPressureEvent onPressure = new();

		public InteractTargetEvent OnEnterEvent => onEnter;

		public InteractTargetEvent OnExitEvent => onExit;

		public InteractTargetEvent OnPressEvent => onPress;

		public InteractTargetEvent OnReleaseEvent => onRelease;

		public InteractPressureEvent OnPressureEvent => onPressure;

		private void RaiseEnter(ICursor cursor) {
			if (onEnter != null)
				onEnter.Invoke(this, cursor as CursorBase);
		}

		private void RaiseExit(ICursor cursor) {
			if (onExit != null)
				onExit.Invoke(this, cursor as CursorBase);
		}

		private void RaisePress(ICursor cursor) {
			if (onPress != null)
				onPress.Invoke(this, cursor as CursorBase);
		}

		private void RaiseRelease(ICursor cursor) {
			if (onRelease != null)
				onRelease.Invoke(this, cursor as CursorBase);
		}

		private void RaisePressure(ICursor cursor, float value) {
			if (onPressure != null)
				onPressure.Invoke(this, cursor as CursorBase, value);
		}
	}
}
