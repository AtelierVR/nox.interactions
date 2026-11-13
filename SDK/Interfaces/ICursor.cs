using UnityEngine;

namespace Nox.Interactions {
	public interface ICursor {
		Transform Origin { get; }

		EntityRig Rig { get; set; }

		LayerMask Layers { get; set; }

		CursorState State { get; }

		IInteractTarget Target { get; }

		Vector3 Point { get; }

		Vector3 Normal { get; }

		float Pressure { get; }

		bool TryCast(out RaycastHit contact);

		bool TryPress(in RaycastHit contact);

		bool TryRelease(in RaycastHit contact);
	}

	public interface IRayCursor : ICursor {
		PointerMode Mode { get; set; }

		float Length { get; set; }

		float Radius { get; set; }

		bool TryTarget(IInteractTarget target, in RaycastHit contact);

		void ClearTarget(IInteractTarget target);
	}

	public interface IPokeCursor : ICursor {
		float Radius { get; set; }

		float Depth { get; set; }

		float PressDepth { get; set; }

		bool TryPoke(out RaycastHit contact);
	}
}
