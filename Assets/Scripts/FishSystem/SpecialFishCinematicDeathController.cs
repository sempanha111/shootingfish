using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SpecialFishCinematicDeathController : MonoBehaviour
{
    private static readonly HashSet<int> activeSequenceInstanceIds =
        new HashSet<int>();

    private bool registeredAsActiveSequence;

    public static int ActiveSequenceCount
    {
        get { return activeSequenceInstanceIds.Count; }
    }

    public static bool AnySequenceRunning
    {
        get { return activeSequenceInstanceIds.Count > 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveSequenceRegistry()
    {
        activeSequenceInstanceIds.Clear();
    }

    [Header("Reusable Cinematic Profile")]
    [SerializeField]
    private SpecialFishCinematicDeathProfile profile;

    [Header("Optional Overrides")]
    [SerializeField] private FishScript owner;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Camera targetCamera;

    [Header("Project Speed Compatibility")]
    [Tooltip(
        "Scales only the cinematic screen-travel speeds. The original " +
        "Armored Crab defaults were authored for a faster world scale. " +
        "The recommended value for this project is 0.50."
    )]
    [SerializeField, Range(0.10f, 1.50f)]
    private float cinematicMovementSpeedScale = 0.50f;

    [Header("Runtime Information")]
    [SerializeField] private bool sequenceRunning;
    [SerializeField] private int finalShooterId;
    [SerializeField] private int finalGunLevel;
    [SerializeField] private Transform finalAttackerTransform;

    private Coroutine sequenceRoutine;
    private GameManager gameManager;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private bool[] originalRendererEnabled;
    private Collider2D[] cachedColliders;
    private bool[] originalColliderEnabled;
    private Vector3 originalScale;
    private bool visualStateCached;
    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private float originalTimeScale = 1f;
    private bool timeScaleChanged;
    private float accumulatedRotation;
    private Vector3 finalExplosionPosition;
    private bool movementShakeLoopActive;

    private readonly List<Vector3> runtimeRoute = new List<Vector3>(12);
    private readonly List<CinematicRoutePoint> runtimeRouteSettings =
        new List<CinematicRoutePoint>(12);
    private readonly HashSet<FishScript> damagedFish =
        new HashSet<FishScript>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[96];

    public bool HasProfile
    {
        get { return profile != null; }
    }

    public bool IsRunning
    {
        get { return sequenceRunning; }
    }

    public SpecialFishCinematicDeathProfile Profile
    {
        get { return profile; }
    }

    private void OnValidate()
    {
        if (cinematicMovementSpeedScale <= 0.01f)
        {
            cinematicMovementSpeedScale = 0.50f;
        }

        cinematicMovementSpeedScale = Mathf.Clamp(
            cinematicMovementSpeedScale,
            0.10f,
            1.50f
        );
    }

    private void Awake()
    {
        CacheReferences();
        CacheVisualState();
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        sequenceRunning = false;
        UnregisterActiveSequence();
        movementShakeLoopActive = false;
        RestoreTimeScale();
        EnsureVisualStateCached();
        RestoreVisualState();
        finalAttackerTransform = null;
    }

    public void ResetForPool()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        sequenceRunning = false;
        UnregisterActiveSequence();
        movementShakeLoopActive = false;
        finalShooterId = 0;
        finalGunLevel = 1;
        finalAttackerTransform = null;
        accumulatedRotation = 0f;
        RestoreTimeScale();

        // ResetForPool can be called by FishScript.OnEnable before this
        // component's Awake callback has cached its authored prefab scale.
        // Cache lazily so the first activation never restores Vector3.zero.
        EnsureVisualStateCached();
        RestoreVisualState();
    }


    public float ModifyIncomingDamage(float rawDamage)
    {
        float safeDamage = Mathf.Max(0f, rawDamage);

        if (profile == null || sequenceRunning)
        {
            return safeDamage;
        }

        return safeDamage * (1f - Mathf.Clamp01(profile.damageResistance));
    }

    public void CaptureFinalAttacker(int shooterId, int gunLevel)
    {
        finalShooterId = Mathf.Max(0, shooterId);
        finalGunLevel = Mathf.Max(1, gunLevel);
        finalAttackerTransform = ResolveAttackerTransform(
            finalShooterId,
            finalGunLevel
        );
    }

    public bool TryBeginCinematicDeath(
        float rewardAmount,
        int bulletId,
        int activeGunLevel
    )
    {
        if (profile == null || sequenceRunning || !isActiveAndEnabled)
        {
            return false;
        }

        CacheReferences();
        CaptureFinalAttacker(bulletId, activeGunLevel);

        if (owner == null)
        {
            return false;
        }

        owner.PrepareForCinematicDeath(false);
        CacheVisualState();

        if (profile.disableCollidersDuringSequence)
        {
            SetCollidersEnabled(false);
        }

        sequenceRunning = true;
        RegisterActiveSequence();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        accumulatedRotation = transform.eulerAngles.z;
        originalTimeScale = Time.timeScale;
        timeScaleChanged = false;

        sequenceRoutine = StartCoroutine(
            RunSequence(rewardAmount, bulletId, activeGunLevel)
        );

        return true;
    }

    private IEnumerator RunSequence(
        float rewardAmount,
        int bulletId,
        int activeGunLevel
    )
    {
        if (profile.enableConvulsion)
        {
            yield return RunConvulsionStage();
        }

        if (profile.enableMainCharge)
        {
            yield return RunMainChargeStage(bulletId);
        }

        if (profile.certificateTiming ==
            CinematicCertificateTiming.RouteStart)
        {
            yield return PlayCertificateAfterDelay();
        }

        if (profile.enableFullScreenMovement)
        {
            yield return RunFullScreenMovement(
                bulletId,
                activeGunLevel
            );
        }

        if (profile.approachFinalAttacker)
        {
            Vector3 attackerTarget = ResolveTargetPosition(
                CinematicTargetType.FinalAttacker,
                new Vector2(0.5f, 0.5f)
            );

            yield return MoveToPosition(
                attackerTarget,
                GetScaledCinematicSpeed(profile.attackerApproachSpeed),
                profile.movementScale,
                profile.movementEndRotationSpeed,
                profile.movementRedStrength,
                profile.movementPulseStrength
            );

            yield return Wait(profile.attackerPauseDuration);
        }

        if (profile.certificateTiming ==
            CinematicCertificateTiming.BeforeFinalCharge)
        {
            yield return PlayCertificateAfterDelay();
        }

        finalExplosionPosition = ResolveTargetPosition(
            profile.finalTarget,
            profile.finalCustomViewport
        );

        yield return MoveToPosition(
            finalExplosionPosition,
            GetScaledCinematicSpeed(profile.finalMoveSpeed),
            profile.finalChargeScale,
            profile.finalChargeRotationSpeed,
            profile.finalChargeRedStrength,
            profile.finalChargePulseStrength
        );

        yield return RunFinalChargeStage();

        if (profile.certificateTiming ==
            CinematicCertificateTiming.FinalExplosion)
        {
            yield return PlayCertificateAfterDelay();
        }

        yield return RunFinalExplosion(
            rewardAmount,
            bulletId,
            activeGunLevel
        );

        if (profile.rewardTiming == CinematicRewardTiming.SequenceEnd)
        {
            owner.ResolveCinematicDeathReward(rewardAmount, bulletId);
        }

        float requiredCleanupDelay = profile.finalCleanupDelay;

        if (profile.enableBossSkill && profile.bossSkillIndex >= 0)
        {
            requiredCleanupDelay = Mathf.Max(
                requiredCleanupDelay,
                profile.bossSkillDelay + 0.08f
            );
        }

        if (profile.optionalFinalDeathPrefab != null)
        {
            requiredCleanupDelay = Mathf.Max(
                requiredCleanupDelay,
                profile.optionalFinalPrefabDelay + 0.08f
            );
        }

        yield return Wait(requiredCleanupDelay);

        CleanupSequence();
        owner.FinishCinematicDeath(rewardAmount, bulletId);
    }

    private IEnumerator RunConvulsionStage()
    {
        TrySetAnimatorTrigger(profile.convulsionAnimatorTrigger);
        PlaySound(profile.convulsionSound);
        PlayDirectEffect(
            profile.convulsionEffectPrefab,
            transform.position,
            1f,
            0f,
            profile.convulsionEffectVisibleDuration,
            3300
        );

        Vector3 stageStartPosition = transform.position;
        Quaternion stageStartRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < profile.convulsionDuration && sequenceRunning)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(
                elapsed / profile.convulsionDuration
            );
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            float wave = Mathf.Sin(
                elapsed * profile.convulsionFrequency * Mathf.PI * 2f
            );

            Vector2 shake = Random.insideUnitCircle *
                profile.convulsionPositionStrength * envelope;

            transform.position = stageStartPosition +
                new Vector3(shake.x, shake.y, 0f);
            transform.rotation = stageStartRotation * Quaternion.Euler(
                0f,
                0f,
                wave * profile.convulsionRotationDegrees * envelope
            );

            float scale = 1f + wave *
                profile.convulsionScaleStrength * envelope;
            transform.localScale = originalScale * scale;

            ApplyRedTint(
                profile.convulsionRedStrength * envelope
            );

            yield return null;
        }

        transform.position = stageStartPosition;
        transform.rotation = stageStartRotation;
        transform.localScale = originalScale;
    }

    private IEnumerator RunMainChargeStage(int bulletId)
    {
        TrySetAnimatorTrigger(profile.mainDeathAnimatorTrigger);
        PlaySound(profile.mainChargeSound);
        PlayOptionalNormalNetBackground(transform.position);

        bool sequenceNetBoomFirst =
            profile.chargePresentationOrder ==
            CinematicChargePresentationOrder.NetBoomThenChargeEffects &&
            profile.playNetBoomAtCharge;

        if (sequenceNetBoomFirst)
        {
            // The complete main-charge motion begins immediately at the
            // start of Stage 2A. Scale growth, pulse, rotation, and red tint
            // continue while the Net Boom prelude is playing. Stage 2B only
            // adds the charge prefabs and earthquake after the Net Boom ends.
            if (!profile.startChargeEarthquakeWithEffects)
            {
                PlayChargeEarthquake();
            }

            yield return RunSequencedMainChargeStage(bulletId);
            yield break;
        }

        // Simultaneous/legacy presentation: all presentation effects start
        // immediately, then the same complete main-charge motion runs.
        if (!profile.startChargeEarthquakeWithEffects)
        {
            PlayChargeEarthquake();
        }

        if (profile.playNetBoomAtCharge)
        {
            StartCoroutine(
                PlayNetBoomBurst(
                    transform.position,
                    profile.chargeNetBoomIndex,
                    profile.chargeNetBoomCount,
                    profile.chargeNetBoomInterval,
                    0.20f,
                    bulletId,
                    1f,
                    Vector2.zero,
                    false,
                    0
                )
            );
        }

        PlayChargeEffectsAndOptionalEarthquake();
        yield return RunMainChargeMotion();
    }

    private IEnumerator RunSequencedMainChargeStage(int bulletId)
    {
        int safeCount = Mathf.Clamp(
            profile.chargeNetBoomCount,
            1,
            24
        );
        float safeInterval = Mathf.Max(
            0f,
            profile.chargeNetBoomInterval
        );
        float burstSpan = safeCount > 1
            ? safeInterval * (safeCount - 1)
            : 0f;
        float minimumReadEnd = burstSpan +
            Mathf.Max(0f, profile.chargeNetBoomReadDuration);
        float extraDelay = Mathf.Max(
            0f,
            profile.chargeEffectDelayAfterNetBoom
        );
        float configuredDuration = Mathf.Max(
            0.01f,
            profile.mainChargeDuration
        );
        float minimumStage2BDuration = Mathf.Max(
            0f,
            profile.chargeStage2BMinimumDuration
        );

        int spawned = 0;
        float nextSpawnTime = 0f;
        float detectedNetBoomEnd = 0f;
        float elapsed = 0f;
        float totalDuration = configuredDuration;
        bool stage2BStarted = false;

        while (sequenceRunning)
        {
            while (spawned < safeCount &&
                   elapsed + 0.0001f >= nextSpawnTime)
            {
                Vector2 offset = Random.insideUnitCircle * 0.20f;
                float detectedDuration = PlayNetBoomAndGetDuration(
                    transform.position +
                    new Vector3(offset.x, offset.y, 0f),
                    profile.chargeNetBoomIndex,
                    bulletId
                );

                detectedNetBoomEnd = Mathf.Max(
                    detectedNetBoomEnd,
                    elapsed + detectedDuration
                );
                spawned++;
                nextSpawnTime = spawned * safeInterval;
            }

            float requiredPreludeEnd = Mathf.Max(
                minimumReadEnd,
                detectedNetBoomEnd
            ) + extraDelay;

            if (!stage2BStarted &&
                spawned >= safeCount &&
                elapsed >= requiredPreludeEnd)
            {
                stage2BStarted = true;

                // Guarantee that Stage 2B remains visible for at least its
                // configured minimum time, even when a long Net Boom consumes
                // most of Main Charge Duration.
                totalDuration = Mathf.Max(
                    configuredDuration,
                    elapsed + minimumStage2BDuration
                );

                // Stage 2B begins: charge prefabs and earthquake start on the
                // same frame. The main charge motion does not restart; it
                // continues seamlessly from Stage 2A.
                PlayChargeEffectsAndOptionalEarthquake();
            }

            ApplyMainChargeMotionFrame(elapsed, totalDuration);

            if (stage2BStarted && elapsed >= totalDuration)
            {
                break;
            }

            elapsed += DeltaTime;
            yield return null;
        }

        if (sequenceRunning)
        {
            ApplyMainChargeMotionFrame(totalDuration, totalDuration);
        }
    }

    private IEnumerator RunMainChargeMotion()
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(
            0.01f,
            profile.mainChargeDuration
        );

        while (elapsed < safeDuration && sequenceRunning)
        {
            ApplyMainChargeMotionFrame(elapsed, safeDuration);
            elapsed += DeltaTime;
            yield return null;
        }

        if (sequenceRunning)
        {
            ApplyMainChargeMotionFrame(safeDuration, safeDuration);
        }
    }

    private void ApplyMainChargeMotionFrame(
        float elapsed,
        float duration
    )
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float normalized = Mathf.Clamp01(elapsed / safeDuration);
        float eased = Mathf.SmoothStep(0f, 1f, normalized);
        float pulseFrequency = Mathf.Max(
            0.1f,
            profile.mainChargePulseFrequency
        );
        float pulse = Mathf.Sin(elapsed * pulseFrequency) *
            profile.mainChargePulseStrength;

        float scale = Mathf.Lerp(
            1f,
            profile.mainChargeScale,
            eased
        ) + pulse;

        accumulatedRotation +=
            profile.mainChargeRotationSpeed * DeltaTime;
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            accumulatedRotation
        );
        transform.localScale = originalScale *
            Mathf.Max(0.05f, scale);
        ApplyRedTint(
            profile.mainChargeRedStrength * eased
        );
    }

    private void PlayChargeEffectsAndOptionalEarthquake()
    {
        Vector3 chargeCenter = transform.position + new Vector3(
            profile.chargeEffectOffset.x,
            profile.chargeEffectOffset.y,
            0f
        );

        PlayEffectArray(
            profile.chargeEffectPrefabs,
            chargeCenter,
            profile.chargeEffectScatterRadius,
            profile.chargeEffectVisibleDuration,
            3340,
            profile.chargeEffectScale
        );

        if (profile.startChargeEarthquakeWithEffects)
        {
            PlayChargeEarthquake();
        }
    }

    private void PlayChargeEarthquake()
    {
        if (profile.chargeCameraShakeStrength <= 0f ||
            profile.chargeCameraShakeDuration <= 0f ||
            gameManager == null)
        {
            return;
        }

        gameManager.PlayEarthquake(
            profile.chargeCameraShakeDuration,
            profile.chargeCameraShakeStrength
        );
    }

    private IEnumerator RunFullScreenMovement(
        int bulletId,
        int activeGunLevel
    )
    {
        PlaySound(profile.movementSound);
        BuildRuntimeRoute();

        movementShakeLoopActive =
            profile.enableContinuousMovementShake;

        if (movementShakeLoopActive)
        {
            StartCoroutine(ContinuousMovementShakeLoop());
        }

        int routeCount = runtimeRoute.Count;

        for (int i = 0; i < routeCount && sequenceRunning; i++)
        {
            CinematicRoutePoint point = runtimeRouteSettings[i];
            float routeProgress = routeCount <= 1
                ? 1f
                : i / (float)(routeCount - 1);
            float rotationSpeed = Mathf.Lerp(
                profile.movementStartRotationSpeed,
                profile.movementEndRotationSpeed,
                routeProgress
            );

            yield return MoveToPosition(
                runtimeRoute[i],
                GetScaledCinematicSpeed(profile.movementSpeed) *
                    Mathf.Max(0.1f, point.speedMultiplier),
                profile.movementScale,
                rotationSpeed,
                profile.movementRedStrength,
                profile.movementPulseStrength
            );

            if (point.triggerArrivalExplosion)
            {
                TriggerRouteArrival(
                    transform.position,
                    bulletId,
                    activeGunLevel
                );
            }

            if (profile.certificateTiming ==
                    CinematicCertificateTiming.AfterRoutePoint &&
                i == profile.certificateAfterRoutePoint)
            {
                yield return PlayCertificateAfterDelay();
            }

            yield return Wait(point.pauseDuration);
        }

        movementShakeLoopActive = false;
    }

    private float GetScaledCinematicSpeed(float configuredSpeed)
    {
        float scale = Mathf.Clamp(
            cinematicMovementSpeedScale <= 0.01f
                ? 0.50f
                : cinematicMovementSpeedScale,
            0.10f,
            1.50f
        );

        return Mathf.Max(0.05f, configuredSpeed * scale);
    }

    private IEnumerator ContinuousMovementShakeLoop()
    {
        while (sequenceRunning && movementShakeLoopActive)
        {
            if (gameManager != null &&
                profile.continuousMovementShakeStrength > 0f &&
                profile.continuousMovementShakeDuration > 0f)
            {
                gameManager.PlayEarthquake(
                    profile.continuousMovementShakeDuration,
                    profile.continuousMovementShakeStrength
                );
            }

            yield return Wait(
                profile.continuousMovementShakeInterval
            );
        }
    }

    private void TriggerRouteArrival(
        Vector3 position,
        int bulletId,
        int activeGunLevel
    )
    {
        PlaySound(profile.movementArrivalSound);

        if (profile.movementArrivalShakeStrength > 0f &&
            profile.movementArrivalShakeDuration > 0f &&
            gameManager != null)
        {
            gameManager.PlayEarthquake(
                profile.movementArrivalShakeDuration,
                profile.movementArrivalShakeStrength
            );
        }

        if (profile.playNetBoomAtRoutePoints)
        {
            StartCoroutine(
                PlayNetBoomBurst(
                    position,
                    profile.movementNetBoomIndex,
                    profile.movementNetBoomCount,
                    profile.movementExplosionInterval,
                    profile.movementExplosionScatterRadius,
                    bulletId,
                    profile.movementNetBoomScale,
                    profile.movementNetBoomOffset,
                    profile.movementEffectsMatchOwnerSorting,
                    profile.movementNetBoomSortingOrderOffset
                )
            );
        }

        PlayOptionalNormalNetBackground(position);

        Vector3 arrivalEffectCenter = position + new Vector3(
            profile.movementArrivalEffectOffset.x,
            profile.movementArrivalEffectOffset.y,
            0f
        );

        PlayEffectArray(
            profile.movementArrivalEffectPrefabs,
            arrivalEffectCenter,
            profile.movementExplosionScatterRadius,
            profile.movementArrivalEffectVisibleDuration,
            3450,
            profile.movementArrivalEffectScale,
            profile.movementEffectsMatchOwnerSorting,
            profile.movementArrivalEffectSortingOrderOffset
        );

        ApplyAreaDamage(
            position,
            profile.movementAreaDamage,
            bulletId,
            activeGunLevel
        );
    }

    private IEnumerator RunFinalChargeStage()
    {
        PlaySound(profile.finalChargeSound);

        if (profile.finalChargeShakeStrength > 0f &&
            profile.finalChargeShakeDuration > 0f &&
            gameManager != null)
        {
            gameManager.PlayEarthquake(
                profile.finalChargeShakeDuration,
                profile.finalChargeShakeStrength
            );
        }

        float elapsed = 0f;

        while (elapsed < profile.finalChargeDuration && sequenceRunning)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(
                elapsed / profile.finalChargeDuration
            );
            float pulse = Mathf.Sin(elapsed * 12f) *
                profile.finalChargePulseStrength;
            float scale = Mathf.Lerp(
                profile.movementScale,
                profile.finalChargeScale,
                Mathf.SmoothStep(0f, 1f, normalized)
            ) + pulse;

            accumulatedRotation +=
                profile.finalChargeRotationSpeed * DeltaTime;
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                accumulatedRotation
            );
            transform.localScale = originalScale * scale;
            ApplyRedTint(
                Mathf.Lerp(
                    profile.movementRedStrength,
                    profile.finalChargeRedStrength,
                    normalized
                )
            );

            yield return null;
        }
    }

    private IEnumerator RunFinalExplosion(
        float rewardAmount,
        int bulletId,
        int activeGunLevel
    )
    {
        PlaySound(profile.finalExplosionSound);

        if (profile.enableSlowMotion)
        {
            BeginSlowMotion();
        }

        if (profile.finalExplosionShakeStrength > 0f &&
            profile.finalExplosionShakeDuration > 0f &&
            gameManager != null)
        {
            gameManager.PlayEarthquake(
                profile.finalExplosionShakeDuration,
                profile.finalExplosionShakeStrength
            );
        }

        PlayDirectEffect(
            profile.mainBombExplosionPrefab,
            finalExplosionPosition,
            profile.mainBombScale,
            Random.Range(0f, 360f),
            profile.mainBombVisibleDuration,
            profile.mainBombSortingOrder
        );

        PlayOptionalNormalNetBackground(finalExplosionPosition);

        StartCoroutine(
            PlayFinalBackgroundExplosions(bulletId)
        );

        PlayEffectArray(
            profile.additionalFinalExplosionPrefabs,
            finalExplosionPosition,
            Mathf.Max(0.1f, profile.backgroundExplosionRadius * 0.55f),
            profile.finalEffectVisibleDuration,
            profile.mainBombSortingOrder + 20
        );

        ApplyAreaDamage(
            finalExplosionPosition,
            profile.finalAreaDamage,
            bulletId,
            activeGunLevel
        );

        if (profile.rewardTiming ==
            CinematicRewardTiming.FinalExplosion)
        {
            owner.ResolveCinematicDeathReward(rewardAmount, bulletId);
        }

        if (profile.enableBossSkill && profile.bossSkillIndex >= 0)
        {
            StartCoroutine(
                PlayBossSkillAfterDelay(
                    bulletId,
                    activeGunLevel
                )
            );
        }

        if (profile.optionalFinalDeathPrefab != null)
        {
            StartCoroutine(PlayOptionalFinalPrefab());
        }

        yield return Wait(profile.hideBodyAfterExplosionDelay);
        SetRenderersEnabled(false);

        if (profile.enableSlowMotion)
        {
            yield return Wait(profile.slowMotionDuration);
            RestoreTimeScale();
        }
    }

    private IEnumerator PlayFinalBackgroundExplosions(int bulletId)
    {
        int count = Mathf.Max(0, profile.backgroundNetBoomCount);

        for (int i = 0; i < count && sequenceRunning; i++)
        {
            Vector2 offset = Random.insideUnitCircle *
                profile.backgroundExplosionRadius;
            Vector3 position = finalExplosionPosition +
                new Vector3(offset.x, offset.y, 0f);

            float randomScale = Random.Range(
                profile.backgroundExplosionScaleRange.x,
                profile.backgroundExplosionScaleRange.y
            );

            // Stage 8 uses the configured random scale for the pooled
            // Net Boom itself, not only for optional companion prefabs.
            // AnimatiorManager restores the prefab's authored scale when
            // the effect returns to the pool.
            PlayNetBoom(
                position,
                profile.finalNetBoomIndex,
                bulletId,
                randomScale,
                false,
                0
            );

            if (profile.additionalFinalExplosionPrefabs != null &&
                profile.additionalFinalExplosionPrefabs.Length > 0 &&
                Random.value < 0.42f)
            {
                GameObject prefab = GetRandomValidPrefab(
                    profile.additionalFinalExplosionPrefabs
                );
                PlayDirectEffect(
                    prefab,
                    position,
                    randomScale,
                    Random.Range(0f, 360f),
                    profile.finalEffectVisibleDuration,
                    profile.mainBombSortingOrder + 10 + i
                );
            }

            yield return Wait(profile.backgroundExplosionInterval);
        }
    }

    private IEnumerator PlayBossSkillAfterDelay(
        int bulletId,
        int activeGunLevel
    )
    {
        yield return Wait(profile.bossSkillDelay);

        if (!sequenceRunning || gameManager == null ||
            gameManager.animatiorManager == null)
        {
            yield break;
        }

        GameObject[] skillPrefabs =
            gameManager.animatiorManager.mainBossFrontGunSkillPrefabs;

        if (skillPrefabs == null ||
            profile.bossSkillIndex < 0 ||
            profile.bossSkillIndex >= skillPrefabs.Length ||
            skillPrefabs[profile.bossSkillIndex] == null)
        {
            yield break;
        }

        Vector3 skillPosition = ResolveTargetPosition(
            profile.bossSkillPosition,
            profile.bossSkillCustomViewport
        );

        PlaySound(profile.bossSkillSound);
        PlayDirectEffect(
            skillPrefabs[profile.bossSkillIndex],
            skillPosition,
            profile.bossSkillScale,
            profile.bossSkillRotation,
            profile.bossSkillVisibleDuration,
            profile.bossSkillSortingOrder
        );

        if (!profile.bossSkillVisualOnly)
        {
            ApplyAreaDamage(
                skillPosition,
                profile.bossSkillAreaDamage,
                bulletId,
                activeGunLevel
            );
        }
    }

    private IEnumerator PlayOptionalFinalPrefab()
    {
        yield return Wait(profile.optionalFinalPrefabDelay);

        if (!sequenceRunning)
        {
            yield break;
        }

        PlaySound(profile.optionalFinalPrefabSound);
        PlayDirectEffect(
            profile.optionalFinalDeathPrefab,
            GetViewportWorldPosition(new Vector2(0.5f, 0.5f)),
            profile.optionalFinalPrefabScale,
            profile.optionalFinalPrefabRotation,
            profile.optionalFinalPrefabVisibleDuration,
            profile.optionalFinalPrefabSortingOrder
        );
    }

    private IEnumerator PlayCertificateAfterDelay()
    {
        if (profile.certificateTiming ==
            CinematicCertificateTiming.Disabled)
        {
            yield break;
        }

        yield return Wait(profile.certificateSpawnDelay);

        if (!sequenceRunning || gameManager == null ||
            gameManager.animatiorManager == null)
        {
            yield break;
        }

        Vector3 position = ResolveTargetPosition(
            profile.certificatePosition,
            profile.certificateCustomViewport
        );

        PlaySound(profile.certificateSound);

        if (profile.useExistingCertificateSystem)
        {
            gameManager.animatiorManager.PlayCertificateText(
                position,
                profile.certificatePrefabIndex
            );
        }
        else
        {
            PlayDirectEffect(
                profile.directCertificatePrefab,
                position,
                profile.certificateScale,
                profile.certificateRotation,
                profile.certificateVisibleDuration,
                profile.certificateSortingOrder
            );
        }
    }

    private IEnumerator MoveToPosition(
        Vector3 target,
        float speed,
        float targetScaleMultiplier,
        float rotationSpeed,
        float redStrength,
        float pulseStrength
    )
    {
        Vector3 start = transform.position;
        float distance = Vector2.Distance(start, target);
        float duration = Mathf.Max(0.08f, distance / Mathf.Max(0.1f, speed));
        float elapsed = 0f;

        while (elapsed < duration && sequenceRunning)
        {
            elapsed += DeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = profile.movementCurve != null
                ? profile.movementCurve.Evaluate(normalized)
                : Mathf.SmoothStep(0f, 1f, normalized);

            transform.position = Vector3.LerpUnclamped(
                start,
                target,
                curved
            );

            accumulatedRotation += rotationSpeed * DeltaTime;
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                accumulatedRotation
            );

            float pulse = Mathf.Sin(elapsed * 8f) * pulseStrength;
            transform.localScale = originalScale *
                (targetScaleMultiplier + pulse);
            ApplyRedTint(redStrength);

            yield return null;
        }

        transform.position = target;
    }

    private void BuildRuntimeRoute()
    {
        runtimeRoute.Clear();
        runtimeRouteSettings.Clear();

        if (profile.routeMode != CinematicRouteMode.Automatic &&
            profile.manualRoutePoints != null)
        {
            for (int i = 0; i < profile.manualRoutePoints.Length; i++)
            {
                CinematicRoutePoint point = profile.manualRoutePoints[i];

                if (point == null)
                {
                    continue;
                }

                runtimeRoute.Add(
                    ResolveTargetPosition(
                        point.targetType,
                        point.viewportPosition
                    )
                );
                runtimeRouteSettings.Add(point);
            }
        }

        if (profile.routeMode !=
            CinematicRouteMode.ManualViewportPoints)
        {
            int pointsToAdd = Mathf.Max(
                0,
                profile.automaticRoutePointCount - runtimeRoute.Count
            );

            Vector2[] pattern =
            {
                new Vector2(0.86f, 0.84f),
                new Vector2(0.14f, 0.16f),
                new Vector2(0.14f, 0.84f),
                new Vector2(0.86f, 0.16f),
                new Vector2(0.50f, 0.50f),
                new Vector2(0.78f, 0.54f),
                new Vector2(0.24f, 0.48f)
            };

            int start = Random.Range(0, pattern.Length);

            for (int i = 0; i < pointsToAdd; i++)
            {
                Vector2 viewport = pattern[(start + i) % pattern.Length];
                viewport += Random.insideUnitCircle * 0.035f;

                CinematicRoutePoint generated =
                    new CinematicRoutePoint();
                generated.targetType =
                    CinematicTargetType.CustomViewport;
                generated.viewportPosition = viewport;
                generated.speedMultiplier = Random.Range(0.88f, 1.18f);
                generated.pauseDuration = Random.Range(0.10f, 0.22f);
                generated.triggerArrivalExplosion = true;

                runtimeRoute.Add(GetViewportWorldPosition(viewport));
                runtimeRouteSettings.Add(generated);
            }
        }

        if (runtimeRoute.Count == 0)
        {
            CinematicRoutePoint fallback = new CinematicRoutePoint();
            fallback.targetType = CinematicTargetType.ScreenCenter;
            runtimeRoute.Add(
                GetViewportWorldPosition(new Vector2(0.5f, 0.5f))
            );
            runtimeRouteSettings.Add(fallback);
        }
    }

    private Vector3 ResolveTargetPosition(
        CinematicTargetType targetType,
        Vector2 customViewport
    )
    {
        switch (targetType)
        {
            case CinematicTargetType.ScreenCenter:
                return GetViewportWorldPosition(
                    new Vector2(0.5f, 0.5f)
                );

            case CinematicTargetType.RandomViewport:
                return GetViewportWorldPosition(
                    new Vector2(
                        Random.Range(
                            profile.viewportEdgePadding,
                            1f - profile.viewportEdgePadding
                        ),
                        Random.Range(
                            profile.viewportEdgePadding,
                            1f - profile.viewportEdgePadding
                        )
                    )
                );

            case CinematicTargetType.DensestFishArea:
                return FindDensestFishPosition();

            case CinematicTargetType.FinalAttacker:
                return GetFinalAttackerPosition();

            case CinematicTargetType.CustomViewport:
            default:
                return GetViewportWorldPosition(customViewport);
        }
    }

    private Vector3 GetFinalAttackerPosition()
    {
        if (finalAttackerTransform == null)
        {
            finalAttackerTransform = ResolveAttackerTransform(
                finalShooterId,
                finalGunLevel
            );
        }

        if (finalAttackerTransform == null)
        {
            return GetViewportWorldPosition(new Vector2(0.5f, 0.5f));
        }

        Camera camera = ActiveCamera;

        if (camera == null)
        {
            return transform.position;
        }

        Vector3 viewport = camera.WorldToViewportPoint(
            finalAttackerTransform.position
        );
        viewport.x += profile.attackerViewportOffset.x;
        viewport.y += profile.attackerViewportOffset.y;

        return GetViewportWorldPosition(
            new Vector2(viewport.x, viewport.y)
        );
    }

    private Vector3 FindDensestFishPosition()
    {
        if (gameManager == null ||
            gameManager.fishInScreenList == null ||
            gameManager.fishInScreenList.Count == 0)
        {
            return GetViewportWorldPosition(new Vector2(0.5f, 0.5f));
        }

        FishScript best = null;
        int bestNearbyCount = -1;
        float neighborhoodSqr = 3.2f * 3.2f;
        List<FishScript> fishes = gameManager.fishInScreenList;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishScript candidate = fishes[i];

            if (candidate == null || candidate == owner ||
                !candidate.IsAliveTarget)
            {
                continue;
            }

            int nearby = 0;
            Vector3 candidatePosition = candidate.transform.position;

            for (int j = 0; j < fishes.Count; j++)
            {
                FishScript other = fishes[j];

                if (other == null || other == owner ||
                    !other.IsAliveTarget)
                {
                    continue;
                }

                if ((other.transform.position - candidatePosition)
                    .sqrMagnitude <= neighborhoodSqr)
                {
                    nearby++;
                }
            }

            if (nearby > bestNearbyCount)
            {
                bestNearbyCount = nearby;
                best = candidate;
            }
        }

        if (best == null)
        {
            return GetViewportWorldPosition(new Vector2(0.5f, 0.5f));
        }

        Camera camera = ActiveCamera;

        if (camera == null)
        {
            return best.transform.position;
        }

        Vector3 bestViewport = camera.WorldToViewportPoint(
            best.transform.position
        );

        return GetViewportWorldPosition(
            new Vector2(bestViewport.x, bestViewport.y)
        );
    }

    private Vector3 GetViewportWorldPosition(Vector2 viewport)
    {
        Camera camera = ActiveCamera;

        if (camera == null)
        {
            return transform.position;
        }

        float padding = profile != null
            ? Mathf.Clamp(profile.viewportEdgePadding, 0.02f, 0.45f)
            : 0.10f;

        viewport.x = Mathf.Clamp(viewport.x, padding, 1f - padding);
        viewport.y = Mathf.Clamp(viewport.y, padding, 1f - padding);

        float depth = Mathf.Abs(
            transform.position.z - camera.transform.position.z
        );
        Vector3 world = camera.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, depth)
        );
        world.z = transform.position.z;
        return world;
    }

    private Transform ResolveAttackerTransform(
        int shooterId,
        int gunLevel
    )
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager == null)
        {
            return null;
        }

        if (shooterId == 0 && gameManager.weaponsScripts != null)
        {
            if (gameManager.weaponsScripts.activeGun != null)
            {
                return gameManager.weaponsScripts.activeGun.transform;
            }

            return gameManager.weaponsScripts.transform;
        }

        GameObject[] guns = null;
        Transform fallback = null;

        if (shooterId == 1 && gameManager.gun1 != null)
        {
            guns = gameManager.gun1.Gun;
            fallback = gameManager.gun1.transform;
        }
        else if (shooterId == 2 && gameManager.gun2 != null)
        {
            guns = gameManager.gun2.Gun;
            fallback = gameManager.gun2.transform;
        }
        else if (shooterId == 3 && gameManager.gun3 != null)
        {
            guns = gameManager.gun3.Gun;
            fallback = gameManager.gun3.transform;
        }

        if (guns != null && guns.Length > 0)
        {
            int index = Mathf.Clamp(gunLevel - 1, 0, guns.Length - 1);

            if (guns[index] != null)
            {
                return guns[index].transform;
            }
        }

        return fallback;
    }

    private void ApplyAreaDamage(
        Vector3 center,
        CinematicAreaDamageSettings settings,
        int bulletId,
        int activeGunLevel
    )
    {
        if (settings == null || !settings.enabled || settings.damage <= 0f)
        {
            return;
        }

        damagedFish.Clear();

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            center,
            Mathf.Max(0.1f, settings.radius),
            overlapBuffer,
            settings.fishLayers
        );

        int affectedCount = 0;
        int maximumTargets = Mathf.Clamp(
            settings.maximumTargets,
            1,
            overlapBuffer.Length
        );

        for (int i = 0; i < hitCount && affectedCount < maximumTargets; i++)
        {
            Collider2D hit = overlapBuffer[i];

            if (hit == null)
            {
                continue;
            }

            FishScript victim = hit.GetComponentInParent<FishScript>();

            if (victim == null || victim == owner ||
                !victim.IsAliveTarget || damagedFish.Contains(victim))
            {
                continue;
            }

            FishTier tier = victim.GetFishTier();

            if ((settings.mainBossImmune && tier == FishTier.MainBoss) ||
                (settings.specialFishImmune && tier == FishTier.Special))
            {
                continue;
            }

            float tierMultiplier = GetTierDamageMultiplier(
                tier,
                settings
            );

            if (tierMultiplier <= 0f)
            {
                continue;
            }

            float distanceMultiplier = 1f;

            if (settings.useDistanceFalloff)
            {
                float distance = Vector2.Distance(
                    center,
                    victim.GetTargetCenterWorld()
                );
                float normalized = Mathf.Clamp01(
                    distance / Mathf.Max(0.1f, settings.radius)
                );
                distanceMultiplier = Mathf.Lerp(
                    1f,
                    settings.minimumFalloffMultiplier,
                    normalized
                );
            }

            float damage = settings.damage *
                tierMultiplier * distanceMultiplier;

            if (damage <= 0f)
            {
                continue;
            }

            damagedFish.Add(victim);
            affectedCount++;

            float healthBefore = victim.CurrentHealth;
            SpriteRenderer targetRenderer =
                victim.GetComponent<SpriteRenderer>();

            if (targetRenderer == null)
            {
                targetRenderer =
                    victim.GetComponentInChildren<SpriteRenderer>();
            }

            victim.TakeDamage(
                targetRenderer,
                damage,
                bulletId,
                activeGunLevel
            );

            if (settings.requestReaction &&
                healthBefore > damage &&
                victim.IsAliveTarget)
            {
                victim.PlayExternalExplosionReaction(
                    settings.reactionDuration,
                    settings.reactionScaleStrength,
                    settings.reactionRotationDegrees,
                    settings.reactionRedStrength
                );
            }
        }
    }

    private static float GetTierDamageMultiplier(
        FishTier tier,
        CinematicAreaDamageSettings settings
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                return settings.smallMultiplier;
            case FishTier.Medium:
                return settings.mediumMultiplier;
            case FishTier.Special:
                return settings.specialMultiplier;
            case FishTier.MiniBoss:
                return settings.miniBossMultiplier;
            case FishTier.MainBoss:
                return settings.mainBossMultiplier;
            default:
                return 1f;
        }
    }

    private void PlayOptionalNormalNetBackground(Vector3 position)
    {
        if (profile == null ||
            !profile.allowNormalNetBackgroundEffect ||
            gameManager == null)
        {
            return;
        }

        gameManager.SpawnNet(
            Mathf.Max(1, finalGunLevel),
            position,
            0.30f
        );
    }

    private IEnumerator PlayNetBoomBurst(
        Vector3 center,
        int effectIndex,
        int count,
        float interval,
        float scatterRadius,
        int bulletId,
        float scale,
        Vector2 fixedOffset,
        bool matchOwnerSorting,
        int sortingOrderOffset
    )
    {
        int safeCount = Mathf.Clamp(count, 1, 24);
        Vector3 offsetCenter = center + new Vector3(
            fixedOffset.x,
            fixedOffset.y,
            0f
        );

        for (int i = 0; i < safeCount && sequenceRunning; i++)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle * scatterRadius;
            PlayNetBoom(
                offsetCenter + new Vector3(
                    randomOffset.x,
                    randomOffset.y,
                    0f
                ),
                effectIndex,
                bulletId,
                scale,
                matchOwnerSorting,
                sortingOrderOffset + i
            );
            yield return Wait(interval);
        }
    }

    private float PlayNetBoomAndGetDuration(
        Vector3 position,
        int effectIndex,
        int bulletId
    )
    {
        if (gameManager == null ||
            gameManager.animatiorManager == null)
        {
            return 0f;
        }

        float duration =
            gameManager.animatiorManager.PlayNetBoomAndGetDuration(
                position,
                bulletId,
                owner != null ? owner.id : 0,
                effectIndex
            );

        if (gameManager.SoundManager != null)
        {
            gameManager.SoundManager.PlayNetBoomSound();
        }

        return duration;
    }

    private void PlayNetBoom(
        Vector3 position,
        int effectIndex,
        int bulletId
    )
    {
        PlayNetBoom(
            position,
            effectIndex,
            bulletId,
            1f,
            false,
            0
        );
    }

    private void PlayNetBoom(
        Vector3 position,
        int effectIndex,
        int bulletId,
        float scale,
        bool matchOwnerSorting,
        int sortingOrderOffset
    )
    {
        if (gameManager == null ||
            gameManager.animatiorManager == null)
        {
            return;
        }

        int sortingLayerId = int.MinValue;
        int sortingOrder = int.MinValue;

        if (matchOwnerSorting)
        {
            ResolveOwnerSorting(
                out sortingLayerId,
                out int ownerSortingOrder
            );
            sortingOrder = ownerSortingOrder + sortingOrderOffset;
        }

        gameManager.animatiorManager.PlayNetBoomAdvanced(
            position,
            bulletId,
            owner != null ? owner.id : 0,
            effectIndex,
            Vector3.one * Mathf.Max(0.01f, scale),
            sortingLayerId,
            sortingOrder
        );

        if (gameManager.SoundManager != null)
        {
            gameManager.SoundManager.PlayNetBoomSound();
        }
    }

    private void PlayEffectArray(
        GameObject[] prefabs,
        Vector3 center,
        float scatterRadius,
        float visibleDuration,
        int sortingOrder,
        float scale = 1f,
        bool matchOwnerSorting = false,
        int ownerSortingOrderOffset = 0
    )
    {
        if (prefabs == null)
        {
            return;
        }

        int sortingLayerId = int.MinValue;
        int resolvedBaseOrder = sortingOrder;

        if (matchOwnerSorting)
        {
            ResolveOwnerSorting(
                out sortingLayerId,
                out int ownerSortingOrder
            );
            resolvedBaseOrder =
                ownerSortingOrder + ownerSortingOrderOffset;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
            {
                continue;
            }

            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            PlayDirectEffect(
                prefabs[i],
                center + new Vector3(offset.x, offset.y, 0f),
                scale,
                Random.Range(0f, 360f),
                visibleDuration,
                resolvedBaseOrder + i,
                sortingLayerId
            );
        }
    }

    private void PlayDirectEffect(
        GameObject prefab,
        Vector3 position,
        float scale,
        float rotation,
        float visibleDuration,
        int sortingOrder,
        int sortingLayerId = int.MinValue
    )
    {
        if (prefab == null || gameManager == null ||
            gameManager.animatiorManager == null)
        {
            return;
        }

        gameManager.animatiorManager.PlayPooledEffectAdvanced(
            prefab,
            position,
            rotation,
            Vector3.one * Mathf.Max(0.01f, scale),
            visibleDuration,
            0.15f,
            sortingOrder,
            sortingLayerId
        );
    }

    private void ResolveOwnerSorting(
        out int sortingLayerId,
        out int sortingOrder
    )
    {
        SortingGroup group = GetComponent<SortingGroup>();

        if (group != null)
        {
            sortingLayerId = group.sortingLayerID;
            sortingOrder = group.sortingOrder;
            return;
        }

        sortingLayerId = 0;
        sortingOrder = 0;
        bool found = false;

        if (renderers == null)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];

            if (candidate == null)
            {
                continue;
            }

            if (!found || candidate.sortingOrder > sortingOrder)
            {
                sortingLayerId = candidate.sortingLayerID;
                sortingOrder = candidate.sortingOrder;
                found = true;
            }
        }
    }

    private static GameObject GetRandomValidPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        int start = Random.Range(0, prefabs.Length);

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject candidate = prefabs[(start + i) % prefabs.Length];

            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CacheReferences()
    {
        if (owner == null)
        {
            owner = GetComponent<FishScript>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodyAnimator == null)
        {
            bodyAnimator = GetComponent<Animator>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
    }

    private void CacheVisualState()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        originalRendererEnabled = new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i] != null
                ? renderers[i].color
                : Color.white;
            originalRendererEnabled[i] = renderers[i] != null &&
                renderers[i].enabled;
        }

        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        originalColliderEnabled = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            originalColliderEnabled[i] = cachedColliders[i] != null &&
                cachedColliders[i].enabled;
        }

        originalScale = transform.localScale;
        originalRotation = transform.rotation;
        originalPosition = transform.position;
        visualStateCached = true;
    }

    private void EnsureVisualStateCached()
    {
        if (visualStateCached)
        {
            return;
        }

        CacheReferences();
        CacheVisualState();
    }

    private void RestoreVisualState()
    {
        EnsureVisualStateCached();
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                renderers[i].color =
                    i < originalColors.Length
                        ? originalColors[i]
                        : Color.white;
                renderers[i].enabled =
                    i < originalRendererEnabled.Length
                        ? originalRendererEnabled[i]
                        : true;
            }
        }

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == null)
                {
                    continue;
                }

                cachedColliders[i].enabled =
                    i < originalColliderEnabled.Length &&
                    originalColliderEnabled[i];
            }
        }

        transform.localScale = originalScale;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (cachedColliders == null)
        {
            return;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = enabled;
            }
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabled;
            }
        }
    }

    private void ApplyRedTint(float strength)
    {
        if (renderers == null)
        {
            return;
        }

        float safeStrength = Mathf.Clamp01(strength);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Color baseColor = i < originalColors.Length
                ? originalColors[i]
                : Color.white;
            Color target = new Color(
                1f,
                baseColor.g * 0.18f,
                baseColor.b * 0.18f,
                baseColor.a
            );
            renderer.color = Color.Lerp(
                baseColor,
                target,
                safeStrength
            );
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void TrySetAnimatorTrigger(string triggerName)
    {
        if (bodyAnimator == null ||
            !bodyAnimator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        int triggerHash = Animator.StringToHash(triggerName);
        AnimatorControllerParameter[] parameters =
            bodyAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == triggerHash &&
                parameters[i].type == AnimatorControllerParameterType.Trigger)
            {
                bodyAnimator.ResetTrigger(triggerHash);
                bodyAnimator.SetTrigger(triggerHash);
                return;
            }
        }
    }

    private void BeginSlowMotion()
    {
        if (timeScaleChanged)
        {
            return;
        }

        originalTimeScale = Time.timeScale;
        Time.timeScale = profile.slowMotionTimeScale;
        timeScaleChanged = true;
    }

    private void RestoreTimeScale()
    {
        if (!timeScaleChanged)
        {
            return;
        }

        Time.timeScale = originalTimeScale;
        timeScaleChanged = false;
    }

    private void CleanupSequence()
    {
        if (profile.stopCameraShakeOnCleanup && gameManager != null)
        {
            gameManager.StopEarthquake();
        }

        if (profile.restoreTimeScaleOnCleanup)
        {
            RestoreTimeScale();
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        finalAttackerTransform = null;
        movementShakeLoopActive = false;
        sequenceRunning = false;
        UnregisterActiveSequence();
        sequenceRoutine = null;
    }

    private void RegisterActiveSequence()
    {
        int instanceId = GetInstanceID();

        if (registeredAsActiveSequence &&
            activeSequenceInstanceIds.Contains(instanceId))
        {
            return;
        }

        activeSequenceInstanceIds.Add(instanceId);
        registeredAsActiveSequence = true;
    }

    private void UnregisterActiveSequence()
    {
        activeSequenceInstanceIds.Remove(GetInstanceID());
        registeredAsActiveSequence = false;
    }

    private IEnumerator Wait(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration && sequenceRunning)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private float DeltaTime
    {
        get
        {
            return profile != null && profile.useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }
    }

    private Camera ActiveCamera
    {
        get
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }
    }
}
