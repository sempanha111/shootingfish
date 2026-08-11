# Rocket, Net Sound, Parade, and Saved Coin Setup

## 1. Big Rocket prefab

Use the Big Rocket prefab on `WeaponsScripts > Rocket Bullet Prefab`.

Required components on the Rocket root:

- `BigRocketBullet`
- `Rigidbody2D`
  - Body Type: Dynamic
  - Gravity Scale: `0`
  - Collision Detection: Continuous
- One `Collider2D`
  - Is Trigger: enabled
  - Enabled: enabled

Do not add `BulletScript` to the Rocket. If it remains on a copied prefab, the
weapon setup disables it automatically. Rocket trigger colliders remain enabled
during flight. A non-target trigger is ignored; only the exact locked fish can
resolve the impact.

## 2. Net Boom sound

On the gameplay object containing `SoundManager`:

1. Expand `Net Boom Sounds`.
2. Set the array size and assign one or more `AudioSource` objects containing
   your Net Boom clips.
3. Optionally assign `Net Boom Fallback`.

The sound plays for normal `AnimatiorManager.PlayNetBoom` effects and the Big
Rocket impact Net. Missing AudioSource or AudioClip assignments are ignored
safely.

## 3. Parade eligibility

The warning for range `19-37` meant that every prefab in that serialized range
was rejected by `FishScript.CanJoinParade()`.

For each ordinary fish prefab that may join:

- `Parade Participation`: `Auto By Tier`, or
- `Parade Participation`: `Always Allow`

Use `Always Block` only for individual prefabs that must never appear in a
parade. The updated selector preserves those exclusions, accepts a FishScript
on a child, searches for another eligible prefab outside an empty range, and
skips the wave if none exist. It never forces the first blocked fish.

## 4. Persistent player coin

`GameManager.Amount` remains the default player balance. The first gameplay
launch saves it, and future launches continue from the saved balance.

- Only shooter ID `0` is persistent.
- NPC balances in `Gun1`, `Gun2`, and `Gun3` remain scene-only.
- Balance is flushed when returning Home, pausing the app, or closing the app.

Home scene setup:

1. Add `HomeCoinDisplay` to `HomeScreenController`.
2. Drag `Canvas/Coin/TotalCoin` into `Total Coin Text`.
3. Select `Reset_btn` and add an `On Click()` event.
4. Drag `HomeScreenController` into the event.
5. Select `HomeCoinDisplay > ResetPlayerCoin()`.

`TotalCoin` refreshes whenever the Home object becomes active. Reset restores
the default originally configured in `GameManager.Amount` (fallback `2000`).
