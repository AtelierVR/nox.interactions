using Nox.Interactions;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.CCK.Interactions {
	[RequireComponent(typeof(Button))]
	public class UiButtonTarget : InteractTarget {
		[SerializeField] private Button button;

		public Button Button {
			get => button;
			set => button = value;
		}

		private void Reset() {
			button = GetComponent<Button>();
		}

		public override bool CanInteract(ICursor cursor) {
			if (!base.CanInteract(cursor)) return false;

			return button != null && button.IsInteractable();
		}

		public override void OnRelease(ICursor cursor, in RaycastHit contact) {
			base.OnRelease(cursor, contact);

			if (button != null)
				button.onClick.Invoke();
		}
	}
}
