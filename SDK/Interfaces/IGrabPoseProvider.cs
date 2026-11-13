using UnityEngine;

namespace Nox.Interactions {
	public interface IGrabPoseProvider {
		bool TryGetPose(EntityRig rig, out Vector3 position, out Quaternion rotation);
	}
}
