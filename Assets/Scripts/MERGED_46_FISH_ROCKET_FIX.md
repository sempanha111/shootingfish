# 46-Fish + Auto Shot + Big Rocket Integration Fix

This package keeps the realistic 46-fish, level, population, group-spacing,
and parade update while restoring the complete Auto Shot, Big Rocket, and
saved Home coin integration.

## Compile fixes included

- `FishScript.GetTargetCenterWorld()`
- `FishScript.TargetLifeVersion`
- `FishScript.IsAliveTarget`
- `FishScript.IsTargetVisibleTo(Camera)`
- `FishScript.TakeRocketDamage(...)`
- `WeaponsScripts.ResolveRocketImpact(...)`
- `GameManager.TryPayFixedShotCost(...)`
- `GameManager.PlayEarthquake(...)`

## Required files that were restored

- `BigRocketBullet.cs`
- `HomeCoinDisplay.cs`
- `PlayerCoinWallet.cs`
- Full Auto Shot and radial-AOE `WeaponsScripts.cs`
- Rocket-safe `BulletScript.cs`
- Saved-coin-aware `GameManager.cs` and `UIManager.cs`

## Import

1. Back up the Unity project.
2. Copy the entire `Scripts` folder into `Assets/Scripts` and replace matching
   files. Do not copy only `BigRocketBullet.cs`, because the APIs are shared
   across several scripts.
3. Let Unity finish compiling before changing Inspector references.

## Home coin display

1. Add `HomeCoinDisplay` to the Home scene coin display object.
2. Assign the Home legacy Unity UI `Text` coin label to `Total Coin Text`.
3. Connect an optional reset button to `HomeCoinDisplay.ResetPlayerCoin()` or
   `HomeCoinDisplay.ResetCoin()`.

`PlayerCoinWallet` stores only fictional arcade points. `GameManager` loads the
saved balance in `Awake`, saves after balance changes, on pause/quit, and before
`UIManager.GoHome()` loads the Home scene.

## Big Rocket

Use the fields in `AUTO_SHOT_ROCKET_SETUP.md`. The Rocket locks both the fish
reference and `TargetLifeVersion`, preventing an old Rocket from damaging a
different pooled life. `ResolveRocketImpact` applies direct damage once and
radial damage to nearby unique fish, and each defeated fish awards its own
fixed `CoinFish` reward.

## Complete one-click setup

After arranging all 46 prefab references, use:

- `SwapFishScript -> Apply Complete Recommended 46-Fish Setup`
- `SwapFishScript -> Validate Recommended 46-Fish Setup`

The bulk command safely applies the individual Fish, director, generated
profile, Epic controller, Sorting Layer, and fictional arcade-point presets.
The original individual context-menu commands are still available.
