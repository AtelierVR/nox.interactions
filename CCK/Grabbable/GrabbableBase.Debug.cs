using System.Text;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		/// <summary>What a hold looks like right now, for the debug views and for the self test.</summary>
		public struct HoldReport {
			/// <summary>Whether the object has a hold to report.</summary>
			public bool valid;

			/// <summary>Grabbers the pose could be read from.</summary>
			public int grabbers;

			/// <summary>Grabbers among them that have a follower.</summary>
			public int followers;

			/// <summary>Object pose the grabbers ask for.</summary>
			public Vector3 position;

			public Quaternion rotation;

			/// <summary>Farthest distance between two requested poses: how much the grabbers disagree.</summary>
			public float spread;

			/// <summary>Farthest distance a follower has to be pulled off its tracked pose to sit on the object.</summary>
			public float offset;

			/// <summary>Farthest distance between the grab point of a follower and the grip the hold asks for (0 when the hands are against the object).</summary>
			public float error;

			/// <summary>Farthest angle between the rotation of a follower and the grip rotation it was given (0 when the grip does not slide on the object).</summary>
			public float angle;

			/// <summary>Whether the object sits between the grabbers (inside the spread of what they ask for).</summary>
			public bool between;
		}

		public bool TryGetDebugHoldPose(out Vector3 position, out Quaternion rotation)
			=> TryGetHoldPose(out position, out rotation);

		public bool TryGetDebugRequestedPose(IGrabber grabber, out Vector3 position, out Quaternion rotation) {
			position = Vector3.zero;
			rotation = Quaternion.identity;

			if (grabber == null)
				return false;

			for (var i = 0; i < holds.Count; i++)
				if (ReferenceEquals(holds[i].grabber, grabber))
					return TryGetRequestedPose(holds[i], out position, out rotation);

			return false;
		}

		/// <summary>
		/// Pose a follower has to take for its grip to land on the object: what <c>PullHands</c> writes as an offset. False
		/// when the grabber has no follower (it <b>is</b> its grab point, nothing to pull) or when one grabber holds a free
		/// object (the object follows the grabber, not the opposite).
		/// </summary>
		public bool TryGetDebugHandPose(IGrabber grabber, out Vector3 position, out Quaternion rotation) {
			position = Vector3.zero;
			rotation = Quaternion.identity;

			if (grabber == null || !GripOnObject || !TrySolveHold(0f, out var solution))
				return false;

			var grip = GripPose(solution);

			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (!hold.follower || !ReferenceEquals(hold.grabber, grabber))
					continue;

				return GrabHoldSolver.TryGetHandPose(hold, grip, out position, out rotation);
			}

			return false;
		}

		/// <summary>
		/// Numbers of the hold: what every grabber asks for, how much they disagree, and how far a follower has to be pulled
		/// to sit on the object. This is what the self test and a live probe read to tell a correct grab from an object that
		/// drifted away from its grabbers.
		/// </summary>
		public bool TryGetHoldReport(out HoldReport report) {
			report = new HoldReport();

			if (!TrySolveHold(0f, out var solution))
				return false;

			report.valid = true;
			report.position = solution.position;
			report.rotation = solution.rotation;
			report.grabbers = solution.contributors;
			report.followers = solution.followers;

			var first = true;
			var min = Vector3.zero;
			var max = Vector3.zero;

			for (var i = 0; i < holds.Count; i++) {
				var hold = holds[i];

				if (TryGetRequestedPose(hold, out var requested, out _)) {
					if (first) {
						min = max = requested;
						first = false;
					}
					else {
						min = Vector3.Min(min, requested);
						max = Vector3.Max(max, requested);
					}
				}

				if (!hold.follower || !TryGetDebugHandPose(hold.grabber, out var handPosition, out var handRotation))
					continue;

				var point = hold.grabber.GrabPoint;
				if (!point)
					continue;

				report.error = Mathf.Max(report.error, Vector3.Distance(point.position, handPosition));
				report.angle = Mathf.Max(report.angle, Quaternion.Angle(point.rotation, handRotation));

				if (hold.grabber is not IGrabberPose pose || !pose.TryGetTrackedPose(out var tracked, out _))
					continue;

				report.offset = Mathf.Max(report.offset, Vector3.Distance(handPosition, tracked));
			}

			if (first) {
				report.between = true;
				return true;
			}

			report.spread = (max - min).magnitude;
			report.between = (report.position - (min + max) * 0.5f).magnitude <= report.spread * 0.5f + 0.001f;

			return true;
		}

		/// <summary>
		/// Checks a hold against what makes it look wrong: a position that is not a number, an object outside what its
		/// grabbers ask for, or a follower that would have to leave its controller to reach it.
		/// </summary>
		public bool TryValidateHold(out HoldReport report, out string error) {
			error = null;

			if (!TryGetHoldReport(out report))
				return true;

			if (!IsFinite(report.position) || !IsFinite(report.rotation)) {
				error = "the hold pose is not a number";
				return false;
			}

			if (!report.between) {
				error = $"the object is outside the poses of its grabbers (spread {report.spread:0.000} m)";
				return false;
			}

			if (report.followers > 0 && solve.maxHandOffset > 0f && report.offset > solve.maxHandOffset + 0.0001f) {
				error = $"a follower has to travel {report.offset:0.000} m off its controller (max {solve.maxHandOffset:0.000} m)";
				return false;
			}

			return true;
		}

		/// <summary>Human readable state of the hold, for a log or a probe.</summary>
		public string DescribeHold() {
			var text = new StringBuilder();

			text.Append($"{name}: state={state} grabbers={grabbers.Count} holds={holds.Count} physics={physicsHeld} dynamic={dynamicHeld}");

			if (TryGetHoldReport(out var report))
				text.Append($" pos={report.position} spread={report.spread:0.000} offset={report.offset:0.000} error={report.error:0.000} angle={report.angle:0.0} between={report.between}");

			return text.ToString();
		}

		private static bool IsFinite(in Vector3 value)
			=> !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
			   && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

		private static bool IsFinite(in Quaternion value)
			=> !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) && !float.IsNaN(value.w)
			   && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z) && !float.IsInfinity(value.w);
	}
}
