# Mermaid Showcase Main Boss Profile

This optional profile is designed for the large mermaid-style Main Boss shown
in the reference image. It affects only the fish selected when the installer is
run.

## Apply it

1. Let Unity finish compiling the imported scripts.
2. Select the mermaid Main Boss prefab in the Project window. You may also
   select its scene instance.
3. Run:
   `Tools > Shooting Fish > Apply Mermaid Showcase Profile To Selected Main Boss`
4. Save the prefab or scene when Unity asks.

The installer creates:

`Assets/ShootingFish/Profiles/Mermaid_Showcase_MainBoss.asset`

and assigns it only to the selected `FishScript`.

## Result

- Healthy: wide graceful figure-eight with broad patrol passes.
- Aggressive: more patrol changes and occasional controlled charge.
- Critical: keeps moving with subtle stagger moments instead of stopping.
- Arena coverage: 84% width and 72% height.
- Center behavior: may cross the center naturally, but does not repeatedly
  select the exact center as its destination.
- Presence: stays until defeated.
- Parade and ambient spawning: disabled for this boss.
- Other fish, health, reward, death profile, Animator, and visuals: unchanged.

## Recommended prefab values

- `Move Speed`: 1.2 to 1.5 (start at 1.3).
- Rigidbody2D: simulated, gravity 0, no frozen X/Y position.
- Animator clips: animate SpriteRenderer properties only; do not key the root
  Transform Position.
- Do not attach `ResponsiveObjectPlacement` to the boss root.

If the complete 46-fish setup is run again, apply this mermaid profile again
afterward because the complete setup intentionally restores its shared default
Main Boss profile.
