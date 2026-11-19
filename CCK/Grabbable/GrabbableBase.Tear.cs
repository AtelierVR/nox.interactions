using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private static readonly List<Joint> foundJoints = new();

		private bool tearing;

		private Vector3 tearFrom;

		/// <summary>Whether the object is joined to something else and can be torn off it.</summary>
		public bool CanTear
			=> tear.enabled
				&& state == GrabState.Held
				&& Attachments().Count > 0;

		public void Squeeze(IGrabber grabber) {
			if (state != GrabState.Held)
				return;

			RaiseSqueezed(grabber, true);
		}

		public void Unsqueeze(IGrabber grabber)
			=> RaiseSqueezed(grabber, false);

		/// <summary>
		/// Breaks the joints that attach the object to something else, so the object can be taken: the joints are removed
		/// and the hold stays, which is what taking a door or a lever off its frame means.
		/// </summary>
		public bool TryTear(IGrabber grabber) {
			if (tearing || state != GrabState.Held)
				return false;

			var joints = Attachments();

			if (joints.Count == 0)
				return false;

			tearing = true;

			for (var i = 0; i < joints.Count; i++)
				joints[i].Destroy();

			foundJoints.Clear();
			tearing = false;

			RaiseTorn(grabber);
			return true;
		}

		/// <summary>
		/// Tears the object off when it has been pulled away from where it was grabbed: the gesture of ripping a joined
		/// prop off its frame, the automatic side of <see cref="TryTear"/>.
		/// </summary>
		private void CheckTear(IGrabber grabber) {
			if (!tear.enabled || tear.distance <= 0f)
				return;

			if ((transform.position - tearFrom).sqrMagnitude <= tear.distance * tear.distance)
				return;

			TryTear(grabber);
		}

		private List<Joint> Attachments() {
			foundJoints.Clear();
			GetComponents(foundJoints);

			return foundJoints;
		}
	}
}
