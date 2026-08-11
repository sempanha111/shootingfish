# Latest 46-Fish One-Click Update

This patch is built for the merged 46-fish package that already contains Auto
Shot, Big Rocket AOE, HomeCoinDisplay, PlayerCoinWallet, and
SoundManager.PlayNetBoomSound(). It does not replace or remove those systems.

## Fixed

- The individual prefab preset no longer silently clamps an invalid ID into
  the `0-45` range.
- `Fish[]` is now the source of truth for bulk setup; array index becomes ID.
- Setup stops before editing when Fish[] is not exactly 46 unique prefab
  assets or a prefab is missing FishScript on its root.
- Parade selection weight is zero for blocked fish.
- All detailed artwork-specific horizontal, vertical, and padding overrides
  are included.
- Recommended mini-boss movement reactions, normal main-boss presence, render
  sorting, and the two gimmick profiles are included.
- Obsolete editor/profile files from earlier packages are overwritten with
  compile-safe compatibility versions.

## Added

- `Editor/FishArcade46Setup.cs`
- `Editor/FishArcadeProfileSetup.cs`
- `FishSystem/FishStatsProfile.cs` compatibility file
- `ONE_CLICK_46_FISH_SETUP.md`

The bulk command creates and assigns seven Movement Profiles, six Death
Profiles, and three separate Epic Boss Profiles. It also adds SortingGroup to
fish prefabs, creates the Fish Sorting Layer when absent, assigns existing Epic
component references, applies the director preset, and runs validation.

Existing player point and weapon point settings are preserved.

## Professional movement, reaction, and defeat update

- All free-swimming styles now share a smooth acceleration-limited steering
  core with wide waypoints, species tuning, Perlin wander, and speed pulses.
- Fish can make a continuous off-screen outer arc and return; ordinary fish
  later use a smooth final exit instead of stopping or teleporting.
- Stuck watchdogs recover normal fish and Epic Bosses without snapping them to
  screen center.
- Health-phase styles are held for meaningful durations, so direction changes
  no longer restart every health-check interval.
- Hit reactions now use variable micro-convulsions with recovery gaps.
- Auto Shot cancellation/retargeting immediately releases the old target's
  reaction; manual hits use a short hit-cadence grace period.
- Non-Epic defeats now include impact, drift, tumble, fade, timed reward beat,
  effect pulses, collider safety, and one-time reward resolution.
- Epic Boss movement now includes heavy smoothing, adaptive low-health
  pressure, large targets, continuous cinematic outer passes, and anti-stuck
  recovery.
- Epic final defeat adds procedural scale, drift, tumble, fade, repeated
  effects, and a second reward-beat camera shake.
- The validator now checks that professional movement, reactive hit motion,
  cinematic deaths, and Epic profile polish are enabled.
- Added `PROFESSIONAL_MOVEMENT_DEATH_EPIC_UPGRADE.md`.
