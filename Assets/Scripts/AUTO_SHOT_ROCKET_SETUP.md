# Auto Shot and Big Rocket Radial AOE Setup

This setup covers targeting, pooling, effects, and cooldowns. It does not
configure point spending. For a score-only build, leave per-shot spend values
at `0` and use cooldowns for skill limits.

## 1. WeaponsScripts Inspector

Select the GameObject that already contains `WeaponsScripts` (normally
`GameManager`). Assign these fields:

### Auto Shot - Normal Gun GPS

- **Point Gps Prefab**: the world-space `Point GPS` crosshair prefab.
- **Fish Target Layers**: select only the layer(s) used by fish colliders.
- **Gps Effect Parent**: optional world-space Effects/Particle parent.

### Skill Button Selection UI

- **Auto Shot Skill Ui**: drag the root `RectTransform` of the Auto button.
- **Big Rocket Skill Ui**: drag the root `RectTransform` of the BigRoket button.
- **Active Skill Ui Scale**: `1.15` recommended.
- **Skill Ui Scale Duration**: `0.12` recommended.

The active skill button grows smoothly. Clicking it again turns it off and
returns its local scale to exactly `(1, 1, 1)`. Auto Shot and Big Rocket may now
both be active at the same time.

### Big Rocket Gun

- **Rocket Gun Visual**: the GameObject shown when the Rocket skill is active.
- **Rocket Aim Transform**: the rotating turret/aim child. Leave empty if the
  whole Rocket Gun Visual should rotate.
- **Rocket Bullet Prefab**: the Rocket projectile prefab.
- **Rocket Pool Parent**: optional pooled-bullet parent.
- **Rocket Net Effect Prefab**: drag the Net effect prefab here. This is the
  new explicit assignment requested for Rocket impacts.
- **Big Rocket Shot Cost**: keep `0` for the score-only setup.
- **Rocket Gun Animator**: optional Animator on the Rocket gun.
- **Rocket Shoot Trigger**: Animator trigger name, default `Shoot`.

Recommended Rocket tuning:

- Big Rocket Shot Cost: `0`
- Rocket Speed: `5`
- Rocket Minimum Seconds Between Shots: `0.65`
- Rocket Homing Turn Degrees Per Second: `300`
- Rocket Lifetime: `12`
- Rocket Impact Distance: `0.22`
- Rocket Damage: `1` (same as the current normal bullet damage)
- Rocket Net Visible Time: `0.35`
- Earthquake Duration: `0.30`
- Earthquake Strength: `0.16`

### Big Rocket Radial Area Damage

- **Rocket Area Damage Layers**: select only your fish layer(s).
- **Rocket Area Radius**: start with `1.6`. The area is a full circle centered
  on the Rocket impact and reaches the same distance on every side.
- **Rocket Area Damage Multiplier**: `1.0` for the same damage as the direct
  Rocket hit, or `0.65` for softer splash damage.
- **Rocket Maximum Area Targets**: `32`.

At impact the script creates a disabled child named `RocketDamageCircle` under
the pooled Net effect. It uses one non-allocating circular overlap scan, damages
each unique fish once, and keeps the helper collider disabled to prevent repeat
damage. Do not add a BoxCollider2D or damage script to the Net prefab.

The Rocket may use a copy of a normal bullet prefab. At runtime the script
disables every normal `BulletScript` on the root and children and disables the
Rocket's physics colliders. Target impact is detected by distance, so no other
fish or obstacle can fire a trigger that consumes the Rocket. `BulletScript`
also contains a second safety guard if an old prefab re-enables it.

## 2. Earthquake Assignment

On the same `GameManager` GameObject, expand:

`Game Manager (Script) > Shared Earthquake Effect`

- Assign **Earthquake Target** to `Main Camera` (or a camera-parent transform).
- Leaving it empty also works when the camera is tagged `MainCamera`, because
  `Camera.main.transform` is used automatically.

The connection is:

`BigRocketBullet target-distance impact` ->
`WeaponsScripts.PlayRocketImpactEffects` ->
`GameManager.PlayEarthquake`.

## 3. UI Buttons

### Auto Shot button

In the button `OnClick()` list, assign the GameObject containing
`WeaponsScripts`, then select:

`WeaponsScripts > AutoShot()`

After activation, click a fish once. The GPS follows it and the currently
selected normal gun auto-fires. These are ordinary bullets, so another fish can
block them. Click Auto Shot again to cancel, or select a normal gun level.

### Rocket skill button

In the Rocket button `OnClick()` list, assign:

`WeaponsScripts > ActivateRocketSkill()`

Without Auto Shot, click a fish to fire exactly one Rocket. It phases through
everything except the selected fish. On impact, the large Net's circular area
damages nearby fish. Every fish defeated by the blast awards its own fixed
`CoinFish` value exactly once; it never copies the selected fish's reward.

To auto-fire Big Rockets, enable both buttons in either order:

1. Click **BigRoket**.
2. Click **Auto Shot**.
3. Click one fish to lock it.

Big Rocket then fires at `Rocket Minimum Seconds Between Shots` until the fish
is defeated/leaves, the target changes, Auto Shot is disabled, or Big Rocket is
disabled. The cooldown controls firing pace.

Click BigRoket again to return to the normal gun. You can also call
`ReturnToNormalGun()` or use the existing gun-level controls, which already call
`ActivateGun(level)`.

## 4. GunMe collision fix

Keep `GunCollider` on each `GunMe` trigger. It now ignores only the physical
gun/fish collider pair. It never disables the fish collider, so the same fish
remains targetable and shootable by the player, every NPC, and every bullet.

## 5. Gameplay click events

Keep the existing gameplay input/EventTrigger calls:

- Pointer Down -> `WeaponsScripts.OnClickDown()`
- Pointer Up -> `WeaponsScripts.OnClickUp()`
- If used, Pointer Click -> `WeaponsScripts.ShootingClick()`

The script prevents Pointer Down and Pointer Click in the same frame from
creating two Rocket shots.
