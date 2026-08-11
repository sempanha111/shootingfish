using UnityEngine;

public static class FishScreenBounds
{
    public const float MinimumViewportPadding = 0.10f;

    public static Rect GetWorldRect(Camera targetCamera, float viewportPadding, float worldZ)
    {
        if (targetCamera == null)
        {
            return new Rect(-8f, -5f, 16f, 10f);
        }

        float padding = Mathf.Max(MinimumViewportPadding, viewportPadding);
        float depth = Mathf.Abs(worldZ - targetCamera.transform.position.z);
        Vector3 minimum = targetCamera.ViewportToWorldPoint(
            new Vector3(-padding, -padding, depth)
        );
        Vector3 maximum = targetCamera.ViewportToWorldPoint(
            new Vector3(1f + padding, 1f + padding, depth)
        );

        float minX = Mathf.Min(minimum.x, maximum.x);
        float maxX = Mathf.Max(minimum.x, maximum.x);
        float minY = Mathf.Min(minimum.y, maximum.y);
        float maxY = Mathf.Max(minimum.y, maximum.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    public static Bounds GetCombinedVisualBounds(GameObject target)
    {
        Bounds combined = new Bounds(target != null ? target.transform.position : Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        if (target == null)
        {
            return combined;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            combined = new Bounds(target.transform.position, Vector3.one * 0.5f);
        }

        return combined;
    }


    public static Bounds GetCombinedVisualBounds(
        Transform fallbackTransform,
        Renderer[] renderers,
        Collider2D[] colliders
    )
    {
        Vector3 fallbackPosition = fallbackTransform != null
            ? fallbackTransform.position
            : Vector3.zero;
        Bounds combined = new Bounds(fallbackPosition, Vector3.zero);
        bool hasBounds = false;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(collider.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            combined = new Bounds(fallbackPosition, Vector3.one * 0.5f);
        }

        return combined;
    }

    public static bool IsFullyOutside(
        Camera targetCamera,
        Transform fallbackTransform,
        Renderer[] renderers,
        Collider2D[] colliders,
        float viewportPadding
    )
    {
        if (targetCamera == null || fallbackTransform == null)
        {
            return false;
        }

        Bounds bounds = GetCombinedVisualBounds(
            fallbackTransform,
            renderers,
            colliders
        );
        Rect padded = GetWorldRect(
            targetCamera,
            viewportPadding,
            fallbackTransform.position.z
        );

        return bounds.max.x < padded.xMin ||
               bounds.min.x > padded.xMax ||
               bounds.max.y < padded.yMin ||
               bounds.min.y > padded.yMax;
    }

    public static bool IsFullyOutside(
        Camera targetCamera,
        GameObject target,
        float viewportPadding
    )
    {
        if (targetCamera == null || target == null)
        {
            return false;
        }

        Bounds bounds = GetCombinedVisualBounds(target);
        Rect padded = GetWorldRect(targetCamera, viewportPadding, target.transform.position.z);

        return bounds.max.x < padded.xMin ||
               bounds.min.x > padded.xMax ||
               bounds.max.y < padded.yMin ||
               bounds.min.y > padded.yMax;
    }

    public static bool IsInsideVisibleView(Camera targetCamera, Vector3 worldPosition)
    {
        if (targetCamera == null)
        {
            return false;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }

    public static Vector3 GetEdgePoint(
        Camera targetCamera,
        FishScreenSide side,
        float normalizedLane,
        float viewportPadding,
        float extraWorldDistance,
        float worldZ
    )
    {
        if (targetCamera == null)
        {
            return Vector3.zero;
        }

        float padding = Mathf.Max(MinimumViewportPadding, viewportPadding);
        float lane = Mathf.Clamp01(normalizedLane);
        float x = 0.5f;
        float y = 0.5f;

        switch (side)
        {
            case FishScreenSide.Left:
                x = -padding;
                y = lane;
                break;
            case FishScreenSide.Right:
                x = 1f + padding;
                y = lane;
                break;
            case FishScreenSide.Top:
                x = lane;
                y = 1f + padding;
                break;
            case FishScreenSide.Bottom:
                x = lane;
                y = -padding;
                break;
        }

        float depth = Mathf.Abs(worldZ - targetCamera.transform.position.z);
        Vector3 world = targetCamera.ViewportToWorldPoint(new Vector3(x, y, depth));
        world.z = worldZ;

        Vector3 outward = Vector3.zero;
        switch (side)
        {
            case FishScreenSide.Left: outward = Vector3.left; break;
            case FishScreenSide.Right: outward = Vector3.right; break;
            case FishScreenSide.Top: outward = Vector3.up; break;
            case FishScreenSide.Bottom: outward = Vector3.down; break;
        }

        return world + outward * Mathf.Max(0f, extraWorldDistance);
    }

    public static FishScreenSide GetNearestSide(Camera targetCamera, Vector3 worldPosition)
    {
        if (targetCamera == null)
        {
            return FishScreenSide.Right;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(worldPosition);
        float left = Mathf.Abs(viewport.x);
        float right = Mathf.Abs(1f - viewport.x);
        float bottom = Mathf.Abs(viewport.y);
        float top = Mathf.Abs(1f - viewport.y);

        float minimum = Mathf.Min(left, right, bottom, top);
        if (minimum == left) return FishScreenSide.Left;
        if (minimum == right) return FishScreenSide.Right;
        if (minimum == top) return FishScreenSide.Top;
        return FishScreenSide.Bottom;
    }

    public static FishScreenSide Opposite(FishScreenSide side)
    {
        switch (side)
        {
            case FishScreenSide.Left: return FishScreenSide.Right;
            case FishScreenSide.Right: return FishScreenSide.Left;
            case FishScreenSide.Top: return FishScreenSide.Bottom;
            default: return FishScreenSide.Top;
        }
    }
}
