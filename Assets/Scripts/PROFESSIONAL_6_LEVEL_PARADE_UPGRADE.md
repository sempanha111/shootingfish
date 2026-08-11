# Professional Six-Level Parade Upgrade

This package keeps the project as a fictional arcade fish shooter. It does not
add wagering, cash-out, or real-money mechanics.

## Six clear level identities

| Level | Signature | Readable gameplay identity |
|---|---|---|
| 1 | Ribbon Current | Calm horizontal rows with restrained sweep |
| 2 | Arrowhead School | One leader with a living V-shaped school |
| 3 | Moving Halo | A moving center fish surrounded by circular followers |
| 4 | Cardinal Cross | Synchronized Left, Right, Top, and Bottom attacks |
| 5 | Twin Current | Two mirrored snake currents crossing the field |
| 6 | Royal Armada | A Special leader protected by shield, flank, and orbit guards |

Pre-boss waves now select the signature belonging to the current level instead
of randomly sharing all patterns between levels.

## Schooling

Followers preserve their authored slot but receive small per-fish speed,
position, and turn variations. The formation remains recognizable while no two
fish look perfectly synchronized.

The one-click prefab preset sets stronger micro-movement for Small fish and
slightly calmer motion for Medium and larger fish.

## Bodyguards

- Shield guards hold a dense front arc.
- Flank guards breathe along the sides.
- Orbit guards continuously circle the valuable target.
- Only Small or Medium prefabs are selected as guards.
- Special feature targets, MiniBosses, MainBosses, and Epic Bosses can receive
  bodyguards.
- Guards remain ordinary collidable fish, so they physically intercept shots.

## Vertical routes

The six recommended Top/Bottom chances are:

`8%, 18%, 32%, 58%, 44%, 52%`

Ordinary ambient schools use 42% of the current level's chance. Vertical fish
spawn at the configured `TopPos` or `BottomPos` and travel to the opposite
absolute edge.

## Setup

1. Replace the existing Scripts folder and let Unity compile.
2. On `SwapFishScript`, assign `TopPos` and `BottomPos` outside the visible
   camera bounds.
3. Run `Apply Complete Recommended 46-Fish Setup`.
4. Run `Validate Recommended 46-Fish Setup`.
5. Confirm Small and Medium fish have working Rigidbody2D and colliders.

## Recommended Play Mode checks

1. Each level produces only its own signature family during normal and
   pre-boss feature waves.
2. Level 4 visibly enters from all four edges.
3. Level 6 Special targets are protected by three different guard roles.
4. MiniBoss, MainBoss, and Epic guards remain attached through wide turns.
5. Followers breathe but do not overlap or destroy the formation silhouette.
6. Fish entering from Top or Bottom reach the opposite edge and return to the
   pool naturally.
7. Auto Shot, Big Rocket AOE, fixed rewards, certificates, Net Boom, and coin
   Animator scaling continue to work unchanged.

