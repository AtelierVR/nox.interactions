using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[DefaultExecutionOrder(-100)]
	[RequireComponent(typeof(Rigidbody))]
	public partial class GrabbableBase : MonoBehaviour, IGrabbable, IGrabGroup, IGrabBody, IGrabColliders,
		IGrabAccess, IGrabAttachment, IGrabThrow, IGrabHighlight, IGrabSqueeze, IGrabTear, IGrabVelocity, IGrabPriority {
		[Header("Grab")]
		[SerializeField] private Rigidbody body;
		[SerializeField] private List<Collider> grabColliders = new();
		[SerializeField] private Material highlightMaterial;
		[SerializeField] private GrabbableSettings grab = GrabbableSettings.Default;

		[Header("Held")]
		[SerializeField] private HeldSettings held = HeldSettings.Default;

		[Header("Release")]
		[SerializeField] private ReleaseSettings release = ReleaseSettings.Default;
		[SerializeField]
		private Motion motion = new();

		[Header("Tear")]
		[SerializeField]
		private TearSettings tear = TearSettings.Default;

		private readonly List<IGrabber> grabbers = new();

		private static readonly List<Collider> foundColliders = new();

		private GrabState state = GrabState.Free;

		public Transform Target => transform;

		public Rigidbody Body => body;

		public GrabState State => state;

		public int HeldCount => grabbers.Count;

		public IReadOnlyList<IGrabber> Grabbers => grabbers;

		/// <summary>
		/// Grabber that carries the object: the first one to take it. Its joint is the one a physical hold binds, so the
		/// object is never pulled by two rigid constraints at once.
		/// </summary>
		public IGrabber Driver
			=> grabbers.Count > 0
				? grabbers[0]
				: null;

		/// <summary>Whether this object is carried by a joint, so a grabber has to bind one to hold it.</summary>
		public bool HasPhysicsDriver
			=> physicsHeld && grabbers.Count > 0;

		/// <summary>
		/// Whether <paramref name="grabber"/> is the one carrying a physical hold, i.e. the one that binds the joint. Every
		/// other grabber assists: it holds the same object without a second joint, which would fight the first.
		/// </summary>
		public bool IsDriver(IGrabber grabber)
			=> physicsHeld
			   && grabber != null
			   && grabbers.Count > 0
			   && ReferenceEquals(Driver, grabber);

		public IReadOnlyList<Collider> GrabColliders => grabColliders;

		public GrabbableSettings GrabConfig {
			get => grab;
			set => grab = value;
		}

		public HeldSettings HeldConfig {
			get => held;
			set => held = value.Sanitized();
		}

		public ReleaseSettings ReleaseConfig {
			get => release;
			set => release = value;
		}

		public TearSettings TearConfig {
			get => tear;
			set => tear = value;
		}

		public bool IsGrabbable {
			get => grab.isGrabbable;
			set => grab.isGrabbable = value;
		}

		public EntityRig Allowed {
			get => grab.allowed;
			set => grab.allowed = value;
		}

		public GrabType GrabType {
			get => grab.type;
			set => grab.type = value;
		}

		public GrabPoseType PoseType {
			get => grab.pose;
			set => grab.pose = value;
		}

		public bool InstantGrab {
			get => grab.instant;
			set => grab.instant = value;
		}

		public bool SingleGrabberOnly {
			get => grab.single;
			set => grab.single = value;
		}

		public bool AllowGrabberSwap {
			get => grab.allowSwap;
			set => grab.allowSwap = value;
		}

		public float PriorityWeight {
			get => grab.priority;
			set => grab.priority = value;
		}

		public bool RequireFreeBody {
			get => grab.requireFree;
			set => grab.requireFree = value;
		}

		public bool IgnoreWeight => held.ignoreWeight;

		public float FollowSpeed => held.followSpeed;

		public float MaxHeldVelocity => held.followSpeed;

		public float HeldMassDivider {
			get => held.massDivider;
			set => held.massDivider = Mathf.Max(1f, value);
		}

		public float JointBreakForce => release.jointBreakForce;

		public float ThrowPower {
			get => release.throwPower;
			set => release.throwPower = value;
		}

		public Motion Motion {
			get => motion;
			set => motion = value != null ? value : new Motion();
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

			motion ??= new Motion();
			solve = solve.Sanitized();
			held = held.Sanitized();

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
			if (!enabled || !grab.isGrabbable || grabber == null)
				return false;
			if (state == GrabState.Pending || state == GrabState.Releasing)
				return false;
			if (grabbers.Contains(grabber))
				return false;
			if (grab.single && grabbers.Count > 0 && !grab.allowSwap)
				return false;

			// a body the physics owns (kinematic, constrained) is not taken: the hold would fight the constraints
			if (grab.requireFree && body != null && (body.isKinematic || body.constraints != RigidbodyConstraints.None))
				return false;

			if (grabber.Rig == EntityRig.None)
				return true;

			// one grabber per rig: the ray of a hand cannot take what the hand itself already holds (and the other way
			// around), while the other hand still can
			if (IsHeldByRig(grabber.Rig))
				return false;

			return (grab.allowed & grabber.Rig) != 0;
		}

		private bool IsHeldByRig(EntityRig rig) {
			for (var i = 0; i < grabbers.Count; i++)
				if (grabbers[i] != null && grabbers[i].Rig == rig)
					return true;

			return false;
		}
	}
}
