using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public class FingerConformer : MonoBehaviour, IGrabConformer {
		[SerializeField] private Transform[] joints = new Transform[3];
		[SerializeField] private Transform tip;
		[SerializeField] private float tipRadius = InteractionConstants.DefaultTipRadius;
		[SerializeField]
		private Vector3[] closedEuler = new[] {
			new Vector3(70f, 0f, 0f),
			new Vector3(80f, 0f, 0f),
			new Vector3(55f, 0f, 0f)
		};

		private readonly Collider[] results = new Collider[4];

		private Quaternion[] openPose;
		private Quaternion[] closedPose;
		private float bend;

		public float Bend => bend;

		private void Awake() {
			var count = joints != null ? joints.Length : 0;

			openPose = new Quaternion[count];
			closedPose = new Quaternion[count];

			for (var i = 0; i < count; i++) {
				if (joints[i] == null) continue;

				openPose[i] = joints[i].localRotation;

				var euler = i < closedEuler.Length ? closedEuler[i] : Vector3.zero;
				closedPose[i] = Quaternion.Euler(euler);
			}
		}

		public void Conform(IGrabbable grabbable, in RaycastHit contact, int steps) {
			if (grabbable == null || tip == null) return;

			int mask = InteractionConstants.ToMask(grabbable.Target.gameObject.layer);
			bend = Sweep(mask, steps);
			ApplyBend(bend);
		}

		public void Relax(float duration) {
			var speed = duration > 0f ? Time.deltaTime / duration : 1f;
			bend = Mathf.MoveTowards(bend, 0f, speed);
			ApplyBend(bend);
		}

		private float Sweep(int layerMask, int steps) {
			steps = Mathf.Max(steps, 4);

			var coarse = 5f / steps;
			var hit = -1f;

			for (var value = 0f; value <= 1f; value += coarse) {
				if (!Overlaps(layerMask, value)) continue;
				hit = value;
				break;
			}

			if (hit < 0f) return 1f;
			if (hit <= 0f) return 0f;

			var from = Mathf.Max(0f, hit - coarse);
			var fine = 1f / steps;

			for (var value = from; value <= hit; value += fine) {
				if (Overlaps(layerMask, value)) return value;
			}

			return hit;
		}

		private bool Overlaps(int layerMask, float value) {
			ApplyBend(value);
			return Physics.OverlapSphereNonAlloc(tip.position, tipRadius, results, layerMask, QueryTriggerInteraction.Ignore) > 0;
		}

		private void ApplyBend(float value) {
			if (joints == null) return;

			for (var i = 0; i < joints.Length; i++) {
				if (joints[i] == null) continue;
				joints[i].localRotation = Quaternion.Slerp(openPose[i], closedPose[i], value);
			}
		}
	}
}
