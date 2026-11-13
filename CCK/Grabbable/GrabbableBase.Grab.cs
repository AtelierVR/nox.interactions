using System.Collections.Generic;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private float cachedDrag;
		private float cachedAngularDrag;
		private float cachedMass;
		private bool bodyCached;

		public bool TryAddGrabber(IGrabber grabber, in RaycastHit contact, float duration) {
			if (!CanBeGrabbedBy(grabber))
				return false;

			grabbers.Add(grabber);
			state = GrabState.Held;
			Motion.Clear();

			if (body != null)
				ApplyBody();
			IgnoreBody(grabber, true);

			if (grabbers.Count == 1) {
				CaptureOffset(grabber, grabber.Rig, contact);
				BeginHold(duration, grabber);
			}

			RaiseGrabbed(grabber);
			return true;
		}

		public bool TryRemoveGrabber(IGrabber grabber, float releaseMultiplier) {
			if (grabber == null || !grabbers.Remove(grabber))
				return false;

			IgnoreBody(grabber, false);
			RaiseReleased(grabber, releaseMultiplier);

			if (grabbers.Count > 0) {
				CaptureOffset(grabbers[0], grabbers[0].Rig, default);
				return true;
			}

			state = GrabState.Free;
			ClearOffset();

			EndHold();

			if (body != null)
				RestoreBody();

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

		private void CacheBody() {
			if (body == null) return;

			cachedDrag = body.linearDamping;
			cachedAngularDrag = body.angularDamping;
			cachedMass = body.mass;
			bodyCached = true;
		}

		private void ApplyBody() {
			CacheBody();
			body.linearDamping = Mathf.Max(body.linearDamping, limits.drag);
			body.mass = Mathf.Max(body.mass, limits.mass);
		}

		private void RestoreBody() {
			if (!bodyCached) return;

			body.linearDamping = cachedDrag;
			body.angularDamping = cachedAngularDrag;
			body.mass = cachedMass;
			bodyCached = false;
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
			if (materialsCached)
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

		private void IgnoreBody(IGrabber grabber, bool ignore) {
			if (grabber is not IGrabberBody holder) return;

			var colliders = holder.BodyColliders;
			if (colliders == null) return;

			var count = colliders.Count;
			var ownCount = grabColliders.Count;

			for (var i = 0; i < ownCount; i++) {
				var own = grabColliders[i];
				if (own == null) continue;

				for (var j = 0; j < count; j++) {
					var other = colliders[j];
					if (other == null) continue;
					Physics.IgnoreCollision(own, other, ignore);
				}
			}
		}
	}
}
