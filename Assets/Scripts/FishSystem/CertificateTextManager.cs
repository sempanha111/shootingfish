using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CertificateTextManager : MonoBehaviour
{
    [Header("Certificate Pool")]
    public GameObject[] certificateTextPrefabs;
    public Transform certificateParent;
    public Canvas targetCanvas;

    [Min(0f)]
    public float hideDelay = 0.2f;

    [Header("World-Space Position")]
    [Tooltip("Base position added when certificate prefabs use SpriteRenderer/world space.")]
    public Vector2 worldBaseOffset = new Vector2(0f, 1.25f);

    [Tooltip("The certificate is placed at least this far left or right of the fish.")]
    [Min(0f)]
    public float worldMinimumHorizontalOffset = 0.8f;

    [Tooltip("Maximum random distance to the left or right of the fish.")]
    [Min(0f)]
    public float worldMaximumHorizontalOffset = 1.45f;

    [Tooltip("Additional random upward offset: X = minimum, Y = maximum.")]
    public Vector2 worldRandomVerticalOffset = new Vector2(0.1f, 0.55f);

    [Header("Canvas Position")]
    [Tooltip("Base UI offset in Canvas pixels.")]
    public Vector2 canvasBaseOffset = new Vector2(0f, 120f);

    [Tooltip("Minimum random horizontal Canvas separation from reward text.")]
    [Min(0f)]
    public float canvasMinimumHorizontalOffset = 90f;

    [Tooltip("Maximum random horizontal Canvas separation from reward text.")]
    [Min(0f)]
    public float canvasMaximumHorizontalOffset = 180f;

    [Tooltip("Additional random upward Canvas offset: X = minimum, Y = maximum.")]
    public Vector2 canvasRandomVerticalOffset = new Vector2(10f, 60f);

    [Tooltip("Keep UI certificate RectTransforms inside their parent area.")]
    public bool clampCanvasPosition = true;

    [Min(0f)]
    public float canvasEdgePadding = 20f;

    [Header("Visible-Screen Safety")]
    [Tooltip("Also clamps SpriteRenderer and world-space certificate prefabs using their complete rendered bounds.")]
    public bool clampWorldPosition = true;

    [Tooltip("Pixel padding kept between world-space certificate artwork and the camera edge.")]
    [Min(0f)]
    public float worldScreenEdgePadding = 28f;

    [Tooltip("Scales down a certificate only when its complete bounds are larger than the padded visible area.")]
    public bool shrinkOversizedCertificates = true;

    [Range(0.10f, 1f)]
    public float minimumScreenFitScale = 0.55f;

    [Header("Random Placement")]
    [Tooltip("Randomly choose the left or right side. Disabling this always uses the right side.")]
    public bool randomizeLeftAndRight = true;

    private readonly List<List<GameObject>> pools =
        new List<List<GameObject>>();

    private Camera worldCamera;

    private void Awake()
    {
        worldCamera = Camera.main;
        EnsurePools();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        worldMinimumHorizontalOffset =
            Mathf.Max(0f, worldMinimumHorizontalOffset);

        worldMaximumHorizontalOffset =
            Mathf.Max(
                worldMinimumHorizontalOffset,
                worldMaximumHorizontalOffset
            );

        canvasMinimumHorizontalOffset =
            Mathf.Max(0f, canvasMinimumHorizontalOffset);

        canvasMaximumHorizontalOffset =
            Mathf.Max(
                canvasMinimumHorizontalOffset,
                canvasMaximumHorizontalOffset
            );

        if (worldRandomVerticalOffset.y <
            worldRandomVerticalOffset.x)
        {
            worldRandomVerticalOffset.y =
                worldRandomVerticalOffset.x;
        }

        if (canvasRandomVerticalOffset.y <
            canvasRandomVerticalOffset.x)
        {
            canvasRandomVerticalOffset.y =
                canvasRandomVerticalOffset.x;
        }

        canvasEdgePadding = Mathf.Max(0f, canvasEdgePadding);
        worldScreenEdgePadding = Mathf.Max(
            0f,
            worldScreenEdgePadding
        );
        minimumScreenFitScale = Mathf.Clamp(
            minimumScreenFitScale,
            0.10f,
            1f
        );
    }
#endif

    public void PlayRandom(Vector3 worldPosition)
    {
        int index = GetRandomValidPrefabIndex();

        if (index >= 0)
        {
            Play(index, worldPosition);
        }
    }

    public void Play(int prefabIndex, Vector3 worldPosition)
    {
        EnsurePools();

        if (!IsValidPrefab(prefabIndex))
        {
            return;
        }

        GameObject instance = Get(prefabIndex);

        if (instance == null)
        {
            return;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        token.poolIndex = prefabIndex;

        int version = ++token.playVersion;

        Transform instanceTransform = instance.transform;

        instanceTransform.SetParent(
            certificateParent != null
                ? certificateParent
                : transform,
            false
        );

        token.RestoreDefaultTransform(instanceTransform);

        Vector2 randomOffset = CreatePositionOffset(
            IsCanvasCertificate(instanceTransform)
        );

        ResetVisuals(instance);
        instance.SetActive(true);

        SetCertificatePosition(
            instanceTransform,
            worldPosition,
            randomOffset
        );

        instanceTransform.SetAsLastSibling();

        float duration = RestartAnimators(instance);

        StartCoroutine(
            ReturnAfter(
                instance,
                version,
                duration + hideDelay
            )
        );
    }

    private bool IsCanvasCertificate(Transform target)
    {
        if (!(target is RectTransform) ||
            !(target.parent is RectTransform))
        {
            return false;
        }

        if (targetCanvas == null)
        {
            targetCanvas = target.GetComponentInParent<Canvas>();
        }

        return targetCanvas != null;
    }

    private Vector2 CreatePositionOffset(bool canvasPosition)
    {
        float side = 1f;

        if (randomizeLeftAndRight &&
            Random.value < 0.5f)
        {
            side = -1f;
        }

        if (canvasPosition)
        {
            float horizontal = Random.Range(
                canvasMinimumHorizontalOffset,
                canvasMaximumHorizontalOffset
            );

            float vertical = Random.Range(
                canvasRandomVerticalOffset.x,
                canvasRandomVerticalOffset.y
            );

            return canvasBaseOffset +
                   new Vector2(side * horizontal, vertical);
        }

        float worldHorizontal = Random.Range(
            worldMinimumHorizontalOffset,
            worldMaximumHorizontalOffset
        );

        float worldVertical = Random.Range(
            worldRandomVerticalOffset.x,
            worldRandomVerticalOffset.y
        );

        return worldBaseOffset +
               new Vector2(
                   side * worldHorizontal,
                   worldVertical
               );
    }

    private void SetCertificatePosition(
        Transform target,
        Vector3 worldPosition,
        Vector2 positionOffset
    )
    {
        RectTransform targetRect =
            target as RectTransform;

        RectTransform parentRect =
            target.parent as RectTransform;

        if (targetRect == null ||
            parentRect == null ||
            targetCanvas == null)
        {
            target.position =
                worldPosition +
                new Vector3(
                    positionOffset.x,
                    positionOffset.y,
                    0f
                );

            if (clampWorldPosition)
            {
                ClampWorldCertificateToCamera(target);
            }

            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                worldCamera,
                worldPosition
            );

        Camera canvasCamera =
            targetCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;

        Vector2 localPosition;

        bool converted =
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    canvasCamera,
                    out localPosition
                );

        if (!converted)
        {
            return;
        }

        targetRect.anchoredPosition =
            localPosition + positionOffset;

        if (clampCanvasPosition)
        {
            Canvas.ForceUpdateCanvases();
            ClampInsideParent(
                targetRect,
                parentRect,
                canvasEdgePadding
            );
            Canvas.ForceUpdateCanvases();
            ClampCanvasCertificateToScreen(
                targetRect,
                parentRect,
                canvasCamera
            );
        }
    }

    private static void ClampInsideParent(
        RectTransform targetRect,
        RectTransform parentRect,
        float padding
    )
    {
        Rect parentBounds = parentRect.rect;
        Rect targetBounds = targetRect.rect;
        Vector2 pivot = targetRect.pivot;
        Vector2 position = targetRect.anchoredPosition;

        float leftSpace =
            targetBounds.width * pivot.x;

        float rightSpace =
            targetBounds.width * (1f - pivot.x);

        float bottomSpace =
            targetBounds.height * pivot.y;

        float topSpace =
            targetBounds.height * (1f - pivot.y);

        float minimumX =
            parentBounds.xMin + padding + leftSpace;

        float maximumX =
            parentBounds.xMax - padding - rightSpace;

        float minimumY =
            parentBounds.yMin + padding + bottomSpace;

        float maximumY =
            parentBounds.yMax - padding - topSpace;

        if (minimumX <= maximumX)
        {
            position.x = Mathf.Clamp(
                position.x,
                minimumX,
                maximumX
            );
        }

        if (minimumY <= maximumY)
        {
            position.y = Mathf.Clamp(
                position.y,
                minimumY,
                maximumY
            );
        }

        targetRect.anchoredPosition = position;
    }

    private void ClampCanvasCertificateToScreen(
        RectTransform targetRect,
        RectTransform parentRect,
        Camera canvasCamera
    )
    {
        Rect screenRect = targetCanvas != null
            ? targetCanvas.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);
        Rect safeRect = GetPaddedScreenRect(
            screenRect,
            canvasEdgePadding
        );
        Rect bounds;

        if (!TryGetRectScreenBounds(
                targetRect,
                canvasCamera,
                out bounds
            ))
        {
            return;
        }

        if (shrinkOversizedCertificates)
        {
            float fitScale = GetFitScale(bounds, safeRect);

            if (fitScale < 0.999f)
            {
                targetRect.localScale *= Mathf.Max(
                    minimumScreenFitScale,
                    fitScale
                );
                Canvas.ForceUpdateCanvases();
            }
        }

        for (int pass = 0; pass < 2; pass++)
        {
            if (!TryGetRectScreenBounds(
                    targetRect,
                    canvasCamera,
                    out bounds
                ))
            {
                return;
            }

            Vector2 screenCorrection = GetScreenCorrection(
                bounds,
                safeRect
            );

            if (screenCorrection.sqrMagnitude <= 0.01f)
            {
                break;
            }

            Vector2 localCorrection;

            if (!TryConvertScreenDeltaToLocal(
                    parentRect,
                    canvasCamera,
                    screenCorrection,
                    out localCorrection
                ))
            {
                break;
            }

            targetRect.anchoredPosition += localCorrection;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void ClampWorldCertificateToCamera(Transform target)
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (worldCamera == null)
        {
            return;
        }

        Rect safeRect = GetPaddedScreenRect(
            worldCamera.pixelRect,
            worldScreenEdgePadding
        );
        Rect bounds;

        if (!TryGetWorldScreenBounds(target, worldCamera, out bounds))
        {
            return;
        }

        if (shrinkOversizedCertificates)
        {
            float fitScale = GetFitScale(bounds, safeRect);

            if (fitScale < 0.999f)
            {
                target.localScale *= Mathf.Max(
                    minimumScreenFitScale,
                    fitScale
                );
            }
        }

        for (int pass = 0; pass < 2; pass++)
        {
            if (!TryGetWorldScreenBounds(target, worldCamera, out bounds))
            {
                return;
            }

            Vector2 correction = GetScreenCorrection(bounds, safeRect);

            if (correction.sqrMagnitude <= 0.01f)
            {
                break;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(
                target.position
            );
            screenPosition.x += correction.x;
            screenPosition.y += correction.y;
            Vector3 correctedWorld = worldCamera.ScreenToWorldPoint(
                screenPosition
            );
            correctedWorld.z = target.position.z;
            target.position = correctedWorld;
        }
    }

    private static bool TryGetRectScreenBounds(
        RectTransform targetRect,
        Camera canvasCamera,
        out Rect bounds
    )
    {
        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        bool hasPoint = false;
        float minimumX = 0f;
        float maximumX = 0f;
        float minimumY = 0f;
        float maximumY = 0f;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                canvasCamera,
                corners[i]
            );
            AddScreenPoint(
                point,
                ref hasPoint,
                ref minimumX,
                ref maximumX,
                ref minimumY,
                ref maximumY
            );
        }

        bounds = hasPoint
            ? Rect.MinMaxRect(
                minimumX,
                minimumY,
                maximumX,
                maximumY
            )
            : default(Rect);
        return hasPoint;
    }

    private static bool TryGetWorldScreenBounds(
        Transform target,
        Camera targetCamera,
        out Rect bounds
    )
    {
        bool hasPoint = false;
        float minimumX = 0f;
        float maximumX = 0f;
        float minimumY = 0f;
        float maximumY = 0f;
        RectTransform rectTransform = target as RectTransform;

        if (rectTransform != null)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 point = targetCamera.WorldToScreenPoint(corners[i]);

                if (point.z < 0f)
                {
                    continue;
                }

                AddScreenPoint(
                    point,
                    ref hasPoint,
                    ref minimumX,
                    ref maximumX,
                    ref minimumY,
                    ref maximumY
                );
            }
        }

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 minimum = rendererBounds.min;
            Vector3 maximum = rendererBounds.max;
            Vector3[] corners =
            {
                new Vector3(minimum.x, minimum.y, minimum.z),
                new Vector3(minimum.x, maximum.y, minimum.z),
                new Vector3(maximum.x, minimum.y, minimum.z),
                new Vector3(maximum.x, maximum.y, minimum.z),
                new Vector3(minimum.x, minimum.y, maximum.z),
                new Vector3(minimum.x, maximum.y, maximum.z),
                new Vector3(maximum.x, minimum.y, maximum.z),
                new Vector3(maximum.x, maximum.y, maximum.z)
            };

            for (int cornerIndex = 0;
                 cornerIndex < corners.Length;
                 cornerIndex++)
            {
                Vector3 point = targetCamera.WorldToScreenPoint(
                    corners[cornerIndex]
                );

                if (point.z < 0f)
                {
                    continue;
                }

                AddScreenPoint(
                    point,
                    ref hasPoint,
                    ref minimumX,
                    ref maximumX,
                    ref minimumY,
                    ref maximumY
                );
            }
        }

        if (!hasPoint)
        {
            Vector3 point = targetCamera.WorldToScreenPoint(target.position);

            if (point.z >= 0f)
            {
                AddScreenPoint(
                    point,
                    ref hasPoint,
                    ref minimumX,
                    ref maximumX,
                    ref minimumY,
                    ref maximumY
                );
            }
        }

        bounds = hasPoint
            ? Rect.MinMaxRect(
                minimumX,
                minimumY,
                maximumX,
                maximumY
            )
            : default(Rect);
        return hasPoint;
    }

    private static void AddScreenPoint(
        Vector2 point,
        ref bool hasPoint,
        ref float minimumX,
        ref float maximumX,
        ref float minimumY,
        ref float maximumY
    )
    {
        if (!hasPoint)
        {
            minimumX = maximumX = point.x;
            minimumY = maximumY = point.y;
            hasPoint = true;
            return;
        }

        minimumX = Mathf.Min(minimumX, point.x);
        maximumX = Mathf.Max(maximumX, point.x);
        minimumY = Mathf.Min(minimumY, point.y);
        maximumY = Mathf.Max(maximumY, point.y);
    }

    private static Rect GetPaddedScreenRect(Rect screenRect, float padding)
    {
        float safePadding = Mathf.Clamp(
            padding,
            0f,
            Mathf.Min(screenRect.width, screenRect.height) * 0.45f
        );

        return Rect.MinMaxRect(
            screenRect.xMin + safePadding,
            screenRect.yMin + safePadding,
            screenRect.xMax - safePadding,
            screenRect.yMax - safePadding
        );
    }

    private static float GetFitScale(Rect bounds, Rect safeRect)
    {
        if (bounds.width <= 0.01f || bounds.height <= 0.01f)
        {
            return 1f;
        }

        return Mathf.Min(
            1f,
            safeRect.width / bounds.width,
            safeRect.height / bounds.height
        );
    }

    private static Vector2 GetScreenCorrection(
        Rect bounds,
        Rect safeRect
    )
    {
        float correctionX = GetAxisCorrection(
            bounds.xMin,
            bounds.xMax,
            safeRect.xMin,
            safeRect.xMax
        );
        float correctionY = GetAxisCorrection(
            bounds.yMin,
            bounds.yMax,
            safeRect.yMin,
            safeRect.yMax
        );
        return new Vector2(correctionX, correctionY);
    }

    private static float GetAxisCorrection(
        float boundsMinimum,
        float boundsMaximum,
        float safeMinimum,
        float safeMaximum
    )
    {
        if (boundsMaximum - boundsMinimum >= safeMaximum - safeMinimum)
        {
            return (safeMinimum + safeMaximum) * 0.5f -
                   (boundsMinimum + boundsMaximum) * 0.5f;
        }

        if (boundsMinimum < safeMinimum)
        {
            return safeMinimum - boundsMinimum;
        }

        if (boundsMaximum > safeMaximum)
        {
            return safeMaximum - boundsMaximum;
        }

        return 0f;
    }

    private static bool TryConvertScreenDeltaToLocal(
        RectTransform parentRect,
        Camera canvasCamera,
        Vector2 screenDelta,
        out Vector2 localDelta
    )
    {
        Vector2 referenceScreen = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            parentRect.position
        );
        Vector2 localStart;
        Vector2 localEnd;
        bool startConverted = RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                parentRect,
                referenceScreen,
                canvasCamera,
                out localStart
            );
        bool endConverted = RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                parentRect,
                referenceScreen + screenDelta,
                canvasCamera,
                out localEnd
            );

        localDelta = localEnd - localStart;
        return startConverted && endConverted;
    }

    private GameObject Get(int index)
    {
        List<GameObject> pool = pools[index];

        for (int i = 0; i < pool.Count; i++)
        {
            GameObject pooledObject = pool[i];

            if (pooledObject != null &&
                !pooledObject.activeSelf)
            {
                return pooledObject;
            }
        }

        Transform parent =
            certificateParent != null
                ? certificateParent
                : transform;

        GameObject instance = Instantiate(
            certificateTextPrefabs[index],
            parent
        );

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token =
                instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        instance.SetActive(false);
        pool.Add(instance);

        return instance;
    }

    private IEnumerator ReturnAfter(
        GameObject instance,
        int version,
        float delay
    )
    {
        yield return new WaitForSeconds(
            Mathf.Max(0.02f, delay)
        );

        if (instance == null)
        {
            yield break;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token != null &&
            token.playVersion == version)
        {
            instance.SetActive(false);
        }
    }

    private float RestartAnimators(GameObject root)
    {
        float longestDuration = 0.1f;

        Animator[] animators =
            root.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, 0f);
            animator.Update(0f);

            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorClipInfo(0);

            if (clips.Length > 0 &&
                clips[0].clip != null)
            {
                float duration =
                    clips[0].clip.length /
                    Mathf.Max(0.01f, animator.speed);

                longestDuration =
                    Mathf.Max(
                        longestDuration,
                        duration
                    );
            }
        }

        return longestDuration;
    }

    private static void ResetVisuals(GameObject root)
    {
        CanvasGroup[] canvasGroups =
            root.GetComponentsInChildren<CanvasGroup>(true);

        for (int i = 0;
             i < canvasGroups.Length;
             i++)
        {
            canvasGroups[i].alpha = 1f;
        }

        SpriteRenderer[] spriteRenderers =
            root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0;
             i < spriteRenderers.Length;
             i++)
        {
            Color color = spriteRenderers[i].color;
            color.a = 1f;
            spriteRenderers[i].color = color;
        }
    }

    private int GetRandomValidPrefabIndex()
    {
        if (certificateTextPrefabs == null)
        {
            return -1;
        }

        int validCount = 0;

        for (int i = 0;
             i < certificateTextPrefabs.Length;
             i++)
        {
            if (certificateTextPrefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return -1;
        }

        int selectedValidIndex =
            Random.Range(0, validCount);

        for (int i = 0;
             i < certificateTextPrefabs.Length;
             i++)
        {
            if (certificateTextPrefabs[i] == null)
            {
                continue;
            }

            if (selectedValidIndex-- == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsValidPrefab(int index)
    {
        return certificateTextPrefabs != null &&
               index >= 0 &&
               index < certificateTextPrefabs.Length &&
               certificateTextPrefabs[index] != null;
    }

    private void EnsurePools()
    {
        int requiredPoolCount =
            certificateTextPrefabs == null
                ? 0
                : certificateTextPrefabs.Length;

        while (pools.Count < requiredPoolCount)
        {
            pools.Add(new List<GameObject>());
        }
    }
}
