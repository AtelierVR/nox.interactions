using System;
using Nox.Interactions;
using UnityEngine.Events;

namespace Nox.CCK.Interactions {
	[Serializable]
	public class GrabberGrabEvent : UnityEvent<GrabberBase, GrabbableBase> { }

	[Serializable]
	public class GrabberReleaseEvent : UnityEvent<GrabberBase, GrabbableBase> { }

	[Serializable]
	public class GrabberTargetEvent : UnityEvent<GrabberBase, GrabbableBase> { }

	[Serializable]
	public class GrabberStateEvent : UnityEvent<GrabberBase, GrabState> { }

	[Serializable]
	public class GrabbableGrabEvent : UnityEvent<GrabbableBase, GrabberBase> { }

	[Serializable]
	public class GrabbableReleaseEvent : UnityEvent<GrabbableBase, GrabberBase> { }

	[Serializable]
	public class GrabbableHighlightEvent : UnityEvent<GrabbableBase, GrabberBase, bool> { }

	[Serializable]
	public class CursorTargetEvent : UnityEvent<CursorBase, InteractTarget> { }

	[Serializable]
	public class InteractTargetEvent : UnityEvent<InteractTarget, CursorBase> { }

	[Serializable]
	public class InteractPressureEvent : UnityEvent<InteractTarget, CursorBase, float> { }
}
