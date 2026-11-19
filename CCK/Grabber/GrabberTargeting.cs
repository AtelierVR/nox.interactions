using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	/// <summary>
	/// Targeting of every grabber, shared by the components and by anything that implements <see cref="IGrabber"/> itself
	/// (a hand that carries its grabber in its own class, as AutoHand does).
	/// <para>
	/// An overlap around the grab point gathers the candidates, a ray from the grab point to the colliders of each of them
	/// finds the contact, and the one closest to the aim axis (forward blended towards right) wins.
	/// </para>
	/// </summary>
	public static class GrabberTargeting {
		public const int BufferSize = 32;

		public static bool TryFind(
			IGrabber grabber, Vector3 origin, Vector3 forward, Vector3 right, float reach, LayerMask layers,
			Collider[] buffer, out IGrabbable grabbable, out RaycastHit contact
		) {
			grabbable = null;
			contact = default;

			if (grabber == null || buffer == null || reach <= 0f)
				return false;

			var aim = Vector3.Lerp(forward, right, 0.65f);

			var count = Physics.OverlapSphereNonAlloc(origin, reach, buffer, layers, QueryTriggerInteraction.Ignore);
			var bestScore = float.MaxValue;

			for (var i = 0; i < count; i++) {
				var candidateCollider = buffer[i];
				if (candidateCollider == null) continue;

				var candidate = candidateCollider.GetComponentInParent<IGrabbable>();
				if (candidate == null || !grabber.CanGrab(candidate))
					continue;

				if (!TryContact(candidate, candidateCollider, origin, forward, reach, out var hit))
					continue;

				var priority = candidate is IGrabPriority prioritized
					? Mathf.Max(0.01f, prioritized.PriorityWeight)
					: 1f;
				var score = (hit.distance / priority) - Vector3.Dot(aim, hit.point - origin) * reach * 0.5f;
				if (score >= bestScore)
					continue;

				bestScore = score;
				grabbable = candidate;
				contact = hit;
			}

			return grabbable != null;
		}

		/// <summary>
		/// Contact of a candidate: the ray is aimed at the closest point of each of its own colliders, so the hit always
		/// belongs to the object the grabber takes (AutoHand resolves the grabbable from the object it hit). Casting into
		/// the world would return the first collider in the way, and the hold would be built on a point of something else:
		/// an object held by a point it does not own is pulled and turned by the joint.
		/// </summary>
		private static bool TryContact(
			IGrabbable candidate, Collider fallback, Vector3 origin, Vector3 forward, float reach, out RaycastHit contact
		) {
			contact = default;

			var best = reach * 2f;
			var found = false;

			var colliders = (candidate as IGrabColliders)?.GrabColliders;

			if (colliders != null && colliders.Count > 0) {
				for (var i = 0; i < colliders.Count; i++) {
					if (!CastTo(colliders[i], origin, forward, ref best, out var hit)) continue;

					contact = hit;
					found = true;
				}

				return found;
			}

			if (!CastTo(fallback, origin, forward, ref best, out var single))
				return false;

			contact = single;
			return true;
		}

		/// <summary>
		/// Ray of one collider, aimed at its closest point to the grab point. A grab point sitting on the surface makes the
		/// ray start inside the collider (which then ignores it), so the origin is nudged toward the centre, as AutoHand
		/// does. A collider behind the grab point is not something the palm is aimed at (AutoHand's 120 degree cone).
		/// </summary>
		private static bool CastTo(Collider collider, Vector3 origin, Vector3 forward, ref float best, out RaycastHit hit) {
			hit = default;

			if (!collider)
				return false;

			var direction = collider.ClosestPoint(origin) - origin;
			if (direction.sqrMagnitude < InteractionConstants.ContactEpsilon)
				direction = forward;

			direction.Normalize();

			if (Vector3.Dot(direction, forward) < -0.5f)
				return false;

			var ray = new Ray(origin, direction);

			if (!collider.Raycast(ray, out hit, best)) {
				ray.origin = Vector3.MoveTowards(origin, collider.bounds.center, 0.002f);

				if (!collider.Raycast(ray, out hit, best))
					return false;
			}

			best = hit.distance;
			return true;
		}
	}
}
