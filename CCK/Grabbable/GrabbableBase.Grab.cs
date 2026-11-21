using System.Collections.Generic;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		public bool TryAddGrabber(IGrabber grabber, in RaycastHit contact, float duration)
			=> AddGrabber(grabber, contact.point, contact.collider, duration);

		public bool TryAddGrabberAt(IGrabber grabber, Vector3 point, float duration)
			=> AddGrabber(grabber, point, null, duration);

		private bool AddGrabber(IGrabber grabber, Vector3 point, Collider surface, float duration) {
			// a grabber destroyed while it held the object never released it: drop it before deciding this is a first grab
			PruneGrabbers();

			if (!CanBeGrabbedBy(grabber))
				return false;

			// swapping: a single grabber object that allows it hands itself over to the new grabber instead of refusing
			if (grab.single && grab.allowSwap && grabbers.Count > 0)
				RemoveAllGrabbers(0f);

			// oldest first: the grabber that took the object keeps driving it. Another hand holds the same object without
			// pulling it anywhere, so both hands stay on what they grabbed
			grabbers.Add(grabber);
			state = GrabState.Held;
			motion.Clear();

			IgnoreBody(grabber, true);
			WatchBody(grabber);

			if (grabbers.Count == 1) {
				CaptureOffset(grabber, point, surface);
				BeginHold(grab.instant ? 0f : duration, grabber);
				tearFrom = transform.position;
			}
			else {
				// the object is taken where it is, and every hand is seated on it: each grip is captured against the palm of
				// its grabber, so the followers bring the hands against the object (AutoHand's AutoPose) while the object
				// itself does not move. A hand that already rests on it gets no shift and is not displaced.
				for (var i = 0; i < grabbers.Count; i++)
					SetHold(
						grabbers[i],
						transform.position,
						transform.rotation,
						true,
						SeatShift(grabbers[i])
					);
			}

			RaiseGrabbed(grabber);

			return true;
		}

		private const float SIMULTANEOUS_RELEASE_WINDOW = 0.15f;
		private float _lastSharedReleaseTime = -1f;

		public bool TryRemoveGrabber(IGrabber grabber, float releaseMultiplier) {
			if (grabber == null)
				return false;

			var wasShared = grabbers.Count > 1;

			if (!grabbers.Remove(grabber))
				return false;

			RemoveHold(grabber);

			if (grabber is IGrabberOffset offset)
				offset.ClearOffset();

			IgnoreBody(grabber, false);
			UnwatchBody(grabber);
			RaiseReleased(grabber, releaseMultiplier);

			if (wasShared)
				_lastSharedReleaseTime = Time.time;

			if (grabbers.Count > 0) {
				RebaseOffset(grabbers[0]);
				return true;
			}

			var simultaneous = wasShared
				|| (Time.time - _lastSharedReleaseTime <= SIMULTANEOUS_RELEASE_WINDOW);

			state = GrabState.Free;
			ClearOffset();
			EndHold();

			if (!simultaneous)
				Throw(releaseMultiplier);
			return true;
		}

		public void RemoveAllGrabbers(float releaseMultiplier) {
			for (var i = grabbers.Count - 1; i >= 0; i--)
				TryRemoveGrabber(grabbers[i], releaseMultiplier);
		}

		public void ApplyThrow(Vector3 linear, Vector3 angular, float multiplier) {
			if (body == null || body.isKinematic || grabbers.Count > 0)
				return;

			body.linearVelocity = linear * multiplier;

			if (!float.IsNaN(angular.x) && !float.IsNaN(angular.y) && !float.IsNaN(angular.z))
				body.angularVelocity = angular;
		}

		private void Throw(float multiplier) {
			if (multiplier == 0f || release.throwPower <= 0f)
				return;

			if (!motion.TryGet(out var linear, out var angular))
				return;

			ApplyThrow(linear, angular, multiplier * release.throwPower);
		}

		private void IgnoreBody(IGrabber grabber, bool ignore) {
			if (grabber is not IGrabberBody holder)
				return;

			var colliders = holder.BodyColliders;
			if (colliders == null)
				return;

			var ownCount = grabColliders.Count;

			for (var i = 0; i < ownCount; i++) {
				var own = grabColliders[i];
				if (own == null) continue;

				for (var j = 0; j < colliders.Count; j++) {
					var other = colliders[j];
					if (other == null) continue;
					Physics.IgnoreCollision(own, other, ignore);
				}
			}
		}

		private readonly List<IGrabberBody> bodies = new();
		private readonly List<int> bodySignatures = new();

		private const float IgnoreRefreshInterval = 0.5f;

		private float ignoreRefreshAt;

		private void WatchBody(IGrabber grabber) {
			if (grabber is not IGrabberBody holder)
				return;

			bodies.Add(holder);
			bodySignatures.Add(SignatureOf(holder));
			RefreshExclusion();
		}

		private void UnwatchBody(IGrabber grabber) {
			for (var i = bodies.Count - 1; i >= 0; i--) {
				if (!ReferenceEquals(bodies[i], grabber)) continue;
				bodies.RemoveAt(i);
				bodySignatures.RemoveAt(i);
			}

			RefreshExclusion();
		}

		private readonly List<int> cachedExcludeLayers = new();

		private int appliedExcludeMask = -1;

		/// <summary>
		/// Excludes the layers of the body carrying the object from its own colliders, so the hand can never push what it
		/// holds. The ignore pairs are lost whenever the hand rebuilds a collider (shape or material), while these layer
		/// overrides belong to the collider and survive it.
		/// </summary>
		private void RefreshExclusion() {
			var mask = 0;

			for (var i = 0; i < bodies.Count; i++) {
				var holder = bodies[i];
				if (holder == null || (holder is Object view && !view)) continue;

				var colliders = holder.BodyColliders;
				if (colliders == null) continue;

				for (var j = 0; j < colliders.Count; j++) {
					var collider = colliders[j];
					if (collider) mask |= 1 << collider.gameObject.layer;
				}
			}

			if (mask == appliedExcludeMask)
				return;

			appliedExcludeMask = mask;

			if (cachedExcludeLayers.Count != grabColliders.Count) {
				cachedExcludeLayers.Clear();

				for (var i = 0; i < grabColliders.Count; i++) {
					var own = grabColliders[i];
					cachedExcludeLayers.Add(own ? own.excludeLayers.value : 0);
				}
			}

			for (var i = 0; i < grabColliders.Count; i++) {
				var own = grabColliders[i];
				if (!own) continue;

				own.excludeLayers = cachedExcludeLayers[i] | mask;
			}

			if (mask != 0)
				return;

			cachedExcludeLayers.Clear();
		}

		/// <summary>
		/// Re-applies the ignores of the bodies that hold this object. A physical hand reshapes its palm and finger colliders
		/// after the grab (and every time the avatar is re-fitted), and reshaping a collider drops its ignore pairs: a collider
		/// the hold never ignored then pushes the dynamic hand away from what it carries, and the hand ends up circling
		/// around the object it holds.
		/// </summary>
		public void RefreshIgnores()
			=> RefreshBodies(true);

		private void RefreshBodies(bool force = false) {
			var refresh = force || Time.unscaledTime >= ignoreRefreshAt;

			if (refresh)
				ignoreRefreshAt = Time.unscaledTime + IgnoreRefreshInterval;

			for (var i = 0; i < bodies.Count; i++) {
				var holder = bodies[i];
				if (holder == null || (holder is Object view && !view)) continue;

				var signature = SignatureOf(holder);
				if (!refresh && signature == bodySignatures[i]) continue;

				bodySignatures[i] = signature;
				IgnoreBody(holder as IGrabber, true);
			}
		}

		private static int SignatureOf(IGrabberBody holder) {
			var colliders = holder.BodyColliders;
			if (colliders == null) return 0;

			var signature = colliders.Count;

			for (var i = 0; i < colliders.Count; i++) {
				var collider = colliders[i];

				if (!collider || !collider.enabled)
					signature *= 31;
				else
					signature = signature * 31 + collider.GetEntityId().GetHashCode();
			}

			return signature;
		}

		private static PhysicsMaterial heldMaterial;

		private static PhysicsMaterial HeldMaterial {
			get {
				if (heldMaterial != null)
					return heldMaterial;

				heldMaterial = new PhysicsMaterial("NoxHeldNoFriction") {
					dynamicFriction = 0f,
					staticFriction = 0f,
					bounciness = 0f,
					frictionCombine = PhysicsMaterialCombine.Minimum,
					bounceCombine = PhysicsMaterialCombine.Minimum
				};

				return heldMaterial;
			}
		}

		private readonly List<PhysicsMaterial> cachedMaterials = new();

		private bool materialsCached;

		private void ApplyHeldMaterial() {
			if (materialsCached || !held.noFriction)
				return;

			cachedMaterials.Clear();

			for (var i = 0; i < grabColliders.Count; i++) {
				var collider = grabColliders[i];
				cachedMaterials.Add(collider != null ? collider.sharedMaterial : null);

				if (collider != null)
					collider.sharedMaterial = HeldMaterial;
			}

			materialsCached = true;
		}

		private void RestoreHeldMaterial() {
			if (!materialsCached)
				return;

			materialsCached = false;

			for (var i = 0; i < grabColliders.Count && i < cachedMaterials.Count; i++) {
				var collider = grabColliders[i];
				if (collider != null)
					collider.sharedMaterial = cachedMaterials[i];
			}

			cachedMaterials.Clear();
		}
	}
}
