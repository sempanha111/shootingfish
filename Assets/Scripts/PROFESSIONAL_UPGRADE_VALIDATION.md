# Professional Upgrade Validation

Validation date: 2026-08-02

## Passed static checks

- All 33 C# files parsed with the C# Tree-sitter grammar.
- Zero syntax-error files.
- Zero `yield` statements outside iterator-returning methods.
- Zero duplicate type declarations.
- Zero duplicate exact method signatures.
- HP, fixed-reward, and speed preset tables each contain exactly 46 values.
- Required merged systems remain present:
  - `BigRocketBullet`
  - `HomeCoinDisplay`
  - `PlayerCoinWallet`
  - `SoundManager.PlayNetBoomSound`
  - `FishScript.GetTargetCenterWorld`
  - `FishScript.TargetLifeVersion`
  - `WeaponsScripts.ResolveRocketImpact`
- The one-click editor script assigns the new organic Movement, cinematic
  Death, and professional Epic Boss values.
- The validator checks that organic steering, reactive hit motion, cinematic
  deaths, and Epic profile polish are enabled.
- Reward presentation uses a one-time guard before granting points or
  reporting a defeated target boss.
- Auto Shot release/retarget notification checks the pooled fish life token
  before modifying the old target.

## Required Unity verification

Static analysis cannot replace compiling and running the actual Unity project,
because scenes, prefab artwork, Animator Controllers, colliders, effects,
audio, layers, and Unity version are not included in a scripts-only package.

After import, clear the Unity Console, run both one-click commands, and finish
the play-mode acceptance test in
`PROFESSIONAL_MOVEMENT_DEATH_EPIC_UPGRADE.md`.
