using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[DefaultExecutionOrder(-100)]
	[RequireComponent(typeof(Rigidbody))]
	public partial class GrabbableBase : MonoBehaviour, IGrabbable, IGrabGroup, IGrabBody, IGrabColliders,
		IGrabAccess, IGrabAttachment, IGrabHold, IGrabThrow, IGrabHighlight, IGrabPoseProvider, IGrabApproach {
		[SerializeField] private Rigidbody body;
		[SerializeField] private Transform grabAnchor;
		[SerializeField] private Material highlightMaterial;
		[SerializeField] private GrabbablePose grabPose;
		[SerializeField] private List<Collider> grabColliders = new();

		[SerializeField] private EntityRig allowed = EntityRig.All;
		[SerializeField] private GrabMode mode = GrabMode.Free;
		[SerializeField] private GrabPoseType poseType = GrabPoseType.Freeform;
		[SerializeField] private GrabType grabType = GrabType.Default;
		[SerializeField] private bool instantGrab;
		[SerializeField] private GrabRelease release = GrabRelease.Default;
		[SerializeField] private GrabHold hold = GrabHold.Default;
		[SerializeField] private GrabLimits limits = GrabLimits.Default;
		[SerializeField] private bool singleGrabberOnly = false;

		private readonly List<IGrabber> grabbers = new();

		private static readonly List<Collider> foundColliders = new();

		private GrabState state = GrabState.Free;

		public Transform Target => transform;

		public Rigidbody Body => body;

		public GrabState State => state;

		public bool IsPhysicsHold
			=> hold.physicsMass > 0f && body != null && cachedMass >= hold.physicsMass;

		public bool PhysicsHeld
			=> physicsHeld;

		public int HeldCount => grabbers.Count;

		public IReadOnlyList<Collider> GrabColliders => grabColliders;

		public IReadOnlyList<IGrabber> Grabbers => grabbers;

		public Transform GrabAnchor => grabAnchor != null ? grabAnchor : transform;

		public GrabbablePose PoseAsset {
			get => grabPose;
			set => grabPose = value;
		}

		public EntityRig Allowed {
			get => allowed;
			set => allowed = value;
		}

		public GrabMode Mode {
			get => mode;
			set => mode = value;
		}

		public GrabPoseType PoseType {
			get => poseType;
			set => poseType = value;
		}

		public GrabType GrabType {
			get => grabType;
			set => grabType = value;
		}

		public bool InstantGrab {
			get => instantGrab;
			set => instantGrab = value;
		}

		public float ThrowMultiplier {
			get => release.multiplier;
			set => release.multiplier = value;
		}

		public GrabHold Hold {
			get => hold;
			set => hold = value;
		}

		public GrabRelease Release {
			get => release;
			set => release = value;
		}

		public GrabLimits Limits {
			get => limits;
			set => limits = value;
		}

		public bool SingleGrabberOnly {
			get => singleGrabberOnly;
			set => singleGrabberOnly = value;
		}

		protected virtual void Reset() {
			body = GetComponent<Rigidbody>();
			CollectColliders(true);

			var layer = InteractionConstants.GrabbableLayer;
			if (layer >= 0 && gameObject.layer == 0)
				gameObject.layer = layer;
		}

		protected virtual void Awake() {
			if (body == null)
				body = GetComponent<Rigidbody>();
			if (grabColliders.Count == 0)
				CollectColliders(true);
			CacheBody();

			follower = gameObject.GetOrAddComponent<HoldFollower>();
			follower.Target = this;
			follower.enabled = false;
		}

		protected virtual void OnDisable() {
			RemoveAllGrabbers(0f);
			ClearHighlights();
		}

		public void CollectColliders(bool includeInactive) {
			grabColliders.Clear();
			foundColliders.Clear();

			GetComponentsInChildren(includeInactive, foundColliders);

			for (var i = 0; i < foundColliders.Count; i++) {
				var collider = foundColliders[i];
				if (collider == null || collider.isTrigger) continue;
				grabColliders.Add(collider);
			}

			foundColliders.Clear();
		}

		public bool CanBeGrabbedBy(IGrabber grabber) {
			if (!enabled || grabber == null) return false;
			if (state == GrabState.Pending || state == GrabState.Releasing) return false;
			if (singleGrabberOnly && grabbers.Count > 0 && !grabbers.Contains(grabber)) return false;
			if (grabber.Rig == EntityRig.None) return true;

			return (allowed & grabber.Rig) != 0;
		}

		public bool TryGetPose(EntityRig rig, out Vector3 position, out Quaternion rotation) {
			if (grabPose != null) return grabPose.TryGetPose(rig, out position, out rotation);

			position = Vector3.zero;
			rotation = Quaternion.identity;
			return false;
		}
	}
}
