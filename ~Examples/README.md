# Exemples

Assets d'exemple pour le CCK `Nox.CCK.Interactions`.

## Contenu

| Fichier | Rôle |
|---|---|
| `Prefabs/GrabCube.prefab` | cube attrapable complet |
| `Materials/GrabCube.mat` | matériau du cube (URP Lit, bleu) |
| `Materials/GrabCubeHighlight.mat` | matériau de surbrillance (cyan + émission) |
| `Prefabs/GrabMiniCube.prefab` | mini cube ultra léger, insensible à la masse et au drag |
| `Materials/GrabMiniCube.mat` | matériau du mini cube (URP Lit, orange émissif) |
| `Prefabs/GrabHeavyCube.prefab` | cube lourd (25 kg), lent à suivre et difficile à lancer |
| `Materials/GrabHeavyCube.mat` | matériau du cube lourd (URP Lit, gris chaud) |

## Ce que contient `GrabCube.prefab`

- `MeshFilter` et `MeshRenderer` du cube primitif, matériau `GrabCube`.
- `BoxCollider` natif, sur le layer `Grabbable` (7).
- `Rigidbody` : masse 1, gravité active, interpolation `Interpolate`.
- `GrabbableBase` :
  - `body` = le `Rigidbody` du cube
  - `grabColliders` = le `BoxCollider`
  - `highlightMaterial` = `GrabCubeHighlight`
  - `enabled` = true (le composant fait office de flag), `allowed` = `All`, `singleGrabberOnly` = false
  - `mode` = `Free` (l'objet garde la pose qu'il avait au grab)
  - `poseType` = `Freeform`
- suivi dans `LateUpdate` (via `HoldFollower`, ordre d'exécution 10000, donc après tous les grabbers) : le corps est rendu kinématique pendant la prise, `transform` est écrit une seule fois par frame avec lissage `hold.followSpeed` / `hold.turnSpeed` et un blend de `duration` au moment du grab
- `limits.drag` = 1.5, `limits.mass` = 0.1

## Ce que contient `GrabMiniCube.prefab`

Cube de 10 cm (scale 0.1), pensé pour un objet qu'on ne veut ni ralenti ni alourdi.

- `BoxCollider` natif, layer `Grabbable` (7).
- `Rigidbody` : masse 0.05, `linearDamping` 0, `angularDamping` 0.02, `Interpolate`, `ContinuousDynamic` (petit et rapide, il doit être détecté contre les parois fines).
- `GrabbableBase` :
  - `limits.drag` = 0 et `limits.mass` = 0 : aucune masse minimale imposée pendant la prise, aucun drag ajouté
  - `hold.followSpeed` = 30, `hold.turnSpeed` = 1440 (le suivi colle à la main sans traîner)
  - `hold.snap` = `Position` : au grab, l'objet est téléporté sur le `GrabPoint` mais garde son orientation (`Rotation` et `Both` sont aussi disponibles, `None` garde l'offset et le blend)
  - `hold.atHoldDistance` = true : l'objet est ramené à sa propre `hold.distance` au moment du grab (si `hold.distance` = 0, c'est le `min` du grabber qui sert de base)
  - `hold.distance` = 1.2 : distance de maintien demandée par l'objet, ignorée si `hold.atHoldDistance` est false
  - `hold.fixedDistance` = false : la molette peut l'éloigner / le rapprocher (le mettre à true fige la distance)
  - `release.multiplier` = 0.5 et `release.motion.maxSpeed` = 6 : lancer court, l'objet ne part plus à 30 m/s
  - `grabType` = `GrabbableToHand` : l'objet vole vers la main, la main ne bouge pas. Les doigts sont posés **à l'arrivée**, quand l'objet a fini son blend sur le grab point (en `HandToGrabbable` c'est la main qui va chercher l'objet)
  - `mode` = `Free`, `allowed` = `All`

La tenue est purement cinématique (`hold.dynamic` = false) : ni la masse, ni le drag, ni la gravité n'agissent pendant la prise. La masse et le drag ne redeviennent actifs qu'au relâchement, où la vélocité du `Motion` est appliquée.

## Ce que contient `GrabHeavyCube.prefab`

Cube de 60 cm, la version « lourde » de `GrabCube`. Reprise de l'exemple `Barrel` d'AutoHand (`Examples/Prefabs/Kitchen/Barrel.prefab` : masse 100, `parentOnGrab` = false, `ignoreWeight` = false, `jointBreakForce` = 10000), traduite dans les réglages de `GrabbableBase` :

- `Rigidbody` : masse **25**, `linearDamping` 1.5, `angularDamping` 2, `Interpolate`, détection de collision continue. Il ne flotte pas et ne rebondit pas.
- `GrabbableBase` :
  - `hold.physicsMass` = **5** : au-dessus de 5 kg la tenue est **physique**. L'objet reste un corps dynamique, `HoldFollower` est coupé et `GrabJointBinder` pose un `ConfigurableJoint` rigide entre la main et le cube. C'est le poids réel : la main est retenue par le cube, elle ne le suit plus au tracker, et au-delà de `HandGrabber.physicalReleaseDistance` (0.5 m) la main lâche
  - `hold.followSpeed` = **3** : en tenue physique, c'est la vitesse maximale de la main pendant la prise
  - `hold.referenceMass` = **1** : ne sert qu'en repli (si un grabber ne sait pas porter de joint) : la vitesse effective vaut `followSpeed × sqrt(referenceMass / masse)`
  - `grabType` = `Default` : c'est la main qui décide. La masse (25 kg) dépasse `maxReachMass` (2 kg) de `HandGrabber`, donc la main ne tire pas l'objet vers elle, c'est l'objet qui vient (même logique que `HandToGrabbable` refusé pour un objet lourd chez AutoHand)
  - `release.multiplier` = **0.3** et `release.motion.maxSpeed` = **2** : un lancer court, l'objet ne part pas à 6 m/s
  - `singleGrabberOnly` = false : les deux mains peuvent le porter
- `limits.drag` = 1.5 et `limits.mass` = 0.1 : planchers appliqués pendant la prise. `limits.mass` est un **minimum**, pas un maximum : la masse de 25 kg du `Rigidbody` n'est jamais réduite

Repères : `GrabCube` (1 kg) glisse dans la main ; `GrabHeavyCube` (25 kg, tenue physique) est lourd : on le porte lentement, il bute contre le décor, et la main reste accrochée au cube plutôt que de suivre le tracker ; `GrabMiniCube` (0.05 kg, followSpeed 30) colle à la main.

## Tester en quelques étapes

1. Dans la scène, créer un GameObject `Hand` avec un enfant `GrabPoint` placé à environ 10 cm devant.
2. Ajouter `GrabberBase` sur `Hand`, glisser `GrabPoint` dans le champ `grabPoint`. Laisser `targetLayers` vide : il est forcé sur `Grabbable` dans `Awake`.
3. Déposer `GrabCube.prefab` devant la main.
4. Depuis l'inspecteur ou un script :
   - `grabber.TryFindTarget(out var target, out var hit)` puis `grabber.TryGrab(target, hit, 0.1f)`
   - ou `grabber.SetTarget(target, hit)` puis `grabber.TryGrabTarget(0.1f)`
   - relâcher : `grabber.TryReleaseHeld(1f)` : la vitesse de lancer sort du `Motion` du grabbable, avec une fenêtre de grâce si la main vient de s'arrêter

Le grab est aussi observable via `OnGrabbedEvent`, `OnReleasedEvent` et `OnTargetChangedEvent` (UnityEvents exposés par `GrabberBase`), et via `OnGrabbedEvent` / `OnReleasedEvent` / `OnHighlightChangedEvent` sur `GrabbableBase`.

## Variantes rapides

| Objectif | Réglage |
|---|---|
| Objet qui se colle à une prise fixe | `mode` = `Snap` et renseigner `grabAnchor` (transform enfant à l'endroit de la prise) |
| Objet qui reste droit (plateau) | `mode` = `FixedRotation` |
| Objet qui tourne sur place (molette) | `mode` = `FixedPosition` |
| Objet lourd et lent | `hold.referenceMass` = 1 sur un `Rigidbody` de 25 kg (`limits.mass` ne sert qu'à imposer un minimum) | || Objet léger qui suit la main au millimètre | `limits.drag` = 0, `limits.mass` = 0, `hold.followSpeed` = 30 |
| Objet qui se téléporte sur la prise au grab | `hold.snap` = `Position` (ou `Both` pour aligner aussi la rotation) |
| Objet ramené devant la caméra au grab | `hold.atHoldDistance` = true + `hold.distance` (0 = `min` du grabber) |
| Objet qu'on ne peut pas éloigner à la molette | `hold.fixedDistance` = true |
| Lancer plus court | baisser `release.multiplier`, et `release.motion.maxSpeed` avec lui |
| Objet lié physiquement (poignée de porte) | attacher un `ConfigurableJoint` via `GrabJointBinder.Bind(grabber, grabbable, worldAnchor)` |

## Rigidbody et suivi

Un objet tenu est rendu **kinématique** par défaut (`hold.dynamic` = false) : `HoldFollower` écrit `transform` dans `LateUpdate`, après le placement de tous les `GrabPoint`. Un seul écrivain par frame, donc pas de tremblement entre la position physique et la position du point de prise.

`hold.dynamic` = true laisse le `Rigidbody` **non cinématique** pendant la tenue : il pousse et bloque les autres corps, et le décor le repousse. Le suivi reste une écriture de `transform`, donc aucun effort n'est transmis à la main : le poids ne se ressent que par `hold.referenceMass` (voir `GrabHeavyCube`). Garder `Interpolate` et une détection de collision continue dans ce cas.

Le poids, lui, ne dépend pas de `dynamic`. Deux modes : `hold.referenceMass` ralentit le suivi cinématique (l'objet traîne mais reste téléporté), et `hold.physicsMass` passe la tenue en **physique** (corps dynamique + joint rigide + main retenue), pour les objets réellement lourds. `limits.mass` est un plancher de masse pendant la tenue, jamais un plafond.

## Note sur le nom du dossier

Le dossier s'appelle `~Examples` (tilde en préfixe). Unity n'ignore que les dossiers qui **terminent** par `~` : ceux-ci sont bien importés et visibles.
