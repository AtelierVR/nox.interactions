using System.Collections.Generic;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		[Header("Hold")]
		[SerializeField, Tooltip("How a hold shared by several grabbers is solved: how fast the object may be moved, and how far a follower is pulled onto it.")]
		private HoldSolveSettings solve = HoldSolveSettings.Default;

		/// <summary>Grip of every grabber, in the order of <c>grabbers</c>.</summary>
		private readonly List<GrabberHold> holds = new();

		public HoldSolveSettings SolveSettings {
			get => solve;
			set => solve = value.Sanitized();
		}

		/// <summary>
		/// Moves the object onto the pose its grabbers ask for, and pulls the grabbers that have a follower onto it.
		/// <para>
		/// A weightless hold is driven from here when the body may be frozen. A body that stays dynamic is driven by
		/// <see cref="DriveHeld"/> at the physics step instead, so the world can still constrain it. A physical hold is driven
		/// by the joint of its driver: the object is not moved, but the grabbers assisting it are still pulled onto it.
		/// </para>
		/// </summary>
		public void FollowHeld(float deltaTime) {
			if (state != GrabState.Held)
				return;

			PruneGrabbers();

			if (state != GrabState.Held || grabbers.Count == 0)
			return;

			UpdateGripLatch();

			if (physicsHeld) {
				PullHands(HoldSolution.Of(transform.position, transform.rotation));
				return;
			}

			if (!TrySolveHold(deltaTime, out var solution))
				return;

			// the body of a weightless hold is driven at the physics step, with its own constraints
			if (!dynamicHeld && TryGetHoldPoint(out var point))
				FollowPoint(point, deltaTime);

			// after the object moved: a hand sits on the hold pose of the object where it actually is, not where it was
			PullHands(solution);
		}

		private const float REFERENCE_STEP = 0.011111f;

		/// <summary>
		/// Drives a weightless hold whose body stays dynamic (AutoHand's <c>WeightlessFollower</c>): a velocity servo pulls the
		/// body onto its grip and turns it, capped at <see cref="HeldSettings.followSpeed"/>.
		/// <para>
		/// The body is <b>not</b> teleported: contacts, joints and any constraint of the scene keep applying, so an object
		/// pushed against a wall stops instead of going through it.
		/// </para>
		/// </summary>
		public void DriveHeld(float deltaTime) {
			if (state != GrabState.Held || !dynamicHeld || body == null || grabbers.Count == 0)
				return;

			if (!TrySolveHold(0f, out var solution))
				return;

			Drive(in solution, deltaTime);
		}

		private void FixedUpdate()
			=> DriveHeld(Time.fixedDeltaTime);

		/// <summary>
		/// Compares the step the servo asked for with the one the body actually made: a body that went less than half of the
		/// way is held by the world (a contact, a constraint), so it is where its grip is and its grabbers have to follow it
		/// instead of the opposite.
		/// <para>
		/// This is also what tells a grab apart from a grab that is still bringing the object in: an object on its way to a
		/// grabbable-to-hand palm <b>moves</b> with the servo, it is not blocked, so the hand is left on its controller.
		/// </para>
		/// </summary>
		private void TrackBlocked(float deltaTime) {
			var asked = servoAsked * deltaTime;
			var moved = body.position - servoFrom;

			servoBlocked = asked.sqrMagnitude > 0.000001f && moved.sqrMagnitude < asked.sqrMagnitude * 0.25f;
		}

		private void Drive(in HoldSolution solution, float deltaTime) {
			if (deltaTime <= 0f)
				return;

			var frameScale = REFERENCE_STEP / deltaTime;

			var maxSpeed = held.followSpeed > 0f ? held.followSpeed : InteractionConstants.DefaultHeldVelocity;
			var maxAngular = held.maxAngularSpeed > 0f ? held.maxAngularSpeed : 720f;

			var delta = solution.position - body.position;
			var distance = Mathf.Clamp(delta.magnitude, 0f, 0.5f);

			var desired = delta.sqrMagnitude > 0.000001f
				? delta.normalized * (held.positionStrength * distance)
				: Vector3.zero;

			desired = new Vector3(
				Mathf.Clamp(desired.x, -maxSpeed, maxSpeed),
				Mathf.Clamp(desired.y, -maxSpeed, maxSpeed),
				Mathf.Clamp(desired.z, -maxSpeed, maxSpeed)
			);

			var change = Mathf.Max(held.minVelocityChange, 0.0001f) * frameScale;
			var grow = Mathf.Min(1f, frameScale);

			TrackBlocked(deltaTime);

			var velocity = body.linearVelocity;
			body.linearDamping = Mathf.Max(held.minDrag, 0f) * frameScale;

			var asked = new Vector3(
				Mathf.MoveTowards(velocity.x, desired.x, change + Mathf.Abs(velocity.x) * grow),
				Mathf.MoveTowards(velocity.y, desired.y, change + Mathf.Abs(velocity.y) * grow),
				Mathf.MoveTowards(velocity.z, desired.z, change + Mathf.Abs(velocity.z) * grow)
			);

			body.linearVelocity = asked;

			servoFrom = body.position;
			servoAsked = asked;

			var turn = solution.rotation * Quaternion.Inverse(body.rotation);
			turn.ToAngleAxis(out var angle, out var axis);

			body.angularDamping = Mathf.Max(held.minAngleDrag, 0f) * frameScale;

			if (float.IsInfinity(axis.x) || Mathf.Approximately(angle, 0f))
				return;

			if (angle > 180f)
				angle -= 360f;

			var angular = Vector3.ClampMagnitude(axis.normalized * (angle * Mathf.Deg2Rad * held.rotationStrength), maxAngular);

			var spin = body.angularVelocity;

			body.angularVelocity = new Vector3(
				Mathf.MoveTowards(spin.x, angular.x, change * 10f + Mathf.Abs(spin.x) * grow),
				Mathf.MoveTowards(spin.y, angular.y, change * 10f + Mathf.Abs(spin.y) * grow),
				Mathf.MoveTowards(spin.z, angular.z, change * 10f + Mathf.Abs(spin.z) * grow)
			);

			if (desired.sqrMagnitude > 0.0001f || angular.sqrMagnitude > 0.0001f)
				body.WakeUp();
		}

		/// <summary>
		/// Where the object goes while it is held. The default implementation follows the solution of the hold; an object
		/// that is not carried on its grab point (a mirror that faces its grabber, for instance) overrides it.
		/// </summary>
		protected virtual void FollowPoint(Transform point, float deltaTime) {
			if (!point || !TrySolveHold(deltaTime, out var solution))
				return;

			ApplySolution(solution, deltaTime);
		}

		private void ApplySolution(in HoldSolution solution, float deltaTime) {
			var position = solution.position;
			var rotation = solution.rotation;

			if (blendDuration > 0f && blendElapsed < blendDuration) {
				blendElapsed += deltaTime;

				var blend = Mathf.Clamp01(blendElapsed / blendDuration);
				blend = 1f - (1f - blend) * (1f - blend);

				position = Vector3.Lerp(blendFromPosition, position, blend);
				rotation = Quaternion.Slerp(blendFromRotation, rotation, blend);
			}

			var current = transform.position;
			var step = held.followSpeed * deltaTime;

			// a shared hold follows its grabbers without the speed cap of a single one: the object has to sit between them
			if (!SharedHold && step > 0f && Vector3.SqrMagnitude(position - current) > step * step)
				position = Vector3.MoveTowards(current, position, step);

			position = RayCarried
				? StopAtObstacle(current, position)
				: position;

			if (position == current && rotation == transform.rotation)
				return;

			transform.SetPositionAndRotation(position, rotation);
		}

		/// <summary>
		/// Whether the object is carried at a distance by a ray: its pose is written straight on the transform, wherever the tip
		/// goes, so nothing in the world stops it. A hand grab is not like that - the object is taken where it is and barely
		/// moves - so only a ray hold is guarded against an obstacle.
		/// </summary>
		private bool RayCarried
			=> grabbers.Count > 0
				&& Alive(grabbers[0])
				&& grabbers[0].Kind == GrabKind.Indirect;

		/// <summary>
		/// Where a kinematic hold may move to: the pose is written straight on the transform, so the world never stops it and
		/// an object driven into a wall is pushed through it, then thrown out the moment it is let go. The step is swept, and
		/// an object that runs into something it cannot push stays where it is until the way is clear.
		/// <para>
		/// A surface the object rests or slides on is not in the way, and neither is a light body it could move: only a
		/// surface facing the motion, held by the scene or by something heavier, stops it. The colliders of the grabbers are
		/// left out, so the object still comes into the hand it is brought to.
		/// </para>
		/// </summary>
		private Vector3 StopAtObstacle(Vector3 from, Vector3 to) {
			var step     = to - from;
			var distance = step.magnitude;

			if (body == null || distance < 0.0001f)
				return to;

			var direction = step / distance;
			var hits      = body.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore);
			var stop      = distance;

			for (var i = 0; i < hits.Length; i++) {
				var hit      = hits[i];
				var collider = hit.collider;

				if (!collider || hit.distance >= stop)
					continue;

				if ((appliedExcludeMask & (1 << collider.gameObject.layer)) != 0)
					continue;

				if (Vector3.Dot(hit.normal, direction) > -0.2f)
					continue;

				var blocker = hit.rigidbody;

				if (blocker != null && !blocker.isKinematic && blocker.mass <= body.mass)
					continue;

				stop = hit.distance;
			}

			return stop >= distance
				? to
				: from + direction * Mathf.Max(stop - 0.005f, 0f);
		}

		/// <summary>
		/// Pose the grabbers ask for. It is the strict mean of their requests: the object is exactly <b>between</b> them,
		/// every grabber weighing the same, and the step is capped so a grabber that jumps cannot teleport it.
		/// </summary>
		private bool TrySolveHold(float deltaTime, out HoldSolution solution)
			=> GrabHoldSolver.TrySolve(
				holds,
				TracksFrame,
				transform.position,
				transform.rotation,
				deltaTime,
				solve,
				out solution
			);

		/// <summary>Pose the grabbers ask for, without moving anything (<c>deltaTime</c> 0: no step cap).</summary>
		private bool TryGetHoldPose(out Vector3 position, out Quaternion rotation) {
			if (TrySolveHold(0f, out var solution)) {
				position = solution.position;
				rotation = solution.rotation;
				return true;
			}

			position = transform.position;
			rotation = transform.rotation;
			return false;
		}

		/// <summary>Request of one grabber, for the debug views.</summary>
		private bool TryGetRequestedPose(in GrabberHold hold, out Vector3 position, out Quaternion rotation) {
			position = Vector3.zero;
			rotation = Quaternion.identity;

			if (!GrabHoldSolver.TryRequest(hold, TracksFrame, out var request))
				return false;

			position = request.position;
			rotation = request.rotation;
			return true;
		}

		/// <summary>
		/// Whether several grabbers hold the object. It is then solved between all of them, instead of following the one
		/// that took it first.
		/// </summary>
		private bool SharedHold
			=> holds.Count > 1;

		/// <summary>
		/// Whether the hold carries the object with a servo on its own body (see <c>dynamicHeld</c>): the world can stop such
		/// an object at any time, so it is the object that holds the grip and the grabbers that are pulled onto it.
		/// </summary>
		private bool ServoCarried
			=> held.ignoreWeight && body != null && KeepBodyDynamic;

		/// <summary>
		/// Whether the grip has to be read on the object rather than on the grab point of the grabber that took it: a hold
		/// shared by several grabbers, or one whose object is carried by a servo the world is blocking.
		/// </summary>
		private bool GripOnObject
			=> SharedHold || gripLatched;

		/// <summary>
		/// Whether the pose the hold asks for is read from the tracked pose of the grabbers instead of their grab point. A grip
		/// read from the grab point of a grabber the object offsets would make the object chase itself, so a servo carried hold
		/// always reads the tracked pose, blocked or not. The grip itself is still captured on the grabber (see <c>SetHold</c>):
		/// the object stays where it is in the hand, and a hand that goes back to its controller brings it along.
		/// </summary>
		private bool TracksFrame
			=> SharedHold || ServoCarried;

		/// <summary>
		/// Brings the grabbers that have a follower onto the object: each one is given an offset on its follow point, so the
		/// object drags the grabbers with it instead of drifting away from grabbers that stay on their controllers.
		/// AutoHand gets the same result with a joint on every holding hand; a weightless hold has none, so the offset is
		/// the equivalent of that constraint.
		/// <para>
		/// A single grabber of a <b>free</b> object needs no offset: it already carries the object and the hand must stay
		/// free on its controller (an object coming into a grabbable-to-hand palm is still travelling, it is not blocked).
		/// A <b>blocked</b> object is the opposite: the world stopped it, so the hand has to stay on its grip instead of
		/// following its controller through the object (the hand and the object are excluded from each other, so nothing
		/// else holds it back).
		/// </para>
		/// <para>
		/// Only the followers are offset: a grabber without one <b>is</b> its grab point, and moving it would move the object
		/// it holds. The driver of a physical hold is skipped too, its joint already carries the object.
		/// </para>
		/// </summary>
		private void PullHands(in HoldSolution solution) {
			if (!solution.valid || !GripOnObject) {
				ClearHandOffsets();
				return;
			}

			// where the grips are: the pose the grabbers solve together, or the pose the object actually has, since a
			// servo carried object is where its grip is even when the world stopped it short of what was asked
			var grip = GripPose(solution);

			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (!hold.Valid || !hold.follower || hold.grabber is not IGrabberFollower follower)
					continue;

				if (!follower.FollowsBody
					|| (physicsHeld && IsDriver(hold.grabber))
					|| !follower.TryGetTrackedPose(out var trackedPosition, out var trackedRotation)
					|| !GrabHoldSolver.TryGetHandOffset(hold, grip, trackedPosition, trackedRotation, solve,
						out var offsetPosition, out var offsetRotation)
				) {
					follower.ClearOffset();
					continue;
				}

				follower.SetOffset(offsetPosition, offsetRotation);
			}
		}

		/// <summary>
		/// Pose the grips sit on: the one the hold solves for when the grabbers share the object, else the pose the object
		/// actually has. A servo carried object stopped by the world is <b>short</b> of what the hold asks for, and the grip
		/// is on the object, not on where it would be if nothing had blocked it.
		/// </summary>
		private HoldSolution GripPose(in HoldSolution solution)
			=> SharedHold
				? solution
				: HoldSolution.Of(transform.position, transform.rotation);

		/// <summary>
		/// Arms the grip of the grabbers on the object while the world blocks it, and disarms it once the object caught up
		/// with what the hold asks for.
		/// <para>
		/// An object the servo is still carrying to its grip (a grabbable-to-hand one coming into the palm) is <b>not</b>
		/// blocked: the hand then stays on its controller, and it is the object that travels. Releasing the grip only once
		/// the object is back on its pose keeps the offset at zero when it is dropped, so the hand never snaps.
		/// </para>
		/// </summary>
		private void UpdateGripLatch() {
			if (SharedHold || !ServoCarried) {
				SetGripLatched(false);
				return;
			}

			if (servoBlocked) {
				SetGripLatched(true);
				return;
			}

			if (!gripLatched || !TrySolveHold(0f, out var solution))
				return;

			if (Vector3.SqrMagnitude(transform.position - solution.position) > HoldSettledTolerance * HoldSettledTolerance
				|| Quaternion.Angle(transform.rotation, solution.rotation) > 1f)
				return;

			SetGripLatched(false);
		}

		/// <summary>
		/// Arms or disarms the grip of the grabbers on the object. Nothing else changes: the pose the object is asked for is
		/// always read in the tracked pose (see <see cref="TracksFrame"/>), so arming only decides whether the grabbers are
		/// pulled onto the object.
		/// </summary>
		private void SetGripLatched(bool value)
			=> gripLatched = value;

		private void ClearHandOffsets() {
			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (hold.Valid && hold.grabber is IGrabberOffset offset)
					offset.ClearOffset();
			}
		}

		private bool TryGetHoldPoint(out Transform point) {
			var grabber = grabbers.Count > 0 ? grabbers[0] : null;
			point = Alive(grabber) ? grabber.GrabPoint : null;

			return point != null;
		}

		/// <summary>Whether the grabber still exists: an avatar or rig rebuild destroys a hand, whose grab would stay registered.</summary>
		private static bool Alive(IGrabber grabber)
			=> grabber != null && (grabber is not Object view || view);

		/// <summary>
		/// Drops the grabbers that were destroyed without releasing, and their grip.
		/// <para>
		/// A hand destroyed while it holds something (avatar change, rig rebuild, scene unload) never releases: its entry
		/// stays in the list and the next grab looks like a second grabber joined, which switches the hold to the shared
		/// branch and stops a grabbable-to-hand object from coming into the hand.
		/// </para>
		/// </summary>
		private void PruneGrabbers() {
			for (var i = grabbers.Count - 1; i >= 0; i--)
				if (!Alive(grabbers[i]))
					grabbers.RemoveAt(i);

			for (var i = holds.Count - 1; i >= 0; i--)
				if (!holds[i].Valid)
					holds.RemoveAt(i);

			if (grabbers.Count != 0 || state != GrabState.Held)
				return;

			state = GrabState.Free;
			ClearOffset();
			EndHold();
		}

		private void CaptureOffset(IGrabber grabber, Vector3 contactPoint, Collider surface) {
			if (CentersOnGrabPoint(grabber) && surface) {
				var holding = HoldingPointOf(grabber, contactPoint);
				var position = RestAgainstPalm(grabber, holding + (transform.position - contactPoint));

				SetHold(grabber, position, transform.rotation, TracksFrame);
				return;
			}

			// the object is taken where it is: the hand stays free on its controller and the object follows it
			RebaseOffset(grabber, TracksFrame);
		}

		/// <summary>
		/// Point an object brought into the hand lands on: the palm skin facing the contact of a grabber that knows where its
		/// palm is, else its holding point.
		/// </summary>
		private Vector3 HoldingPointOf(IGrabber grabber, Vector3 contact) {
			if (grabber == null)
				return transform.position;

			if (grabber is IGrabberPalm palm && palm.TryGetPalmSurface(contact, out var surface))
				return surface;

			return grabber.HoldingPoint;
		}

		/// <summary>
		/// Pushes a pose out of the palm when the palm volume ends up inside the object, along the shortest way out
		/// (<see cref="Physics.ComputePenetration"/>). A hand placed on an object has no contact point to aim at, and the
		/// object has to leave the palm along the axis that separates them: sideways would slide it, where the hand above it
		/// only has to let it down.
		/// <para>
		/// <see cref="Physics.ComputePenetration"/> reports the direction that moves the <b>first</b> collider (the palm)
		/// away from the second one, so the object leaves along the opposite direction.
		/// </para>
		/// </summary>
		private Vector3 RestAgainstPalm(IGrabber grabber, Vector3 position) {
			if (grabber is not IGrabberPalm { PalmVolume: { } palm } || !palm)
				return position;

			var offset = position - transform.position;

			for (var i = 0; i < grabColliders.Count; i++) {
				var collider = grabColliders[i];
				if (!collider) continue;

				var penetrating = Physics.ComputePenetration(
					palm, palm.transform.position, palm.transform.rotation,
					collider, collider.transform.position + offset, collider.transform.rotation,
					out var direction, out var distance
				);

				if (penetrating)
					offset -= direction * distance;
			}

			return transform.position + offset;
		}

		/// <summary>
		/// Captures the grip where the object is right now, so nothing moves: a second hand takes the object where it is,
		/// and the grabber that takes the hold over when the first one lets go picks it up from the same pose.
		/// </summary>
		private void RebaseOffset(IGrabber grabber)
			=> RebaseOffset(grabber, SharedHold);

		private void RebaseOffset(IGrabber grabber, bool tracked) {
			SetHold(grabber, transform.position, transform.rotation, tracked);

			blendDuration = 0f;
			blendElapsed = 0f;
		}

		private const float MaxSeatShift = 0.25f;

		/// <summary>
		/// Translation that puts the palm of a grabber against the object: the gap between the palm skin facing the object and
		/// the nearest point of the object, capped. A palm that already touches it gets zero. This is what keeps both hands on
		/// an object held by several grabbers, the way AutoHand lands the palm on the hit point (its <c>AutoPose</c>).
		/// </summary>
		private Vector3 SeatShift(IGrabber grabber) {
			if (grabber is not IGrabberPalm { PalmVolume: { } palm } || !palm)
				return Vector3.zero;

			var skin = palm.ClosestPoint(transform.position);
			var best = skin;
			var bestDistance = float.MaxValue;

			for (var i = 0; i < grabColliders.Count; i++) {
				var collider = grabColliders[i];
				if (!collider) continue;

				var candidate = collider.ClosestPoint(skin);
				var distance = (candidate - skin).sqrMagnitude;

				if (distance >= bestDistance) continue;

				bestDistance = distance;
				best = candidate;
			}

			if (bestDistance == float.MaxValue)
				return Vector3.zero;

			var shift = best - skin;

			if (shift.sqrMagnitude <= InteractionConstants.ContactEpsilon)
				return Vector3.zero;

			return Vector3.ClampMagnitude(shift, MaxSeatShift);
		}

		private void SetHold(
			IGrabber grabber,
			Vector3 position,
			Quaternion rotation,
			bool tracked,
			Vector3 pointShift = default
		) {
			if (grabber == null)
				return;

			if (!GrabHoldSolver.TryGetFrame(grabber, tracked, out var framePosition, out var frameRotation))
				return;

			var point = grabber.GrabPoint;
			var grabPoint = (point ? point.position : position) + pointShift;
			var grabRotation = point ? point.rotation : rotation;

			var hold = GrabberHold.Capture(
				grabber,
				grabber is IGrabberFollower,
				framePosition,
				frameRotation,
				position,
				rotation,
				grabPoint,
				grabRotation
			);

			for (var i = 0; i < holds.Count; i++) {
				if (!ReferenceEquals(holds[i].grabber, grabber))
					continue;

				holds[i] = hold;
				return;
			}

			holds.Add(hold);
		}

		private void RemoveHold(IGrabber grabber) {
			for (var i = holds.Count - 1; i >= 0; i--)
				if (!holds[i].Valid || ReferenceEquals(holds[i].grabber, grabber))
					holds.RemoveAt(i);
		}

		private void ClearOffset()
			=> holds.Clear();

		/// <summary>
		/// Whether the hold moves the object onto the holding point. A grabber that states it pulls the object to its point
		/// has the last word (a hand that resolved a <see cref="GrabType.GrabbableToHand"/> grab or a far grabber that
		/// brings what it caught into the hand); else the preference of the object decides, and a grabber that keeps the
		/// object where it caught it (a ray) is the default.
		/// </summary>
		private bool CentersOnGrabPoint(IGrabber grabber) {
			if (grabber == null)
				return true;

			// an object already held by another grabber is never brought into the new one: it is carried, so the object goes
			// between its grabbers (see HoldCentre). AutoHand takes the same branch for a grabbable-to-hand grab on a held
			// object (Hand.GrabObject: HeldCount() > 0). The first grabber always has the last word, whatever the object asks.
			if (grabbers.Count > 1)
				return false;

			if (grabber is IGrabPull pull)
				return pull.PullsToPoint;

			return grab.type == GrabType.GrabbableToHand;
		}
	}
}
