# Shadow Always Below Fish

`SpriteShadow` now keeps its positional offset in world space. Turning the
fish, using `flipX`/`flipY`, or using a negative parent scale no longer rotates
the shadow from below the fish to above it.

## Inspector setup

On each fish prefab's `SpriteShadow` component:

- Assign the fish renderer to `Source Sprite Renderer`.
- Assign the existing shadow child to `Shadow Sprite Renderer`.
- Keep `Keep Offset In World Space` enabled.
- Use a negative `Offset Y`, for example `-0.2`.
- Keep `Copy Flip` enabled so the shadow silhouette matches the fish.
- Keep `Copy Sorting Layer` enabled and use `Sorting Order Offset = -1`.

No one-click fish setup rerun is required. Existing prefabs receive the new
world-space behavior automatically; only adjust `Offset` when a specific fish
needs a wider or lower shadow position.
