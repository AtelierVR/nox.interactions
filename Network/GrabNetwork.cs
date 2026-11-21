using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;
using PartRig = Nox.CCK.Entities.EntityRig;

namespace Nox.CCK.Interactions.Network {
	public static class GrabNetwork {
		public static readonly long RequestEvent = Hash.CRC64("nox.grab.request");
		public static readonly long ReleaseEvent = Hash.CRC64("nox.grab.release");
		public static readonly long OwnerEvent   = Hash.CRC64("nox.grab.owner");
		public static readonly long StateEvent   = Hash.CRC64("nox.grab.state");
		public static readonly long SpawnEvent   = Hash.CRC64("nox.grab.spawn");
		public static readonly long DestroyEvent = Hash.CRC64("nox.grab.destroy");

		public static ushort RigCode(EntityRig rig)
			=> (ushort)(int)rig;

		public static ushort PartIndex(ushort rigCode) {
			var rig = (EntityRig)rigCode;

			if ((rig & EntityRig.Head) != 0)
				return (ushort)PartRig.Head;
			if ((rig & EntityRig.LeftHand) != 0)
				return (ushort)PartRig.LeftHand;
			if ((rig & EntityRig.RightHand) != 0)
				return (ushort)PartRig.RightHand;
			if ((rig & EntityRig.LeftFoot) != 0)
				return (ushort)PartRig.LeftFoot;
			if ((rig & EntityRig.RightFoot) != 0)
				return (ushort)PartRig.RightFoot;

			return (ushort)PartRig.Spine;
		}

		public static byte[] EncodeRequest(uint id, ushort rig, byte kind, Vector3 point, float duration) {
			var buffer = new Buffer();
			buffer.Write(id);
			buffer.Write(rig);
			buffer.Write(kind);
			buffer.Write(point);
			buffer.Write(duration);
			return buffer.ToArray();
		}

		public static bool DecodeRequest(byte[] raw, out uint id, out ushort rig, out byte kind, out Vector3 point, out float duration) {
			id       = 0;
			rig      = 0;
			kind     = 0;
			point    = default;
			duration = 0f;

			if (raw == null || raw.Length < 23)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id       = buffer.ReadUInt();
			rig      = buffer.ReadUShort();
			kind     = buffer.ReadByte();
			point    = buffer.ReadVector3();
			duration = buffer.ReadFloat();
			return true;
		}

		public static byte[] EncodeRelease(uint id, ushort rig, float multiplier) {
			var buffer = new Buffer();
			buffer.Write(id);
			buffer.Write(rig);
			buffer.Write(multiplier);
			return buffer.ToArray();
		}

		public static bool DecodeRelease(byte[] raw, out uint id, out ushort rig, out float multiplier) {
			id         = 0;
			rig        = 0;
			multiplier = 0f;

			if (raw == null || raw.Length < 10)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id         = buffer.ReadUInt();
			rig        = buffer.ReadUShort();
			multiplier = buffer.ReadFloat();
			return true;
		}

		public static byte[] EncodeOwner(uint id, uint owner) {
			var buffer = new Buffer();
			buffer.Write(id);
			buffer.Write(owner);
			return buffer.ToArray();
		}

		public static bool DecodeOwner(byte[] raw, out uint id, out uint owner) {
			id    = 0;
			owner = 0;

			if (raw == null || raw.Length < 8)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id    = buffer.ReadUInt();
			owner = buffer.ReadUInt();
			return true;
		}

		public static byte[] EncodeState(uint id, Vector3 position, Quaternion rotation, Vector3 velocity, uint owner) {
			var buffer = new Buffer();
			buffer.Write(id);
			buffer.Write(position);
			buffer.Write(rotation);
			buffer.Write(velocity);
			buffer.Write(owner);
			return buffer.ToArray();
		}

		public static bool DecodeState(byte[] raw, out uint id, out Vector3 position, out Quaternion rotation, out Vector3 velocity, out uint owner) {
			id       = 0;
			position = default;
			rotation = Quaternion.identity;
			velocity = default;
			owner    = 0;

			if (raw == null || raw.Length < 48)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id       = buffer.ReadUInt();
			position = buffer.ReadVector3();
			rotation = buffer.ReadQuaternion();
			velocity = buffer.ReadVector3();
			owner    = buffer.ReadUInt();
			return true;
		}

		public static byte[] EncodeSpawn(uint id, string prefabKey, Vector3 position, Quaternion rotation) {
			var buffer = new Buffer();
			buffer.Write(id);
			buffer.Write(prefabKey ?? string.Empty);
			buffer.Write(position);
			buffer.Write(rotation);
			return buffer.ToArray();
		}

		public static bool DecodeSpawn(byte[] raw, out uint id, out string prefabKey, out Vector3 position, out Quaternion rotation) {
			id        = 0;
			prefabKey = string.Empty;
			position  = default;
			rotation  = Quaternion.identity;

			if (raw == null || raw.Length < 6)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id        = buffer.ReadUInt();
			prefabKey = buffer.ReadString();
			position  = buffer.ReadVector3();
			rotation  = buffer.ReadQuaternion();
			return true;
		}

		public static byte[] EncodeDestroy(uint id) {
			var buffer = new Buffer();
			buffer.Write(id);
			return buffer.ToArray();
		}

		public static bool DecodeDestroy(byte[] raw, out uint id) {
			id = 0;

			if (raw == null || raw.Length < 4)
				return false;

			Buffer buffer = raw;
			buffer.Start();
			id = buffer.ReadUInt();
			return true;
		}
	}
}
