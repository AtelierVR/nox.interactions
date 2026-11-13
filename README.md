# nox.interactions

Contrats de grab génériques + implémentations de référence. Le but est de sortir la logique d'interaction d'AutoHand pour qu'elle serve à n'importe quelle plateforme : main XR, pointeur desktop, NPC, script, réseau.

## Deux couches

```
SDK/   Nox.Interactions        interfaces + enums + constantes statiques
CCK/   Nox.CCK.Interactions    implémentations de référence et utilitaires
```

Le SDK ne référence **rien** (pas même `Nox.CCK`) : il ne dépend que de `UnityEngine`.

Les enums et la classe de constantes vivent dans le SDK **par nécessité** : ils apparaissent dans les signatures d'interface, et un enum utilisé par le SDK ne peut pas être défini dans le CCK (cycle d'assemblies, le CCK référence toujours le SDK).

## SDK

### Enums

| Enum | Valeurs | Rôle |
|---|---|---|
| `EntityRig` | flags `None`, `Head`, `LeftHand`, `RightHand`, `LeftFoot`, `RightFoot`, `Body`, `Hands`, `Feet`, `All` | partie de rig d'une entité autorisée à tenir ; `None` = grabber non rattaché à un rig (pointeur, script) |
| `GrabState` | `Free`, `Pending`, `Held`, `Releasing` | état partagé grabber/grabbable |
| `GrabMode` | `Free`, `Snap`, `FixedRotation`, `FixedPosition` | comment l'objet suit son grab point |
| `GrabPoseType` | `Freeform`, `Pinch`, `Custom` | forme de main attendue (procédurale ou capturée) |
| `GrabSource` | `Direct`, `Ray`, `Script`, `Remote` | origine de l'ordre de grab, utile au réseau |
| `PointerMode` | `Near`, `Far` | rayon court avec sphere de proximité, ou rayon long |
| `CursorState` | `Idle`, `Hover`, `Press` | état d'un curseur sur sa cible |

### `InteractionConstants`

Noms de layers (`Grabbable`, `Grabbing`, `Hand`), portée par défaut, durées d'animation min/max, force de rupture de joint, résolution de conforming, etc. Expose aussi `GrabbableMask`, `GrabbingMask` et `ToMask(int)` pour éviter les duplications de `1 << layer`.

### Interfaces

| Interface | Rôle |
|---|---|
| `IGrabber` | celui qui tient : grab point, rig, état, objet tenu, `TryGrab`, `TryRelease` |
| `IGrabberTargeting` | celui qui vise : `TryFindTarget`, `SetTarget`, `ClearTarget`, layers |
| `IGrabberBody` | colliders du corps du grabber, ignorés par l'objet tenu (optionnel) |
| `IGrabberPalm` | normale de paume, pour poser l'objet sur la main (optionnel) |
| `IGrabbable` | contrat **minimal** du tenu : `Target`, `State`, `CanBeGrabbedBy`, `TryAddGrabber`, `TryRemoveGrabber` |
| `IGrabGroup` | suivi du multi-grab (optionnel) : `HeldCount`, `Grabbers`, `SingleGrabberOnly`, `RemoveAllGrabbers` |
| `IGrabBody` | `Rigidbody` du tenu, pour les liens physiques (optionnel) |
| `IGrabColliders` | colliders de prise, pour le calcul de bounds distantes (optionnel) |
| `IGrabAccess` | rigs autorisés (`Allowed`) en écriture (optionnel) |
| `IGrabAttachment` | `Mode` et `PoseType`, la façon dont l'objet se pose dans la main (optionnel) |
| `IGrabHold` | `Hold` : snap, vitesses de suivi, distance de maintien (optionnel) |
| `IGrabThrow` | `ThrowMultiplier` + `ApplyThrow(linear, angular, multiplier)` pour imposer une vitesse (optionnel) |
| `IGrabHighlight` | surbrillance pilotée par le grabber (optionnel) |
| `IGrabPoseProvider` | pose de main par rig, en espace objet (optionnel) |
| `IGrabConformer` | celui qui déforme ses doigts sur l'objet (optionnel) |
| `IMotion` | mémoire de mouvement : `Track`, `Clear`, `TryGet(out linear, out angular)` |
| `ICursor` | ce qui vise : `Origin`, `Rig`, `Point`, `Normal`, `Pressure`, `TryCast`, `TryPress`, `TryRelease` |
| `IRayCursor` | curseur à rayon : `Mode` (`Near`/`Far`), `Length`, `Radius`, `TryTarget`, `ClearTarget` |
| `IPokeCursor` | curseur à doigt : `Radius`, `Depth`, `PressDepth`, `TryPoke` |
| `IInteractTarget` | ce qui réagit : `CanInteract`, `OnHoverEnter`, `OnHoverExit`, `OnPress`, `OnRelease` |
| `IPokeTarget` | cible qui reçoit en plus la pression du doigt : `OnPressure` |

Règles de conception :

- **`IGrabbable` est minimal, le reste est optionnel.** Un grabber ne dépend que de `Target`, `State`, `CanBeGrabbedBy`, `TryAddGrabber` et `TryRemoveGrabber` ; les capacités (corps physique, colliders, réglages de tenue, lancer, surbrillance, pose de main) sont des interfaces séparées testées par `is`. Un proxy réseau, un trigger de porte ou un objet piloté par script n'implémente que ce dont il a besoin, et un consommateur écrit `if (grabbable is IGrabHold hold)` au lieu de supposer l'implémentation concrète.
- **Aucune méthode sans paramètre.** Tout ce qui agit prend ses arguments (`TryGrab(grabbable, contact, duration)`, `TryRelease(grabbable, multiplier, velocity, angularVelocity)`).
- Les valeurs simples se lisent et s'écrivent par **propriétés** (`ReachDistance`, `TargetLayers`, `Mode`, `Allowed`), pas par des `SetX()` sans paramètre.
- Un grabbable se désactive par le **composant** : `enabled = false` le retire de `CanBeGrabbedBy` et `OnDisable` libère les mains et coupe les surbrillances. Pas de flag `isGrabbable` séparé.
- Le filtrage d'accès se fait par **flags** : `IGrabbable.Allowed` est un `EntityRig` et `CanBeGrabbedBy` teste `(Allowed & grabber.Rig) != 0`. Un grabber avec `Rig == None` (pointeur, script) passe toujours.
- Toute opération qui peut échouer retourne `bool`. Aucune exception pour un flux normal.
- Le **contact** est un `UnityEngine.RaycastHit` passé en `in` : pas de struct maison dans le SDK. Un contact invalide est un `default(RaycastHit)`, c'est-à-dire `contact.collider == null` (`GrabContactUtility.HasSurface`).
- Le grabbable est **maître de son mode** : c'est lui qui décide `Free` / `Snap` etc., pas le grabber. Une même main tient donc un verre et une arme différemment.

## CCK

| Type | Rôle |
|---|---|
| `GrabberBase` | implémentation concrète de `IGrabber` + `IGrabberTargeting` |
| `GrabbableBase` | implémentation concrète de `IGrabbable` + `IGrabGroup`, `IGrabBody`, `IGrabColliders`, `IGrabAccess`, `IGrabAttachment`, `IGrabHold`, `IGrabThrow`, `IGrabHighlight`, `IGrabPoseProvider` |
| `GrabbablePose` | `ScriptableObject` : une `List<RigPose>` (rig + position + rotation locales), une entrée par rig |
| `FingerConformer` | `IGrabConformer` : enroule des joints jusqu'au contact de la pointe |
| `GrabJointBinder` | création/rupture du `ConfigurableJoint` |
| `GrabContactUtility` | lecture d'un `RaycastHit` éventuellement invalide |
| `GrabbableHighlight` | halo de survol : copies de mesh invisibles sur le layer de glow, maskées par `OutlineGlowFeature` (tout est `virtual`) |
| `Motion` | mémoire de mouvement (échantillons position/rotation) du grabbable, implémentation par défaut de `IMotion` |
| `CursorBase` | `ICursor` : layers, rig, cible courante, dispatch hover/press/release |
| `RayCursor` | `IRayCursor` : near (sphere + raycast au plus proche) ou far (rayon simple) |
| `PokeCursor` | `IPokeCursor` : doigt, pression, press à `pressDepth`, grab optionnel |
| `InteractTarget` | `IPokeTarget` : cibles 3D et UI, UnityEvents + filtre `Allowed` |
| `UiButtonTarget` | `InteractTarget` qui déclenche `Button.onClick` |

`GrabbableBase` et `GrabberBase` sont découpés en **partials** par responsabilité :

```
Grabbable/GrabbableBase.cs            état, colliders, CanBeGrabbedBy, pose
Grabbable/GrabbableBase.Grab.cs       TryAddGrabber / TryRemoveGrabber / throw
Grabbable/GrabbableBase.Follow.cs     FixedUpdate : suit le grab point selon GrabMode
Grabbable/GrabbableBase.Highlight.cs  surbrillance : comptage par grabber + hooks virtuels
Grabbable/GrabbableHighlight.cs       rendu du halo (copies de mesh sur le layer de glow)
Grabbable/GrabbableBase.Events.cs     UnityEvents + raiseurs

Grabber/GrabberBase.cs                références et propriétés
Grabber/GrabberBase.Targeting.cs      overlap sphere + raycast + score de ciblage
Grabber/GrabberBase.Grab.cs           TryGrab / TryRelease
Grabber/GrabberBase.Events.cs         UnityEvents + raiseurs
```

### Curseurs : near / far, poke et UI

Un **curseur** est ce qui vise : une main, un pointeur, un doigt. Deux implémentations concrètes partagent la même cible.

```
CursorBase          couche commune : layers, rig, état, cible courante, dispatch
├─ RayCursor        near (sphere + raycast au plus proche) ou far (rayon simple)
└─ PokeCursor       doigt : SphereCast sur depth, pression, press quand la surface est à pressDepth
```

- `ICursor` porte `Point`, `Normal`, `State`, `Pressure` et `Rig` : le filtre `Allowed` des cibles réutilise `EntityRig`, exactement comme pour le grab.
- `IInteractTarget` reçoit `OnHoverEnter` / `OnHoverExit` / `OnPress` / `OnRelease` ; `IPokeTarget` ajoute `OnPressure`.
- `RayCursor` en mode `Near` fait un overlap sphere de rayon `Radius`, puis un raycast par collider vers son `ClosestPoint` : le plus proche gagne. En mode `Far`, un simple `Physics.Raycast` sur `Length`.
- `PokeCursor` : pression = `1 - distance / depth`, clampé 0..1. Le press part quand `distance <= PressDepth` (12 mm par défaut) : c'est la marge qui évite de cliquer devant la surface, un press trop tôt vient de `PressDepth` plus grand que la pénétration réelle du doigt.
- L'axe du doigt est `Tip.TransformDirection(Axis)` (`forward` par défaut). Un doigt AutoHand pointe sur son **`up`** : mettre `Axis = (0, 1, 0)` sur ces rigs.
- **UI** : un élément d'interface n'a pas de contrat à part, il implémente `IInteractTarget` comme un bouton 3D. `UiButtonTarget` fait le pont avec `UnityEngine.UI.Button` (`onClick` au release, `IsInteractable()` dans `CanInteract`) : un collider sur le bouton et un curseur qui pointe dessus suffisent, le GraphicRaycaster n'est pas sollicité.
- **Grab** : volontairement séparé. `GrabberBase` gère le grab (`TryFindTarget` + `TryGrab`), les curseurs gèrent l'activation. `PokeCursor.GrabOnPress` permet en plus de grab en piquant, la main reste maître du release.

```csharp
RayCursor ray = null;                                   // near ou far, Update fait le hover
if (ray.TryCast(out RaycastHit hit))
    ray.TryPress(hit);                                  // OnPress sur la cible + UnityEvents

PokeCursor poke = null;                                 // Update gère hover et press
if (poke.TryPoke(out RaycastHit contact))
    Debug.DrawLine(poke.Point, contact.point);           // point et normale disponibles pour le visuel
```

### Pourquoi les UnityEvents utilisent des types CCK

`UnityEvent<IGrabber, IGrabbable>` n'est pas sérialisable par l'inspecteur (Unity ne sait pas sérialiser un paramètre d'interface). Les événements exposent donc les types concrets du CCK : `UnityEvent<GrabberBase, GrabbableBase>`. Le SDK reste pur interface, le CCK fournit la surface éditeur.

Si un grabber ou un grabbable d'une autre plateforme n'hérite pas de `GrabbableBase`, l'argument de l'événement vaut `null` : c'est un événement de confort, pas le chemin de données principal.

### Highlight

`IGrabHighlight` est porté par `GrabbableBase` : `SetHighlight(grabber, visible, material)` compte les demandes par grabber (un curseur desktop et un rayon XR peuvent viser le même objet) et n'allume le halo que pour la première, ne l'éteint qu'à la dernière.

Le rendu par défaut est le **halo de `OutlineGlowFeature`** (celui de `nox.cck`, réimplémenté ici) : `GrabbableHighlight` pose une copie invisible de chaque `MeshRenderer` (layer UI, `sharedMesh`, ombres / probes / motion vectors coupés) avec le **matériau `Resources/GrabbableGlow.mat`** — shader `Hidden/Nox/GlowMaskProxy`, queue `2002` — que le feature rend en masque, floute et composite en halo + contour. Le matériau est un asset, pas une instance créée à l'exécution : le champ `material` du composant le surcharge au besoin. Les copies ne sont construites qu'au premier survol, sont partagées, et l'objet s'annonce dans `GlowSources` pour que le feature ne soit enfilé que si quelque chose peut briller.

Le shader du feature (`Hidden/!Neon/OutlineGlowPost`) est **dans `Resources/` de ce package** (même nom, même GUID : l'asset de renderer continue de le référencer). Le masque est la silhouette **exacte** de l'objet (aucun déplacement de sommet : un poussé le long des normales fait des bosses aux coins et ouvre un liseré de fond entre l'objet et son halo), l'épaisseur de l'anneau est en pixels écran (`_GlowParams.y`).

Pour que le **halo** garde la même largeur au milieu d'une arête et autour d'un sommet, le composite ne lit pas le flou seul : il prend le **max du flou sur un petit cercle** (8 directions, rayon `clamp(15 % de glowWidth, 2, 6 px)`). Un gaussien perd de la couverture sur un coin convexe — au même recul un coin donne moins que le milieu d'une arête — et le halo s'y resserrait ; le max rend au coin la valeur de l'arête qu'il borde.

L'anneau de contour (trait plein) est une **dilatation circulaire** du masque : l'anneau est « tout pixel à moins de `outlineW` de l'objet », donc d'une largeur constante perpendiculairement à l'arête comme autour d'un sommet. Elle est échantillonnée sur **24 directions × 3 rayons** : avec deux rayons (l'original), le bord extérieur est un 16-gone et la largeur apparente dépend de l'orientation de l'arête (un segment paraît plus large qu'un autre). ⚠️ Ne pas étirer la portée par direction (`1/dot(dir, normale)`) pour « suivre l'angle du coin » : près d'un coin les échantillons étirés dépassent l'objet, ne trouvent rien, et l'anneau s'y amincit — c'est l'inverse de l'effet voulu.

Le halo est animé par un **bruit tuilable** (`Resources/T_Noise.png`, value noise 3 octaves 256², Repeat/bilinear/linéaire) que le composite lit avec le temps : la lecture du flou est décalée par `(noise - 0.5) × distorsion × glowWidth` (mouvement) et l'intensité est modulée par le bruit (respiration). Réglages dans `_NoiseParams` = `(échelle, vitesse, distorsion, modulation)`, tous à zéro par défaut ⇒ rendu inchangé tant que rien n'est publié. La passe visible étant celle du feature (son propre matériau), `GrabbableHighlight.ApplyNoise()` publie la texture et les paramètres en **globals** (`Shader.SetGlobalTexture/Vector`), réglables par les virtuels `NoiseScale/NoiseSpeed/NoiseDistortion/NoiseAmount`.

Tout est **`virtual`**, à deux niveaux :

| Niveau | Méthodes | Pour changer |
|---|---|---|
| `GrabbableBase` | `CanHighlight`, `ResolveHighlightMaterial`, `CreateHighlightView`, `UpdateHighlight`, `IsUsedByAny` | le matériau retenu, la vue utilisée, quand le halo est autorisé |
| `GrabbableHighlight` | `Show`, `Hide`, `LayerName`, `GlowMaterial` (ou `GlowResourcePath`), `ResolveGlowMaterial`, `CollectSources`, `IsSource`, `CreateProxy`, `DestroyProxy`, `Build`, `Clear` | ce qui est copié, où et avec quel matériau (mesh skinné, matériau émissif, autre pipeline) |

### Conforming

`FingerConformer` capture la pose ouverte à l'`Awake`, expose des Eulers de pose fermée, puis balaie `Open → Closed` en deux passes (grossière `5/steps`, fine `1/steps`) en testant `Physics.OverlapSphereNonAlloc` au bout du doigt. C'est la version générique de `Finger.BendFingerUntilHit` d'AutoHand : chaque doigt trouve son propre angle de contact, donc une main plate reste plate sur un panneau et s'enroule sur une arête.

### Suivi de l'objet

Un grabbable **ne se reparente jamais** : il lit le `GrabPoint` de son grabber et s'aligne dessus. Par défaut l'objet est rendu kinématique pendant la tenue et `HoldFollower` écrit `transform` en `LateUpdate` (ordre 10000), une seule fois par frame. `hold.dynamic` = true laisse le corps **non cinématique** (il reste un corps physique pour le décor et les autres corps), mais le suivi est toujours une écriture de `transform` : aucun effort physique n'est transmis à la main. La hiérarchie de scène reste intacte, ce qui évite de traîner un objet par un transform qui bouge en physique, et un grabbable peut être tenu par n'importe quel grabber sans que l'arbre change.

Le **poids** a deux modes.

1. **Émulation cinématique** : `hold.referenceMass` (en kg, 0 = la masse est ignorée). Jusqu'à cette masse l'objet suit à `followSpeed` / `turnSpeed`, au-delà la vitesse effective est divisée par `sqrt(masse / referenceMass)`. L'objet ralentit, mais reste téléporté : il ne bloque pas la main et traverse le décor.
2. **Tenue physique** : `hold.physicsMass` (en kg, 0 = jamais). Au-dessus de cette masse, et si le grabber déclare `IPhysicsGrabber.PhysicsJoint = true`, l'objet **reste un corps dynamique** et `HoldFollower` est coupé : c'est un `ConfigurableJoint` rigide (6 axes verrouillés) créé par le grabber qui porte la prise. Le `GrabPoint` devient un **point de suivi** et non plus une téléportation : un grabber capable de physique (la main nox) est alors lui-même poussé en vitesse vers le tracker, donc un objet lourd ou coincé le retient, et au-delà de sa distance de décrochage il lâche. Avec `physicsMass`, `followSpeed` sert de **vitesse maximale du grabber** pendant la tenue.

`limits.mass` est dans les deux cas un **plancher** de masse pendant la tenue (comme `minHeldMass` d'AutoHand), jamais un plafond : une masse de 25 kg n'est pas réduite par `limits.mass`.

Les réglages d'un grabbable sont groupés dans des structs sérialisés : `hold` (`GrabHold` : dynamic, followSpeed, turnSpeed, referenceMass, physicsMass, snap, fixedDistance, atHoldDistance, distance), `limits` (`GrabLimits` : drag, mass) et `release` (`GrabRelease` : enabled, multiplier, motion). Côté grabber : `ray` (`GrabRay` : layers, reach, offset) et `highlight` (`GrabHighlight` : enabled, material).

| Outil | Comportement |
|---|---|
| `hold` | tenue : snap, vitesse de suivi, poids (`referenceMass`), tenue physique (`physicsMass`), distance de maintien |
| `IPhysicsGrabber` | un grabber qui sait porter une prise par joint (`PhysicsJoint`). La main nox l'implémente ; un grabber de distance ne le déclare pas |
| `GrabJointBinder.Bind(..., rigid)` | joint de prise : `rigid = true` verrouille aussi les 3 axes angulaires (tenue lourde), `false` laisse l'objet tourner librement (poignée de porte) |

### Throw avec mémoire de mouvement

C'est le **grabbable** qui possède la mémoire. Pendant la tenue, `GrabbableBase.LateUpdate` échantillonne la position et la rotation du `GrabPoint` du premier grabber dans son `Motion`. Au dernier lâcher, `TryRemoveGrabber` calcule la vitesse sur les `span` dernières secondes (0.1 s) et applique le throw.

C'est le « coyote » du throw : si la main s'est arrêtée juste avant le relâchement (mur, décélération, frame de repos), le calcul est élargi jusqu'à `span + grace` (0.25 s) tant que le déplacement reste sous `minMove`. Un objet lâché après un coup sec part donc quand même, comme un saut déclenché juste après avoir quitté le bord.

| Membre | Défaut | Rôle |
|---|---|---|
| `release.enabled` | true | coupe l'enregistrement et le throw |
| `release.multiplier` | 1 | facteur appliqué à la vitesse mesurée |
| `Motion` | instance `Motion` | implémentation utilisée (`IMotion`), remplaçable à l'exécution |
| `memory` | 0.3 | durée maximale des échantillons conservés |
| `span` | 0.1 | fenêtre de calcul normale |
| `grace` | 0.15 | prolongation quand la main est immobile |
| `minMove` | 0.002 | déplacement en dessous duquel on remonte dans l'historique |
| `maxSpeed` | 20 | plafond de vitesse linéaire |

Personnalisation : implémenter `IMotion` (`Track`, `Clear`, `TryGet`) et l'affecter à `grabbable.Motion` ; `release.enabled = false` arrête tout. Les surcharges à quatre paramètres (`linear`, `angular`) imposent une vitesse explicite (réseau, script) et remplacent le throw calculé.

Attention au nom : `UnityEngine.Motion` existe déjà (base des `AnimationClip`). Dans un fichier qui fait à la fois `using Nox.CCK.Interactions;` et `using UnityEngine;`, le nom `Motion` est ambigu : passer par `IMotion` ou `grabbable.Motion` évite d'avoir à qualifier le type.

## Exemple d'usage

```csharp
public class PointerGrabber : MonoBehaviour
{
    private readonly GrabberBase grabber;
    private readonly GrabbableBase grabbable;

    public void Press(float duration)
    {
        if (!grabber.TryFindTarget(out var target, out var contact)) return;
        grabber.TryGrab(target, contact, duration);
    }

    public void Release(float multiplier)
    {
        grabber.TryReleaseHeld(multiplier);
    }

    private void Bind(Vector3 worldAnchor)
    {
        var joint = GrabJointBinder.Bind(grabber, grabbable, worldAnchor);

        void OnGrabbed(GrabberBase source, GrabbableBase item)
        {
            item.OnReleasedEvent.AddListener(OnReleased);
        }

        void OnReleased(GrabbableBase item, GrabberBase source)
        {
            GrabJointBinder.Unbind(joint);
            item.OnReleasedEvent.RemoveListener(OnReleased);
        }

        grabber.OnGrabbedEvent.AddListener(OnGrabbed);
    }
}
```

## Relation avec `nox.cck/Interaction`

`Nox.CCK.Interaction` (singulier) contient déjà une version minimale de `IGrabber` / `IGrabbable` orientée « l'objet se déplace lui-même », utilisée par `GrabbableObject`, `PlayerMenu` et le réseau. Ce mod-ci est volontairement **autonome** et plus large (état, pose, conforming, joints, highlight) pour servir de base à une extraction.

Tant que les deux existent :

- **aucun pont entre les deux systèmes** : un composant migre entièrement (nouveau contrat, nouveau grabber), il ne porte pas une seconde vue de l'ancien contrat. `PersonalMirror` (`nox.mirror`) est le premier objet migré : il n'est plus visible des grabbers de `nox.cck`, c'est le grabber qui doit migrer.
- ne pas mélanger les deux jeux d'interfaces dans un même composant ;
- si une migration est décidée, `Nox.CCK.Interaction.IGrabber` correspond à `Nox.Interactions.IGrabber`, et `Nox.CCK.Interaction.IGrabbable` à `Nox.Interactions.IGrabbable` ;
- les enums `Nox.CCK.Interaction.Handedness` / `GrabMode` / `GrabInputMode` se projettent directement sur `Nox.Interactions.Handedness` / `GrabMode`.

## Conventions de code

- Un fichier par responsabilité, découpage `partial` pour les composants.
- Propriétés pour les valeurs, méthodes paramétrées pour les actions.
- Aucun commentaire dans le code : le README porte l'explication.
