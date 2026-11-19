# Exemples

Assets d'exemple pour le CCK `Nox.CCK.Interactions`.

## Contenu

| Fichier | Rôle |
|---|---|
| `Prefabs/GrabCube.prefab` | cube attrapable complet |
| `Materials/GrabCube.mat` | matériau du cube (URP Lit, bleu) |
| `Materials/GrabCubeHighlight.mat` | matériau de surbrillance (cyan + émission) |
| `Prefabs/GrabMiniCube.prefab` | mini cube ultra léger (10 cm) |
| `Materials/GrabMiniCube.mat` | matériau du mini cube (URP Lit, orange émissif) |
| `Prefabs/GrabHeavyCube.prefab` | cube lourd (25 kg), tenue physique |
| `Materials/GrabHeavyCube.mat` | matériau du cube lourd (URP Lit, gris chaud) |

## La logique de `GrabbableBase`

Deux modes, comme `Grabbable` d'AutoHand :

- **`ignoreWeight` = true** (tenue sans poids) : au grab le corps devient cinématique et `HoldFollower` écrit
  `transform` dans `LateUpdate`, après le placement de tous les `GrabPoint`. Un seul écrivain par frame, donc
  pas de tremblement. L'objet suit son point de prise à `followSpeed` m/s, en gardant l'offset et la rotation
  qu'il avait au grab.
- **`ignoreWeight` = false** (tenue physique) : le corps reste dynamique, le grabber lui pose un
  `ConfigurableJoint` (voir `jointBreakForce`), et la main est retenue par l'objet. C'est le poids réel.

Réglages :

| Réglage | Rôle |
|---|---|
| `body` | `Rigidbody` du cube (rempli au `Reset`) |
| `grabColliders` | colliders attrapables (remplis au `Reset` et dans `Awake`) |
| `allowed` | rigs autorisés (mains, tête, pieds, corps) |
| `grabType` | `Default`, `HandToGrabbable` (la main va chercher l'objet) ou `GrabbableToHand` (l'objet vole vers la main) |
| `poseType` | `Grab` ou `Pinch` (pose des doigts) |
| `instantGrab` | attache immédiate, sans blend |
| `singleGrabberOnly` | un seul grabber peut le tenir |
| `ignoreWeight` | tenue cinématique (true) ou tenue physique par joint (false) |
| `heldNoFriction` | remplace le matériau physique des colliders pendant la tenue |
| `followSpeed` | vitesse maximale de suivi du point de prise (m/s, ignorée en tenue physique) |
| `minHeldDrag` / `minHeldAngleDrag` / `minHeldMass` | planchers appliqués au corps pendant une tenue physique |
| `throwPower` | multiplicateur du lancer mesuré au point de prise |
| `jointBreakForce` | force de rupture du joint d'une tenue physique (N) |
| `tearable` | l'objet tenu peut être arraché de ce qui le retient (joint sur l'objet) |
| `tearDistance` | distance de traction avant arrachement (m) |
| `motion` | échantillonnage de la vitesse au point de prise (fenêtre, grâce, vitesse max) |

Le lancer ne dépend pas du grabber : `Motion` échantillonne le point de prise pendant la tenue, et
`TryRemoveGrabber` applique `vitesse × multiplicateur de release × throwPower` au corps au relâchement.

### Plusieurs grabbers sur le même objet

Avec `singleGrabberOnly` à false (les trois cubes), un objet déjà tenu peut être pris par un autre grabber :
une seconde main, un rayon, le desktop. La règle est la même pour tous (`SharedHold` = plus d'un grabber) :
aucun grabber n'en domine un autre.

- **Position** : `TryGetHoldPose` **moyenne** la pose demandée par chaque grabber → l'objet est tiré
  **entre** eux (milieu des poses **trackées** des mains / des points de prise des rayons), sans plafond de
  vitesse (`followSpeed` est ignoré). Chacun tire donc à parts égales.
- **Rotation** : celle du **premier grabber `Direct`** (sinon du premier), c'est-à-dire
  `rotation trackée * offset de prise` — une orientation **continue**. Surtout pas l'axe main↔main : il
  s'inverse quand les mains se croisent ou se tirent l'une vers l'autre, l'objet fait un demi-tour et les
  deux mains se retrouvent à la place de l'autre.
- **Mains** : elles ne sont **jamais déplacées**. Chacune reste sur son contrôleur et c'est l'objet qui vient
  entre elles : une main tirée vers l'objet (verrou de pose) se figeait à 50 cm de la main réelle et l'ensemble
  ne suivait plus le joueur. La pose demandée par une main est donc sa pose **trackée**
  (`IGrabberPose.TryGetTrackedPose`), pas la pose de son point de prise.

Chaque grabber se lâche **séparément** (`TryRemoveGrabber`) : l'objet reste tenu par les autres, et si c'est le
porteur qui lâche, le suivant reprend la tenue sur la pose courante (`RebaseOffset`), sans saut.

## Écraser et arracher (comme AutoHand)

Deux contrats du SDK, implémentés par `GrabbableBase` et alimentés par les grabbers :

| Interface | AutoHand | Ce que ça fait ici |
|---|---|---|
| `IGrabSqueeze` | `onSqueeze` / `OnSqueeze(hand)` | `Squeeze(grabber)` / `Unsqueeze(grabber)` → événements `onSqueezed` / `onUnsqueezed` (UnityEvents `(grabbable, grabber)`) |
| `IGrabTear` | joint qui casse (`OnJointBreak`) | `CanTear`, `TryTear(grabber)` → détruit les `Joint` qui retiennent l'objet et lève `onTorn` ; l'objet **reste tenu** |

- L'écrasement est lu sur l'axe de **grip** de la main (`finger.left.middle` / `finger.right.middle`) :
  au-dessus de `squeezeThreshold` (0.8) → `Squeeze`, en dessous de `squeezeRelease` (0.6) → `Unsqueeze`.
  Vaut pour une tenue à la main **ou** au rayon. Le grabbable décide de ce qu'écraser veut dire (shape key,
  son, casse).
- L'arrachement est automatique : un objet qui porte un `Joint` (porte, levier, tiroir) se détache quand on
  l'éloigne de `tearDistance` (0.2 m) de l'endroit où il a été saisi, et il reste dans la main. `tearable`
  coupe le comportement, `TryTear` permet de le déclencher par script.

## Les trois cubes

`GrabCube` (1 kg) : `ignoreWeight` = true, `followSpeed` = 10, `throwPower` = 1, `grabType` = `HandToGrabbable`.
Il glisse dans la main et se lance normalement.

`GrabMiniCube` (10 cm, masse 0.05) : `followSpeed` = 30 (colle à la main), `throwPower` = 0.5 et
`motion.maxSpeed` = 6 (lancer court), `grabType` = `GrabbableToHand` (l'objet vole vers la main),
aucun plancher de drag ou de masse.

`GrabHeavyCube` (60 cm, 25 kg) : `ignoreWeight` = false, donc tenue physique. `followSpeed` = 3 (vitesse max de
la main pendant la prise), `minHeldDrag` = 1.5 et `minHeldAngleDrag` = 2, `jointBreakForce` = 10000 N,
`throwPower` = 0.3 et `motion.maxSpeed` = 2 (lancer court). La main ne peut pas le tirer (`maxReachMass` de
`HandGrabber` = 2 kg), c'est donc l'objet qui vient, comme chez AutoHand.

## Tester en quelques étapes

1. Dans la scène, créer un GameObject `Hand` avec un enfant `GrabPoint` placé à environ 10 cm devant.
2. Ajouter `GrabberBase` sur `Hand`, glisser `GrabPoint` dans le champ `grabPoint`. Laisser `targetLayers` vide :
   il est forcé sur `Grabbable` dans `Awake`.
3. Déposer `GrabCube.prefab` devant la main.
4. Depuis l'inspecteur ou un script :
   - `grabber.TryFindTarget(out var target, out var hit)` puis `grabber.TryGrab(target, hit, 0.1f)`
   - relâcher : `grabber.TryReleaseHeld(1f)`

Le grab est aussi observable via `OnGrabbedEvent`, `OnReleasedEvent` et `OnTargetChangedEvent` (UnityEvents de
`GrabberBase`), et via `OnGrabbedEvent` / `OnReleasedEvent` / `OnHighlightChangedEvent` sur `GrabbableBase`.

## Note sur le nom du dossier

Le dossier s'appelle `~Examples` (tilde en préfixe). Unity n'ignore que les dossiers qui **terminent** par `~` :
ceux-ci sont bien importés et visibles.
