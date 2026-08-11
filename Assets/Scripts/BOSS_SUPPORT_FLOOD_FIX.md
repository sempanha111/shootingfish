# Boss Support Flood Fix

## Cause

The old director checked for boss support approximately every 6–9 seconds for
the entire boss battle. Every check had a 55% chance to double the requested
fish count. The `[DOUBLE SUPPORT]` message was also printed before the active
fish limit was applied, so the Console could report 16 fish even when fewer—or
none—were spawned.

## New recommended behavior

- Maximum successful support waves per boss batch: `2`
- Maximum fish per support wave: `8`
- Minimum free slots required before spawning: `3`
- First support delay: `12–18` seconds
- Repeat support delay: `22–30` seconds
- Double support chance: `18%`
- Maximum double support events per boss batch: `1`
- Multi-boss battles stop requesting new support after the first boss resolves
- Full-screen attempts retry after `4` seconds without consuming a wave
- Console messages report the actual post-cap spawn count and wave number

Existing bodyguards remain separate from these reinforcements and continue to
leave or return to their pools normally.

## Inspector tuning

The settings are under `SwapFishScript > Boss Support Reinforcements`.

For a lighter battle, use:

- `Maximum Boss Support Waves Per Batch = 1`
- `Maximum Boss Support Fish Per Wave = 6`

To disable repeating boss support completely, set:

- `Maximum Boss Support Waves Per Batch = 0`

Boss entrance bodyguards are controlled separately under
`Boss Body Guard Formation`.
