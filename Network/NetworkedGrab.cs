using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Entities;
using Nox.Interactions;
using Nox.ModLoader;
using Nox.Players;
using Nox.Sessions;
using UnityEngine;
using UnityEngine.Events;

namespace Nox.CCK.Interactions.Network {
	[DefaultExecutionOrder(20000)]
	[RequireComponent(typeof(GrabbableBase))]
	public sealed class NetworkedGrab : MonoBehaviour, INetworkedGrabbable, IOwnedEntity, IEntityType {
		[SerializeField, Tooltip("Hand the ownership to the player that grabs the object. Off: the session master keeps it.")]
		private bool takeOwnershipOnGrab;

		[SerializeField, Min(0f), Tooltip("Minimum delay between two transform broadcasts, in seconds.")]
		private float sendInterval = 1f / 15f;

		[SerializeField, Min(0f), Tooltip("Period of a full pose broadcast even when the object is at rest, in seconds.")]
		private float keyframeInterval = 2f;

		[SerializeField, Min(0f), Tooltip("How fast an object owned by another player catches up with its authoritative pose.")]
		private float mirrorSpeed = 20f;

		private GrabbableBase _grabbable;

		private INetworkSession _net;
		private bool _resolved;

		private uint _localId;
		private uint _masterId;

		private readonly Dictionary<long, RemoteGrabber> _remote = new();
		private readonly HashSet<IGrabber> _local = new();
		private readonly List<IGrabber> _buffer = new();

		private float _sendAt;
		private float _keyframeAt;

		private bool _hasMirror;
		private Vector3 _mirrorPosition;
		private Quaternion _mirrorRotation;

		private bool _bodyCached;
		private bool _wasKinematic;
		private bool _wasGravity;
		private bool _wasHeld;

		private static readonly uint Invalid = uint.MaxValue;

		private readonly DataContainer _data = new();
		private readonly UnityEvent<IPlayer, IPlayer> _onOwnershipChanged = new();

		private uint _ownerId = IOwnedEntity.MASTER_OWNER_ID;

		public IGrabbable Grabbable
			=> _grabbable;

		#region IEntity

		public uint Id { get; set; }

		public IDataContainer Data
			=> _data;

		public uint Type
			=> WorldPath.DeriveType(transform);

		#endregion

		#region IOwnedEntity

		public IPlayer Owner
			=> _ownerId == IOwnedEntity.MASTER_OWNER_ID
				? _net?.MasterPlayer
				: _net?.Entities.GetEntity<IPlayer>(_ownerId);

		public bool IsOwner
			=> _ownerId == IOwnedEntity.MASTER_OWNER_ID;

		public UnityEvent<IPlayer, IPlayer> OnOwnershipChanged
			=> _onOwnershipChanged;

		public void ApplyOwner(IPlayer receiver, IPlayer requester)
			=> SetLocalOwner(receiver?.Id ?? IOwnedEntity.MASTER_OWNER_ID);

		public uint GetOwnerId()
			=> _ownerId;

		public void SetLocalOwner(uint ownerId) {
			if (_ownerId == ownerId)
				return;

			var previous = _net?.Entities.GetEntity<IPlayer>(_ownerId);
			_ownerId = ownerId;
			_onOwnershipChanged.Invoke(Owner, previous);
		}

		#endregion

		private void Awake()
			=> _grabbable = GetComponent<GrabbableBase>();

		private void OnEnable()
			=> ResolveAndAttach();

		private void OnDisable() {
			foreach (var grabber in _remote.Values)
				DestroyRemote(grabber);

			_remote.Clear();
			_local.Clear();

			Unsubscribe();
			RestoreBody();
			_resolved = false;
		}

		private bool ResolveAndAttach() {
			if (_resolved)
				return true;

			var api = ModManager.GetMod("sessions")?.GetInstance<ISessionAPI>();
			if (api == null || string.IsNullOrEmpty(api.Current) || !api.TryGet(api.Current, out var session))
				return false;

			if (session is not INetworkSession net) {
				_resolved = true;
				return true;
			}

			if (net.LocalPlayer == null || net.MasterPlayer == null)
				return false;

			_net      = net;
			_localId  = net.LocalPlayer.Id;
			_masterId = net.MasterPlayer?.Id ?? Invalid;

			net.OnEventReceived.AddListener(OnEvent);
			net.OnPlayerLeft.AddListener(OnPlayerLeft);
			net.OnAuthorityTransferred.AddListener(OnAuthorityTransferred);

			_resolved = true;
			return true;
		}

		private void Unsubscribe() {
			if (_net == null)
				return;

			_net.OnEventReceived.RemoveListener(OnEvent);
			_net.OnPlayerLeft.RemoveListener(OnPlayerLeft);
			_net.OnAuthorityTransferred.RemoveListener(OnAuthorityTransferred);
			_net = null;
		}

		private void RunOwner() {
			RestoreBody();

			var body   = _grabbable.Body;
			var held   = _grabbable.HeldCount > 0;
			var moving = body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.0004f;
			var edge   = held != _wasHeld;

			_wasHeld = held;

			var keyframe = Time.time >= _keyframeAt;

			if (!edge && !keyframe) {
				if (!held && !moving)
					return;
				if (Time.time < _sendAt)
					return;
			}

			var position = transform.position;
			var rotation = transform.rotation;
			var velocity = body != null ? body.linearVelocity : Vector3.zero;

			SendState(position, rotation, velocity);

			_sendAt = Time.time + sendInterval;

			if (keyframe)
				_keyframeAt = Time.time + keyframeInterval;
		}

		private void SendState(Vector3 position, Quaternion rotation, Vector3 velocity) {
			_net?.EmitEvent(
				GrabNetwork.StateEvent,
				GrabNetwork.EncodeState(Id, position, rotation, velocity, GetOwnerId())
			).Forget();
		}

		private void RunObserver() {
			CacheBody();

			if (_hasMirror)
				ApplyMirror();
		}

		private void ApplyMirror() {
			var deltaTime = Time.deltaTime;
			var blend     = mirrorSpeed > 0f ? 1f - Mathf.Exp(-mirrorSpeed * deltaTime) : 1f;

			var current = transform.position;
			var position = Vector3.Lerp(current, _mirrorPosition, blend);
			var rotation = Quaternion.Slerp(transform.rotation, _mirrorRotation, blend);

			if ((_mirrorPosition - current).sqrMagnitude > 4f) {
				position = _mirrorPosition;
				rotation = _mirrorRotation;
			}

			transform.SetPositionAndRotation(position, rotation);

			var body = _grabbable.Body;
			if (body != null && !body.isKinematic) {
				body.linearVelocity  = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
		}

		private void CacheBody() {
			var body = _grabbable.Body;
			if (body == null || body.isKinematic)
				return;

			if (!_bodyCached) {
				_bodyCached  = true;
				_wasKinematic = body.isKinematic;
				_wasGravity   = body.useGravity;
			}

			body.linearVelocity  = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.useGravity      = false;
			body.isKinematic     = true;
		}

		private void RestoreBody() {
			if (!_bodyCached)
				return;

			var body = _grabbable.Body;
			_bodyCached = false;

			if (body == null)
				return;

			body.isKinematic = _wasKinematic;
			body.useGravity  = _wasGravity;
		}

		private void SyncLocalGrabbers() {
			_buffer.Clear();

			var grabbers = _grabbable.Grabbers;
			for (var i = 0; i < grabbers.Count; i++) {
				var grabber = grabbers[i];
				if (grabber == null || grabber.Source == GrabSource.Remote)
					continue;
				_buffer.Add(grabber);
			}

			foreach (var grabber in _local) {
				if (grabber == null || _buffer.Contains(grabber))
					continue;
				EmitRelease(grabber);
			}

			foreach (var grabber in _buffer) {
				if (_local.Contains(grabber))
					continue;
				EmitRequest(grabber);
			}

			_local.Clear();
			foreach (var grabber in _buffer)
				_local.Add(grabber);
		}

		private void EmitRequest(IGrabber grabber) {
			if (grabber == null)
				return;

			if (_net == null)
				return;

			var point = grabber is GrabberBase source && source.Contact.collider
				? source.Contact.point
				: transform.position;

			_net.EmitEvent(
				GrabNetwork.RequestEvent,
				GrabNetwork.EncodeRequest(Id, GrabNetwork.RigCode(grabber.Rig), (byte)grabber.Kind, point, 0f)
			).Forget();
		}

		private void EmitRelease(IGrabber grabber) {
			if (grabber == null)
				return;

			if (_net == null)
				return;

			_net.EmitEvent(
				GrabNetwork.ReleaseEvent,
				GrabNetwork.EncodeRelease(Id, GrabNetwork.RigCode(grabber.Rig), 0f)
			).Forget();
		}

		private void OnEvent(long name, byte[] payload, IPlayer sender) {
			if (name == GrabNetwork.RequestEvent) {
				if (GrabNetwork.DecodeRequest(payload, out var id, out var rig, out var kind, out var point, out var duration) && id == Id)
					OnRemoteRequest(sender, rig, kind, point, duration);
				return;
			}

			if (name == GrabNetwork.ReleaseEvent) {
				if (GrabNetwork.DecodeRelease(payload, out var id, out var rig, out var multiplier) && id == Id)
					OnRemoteRelease(sender, rig, multiplier);
				return;
			}

			if (name == GrabNetwork.OwnerEvent) {
				if (GrabNetwork.DecodeOwner(payload, out var id, out var owner) && id == Id)
					SetOwner(owner, false);
				return;
			}

			if (name == GrabNetwork.StateEvent) {
				if (GrabNetwork.DecodeState(payload, out var id, out var position, out var rotation, out var velocity, out var owner) && id == Id)
					OnState(sender, owner, position, rotation);
			}
		}

		private void OnRemoteRequest(IPlayer sender, ushort rigCode, byte kind, Vector3 point, float duration) {
			if (_net == null || sender == null || sender.Id == _localId || !IsOwner)
				return;

			var rig = (EntityRig)rigCode;
			if (rig != EntityRig.None && (_grabbable.Allowed & rig) == 0)
				return;

			var key = Key(sender.Id, rigCode);
			if (!_remote.TryGetValue(key, out var grabber) || grabber == null) {
				grabber = CreateRemote(sender, rigCode, (GrabKind)kind);
				_remote[key] = grabber;
			}

			if (!_grabbable.TryAddGrabberAt(grabber, point, duration)) {
				_remote.Remove(key);
				DestroyRemote(grabber);
				return;
			}

			grabber.SetHeld(_grabbable);

			if (takeOwnershipOnGrab)
				SetOwner(sender.Id, true);
		}

		private void OnRemoteRelease(IPlayer sender, ushort rigCode, float multiplier) {
			if (sender == null)
				return;

			var key = Key(sender.Id, rigCode);
			if (!_remote.TryGetValue(key, out var grabber) || grabber == null)
				return;

			_grabbable.TryRemoveGrabber(grabber, multiplier);
			DestroyRemote(grabber);
			_remote.Remove(key);
		}

		private void OnState(IPlayer sender, uint owner, Vector3 position, Quaternion rotation) {
			if (IsOwner)
				return;

			SetLocalOwner(owner);
			_mirrorPosition = position;
			_mirrorRotation = rotation;
			_hasMirror     = true;
		}

		private void OnPlayerLeft(IPlayer player) {
			if (player == null)
				return;

			var leaving = player.Id;

			foreach (var key in new List<long>(_remote.Keys)) {
				if (_remote.TryGetValue(key, out var grabber) && grabber != null && grabber.PlayerId == leaving) {
					_grabbable.TryRemoveGrabber(grabber, 0f);
					DestroyRemote(grabber);
					_remote.Remove(key);
				}
			}

			if (GetOwnerId() == leaving)
				SetLocalOwner(uint.MaxValue);
		}

		private void OnAuthorityTransferred(IPlayer current, IPlayer previous) {
			_masterId = current?.Id ?? Invalid;

			if (GetOwnerId() == Invalid || GetOwnerId() == previous?.Id)
				SetLocalOwner(_masterId);
		}

		public bool ApplyGrab(uint playerId, ushort rigCode, GrabKind kind, Vector3 contact, float duration) {
			var player = ResolvePlayer(playerId);
			if (player == null)
				return false;

			OnRemoteRequest(player, rigCode, (byte)kind, contact, duration);
			return true;
		}

		public bool ApplyRelease(uint playerId, ushort rigCode, float multiplier) {
			var player = ResolvePlayer(playerId);
			if (player == null)
				return false;

			OnRemoteRelease(player, rigCode, multiplier);
			return true;
		}

		public void ApplyState(Vector3 position, Quaternion rotation, Vector3 velocity, uint ownerId)
			=> OnState(null, ownerId, position, rotation);

		public void SetOwnerId(uint ownerId)
			=> SetOwner(ownerId, false);

		private IPlayer ResolvePlayer(uint playerId) {
			if (_net == null)
				return null;

			return _net.Entities.GetEntity<IPlayer>(playerId);
		}

		private void SetOwner(uint owner, bool broadcast) {
			if (_ownerId == owner)
				return;

			SetLocalOwner(owner);

			if (broadcast && _net != null)
				_net.EmitEvent(GrabNetwork.OwnerEvent, GrabNetwork.EncodeOwner(Id, owner)).Forget();
		}

		private RemoteGrabber CreateRemote(IPlayer player, ushort rigCode, GrabKind kind) {
			var host = new GameObject($"RemoteGrabber {player.Id}:{rigCode}");
			host.transform.SetParent(transform, false);

			var grabber = host.AddComponent<RemoteGrabber>();
			grabber.Bind(player, rigCode, GrabNetwork.PartIndex(rigCode), kind);
			return grabber;
		}

		private static void DestroyRemote(RemoteGrabber grabber) {
			if (grabber != null)
				grabber.gameObject.Destroy();
		}

		private static long Key(uint playerId, ushort rigCode)
			=> ((long)playerId << 16) | rigCode;
	}
}
