# 46-Fish Realistic Arcade Setup

This build is designed for fictional arcade points only. It does not include real-money wagering, cash-out, or gambling services.

## 1. What was updated

The updated system changes all three layers together:

1. `FishScript` now stores each species' natural ambient behavior separately from parade permission.
2. `SwapFishScript` keeps a minimum target population, spawns Small fish mostly alone, and uses each prefab's measured sprite size for formation spacing.
3. Level flow uses short warning, recovery, and tide transitions so the screen does not remain empty.
4. Fish ID fallback now matches the new 46-fish order.
5. A missing death profile now pays fixed `CoinFish` instead of multiplying it by shot cost.
6. New `FishDeathProfile` assets default to `FixedFishCoin`.
7. `GameManager.Amount` defaults to `100000` fictional arcade points.

## 2. Replace the scripts

Back up the Unity project first. Copy the supplied `Scripts` folder into `Assets/Scripts` and allow Unity to replace the matching files.

The important changed files are:

- `FishScript.cs`
- `SwapFishScript.cs`
- `GameManager.cs`
- `FishSystem/FishDeathProfile.cs`

Keep all supplied dependency scripts together. Wait until Unity finishes compiling before changing Inspector values.

## 3. Exact fish-array order

Arrange `SwapFishScript > Fish[]` exactly as follows:

| Array index | Folder | Count |
|---:|---|---:|
| 0-11 | Fish Small | 12 |
| 12-30 | Fish Medium | 19 |
| 31-35 | Fish Special | 5 |
| 36-38 | Fish Mini Boss | 3 |
| 39-42 | Fish Main Boss | 4 |
| 43-45 | Fish Epic Boss | 3 |

For every prefab, `FishScript > ID` must equal its `Fish[]` array index.

## 4. Apply the complete one-click setup

1. Select the GameObject containing `SwapFishScript`.
2. Open the component's three-dot/gear menu.
3. Run `Apply Complete Recommended 46-Fish Setup`.
4. Run `Validate Recommended 46-Fish Setup`.
5. Verify that spawn transforms, backgrounds, audio, effects, weapon
   prefabs, and UI references remain assigned.

The command configures:

- Every prefab ID from its `Fish[]` array index.
- Every fish's recommended prefab defaults and detailed size overrides.
- Generated and assigned Movement/Death profiles.
- Three generated Epic Boss profiles and Epic controllers.
- Array ranges and Special indices.
- Six level boss-target arrays.
- High-density ambient spawning.
- Minimum population refill.
- Size-aware parade base spacing.
- Two-boss maximum.
- Short level transitions.
- Bodyguards and gentle progression.
- The `Fish` Sorting Layer when missing.

The complete fish command deliberately preserves existing player point and
weapon point settings.

Unity preserves previously serialized scene values, so running this command is important. Merely replacing the script does not overwrite every old Inspector value.

The command validates all 46 entries before changing prefabs. It stops when
an entry is empty, duplicated, a scene instance, or missing `FishScript` on
the prefab root.

## 5. Optional individual fish preset

Normally, do not repeat the setup prefab-by-prefab. Use this only when you
want to reset one fish later:

1. Confirm that fish's `ID` is its `Fish[]` index.
2. Open `FishScript`'s component menu.
3. Run `Apply Recommended 46-Fish Prefab Defaults`.
4. Save/apply the prefab.
5. Keep the generated Movement and Death Profile assignments.

The individual command assigns the tier, HP, fixed reward, speed, ambient
behavior, parade permission, sorting, boss behavior, gimmick values, and
artwork-spacing multipliers for that ID. It does not create profile assets;
the complete director command owns profile creation and assignment.

## 6. Individual fish setup

The names describe the artwork shown in the supplied reference images. If a sprite is in a slightly different visual order, keep the numeric index order and choose the closest movement profile.

### Small fish: indices 0-11

| Index | Visual | HP | Reward | Speed | Natural spawn | Movement profile |
|---:|---|---:|---:|---:|---|---|
| 0 | Orange clownfish | 60 | 100 | 1.24 | Solo | SmallCruiser |
| 1 | Yellow reef fish | 80 | 130 | 1.18 | Solo; parade allowed | SmallDarter |
| 2 | Blue long-fin fish | 100 | 160 | 1.12 | Solo | SmallCruiser |
| 3 | White lionfish | 120 | 190 | 0.98 | Solo | RayGlider |
| 4 | Blue striped fish | 150 | 235 | 1.20 | Solo; parade allowed | SmallDarter |
| 5 | Small golden ray | 180 | 280 | 1.06 | Usually solo; rare pair | RayGlider |
| 6 | Blue winged ray | 220 | 340 | 1.10 | Usually solo; rare pair | RayGlider |
| 7 | Small swordfish | 260 | 400 | 1.30 | Solo | OceanHunter |
| 8 | Red flying fish | 320 | 490 | 1.22 | Solo | SmallDarter |
| 9 | Orange fantasy fish | 380 | 580 | 1.08 | Solo | SmallCruiser |
| 10 | Blue/orange guppy | 450 | 680 | 1.15 | Solo | SmallDarter |
| 11 | Striped tropical fish | 540 | 810 | Mostly solo; 30% small school; parade allowed | SmallCruiser |

Small fish have ambient weight `1.35`, making them frequent. Only indices `1`, `4`, and `11` join organized parades; the others still spawn normally as solo fish.

### Medium fish: indices 12-30

| Index | Visual | HP | Reward | Speed | Natural spawn | Movement profile |
|---:|---|---:|---:|---:|---|---|
| 12 | Armored striped fish | 700 | 1,050 | 0.86 | Occasional small school; parade | SmallCruiser |
| 13 | Blue billfish | 850 | 1,260 | 1.14 | Solo | OceanHunter |
| 14 | Golden ray | 1,000 | 1,480 | 0.92 | Occasional pair; parade | RayGlider |
| 15 | Purple octopus | 1,200 | 1,760 | 0.78 | Solo | PulseCreature |
| 16 | Blue puffer | 1,450 | 2,100 | 0.82 | Solo | SmallCruiser |
| 17 | Yellow puffer | 1,700 | 2,450 | 0.80 | Solo | SmallCruiser |
| 18 | Hat fish | 2,000 | 2,880 | 0.95 | Solo | SmallDarter |
| 19 | Green reptile fish | 2,350 | 3,370 | 0.98 | Solo | OceanHunter |
| 20 | Green tropical fish | 2,750 | 3,930 | 0.88 | Occasional small school; parade | SmallCruiser |
| 21 | Large green puffer | 3,200 | 4,550 | 0.76 | Solo | SmallCruiser |
| 22 | Electric eel | 3,700 | 5,250 | 1.06 | Solo | OceanHunter |
| 23 | Butterfly pair | 4,300 | 6,080 | 0.74 | Pair; parade | RayGlider |
| 24 | Sea turtle | 5,000 | 7,050 | 0.70 | Solo; extra vertical room | RayGlider |
| 25 | Blue ray | 5,800 | 8,150 | 0.84 | Occasional pair; extra room | RayGlider |
| 26 | Purple ray | 6,700 | 9,400 | 0.82 | Occasional pair; extra room | RayGlider |
| 27 | Blue shark | 7,800 | 10,900 | 1.14 | Solo | OceanHunter |
| 28 | Yellow-blue fast fish | 9,000 | 12,600 | 1.10 | Occasional school; parade | OceanHunter |
| 29 | Golden coin creature | 10,400 | 14,600 | 0.72 | Rare solo | PulseCreature |
| 30 | Dark bomb creature | 12,000 | 16,800 | 0.68 | Rare solo | PulseCreature |

For index `30`, optional gimmick:

- Gimmick Type: `BombCrab`
- Gimmick Radius: `2.5`
- Gimmick Damage: `1200`

### Special fish: indices 31-35

| Index | Visual | HP | Reward | Speed | Movement profile |
|---:|---|---:|---:|---:|---|
| 31 | Pink crystal lobster | 15,000 | 22,000 | 0.72 | PulseCreature |
| 32 | Golden armored carp | 18,000 | 26,000 | 0.78 | OceanHunter |
| 33 | Golden predator | 22,000 | 32,000 | 0.74 | OceanHunter |
| 34 | Golden energy shell | 27,000 | 39,000 | 0.68 | PulseCreature |
| 35 | Golden jellyfish | 33,000 | 48,000 | 0.62 | PulseCreature |

All Special fish use:

- Natural Spawn Mode: `EventOnly`
- Ambient Spawn Weight: `0`
- Parade Participation: `AlwaysBlock`
- Parade Selection Weight: `0`

For index `34`, optional gimmick:

- Gimmick Type: `LightningChain`
- Gimmick Radius: `3`
- Gimmick Damage: `1500`

### Mini Boss: indices 36-38

| Index | Visual | HP | Reward | Speed | Primary movement |
|---:|---|---:|---:|---:|---|
| 36 | Blue-gold shark | 42,000 | 62,000 | 0.64 | MiniBossHunter |
| 37 | Golden seahorse | 55,000 | 81,000 | 0.60 | MiniBossOrbit |
| 38 | Golden frog | 72,000 | 106,000 | 0.54 | MiniBossCharge |

All Mini Bosses use `EventOnly` and `AlwaysBlock`. The frog receives extra automatic formation footprint, but it should not be used in a normal parade.

### Main Boss: indices 39-42

| Index | Visual | HP | Reward | Speed | Primary movement |
|---:|---|---:|---:|---:|---|
| 39 | Golden shark | 95,000 | 145,000 | 0.52 | BossPatrol / BossCharge |
| 40 | Jester character | 125,000 | 190,000 | 0.46 | BossFigureEight / BossPatrol |
| 41 | Blue energy dragon | 165,000 | 250,000 | 0.50 | BossPatrol / BossCharge |
| 42 | Golden treasure pot | 215,000 | 325,000 | 0.42 | BossOrbit / BossPatrol |

Main Boss presence:

- Mode: `RandomPerSpawn`
- Timed Retreat Chance: `0.60`
- Arena Stay Duration: `28-42`
- Arena Width: `0.84`
- Arena Height: `0.74`
- Smooth Turn: `48`
- Retreat Speed: `1`
- Outside Distance: `3`

Level-target bosses are persistent while their target encounter is active.

### Epic Boss: indices 43-45

| Index | Visual | HP | Reward | Speed | Identity |
|---:|---|---:|---:|---:|---|
| 43 | Thunder ocean king | 300,000 | 460,000 | 0.38 | Heavy dramatic turns |
| 44 | Golden sea serpent | 420,000 | 650,000 | 0.44 | Wide fast sweeps |
| 45 | Abyss guardian | 580,000 | 900,000 | 0.40 | Defensive arena movement |

Epic Boss setup:

- Fish Tier: `MainBoss`
- Natural Spawn: `EventOnly`
- Parade: `AlwaysBlock`
- Add `EpicBossController`
- Assign the correct `EpicBossProfile`
- Additional stat multipliers: Off / `1, 1, 1`

## 7. Movement Profile assets

Create assets with `Create > Fish Arcade > Fish Movement Profile`.

### SmallCruiser

- Healthy: LaneGlide `50`, ArcSweep `30`, SchoolFollow `20`
- Aggressive: ArcSweep `30`, ZigZagBurst `30`, HorizontalRush `25`, VerticalDive `15`
- Critical: ZigZagBurst `45`, HorizontalRush `35`, VerticalDive `20`

### SmallDarter

- Healthy: LaneGlide `35`, ZigZagBurst `30`, HorizontalRush `20`, ArcSweep `15`
- Aggressive: ZigZagBurst `40`, HorizontalRush `35`, VerticalDive `25`
- Critical: HorizontalRush `45`, ZigZagBurst `40`, VerticalDive `15`

### RayGlider

- Healthy: ArcSweep `50`, SpiralCross `25`, LaneGlide `15`, VerticalDive `10`
- Aggressive: ArcSweep `30`, SpiralCross `30`, VerticalDive `25`, ZigZagBurst `15`
- Critical: SpiralCross `35`, VerticalDive `30`, ZigZagBurst `25`, ArcSweep `10`

### PulseCreature

- Healthy: VerticalDive `45`, ArcSweep `30`, SpiralCross `25`
- Aggressive: VerticalDive `35`, SpiralCross `35`, ZigZagBurst `30`
- Critical: VerticalDive `40`, ZigZagBurst `35`, SpiralCross `25`

### OceanHunter

- Healthy: LaneGlide `35`, HorizontalRush `30`, ArcSweep `20`, ZigZagBurst `15`
- Aggressive: HorizontalRush `45`, ZigZagBurst `30`, VerticalDive `15`, ArcSweep `10`
- Critical: HorizontalRush `50`, ZigZagBurst `35`, VerticalDive `15`

### MiniBossProfile

- Healthy: MiniBossHunter `50`, MiniBossOrbit `30`, ZigZagBurst `20`
- Aggressive: MiniBossCharge `45`, MiniBossHunter `35`, ZigZagBurst `20`
- Critical: CriticalStagger `60`, MiniBossCharge `25`, MiniBossHunter `15`

### MainBossProfile

- Healthy: BossPatrol `55`, BossFigureEight `30`, BossOrbit `15`
- Aggressive: BossCharge `40`, BossPatrol `35`, BossFigureEight `25`
- Critical: CriticalStagger `60`, BossCharge `20`, BossPatrol `20`

## 8. Artwork-aware spacing

The director now measures each prefab's non-Shadow SpriteRenderer artwork and calculates both horizontal and vertical room.

`FishScript > Natural Species Spawning` contains:

- Formation Horizontal Multiplier
- Formation Vertical Multiplier
- Formation Padding

Recommended starting values:

| Artwork | Horizontal | Vertical | Padding |
|---|---:|---:|---:|
| Ordinary small fish | 1.00 | 1.00 | 0.15 |
| Long swordfish/eel | 1.15-1.30 | 1.00 | 0.18 |
| Ray/turtle/broad-fin fish | 1.20 | 1.45 | 0.20 |
| Huge frog/pot/Epic artwork | 1.40 | 1.55 | 0.25 |

If fish overlap vertically, increase only `Formation Vertical Multiplier` on that prefab. Do not increase the director's global lane spacing for every fish.

## 9. Death Profile assets

All six profiles must use:

- Reward Calculation Mode: `FixedFishCoin`
- Reward Multiplier: `1`
- Maximum Small Shot Bonus: `0`
- Minimum Reward: `0`
- Maximum Reward: `0`

| Field | Small | Medium | Special | Mini | Main | Epic |
|---|---:|---:|---:|---:|---:|---:|
| Reward Text | Small | Big | Big | Big | None | None |
| Standard Coin | On | On | On | On | Off | Off |
| Coin Plays | 1 | 1 | 2 | 3 | 1 | 1 |
| Coin Interval | 0 | 0 | 0.08 | 0.10 | 0 | 0 |
| Coin Spread | 0.10 | 0.16 | 0.30 | 0.45 | 0 | 0 |
| Front-Gun Skill | Off | Off | Off | Off | On | On |
| Boss Coin Burst | Off | Off | Off | Off | On | On |
| Net Boom | Off | On | On | On | Off | On |
| Net Index | 0 | 0 | 1 | 2 | 0 | 3 |
| Big Win Sound | Off | Off | On | On | On | On |
| Certificate Chance | 0 | 0.04 | 0.25 | 0.60 | 1 | 1 |
| Minimum Certificate | 0 | 5,000 | 20,000 | 60,000 | 140,000 | 450,000 |

The certificate is only a visual celebration. It must not alter the actual fixed reward.

## 10. Weapon and input setup

The complete fish command does not change balances, point spending, or weapon
values. Keep the game score-only and use cooldowns or ordinary non-purchasable
ammo for skill limits. Existing legacy point fields remain only for project
compatibility.

Bullet setup:

- Net Time: `0.30`
- Rigidbody2D Gravity: `0`
- Collision Detection: `Continuous`
- Collider2D Is Trigger: On
- Default active gun: `1`
- Player fire rate: `3.5 shots/second`
- Animation Shoot Wait: `0.11`

## 11. Expected screen pacing

The director preset produces:

| Level | Normal capacity | Minimum refill target |
|---:|---:|---:|
| 1 | 26 | 18 |
| 2 | 28 | 19 |
| 3 | 30 | 20 |
| 4 | 32 | 21 |
| 5 | 34 | 22 |
| 6 | 36 | 23 |

Boss battles can temporarily add `14` more fish, capped by the absolute maximum of `56`.

The Small selection share gently changes from `58%` at Level 1 to `48%` at Level 6. Most Small prefabs are forced to solo mode, while selected reef fish and Medium fish occasionally form natural pairs or schools.

## 12. Rendering order

| Category | Sorting order |
|---|---:|
| Small | 100 |
| Medium | 400 |
| Special | 800 |
| Mini Boss | 1400 |
| Main Boss | 2000 |
| Epic Boss | 2200 |
| Net/impact | 2600 |
| Reward text/coins | 3000 |
| World gun | 3400 |

On every fish:

- Automatic Fish Sorting: On
- Sorting Layer: `Fish`
- Use Sorting Group: On
- Normal Sorting Variation: `40`

## 13. First test checklist

1. Enter Play Mode and confirm the screen reaches at least 18 active targets quickly.
2. Watch indices `0-10`: they should normally enter alone.
3. Confirm a fish marked `AlwaysBlock` still appears as an ambient fish.
4. Confirm only IDs `1, 4, 11, 12, 14, 20, 23, 28` enter normal parades.
5. Confirm Special, Mini, Main, and Epic fish never enter an ordinary parade.
6. Test a ray parade and verify its rows receive more vertical room automatically.
7. Kill one Small fish with Gun 1 and verify the reward equals its fixed `CoinFish`, not `CoinFish x shot cost`.
8. Finish a boss and verify the next level begins without a long empty-screen pause.
9. Test Level 6 and verify no more than two target bosses appear simultaneously.
10. Use the Unity Profiler. If the target device struggles, lower Absolute Maximum Active Fish from `56` to `42` and Minimum Active Fish Base from `18` to `14`.
