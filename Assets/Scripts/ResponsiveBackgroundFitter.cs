using UnityEngine;

/// <summary>
/// Keeps a world-space SpriteRenderer background large enough to cover
/// the active camera view. It automatically refits when the sprite,
/// camera aspect, camera size, or parent scale changes.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ResponsiveBackgroundFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Fit Settings")]
    [Tooltip("1.10 means 110% of the minimum cover size, leaving extra room for camera shake.")]
    [Min(1f)]
    [SerializeField] private float overscanMultiplier = 1.10f;

    [Tooltip("Keep the sprite centered on the camera whenever a refit occurs.")]
    [SerializeField] private bool centerOnCamera = true;

    [Tooltip("Preserve the background object's current Z position.")]
    [SerializeField] private bool preserveZPosition = true;

    [Header("Automatic Refresh")]
    [SerializeField] private bool refitWhenSpriteChanges = true;
    [SerializeField] private bool refitWhenCameraChanges = true;

    private Sprite lastSprite;
    private float lastOrthographicSize = float.NaN;
    private float lastCameraAspect = float.NaN;
    private Vector3 lastParentLossyScale = new Vector3(float.NaN, float.NaN, float.NaN);
    private bool hasWarnedMissingReference;

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        overscanMultiplier = 1.10f;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        FitNow();
    }

    private void Start()
    {
        FitNow();
    }

    private void LateUpdate()
    {
        CacheReferences();

        if (targetRenderer == null || targetCamera == null)
        {
            WarnMissingReferenceOnce();
            return;
        }

        bool needsRefit = false;

        if (refitWhenSpriteChanges && lastSprite != targetRenderer.sprite)
        {
            needsRefit = true;
        }

        if (refitWhenCameraChanges)
        {
            if (!Mathf.Approximately(lastOrthographicSize, targetCamera.orthographicSize) ||
                !Mathf.Approximately(lastCameraAspect, targetCamera.aspect))
            {
                needsRefit = true;
            }

            Vector3 parentScale = GetParentLossyScale();

            if (!Approximately(lastParentLossyScale, parentScale))
            {
                needsRefit = true;
            }
        }

        if (needsRefit)
        {
            FitNow();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        overscanMultiplier = Mathf.Max(1f, overscanMultiplier);
        CacheReferences();

        if (!Application.isPlaying)
        {
            FitNow();
        }
    }
#endif

    /// <summary>
    /// Assign a new background sprite and immediately fit it to the camera.
    /// </summary>
    public void SetSpriteAndRefit(Sprite newSprite)
    {
        CacheReferences();

        if (targetRenderer == null)
        {
            WarnMissingReferenceOnce();
            return;
        }

        targetRenderer.sprite = newSprite;
        FitNow();
    }

    /// <summary>
    /// Recalculate the background position and scale immediately.
    /// Can be called after changing resolution, camera size, or background sprite.
    /// </summary>
    [ContextMenu("Fit Background Now")]
    public void FitNow()
    {
        CacheReferences();

        if (targetRenderer == null ||
            targetRenderer.sprite == null ||
            targetCamera == null)
        {
            WarnMissingReferenceOnce();
            return;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogWarning(
                $"ResponsiveBackgroundFitter on '{name}' requires an Orthographic Camera.",
                this
            );
            return;
        }

        Vector2 spriteSize = targetRenderer.sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        float cameraHeight = targetCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;

        Vector3 parentScale = GetParentLossyScale();
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));

        float requiredLocalScaleX =
            cameraWidth / (spriteSize.x * parentScaleX);

        float requiredLocalScaleY =
            cameraHeight / (spriteSize.y * parentScaleY);

        // Cover the entire camera view without stretching the sprite.
        float uniformLocalScale =
            Mathf.Max(requiredLocalScaleX, requiredLocalScaleY) *
            overscanMultiplier;

        Vector3 scale = transform.localScale;
        scale.x = uniformLocalScale;
        scale.y = uniformLocalScale;
        transform.localScale = scale;

        if (centerOnCamera)
        {
            Vector3 position = transform.position;
            float savedZ = position.z;

            position.x = targetCamera.transform.position.x;
            position.y = targetCamera.transform.position.y;

            if (preserveZPosition)
            {
                position.z = savedZ;
            }
            else
            {
                position.z = targetCamera.transform.position.z + 10f;
            }

            transform.position = position;
        }

        lastSprite = targetRenderer.sprite;
        lastOrthographicSize = targetCamera.orthographicSize;
        lastCameraAspect = targetCamera.aspect;
        lastParentLossyScale = GetParentLossyScale();
        hasWarnedMissingReference = false;
    }

    private void CacheReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private Vector3 GetParentLossyScale()
    {
        return transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;
    }

    private void WarnMissingReferenceOnce()
    {
        if (hasWarnedMissingReference)
        {
            return;
        }

        hasWarnedMissingReference = true;

        Debug.LogWarning(
            $"ResponsiveBackgroundFitter on '{name}' needs a Camera, " +
            "a SpriteRenderer, and an assigned Sprite.",
            this
        );
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.z, b.z);
    }
}
