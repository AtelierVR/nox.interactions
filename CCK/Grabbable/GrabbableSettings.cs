using System;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	[Serializable]
	public struct GrabbableSettings {
		[Tooltip("Whether the object accepts a grabber at all. Off, it is held until released and cannot be taken again.")]
		public bool isGrabbable;
		[Tooltip("Rigs allowed to take the object.")]
		public EntityRig allowed;
		[Tooltip("How a direct grab brings the object into the hand, when the grabber has no preference.")]
		public GrabType type;
		[Tooltip("Pose the hand takes while holding the object.")]
		public GrabPoseType pose;
		[Tooltip("Attach at once, without the grab blend.")]
		public bool instant;
		[Tooltip("Keep the first grabber only: another one cannot take the object.")]
		public bool single;
		[Tooltip("A single-grabber object hands itself over to the new grabber instead of refusing it.")]
		public bool allowSwap;
		[Min(0f), Tooltip("Grab priority: divides the distance to the palm when the grabber picks between candidates, so a higher weight wins against a closer object.")]
		public float priority;
		[Tooltip("Refuse a body the physics owns (kinematic or constrained): the hold would fight the constraints.")]
		public bool requireFree;

		public static GrabbableSettings Default => new() {
			isGrabbable = true,
			allowed = EntityRig.All,
			type = GrabType.Default,
			pose = GrabPoseType.Grab,
			instant = false,
			single = false,
			allowSwap = true,
			priority = 1f,
			requireFree = false
		};
	}

	[Serializable]
	public struct HeldSettings {
		[Tooltip("The object follows the grab point while held. Off: it stays a dynamic body, carried by a joint.")]
		public bool ignoreWeight;
		[Tooltip("Replace the physics material of the colliders while held.")]
		public bool noFriction;
		[Min(0f), Tooltip("Fastest the object follows its grab point, in metres per second.")]
		public float followSpeed;
		[Min(0f), Tooltip("Stiffness of the velocity servo that brings the object onto its grip. The body stays dynamic, so the world can still block it (AutoHand's WeightlessFollower followPositionStrength).")]
		public float positionStrength;
		[Min(0f), Tooltip("Stiffness of the angular servo that turns the object onto its grip (AutoHand's followRotationStrength).")]
		public float rotationStrength;
		[Min(0f), Tooltip("Smallest velocity change per physics step, in m/s: below this the object is not pulled, which keeps a hold at rest from vibrating.")]
		public float minVelocityChange;
		[Min(0f), Tooltip("Fastest the object turns while held, in degrees per second.")]
		public float maxAngularSpeed;
		[Min(0f), Tooltip("Lowest linear damping of the body while it is carried.")]
		public float minDrag;
		[Min(0f), Tooltip("Lowest angular damping of the body while it is carried.")]
		public float minAngleDrag;
		[Min(0f), Tooltip("Lightest the body may become while it is carried.")]
		public float minMass;
		[Min(1f), Tooltip("Divider applied to the mass of the body while it is carried, the way AutoHand makes a held body easier to move.")]
		public float massDivider;

		public static HeldSettings Default => new() {
			ignoreWeight = true,
			noFriction = true,
			followSpeed = InteractionConstants.DefaultHeldVelocity,
			positionStrength = 30f,
			rotationStrength = 30f,
			minVelocityChange = 0.55f,
			maxAngularSpeed = 720f,
			minDrag = 1.5f,
			minAngleDrag = 3f,
			minMass = 0.1f,
			massDivider = 2f
		};

		/// <summary>
		/// Replaces the values a prefab authored before they existed left at zero: without it a hold would have no servo at
		/// all and the object would never come to its grip.
		/// </summary>
		public HeldSettings Sanitized() {
			var fallback = Default;

			if (positionStrength <= 0f) positionStrength = fallback.positionStrength;
			if (rotationStrength <= 0f) rotationStrength = fallback.rotationStrength;
			if (minVelocityChange <= 0f) minVelocityChange = fallback.minVelocityChange;
			if (maxAngularSpeed <= 0f) maxAngularSpeed = fallback.maxAngularSpeed;
			if (followSpeed <= 0f) followSpeed = fallback.followSpeed;
			if (massDivider < 1f) massDivider = fallback.massDivider;

			return this;
		}
	}

	[Serializable]
	public struct ReleaseSettings {
		[Min(0f), Tooltip("Scales the throw measured at the grab point.")]
		public float throwPower;
		[Min(0f), Tooltip("Break force of the joint of a physical hold, in newtons.")]
		public float jointBreakForce;

		public static ReleaseSettings Default => new() {
			throwPower = InteractionConstants.DefaultThrowMultiplier,
			jointBreakForce = InteractionConstants.DefaultJointBreakForce
		};
	}

	[Serializable]
	public struct TearSettings {
		[Tooltip("Let a held object that is joined to something else be torn off by pulling it away.")]
		public bool enabled;
		[Min(0f), Tooltip("Distance the held object may be pulled away from where it was grabbed before it tears off, in metres.")]
		public float distance;

		public static TearSettings Default => new() {
			enabled = true,
			distance = 0.2f
		};
	}
}
