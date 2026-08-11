using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    public enum CoinBurstPattern
    {
        Auto = 0,
        // Explicit values preserve old serialized Triangle/Diamond assets.
        Triangle = 3,
        Diamond = 4
    }

    public GameObject coinprefab;
    public Transform coinparent;
    public Transform[] coinEnd;

    [Header("Visual Coin Limits")]
    [SerializeField, Range(1, 8)]
    private int maximumVisualCoinsPerPlay = 8;

    [SerializeField, Min(0.01f)]
    private float visualCoinMoveSpeed = 15f;

    [SerializeField, Min(0f)]
    private float visualCoinMoveDelay = 1f;

    [Header("Reward Ownership Presentation")]
    [Tooltip("Opacity used when GunMe (shooter ID 0) owns the reward.")]
    [SerializeField, Range(0.1f, 1f)]
    private float localPlayerCoinOpacity = 1f;

    [Tooltip("Opacity used for NPC or other-gun rewards. These coins are visual only for the local player.")]
    [SerializeField, Range(0.05f, 1f)]
    private float otherGunCoinOpacity = 0.35f;

    [SerializeField]
    private bool dimOtherGunCoins = true;

    [Header("Dynamic Coin Patterns")]
    [SerializeField] private CoinBurstPattern coinBurstPattern =
        CoinBurstPattern.Auto;

    [Tooltip("Auto uses Triangle below this reward and Diamond at or above it.")]
    [SerializeField, Min(0f)] private float diamondMinimumReward = 50f;

    [Tooltip("Base distance between coins inside every pattern. v13.13 allows much tighter layouts for boss/reward presentations.")]
    [SerializeField, Range(0.05f, 3f)]
    private float patternSpacing = 1f;

    [Tooltip("Multiplies the entire Triangle/Diamond layout without changing individual coin size. Below 1 compresses the pattern; above 1 expands it.")]
    [SerializeField, Range(0.10f, 3f)]
    private float patternScale = 1f;

    [Tooltip("Temporary visual scale applied to each pooled coin while it is active. 1 keeps the prefab's authored size.")]
    [SerializeField, Range(0.10f, 3f)]
    private float visualCoinScaleMultiplier = 1f;

    [SerializeField, HideInInspector]
    private int coinSettingsVersion;

    private const int CurrentCoinSettingsVersion = 4;

    private readonly List<GameObject> coinPooling = new List<GameObject>();
    private readonly Dictionary<GameObject, SpriteRenderer[]>
        coinRendererCache = new Dictionary<GameObject, SpriteRenderer[]>();

    private void Awake()
    {
        UpgradeCoinSettingsIfNeeded();
    }

    private void OnValidate()
    {
        UpgradeCoinSettingsIfNeeded();
        maximumVisualCoinsPerPlay = Mathf.Clamp(
            maximumVisualCoinsPerPlay,
            1,
            8
        );
        patternSpacing = Mathf.Clamp(patternSpacing, 0.05f, 3f);
        patternScale = Mathf.Clamp(patternScale, 0.10f, 3f);
        visualCoinScaleMultiplier = Mathf.Clamp(
            visualCoinScaleMultiplier,
            0.10f,
            3f
        );
        localPlayerCoinOpacity = Mathf.Clamp(
            localPlayerCoinOpacity,
            0.1f,
            1f
        );
        otherGunCoinOpacity = Mathf.Clamp(
            otherGunCoinOpacity,
            0.05f,
            1f
        );
    }

    // Compatibility entry point used by old scripts.
    public void coinAnima(FishScript fish, int bulletId)
    {
        if (fish == null)
        {
            return;
        }

        PlayCoinAnimations(
            fish.transform.position,
            fish.CoinFish,
            bulletId,
            1,
            0f,
            0f
        );
    }

    /// <summary>
    /// Plays one or more pooled coin bursts from a fixed death position.
    /// This coroutine runs on CoinManager, so it continues after the fish
    /// GameObject is returned to its pool.
    /// </summary>
    public void PlayCoinAnimations(
        Vector3 sourcePosition,
        float rewardAmount,
        int bulletId,
        int playCount,
        float interval,
        float spreadRadius
    )
    {
        PlayCoinAnimations(
            sourcePosition,
            rewardAmount,
            bulletId,
            playCount,
            interval,
            spreadRadius,
            0,
            CoinBurstPattern.Auto
        );
    }

    /// <summary>
    /// Profile-driven standard coin burst. visualCoinCountOverride 0 uses the
    /// reward amount; 1-8 requests a density which is normalized to a complete
    /// Triangle or Diamond layout.
    /// </summary>
    public void PlayCoinAnimations(
        Vector3 sourcePosition,
        float rewardAmount,
        int bulletId,
        int playCount,
        float interval,
        float spreadRadius,
        int visualCoinCountOverride,
        CoinBurstPattern patternOverride
    )
    {
        if (!IsValidCoinTarget(bulletId) || coinprefab == null)
        {
            return;
        }

        StartCoroutine(
            PlayCoinAnimationsRoutine(
                sourcePosition,
                Mathf.Max(0f, rewardAmount),
                bulletId,
                Mathf.Clamp(playCount, 1, 6),
                Mathf.Max(0f, interval),
                Mathf.Max(0f, spreadRadius),
                Mathf.Clamp(visualCoinCountOverride, 0, 8),
                patternOverride
            )
        );
    }

    private IEnumerator PlayCoinAnimationsRoutine(
        Vector3 sourcePosition,
        float rewardAmount,
        int bulletId,
        int playCount,
        float interval,
        float spreadRadius,
        int visualCoinCountOverride,
        CoinBurstPattern patternOverride
    )
    {
        for (int playIndex = 0; playIndex < playCount; playIndex++)
        {
            Vector2 randomOffset =
                spreadRadius > 0f
                    ? UnityEngine.Random.insideUnitCircle * spreadRadius
                    : Vector2.zero;

            SpawnCoinSet(
                sourcePosition + (Vector3)randomOffset,
                rewardAmount,
                bulletId,
                visualCoinCountOverride,
                patternOverride
            );

            if (playIndex < playCount - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    private void SpawnCoinSet(
        Vector3 sourcePosition,
        float rewardAmount,
        int bulletId,
        int visualCoinCountOverride,
        CoinBurstPattern patternOverride
    )
    {
        int requestedVisualCoinCount = visualCoinCountOverride > 0
            ? Mathf.Clamp(visualCoinCountOverride, 1, 8)
            : CalculateVisualCoinCount(rewardAmount);

        CoinBurstPattern selectedPattern =
            SelectPattern(rewardAmount, patternOverride);

        int visualCoinCount = GetCompletePatternCoinCount(
            requestedVisualCoinCount,
            selectedPattern
        );

        Vector3[] offsets = CreateOffsets(
            visualCoinCount,
            selectedPattern
        );

        float safePatternScale = Mathf.Clamp(patternScale, 0.10f, 3f);
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] *= safePatternScale;
        }

        for (int i = 0; i < visualCoinCount; i++)
        {
            GameObject coinObject = GetPooledCoin();

            if (coinObject == null)
            {
                return;
            }

            PooledEffectToken token =
                coinObject.GetComponent<PooledEffectToken>();

            if (token == null)
            {
                token = coinObject.AddComponent<PooledEffectToken>();
            }

            token.CaptureDefaultTransform(coinObject.transform);
            token.RestoreDefaultTransform(coinObject.transform);
            int version = ++token.playVersion;

            coinObject.transform.position = sourcePosition + offsets[i];
            float safeCoinScale = Mathf.Clamp(
                visualCoinScaleMultiplier,
                0.10f,
                3f
            );
            Vector3 defaultScale = token.defaultLocalScale;
            coinObject.transform.localScale = new Vector3(
                defaultScale.x * safeCoinScale,
                defaultScale.y * safeCoinScale,
                defaultScale.z
            );

            coinObject.SetActive(true);
            ApplyRewardOwnershipOpacity(coinObject, bulletId);

            StartCoroutine(
                MoveCoinRoutine(
                    coinObject,
                    version,
                    coinEnd[bulletId],
                    visualCoinMoveDelay
                )
            );
        }
    }

    private GameObject GetPooledCoin()
    {
        for (int i = 0; i < coinPooling.Count; i++)
        {
            GameObject pooledCoin = coinPooling[i];

            if (pooledCoin != null && !pooledCoin.activeSelf)
            {
                return pooledCoin;
            }
        }

        GameObject newCoin = Instantiate(coinprefab, coinparent);
        PooledEffectToken token =
            newCoin.GetComponent<PooledEffectToken>();
        if (token == null)
        {
            token = newCoin.AddComponent<PooledEffectToken>();
        }
        token.CaptureDefaultTransform(newCoin.transform);
        newCoin.SetActive(false);
        coinPooling.Add(newCoin);
        CacheCoinRenderers(newCoin);
        return newCoin;
    }

    private int CalculateVisualCoinCount(float rewardAmount)
    {
        float clampedReward = Mathf.Clamp(rewardAmount, 0.1f, 500f);

        int result = clampedReward < 1f ? 1 :
                     clampedReward < 5f ? 2 :
                     clampedReward < 15f ? 3 :
                     clampedReward < 25f ? 4 :
                     clampedReward < 50f ? 5 :
                     clampedReward < 80f ? 6 :
                     clampedReward <= 120f ? 7 : 8;

        return Mathf.Clamp(result, 1, Mathf.Min(8, maximumVisualCoinsPerPlay));
    }

    private CoinBurstPattern SelectPattern(
        float rewardAmount,
        CoinBurstPattern patternOverride
    )
    {
        if (patternOverride != CoinBurstPattern.Auto)
        {
            return patternOverride;
        }

        if (coinBurstPattern != CoinBurstPattern.Auto)
        {
            return coinBurstPattern;
        }

        if (rewardAmount >= diamondMinimumReward)
        {
            return CoinBurstPattern.Diamond;
        }

        return CoinBurstPattern.Triangle;
    }

    /// <summary>
    /// Prevents partial shapes. Triangle uses complete rows (1, 3, or 6
    /// coins). Diamond uses a complete perimeter (1, 4, or 8 coins).
    /// </summary>
    private int GetCompletePatternCoinCount(
        int requestedCount,
        CoinBurstPattern pattern
    )
    {
        int safeMaximum = Mathf.Clamp(
            maximumVisualCoinsPerPlay,
            1,
            8
        );
        requestedCount = Mathf.Clamp(requestedCount, 1, safeMaximum);

        if (pattern == CoinBurstPattern.Diamond)
        {
            if (requestedCount <= 1 || safeMaximum < 4)
            {
                return 1;
            }

            if (requestedCount <= 6 || safeMaximum < 8)
            {
                return 4;
            }

            return 8;
        }

        if (requestedCount <= 1 || safeMaximum < 3)
        {
            return 1;
        }

        if (requestedCount <= 4 || safeMaximum < 6)
        {
            return 3;
        }

        return 6;
    }

    private Vector3[] CreateOffsets(
        int count,
        CoinBurstPattern pattern
    )
    {
        count = Mathf.Max(1, count);
        Vector3[] offsets = new Vector3[count];

        if (count == 1)
        {
            offsets[0] = Vector3.zero;
            return offsets;
        }

        if (pattern == CoinBurstPattern.Triangle)
        {
            return CreateTriangleOffsets(count);
        }

        if (pattern == CoinBurstPattern.Diamond)
        {
            return CreateDiamondOffsets(count);
        }

        return CreateTriangleOffsets(count);
    }

    private Vector3[] CreateTriangleOffsets(int count)
    {
        Vector3[] offsets = new Vector3[count];
        int written = 0;
        int row = 0;

        while (written < count)
        {
            int rowCount = Mathf.Min(row + 1, count - written);
            float rowCenter = (rowCount - 1) * 0.5f;

            for (int column = 0;
                 column < rowCount && written < count;
                 column++)
            {
                offsets[written++] = new Vector3(
                    (column - rowCenter) * patternSpacing,
                    -row * patternSpacing * 0.78f,
                    0f
                );
            }

            row++;
        }

        CenterOffsets(offsets);
        return offsets;
    }

    private Vector3[] CreateDiamondOffsets(int count)
    {
        Vector3[] offsets = new Vector3[count];

        if (count <= 1)
        {
            offsets[0] = Vector3.zero;
            return offsets;
        }

        // Four points form the complete small diamond. Eight points add one
        // evenly spaced coin to every edge, so the shape is never partial.
        if (count == 4)
        {
            offsets[0] = new Vector3(0f, patternSpacing, 0f);
            offsets[1] = new Vector3(patternSpacing, 0f, 0f);
            offsets[2] = new Vector3(0f, -patternSpacing, 0f);
            offsets[3] = new Vector3(-patternSpacing, 0f, 0f);
            return offsets;
        }

        float outer = patternSpacing * 1.45f;
        float middle = outer * 0.5f;

        offsets[0] = new Vector3(0f, outer, 0f);
        offsets[1] = new Vector3(middle, middle, 0f);
        offsets[2] = new Vector3(outer, 0f, 0f);
        offsets[3] = new Vector3(middle, -middle, 0f);
        offsets[4] = new Vector3(0f, -outer, 0f);
        offsets[5] = new Vector3(-middle, -middle, 0f);
        offsets[6] = new Vector3(-outer, 0f, 0f);
        offsets[7] = new Vector3(-middle, middle, 0f);

        return offsets;
    }

    private static void CenterOffsets(Vector3[] offsets)
    {
        if (offsets == null || offsets.Length == 0)
        {
            return;
        }

        Vector3 average = Vector3.zero;

        for (int i = 0; i < offsets.Length; i++)
        {
            average += offsets[i];
        }

        average /= offsets.Length;

        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] -= average;
        }
    }

    private IEnumerator MoveCoinRoutine(
        GameObject coinObject,
        int version,
        Transform targetEnd,
        float delay
    )
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!IsCurrentPlay(coinObject, version) || targetEnd == null)
        {
            DisableIfCurrent(coinObject, version);
            yield break;
        }

        float distance = Vector2.Distance(
            coinObject.transform.position,
            targetEnd.position
        );

        while (IsCurrentPlay(coinObject, version) && distance > 0.01f)
        {
            coinObject.transform.position = Vector2.MoveTowards(
                coinObject.transform.position,
                targetEnd.position,
                Time.deltaTime * visualCoinMoveSpeed
            );

            distance = Vector2.Distance(
                coinObject.transform.position,
                targetEnd.position
            );

            yield return null;
        }

        if (!IsCurrentPlay(coinObject, version))
        {
            yield break;
        }

        coinObject.transform.position = targetEnd.position;
        ResetCoinVisual(coinObject);
        RestoreCoinTransform(coinObject);
        coinObject.SetActive(false);
    }

    private bool IsValidCoinTarget(int bulletId)
    {
        return coinEnd != null &&
               bulletId >= 0 &&
               bulletId < coinEnd.Length &&
               coinEnd[bulletId] != null;
    }

    private static bool IsCurrentPlay(GameObject target, int version)
    {
        if (target == null)
        {
            return false;
        }

        PooledEffectToken token = target.GetComponent<PooledEffectToken>();
        return token != null && token.playVersion == version;
    }

    private void DisableIfCurrent(GameObject target, int version)
    {
        if (IsCurrentPlay(target, version))
        {
            ResetCoinVisual(target);
            RestoreCoinTransform(target);
            target.SetActive(false);
        }
    }

    private static void RestoreCoinTransform(GameObject coinObject)
    {
        if (coinObject == null)
        {
            return;
        }

        PooledEffectToken token =
            coinObject.GetComponent<PooledEffectToken>();

        if (token != null)
        {
            token.RestoreDefaultTransform(coinObject.transform);
        }
    }

    private void ApplyRewardOwnershipOpacity(
        GameObject coinObject,
        int bulletId
    )
    {
        if (coinObject == null)
        {
            return;
        }

        float opacity = bulletId == 0 || !dimOtherGunCoins
            ? localPlayerCoinOpacity
            : otherGunCoinOpacity;

        SpriteRenderer[] renderers = GetCoinRenderers(coinObject);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = opacity;
            renderer.color = color;
        }
    }

    private void ResetCoinVisual(GameObject coinObject)
    {
        if (coinObject == null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetCoinRenderers(coinObject);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = 1f;
            renderer.color = color;
        }
    }

    private SpriteRenderer[] GetCoinRenderers(GameObject coinObject)
    {
        if (coinObject == null)
        {
            return Array.Empty<SpriteRenderer>();
        }

        if (!coinRendererCache.TryGetValue(
                coinObject,
                out SpriteRenderer[] renderers
            ) || renderers == null)
        {
            renderers = CacheCoinRenderers(coinObject);
        }

        return renderers;
    }

    private SpriteRenderer[] CacheCoinRenderers(GameObject coinObject)
    {
        SpriteRenderer[] renderers = coinObject != null
            ? coinObject.GetComponentsInChildren<SpriteRenderer>(true)
            : Array.Empty<SpriteRenderer>();

        if (coinObject != null)
        {
            coinRendererCache[coinObject] = renderers;
        }

        return renderers;
    }

    private void UpgradeCoinSettingsIfNeeded()
    {
        if (coinSettingsVersion < 2)
        {
            // Legacy pattern migration. Existing v12 components are already
            // version 2, so their authored visual-coin limit is preserved.
            maximumVisualCoinsPerPlay = 8;
            patternSpacing = 1f;
            coinBurstPattern = CoinBurstPattern.Auto;
            diamondMinimumReward = 50f;
            coinSettingsVersion = 2;
        }

        if (coinSettingsVersion < 3)
        {
            // v13 adds ownership presentation only. Do not overwrite the
            // user's existing coin count, speed, delay, or pattern settings.
            localPlayerCoinOpacity = 1f;
            otherGunCoinOpacity = 0.35f;
            dimOtherGunCoins = true;
            coinSettingsVersion = 3;
        }

        if (coinSettingsVersion < CurrentCoinSettingsVersion)
        {
            // v13.13 adds pattern compression and pooled coin-size control.
            // Existing spacing and authored prefab scale are preserved.
            patternScale = 1f;
            visualCoinScaleMultiplier = 1f;
            coinSettingsVersion = CurrentCoinSettingsVersion;
        }
    }

    // Compatibility methods retained for existing external calls.
    public void MoveObject(GameObject obj, Transform targetEnd, float delay)
    {
        if (obj == null || targetEnd == null)
        {
            return;
        }

        PooledEffectToken token = obj.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = obj.AddComponent<PooledEffectToken>();
        }

        int version = ++token.playVersion;
        obj.SetActive(true);
        StartCoroutine(MoveCoinRoutine(obj, version, targetEnd, delay));
    }
}
