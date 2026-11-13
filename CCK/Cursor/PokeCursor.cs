using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public class PokeCursor : CursorBase, IPokeCursor {
		[SerializeField] private Transform tip;
		[SerializeField] private Vector3 axis = Vector3.forward;
		[SerializeField] private float radius = 0.01f;
		[SerializeField] private float depth = 0.03f;
		[SerializeField] private float pressDepth = 0.012f;
		[SerializeField] private bool grabOnPress = false;
		[SerializeField] private float grabDuration = 0.1f;
		[SerializeField] private GrabberBase hand;

		private float pressure;
		private bool armed;

		public Transform Tip => tip != null ? tip : Origin;

		public Vector3 Axis {
			get => axis;
			set => axis = value;
		}

		public float Radius {
			get => radius;
			set => radius = value;
		}

		public float Depth {
			get => depth;
			set => depth = value;
		}

		public float PressDepth {
			get => pressDepth;
			set => pressDepth = value;
		}

		public bool GrabOnPress {
			get => grabOnPress;
			set => grabOnPress = value;
		}

		public GrabberBase Hand {
			get => hand;
			set => hand = value;
		}

		public override float Pressure => pressure;

		private void Update() {
			var found = TryCast(out var contact);

			if (found) {
				SetHit(contact);
				pressure = Mathf.Clamp01(1f - contact.distance / Mathf.Max(depth, 0.0001f));
			}
			else {
				SetHit(default);
				pressure = 0f;
			}

			UpdateTarget(found ? FindTarget(contact) : null, contact);

			var pressed = found && contact.distance <= pressDepth;

			if (pressed) {
				TryPress(contact);

				if (!armed && grabOnPress && hand != null)
					Grab(contact);
			}
			else {
				TryRelease(contact);
			}

			armed = pressed;
		}

		public override bool TryCast(out RaycastHit contact) {
			return Physics.SphereCast(Tip.position, radius, Tip.TransformDirection(axis), out contact, depth, Layers, QueryTriggerInteraction.Ignore);
		}

		public bool TryPoke(out RaycastHit contact) {
			return TryCast(out contact);
		}

		private void Grab(in RaycastHit contact) {
			var grabbable = contact.collider.GetComponentInParent<IGrabbable>();
			if (grabbable == null) return;

			hand.TryGrab(grabbable, contact, grabDuration);
		}
	}
}
