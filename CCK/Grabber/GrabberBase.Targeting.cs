using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabberBase {
		private readonly Collider[] overlapBuffer = new Collider[GrabberTargeting.BufferSize];

		public virtual void SetTarget(IGrabbable grabbable, in RaycastHit contact) {
			if (target == grabbable) {
				hit = contact;
				return;
			}

			ClearTarget(target);

			target = grabbable;
			hit = contact;

			if (highlight.enabled && grabbable is IGrabHighlight highlightTarget)
				highlightTarget.SetHighlight(this, true, highlight.material);

			RaiseTargetChanged(target);
		}

		public virtual void ClearTarget(IGrabbable grabbable) {
			if (grabbable == null || target != grabbable)
				return;

			if (grabbable is IGrabHighlight highlightTarget)
				highlightTarget.SetHighlight(this, false, highlight.material);

			target = null;
			hit = default;
			RaiseTargetChanged(null);
		}

		public virtual bool TryFindTarget(out IGrabbable grabbable, out RaycastHit contact) {
			grabbable = null;
			contact = default;

			if (held != null)
				return false;

			var point = GrabPoint;
			var mask = ray.layers & ~ray.ignoreLayers;

			return GrabberTargeting.TryFind(
				this,
				point.position,
				point.forward,
				point.right,
				ray.reach,
				mask,
				overlapBuffer,
				out grabbable,
				out contact
			);
		}
	}
}
