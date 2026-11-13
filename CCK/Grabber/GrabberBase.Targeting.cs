using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabberBase {
		private readonly Collider[] overlapBuffer = new Collider[32];

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
			var origin = point.position;
			var forward = point.forward;
			var aim = Vector3.Lerp(forward, point.right, 0.65f);

			var count = Physics.OverlapSphereNonAlloc(
				origin,
				ray.reach,
				overlapBuffer,
				ray.layers,
				QueryTriggerInteraction.Ignore
			);
			var bestScore = float.MaxValue;

			for (var i = 0; i < count; i++) {
				var candidateCollider = overlapBuffer[i];
				if (candidateCollider == null) continue;

				var candidate = candidateCollider.GetComponentInParent<IGrabbable>();
				if (candidate == null || !candidate.CanBeGrabbedBy(this))
					continue;

				if (!RaycastCandidate(origin, forward, candidateCollider, out var hit))
					continue;

				var score = ScoreCandidate(origin, aim, hit);
				if (score >= bestScore)
					continue;

				bestScore = score;
				grabbable = candidate;
				contact = hit;
			}

			return grabbable != null;
		}

		private bool RaycastCandidate(Vector3 origin, Vector3 forward, Collider candidateCollider, out RaycastHit hit) {
			var direction = candidateCollider.ClosestPoint(origin) - origin;
			if (direction.sqrMagnitude < InteractionConstants.ContactEpsilon)
				direction = forward;

			return Physics.Raycast(
				origin,
				direction.normalized,
				out hit,
				ray.reach * 2f,
				ray.layers,
				QueryTriggerInteraction.Ignore
			);
		}

		private float ScoreCandidate(Vector3 origin, Vector3 aim, in RaycastHit hit)
			=> hit.distance - Vector3.Dot(aim, hit.point - origin) * ray.reach;
	}
}
