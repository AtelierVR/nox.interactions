using System.Collections.Generic;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[CreateAssetMenu(menuName = "Nox/Interactions/Grab Pose", fileName = "GrabPose")]
	public class GrabbablePose : ScriptableObject, IGrabPoseProvider {
		[SerializeField] private List<RigPose> poses = new();

		public IReadOnlyList<RigPose> Poses
			=> poses;

		public bool HasPose(EntityRig rig)
			=> Find(rig) >= 0;

		public bool TryGetPose(EntityRig rig, out Vector3 position, out Quaternion rotation) {
			var index = Find(rig);

			if (index < 0) {
				position = Vector3.zero;
				rotation = Quaternion.identity;
				return false;
			}

			position = poses[index].position;
			rotation = poses[index].rotation;
			return true;
		}

		public void SavePose(EntityRig rig, Transform target, Transform point) {
			if (rig == EntityRig.None || target == null || point == null)
				return;

			var pose = new RigPose(
				rig,
				target.InverseTransformPoint(point.position),
				Quaternion.Inverse(target.rotation) * point.rotation
			);

			var index = FindExact(rig);

			if (index < 0)
				poses.Add(pose);
			else
				poses[index] = pose;
		}

		public void ClearPose(EntityRig rig) {
			for (var i = poses.Count - 1; i >= 0; i--) {
				if (!poses[i].Matches(rig))
					continue;
				poses.RemoveAt(i);
			}
		}

		private int Find(EntityRig rig) {
			var overlapping = -1;

			for (var i = 0; i < poses.Count; i++) {
				if (poses[i].IsExact(rig))
					return i;
				if (overlapping < 0 && poses[i].Matches(rig))
					overlapping = i;
			}

			return overlapping;
		}

		private int FindExact(EntityRig rig) {
			for (var i = 0; i < poses.Count; i++)
				if (poses[i].IsExact(rig))
					return i;

			return -1;
		}
	}
}
