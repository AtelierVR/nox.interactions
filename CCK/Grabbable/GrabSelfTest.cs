using System;
using System.Collections.Generic;
using System.Text;
using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nox.CCK.Interactions {
	/// <summary>
	/// Grabber used by <see cref="GrabSelfTest"/>: a tracked controller and a body driven by a follower, the way
	/// <c>XRFollow</c> drives a hand.
	/// <para>
	/// The body follows the tracked pose plus the offset the object writes (<see cref="IGrabberFollower"/>), so the test
	/// exercises the same contract a hand does without needing a player, a rig or a headset.
	/// </para>
	/// </summary>
	[AddComponentMenu("")]
	public sealed class GrabProbe : MonoBehaviour, IGrabber, IGrabberFollower, IGrabberBody, IGrabberPalm {
		/// <summary>Pose the controller reports (the tracked node of a hand).</summary>
		public Transform tracked;

		/// <summary>Volume of the palm, the part an object rests against.</summary>
		public SphereCollider palm;

		/// <summary>Object held through this probe.</summary>
		public IGrabbable held;

		private GrabState state = GrabState.Free;
		private Vector3 offsetPosition;
		private Quaternion offsetRotation = Quaternion.identity;

		public Transform GrabPoint => transform;

		public EntityRig Rig => EntityRig.None;

		public GrabSource Source => GrabSource.Direct;

		public GrabKind Kind => GrabKind.Direct;

		public GrabState State => state;

		public IGrabbable HeldGrabbable => held;

		public Vector3 HoldingPoint => transform.position;

		public float ReachDistance { get; set; } = InteractionConstants.DefaultReachDistance;

		public IReadOnlyList<Collider> BodyColliders => Array.Empty<Collider>();

		public bool FollowsBody => true;

		public Vector3 Offset => offsetPosition;

		public Collider PalmVolume => palm;

		public bool TryGetPalmSurface(Vector3 worldPoint, out Vector3 point) {
			point = transform.position;

			if (!palm)
				return false;

			var centre = palm.bounds.center;
			var direction = worldPoint - centre;

			if (direction.sqrMagnitude < 0.000001f)
				return false;

			point = palm.ClosestPoint(centre + direction.normalized * palm.bounds.extents.magnitude * 2f);
			return true;
		}

		public bool TryGetTrackedPose(out Vector3 position, out Quaternion rotation) {
			if (!tracked) {
				position = Vector3.zero;
				rotation = Quaternion.identity;
				return false;
			}

			position = tracked.position;
			rotation = tracked.rotation;
			return true;
		}

		public void SetOffset(Vector3 position, Quaternion rotation) {
			offsetPosition = position;
			offsetRotation = rotation;
		}

		public void ClearOffset()
			=> SetOffset(Vector3.zero, Quaternion.identity);

		/// <summary>One step of the follower: the body chases the tracked pose plus the hold offset.</summary>
		public void Step(float blend = 1f) {
			if (!tracked)
				return;

			var position = tracked.position + offsetPosition;
			var rotation = tracked.rotation * offsetRotation;

			transform.SetPositionAndRotation(
				blend >= 1f ? position : Vector3.Lerp(transform.position, position, blend),
				blend >= 1f ? rotation : Quaternion.Slerp(transform.rotation, rotation, blend)
			);
		}

		public bool CanGrab(IGrabbable grabbable)
			=> grabbable != null
			   && held == null
			   && state == GrabState.Free
			   && grabbable.CanBeGrabbedBy(this);

		public bool TryGrab(IGrabbable grabbable, in RaycastHit contact, float duration) {
			if (!CanGrab(grabbable))
				return false;

			if (!grabbable.TryAddGrabber(this, contact, duration))
				return false;

			held = grabbable;
			state = GrabState.Held;
			return true;
		}

		public bool TryRelease(IGrabbable grabbable, float throwMultiplier) {
			if (grabbable == null || !ReferenceEquals(held, grabbable))
				return false;

			if (!grabbable.TryRemoveGrabber(this, throwMultiplier))
				return false;

			held = null;
			state = GrabState.Free;
			return true;
		}

		public bool TryRelease(IGrabbable grabbable, float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity)
			=> TryRelease(grabbable, throwMultiplier);

		public bool TryReleaseHeld(float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity)
			=> TryRelease(held, throwMultiplier);
	}

	/// <summary>
	/// Replays a grab with any number of grabbers on a throw-away object, outside of play mode if need be, and checks the
	/// invariants a hold has to keep:
	/// <list type="bullet">
	/// <item>the object stays <b>between</b> the tracked poses, it never flies off to one of them;</item>
	/// <item>the grabbers with a follower are pulled <b>onto</b> the object, they do not stay on their controllers;</item>
	/// <item>a grabber joining a hold does not move the object.</item>
	/// </list>
	/// It is the harness the grab pipeline is verified with: run it, read the lines, and any regression of the solver shows
	/// up as a failing step instead of as a handset session.
	/// <para>
	/// The same invariants can be read on a live object through <c>GrabbableBase.TryGetHoldReport</c>.
	/// </para>
	/// </summary>
	[AddComponentMenu("Nox/Interactions/Grab Self Test")]
	public sealed class GrabSelfTest : MonoBehaviour {
		public struct Result {
			public bool pass;
			public int steps;
			public int failures;
			public string[] lines;

			public override string ToString() {
				var text = new StringBuilder($"grab self test: {(pass ? "PASS" : "FAIL")} ({failures} failure(s) over {steps} step(s))");

				if (lines == null)
					return text.ToString();

				for (var i = 0; i < lines.Length; i++)
					text.Append('\n').Append("  ").Append(lines[i]);

				return text.ToString();
			}
		}

		private const float Tolerance = 0.02f;

		[SerializeField, Min(2), Tooltip("Number of grabbers taking the object.")]
		private int grabbers = 2;

		[SerializeField, Min(0.05f), Tooltip("Distance of the controllers from the object, in metres.")]
		private float spacing = 0.2f;

		[SerializeField, Min(4), Tooltip("Steps of the hold to replay.")]
		private int steps = 120;

		[SerializeField, Min(0f), Tooltip("Distance the controllers separate by over the replay, in metres.")]
		private float separation = 0.1f;

		[SerializeField, Min(0f), Tooltip("Angle the controllers turn by, in opposite directions, over the rotation replay, in degrees.")]
		private float rotation = 20f;

		[SerializeField, Tooltip("Write the result to the console.")]
		private bool logResult = true;

		[SerializeField, Tooltip("Run the replay when the component starts, so the check also happens in a scene left unattended.")]
		private bool runOnEnable;

		private void Start() {
			if (runOnEnable)
				Run();
		}

		/// <summary>Runs the replay with the serialized settings.</summary>
		[ContextMenu("Run Grab Self Test")]
		public void RunFromMenu()
			=> Run();

		/// <summary>Runs the replay with the serialized settings.</summary>
		public Result Run()
			=> Run(grabbers, spacing, steps, separation, rotation);

		public Result Run(int count, float distance, int stepCount, float separate, float turn = 20f) {
			count = Mathf.Max(2, count);
			stepCount = Mathf.Max(4, stepCount);
			separate = Mathf.Max(0f, separate);

			var lines = new List<string>();
			var failures = 0;

			void Check(bool condition, string message) {
				if (condition)
					return;

				failures++;
				lines.Add($"FAIL {message}");
			}

			var root = new GameObject("Grab Self Test");
			root.transform.SetParent(null, false);

			try {
				var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
				cube.name = "Held";
				cube.transform.SetParent(root.transform, false);
				cube.transform.localScale = Vector3.one * 0.1f;

				var body = cube.GetOrAddComponent<Rigidbody>();
				body.useGravity = false;
				body.mass = 1f;

				var grabbable = cube.AddComponent<GrabbableBase>();

				var grab = grabbable.GrabConfig;
				grab.isGrabbable = true;
				grab.allowed = EntityRig.All;
				grab.type = GrabType.Default;
				grab.single = false;
				grab.allowSwap = true;
				grab.instant = true;
				grabbable.GrabConfig = grab;

				var held = grabbable.HeldConfig;
				held.ignoreWeight = true;
				held.followSpeed = 20f;
				grabbable.HeldConfig = held;

				var probes = new GrabProbe[count];
				var directions = new Vector3[count];
				var rotations = new Quaternion[count];

				for (var i = 0; i < count; i++) {
					// spread the controllers around a circle, and put the body on the object surface facing its controller
					var angle = 360f / count * i * Mathf.Deg2Rad;
					var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
					directions[i] = direction;

					var probe = new GameObject($"Probe {i}");
					probe.transform.SetParent(root.transform, false);
					probe.transform.position = cube.transform.position + direction * (0.08f + i * 0.12f);
					probe.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
					rotations[i] = probe.transform.rotation;

					var palm = probe.AddComponent<SphereCollider>();
					palm.radius = 0.02f;
					palm.isTrigger = true;

					var tracked = new GameObject($"Probe {i} Tracked");
					tracked.transform.SetParent(root.transform, false);
					tracked.transform.position = cube.transform.position + direction * distance;
					tracked.transform.rotation = probe.transform.rotation;

					var component = probe.AddComponent<GrabProbe>();
					component.tracked = tracked.transform;
					component.palm = palm;
					probes[i] = component;
				}

				var startPosition = cube.transform.position;

				for (var i = 0; i < count; i++) {
					var before = cube.transform.position;

					Check(probes[i].TryGrab(grabbable, default, 0f), $"grabber {i} was refused by the object");
					probes[i].Step();

					if (i == 0)
						continue;

					var moved = Vector3.Distance(before, cube.transform.position);
					Check(moved <= Tolerance, $"grabber {i} moved the object by {moved:0.000} m when it joined");
				}

				if (count == 2) {
					// the object does not move when a grabber joins: the hands are the ones brought onto it, so both stay on it
					Check(Vector3.Distance(cube.transform.position, startPosition) <= Tolerance,
						  "the object moved when a second grabber joined");

					for (var i = 0; i < count; i++) {
						if (!grabbable.TryGetDebugHandPose(probes[i], out var target, out _))
							continue;

						var seat = Vector3.Distance(probes[i].transform.position, target);
						Check(seat <= grabbable.SolveSettings.maxHandOffset + Tolerance,
							  $"grabber {i} would have to travel {seat:0.000} m to reach the object");
					}
				}

				Check(grabbable.HeldCount == count, $"the object holds {grabbable.HeldCount} grabber(s) out of {count}");

				var delta = 1f / 90f;
				var maxStep = 0f;

				for (var step = 0; step < stepCount; step++) {
					var ratio = separate <= 0f ? 0f : (float)step / stepCount;

					for (var i = 0; i < count; i++) {
						var pose = probes[i].tracked;
						pose.position = cube.transform.position + directions[i] * (distance + ratio * separate);
					}

					var previous = cube.transform.position;

					grabbable.FollowHeld(delta);
					grabbable.DriveHeld(delta);

					for (var i = 0; i < count; i++)
						probes[i].Step();

					maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, cube.transform.position));
				}

				// the grip rotation must stay fixed on the object: the controllers turn, the hands turn back to keep their grip
				// the axis is tilted on purpose: a turn about the up axis commutes with a look rotation, so it would not tell a
				// correct rotation offset from one computed in the wrong frame (a wrist twists about any axis, not just up)
				var turnAxis = new Vector3(0.62f, 0.35f, 0.7f).normalized;

				for (var step = 0; step < stepCount; step++) {
					var ratio = turn == 0f ? 0f : (step + 1f) / stepCount;

					for (var i = 0; i < count; i++) {
						var direction = i % 2 == 0 ? 1f : -1f;
						probes[i].tracked.rotation = rotations[i] * Quaternion.AngleAxis(direction * ratio * turn, turnAxis);
					}

					grabbable.FollowHeld(delta);
					grabbable.DriveHeld(delta);

					for (var i = 0; i < count; i++)
						probes[i].Step();
				}

				var report = grabbable.TryGetHoldReport(out var numbers);

				Check(report, "the object has no hold to report");
				Check(numbers.between, $"the object sits outside what its grabbers ask for (spread {numbers.spread:0.000} m)");
				Check(grabbable.TryValidateHold(out _, out var error), error ?? "the hold is invalid");

				var cap = grabbable.SolveSettings.maxSpeed * delta + 0.0001f;
				Check(maxStep <= cap, $"the object moved {maxStep:0.000} m in one step (cap {cap:0.000} m)");

				Check(numbers.error <= Tolerance * 2.5f, $"a grabber is {numbers.error:0.000} m away from its grip");

				if (count == 2) {
					var left = Vector3.Distance(numbers.position, probes[0].tracked.position);
					var right = Vector3.Distance(numbers.position, probes[1].tracked.position);

					Check(Mathf.Abs(left - right) <= Tolerance * 2f,
						  $"the object stayed on a grabber ({left:0.000} m from one, {right:0.000} m from the other)");
				}

				// the object must sit where the distance between its grab points and the grabbers is the shortest: no small move
				// of the object may bring a hand closer to its grip. A mean of the requested poses does not have that property as
				// soon as the grabbers disagree, which is what made the object leave the hands when the wrists turned.
				if (count > 0) {
					var locals = new Vector3[count];
					var tracked = new Vector3[count];

					for (var i = 0; i < count; i++) {
						tracked[i] = probes[i].tracked.position;

						locals[i] = grabbable.TryGetDebugHandPose(probes[i], out var target, out _)
							? Quaternion.Inverse(cube.transform.rotation) * (target - cube.transform.position)
							: Vector3.zero;
					}

					float Cost(Vector3 position, Quaternion rotation) {
						var sum = 0f;

						for (var i = 0; i < count; i++)
							sum += (position + rotation * locals[i] - tracked[i]).sqrMagnitude;

						return sum;
					}

					var reference = Cost(cube.transform.position, cube.transform.rotation);

					for (var axis = 0; axis < 3; axis++) {
						var direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
						var moved = Cost(cube.transform.position + direction * 0.01f, cube.transform.rotation);

						Check(moved >= reference - 0.0005f,
							  $"moving the object along {direction} brings a hand closer ({reference:0.0000} -> {moved:0.0000}): it is not at the shortest distance");
					}
				}

				for (var i = 0; i < count; i++) {
					var tracked = probes[i].tracked.position;
					var hand = probes[i].transform.position;

					Check(grabbable.TryGetDebugHandPose(probes[i], out var target, out var targetRotation), $"grabber {i} has no hand pose in the hold");
					Check(Vector3.Distance(hand, target) <= Tolerance * 2.5f, $"grabber {i} is {Vector3.Distance(hand, target):0.000} m away from its grip");
					Check(Quaternion.Angle(probes[i].transform.rotation, targetRotation) <= 2f,
						  $"the grip of grabber {i} turned {Quaternion.Angle(probes[i].transform.rotation, targetRotation):0.0}° on the object");
					Check(Vector3.Distance(hand, tracked) <= grabbable.SolveSettings.maxHandOffset + Tolerance, $"grabber {i} was pulled {Vector3.Distance(hand, tracked):0.000} m off its controller");

					if (separate > 0f)
						Check(Vector3.Distance(hand, tracked) > 0.005f, $"grabber {i} stayed on its controller instead of moving onto the object");
				}

				if (count == 2) {
					var a = probes[0].tracked.position;
					var b = probes[1].tracked.position;

					Check(Between(numbers.position, a, b), "the object is not between the two controllers");
					Check(Between(probes[0].transform.position, a, b) && Between(probes[1].transform.position, a, b),
						  "a grabber ended outside the segment between the two controllers");

					// turning the wrists must not swing the object away from the hands: it stays between the grabber bodies
					Check(Between(numbers.position, probes[0].transform.position, probes[1].transform.position),
						  "the object is not between the two grabbers");
				}

				// a grabbable-to-hand object taken by one grabber must come into its palm, the object bringing itself in
				var mono = GameObject.CreatePrimitive(PrimitiveType.Cube);
				mono.name = "Held Mono";
				mono.transform.SetParent(root.transform, false);
				mono.transform.position = new Vector3(0f, 0f, 1f);
				mono.transform.localScale = Vector3.one * 0.1f;

				var monoBody = mono.GetOrAddComponent<Rigidbody>();
				monoBody.useGravity = false;

				var monoGrabbable = mono.AddComponent<GrabbableBase>();

				var monoGrab = monoGrabbable.GrabConfig;
				monoGrab.isGrabbable = true;
				monoGrab.allowed = EntityRig.All;
				monoGrab.type = GrabType.GrabbableToHand;
				monoGrab.single = false;
				monoGrab.instant = true;
				monoGrabbable.GrabConfig = monoGrab;

				var monoHeld = monoGrabbable.HeldConfig;
				monoHeld.ignoreWeight = true;
				monoHeld.followSpeed = 20f;
				monoGrabbable.HeldConfig = monoHeld;

				var monoHand = new GameObject("Mono Probe");
				monoHand.transform.SetParent(root.transform, false);
				monoHand.transform.position = mono.transform.position + Vector3.up * 0.25f;

				var monoTracked = new GameObject("Mono Probe Tracked");
				monoTracked.transform.SetParent(root.transform, false);
				monoTracked.transform.position = monoHand.transform.position;
				monoTracked.transform.rotation = monoHand.transform.rotation;

				var monoProbe = monoHand.AddComponent<GrabProbe>();
				monoProbe.tracked = monoTracked.transform;

				// a real contact on the object: the grabbable only brings it in when it knows where it was caught
				if (!Physics.Raycast(mono.transform.position + Vector3.up * 0.5f, Vector3.down, out var monoContact, 1f))
					Check(false, "the mono grab test found no contact on the object");
				else {
					Check(monoProbe.TryGrab(monoGrabbable, monoContact, 0f), "the mono grabber was refused by the object");

					for (var step = 0; step < stepCount; step++) {
						monoGrabbable.FollowHeld(delta);
						monoGrabbable.DriveHeld(delta);
					}

					var reached = Vector3.Distance(mono.transform.position, monoHand.transform.position);
					Check(reached <= Tolerance, $"a grabbable-to-hand object stopped {reached:0.000} m away from its palm");

					// the object is servo carried: it is where the grip is, so the hand sits on it and is only ever pulled
					// off its controller by the hold distance, never dragged across the room
					Check(monoProbe.Offset.magnitude <= monoGrabbable.SolveSettings.maxHandOffset + Tolerance,
						  $"the hand was pulled {monoProbe.Offset.magnitude:0.000} m off its controller in a mono-grab");
					Check(monoGrabbable.TryGetDebugHandPose(monoProbe, out var monoGrip, out _),
						  "a servo carried mono-grab has no grip pose to report");
				}

				var result = new Result {
					pass = failures == 0,
					steps = stepCount,
					failures = failures,
					lines = lines.ToArray()
				};

				if (logResult)
					Debug.Log(result.ToString(), this);

				return result;
			}
			finally {
				root.Destroy();
			}
		}

		/// <summary>Whether a point is on the segment of two controllers (with a small margin).</summary>
		private static bool Between(Vector3 point, Vector3 a, Vector3 b) {
			var segment = b - a;
			var length = segment.sqrMagnitude;

			if (length < 0.000001f)
				return Vector3.Distance(point, a) <= 0.05f;

			var ratio = Vector3.Dot(point - a, segment) / length;
			return ratio >= -0.02f && ratio <= 1.02f;
		}
	}
}
