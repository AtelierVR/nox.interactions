using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class InteractTarget : MonoBehaviour, IPokeTarget {
		[SerializeField] private EntityRig allowed = EntityRig.All;

		private bool pressed;

		public EntityRig Allowed {
			get => allowed;
			set => allowed = value;
		}

		public bool IsPressed => pressed;

		public virtual bool CanInteract(ICursor cursor) {
			if (!enabled || cursor == null) return false;
			if (cursor.Rig == EntityRig.None) return true;

			return (allowed & cursor.Rig) != 0;
		}

		public virtual void OnHoverEnter(ICursor cursor, in RaycastHit contact) {
			RaiseEnter(cursor);
		}

		public virtual void OnHoverExit(ICursor cursor) {
			RaiseExit(cursor);
		}

		public virtual void OnPress(ICursor cursor, in RaycastHit contact) {
			pressed = true;
			RaisePress(cursor);
		}

		public virtual void OnRelease(ICursor cursor, in RaycastHit contact) {
			pressed = false;
			RaiseRelease(cursor);
		}

		public virtual void OnPressure(ICursor cursor, in RaycastHit contact, float value) {
			RaisePressure(cursor, value);
		}
	}
}
