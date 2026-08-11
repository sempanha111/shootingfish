# Center Traffic and Certificate Screen-Safety Update

This build keeps the existing 46-fish, Auto Shot, Big Rocket AOE, saved points,
Net Boom, professional movement, death, Epic Boss, and safe Rocket Animator
features.

## Apply the update

1. Replace the old `Scripts` folder with this complete folder.
2. Wait for Unity to finish compiling.
3. On `SwapFishScript`, run `Apply Complete Recommended 46-Fish Setup`.
4. Run `Validate Recommended 46-Fish Setup`.

The one-click command updates existing Movement and Epic Boss profile assets
with the new screen-distribution values.

## Center traffic balance

Recommended `SwapFishScript` values are applied automatically:

- Spread Ambient Across Viewport: On
- Ambient Traffic Lane Count: 4
- Ambient Viewport Padding: 0.10
- Ambient Center Avoidance Half Height: 0.14
- Ambient Lane Jitter: 0.025
- Ambient Target Vertical Drift: 0.045

Ambient groups rotate through separated upper and lower lanes. Normal fish
keep broad routes in one half most of the time, use off-center orbit anchors,
and recover from off-screen positions toward a safe side zone instead of
viewport `(0.5, 0.5)`. Parade lanes also preserve a camera-scaled center gap.

The Rocket radius and damage are unchanged. The balance comes from distributing
targets over the battlefield so a permanent center shot does not repeatedly
cover most active fish.

## Certificate screen safety

On `CertificateTextManager`, keep these values:

- Clamp Canvas Position: On
- Canvas Edge Padding: 20 or higher
- Clamp World Position: On
- World Screen Edge Padding: 28 or higher
- Shrink Oversized Certificates: On
- Minimum Screen Fit Scale: 0.55

For Canvas certificate prefabs, assign `Target Canvas`. If it is left empty,
the manager now attempts to find the parent Canvas automatically. For
SpriteRenderer/world certificate prefabs, assign the gameplay camera as the
scene's `MainCamera`.

The clamp measures all four UI corners or the complete Renderer bounds. It
moves the certificate inside the camera pixel rectangle and scales it down only
when its artwork is larger than the padded visible area.

## Cleanup included

- New pooled fish instantiate at their final spawn position and rotation, so
  movement is initialized once at the correct location.
- Two unreachable private parade/event helpers were removed.
- Previously unused pre-boss final-pause and post-boss recovery settings are
  now connected to the level flow instead of remaining dead configuration.

## Play-mode checks

1. Let at least 20 ambient fish enter and verify upper/lower screen coverage.
2. Leave the Rocket aimed near screen center and confirm it no longer catches
   most fish on every shot.
3. Test one horizontal and one vertical parade.
4. Defeat fish near all four corners using Canvas certificates.
5. Repeat with a world/Sprite certificate if the project uses one.
6. Confirm the complete certificate remains inside the screen on each aspect
   ratio used by the game.

