using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reference-style arcade abilities for the local player. Skills recharge by
/// time and do not use, sell, or purchase any real-world item. Every visual
/// reference is optional; gameplay remains functional when effects are empty.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArcadePowerSkillController : MonoBehaviour
{
    private struct LightningTarget
    {
        public FishScript fish;
        public int lifeVersion;

        public LightningTarget(FishScript target)
        {
            fish = target;
            lifeVersion = target != null
                ? target.TargetLifeVersion
                : 0;
        }
    }

    [Header("Automatic References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WeaponsScripts weaponsScripts;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform effectParent;
    [SerializeField] private AudioSource skillAudioSource;

    [Header("Freeze Wave")]
    [SerializeField, Min(0.1f)] private float freezeDuration = 4.5f;
    [SerializeField, Range(0.02f, 1f)]
    private float normalFishSpeedMultiplier = 0.08f;
    [SerializeField, Range(0.02f, 1f)]
    private float miniBossSpeedMultiplier = 0.34f;
    [SerializeField, Range(0.02f, 1f)]
    private float mainBossSpeedMultiplier = 0.52f;
    [SerializeField, Range(0.02f, 1f)]
    private float epicBossSpeedMultiplier = 0.62f;
    [SerializeField, Range(1, 5)] private int freezeMaximumCharges = 2;
    [SerializeField, Range(0, 5)] private int freezeStartingCharges = 2;
    [SerializeField, Min(0.1f)] private float freezeRechargeSeconds = 22f;
    [SerializeField] private GameObject freezeWorldEffectPrefab;
    [SerializeField, Min(0.05f)] private float freezeEffectLifetime = 1.2f;
    [SerializeField] private AudioClip freezeSound;

    [Header("Freeze UI - Optional")]
    [SerializeField] private Button freezeButton;
    [SerializeField] private Image freezeCooldownFill;
    [SerializeField] private Text freezeCooldownText;
    [SerializeField] private Text freezeChargeText;

    [Header("Lightning Chain")]
    [SerializeField, Min(0.1f)] private float lightningBaseDamage = 3500f;
    [SerializeField, Range(2, 12)] private int lightningMaximumTargets = 6;
    [SerializeField, Min(0.25f)] private float lightningJumpRange = 3.8f;
    [SerializeField, Range(0.25f, 1f)] private float lightningDamageFalloff = 0.82f;
    [SerializeField, Range(0.05f, 1f)]
    private float lightningMiniBossDamageMultiplier = 0.62f;
    [SerializeField, Range(0.02f, 1f)]
    private float lightningMainBossDamageMultiplier = 0.32f;
    [SerializeField, Range(0.02f, 1f)]
    private float lightningEpicBossDamageMultiplier = 0.22f;
    [SerializeField, Min(0f)] private float lightningJumpDelay = 0.055f;
    [SerializeField, Range(1, 5)] private int lightningMaximumCharges = 2;
    [SerializeField, Range(0, 5)] private int lightningStartingCharges = 2;
    [SerializeField, Min(0.1f)] private float lightningRechargeSeconds = 18f;
    [SerializeField] private GameObject lightningHitEffectPrefab;
    [SerializeField] private GameObject lightningSegmentPrefab;
    [SerializeField, Min(0.05f)] private float lightningEffectLifetime = 0.45f;
    [SerializeField] private AudioClip lightningSound;

    [Header("Lightning UI - Optional")]
    [SerializeField] private Button lightningButton;
    [SerializeField] private Image lightningCooldownFill;
    [SerializeField] private Text lightningCooldownText;
    [SerializeField] private Text lightningChargeText;

    private readonly List<GameObject> effectPool =
        new List<GameObject>();
    private readonly List<FishScript> lightningCandidates =
        new List<FishScript>();
    private readonly List<LightningTarget> lightningChain =
        new List<LightningTarget>();
    private readonly HashSet<FishScript> selectedLightningFish =
        new HashSet<FishScript>();

    private int freezeCharges;
    private int lightningCharges;
    private float nextFreezeRechargeTime = -1f;
    private float nextLightningRechargeTime = -1f;
    private float freezeActiveUntil = -1f;
    private float nextFreezeRefreshTime;
    private bool initialized;
    private Coroutine lightningRoutine;

    public int FreezeCharges
    {
        get { return freezeCharges; }
    }

    public int LightningCharges
    {
        get { return lightningCharges; }
    }

    public bool FreezeActive
    {
        get { return Time.time < freezeActiveUntil; }
    }

    private void Awake()
    {
        ResolveReferences();
        InitializeRuntimeState();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!initialized)
        {
            InitializeRuntimeState();
        }

        RefreshUi();
    }

    private void OnDisable()
    {
        ClearFreezeFromCurrentFish();

        if (lightningRoutine != null)
        {
            StopCoroutine(lightningRoutine);
            lightningRoutine = null;
        }
    }

    private void Update()
    {
        ResolveReferences();
        RefreshCharges();
        MaintainFreezeWave();
        RefreshUi();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (weaponsScripts == null)
        {
            weaponsScripts = gameManager != null
                ? gameManager.weaponsScripts
                : FindObjectOfType<WeaponsScripts>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void InitializeRuntimeState()
    {
        freezeMaximumCharges = Mathf.Max(1, freezeMaximumCharges);
        lightningMaximumCharges = Mathf.Max(1, lightningMaximumCharges);
        freezeCharges = Mathf.Clamp(
            freezeStartingCharges,
            0,
            freezeMaximumCharges
        );
        lightningCharges = Mathf.Clamp(
            lightningStartingCharges,
            0,
            lightningMaximumCharges
        );
        nextFreezeRechargeTime = freezeCharges < freezeMaximumCharges
            ? Time.time + Mathf.Max(0.1f, freezeRechargeSeconds)
            : -1f;
        nextLightningRechargeTime = lightningCharges < lightningMaximumCharges
            ? Time.time + Mathf.Max(0.1f, lightningRechargeSeconds)
            : -1f;
        initialized = true;
    }

    public void ActivateFreezeWave()
    {
        RefreshCharges();

        if (freezeCharges <= 0 || FreezeActive)
        {
            return;
        }

        ResolveReferences();
        float duration = Mathf.Max(0.1f, freezeDuration);
        int affected = ApplyFreezeToVisibleFish(duration);

        if (affected <= 0)
        {
            return;
        }

        ConsumeFreezeCharge();
        freezeActiveUntil = Time.time + duration;
        nextFreezeRefreshTime = Time.time + 0.15f;

        PlayEffect(
            freezeWorldEffectPrefab,
            GetScreenCenterWorldPoint(),
            Quaternion.identity,
            freezeEffectLifetime
        );
        PlaySkillSound(freezeSound);

        if (gameManager != null)
        {
            gameManager.PlayEarthquake(0.20f, 0.035f);
        }

        RefreshUi();
    }

    // Compatibility aliases make the methods easy to find in Button OnClick.
    public void FreezeSkill()
    {
        ActivateFreezeWave();
    }

    public void ActivateLightningChain()
    {
        RefreshCharges();

        if (lightningCharges <= 0 || lightningRoutine != null)
        {
            return;
        }

        ResolveReferences();

        if (!BuildLightningChain())
        {
            return;
        }

        ConsumeLightningCharge();
        PlaySkillSound(lightningSound);

        if (gameManager != null)
        {
            gameManager.PlayEarthquake(0.16f, 0.025f);
        }

        lightningRoutine = StartCoroutine(PlayLightningChainRoutine());
        RefreshUi();
    }

    public void LightningSkill()
    {
        ActivateLightningChain();
    }

    private void ConsumeFreezeCharge()
    {
        freezeCharges = Mathf.Max(0, freezeCharges - 1);

        if (freezeCharges < freezeMaximumCharges &&
            nextFreezeRechargeTime < 0f)
        {
            nextFreezeRechargeTime = Time.time +
                Mathf.Max(0.1f, freezeRechargeSeconds);
        }
    }

    private void ConsumeLightningCharge()
    {
        lightningCharges = Mathf.Max(0, lightningCharges - 1);

        if (lightningCharges < lightningMaximumCharges &&
            nextLightningRechargeTime < 0f)
        {
            nextLightningRechargeTime = Time.time +
                Mathf.Max(0.1f, lightningRechargeSeconds);
        }
    }

    private void RefreshCharges()
    {
        float now = Time.time;

        while (freezeCharges < freezeMaximumCharges &&
               nextFreezeRechargeTime >= 0f &&
               now >= nextFreezeRechargeTime)
        {
            freezeCharges++;
            nextFreezeRechargeTime = freezeCharges < freezeMaximumCharges
                ? nextFreezeRechargeTime + Mathf.Max(0.1f, freezeRechargeSeconds)
                : -1f;
        }

        while (lightningCharges < lightningMaximumCharges &&
               nextLightningRechargeTime >= 0f &&
               now >= nextLightningRechargeTime)
        {
            lightningCharges++;
            nextLightningRechargeTime =
                lightningCharges < lightningMaximumCharges
                    ? nextLightningRechargeTime +
                      Mathf.Max(0.1f, lightningRechargeSeconds)
                    : -1f;
        }
    }

    private void MaintainFreezeWave()
    {
        if (!FreezeActive)
        {
            return;
        }

        if (Time.time < nextFreezeRefreshTime)
        {
            return;
        }

        nextFreezeRefreshTime = Time.time + 0.15f;
        float remaining = Mathf.Max(0.02f, freezeActiveUntil - Time.time);
        ApplyFreezeToVisibleFish(remaining);
    }

    private int ApplyFreezeToVisibleFish(float duration)
    {
        if (gameManager == null || gameManager.fishInScreenList == null)
        {
            return 0;
        }

        int affected = 0;
        List<FishScript> fishList = gameManager.fishInScreenList;

        for (int i = fishList.Count - 1; i >= 0; i--)
        {
            FishScript fish = fishList[i];

            if (!IsValidVisibleTarget(fish))
            {
                continue;
            }

            fish.ApplyExternalMovementSlow(
                duration,
                GetFreezeMultiplier(fish)
            );
            affected++;
        }

        FishScript lockedFish = weaponsScripts != null
            ? weaponsScripts.CurrentTrackedFish
            : null;

        if (IsValidVisibleTarget(lockedFish) &&
            !fishList.Contains(lockedFish))
        {
            lockedFish.ApplyExternalMovementSlow(
                duration,
                GetFreezeMultiplier(lockedFish)
            );
            affected++;
        }

        return affected;
    }

    private float GetFreezeMultiplier(FishScript fish)
    {
        if (fish == null)
        {
            return 1f;
        }

        if (fish.IsEpicBoss)
        {
            return epicBossSpeedMultiplier;
        }

        FishTier tier = fish.GetFishTier();

        if (tier == FishTier.MainBoss)
        {
            return mainBossSpeedMultiplier;
        }

        if (tier == FishTier.MiniBoss)
        {
            return miniBossSpeedMultiplier;
        }

        return normalFishSpeedMultiplier;
    }

    private void ClearFreezeFromCurrentFish()
    {
        if (gameManager == null || gameManager.fishInScreenList == null)
        {
            return;
        }

        for (int i = gameManager.fishInScreenList.Count - 1; i >= 0; i--)
        {
            FishScript fish = gameManager.fishInScreenList[i];

            if (fish != null)
            {
                fish.ClearExternalMovementSlow();
            }
        }

        freezeActiveUntil = -1f;
    }

    private bool BuildLightningChain()
    {
        lightningCandidates.Clear();
        lightningChain.Clear();
        selectedLightningFish.Clear();

        if (gameManager == null || gameManager.fishInScreenList == null)
        {
            return false;
        }

        for (int i = 0; i < gameManager.fishInScreenList.Count; i++)
        {
            FishScript candidate = gameManager.fishInScreenList[i];

            if (IsValidVisibleTarget(candidate) &&
                !lightningCandidates.Contains(candidate))
            {
                lightningCandidates.Add(candidate);
            }
        }

        FishScript lockedFish = weaponsScripts != null
            ? weaponsScripts.CurrentTrackedFish
            : null;

        if (IsValidVisibleTarget(lockedFish) &&
            !lightningCandidates.Contains(lockedFish))
        {
            lightningCandidates.Add(lockedFish);
        }

        if (lightningCandidates.Count == 0)
        {
            return false;
        }

        FishScript current = lockedFish;

        if (!IsValidVisibleTarget(current))
        {
            current = SelectBestLightningStart();
        }

        if (current == null)
        {
            return false;
        }

        int targetLimit = Mathf.Clamp(lightningMaximumTargets, 2, 12);

        while (current != null && lightningChain.Count < targetLimit)
        {
            lightningChain.Add(new LightningTarget(current));
            selectedLightningFish.Add(current);
            current = FindNextLightningTarget(current);
        }

        return lightningChain.Count > 0;
    }

    private FishScript SelectBestLightningStart()
    {
        FishScript best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < lightningCandidates.Count; i++)
        {
            FishScript candidate = lightningCandidates[i];
            FishTier tier = candidate.GetFishTier();
            float tierScore = tier == FishTier.MainBoss
                ? 500f
                : tier == FishTier.MiniBoss
                    ? 350f
                    : tier == FishTier.Special
                        ? 240f
                        : tier == FishTier.Medium
                            ? 120f
                            : 30f;
            float score = tierScore +
                (1f - candidate.CurrentHealthNormalized) * 40f +
                Random.Range(0f, 4f);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private FishScript FindNextLightningTarget(FishScript current)
    {
        FishScript nearest = null;
        float maximumDistance = Mathf.Max(0.25f, lightningJumpRange);
        float nearestDistanceSquared = maximumDistance * maximumDistance;
        Vector3 currentPosition = current.GetTargetCenterWorld();

        for (int i = 0; i < lightningCandidates.Count; i++)
        {
            FishScript candidate = lightningCandidates[i];

            if (candidate == null ||
                selectedLightningFish.Contains(candidate) ||
                !IsValidVisibleTarget(candidate))
            {
                continue;
            }

            float distanceSquared = (
                candidate.GetTargetCenterWorld() - currentPosition
            ).sqrMagnitude;

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private IEnumerator PlayLightningChainRoutine()
    {
        Vector3 previousPosition = weaponsScripts != null &&
                                   weaponsScripts.activeGun != null
            ? weaponsScripts.activeGun.transform.position
            : GetScreenCenterWorldPoint();
        int gunLevel = weaponsScripts != null
            ? Mathf.Max(1, weaponsScripts.activeGunLevel)
            : 1;

        for (int i = 0; i < lightningChain.Count; i++)
        {
            LightningTarget entry = lightningChain[i];
            FishScript fish = entry.fish;

            if (fish == null ||
                !fish.IsAliveTarget ||
                fish.TargetLifeVersion != entry.lifeVersion)
            {
                continue;
            }

            Vector3 hitPosition = fish.GetTargetCenterWorld();
            PlayLightningSegment(previousPosition, hitPosition);
            PlayEffect(
                lightningHitEffectPrefab,
                hitPosition,
                Quaternion.identity,
                lightningEffectLifetime
            );

            float damage = lightningBaseDamage *
                Mathf.Pow(lightningDamageFalloff, i) *
                GetLightningTierMultiplier(fish);
            SpriteRenderer targetSprite =
                fish.GetComponent<SpriteRenderer>();

            if (targetSprite == null)
            {
                targetSprite = fish.GetComponentInChildren<SpriteRenderer>();
            }

            fish.TakeDamage(
                targetSprite,
                Mathf.Max(0.1f, damage),
                0,
                gunLevel
            );

            previousPosition = hitPosition;

            if (lightningJumpDelay > 0f)
            {
                yield return new WaitForSeconds(lightningJumpDelay);
            }
        }

        lightningChain.Clear();
        selectedLightningFish.Clear();
        lightningRoutine = null;
        RefreshUi();
    }

    private float GetLightningTierMultiplier(FishScript fish)
    {
        if (fish.IsEpicBoss)
        {
            return lightningEpicBossDamageMultiplier;
        }

        FishTier tier = fish.GetFishTier();

        if (tier == FishTier.MainBoss)
        {
            return lightningMainBossDamageMultiplier;
        }

        if (tier == FishTier.MiniBoss)
        {
            return lightningMiniBossDamageMultiplier;
        }

        return 1f;
    }

    private bool IsValidVisibleTarget(FishScript fish)
    {
        return fish != null &&
               fish.IsAliveTarget &&
               (targetCamera == null || fish.IsTargetVisibleTo(targetCamera));
    }

    private void PlayLightningSegment(Vector3 start, Vector3 end)
    {
        GameObject segment = PlayEffect(
            lightningSegmentPrefab,
            (start + end) * 0.5f,
            Quaternion.identity,
            lightningEffectLifetime
        );

        if (segment == null)
        {
            return;
        }

        LineRenderer line = segment.GetComponentInChildren<LineRenderer>();

        if (line != null)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    private GameObject PlayEffect(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float lifetime
    )
    {
        if (prefab == null)
        {
            return null;
        }

        string expectedName = prefab.name + "(Clone)";
        GameObject instance = null;

        for (int i = 0; i < effectPool.Count; i++)
        {
            GameObject candidate = effectPool[i];

            if (candidate != null &&
                !candidate.activeSelf &&
                candidate.name == expectedName)
            {
                instance = candidate;
                break;
            }
        }

        if (instance == null)
        {
            instance = Instantiate(prefab, effectParent);
            instance.SetActive(false);
            effectPool.Add(instance);
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        int version = ++token.playVersion;
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.SetActive(true);

        Animator effectAnimator = instance.GetComponent<Animator>();

        if (effectAnimator != null && effectAnimator.isActiveAndEnabled)
        {
            effectAnimator.Rebind();
            effectAnimator.Update(0f);
            effectAnimator.Play(0, 0, 0f);
        }

        ParticleSystem[] particles =
            instance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        StartCoroutine(
            HideEffectRoutine(
                instance,
                version,
                Mathf.Max(0.05f, lifetime)
            )
        );

        return instance;
    }

    private IEnumerator HideEffectRoutine(
        GameObject effect,
        int version,
        float delay
    )
    {
        yield return new WaitForSeconds(delay);

        if (effect == null)
        {
            yield break;
        }

        PooledEffectToken token =
            effect.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            effect.SetActive(false);
        }
    }

    private Vector3 GetScreenCenterWorldPoint()
    {
        if (targetCamera == null)
        {
            return Vector3.zero;
        }

        float distance = Mathf.Abs(targetCamera.transform.position.z);
        Vector3 center = targetCamera.ViewportToWorldPoint(
            new Vector3(0.5f, 0.5f, Mathf.Max(0.01f, distance))
        );
        center.z = 0f;
        return center;
    }

    private void PlaySkillSound(AudioClip clip)
    {
        if (skillAudioSource != null && clip != null)
        {
            skillAudioSource.PlayOneShot(clip);
        }
    }

    private void RefreshUi()
    {
        bool freezeIsActive = FreezeActive;

        if (freezeButton != null)
        {
            freezeButton.interactable = freezeCharges > 0 &&
                                       !freezeIsActive;
        }

        if (lightningButton != null)
        {
            lightningButton.interactable = lightningCharges > 0 &&
                                          lightningRoutine == null;
        }

        UpdateChargeText(freezeChargeText, freezeCharges);
        UpdateChargeText(lightningChargeText, lightningCharges);

        if (freezeIsActive)
        {
            float remaining = Mathf.Max(0f, freezeActiveUntil - Time.time);
            SetCooldownUi(
                freezeCooldownFill,
                freezeCooldownText,
                remaining / Mathf.Max(0.1f, freezeDuration),
                remaining
            );
        }
        else
        {
            UpdateRechargeUi(
                freezeCooldownFill,
                freezeCooldownText,
                freezeCharges,
                freezeMaximumCharges,
                nextFreezeRechargeTime,
                freezeRechargeSeconds
            );
        }

        UpdateRechargeUi(
            lightningCooldownFill,
            lightningCooldownText,
            lightningCharges,
            lightningMaximumCharges,
            nextLightningRechargeTime,
            lightningRechargeSeconds
        );
    }

    private static void UpdateChargeText(Text label, int charges)
    {
        if (label != null)
        {
            label.text = Mathf.Max(0, charges).ToString();
        }
    }

    private static void UpdateRechargeUi(
        Image fill,
        Text label,
        int charges,
        int maximumCharges,
        float nextRechargeTime,
        float rechargeSeconds
    )
    {
        if (charges >= maximumCharges || nextRechargeTime < 0f)
        {
            SetCooldownUi(fill, label, 0f, -1f);
            return;
        }

        float remaining = Mathf.Max(0f, nextRechargeTime - Time.time);
        float normalized = remaining / Mathf.Max(0.1f, rechargeSeconds);
        SetCooldownUi(fill, label, normalized, charges <= 0 ? remaining : -1f);
    }

    private static void SetCooldownUi(
        Image fill,
        Text label,
        float normalized,
        float visibleSeconds
    )
    {
        if (fill != null)
        {
            fill.fillAmount = Mathf.Clamp01(normalized);
        }

        if (label != null)
        {
            label.text = visibleSeconds >= 0f
                ? Mathf.CeilToInt(visibleSeconds).ToString()
                : string.Empty;
        }
    }
}
