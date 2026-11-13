using UnityEngine;

namespace Nox.Interactions {
	public interface IGrabConformer {
		void Conform(IGrabbable grabbable, in RaycastHit contact, int steps);

		void Relax(float duration);
	}
}
