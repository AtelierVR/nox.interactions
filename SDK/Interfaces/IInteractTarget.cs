using UnityEngine;

namespace Nox.Interactions {
	public interface IInteractTarget {
		bool CanInteract(ICursor cursor);

		void OnHoverEnter(ICursor cursor, in RaycastHit contact);

		void OnHoverExit(ICursor cursor);

		void OnPress(ICursor cursor, in RaycastHit contact);

		void OnRelease(ICursor cursor, in RaycastHit contact);
	}

	public interface IPokeTarget : IInteractTarget {
		void OnPressure(ICursor cursor, in RaycastHit contact, float pressure);
	}
}
