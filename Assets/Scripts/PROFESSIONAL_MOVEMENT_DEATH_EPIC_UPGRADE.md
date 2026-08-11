# Professional Movement, Death, and Epic Boss Upgrade

This version upgrades the complete merged 46-fish package. Auto Shot, Big
Rocket AOE, saved Home points, Net Boom sound, fixed fish rewards, spawning,
and the existing one-click setup are retained.

The project remains a fictional arcade game. This package does not add
real-money purchases, wagering, cash-out, or prize redemption.

## Install and apply

1. Back up the Unity project.
2. Replace the matching files in `Assets/Scripts` with this package's complete
   `Scripts` folder.
3. Wait for Unity to compile and clear every Console error.
4. Confirm `SwapFishScript > Fish[]` contains exactly 46 unique prefab assets
   in the established order.
5. Select the object containing `SwapFishScript`.
6. Run `Apply Complete Recommended 46-Fish Setup` from the component menu.
7. Run `Validate Recommended 46-Fish Setup` from the same menu.

The apply command is safe to rerun. It creates or updates all generated
Movement, Death, and Epic Boss profiles under:

`Assets/FishArcadeGenerated/Recommended46`

It does not reset player points or change weapon costs.

## Movement behavior now applied

- Every free-swimming style uses acceleration-limited `SmoothDamp` steering.
- Direction changes use broad, distant viewport waypoints instead of small
  random pivots.
- Species keep distinct motion: darters turn quickly, rays glide heavily,
  pulse creatures breathe in speed, hunters travel decisively, and bosses
  steer with deliberate mass.
- Perlin wander and slow speed pulses prevent mechanically straight motion.
- Health phases change turn speed and route selection without repeatedly
  restarting the movement coroutine.
- A stuck watchdog selects a new route and applies a controlled recovery
  vector when travel is too small.
- Ordinary fish can perform one natural off-screen outer arc, then return
  without teleporting.
- Ordinary fish eventually choose a final smooth exit so the pool can recycle
  them naturally.
- Target bosses remain governed by boss encounter rules and are not removed
  by the ordinary-fish lifetime.
- Epic Bosses use heavy velocity smoothing, large arena targets, adaptive
  pressure speed, boundary recovery, anti-stuck logic, and cinematic
  off-screen passes with a real outer arc.

## Fire-reactive convulsion behavior

- A hit can create a very short micro-convulsion rather than a long stun.
- Sustained fire increases the chance of another pulse.
- A short recovery gap returns the fish to its normal swim before another
  pulse can occur.
- Auto Shot cancellation and target changes call the fish directly, ending
  the reaction on that frame.
- Manual or untargeted hits use a `0.34` second hit-cadence grace period because
  there is no persistent target lock to release.
- Critical-health movement is now an evasive struggle, not a permanent
  convulsion loop.
- Epic phase reactions also stop when incoming-fire pressure ends.

Optional Animator parameters are supported and ignored safely when absent:

| Prefab type | Parameter | Type |
|---|---|---|
| Normal, Special, Mini/Main Boss | `HitFlinch` | Trigger |
| Normal, Special, Mini/Main Boss | `UnderFire` | Bool |
| All non-Epic deaths | `Death` | Trigger |
| Epic Boss | `BossHitFlinch` | Trigger |
| Epic Boss | `BossUnderFire` | Bool |
| Epic Boss | `BossIntro` | Trigger |
| Epic Boss | `BossConvulsion` | Trigger |
| Epic Boss | `BossFakeDeath` | Trigger |
| Epic Boss | `BossStrongConvulsion` | Trigger |
| Epic Boss | `BossDeath` | Trigger |

The procedural motion still works when those parameters do not exist.

## Death presentation now applied

- Colliders disable at the start of the defeat sequence.
- The fish receives a short impact-scale beat, controlled drift, tumble,
  shrink, and alpha fade.
- Reward text, coins, sound, and configured effects fire at a timed reward
  beat rather than after an abrupt disappearance.
- Duration and intensity scale by tier: Small remains quick; Special and
  bosses receive longer presentation.
- Custom death effects can pulse several times for Special and boss profiles.
- Special and boss profiles add a restrained reward-beat camera response;
  ordinary fish remain shake-free so frequent defeats stay comfortable.
- Reward resolution is guarded so each defeated fish grants its fixed reward
  exactly once.
- Pooled scale, tint, alpha, and collider state restore on reuse.

Epic Boss final presentation adds a long procedural scale/tumble/drift/fade
sequence, repeated death-effect pulses, an opening camera shake, and a smaller
final shake near the reward beat. Existing Epic effect and audio references
are preserved when the one-click profile assets are updated.

## Epic visual-root recommendation

The one-click setup uses the prefab root as a safe fallback. This supports all
movement, scale reactions, fading, and death polish without changing prefab
hierarchy.

For the richest localized convulsion motion, create or use a child transform
that contains only the visible boss artwork, then assign it to
`EpicBossController > Visual Root`. Keep `Rigidbody2D` and gameplay colliders
on the prefab root. A child Visual Root permits small local position and
rotation reactions without disturbing physics movement.

## Project-specific references to verify

- Fish and boss Animator Controllers.
- Epic introduction, phase, skill, and death effect prefabs.
- Epic sound arrays and AudioSource.
- Custom Death Effect references on generated Death profiles.
- Net Boom effects/audio, reward text, coin targets, and camera used for shake.
- Rigidbody2D interpolation on large bosses (`Interpolate` recommended).

## Play-mode acceptance test

1. Let Small, Medium, ray, hunter, and pulse species swim for at least 30
   seconds. Confirm broad routes, smooth turns, one occasional off-screen
   return, and no stationary fish.
2. Hold Auto Shot on one fish. Confirm brief flinch, normal-swim recovery, and
   possible later flinches under sustained hits.
3. Disable Auto Shot during a pulse. Confirm scale/animation immediately
   returns to normal.
4. Change Auto Shot to another target. Confirm the old target stops reacting.
5. Defeat one fish from every tier. Confirm colliders disable, the cinematic
   motion finishes, and the reward is granted once.
6. Spawn each Epic Boss. Confirm smooth entrance, large arena routes, adaptive
   low-health movement, cinematic outer pass, skills, reactive flinches, and
   final presentation.
7. Let an Epic Boss leave the viewport during its cinematic pass. Confirm it
   follows the same continuous trajectory back and never jumps to screen
   center.
8. Reuse defeated fish from the pool. Confirm original scale, tint, alpha,
   colliders, and movement are restored.

If the validator reports no warnings and these tests pass, the professional
movement/death/Epic profile upgrade is fully applied.
