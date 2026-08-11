# Correct One-Click 46-Fish Setup

## Before running

1. Back up the Unity project.
2. Copy the complete supplied `Scripts` folder to `Assets/Scripts`.
3. Wait until the Unity Console has no compile errors.
4. Set `SwapFishScript > Fish` Size to exactly `46`.
5. Arrange the prefab assets in this order:

| Index | Tier |
|---:|---|
| 0-11 | Small |
| 12-30 | Medium |
| 31-35 | Special |
| 36-38 | Mini Boss |
| 39-42 | Main Boss |
| 43-45 | Epic Boss |

Use prefab assets dragged from the Project window. Do not use scene
instances, null entries, or the same prefab twice.

This package also overwrites the obsolete
`Editor/FishArcadeProfileSetup.cs` and supplies a compile-safe legacy
`FishStatsProfile.cs`, so files left by earlier setup packages cannot keep
referencing the removed `FishTier.EpicBoss` enum value.

## Run it

1. Select the GameObject containing `SwapFishScript`.
2. Open the component's three-dot/gear menu.
3. Click `Apply Complete Recommended 46-Fish Setup`.
4. Wait for Unity to save and refresh the generated assets.
5. Open the same menu and click
   `Validate Recommended 46-Fish Setup`.

You can also run the first command from:

`Tools > Fish Arcade > Apply Complete Recommended 46-Fish Setup`

## What is automatic now

- ID equals `Fish[]` array index for every prefab.
- Correct `0-45` tier mapping.
- HP, fixed reward, move speed, solo/pair/school spawning, ambient weight,
  parade allow/block selection, size spacing, render sorting, mini/main-boss
  behavior, and the two recommended gimmick values.
- Seven generated Movement Profile assets:
  - SmallCruiser
  - SmallDarter
  - RayGlider
  - PulseCreature
  - OceanHunter
  - MiniBossProfile
  - MainBossProfile
- Professional organic values on every Movement Profile: acceleration
  smoothing, broad waypoint distance, species turn rates, speed breathing,
  off-screen loop/return, natural exit, anti-stuck recovery, and reactive
  micro-convulsion timing.
- Six generated Death Profile assets:
  - SmallDeath
  - MediumDeath
  - SpecialDeath
  - MiniBossDeath
  - MainBossDeath
  - EpicBossDeath
- Tier-scaled cinematic death values: impact, drift, tumble, fade, reward beat,
  collider disable, and custom-effect pulse count.
- Three separate Epic Boss Profile assets, with different skill weights for
  IDs `43`, `44`, and `45`.
- Heavy Epic steering, adaptive health pressure, continuous outer passes,
  reactive fire convulsions, procedural final presentation, and repeated
  effect/camera-shake beats.
- `EpicBossController` is added to Epic prefabs when missing, then existing
  Rigidbody2D, Animator, AudioSource, and Collider2D references are filled
  when available.
- High-density director, minimum population, parade, boss, level transition,
  and scaling values.
- Bomb Crab uses Fish `30`; Lightning Chain uses Fish `34`.
- `Fish` Sorting Layer is created if missing.
- Existing player point and weapon point settings are left unchanged.

Generated assets are stored inside the Unity project at:

`Assets/FishArcadeGenerated/Recommended46`

Rerunning the command updates these generated profiles and is safe. Optional
effect, sound, and animation references already assigned inside those assets
are preserved.

## Still assign your own artwork assets

Code cannot guess which asset in your project is the correct visual or sound.
After the one-click command, verify these references:

- Fish Animator Controllers and animation trigger names.
- Sprite artwork and fish hitbox shapes.
- Epic Boss introduction, reaction, skill, and death effect prefabs.
- Epic Boss sound clips and an AudioSource when sound is required.
- Net Boom prefabs and Net Boom AudioSources.
- Certificate prefabs and Canvas references.
- Background sprites and transition effects.
- Auto Shot GPS prefab and skill button.
- Big Rocket gun, aim point, bullet, Net effect, layers, and skill button.
- Home coin text and optional reset button.
- World gun and screen UI sorting/canvas references.

The validator warns when an Epic prefab has no root Rigidbody2D or no
Collider2D. Add those components before gameplay testing.

## Why the old command could look incorrect

The earlier individual command trusted the prefab's existing ID and clamped
invalid values into `0-45`. Several prefabs left at ID `0` could therefore
receive the same Fish 0 values. It also did not create or assign Movement,
Death, or Epic profile assets.

The new bulk command validates the complete array first and uses the array
index directly, so this silent mismatch no longer occurs.
