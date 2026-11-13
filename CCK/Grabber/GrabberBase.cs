using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[DefaultExecutionOrder(10)]
	public partial class GrabberBase : MonoBehaviour, IGrabber, IGrabberTargeting {
		[SerializeField] private Transform grabPoint;
		[SerializeField] private EntityRig rig = EntityRig.None;
		[SerializeField] private GrabSource source = GrabSource.Direct;
		[SerializeField] private GrabKind kind = GrabKind.Direct;
		[SerializeField] private GrabRay ray = GrabRay.Default;
		[SerializeField] private GrabHighlight highlight = GrabHighlight.Default;

		private IGrabbable held;
		private IGrabbable target;
		private RaycastHit hit;
		private GrabState state = GrabState.Free;

		public Transform GrabPoint => grabPoint != null ? grabPoint : transform;

		public EntityRig Rig => rig;

		public GrabSource Source => source;

		public GrabKind Kind => kind;

		public GrabState State => state;

		public IGrabbable HeldGrabbable => held;

		public IGrabbable CurrentTarget => target;

		public RaycastHit Contact => hit;

		public Vector3 HoldingPoint => GrabPoint.position + GrabPoint.forward * ray.offset;

		public GrabRay Ray {
			get => ray;
			set => ray = value;
		}

		public GrabHighlight Highlight {
			get => highlight;
			set => highlight = value;
		}

		public float ReachDistance {
			get => ray.reach;
			set => ray.reach = value;
		}

		public LayerMask TargetLayers {
			get => ray.layers;
			set => ray.layers = value;
		}

		public bool HighlightTargets {
			get => highlight.enabled;
			set => highlight.enabled = value;
		}

		public Material HighlightMaterial {
			get => highlight.material;
			set => highlight.material = value;
		}

		protected virtual void Awake() {
			if (ray.layers == 0)
				ray.layers = InteractionConstants.GrabbableMask;
		}

		public void SetRig(EntityRig value) => rig = value;

		public void SetKind(GrabKind value) => kind = value;

		public void SetHoldingOffset(float offset) => ray.offset = offset;

		public void SetGrabPoint(Transform point) => grabPoint = point;
	}
}
