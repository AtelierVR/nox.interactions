using System.Collections.Generic;
using Nox.CCK.Interaction;
using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.CCK.Interactions {
	/// <summary>
	/// Hover glow of a grabbable, drawn by the <c>OutlineGlowFeature</c> renderer feature: invisible copies of the
	/// meshes are put on the glow layer while a grabber points at the object, the feature renders that layer into a
	/// mask, blurs it and composites a glow and an outline.
	/// <para>
	/// The copies carry the mask proxy material, whose shader sits at the glow render queue, so sharing the layer
	/// with the rest of the UI does not make every panel glow: the feature only draws the queue, never the layer as
	/// a whole.
	/// </para>
	/// <para>
	/// Everything the drawing depends on is virtual - the material, the layer, the renderers that are copied and the
	/// copies themselves - so a grabbable that has to glow differently (a skinned mesh, an emissive material, another
	/// pipeline) derives this component instead of reimplementing the bookkeeping.
	/// </para>
	/// </summary>
	[AddComponentMenu("Nox/Interactions/Grabbable Highlight")]
	[DisallowMultipleComponent]
	public class GrabbableHighlight : MonoBehaviour {
		/// <summary>Layer of the glow copies: the UI layer, which no reflection renders.</summary>
		public const string GlowLayerName = Layers.Ui;

		/// <summary>Render queue of the glow copies (<c>Geometry+2</c>), the one <c>OutlineGlowFeature</c> masks.</summary>
		public const int GlowQueue = 2002;

		/// <summary>Name of the copies, kept out of the next build.</summary>
		protected const string ProxyName = "Glow Proxy";

		/// <summary>Material shipped with the package, used when the component has none.</summary>
		private const string DefaultGlowPath = "GrabbableGlow";

		/// <summary>Tileable noise the glow shader reads to make the halo move and breathe.</summary>
		private const string NoisePath = "T_Noise";

		private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
		private static readonly int NoiseParamsId = Shader.PropertyToID("_NoiseParams");

		/// <summary>Whether the noise was already published to the shader (it is global, once is enough).</summary>
		private static bool noiseApplied;

		[SerializeField, Tooltip("Material of the glow copies: the mask proxy of OutlineGlowFeature. Empty uses the one shipped in Resources.")]
		private Material material;

		/// <summary>Material of the copies, resolved once and shared by every source of the process.</summary>
		private static Material sharedGlow;

		private readonly List<GameObject> proxies = new();

		private bool built;

		/// <summary>Material of the glow copies.</summary>
		public Material GlowMaterial {
			get => material;
			set => material = value;
		}

		/// <summary>Whether the copies are currently drawn.</summary>
		public bool IsVisible
		=> built;

		/// <summary>Builds the glow copies, once, and registers the object as a glow source.</summary>
		public virtual void Show(Material requested) {
			if (built)
				return;

			ApplyNoise();
			Build(requested);
		}

		/// <summary>Removes the glow copies and unregisters the object.</summary>
		public virtual void Hide() {
			if (!built)
				return;

			Clear();
		}

		/// <summary>Layer the copies are put on, the one the glow feature masks.</summary>
		protected virtual string LayerName
		=> GlowLayerName;

		/// <summary>Scale of the noise across the screen: how many tiles wide the breathing is.</summary>
		protected virtual float NoiseScale
			=> 3f;

		/// <summary>Speed the noise scrolls at, in tiles per second.</summary>
		protected virtual float NoiseSpeed
			=> 0.12f;

		/// <summary>Wobble of the halo, as a fraction of the glow width.</summary>
		protected virtual float NoiseDistortion
			=> 0.35f;

		/// <summary>How much the noise modulates the brightness of the halo (0 = no breathing).</summary>
		protected virtual float NoiseAmount
			=> 0.45f;

		/// <summary>
		/// Publishes the noise to the glow shader. The visible pass belongs to the renderer feature - it draws the
		/// mask with its own material - so the texture and its parameters are set as globals instead of living on a
		/// material no draw of ours uses.
		/// </summary>
		protected virtual void ApplyNoise() {
			if (noiseApplied)
				return;

			var noise = Resources.Load<Texture>(NoisePath);

			if (!noise)
				return;

			noiseApplied = true;

			Shader.SetGlobalTexture(NoiseTexId, noise);
			Shader.SetGlobalVector(NoiseParamsId, new Vector4(NoiseScale, NoiseSpeed, NoiseDistortion, NoiseAmount));
		}

		/// <summary>Resources path of the material used when the component has none.</summary>
		protected virtual string GlowResourcePath
			=> DefaultGlowPath;

		/// <summary>
		/// Material of the copies. <paramref name="requested"/> is the highlight material of the grabber that asked
		/// for the glow, for a subclass that wants a colour per grabber; the default copies only carry a mask.
		/// </summary>
		protected virtual Material ResolveGlowMaterial(Material requested)
		=> material ? material : DefaultGlowMaterial;

		/// <summary>Material of the package, loaded once from <see cref="GlowResourcePath"/>.</summary>
		protected virtual Material DefaultGlowMaterial {
			get {
				if (sharedGlow)
					return sharedGlow;

				sharedGlow = Resources.Load<Material>(GlowResourcePath);

				if (!sharedGlow)
					Logger.LogWarning(
					$"[{nameof(GrabbableHighlight)}] Material '{GlowResourcePath}' not found in Resources: no glow will be drawn.",
					this,
					tag: nameof(GrabbableHighlight)
					);

				return sharedGlow;
			}
		}

		/// <summary>Renderers of the object that get a copy.</summary>
		protected virtual MeshRenderer[] CollectSources()
		=> GetComponentsInChildren<MeshRenderer>(true);

		/// <summary>Whether a renderer of the object is a source, the copies themselves being skipped.</summary>
		protected virtual bool IsSource(Renderer source)
		=> source && source.name != ProxyName;

		/// <summary>Creates the copy of one renderer, and returns it (null when there is nothing to draw).</summary>
		protected virtual GameObject CreateProxy(Renderer source, Material glow) {
			var filter = source.GetComponent<MeshFilter>();

			if (!filter || !filter.sharedMesh)
				return null;

			var node = new GameObject(ProxyName) { hideFlags = HideFlags.DontSave };
			node.transform.SetLayer(LayerName);
			node.transform.SetParent(source.transform, false);

			node.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

			var renderer = node.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = glow;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			renderer.lightProbeUsage = LightProbeUsage.Off;
			renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
			renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

			return node;
		}

		/// <summary>Destroys one copy.</summary>
		protected virtual void DestroyProxy(GameObject proxy) {
			if (!proxy)
				return;

			proxy.Destroy();
		}

		/// <summary>Creates the copies of every source and announces the object as a glow source.</summary>
		protected virtual void Build(Material requested) {
			built = true;

			var glow = ResolveGlowMaterial(requested);

			if (!glow)
				return;

			var sources = CollectSources();

			for (var i = 0; i < sources.Length; i++) {
				var source = sources[i];

				if (!IsSource(source))
					continue;

				var proxy = CreateProxy(source, glow);

				if (proxy)
					proxies.Add(proxy);
			}

			if (proxies.Count > 0)
				GlowSources.Add();
		}

		/// <summary>Destroys every copy and stops announcing the object as a glow source.</summary>
		protected virtual void Clear() {
			built = false;

			if (proxies.Count > 0)
				GlowSources.Remove();

			for (var i = 0; i < proxies.Count; i++)
				DestroyProxy(proxies[i]);

			proxies.Clear();
		}

		protected virtual void OnDisable()
		=> Clear();
	}
}
