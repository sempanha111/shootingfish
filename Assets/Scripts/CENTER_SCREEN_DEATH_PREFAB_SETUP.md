# Center-Screen Death Prefab Setup

Each `FishDeathProfile` can now play one celebration prefab at a stable visible-
screen position when its fish reward is granted. The system is optional and
uses pooled objects, so unassigned profiles keep their previous behavior.

## Profile setup

1. Select a generated Death Profile asset, such as `MiniBossDeath`.
2. Enable `Play Center Screen Death Prefab`.
3. Set `Center Screen Death Prefabs` to the required array size and assign the
   available celebration prefabs.
4. Choose a selection mode:
   - `Random No Repeat`: random valid prefab without the same immediate repeat.
   - `Random`: fully random valid prefab.
   - `Exact Index`: always use `Center Screen Death Prefab Index`.
5. Keep `Center Screen Death Viewport Position` at `(0.5, 0.5)` for the exact
   visible-screen center on every aspect ratio.

Useful gates:

- `Center Screen Death Prefab Chance`: final play chance from `0` to `1`.
- `Minimum Center Screen Death Prefab Reward`: minimum fictional arcade-point
  reward required before the prefab can appear.
- `Ignore While Visible`: safest default for Rocket and chain defeats; it keeps
  only one celebration visible.
- `Replace Current`: a newer high-value celebration replaces the active one.
- `Allow Multiple`: permits simultaneous prefabs and should be used sparingly.

## Canvas prefab

Use a UI prefab with a `RectTransform`. Assign the gameplay overlay Canvas to:

`AnimatiorManager > Center Screen Death Canvas`

An active Screen Space Overlay Canvas is found automatically when this field is
empty. `Center Screen Death Canvas Parent` is optional. The prefab is placed as
the last sibling so it appears above normal Canvas children. Its CanvasGroup
does not receive raycasts, so the presentation cannot block the four guns.

Keep the prefab root responsible for placement. Put scale/position animation on
a child visual object so the profile's center and scale multiplier stay stable.

## World Sprite or Particle prefab

Set `Center Screen Death Prefab Space` to `World`, then optionally assign:

- `AnimatiorManager > Center Screen Death Camera`
- `AnimatiorManager > Center Screen Death World Parent`

When empty, the system uses `Camera.main`, world Z `0`, and the existing
animation parent. Set the prefab's SpriteRenderer/ParticleSystem sorting layer
and order high enough to appear over fish and nets. Visual-prefab colliders are
disabled automatically so they cannot intercept bullets or targeting.

## Timing

- `Center Screen Death Visible Duration = 0` detects Animator and particle
  duration automatically.
- Set an explicit duration for looping Animator or ParticleSystem content.
- `Center Screen Death Use Unscaled Time` keeps cleanup reliable during a pause
  or slow-motion presentation.

## Recommended use

- Small: disabled.
- Medium: disabled or a very low chance.
- Special: short celebration with a moderate chance.
- Mini Boss: stronger celebration with `Ignore While Visible`.
- Main/Epic Boss: full celebration with `Replace Current`.

The one-click 46-fish setup does not erase assigned center-screen prefab arrays
or their selection settings.

## HUD removal

`ArcadeHudController.cs` is no longer included or installed. Freeze and
Lightning remain available through `ArcadePowerSkillController` and your own
buttons. If files are merged over an older package instead of replacing the
complete `Scripts` folder, delete the old `ArcadeHudController.cs` manually.
