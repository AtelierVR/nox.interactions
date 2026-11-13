using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public abstract partial class CursorBase : MonoBehaviour, ICursor {
		[SerializeField] private Transform origin;
		[SerializeField] private LayerMask layers;
		[SerializeField] private EntityRig rig = EntityRig.None;
		[SerializeField] private bool interactTargets = true;

		private IInteractTarget target;
		private CursorState state = CursorState.Idle;
		private RaycastHit hit;
		private bool hasHit;

		public Transform Origin => origin != null ? origin : transform;

		public IInteractTarget Target => target;

		public bool InteractTargets {
			get => interactTargets;
			set => interactTargets = value;
		}

		public EntityRig Rig {
			get => rig;
			set => rig = value;
		}

		public LayerMask Layers {
			get => layers;
			set => layers = value;
		}

		public CursorState State => state;

		public Vector3 Point => hasHit ? hit.point : Origin.position;

		public Vector3 Normal => hasHit ? hit.normal : Origin.forward;

		public virtual float Pressure => 0f;

		public abstract bool TryCast(out RaycastHit contact);

		public bool TryPress(in RaycastHit contact) {
			return PressTarget(contact);
		}

		public bool TryRelease(in RaycastHit contact) {
			return ReleaseTarget(contact);
		}

		protected void SetHit(in RaycastHit contact) {
			hit = contact;
			hasHit = contact.collider != null;
		}

		protected IInteractTarget FindTarget(in RaycastHit contact) {
			if (!interactTargets || contact.collider == null) return null;

			var found = contact.collider.GetComponentInParent<IInteractTarget>();
			if (found == null || !found.CanInteract(this)) return null;

			return found;
		}

		protected void UpdateTarget(IInteractTarget next, in RaycastHit contact) {
			if (next == target) {
				if (hasHit && target is IPokeTarget poke)
					poke.OnPressure(this, contact, Pressure);

				return;
			}

			if (target != null) {
				if (state == CursorState.Press)
					ReleaseTarget(hit);

				target.OnHoverExit(this);
				RaiseExit(target);
			}

			target = next;

			if (target == null) {
				state = CursorState.Idle;
				return;
			}

			target.OnHoverEnter(this, contact);
			state = CursorState.Hover;
			RaiseEnter(target);
		}

		private bool PressTarget(in RaycastHit contact) {
			if (target == null || !target.CanInteract(this)) return false;
			if (state == CursorState.Press) return false;

			target.OnPress(this, contact);
			state = CursorState.Press;
			RaisePress(target);
			return true;
		}

		private bool ReleaseTarget(in RaycastHit contact) {
			if (target == null || state != CursorState.Press) return false;

			target.OnRelease(this, contact);
			state = CursorState.Hover;
			RaiseRelease(target);
			return true;
		}
	}
}
