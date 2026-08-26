using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DisplayTextManagerScript : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TextMeshProUGUI textPrefab;
    public TextMeshProUGUI BigtextUI;

    [Header("Font Fallback")]
    [SerializeField]
    private TMP_FontAsset khmerFallbackFont;

    private void Awake()
    {
        AddKhmerFallback(textPrefab);
        AddKhmerFallback(BigtextUI);
    }

    private void AddKhmerFallback(
        TextMeshProUGUI textObject
    )
    {
        if (textObject == null ||
            textObject.font == null ||
            khmerFallbackFont == null)
        {
            return;
        }

        if (!textObject.font
            .fallbackFontAssetTable
            .Contains(khmerFallbackFont))
        {
            textObject.font
                .fallbackFontAssetTable
                .Add(khmerFallbackFont);
        }
    }
    public Transform TextHolder;

    [Header("Normal Floating Text")]
    [SerializeField, Min(0f)] private float normalStartDelay = 0.15f;
    [SerializeField, Min(0f)] private float normalScaleDuration = 0.25f;
    [SerializeField, Min(0f)] private float normalVisibleDuration = 1f;
    [SerializeField, Min(0f)] private float normalFadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float normalDisableDelay = 0.15f;
    [SerializeField] private float normalRiseDistance = 90f;
    [SerializeField] private Vector2 normalTextOffset = Vector2.zero;

    [Header("Big Win Text")]
    [SerializeField, Min(0f)] private float bigTextStartDelay = 0f;
    [SerializeField, Min(0f)] private float bigTextScaleDuration = 0.35f;
    [SerializeField, Min(0f)] private float bigTextVisibleDuration = 3f;
    [SerializeField, Min(0f)] private float bigTextFadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float bigTextDisableDelay = 0.25f;
    [SerializeField] private float bigTextRiseDistance = 45f;
    [SerializeField] private Vector2 bigTextOffset = new Vector2(0f, 70f);
    [SerializeField] private Vector3 bigTextTargetScale = new Vector3(1.15f, 1.15f, 1f);

    private readonly List<TextMeshProUGUI> normalTextPool =
        new List<TextMeshProUGUI>();

    private readonly List<TextMeshProUGUI> bigTextPool =
        new List<TextMeshProUGUI>();

    private readonly Dictionary<TextMeshProUGUI, int> playVersions =
        new Dictionary<TextMeshProUGUI, int>();

    public void DisplaySmallText(string textValue, Vector3 worldPosition)
    {
        StartCoroutine(
            DisplayRoutine(
                textValue,
                worldPosition,
                false
            )
        );
    }

    public void DisplayBigText(string textValue, Vector3 worldPosition)
    {
        StartCoroutine(
            DisplayRoutine(
                textValue,
                worldPosition,
                true
            )
        );
    }





    private IEnumerator DisplayRoutine(
        string textValue,
        Vector3 worldPosition,
        bool isBigText
    )
    {
        float startDelay = isBigText
            ? bigTextStartDelay
            : normalStartDelay;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        TextMeshProUGUI prefab = isBigText
            ? BigtextUI
            : textPrefab;

        List<TextMeshProUGUI> pool = isBigText
            ? bigTextPool
            : normalTextPool;

        TextMeshProUGUI textObject = GetTextFromPool(
            pool,
            prefab,
            isBigText ? "BigWinText" : "FloatingText"
        );

        if (textObject == null)
        {
            yield break;
        }

        int playVersion = BeginPlay(textObject);

        Vector2 offset = isBigText
            ? bigTextOffset
            : normalTextOffset;

        PrepareText(
            textObject,
            textValue,
            worldPosition,
            offset
        );

        float scaleDuration = isBigText
            ? bigTextScaleDuration
            : normalScaleDuration;

        float riseDistance = isBigText
            ? bigTextRiseDistance
            : normalRiseDistance;

        Vector3 targetScale = isBigText
            ? bigTextTargetScale
            : Vector3.one;

        yield return StartCoroutine(
            ScaleAndMoveRoutine(
                textObject,
                playVersion,
                scaleDuration,
                riseDistance,
                targetScale
            )
        );

        if (!IsCurrentPlay(textObject, playVersion))
        {
            yield break;
        }

        float visibleDuration = isBigText
            ? bigTextVisibleDuration
            : normalVisibleDuration;

        if (visibleDuration > 0f)
        {
            yield return new WaitForSeconds(visibleDuration);
        }

        float fadeDuration = isBigText
            ? bigTextFadeDuration
            : normalFadeDuration;

        yield return StartCoroutine(
            FadeRoutine(
                textObject,
                playVersion,
                fadeDuration
            )
        );

        if (!IsCurrentPlay(textObject, playVersion))
        {
            yield break;
        }

        float disableDelay = isBigText
            ? bigTextDisableDelay
            : normalDisableDelay;

        if (disableDelay > 0f)
        {
            yield return new WaitForSeconds(disableDelay);
        }

        DisableSafely(textObject, playVersion);
    }

    private TextMeshProUGUI GetTextFromPool(
        List<TextMeshProUGUI> pool,
        TextMeshProUGUI prefab,
        string objectName
    )
    {
        if (prefab == null)
        {
            Debug.LogError(
                "DisplayTextManager: Missing prefab for " +
                objectName + "."
            );

            return null;
        }

        if (TextHolder == null)
        {
            Debug.LogError(
                "DisplayTextManager: Text Holder is not assigned."
            );

            return null;
        }

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            TextMeshProUGUI pooledText = pool[i];

            if (pooledText == null)
            {
                pool.RemoveAt(i);
                continue;
            }

            if (!pooledText.gameObject.activeSelf)
            {
                return pooledText;
            }
        }

        TextMeshProUGUI newText = Instantiate(
            prefab,
            TextHolder,
            false
        );

        newText.name = objectName + "(Pooled)";
        newText.raycastTarget = false;
        newText.gameObject.SetActive(false);

        pool.Add(newText);

        return newText;
    }

    private int BeginPlay(TextMeshProUGUI textObject)
    {
        if (!playVersions.ContainsKey(textObject))
        {
            playVersions[textObject] = 0;
        }

        playVersions[textObject]++;

        return playVersions[textObject];
    }

    private bool IsCurrentPlay(
        TextMeshProUGUI textObject,
        int expectedVersion
    )
    {
        if (textObject == null)
        {
            return false;
        }

        return playVersions.TryGetValue(
                   textObject,
                   out int currentVersion
               ) &&
               currentVersion == expectedVersion;
    }

    private void PrepareText(
        TextMeshProUGUI textObject,
        string textValue,
        Vector3 worldPosition,
        Vector2 uiOffset
    )
    {
        RectTransform textRect = textObject.rectTransform;

        textObject.text = textValue;
        SetAlpha(textObject, 1f);

        textRect.localScale = Vector3.zero;
        textRect.localRotation = Quaternion.identity;

        SetCanvasPosition(
            textRect,
            worldPosition,
            uiOffset
        );

        textObject.transform.SetAsLastSibling();
        textObject.gameObject.SetActive(true);
    }

    private void SetCanvasPosition(
        RectTransform textRect,
        Vector3 worldPosition,
        Vector2 uiOffset
    )
    {
        if (TextHolder == null)
        {
            return;
        }

        RectTransform holderRect = TextHolder as RectTransform;
        Canvas canvas = TextHolder.GetComponentInParent<Canvas>();
        Camera worldCamera = mainCamera != null
            ? mainCamera
            : Camera.main;

        if (holderRect == null)
        {
            Debug.LogError(
                "DisplayTextManager: Text Holder must be a RectTransform."
            );

            return;
        }

        if (canvas == null)
        {
            Debug.LogError(
                "DisplayTextManager: Text Holder must be inside a Canvas."
            );

            return;
        }

        if (worldCamera == null)
        {
            Debug.LogError(
                "DisplayTextManager: Main Camera is not assigned " +
                "and Camera.main was not found."
            );

            return;
        }

        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                worldCamera,
                worldPosition
            );

        Camera canvasCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = canvas.worldCamera != null
                ? canvas.worldCamera
                : worldCamera;
        }

        bool converted =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                holderRect,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition
            );

        if (!converted)
        {
            Debug.LogWarning(
                "DisplayTextManager: Could not convert world position " +
                "to Canvas position."
            );

            return;
        }

        textRect.anchoredPosition =
            localPosition + uiOffset;
    }

    private IEnumerator ScaleAndMoveRoutine(
        TextMeshProUGUI textObject,
        int playVersion,
        float duration,
        float riseDistance,
        Vector3 targetScale
    )
    {
        if (textObject == null)
        {
            yield break;
        }

        RectTransform textRect = textObject.rectTransform;
        Vector2 startPosition = textRect.anchoredPosition;
        Vector2 targetPosition =
            startPosition + Vector2.up * riseDistance;

        if (duration <= 0f)
        {
            textRect.localScale = targetScale;
            textRect.anchoredPosition = targetPosition;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsCurrentPlay(textObject, playVersion) ||
                !textObject.gameObject.activeSelf)
            {
                yield break;
            }

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            float easedProgress = EaseOutBack(progress);

            textRect.localScale = Vector3.LerpUnclamped(
                Vector3.zero,
                targetScale,
                easedProgress
            );

            textRect.anchoredPosition = Vector2.Lerp(
                startPosition,
                targetPosition,
                Mathf.SmoothStep(0f, 1f, progress)
            );

            yield return null;
        }

        if (IsCurrentPlay(textObject, playVersion))
        {
            textRect.localScale = targetScale;
            textRect.anchoredPosition = targetPosition;
        }
    }

    private IEnumerator FadeRoutine(
        TextMeshProUGUI textObject,
        int playVersion,
        float duration
    )
    {
        if (textObject == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            SetAlpha(textObject, 0f);
            yield break;
        }

        Color startColor = textObject.color;
        Color targetColor = new Color(
            startColor.r,
            startColor.g,
            startColor.b,
            0f
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsCurrentPlay(textObject, playVersion) ||
                !textObject.gameObject.activeSelf)
            {
                yield break;
            }

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            textObject.color = Color.Lerp(
                startColor,
                targetColor,
                progress
            );

            yield return null;
        }

        if (IsCurrentPlay(textObject, playVersion))
        {
            textObject.color = targetColor;
        }
    }

    private void DisableSafely(
        TextMeshProUGUI textObject,
        int playVersion
    )
    {
        if (!IsCurrentPlay(textObject, playVersion))
        {
            return;
        }

        if (textObject.gameObject.activeSelf)
        {
            textObject.gameObject.SetActive(false);
        }
    }

    private static void SetAlpha(
        TextMeshProUGUI textObject,
        float alpha
    )
    {
        Color color = textObject.color;
        color.a = Mathf.Clamp01(alpha);
        textObject.color = color;
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.70158f;
        float shiftedValue = value - 1f;

        return 1f +
               (overshoot + 1f) *
               shiftedValue *
               shiftedValue *
               shiftedValue +
               overshoot *
               shiftedValue *
               shiftedValue;
    }
}
