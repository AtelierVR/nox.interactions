using System.Collections.Generic;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	/// <summary>
	/// Solves the pose of an object held by any number of grabbers, the way AutoHand's <c>AverageSetMoveTo</c> balances
	/// several hands, without the two problems the joint-per-hand approach has:
	/// <list type="bullet">
	/// <item>the object is the <b>strict</b> mean of what the grabbers ask for, so it stays exactly <b>between</b> them and
	/// no grabber - not even the one that took the object first - has more authority than another;</item>
	/// <item>each grabber keeps its own grip, so a grabber whose pose disagrees (a ray held at a distance, a controller
	/// that left its hand behind) only moves the object by its share.</item>
	/// </list>
	/// The solver is pure (no Unity object is written, no state is kept) so the same maths can be replayed by a test, which
	/// is what <see cref="GrabSelfTest"/> does.
	/// </summary>
	public static class GrabHoldSolver {
		/// <summary>
		/// Frame a grip is expressed in: the pose the grabber is tracked at when the hold reads it, else its grab point.
		/// <para>
		/// A hold that reads the tracked pose (the XRFollow node of a hand) reads it because a follower is dragged by what it
		/// holds: reading the body of the hand would make the object chase the hand that chases the object, and the hand
		/// carrying it would then vote for the object to stay where it already is. That is the case of a hold shared by
		/// several grabbers, and of a hold on an object that a servo carries on its own body: the world can stop such an
		/// object, so the grip is on the object and the grabbers are the ones pulled onto it.
		/// </para>
		/// <para>
		/// A single grip of a frozen object stays on the grab point, so the object is glued to the grabber that carries it.
		/// </para>
		/// </summary>
		public static bool TryGetFrame(IGrabber grabber, bool tracked, out Vector3 position, out Quaternion rotation) {
			position = Vector3.zero;
			rotation = Quaternion.identity;

			if (grabber == null)
				return false;

			if (tracked && grabber is IGrabberPose pose && pose.TryGetTrackedPose(out position, out rotation))
				return true;

			var point = grabber.GrabPoint;

			if (!point)
				return false;

			position = point.position;
			rotation = point.rotation;
			return true;
		}

		/// <summary>Pose a single grabber asks the object to take.</summary>
		public static bool TryRequest(
			in GrabberHold hold,
			bool tracked,
			out HoldRequest request
		) => TryRequest(hold, tracked, out _, out _, out request);

		/// <summary>Pose a single grabber asks the object to take, with the frame the grip was expressed in.</summary>
		public static bool TryRequest(
			in GrabberHold hold,
			bool tracked,
			out Vector3 framePosition,
			out Quaternion frameRotation,
			out HoldRequest request
		) {
			request = default;
			framePosition = Vector3.zero;
			frameRotation = Quaternion.identity;

			if (!hold.Valid || !TryGetFrame(hold.grabber, tracked, out framePosition, out frameRotation))
				return false;

			request = new HoldRequest {
				position = framePosition + frameRotation * hold.gripPosition,
				rotation = frameRotation * hold.gripRotation,
				valid = true
			};

			return true;
		}

		/// <summary>
		/// Pose the object takes between its grabbers: the rigid pose that puts its grab points <b>as close as possible</b> to
		/// the followers holding them, i.e. the least-squares fit of the grip points onto the tracked poses.
		/// <para>
		/// It is not the mean of the requests: a request is the object pose implied by one grabber, and averaging them leaves
		/// every grabber with a distance to its own grip as soon as they disagree (the wrists turn around the grip lever), which
		/// is what makes the object leave the hands. The fit minimizes the sum of those distances instead, so the object sits
		/// where the hands are, and the offset each hand carries is the smallest the hold allows. Every grabber weighs the
		/// same, so no grabber, not even the one that took the object first, has more authority than another.
		/// </para>
		/// <para>
		/// The step is capped by <see cref="HoldSolveSettings.maxSpeed"/>: adding a grabber, or a grabber that reports a
		/// pose that has nothing to do with the hold, changes the pose smoothly instead of teleporting the object.
		/// </para>
		/// </summary>
		public static bool TrySolve(
			IReadOnlyList<GrabberHold> holds,
			bool tracked,
			Vector3 currentPosition,
			Quaternion currentRotation,
			float deltaTime,
			in HoldSolveSettings settings,
			out HoldSolution solution
		) {
			solution = default;

			if (holds == null)
				return false;

			var requestSum = Vector3.zero;
			var frameSum = Vector3.zero;
			var gripSum = Vector3.zero;
			var rotation = currentRotation;
			var contributors = 0;
			var followers = 0;

			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (!TryRequest(hold, tracked, out var framePosition, out _, out var request))
					continue;

				requestSum += request.position;
				frameSum += framePosition;
				gripSum += hold.handPosition;

				rotation = contributors == 0
					? request.rotation
					: Quaternion.Slerp(rotation, request.rotation, 1f / (contributors + 1));

				contributors++;

				if (hold.follower)
					followers++;
			}

			if (contributors == 0)
				return false;

			Vector3 position;

			if (contributors == 1) {
				// a single grip is glued to its grabber: the object takes exactly the pose it asks for
				position = requestSum;
			} else {
				Fit(holds, tracked, frameSum / contributors, gripSum / contributors, rotation, out position, out rotation);
			}

			var maxStep = deltaTime > 0f ? settings.maxSpeed * deltaTime : float.PositiveInfinity;

			if (position != currentPosition && !float.IsInfinity(maxStep)) {
				var distance = Vector3.Distance(position, currentPosition);

				if (distance > maxStep)
					position = Vector3.MoveTowards(currentPosition, position, maxStep);
			}

			solution = new HoldSolution {
				position = position,
				rotation = rotation,
				contributors = contributors,
				followers = followers
			};

			return true;
		}

		/// <summary>
		/// Rigid pose that minimizes the distance between the grab points of the hold and the tracked poses that hold them.
		/// <para>
		/// The rotation aligns the spread of the grip points on the object with the spread of the tracked poses: the two
		/// grabbers farthest apart give the axis, and the roll is the one closest to the mean of the requested rotations, so the
		/// object still turns with the wrists. When the hands spread wider than the grips allow (the object cannot stretch),
		/// the remaining distance is split evenly instead of being carried by one hand.
		/// </para>
		/// </summary>
		private static void Fit(
			IReadOnlyList<GrabberHold> holds,
			bool tracked,
			Vector3 meanFrame,
			Vector3 meanGrip,
			Quaternion meanRotation,
			out Vector3 position,
			out Quaternion rotation
		) {
			rotation = meanRotation;
			position = meanFrame - meanRotation * meanGrip;

			var first = Widest(holds, tracked, meanGrip, -1);
			if (first < 0)
				return;

			var far = holds[first].handPosition;
			var second = Widest(holds, tracked, far, first);

			if (second < 0)
				return;

			if (!TryGetFrame(holds[first].grabber, tracked, out var firstFrame, out _)
				|| !TryGetFrame(holds[second].grabber, tracked, out var secondFrame, out _))
				return;

			var spread = far - holds[second].handPosition;
			var reach = firstFrame - secondFrame;

			if (spread.sqrMagnitude < 0.000001f || reach.sqrMagnitude < 0.000001f)
				return;

			var axis = reach.normalized;
			var align = Quaternion.FromToRotation(spread.normalized, axis);

			// swing-twist: keep the rotation that aligns the spread, then roll it to the closest to what the grabbers ask for
			var delta = meanRotation * Quaternion.Inverse(align);

			if (delta.w < 0f)
				delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

			var twist = 2f * Mathf.Atan2(Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis), delta.w) * Mathf.Rad2Deg;

			rotation = Quaternion.AngleAxis(twist, axis) * align;
			position = meanFrame - rotation * meanGrip;
		}

		/// <summary>Hold whose grab point is the farthest from <paramref name="from"/>, ignoring <paramref name="skip"/>.</summary>
		private static int Widest(IReadOnlyList<GrabberHold> holds, bool tracked, Vector3 from, int skip) {
			var best = -1;
			var bestDistance = -1f;

			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (i == skip || !hold.Valid || !TryGetFrame(hold.grabber, tracked, out _, out _))
					continue;

				var distance = (hold.handPosition - from).sqrMagnitude;

				if (best >= 0 && distance <= bestDistance)
					continue;

				best = i;
				bestDistance = distance;
			}

			return best;
		}

		/// <summary>
		/// Pose a grabber has to sit at for its grip to land on the object: its grab point, carried by the object.
		/// <para>
		/// The point is the one the grabber held at capture (<see cref="GrabberHold.handPosition"/>), <b>not</b> the frame the
		/// grip is expressed in: a hand grabs from a controller that sits behind the palm, and writing the hand onto the
		/// controller would leave it a controller-length away from the object.
		/// </para>
		/// </summary>
		public static bool TryGetHandPose(
			in GrabberHold hold,
			in HoldSolution solution,
			out Vector3 position,
			out Quaternion rotation
		) {
			position = Vector3.zero;
			rotation = Quaternion.identity;

			if (!hold.Valid || !solution.valid)
				return false;

			rotation = solution.rotation * hold.handRotation;
			position = solution.position + solution.rotation * hold.handPosition;

			return true;
		}

		/// <summary>
		/// Offset that puts a follower back on its grip, capped so a grabber is never yanked off its controller.
		/// <para>
		/// Both parts are offset: the grip of a grabber is the pose it had on the object when it took it, so the hand keeps
		/// that pose relative to the object (AutoHand stores the same pose in the space of the object, see
		/// <c>Hand.CreateGrabConnection</c> and <c>HandPoseData.SavePose(this, holdingObj.transform)</c>).
		/// </para>
		/// <para>
		/// The rotation is expressed <b>in the tracked frame</b>, the way the follower applies it
		/// (<c>trackedRotation * offset</c>, see <c>Part.ApplyFollowPoint</c>): the hand then lands on the grip rotation and
		/// stops turning with its controller. A world-space delta (<c>target * inverse(tracked)</c>) only works while both
		/// rotations share an axis; on a wrist that twists around another axis the hand keeps following the controller and
		/// the grip slides on the object.
		/// </para>
		/// </summary>
		public static bool TryGetHandOffset(
			in GrabberHold hold,
			in HoldSolution solution,
			Vector3 trackedPosition,
			Quaternion trackedRotation,
			in HoldSolveSettings settings,
			out Vector3 offsetPosition,
			out Quaternion offsetRotation
		) {
			offsetPosition = Vector3.zero;
			offsetRotation = Quaternion.identity;

			if (!TryGetHandPose(hold, solution, out var position, out var rotation))
				return false;

			var delta = position - trackedPosition;

			if (settings.maxHandOffset > 0f && delta.sqrMagnitude > settings.maxHandOffset * settings.maxHandOffset)
				delta = delta.normalized * settings.maxHandOffset;

			offsetPosition = delta;
			offsetRotation = Quaternion.Inverse(trackedRotation) * rotation;
			return true;
		}
	}
}
