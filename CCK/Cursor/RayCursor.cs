using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public class RayCursor : CursorBase, IRayCursor {
		[SerializeField] private PointerMode mode = PointerMode.Far;
		[SerializeField] private float length = 5f;
		[SerializeField] private float radius = 0.08f;

		private readonly Collider[] buffer = new Collider[16];

		public PointerMode Mode {
			get => mode;
			set => mode = value;
		}

		public float Length {
			get => length;
			set => length = value;
		}

		public float Radius {
			get => radius;
			set => radius = value;
		}

		private void Update() {
			if (!TryCast(out var contact)) {
				SetHit(default);
				UpdateTarget(null, default);
				return;
			}

			SetHit(contact);
			UpdateTarget(FindTarget(contact), contact);
		}

		public override bool TryCast(out RaycastHit contact) {
			var source = Origin;

			if (mode == PointerMode.Far)
				return Physics.Raycast(source.position, source.forward, out contact, length, Layers, QueryTriggerInteraction.Ignore);

			contact = default;
			var count = Physics.OverlapSphereNonAlloc(source.position, radius, buffer, Layers, QueryTriggerInteraction.Ignore);
			var best = float.MaxValue;

			for (var i = 0; i < count; i++) {
				if (buffer[i] == null) continue;

				var direction = buffer[i].ClosestPoint(source.position) - source.position;
				if (direction.sqrMagnitude < InteractionConstants.ContactEpsilon)
					direction = source.forward;

				if (!Physics.Raycast(source.position, direction.normalized, out var near, radius * 2f, Layers, QueryTriggerInteraction.Ignore))
					continue;

				if (near.distance >= best) continue;

				best = near.distance;
				contact = near;
			}

			return contact.collider != null;
		}

		public bool TryTarget(IInteractTarget value, in RaycastHit contact) {
			UpdateTarget(value, contact);
			return Target == value;
		}

		public void ClearTarget(IInteractTarget value) {
			if (Target == value)
				UpdateTarget(null, default);
		}
	}
}
