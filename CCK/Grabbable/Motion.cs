using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[Serializable]
	public class Motion {
		private struct Sample {
			public float time;
			public Vector3 position;
			public Quaternion rotation;
		}

		private readonly List<Sample> samples = new();

		public float memory = 0.3f;
		public float span = 0.1f;
		public float grace = 0.15f;
		public float minMove = 0.002f;
		public float maxSpeed = 20f;

		public int Count
			=> samples.Count;

		public void Track(Vector3 position, Quaternion rotation, float time) {
			samples.Add(new Sample {
				time = time,
				position = position,
				rotation = rotation
			});
			Prune(time);
		}

		public void Clear()
			=> samples.Clear();

		public bool TryGet(out Vector3 linear, out Vector3 angular) {
			linear = Vector3.zero;
			angular = Vector3.zero;

			if (samples.Count < 2)
				return false;

			var last = samples.Count - 1;
			var first = IndexAt(samples[last].time - span);

			if (first >= last || Vector3.Distance(samples[first].position, samples[last].position) < minMove)
				first = IndexAt(samples[last].time - span - grace);

			if (first >= last)
				first = 0;

			if (first >= last)
				return false;

			var delta = samples[last].time - samples[first].time;
			if (delta <= 0.0001f)
				return false;

			linear = Vector3.ClampMagnitude((samples[last].position - samples[first].position) / delta, maxSpeed);
			angular = Angular(samples[last].rotation * Quaternion.Inverse(samples[first].rotation), delta);
			return true;
		}

		private int IndexAt(float time) {
			if (samples.Count == 0 || samples[samples.Count - 1].time < time)
				return 0;

			var low = 0;
			var high = samples.Count - 1;

			while (low < high) {
				var middle = (low + high) >> 1;

				if (samples[middle].time < time)
					low = middle + 1;
				else
					high = middle;
			}

			return low;
		}

		private void Prune(float time) {
			var limit = time - memory - span - grace;
			var remove = 0;
			var count = samples.Count;

			while (count - remove > 2 && samples[remove].time < limit)
				remove++;

			if (remove > 0)
				samples.RemoveRange(0, remove);
		}

		private static Vector3 Angular(Quaternion delta, float time) {
			delta.ToAngleAxis(out var angle, out var axis);

			if (angle > 180f)
			angle -= 360f;
			if (float.IsInfinity(axis.x))
				return Vector3.zero;

			return axis * (angle * Mathf.Deg2Rad / time);
		}
	}
}
