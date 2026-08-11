# Unity Fish Arcade Rebuild — Integration Guide

## Architecture / new files

- `FishProfiles.cs`: `FishTier`, reward/health enums, `FishDeathProfile`, and `FishMovementProfile` ScriptableObjects.
- `CertificateTextManager.cs`: pooled, Canvas-aware animated certificate popups.
- `PooledEffectToken.cs`: per-instance play-version token that prevents old coroutines from hiding reused effects.
- Existing files updated: `FishScript.cs`, `AnimatiorManager.cs`, `SwapFishScript.cs`, `GameManager.cs`, `Shoot.cs`, `WeaponsScripts.cs`, `WeaponNPC.cs`, and `BulletScript.cs`.

## Phase 1 — profiles and shared helpers

Create/import `FishProfiles.cs`, `CertificateTextManager.cs`, and `PooledEffectToken.cs`.

Create death assets with **Assets → Create → Fish Arcade → Fish Death Profile**. Suggested defaults:

- Small: Small text, Coin Animation.
- Medium: Big text, Coin Animation, NetBoom, certificate chance `0.08`, minimum win `25`.
- Special: Big text, Coin Animation, NetBoom, Big Win, certificate chance `0.35`.
- MiniBoss: Big text, Coin Animation, NetBoom, Big Win, certificate chance `0.75`.
- MainBoss: retain boss coin-front-gun and surprise particle behavior, Big Win, certificate chance `1.0`.

Create movement assets with **Assets → Create → Fish Arcade → Fish Movement Profile** and fill weighted Healthy/Aggressive/Critical arrays.

## Phase 2 — FishScript

`FishSystem()` is centralized through `PlayDeathPresentation()`; delete old fish-ID death branches after replacing the file.

Each prefab now exposes:

- `Fish Tier`
- `Use Array Index Tier Fallback`
- `Death Profile`
- `Movement Profile`
- `Health Evaluation Interval`

For a manually assigned tier, turn off **Use Array Index Tier Fallback**. When enabled, indexes use: 0–8 MainBoss, 9–20 Small, 21–40 Medium, 41–43 MiniBoss, 44+ assigned Inspector tier.

Legacy enum values remain only to preserve serialized scenes. `Wave` is redirected to `ArcSweep` and is never selected by the rebuilt spawning logic.

## Phase 3 — NetBoom and certificate pooling

On `AnimatiorManager`:

1. Assign all NetBoom prefabs to `Net Boom Prefabs`.
2. Set `Net Boom Hide Delay`.
3. Add `CertificateTextManager` to a Canvas object.
4. Assign two or three certificate prefabs, the Canvas, and certificate parent.
5. Assign that manager to `AnimatiorManager.certificateTextManager`.

The old `NetBoom` and `CerificateText` fields remain as migration compatibility. If `netBoomPrefabs` is empty, the old NetBoom array is copied at runtime.

## Phase 4 — SwapFishScript

New Inspector fields:

- `Weighted Boss Arrival Events`
- `Boss Clear Initial Delay` = `3`
- `Remaining Fish Before Level Change` = `8`
- `Battlefield Clear Maximum Wait` = `10`
- `Absolute Maximum Active Fish` = `110`

The boss sequence waits after the final batch, lets normal fish continue, and only forces the final exit during tide transition. It uses the existing fish pool and no longer calls `FindObjectsOfType` during the tide transition.

Ambient small/medium selection is now:

- L1 75/25
- L2 65/35
- L3 55/45
- L4 50/50
- L5 40/60
- L6 35/65

Parade methods continue selecting one prefab index per group.

## Phase 5 — weapon compatibility

This profile/director update does not change player balances or point-spending
rules. Legacy payment APIs remain only so existing scenes compile. For a
score-only build, use weapon cooldowns and non-purchasable ammo instead of
per-shot point spending.

## Important Inspector checks

- Fish prefab `id` should match its array index when fallback is enabled.
- Assign death and movement profiles on every prefab for fully data-driven behavior.
- Verify every NetBoom prefab has a non-looping Animator clip.
- Certificate prefabs should live under a Canvas and may contain inactive child Animators.
- Keep existing TextMeshPro font/fallback assignments; these scripts do not modify fonts.
- Keep `GameManager` references and existing CoinManager/UI/Sound assignments unchanged.

## Replaced/deleted legacy behavior

- Delete old hard-coded special fish ID checks inside `FishSystem()`.
- Delete direct player/NPC balance subtraction after firing.
- Delete bullet collision `Instantiate/Destroy` net code; `GameManager.SpawnNet()` is used.
- Do not restore `SwimStyle.Wave` spawning calls.
- Do not place `SwapFishScript` code in `WeaponsScripts.cs`.
