# Rocket Animator `Shoot` Parameter Fix

The Big Rocket now checks the assigned Animator Controller before playing its
fire animation. Unity will no longer report:

`Parameter 'Shoot' does not exist.`

The Rocket still launches and applies damage even when no compatible animation
is configured.

## Optional Rocket gun animation

Choose one of these setups on `WeaponsScripts`:

1. Add a **Trigger** parameter named `Shoot` to the Rocket gun's Animator
   Controller and create its transition to the fire animation.
2. Name the Rocket fire Animator state `Shoot`; the script can play that state
   directly when no Trigger exists.
3. Leave `Rocket Shoot Trigger` empty when the Rocket gun should have no
   Animator-driven fire animation.

Names are case-sensitive. `Shoot`, `shoot`, and `Shoot17` are different names.

