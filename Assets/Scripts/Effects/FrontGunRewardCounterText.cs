using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Visual-only rolling/count-up reward text used inside a Main Boss
/// SkillFrontGun prefab. Supports both world-space TextMeshPro and
/// TextMeshProUGUI. It never grants coins and never changes reward values.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrontGunRewardCounterText : MonoBehaviour
{
    [Header("Text Reference")]
    [SerializeField] private TMP_Text textObject;

    [Header("Count Animation")]
    [SerializeField, Min(0f)] private float startDelay = 0.08f;
    [SerializeField, Min(0f)] private float countDuration = 0.60f;
    [SerializeField] private AnimationCurve countCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.65f, 0.88f, 1.1f, 1.1f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

    [Tooltip("0 = count from zero. 0.2 = start at 20% of the final reward.")]
    [SerializeField, Range(0f, 0.95f)] private float startPercent = 0f;

    [Header("Formatting")]
    [SerializeField] private string defaultPrefix = "+";
    [SerializeField] private bool useThousandsSeparator = true;
    [SerializeField] private bool roundToWholeCoins = true;

    [Header("Scale / Punch")]
    [SerializeField] private bool animateScale = true;
    [SerializeField] private Vector3 startScale = new Vector3(0.72f, 0.72f, 1f);
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField, Min(1f)] private float finalPunchScale = 1.12f;
    [SerializeField, Min(0f)] private float finalPunchDuration = 0.12f;
    [SerializeField, Min(0f)] private float finalHold = 0.05f;

    [Header("World Text Sorting")]
    [Tooltip("Only affects 3D/world-space TextMeshPro. TextMeshProUGUI uses Canvas sorting.")]
    [SerializeField] private bool forceWorldTextSorting = true;
    [SerializeField] private string worldSortingLayerName = "SkillFrontGun";
    [SerializeField] private int worldSortingOrder = 50;

    private Coroutine countRoutine;
    private int playVersion;
    private Vector3 authoredLocalScale = Vector3.one;

    public TMP_Text TextObject => textObject;

    private void Reset()
    {
        textObject = GetComponent<TMP_Text>();
        authoredLocalScale = transform.localScale;
        normalScale = authoredLocalScale;
    }

    private void Awake()
    {
        ResolveTextReference();
        authoredLocalScale = transform.localScale;

        // If the Inspector is still at the default scale, respect the scale
        // authored directly on the RewardText child inside the prefab.
        if (normalScale == Vector3.one && authoredLocalScale != Vector3.one)
        {
            normalScale = authoredLocalScale;
        }

        ApplyWorldTextSorting();
    }

    private void OnEnable()
    {
        ResolveTextReference();
        ApplyWorldTextSorting();
    }

    private void OnDisable()
    {
        playVersion++;

        if (countRoutine != null)
        {
            StopCoroutine(countRoutine);
            countRoutine = null;
        }

        if (textObject != null)
        {
            textObject.text = string.Empty;
        }
    }

    public void SetWorldSorting(string sortingLayerName, int sortingOrder)
    {
        worldSortingLayerName = sortingLayerName;
        worldSortingOrder = sortingOrder;
        forceWorldTextSorting = true;
        ApplyWorldTextSorting();
    }

    public void SetVisible(bool visible)
    {
        ResolveTextReference();
        if (textObject != null)
        {
            textObject.enabled = visible;
        }
    }

    public void Play(float targetReward, string prefix = null, float delayOverride = -1f)
    {
        ResolveTextReference();

        if (textObject == null)
        {
            Debug.LogWarning(
                "[FrontGunRewardCounterText] No TMP_Text is assigned on " + name + ".",
                this
            );
            return;
        }

        playVersion++;
        int version = playVersion;

        if (countRoutine != null)
        {
            StopCoroutine(countRoutine);
        }

        textObject.enabled = true;
        ApplyWorldTextSorting();

        string safePrefix = prefix == null ? defaultPrefix : prefix;
        float delay = delayOverride >= 0f ? delayOverride : startDelay;

        countRoutine = StartCoroutine(
            CountRoutine(
                Mathf.Max(0f, targetReward),
                safePrefix ?? string.Empty,
                Mathf.Max(0f, delay),
                version
            )
        );
    }

    private IEnumerator CountRoutine(
        float targetReward,
        string prefix,
        float delay,
        int version
    )
    {
        float startValue = targetReward * Mathf.Clamp01(startPercent);
        Vector3 targetScale = normalScale;
        Vector3 initialScale = animateScale
            ? Vector3.Scale(targetScale, startScale)
            : targetScale;

        transform.localScale = initialScale;
        SetNumber(prefix, startValue);

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!IsCurrent(version))
        {
            yield break;
        }

        if (countDuration <= 0f)
        {
            SetNumber(prefix, targetReward);
            transform.localScale = targetScale;
            yield return PlayFinalPunch(version, targetScale);
            countRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < countDuration)
        {
            if (!IsCurrent(version))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / countDuration);
            float curved = countCurve != null && countCurve.length > 0
                ? Mathf.Clamp01(countCurve.Evaluate(normalized))
                : normalized;

            float currentValue = Mathf.Lerp(startValue, targetReward, curved);
            SetNumber(prefix, currentValue);

            if (animateScale)
            {
                float scaleT = 1f - Mathf.Pow(1f - normalized, 3f);
                transform.localScale = Vector3.Lerp(
                    initialScale,
                    targetScale,
                    scaleT
                );
            }

            yield return null;
        }

        if (!IsCurrent(version))
        {
            yield break;
        }

        SetNumber(prefix, targetReward);
        transform.localScale = targetScale;

        yield return PlayFinalPunch(version, targetScale);

        if (finalHold > 0f && IsCurrent(version))
        {
            yield return new WaitForSeconds(finalHold);
        }

        if (IsCurrent(version))
        {
            countRoutine = null;
        }
    }

    private IEnumerator PlayFinalPunch(int version, Vector3 targetScale)
    {
        if (!animateScale || finalPunchDuration <= 0f || finalPunchScale <= 1f)
        {
            yield break;
        }

        Vector3 punchScale = targetScale * finalPunchScale;
        float half = Mathf.Max(0.001f, finalPunchDuration * 0.5f);
        float elapsed = 0f;

        while (elapsed < half)
        {
            if (!IsCurrent(version))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(
                targetScale,
                punchScale,
                Mathf.Clamp01(elapsed / half)
            );
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            if (!IsCurrent(version))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(
                punchScale,
                targetScale,
                Mathf.Clamp01(elapsed / half)
            );
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private bool IsCurrent(int version)
    {
        return version == playVersion &&
               textObject != null &&
               gameObject.activeInHierarchy;
    }

    private void ResolveTextReference()
    {
        if (textObject == null)
        {
            textObject = GetComponent<TMP_Text>();
        }
    }

    private void ApplyWorldTextSorting()
    {
        if (!forceWorldTextSorting || textObject == null)
        {
            return;
        }

        TextMeshPro worldText = textObject as TextMeshPro;
        if (worldText == null)
        {
            return;
        }

        Renderer textRenderer = worldText.GetComponent<Renderer>();
        if (textRenderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(worldSortingLayerName))
        {
            textRenderer.sortingLayerName = worldSortingLayerName;
        }

        textRenderer.sortingOrder = worldSortingOrder;
    }

    private void SetNumber(string prefix, float value)
    {
        if (textObject == null)
        {
            return;
        }

        if (roundToWholeCoins)
        {
            long wholeValue = (long)Mathf.Round(
                Mathf.Clamp(value, 0f, int.MaxValue)
            );

            textObject.text = useThousandsSeparator
                ? prefix + wholeValue.ToString("N0")
                : prefix + wholeValue.ToString();
        }
        else
        {
            textObject.text = useThousandsSeparator
                ? prefix + value.ToString("N0")
                : prefix + value.ToString("0");
        }
    }
}
