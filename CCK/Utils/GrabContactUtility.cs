using UnityEngine;

namespace Nox.CCK.Interactions {
	public static class GrabContactUtility {
		public static bool HasSurface(in RaycastHit contact)
			=> contact.collider != null;

		public static Vector3 GetPoint(in RaycastHit contact, Vector3 fallback)
			=> contact.collider != null
				? contact.point
				: fallback;

		public static Vector3 GetNormal(in RaycastHit contact, Vector3 fallback)
			=> contact.collider != null
				? contact.normal
				: fallback;
	}
}
