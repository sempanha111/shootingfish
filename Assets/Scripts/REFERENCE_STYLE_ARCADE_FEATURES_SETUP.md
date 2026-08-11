# Reference-Style Arcade Power Skills

This update adds useful fictional arcade power-skill ideas inspired by the supplied
references while preserving the existing four-gun layout, 46 fish, six parade
levels, Auto Shot, Big Rocket, rewards, backgrounds, and pooling.

## Included systems

- Freeze Wave with rechargeable uses and separate resistance for normal fish,
  Mini Bosses, Main Bosses, and Epic Bosses.
- Lightning Chain that starts from the current GPS-locked fish when possible,
  then jumps through nearby visible fish without damaging one fish twice.
- Optional cooldown fills, countdown labels, and red charge badges like the
  reference skill buttons.
- Pooled skill effects. Empty visual/audio fields are safe.
- No separate level, phase, target, timer, or Boss HUD controller is included.

These are fictional arcade abilities only. They do not add wagering, purchases,
cash-out, or real-world value.

## 1. Add the power-skill controller

Fast setup:

`Tools -> Shooting Fish -> Install Reference-Style Power Skills`

Run it once while the gameplay scene is open. It safely reuses existing
components if you run it again.

Select the GameObject that already contains `GameManager` and
`WeaponsScripts`, then add:

`ArcadePowerSkillController`

The Game Manager and Weapons references resolve automatically. Assign only the
objects you have:

- Target Camera: Main Camera
- Effect Parent: world Effects parent
- Skill Audio Source: optional effects AudioSource
- Freeze World Effect Prefab: optional full-screen/world ice burst
- Lightning Hit Effect Prefab: optional hit spark
- Lightning Segment Prefab: optional prefab with a `LineRenderer`
- Freeze/Lightning sounds: optional

Recommended starting values are already the script defaults:

| Setting | Freeze | Lightning |
|---|---:|---:|
| Maximum charges | 2 | 2 |
| Recharge | 22 seconds | 18 seconds |
| Active duration | 4.5 seconds | — |
| Maximum targets | — | 6 |
| Normal damage | — | 3,500 |

Normal fish nearly stop during Freeze. Bosses keep moving slowly, which avoids
making Boss battles trivial. Lightning also applies reduced Boss damage.

## 2. Skill buttons

Create or reuse two circular buttons on the side of the screen.

Freeze button event:

`ArcadePowerSkillController -> ActivateFreezeWave()`

Lightning button event:

`ArcadePowerSkillController -> ActivateLightningChain()`

Optional child UI for each button:

- Cooldown Fill: an `Image` set to Filled / Radial 360
- Cooldown Text: center countdown `Text`
- Charge Text: small badge `Text`, normally placed at the top-right
- Button: the root `Button`, automatically disabled while unavailable

The skill controller should be the only script writing these fills and labels.

## 3. Lightning targeting behavior

If Auto Shot or Rocket GPS already locks a fish, Lightning starts from that
fish. Otherwise it chooses one useful visible target and chains to the nearest
fish inside `Lightning Jump Range`.

Every target is validated by its pool life version, so a recycled fish cannot
receive an old delayed hit. Each fish can be hit only once per activation.

## 4. Existing skills remain unchanged

- Auto Shot remains the target-lock/GPS ability.
- Big Rocket remains the heavy radial AOE ability.
- Freeze and Lightning do not modify Rocket radius, Rocket cost, normal Bet
  values, fish rewards, or player balance.
- The coin Animator remains responsible for coin scale; `CoinManager` still
  moves coins only.

## 5. Final test order

1. Confirm Unity has zero compile errors.
2. Enter Play Mode and wait until normal fish are visible.
3. Press Freeze; normal fish should nearly stop for 4.5 seconds while Bosses
   continue slowly.
4. Spawn a new fish during Freeze; it should receive the remaining slow time.
5. Lock one fish with Auto Shot, then press Lightning; the first jump should use
   that exact locked target.
6. Confirm one Lightning activation never hits the same pooled fish twice.
7. Confirm each charge returns after its configured recharge time.
8. Confirm no `ArcadeHudController` is present or required in the scene.
9. Confirm Auto Shot, Rocket, certificates, backgrounds, and coin animation still
   behave exactly as before.
