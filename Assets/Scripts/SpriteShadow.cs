using UnityEngine;

/// <summary>
/// Uses an existing child SpriteRenderer as the fish shadow.
/// No shadow GameObject is instantiated at runtime.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class SpriteShadow : MonoBehaviour
{
    [Header("Existing Shadow Child")]
    [Tooltip(
        "Screen/world offset from the fish. A negative Y keeps the shadow " +
        "below the fish even when the fish turns or flips."
    )]
    public Vector3 offset = new Vector3(-0.2f, -0.2f, 0f);

    [SerializeField]
    [Tooltip(
        "Keep Offset in world space so fish rotation or negative scale cannot " +
        "move the shadow above the fish."
    )]
    private bool keepOffsetInWorldSpace = true;

    [SerializeField]
    private SpriteRenderer sourceSpriteRenderer;

    [SerializeField]
    private SpriteRenderer shadowSpriteRenderer;

    [Header("Shadow Transform")]
    [SerializeField, Range(0.1f, 1.5f)]
    private float shadowScaleMultiplier = 0.8f;

    [SerializeField]
    private bool applyScaleOnEnable = true;

    [Header("Renderer Sync")]
    [SerializeField]
    private bool copySprite = true;

    [SerializeField]
    private bool copyFlip = true;

    [SerializeField]
    private bool copySortingLayer = true;

    [SerializeField]
    private int sortingOrderOffset = -1;

    private Sprite lastSprite;
    private bool lastFlipX;
    private bool lastFlipY;
    private int lastSortingLayerId;
    private int lastSortingOrder;

    /// <summary>
    /// Exact renderer used as the visible fish body/source by this component.
    /// Exposed read-only so visual-only cinematic systems can distinguish the
    /// authored body from its shadow without copying gameplay behaviour.
    /// </summary>
    public SpriteRenderer SourceSpriteRenderer
    {
        get
        {
            AutoAssignRenderers();
            return sourceSpriteRenderer;
        }
    }

    /// <summary>
    /// Exact renderer used as the fish shadow. This is intentionally exposed
    /// read-only because many existing fish prefabs use an unnamed duplicate
    /// SpriteRenderer as the shadow, so name matching alone is not reliable.
    /// </summary>
    public SpriteRenderer ShadowSpriteRenderer
    {
        get
        {
            AutoAssignRenderers();
            return shadowSpriteRenderer;
        }
    }


    public void ApplyGameplayProfile(FishGameplayProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        AutoAssignRenderers();
        offset = new Vector3(
            profile.shadowOffset.x,
            profile.shadowOffset.y,
            offset.z
        );
        shadowScaleMultiplier = Mathf.Max(0.1f, profile.shadowScale);
        sortingOrderOffset = profile.shadowSortingOrder;

        if (shadowSpriteRenderer != null)
        {
            Color shadowColor = shadowSpriteRenderer.color;
            shadowColor.a = Mathf.Clamp01(profile.shadowOpacity);
            shadowSpriteRenderer.color = shadowColor;
        }

        ApplyInitialShadowSetup();
        Synchronize(true);
    }

    private void Reset()
    {
        AutoAssignRenderers();
        ApplyInitialShadowSetup();
        Synchronize(true);
    }

    private void Awake()
    {
        AutoAssignRenderers();
        ApplyInitialShadowSetup();
        Synchronize(true);
    }

    private void OnEnable()
    {
        AutoAssignRenderers();
        ApplyInitialShadowSetup();
        Synchronize(true);
    }

    private void LateUpdate()
    {
        ApplyShadowPlacement();
        Synchronize(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignRenderers();

        if (!Application.isPlaying)
        {
            ApplyInitialShadowSetup();
            Synchronize(true);
        }
    }
#endif

    private void AutoAssignRenderers()
    {
        if (sourceSpriteRenderer == null)
        {
            sourceSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        if (sourceSpriteRenderer == null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer candidate = renderers[i];

                if (candidate != null && !IsNamedShadow(candidate))
                {
                    sourceSpriteRenderer = candidate;
                    break;
                }
            }
        }

        if (shadowSpriteRenderer != null)
        {
            return;
        }

        SpriteRenderer fallbackRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];

            if (candidate != null && candidate != sourceSpriteRenderer)
            {
                if (IsNamedShadow(candidate))
                {
                    shadowSpriteRenderer = candidate;
                    return;
                }

                if (fallbackRenderer == null)
                {
                    fallbackRenderer = candidate;
                }
            }
        }

        shadowSpriteRenderer = fallbackRenderer;
    }

    private static bool IsNamedShadow(SpriteRenderer candidate)
    {
        return candidate != null &&
               candidate.gameObject.name.IndexOf(
                   "shadow",
                   System.StringComparison.OrdinalIgnoreCase
               ) >= 0;
    }

    private void ApplyInitialShadowSetup()
    {
        if (shadowSpriteRenderer == null)
        {
            return;
        }

        Transform shadowTransform = shadowSpriteRenderer.transform;
        ApplyShadowPlacement();

        if (applyScaleOnEnable)
        {
            shadowTransform.localScale =
                Vector3.one * Mathf.Max(0.1f, shadowScaleMultiplier);
        }
    }

    private void ApplyShadowPlacement()
    {
        if (sourceSpriteRenderer == null || shadowSpriteRenderer == null)
        {
            return;
        }

        Transform sourceTransform = sourceSpriteRenderer.transform;
        Transform shadowTransform = shadowSpriteRenderer.transform;

        if (keepOffsetInWorldSpace)
        {
            // Position is deliberately calculated in world space. This keeps
            // the shadow below the fish when its parent rotates 180 degrees,
            // uses flipX/flipY, or has a negative scale.
            shadowTransform.position = sourceTransform.position + offset;
            shadowTransform.rotation = sourceTransform.rotation;
            return;
        }

        // Optional legacy behaviour for prefabs that intentionally want the
        // shadow offset to rotate together with the fish.
        shadowTransform.localPosition = offset;
        shadowTransform.localRotation = Quaternion.identity;
    }

    private void Synchronize(bool force)
    {
        if (sourceSpriteRenderer == null || shadowSpriteRenderer == null)
        {
            return;
        }

        if (copySprite &&
            (force || lastSprite != sourceSpriteRenderer.sprite))
        {
            shadowSpriteRenderer.sprite = sourceSpriteRenderer.sprite;
            lastSprite = sourceSpriteRenderer.sprite;
        }

        if (copyFlip &&
            (force ||
             lastFlipX != sourceSpriteRenderer.flipX ||
             lastFlipY != sourceSpriteRenderer.flipY))
        {
            shadowSpriteRenderer.flipX = sourceSpriteRenderer.flipX;
            shadowSpriteRenderer.flipY = sourceSpriteRenderer.flipY;

            lastFlipX = sourceSpriteRenderer.flipX;
            lastFlipY = sourceSpriteRenderer.flipY;
        }

        if (copySortingLayer &&
            (force ||
             lastSortingLayerId != sourceSpriteRenderer.sortingLayerID ||
             lastSortingOrder != sourceSpriteRenderer.sortingOrder))
        {
            shadowSpriteRenderer.sortingLayerID =
                sourceSpriteRenderer.sortingLayerID;

            shadowSpriteRenderer.sortingOrder =
                sourceSpriteRenderer.sortingOrder + sortingOrderOffset;

            lastSortingLayerId = sourceSpriteRenderer.sortingLayerID;
            lastSortingOrder = sourceSpriteRenderer.sortingOrder;
        }
    }
}
