using UnityEngine;

namespace Nox.Interactions {
	public interface IMotion {
		void Track(Vector3 position, Quaternion rotation, float time);

		void Clear();

		bool TryGet(out Vector3 linear, out Vector3 angular);
	}
}
