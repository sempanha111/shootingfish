# Read This First

This package uses the 46-fish order:

- `0-11` Small
- `12-30` Medium
- `31-35` Special
- `36-38` Mini Boss
- `39-42` Main Boss
- `43-45` Epic Boss

After replacing the scripts:

1. Arrange `SwapFishScript > Fish[]` in the exact order above and set its
   array Size to `46`.
2. Select the GameObject containing `SwapFishScript`.
3. Open the component's three-dot/gear menu.
4. Run `Apply Complete Recommended 46-Fish Setup`.
5. Run `Validate Recommended 46-Fish Setup`.
6. Follow `ONE_CLICK_46_FISH_SETUP.md` for what is automatic and which
   artwork/audio references still require your assets.
7. Follow `PROFESSIONAL_MOVEMENT_DEATH_EPIC_UPGRADE.md` for the new movement,
   reactive convulsion, cinematic death, Epic Boss, and play-mode checks.
8. Follow `CENTER_BALANCE_CERTIFICATE_FIX.md` for balanced screen traffic,
   parade center-gap behavior, and certificate bounds-safe padding.
9. Follow `REFERENCE_STYLE_ARCADE_FEATURES_SETUP.md` to install and connect
   Freeze Wave, Lightning Chain, and their optional button charge/cooldown UI.
10. Follow `BOSS_SUPPORT_FLOOD_FIX.md` for the bounded boss reinforcement
    settings and lighter recommended values.
11. Follow `CENTER_SCREEN_DEATH_PREFAB_SETUP.md` to assign one or multiple
    center-screen celebration prefabs from each Death Profile.

The complete command now:

- Uses each `Fish[]` array index as the prefab ID.
- Refuses to run when the array is not exactly 46 unique prefab assets.
- Applies HP, fixed reward, speed, tier, spawning, parade, boss, sorting,
  gimmick, and detailed artwork-spacing values.
- Creates and assigns seven Movement Profiles and six Death Profiles.
- Creates and assigns separate Epic Boss profiles for IDs `43`, `44`, `45`.
- Adds/configures `EpicBossController` for the three Epic prefabs.
- Applies the director preset without changing existing player point values.
- Creates the `Fish` Sorting Layer when it is missing.
- Applies acceleration-smoothed organic routes, wide off-screen loops,
  anti-stuck recovery, fire-reactive micro-convulsions, cinematic defeat
  motion, and the upgraded Epic Boss presentation values.
- Distributes ambient, parade, orbit, recovery, and Epic targets away from a
  permanent center stack while keeping occasional natural crossings.
- Clamps the complete certificate artwork inside the visible camera area for
  both Canvas and world/Sprite prefabs.
- Adds optional rechargeable Freeze and Lightning abilities without adding a
  separate level/Boss HUD or modifying normal gun and Rocket values.
- Lets every Death Profile select a pooled Canvas or world-space celebration
  prefab at a stable viewport position, with reward/chance and overlap gates.

The individual `Apply Recommended 46-Fish Prefab Defaults` command remains
available, but it now rejects IDs outside `0-45` instead of silently clamping
them to the wrong fish.

The economy uses fictional arcade points only, fixed fish rewards, and no cash-out or real-money integration.
