using Nox.Entities;
using UnityEngine;

namespace Nox.Interactions {
	/// <summary>
	/// Grabbing operations of a networked grabbable entity. A grabbable is an <see cref="IEntity"/> like any
	/// other world entity; this contract only adds what a remote player's grab does to it.
	/// </summary>
	public interface INetworkedGrabbable : IEntity {
		/// <summary>Grabbing facet of this entity.</summary>
		IGrabbable Grabbable { get; }

		/// <summary>Remote grab: the player's rig slot took the object at <paramref name="contact"/>.</summary>
		/// <returns><c>true</c> when the object accepted the grabber.</returns>
		bool ApplyGrab(uint playerId, ushort rigCode, GrabKind kind, Vector3 contact, float duration);

		/// <summary>Remote release of the player's rig slot.</summary>
		bool ApplyRelease(uint playerId, ushort rigCode, float multiplier);

		/// <summary>Authoritative pose broadcast by the owner.</summary>
		void ApplyState(Vector3 position, Quaternion rotation, Vector3 velocity, uint ownerId);

		/// <summary>Set the owner without reporting it back over the network (used by the receiver).</summary>
		void SetOwnerId(uint ownerId);
	}
}
