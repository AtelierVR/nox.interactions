using Nox.CCK.Development;
using Nox.Interactions;
using UnityEngine;
using Gizmos = Nox.CCK.Development.Gizmos;

namespace Nox.CCK.Interactions {
	[Gizmos("interactions.grabber")]
	public partial class GrabberBase : IGizmos {
		private static readonly Color _freeColor = new(0.55f, 0.55f, 0.55f, 1f);
		private static readonly Color _targetColor = new(1f, 0.9f, 0.2f, 1f);
		private static readonly Color _heldColor = new(0.2f, 0.6f, 1f, 1f);
		private static readonly Color _contactColor = new(1f, 0.35f, 0.1f, 1f);
		private static readonly Color _objectColor = new(1f, 1f, 1f, 1f);
		private static readonly Color _wantedColor = new(1f, 0.75f, 0.2f, 1f);

		public void OnDrawGizmos() {
			if (!isActiveAndEnabled)
				return;

			var point = GrabPoint;
			var origin = HoldingPoint;
			var forward = point.forward;
			var reach = Mathf.Max(ray.reach, 0.001f);

			Gizmos.Color = state == GrabState.Held
				? _heldColor
				: target != null ? _targetColor : _freeColor;

			Gizmos.DrawWireSphere(origin, reach);
			Gizmos.DrawSphere(origin, 0.006f);
			Gizmos.DrawLine(point.position, origin);
			Gizmos.DrawLine(origin, origin + forward * reach);
			Gizmos.DrawDottedLine(origin, origin + Vector3.Lerp(forward, point.right, 0.65f) * reach, 6f);
			Gizmos.DrawArrow(origin + forward * reach, forward, reach * 0.15f, reach * 0.05f);

			if (target?.Target) {
				Gizmos.Color = _targetColor;
				Gizmos.DrawWireSphere(target.Target.position, 0.025f);
				Gizmos.DrawDottedLine(origin, target.Target.position, 4f);
			}

			if (hit.collider) {
				var contact = hit.point;
				var normal = hit.normal;

				Gizmos.Color = _contactColor;
				Gizmos.DrawLine(origin, contact);
				Gizmos.DrawSphere(contact, 0.008f);
				Gizmos.DrawWireDisc(contact, normal, 0.02f);
				Gizmos.DrawArrow(contact, normal, 0.05f, 0.015f);
			}

			if (held?.Target == null || held is not GrabbableBase grabbable)
				return;

			var heldTarget = held.Target;

			Gizmos.Color = _objectColor;
			DrawAxes(heldTarget.position, heldTarget.rotation, 0.07f);

			if (!grabbable.TryGetDebugHoldPose(out var holdPosition, out var holdRotation))
				return;

			Gizmos.Color = _wantedColor;
			DrawAxes(holdPosition, holdRotation, 0.05f);
			Gizmos.DrawDottedLine(heldTarget.position, holdPosition, 5f);

			if (!grabbable.TryGetDebugRequestedPose(this, out var requestPosition, out var requestRotation))
				return;

			Gizmos.DrawWireSphere(requestPosition, 0.02f);
			DrawAxes(requestPosition, requestRotation, 0.03f);
			Gizmos.DrawLine(holdPosition, requestPosition);
		}

		private static void DrawAxes(Vector3 position, Quaternion rotation, float size) {
			Gizmos.DrawLine(position, position + rotation * Vector3.right * size);
			Gizmos.DrawLine(position, position + rotation * Vector3.up * size);
			Gizmos.DrawLine(position, position + rotation * Vector3.forward * size);
			Gizmos.DrawWireSphere(position, size * 0.2f);
		}
	}
}
