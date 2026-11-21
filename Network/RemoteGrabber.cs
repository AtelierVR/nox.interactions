using Nox.Entities;
using Nox.Interactions;
using Nox.Players;
using UnityEngine;

namespace Nox.CCK.Interactions.Network {
	[DefaultExecutionOrder(1450)]
	public sealed class RemoteGrabber : MonoBehaviour, IGrabber, IGrabberPose {
		private IPlayer    _player;
		private ushort     _partIndex;
		private GrabKind   _kind;
		private float      _reach;
		private IGrabbable _held;

		public uint PlayerId { get; private set; }

		public ushort RigCode { get; private set; }

		public Transform GrabPoint
			=> transform;

		public EntityRig Rig
			=> EntityRig.None;

		public GrabSource Source
			=> GrabSource.Remote;

		public GrabKind Kind
			=> _kind;

		public GrabState State
			=> _held != null
				? GrabState.Held
				: GrabState.Free;

		public IGrabbable HeldGrabbable
			=> _held;

		public Vector3 HoldingPoint
			=> transform.position;

		public float ReachDistance {
			get => _reach;
			set => _reach = value;
		}

		internal void Bind(IPlayer player, ushort rigCode, ushort partIndex, GrabKind kind) {
			_player    = player;
			PlayerId   = player?.Id ?? 0;
			RigCode    = rigCode;
			_partIndex = partIndex;
			_kind      = kind;

			SyncToPart();
		}

		internal void SyncToPart() {
			if (_player == null)
				return;

			if (!_player.TryGetPart(_partIndex, out var part) || part == null)
				return;

			transform.SetPositionAndRotation(part.Position, part.Rotation);
		}

		internal void SetHeld(IGrabbable grabbable)
			=> _held = grabbable;

		public bool TryGrab(IGrabbable grabbable, in RaycastHit contact, float duration) {
			if (_held != null || grabbable == null)
				return false;

			if (!grabbable.TryAddGrabber(this, contact, duration))
				return false;

			_held = grabbable;
			return true;
		}

		public bool TryRelease(IGrabbable grabbable, float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity) {
			if (grabbable == null || !grabbable.TryRemoveGrabber(this, throwMultiplier))
				return false;

			_held = null;
			return true;
		}

		public bool TryReleaseHeld(float throwMultiplier, Vector3 throwVelocity, Vector3 throwAngularVelocity)
			=> TryRelease(_held, throwMultiplier, throwVelocity, throwAngularVelocity);

		public bool TryGetTrackedPose(out Vector3 position, out Quaternion rotation) {
			var t      = transform;
			position   = t.position;
			rotation   = t.rotation;
			return true;
		}

		private void LateUpdate() {
			if (_player == null)
				return;

			SyncToPart();
		}
	}
}
