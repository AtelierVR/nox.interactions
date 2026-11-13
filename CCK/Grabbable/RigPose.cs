using System;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[Serializable]
	public struct RigPose {
		public EntityRig rig;
		public Vector3 position;
		public Quaternion rotation;

		public RigPose(EntityRig rig, Vector3 position, Quaternion rotation) {
			this.rig = rig;
			this.position = position;
			this.rotation = rotation;
		}

		public bool Matches(EntityRig value)
			=> rig != EntityRig.None && (rig & value) != 0;

		public bool IsExact(EntityRig value)
			=> rig == value;
	}
}
