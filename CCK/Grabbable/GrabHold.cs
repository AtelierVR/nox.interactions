using System;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	/// <summary>
	/// Grip of one grabber on a held object, captured when it took it.
	/// <para>
	/// The object pose is stored in the frame the grabber reports (its tracked pose when several grabbers hold it, its grab
	/// point otherwise), so the grab can be replayed as long as that frame is the one the grabber keeps reporting. Nothing
	/// else is kept: the pose a hand must sit at to hold the object is derived from the solution, which keeps the grip and
	/// the hand in the same frame by construction.
	/// </para>
	/// </summary>
	public struct GrabberHold {
		/// <summary>Grabber that took the object.</summary>
		public IGrabber grabber;

		/// <summary>Object position, in the frame of the grabber.</summary>
		public Vector3 gripPosition;

		/// <summary>Object rotation, in the frame of the grabber.</summary>
		public Quaternion gripRotation;

		/// <summary>
		/// Grab point of the grabber, in the space of the object at capture. This is where the grabber has to be for its grip
		/// to land on the object: a hand that grabbed from its controller stays at that grip, it is not written onto the
		/// controller.
		/// </summary>
		public Vector3 handPosition;

		/// <summary>Rotation of the grab point, in the space of the object at capture.</summary>
		public Quaternion handRotation;

		/// <summary>
		/// Whether the grabber is driven by a follower (XRFollow / HandFollow): only such a grabber can be pulled onto the
		/// object, the others are their grab point and must be left alone.
		/// </summary>
		public bool follower;

		public bool Valid
			=> grabber != null && (grabber is not UnityEngine.Object view || view);

		public static GrabberHold Capture(IGrabber grabber, bool follower, Vector3 framePosition, Quaternion frameRotation,
									  Vector3 objectPosition, Quaternion objectRotation, Vector3 pointPosition, Quaternion pointRotation) {
			var inverseFrame = Quaternion.Inverse(frameRotation);
			var inverseObject = Quaternion.Inverse(objectRotation);

			return new GrabberHold {
				grabber = grabber,
				gripPosition = inverseFrame * (objectPosition - framePosition),
				gripRotation = inverseFrame * objectRotation,
				handPosition = inverseObject * (pointPosition - objectPosition),
				handRotation = inverseObject * pointRotation,
				follower = follower
			};
		}
	}

	/// <summary>Pose a grabber asks the object to take.</summary>
	public struct HoldRequest {
		public Vector3 position;
		public Quaternion rotation;
		public bool valid;
	}

	/// <summary>Pose of an object held by one or several grabbers.</summary>
	public struct HoldSolution {
		public Vector3 position;
		public Quaternion rotation;

		/// <summary>Grabbers that could be read (0 when the object has no hold).</summary>
		public int contributors;

		/// <summary>Grabbers driven by a follower among the contributors.</summary>
		public int followers;

		public bool valid => contributors > 0;

		public static HoldSolution Of(Vector3 position, Quaternion rotation)
			=> new() { position = position, rotation = rotation, contributors = 1 };
	}

	/// <summary>
	/// Tuning of a hold shared by several grabbers. Its whole job is to stop the grabbers from fighting: the object is
	/// solved from what every grabber asks for, never teleported between them, and a follower is only pulled as far as its
	/// controller allows.
	/// </summary>
	[Serializable]
	public struct HoldSolveSettings {
		[Min(0f), Tooltip("Fastest the object may be moved by its hold, in metres per second: a grabber that jumps cannot send it across the room.")]
		public float maxSpeed;

		[Min(0f), Tooltip("Farthest a follower is pulled off its tracked pose to stay on the object, in metres (a hand is released past its own follow distance, so pulling it farther is pointless).")]
		public float maxHandOffset;

		public static HoldSolveSettings Default
			=> new() {
				maxSpeed = 15f,
				maxHandOffset = 0.5f
			};

		/// <summary>
		/// Replaces the values a prefab authored before the field existed left at zero, and refuses a zero speed (an object
		/// that may not move at all).
		/// </summary>
		public HoldSolveSettings Sanitized() {
			var fallback = Default;

			if (maxSpeed <= 0f) maxSpeed = fallback.maxSpeed;
			if (maxHandOffset < 0f) maxHandOffset = fallback.maxHandOffset;

			return this;
		}
	}
}
