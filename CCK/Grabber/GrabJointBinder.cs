using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public static class GrabJointBinder {
		public static ConfigurableJoint Bind(GrabberBase grabber, GrabbableBase grabbable, Vector3 worldAnchor, float breakForce, bool rigid = false) {
			if (grabber == null || grabbable == null || grabbable.Body == null)
				return null;

			var joint = grabber.gameObject.AddComponent<ConfigurableJoint>();

			joint.autoConfigureConnectedAnchor = false;
			joint.anchor = grabber.transform.InverseTransformPoint(worldAnchor);
			joint.connectedBody = grabbable.Body;
			joint.connectedAnchor = grabbable.Body.transform.InverseTransformPoint(worldAnchor);
			joint.breakForce = breakForce;
			joint.breakTorque = float.PositiveInfinity;

			joint.xMotion = ConfigurableJointMotion.Locked;
			joint.yMotion = ConfigurableJointMotion.Locked;
			joint.zMotion = ConfigurableJointMotion.Locked;
			joint.angularXMotion = rigid ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
			joint.angularYMotion = rigid ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
			joint.angularZMotion = rigid ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;

			return joint;
		}

		public static ConfigurableJoint Bind(GrabberBase grabber, GrabbableBase grabbable, Vector3 worldAnchor)
			=> Bind(grabber, grabbable, worldAnchor, InteractionConstants.DefaultJointBreakForce);

		public static void Unbind(ConfigurableJoint joint) {
			if (joint == null) return;
			joint.Destroy();
		}
	}
}
