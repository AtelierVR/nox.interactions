using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Interactions;
using UnityEngine;

namespace Nox.CCK.Interactions {
	public partial class GrabbableBase {
		private readonly Dictionary<IGrabber, Material> _highlighters = new();

		private GrabbableHighlight _highlightView;

		public Material HighlightMaterial {
			get => highlightMaterial;
			set => highlightMaterial = value;
		}

		public bool IsHighlightedBy(IGrabber grabber)
			=> grabber != null && _highlighters.ContainsKey(grabber);

		public void SetHighlight(IGrabber grabber, bool enabled, Material material) {
			if (grabber == null) return;

			if (!enabled) {
				if (!_highlighters.Remove(grabber, out Material used) || IsUsedByAny(used))
					return;

				if (_highlighters.Count == 0)
					UpdateHighlight(false, used);

				RaiseHighlightChanged(grabber, false);
				return;
			}

			if (!CanHighlight) return;

			var target = ResolveHighlightMaterial(material);

			_highlighters[grabber] = target;
			UpdateHighlight(true, target);
			RaiseHighlightChanged(grabber, true);
		}

		protected virtual bool CanHighlight
			=> isActiveAndEnabled;

		protected virtual Material ResolveHighlightMaterial(Material material)
			=> material != null
				? material
				: highlightMaterial;

		protected virtual GrabbableHighlight HighlightView {
			get {
				if (!_highlightView)
					_highlightView = CreateHighlightView();

				return _highlightView;
			}
		}

		protected virtual GrabbableHighlight CreateHighlightView()
			=> gameObject.GetOrAddComponent<GrabbableHighlight>();

		protected virtual void UpdateHighlight(bool visible, Material material) {
			var view = HighlightView;

			if (!view) return;

			if (visible)
				view.Show(material);
			else
				view.Hide();
		}

		protected virtual bool IsUsedByAny(Material material) {
			foreach (var entry in _highlighters)
				if (entry.Value == material)
					return true;

			return false;
		}

		private void ClearHighlights() {
			_highlighters.Clear();

			if (_highlightView)
				_highlightView.Hide();
		}
	}
}
