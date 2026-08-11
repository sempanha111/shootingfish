using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EpicBossController : MonoBehaviour
{
    private enum RuntimeState
    {
        Inactive,
        Introduction,
        Roaming,
        PhaseReaction,
        Skill,
        Escaping,
        Dying
    }

    [Header("Epic Boss Profile")]
    [SerializeField] private EpicBossProfile profile;

    [Header("Optional Overrides")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private AudioSource bossAudioSource;
    [SerializeField] private Collider2D[] bossColliders;

    [Tooltip("Assign future boss attack scripts here. They are disabled during the introduction, reactions, and death.")]
    [SerializeField] private MonoBehaviour[] attackBehaviours;

    private FishScript owner;
    private SwapFishScript spawnDirector;
    private GameManager gameManager;
    private Camera targetCamera;

    private RuntimeState state = RuntimeState.Inactive;
    private bool configured;
    private bool deathStarted;
    private bool firstConvulsionTriggered;
    private bool fakeDeathTriggered;
    private bool strongConvulsionTriggered;
    private bool pendingFirstConvulsion;
    private bool pendingFakeDeath;
    private bool pendingStrongConvulsion;
    private bool pendingReactiveConvulsion;
    private bool reactiveConvulsionRunning;
    private bool forceReturnToArena;
    private bool timeoutWarningActive;
    private float timeoutEscapeSpeedMultiplier = 1f;
    private Vector3 timeoutEscapeTarget;
    private float timeoutEscapeStartedAt;

    private float lastIncomingHitTime = -100f;
    private int incomingHitChain;
    private float currentHealthRatio = 1f;

    private int lastShooterId;
    private int lastGunLevel = 1;
    private int lastSkillIndex = -1;
    private AudioClip lastPlayedClip;

    private Vector3 roamTarget;
    private float nextTargetChangeTime;
    private float pauseUntil;
    private float speedCycleElapsed;
    private float speedCycleDuration = 6f;
    private bool cinematicPassActive;
    private int cinematicPassStage;
    private int cinematicExitSide = 1;
    private Vector3 cinematicPassTarget;
    private float nextCinematicPassTime;
    private float cinematicPassStartedAt;
    private float cinematicReentryY;

    private Vector2 velocitySmoothReference;
    private float movementNoiseSeed;
    private Vector3 lastStuckPosition;
    private float nextStuckCheckTime;
    private bool preferUpperArena;

    private Vector3 visualDefaultLocalPosition;
    private Quaternion visualDefaultLocalRotation;
    private Vector3 visualDefaultLocalScale;
    private bool visualDefaultsCaptured;
    private SpriteRenderer[] visualRenderers;
    private Color[] visualDefaultColors;

    private bool[] attackDefaultEnabled;
    private bool[] colliderDefaultEnabled;

    private Coroutine introductionRoutine;
    private Coroutine phaseRoutine;
    private Coroutine randomSkillRoutine;
    private Coroutine deathRoutine;
    private Coroutine cameraShakeRoutine;
    private Transform shakingCameraTransform;
    private Vector3 cameraShakeBaseLocalPosition;

    public bool HasProfile
    {
        get { return profile != null; }
    }

    public bool IsActive
    {
        get { return configured && state != RuntimeState.Inactive; }
    }

    public bool CanAttack
    {
        get { return configured && state == RuntimeState.Roaming && !deathStarted; }
    }

    public bool IsPerformingCinematicPass
    {
        get { return cinematicPassActive; }
    }

    private void Awake()
    {
        CacheReferences();
        CaptureDefaults();
    }

    private void OnEnable()
    {
        CacheReferences();
        RestoreVisualDefaults();
        RestoreColliderDefaults();
        SetAttackBehavioursEnabled(false);
    }

    private void OnDisable()
    {
        StopAllRuntimeCoroutines();
        RestoreCameraAfterShake();
        RestoreVisualDefaults();
        RestoreColliderDefaults();
        SetAttackBehavioursEnabled(false);

        configured = false;
        deathStarted = false;
        pendingReactiveConvulsion = false;
        reactiveConvulsionRunning = false;
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;
        TrySetAnimatorBool(profile != null
            ? profile.sustainedFireAnimatorBool
            : string.Empty, false);
        cinematicPassActive = false;
        cinematicPassStage = 0;
        timeoutWarningActive = false;
        timeoutEscapeSpeedMultiplier = 1f;
        timeoutEscapeStartedAt = 0f;
        state = RuntimeState.Inactive;
    }

    private void FixedUpdate()
    {
        if (!configured || profile == null)
        {
            return;
        }

        if (state == RuntimeState.Escaping)
        {
            UpdateTimeoutEscape(Time.fixedDeltaTime);
            return;
        }

        if (state == RuntimeState.Roaming)
        {
            UpdateMajesticMovement(Time.fixedDeltaTime);
        }
    }

    public void ConfigureAsEpicBoss(
        FishScript fishOwner,
        SwapFishScript director
    )
    {
        if (profile == null || fishOwner == null)
        {
            return;
        }

        StopAllRuntimeCoroutines();
        RestoreCameraAfterShake();
        CacheReferences();
        CaptureDefaults();

        owner = fishOwner;
        spawnDirector = director;
        gameManager = GameManager.Instance;
        targetCamera = Camera.main;

        configured = true;
        deathStarted = false;
        firstConvulsionTriggered = false;
        fakeDeathTriggered = false;
        strongConvulsionTriggered = false;
        pendingFirstConvulsion = false;
        pendingFakeDeath = false;
        pendingStrongConvulsion = false;
        pendingReactiveConvulsion = false;
        reactiveConvulsionRunning = false;
        forceReturnToArena = false;
        timeoutWarningActive = false;
        timeoutEscapeSpeedMultiplier = 1f;
        timeoutEscapeStartedAt = 0f;
        cinematicPassActive = false;
        cinematicPassStage = 0;
        cinematicPassTarget = Vector3.zero;
        nextCinematicPassTime = 0f;
        cinematicPassStartedAt = 0f;
        cinematicReentryY = 0.5f;
        lastSkillIndex = -1;
        lastPlayedClip = null;
        speedCycleElapsed = 0f;
        velocitySmoothReference = Vector2.zero;
        movementNoiseSeed = Random.Range(0f, 1000f);
        lastStuckPosition = transform.position;
        nextStuckCheckTime = Time.time +
            Mathf.Max(0.25f, profile.stuckCheckInterval);
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;
        currentHealthRatio = 1f;
        Vector3 startingViewport = GetViewportPosition(transform.position);
        float centerAvoidance = Mathf.Clamp(
            profile.centerAvoidanceHalfHeight,
            0.06f,
            0.30f
        );
        preferUpperArena = Mathf.Abs(startingViewport.y - 0.5f) >
            centerAvoidance * 0.25f
                ? startingViewport.y >= 0.5f
                : Random.value >= 0.5f;
        speedCycleDuration = RandomRange(
            profile.speedCycleDurationRange,
            6f,
            0.25f
        );

        RestoreVisualDefaults();
        RestoreColliderDefaults();
        SetAttackBehavioursEnabled(false);

        if (profile.applyAdditionalStatMultipliers)
        {
            owner.MultiplyCurrentRuntimeStats(
                profile.hpMultiplier,
                profile.rewardMultiplier,
                profile.movementSpeedMultiplier
            );
        }

        owner.StopMovement();

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        introductionRoutine = StartCoroutine(IntroductionRoutine());
    }

    public void ResetForPool()
    {
        StopAllRuntimeCoroutines();
        RestoreCameraAfterShake();
        RestoreVisualDefaults();
        RestoreColliderDefaults();
        SetAttackBehavioursEnabled(false);

        configured = false;
        deathStarted = false;
        pendingReactiveConvulsion = false;
        reactiveConvulsionRunning = false;
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;
        currentHealthRatio = 1f;
        TrySetAnimatorBool(profile != null
            ? profile.sustainedFireAnimatorBool
            : string.Empty, false);
        cinematicPassActive = false;
        cinematicPassStage = 0;
        state = RuntimeState.Inactive;
        owner = null;
        spawnDirector = null;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    public void NotifyHealthChanged(
        float currentHp,
        float maximumHp,
        int shooterId,
        int gunLevel
    )
    {
        if (!configured || profile == null || deathStarted || maximumHp <= 0f)
        {
            return;
        }

        lastShooterId = shooterId;
        lastGunLevel = Mathf.Max(1, gunLevel);

        float healthRatio = Mathf.Clamp01(currentHp / maximumHp);
        currentHealthRatio = healthRatio;
        RegisterReactiveFireHit();

        if (!firstConvulsionTriggered &&
            healthRatio <= profile.firstConvulsionThreshold)
        {
            firstConvulsionTriggered = true;
            pendingFirstConvulsion = true;
        }

        if (!fakeDeathTriggered &&
            healthRatio <= profile.fakeDeathThreshold)
        {
            fakeDeathTriggered = true;
            pendingFakeDeath = true;
        }

        if (!strongConvulsionTriggered &&
            healthRatio <= profile.strongConvulsionThreshold)
        {
            strongConvulsionTriggered = true;
            pendingStrongConvulsion = true;
        }

        TryStartPendingPhaseSequence();
    }

    public void NotifyIncomingFireStopped()
    {
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;
        pendingReactiveConvulsion = false;
        TrySetAnimatorBool(profile != null
            ? profile.sustainedFireAnimatorBool
            : string.Empty, false);

        if (reactiveConvulsionRunning)
        {
            RestoreVisualDefaults();
        }
    }

    private void RegisterReactiveFireHit()
    {
        if (profile == null ||
            !profile.enableReactiveFireConvulsions ||
            deathStarted)
        {
            return;
        }

        float now = Time.time;
        float window = Mathf.Max(0.05f, profile.sustainedFireWindow);
        incomingHitChain = now - lastIncomingHitTime <= window
            ? incomingHitChain + 1
            : 1;
        lastIncomingHitTime = now;

        bool sustained = incomingHitChain >= Mathf.Max(
            1,
            profile.sustainedFireHitCount
        );

        if (sustained ||
            Random.value <= profile.microConvulsionChancePerHit)
        {
            pendingReactiveConvulsion = true;
        }
    }

    public bool TryBeginDeath(int shooterId, int gunLevel)
    {
        if (!configured || profile == null || deathStarted)
        {
            return false;
        }

        deathStarted = true;
        lastShooterId = shooterId;
        lastGunLevel = Mathf.Max(1, gunLevel);

        StopIntroductionRoutine();
        StopPhaseRoutine();
        StopRandomSkillRoutine();
        RestoreVisualDefaults();

        state = RuntimeState.Dying;
        SetAttackBehavioursEnabled(false);

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (profile.disableCollidersDuringDeath)
        {
            SetCollidersEnabled(false);
        }

        deathRoutine = StartCoroutine(DeathSequenceRoutine());
        return true;
    }

    public void BeginTimeoutWarning(float forcedCenterRouteChance)
    {
        if (!configured || profile == null || deathStarted)
        {
            return;
        }

        timeoutWarningActive = true;
        cinematicPassActive = false;
        pauseUntil = 0f;

        if (state == RuntimeState.Roaming &&
            Random.value <= Mathf.Clamp01(forcedCenterRouteChance))
        {
            roamTarget = GetViewportWorldPoint(
                Random.Range(0.38f, 0.62f),
                Random.Range(0.34f, 0.66f)
            );
            nextTargetChangeTime = Time.time + 3f;
            forceReturnToArena = false;
        }

        nextCinematicPassTime = Mathf.Min(
            nextCinematicPassTime,
            Time.time + 1.2f
        );
    }

    /// <summary>
    /// Releases Epic Boss movement authority to FishScript's final visible
    /// exit safety routine. This is used only after the authored timeout escape
    /// failed to leave the padded camera bounds; it never hides the boss in
    /// place and remains safe for pooled reuse.
    /// </summary>
    public void PrepareForForcedNaturalExit()
    {
        if (!configured || deathStarted)
        {
            return;
        }

        StopIntroductionRoutine();
        StopPhaseRoutine();
        StopRandomSkillRoutine();
        RestoreCameraAfterShake();
        SetAttackBehavioursEnabled(false);

        cinematicPassActive = false;
        timeoutWarningActive = false;
        pauseUntil = 0f;
        state = RuntimeState.Inactive;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    public void BeginTimeoutEscape(float speedMultiplier)
    {
        if (!configured || profile == null || deathStarted || owner == null)
        {
            return;
        }

        StopIntroductionRoutine();
        StopPhaseRoutine();
        StopRandomSkillRoutine();
        SetAttackBehavioursEnabled(false);
        cinematicPassActive = false;
        timeoutWarningActive = true;
        timeoutEscapeSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        timeoutEscapeStartedAt = Time.time;
        state = RuntimeState.Escaping;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        FishScreenSide side = body != null && body.velocity.sqrMagnitude > 0.04f
            ? (Mathf.Abs(body.velocity.x) >= Mathf.Abs(body.velocity.y)
                ? (body.velocity.x >= 0f ? FishScreenSide.Right : FishScreenSide.Left)
                : (body.velocity.y >= 0f ? FishScreenSide.Top : FishScreenSide.Bottom))
            : FishScreenBounds.GetNearestSide(targetCamera, transform.position);
        Vector3 viewport = GetViewportPosition(transform.position);
        float lane = side == FishScreenSide.Left || side == FishScreenSide.Right
            ? Mathf.Clamp(viewport.y, 0.10f, 0.90f)
            : Mathf.Clamp(viewport.x, 0.10f, 0.90f);
        Bounds visualBounds = FishScreenBounds.GetCombinedVisualBounds(
            transform,
            visualRenderers,
            bossColliders
        );
        float visualRadius = Mathf.Max(0.8f, visualBounds.extents.magnitude);

        timeoutEscapeTarget = FishScreenBounds.GetEdgePoint(
            targetCamera,
            side,
            lane,
            Mathf.Max(
                FishScreenBounds.MinimumViewportPadding,
                profile.cinematicPassOutsideViewportPadding
            ),
            visualRadius + GetViewportWorldWidth() * 0.08f,
            transform.position.z
        );
    }

    private void UpdateTimeoutEscape(float deltaTime)
    {
        if (owner == null || body == null || targetCamera == null)
        {
            if (owner != null)
            {
                owner.ForceResolveBossTimeoutEscape();
            }
            return;
        }

        Vector2 direction = (timeoutEscapeTarget - transform.position).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.right;
        }

        float speed = owner.MoveSpeed *
            Mathf.Max(1f, timeoutEscapeSpeedMultiplier);
        Vector2 targetVelocity = direction * speed;
        body.velocity = Vector2.MoveTowards(
            body.velocity,
            targetVelocity,
            Mathf.Max(0.25f, profile.boundaryReturnSpeedMultiplier * speed) * deltaTime
        );
        SmoothFaceDirection(direction, profile.smoothTurnDegreesPerSecond * 1.25f);

        if (FishScreenBounds.IsFullyOutside(
                targetCamera,
                transform,
                visualRenderers,
                bossColliders,
                Mathf.Max(
                    FishScreenBounds.MinimumViewportPadding,
                    profile.cinematicPassOutsideViewportPadding
                )))
        {
            owner.CompleteBossTimeoutEscape();
            return;
        }

        if (Time.time - timeoutEscapeStartedAt > 18f)
        {
            owner.ForceResolveBossTimeoutEscape();
        }
    }

    public void RequestReturnToArena()
    {
        if (!configured || state == RuntimeState.Dying)
        {
            return;
        }

        if (cinematicPassActive)
        {
            return;
        }

        forceReturnToArena = true;
        roamTarget = GetSafeArenaRecoveryTarget();
        nextTargetChangeTime = Time.time + 1.5f;

        EmergencyRecoverIfFarOutside();
    }

    private IEnumerator IntroductionRoutine()
    {
        state = RuntimeState.Introduction;
        SetAttackBehavioursEnabled(false);

        TrySetAnimatorTrigger(profile.introductionAnimatorTrigger);

        if (profile.requestDirectorIntroductionParade &&
            spawnDirector != null &&
            profile.introductionParadeWaveCount > 0)
        {
            spawnDirector.PlayEpicBossIntroductionParade(
                profile.introductionParadeWaveCount,
                profile.introductionParadeWaveGap
            );
        }

        PlayEffect(
            profile.introductionEffectPrefab,
            transform.position + profile.introductionEffectOffset,
            profile.introductionEffectVisibleDuration,
            profile.introductionEffectHideDelay
        );
        PlayRandomClip(profile.introductionSounds);

        Vector3 target = GetEntranceTarget();
        float duration = Mathf.Max(0.1f, profile.entranceDuration);
        float elapsed = 0f;
        Vector2 entranceSmoothReference = Vector2.zero;

        while (elapsed < duration && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;

            Vector3 delta = target - transform.position;

            if (delta.sqrMagnitude <=
                profile.targetReachDistance * profile.targetReachDistance)
            {
                break;
            }

            Vector2 direction = delta.normalized;
            if (body != null && owner != null)
            {
                Vector2 desiredVelocity = direction *
                    owner.MoveSpeed *
                    Mathf.Max(0.05f, profile.entranceSpeedMultiplier);

                body.velocity = Vector2.SmoothDamp(
                    body.velocity,
                    desiredVelocity,
                    ref entranceSmoothReference,
                    Mathf.Max(0.08f, profile.velocitySmoothTime),
                    Mathf.Infinity,
                    Time.deltaTime
                );

                if (body.velocity.sqrMagnitude > 0.0025f)
                {
                    SmoothFaceDirection(
                        body.velocity.normalized,
                        profile.smoothTurnDegreesPerSecond
                    );
                }
            }

            yield return null;
        }

        if (body != null)
        {
            float settleElapsed = 0f;

            while (settleElapsed < 0.22f &&
                   gameObject.activeInHierarchy)
            {
                settleElapsed += Time.deltaTime;
                body.velocity = Vector2.SmoothDamp(
                    body.velocity,
                    Vector2.zero,
                    ref entranceSmoothReference,
                    0.10f,
                    Mathf.Infinity,
                    Time.deltaTime
                );
                yield return null;
            }

            body.velocity = Vector2.zero;
        }

        if (profile.introductionHoldDuration > 0f)
        {
            yield return new WaitForSeconds(profile.introductionHoldDuration);
        }

        introductionRoutine = null;

        if (!deathStarted)
        {
            EnterRoamingState();
            TryStartPendingPhaseSequence();
        }
    }

    private void EnterRoamingState(bool chooseNewTarget = true)
    {
        state = RuntimeState.Roaming;
        SetAttackBehavioursEnabled(true);

        if (chooseNewTarget ||
            Vector2.Distance(transform.position, roamTarget) < 0.05f)
        {
            ChooseNewRoamTarget(true);
        }

        ScheduleNextCinematicPassOpportunity();

        if (profile.enableRandomSkills && randomSkillRoutine == null)
        {
            randomSkillRoutine = StartCoroutine(RandomSkillSchedulerRoutine());
        }
    }

    private void UpdateMajesticMovement(float deltaTime)
    {
        if (owner == null || body == null || profile == null)
        {
            return;
        }

        if (cinematicPassActive)
        {
            UpdateCinematicPass(deltaTime);
            return;
        }

        if (profile.enableCinematicOffscreenPasses &&
            Time.time >= nextCinematicPassTime)
        {
            bool shouldPass =
                Random.value <= profile.cinematicPassChance;

            ScheduleNextCinematicPassOpportunity();

            if (shouldPass)
            {
                BeginCinematicPass();
                UpdateCinematicPass(deltaTime);
                return;
            }
        }

        if (Time.time < pauseUntil)
        {
            body.velocity = Vector2.SmoothDamp(
                body.velocity,
                Vector2.zero,
                ref velocitySmoothReference,
                Mathf.Max(0.08f, profile.velocitySmoothTime),
                Mathf.Infinity,
                deltaTime
            );
            return;
        }

        Vector3 viewport = GetViewportPosition(transform.position);
        float padding = Mathf.Clamp(profile.viewportBoundaryPadding, 0.02f, 0.35f);

        bool nearBoundary =
            viewport.x <= padding ||
            viewport.x >= 1f - padding ||
            viewport.y <= padding ||
            viewport.y >= 1f - padding;

        if (nearBoundary && !forceReturnToArena)
        {
            forceReturnToArena = true;
            roamTarget = GetSafeArenaRecoveryTarget();
            nextTargetChangeTime = Time.time + 1.5f;
        }

        EmergencyRecoverIfFarOutside();

        float distanceToTarget = Vector2.Distance(
            transform.position,
            roamTarget
        );

        if (distanceToTarget <= profile.targetReachDistance ||
            Time.time >= nextTargetChangeTime)
        {
            pauseUntil = Time.time + RandomRange(
                profile.targetPauseRange,
                0.2f,
                0f
            );
            ChooseNewRoamTarget(false);
        }

        Vector2 desiredDirection =
            (roamTarget - transform.position).normalized;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            desiredDirection = transform.right;
        }

        float turnSpeed = profile.smoothTurnDegreesPerSecond;

        float wander = (
            Mathf.PerlinNoise(
                movementNoiseSeed,
                Time.time * Mathf.Max(0.02f, profile.roamWanderFrequency)
            ) * 2f - 1f
        ) * Mathf.Max(0f, profile.roamWanderAngleDegrees);

        if (forceReturnToArena)
        {
            wander *= 0.20f;
        }

        desiredDirection = RotateVector(desiredDirection, wander);

        float healthSpeedMultiplier = 1f;

        if (currentHealthRatio <= profile.criticalMovementHealthThreshold)
        {
            healthSpeedMultiplier = Mathf.Max(
                1f,
                profile.criticalMovementSpeedMultiplier
            );
            turnSpeed *= Mathf.Max(1f, profile.criticalTurnSpeedMultiplier);
        }
        else if (currentHealthRatio <=
                 profile.aggressiveMovementHealthThreshold)
        {
            healthSpeedMultiplier = Mathf.Max(
                1f,
                profile.aggressiveMovementSpeedMultiplier
            );
        }

        if (forceReturnToArena)
        {
            turnSpeed *= 1.35f;
        }

        speedCycleElapsed += deltaTime;

        if (speedCycleElapsed >= speedCycleDuration)
        {
            speedCycleElapsed = 0f;
            speedCycleDuration = RandomRange(
                profile.speedCycleDurationRange,
                6f,
                0.25f
            );
        }

        float cycleT = speedCycleDuration <= 0f
            ? 0f
            : speedCycleElapsed / speedCycleDuration;

        float wave = (Mathf.Sin(cycleT * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
        wave = wave * wave * (3f - 2f * wave);

        float speedMultiplier = Mathf.Lerp(
            profile.slowSpeedMultiplier,
            profile.fastSpeedMultiplier,
            wave
        );

        if (timeoutWarningActive)
        {
            speedMultiplier = Mathf.Max(speedMultiplier, 1.08f);
        }

        if (forceReturnToArena)
        {
            speedMultiplier = Mathf.Max(
                speedMultiplier,
                profile.boundaryReturnSpeedMultiplier
            );

            if (viewport.x > padding && viewport.x < 1f - padding &&
                viewport.y > padding && viewport.y < 1f - padding)
            {
                forceReturnToArena = false;
            }
        }

        Vector2 desiredVelocity = desiredDirection *
            owner.MoveSpeed *
            Mathf.Max(0.05f, speedMultiplier) *
            healthSpeedMultiplier;

        body.velocity = Vector2.SmoothDamp(
            body.velocity,
            desiredVelocity,
            ref velocitySmoothReference,
            Mathf.Max(0.08f, profile.velocitySmoothTime),
            Mathf.Infinity,
            deltaTime
        );

        if (body.velocity.sqrMagnitude > 0.0025f)
        {
            SmoothFaceDirection(body.velocity.normalized, turnSpeed);
        }

        if (Time.time >= nextStuckCheckTime)
        {
            float travelled = Vector2.Distance(
                transform.position,
                lastStuckPosition
            );

            if (travelled < Mathf.Max(
                    0.01f,
                    profile.minimumStuckTravelDistance
                ))
            {
                forceReturnToArena = true;
                roamTarget = GetSafeArenaRecoveryTarget();
                nextTargetChangeTime = Time.time + 2f;
                body.velocity = desiredDirection *
                    owner.MoveSpeed *
                    Mathf.Max(0.65f, profile.boundaryReturnSpeedMultiplier);
            }

            lastStuckPosition = transform.position;
            nextStuckCheckTime = Time.time +
                Mathf.Max(0.25f, profile.stuckCheckInterval);
        }
    }

    private void ScheduleNextCinematicPassOpportunity()
    {
        if (profile == null)
        {
            nextCinematicPassTime = float.PositiveInfinity;
            return;
        }

        nextCinematicPassTime =
            Time.time +
            RandomRange(
                profile.cinematicPassIntervalRange,
                14f,
                1f
            );
    }

    private void BeginCinematicPass()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 viewport =
            GetViewportPosition(transform.position);

        bool exitRight =
            body != null && Mathf.Abs(body.velocity.x) > 0.05f
                ? body.velocity.x > 0f
                : viewport.x >= 0.5f;

        float padding = Mathf.Clamp(
            profile.cinematicPassOutsideViewportPadding,
            0.02f,
            0.25f
        );

        cinematicPassActive = true;
        cinematicPassStage = 1;
        cinematicExitSide = exitRight ? 1 : -1;
        cinematicPassStartedAt = Time.time;
        cinematicReentryY = Mathf.Clamp(
            viewport.y +
            (Random.value < 0.5f ? -1f : 1f) *
            profile.cinematicReturnArcVerticalShift,
            0.14f,
            0.86f
        );

        cinematicPassTarget = GetViewportWorldPoint(
            exitRight ? 1f + padding : -padding,
            Mathf.Clamp(
                viewport.y + Random.Range(-0.14f, 0.14f),
                0.18f,
                0.82f
            )
        );
    }

    private void UpdateCinematicPass(float deltaTime)
    {
        if (!cinematicPassActive ||
            profile == null ||
            owner == null ||
            body == null)
        {
            cinematicPassActive = false;
            return;
        }

        if (Time.time - cinematicPassStartedAt >=
            Mathf.Max(2f, profile.cinematicPassMaximumDuration))
        {
            cinematicPassStage = 3;
            cinematicPassTarget = GetSafeArenaRecoveryTarget();
        }

        Vector2 desiredDirection =
            (cinematicPassTarget - transform.position).normalized;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            desiredDirection = transform.right;
        }

        float stageSpeed = Mathf.Max(
            0.1f,
            profile.cinematicPassSpeedMultiplier
        );

        if (cinematicPassStage >= 2)
        {
            stageSpeed *= Mathf.Max(
                0.25f,
                profile.cinematicOutsideArcSpeedMultiplier
            );
        }

        Vector2 desiredVelocity = desiredDirection *
            owner.MoveSpeed *
            stageSpeed;

        body.velocity = Vector2.SmoothDamp(
            body.velocity,
            desiredVelocity,
            ref velocitySmoothReference,
            Mathf.Max(0.06f, profile.velocitySmoothTime * 0.72f),
            Mathf.Infinity,
            deltaTime
        );

        if (body.velocity.sqrMagnitude > 0.0025f)
        {
            SmoothFaceDirection(
                body.velocity.normalized,
                profile.smoothTurnDegreesPerSecond * 1.35f
            );
        }

        Vector3 viewport =
            GetViewportPosition(transform.position);

        float targetDistance = Vector2.Distance(
            transform.position,
            cinematicPassTarget
        );
        bool reachedTarget = targetDistance <=
            Mathf.Max(0.12f, profile.targetReachDistance);

        if (cinematicPassStage == 1 &&
            (reachedTarget || viewport.x < -0.02f || viewport.x > 1.02f))
        {
            float padding = Mathf.Clamp(
                profile.cinematicPassOutsideViewportPadding,
                0.02f,
                0.25f
            );
            cinematicPassStage = 2;
            cinematicPassTarget = GetViewportWorldPoint(
                cinematicExitSide > 0
                    ? 1f + padding * 1.85f
                    : -padding * 1.85f,
                cinematicReentryY
            );
            return;
        }

        if (cinematicPassStage == 2 && reachedTarget)
        {
            cinematicPassStage = 3;
            cinematicPassTarget = GetViewportWorldPoint(
                cinematicExitSide > 0 ? 0.70f : 0.30f,
                cinematicReentryY
            );
            return;
        }

        if (cinematicPassStage == 3 &&
            (reachedTarget ||
             (viewport.x > profile.viewportBoundaryPadding &&
              viewport.x < 1f - profile.viewportBoundaryPadding &&
              viewport.y > profile.viewportBoundaryPadding &&
              viewport.y < 1f - profile.viewportBoundaryPadding)))
        {
            cinematicPassActive = false;
            cinematicPassStage = 0;
            forceReturnToArena = false;
            velocitySmoothReference = Vector2.zero;
            ChooseNewRoamTarget(true);
        }
    }

    private void ChooseNewRoamTarget(bool immediate)
    {
        float padding = Mathf.Clamp(profile.viewportBoundaryPadding, 0.02f, 0.35f);
        Vector3 selected = GetSafeArenaRecoveryTarget();
        Vector2 currentForward = transform.right;
        float minimumWorldDistance = GetViewportWorldWidth() *
            Mathf.Clamp(
                profile.minimumTargetViewportDistance,
                0.20f,
                0.85f
            );
        int attempts = 0;

        if (!immediate &&
            profile.avoidCentralArenaPocket &&
            Random.value <= 0.20f)
        {
            preferUpperArena = !preferUpperArena;
        }

        while (attempts < 12)
        {
            attempts++;

            float x = Random.Range(padding, 1f - padding);
            float y;
            bool allowCenterCandidate =
                profile.avoidCentralArenaPocket &&
                Random.value <= Mathf.Clamp01(profile.centerTargetChance);

            if (profile.avoidCentralArenaPocket &&
                !allowCenterCandidate)
            {
                float halfHeight = Mathf.Clamp(
                    profile.centerAvoidanceHalfHeight,
                    0.06f,
                    0.30f
                );
                float minimumY = preferUpperArena
                    ? Mathf.Max(padding, 0.5f + halfHeight)
                    : padding;
                float maximumY = preferUpperArena
                    ? 1f - padding
                    : Mathf.Min(1f - padding, 0.5f - halfHeight);
                y = minimumY < maximumY
                    ? Random.Range(minimumY, maximumY)
                    : preferUpperArena
                        ? 1f - padding
                        : padding;
            }
            else
            {
                y = Random.Range(padding, 1f - padding);
            }

            if (!allowCenterCandidate &&
                IsInsideCentralArenaPocket(x, y) &&
                attempts < 12)
            {
                continue;
            }

            Vector3 candidate = GetViewportWorldPoint(x, y);
            Vector2 direction = candidate - transform.position;

            if (direction.sqrMagnitude <= 0.01f)
            {
                continue;
            }

            if (direction.magnitude < minimumWorldDistance &&
                attempts < 12)
            {
                continue;
            }

            float angle = Vector2.Angle(currentForward, direction.normalized);
            bool requireLargeTurn = Random.value <= profile.largeTurnChance;

            selected = candidate;

            if (!requireLargeTurn || angle >= profile.minimumLargeTurnAngle)
            {
                break;
            }
        }

        roamTarget = selected;
        float targetDelay = immediate
            ? 0.5f
            : RandomRange(profile.targetChangeDelayRange, 4f, 0.1f);

        if (!immediate &&
            currentHealthRatio <= profile.criticalMovementHealthThreshold)
        {
            targetDelay *= 0.72f;
        }
        else if (!immediate &&
                 currentHealthRatio <=
                 profile.aggressiveMovementHealthThreshold)
        {
            targetDelay *= 0.86f;
        }

        nextTargetChangeTime = Time.time + targetDelay;
    }

    private void TryStartPendingPhaseSequence()
    {
        if (!configured || deathStarted || profile == null)
        {
            return;
        }

        if (state != RuntimeState.Roaming || phaseRoutine != null)
        {
            return;
        }

        if (!pendingFirstConvulsion &&
            !pendingFakeDeath &&
            !pendingStrongConvulsion &&
            !pendingReactiveConvulsion)
        {
            return;
        }

        phaseRoutine = StartCoroutine(ProcessPendingPhasesRoutine());
    }

    private IEnumerator ProcessPendingPhasesRoutine()
    {
        state = RuntimeState.PhaseReaction;
        SetAttackBehavioursEnabled(false);

        if (pendingFirstConvulsion && !deathStarted)
        {
            pendingFirstConvulsion = false;
            yield return RunConvulsionRoutine(
                profile.firstConvulsionAnimatorTrigger,
                profile.firstConvulsionDuration,
                profile.firstConvulsionPositionStrength,
                profile.firstConvulsionRotationStrength,
                profile.firstConvulsionEffectPrefab,
                profile.firstConvulsionSounds,
                true
            );
        }

        if (pendingFakeDeath && !deathStarted)
        {
            pendingFakeDeath = false;
            yield return RunFakeDeathRewardRoutine();
        }

        if (pendingStrongConvulsion && !deathStarted)
        {
            pendingStrongConvulsion = false;
            yield return RunConvulsionRoutine(
                profile.strongConvulsionAnimatorTrigger,
                profile.strongConvulsionDuration,
                profile.strongConvulsionPositionStrength,
                profile.strongConvulsionRotationStrength,
                profile.strongConvulsionEffectPrefab,
                profile.strongConvulsionSounds,
                true
            );
        }

        while (pendingReactiveConvulsion &&
               !deathStarted &&
               IsFirePressureActive())
        {
            pendingReactiveConvulsion = false;
            yield return RunReactiveMicroConvulsionRoutine();

            bool sustained = incomingHitChain >= Mathf.Max(
                1,
                profile.sustainedFireHitCount
            );

            if (sustained && IsFirePressureActive() &&
                Random.value <= profile.repeatMicroConvulsionChance)
            {
                float recovery = RandomRange(
                    profile.microConvulsionRecoveryRange,
                    0.12f,
                    0.02f
                );
                float recoveryElapsed = 0f;

                while (recoveryElapsed < recovery &&
                       IsFirePressureActive() &&
                       !deathStarted)
                {
                    recoveryElapsed += Time.deltaTime;
                    yield return null;
                }

                pendingReactiveConvulsion = IsFirePressureActive();
            }
        }

        phaseRoutine = null;

        if (!deathStarted)
        {
            RestoreVisualDefaults();
            TrySetAnimatorBool(profile.sustainedFireAnimatorBool, false);
            EnterRoamingState(false);
        }
    }

    private IEnumerator RunConvulsionRoutine(
        string animatorTrigger,
        float duration,
        float positionStrength,
        float rotationStrength,
        GameObject effectPrefab,
        AudioClip[] sounds,
        bool stopWhenFireStops
    )
    {
        TrySetAnimatorTrigger(animatorTrigger);
        PlayEffect(effectPrefab, transform.position, duration, 0.15f);
        PlayRandomClip(sounds);

        float safeDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        Vector3 startLocalPosition = visualRoot.localPosition;
        Quaternion startLocalRotation = visualRoot.localRotation;
        Vector3 startLocalScale = visualRoot.localScale;
        Vector2 startingVelocity = body != null
            ? body.velocity
            : Vector2.zero;

        while (elapsed < safeDuration && !deathStarted)
        {
            if (stopWhenFireStops && !IsFirePressureActive())
            {
                break;
            }

            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / safeDuration);
            Vector2 jitter = Random.insideUnitCircle * positionStrength * fade;
            float angle = Mathf.Sin(elapsed * 42f) * rotationStrength * fade;

            if (visualRoot != transform)
            {
                visualRoot.localPosition = startLocalPosition +
                    new Vector3(jitter.x, jitter.y, 0f);
                visualRoot.localRotation = startLocalRotation *
                    Quaternion.Euler(0f, 0f, angle);
            }

            float squash = Mathf.Sin(elapsed * 46f) * 0.045f * fade;
            visualRoot.localScale = Vector3.Scale(
                startLocalScale,
                new Vector3(1f + squash, 1f - squash * 0.72f, 1f)
            );

            if (body != null)
            {
                body.velocity = startingVelocity *
                    Mathf.Clamp(
                        profile.reactiveMovementSpeedMultiplier,
                        0.15f,
                        1f
                    );
            }

            yield return null;
        }

        visualRoot.localPosition = startLocalPosition;
        visualRoot.localRotation = startLocalRotation;
        visualRoot.localScale = startLocalScale;

        if (body != null)
        {
            body.velocity = startingVelocity;
        }

        if (!stopWhenFireStops)
        {
            yield return new WaitForSeconds(0.12f);
        }
    }

    private IEnumerator RunReactiveMicroConvulsionRoutine()
    {
        if (visualRoot == null || profile == null)
        {
            yield break;
        }

        reactiveConvulsionRunning = true;
        TrySetAnimatorTrigger(profile.reactiveConvulsionAnimatorTrigger);
        TrySetAnimatorBool(profile.sustainedFireAnimatorBool, true);

        float duration = RandomRange(
            profile.microConvulsionDurationRange,
            0.10f,
            0.02f
        );
        float elapsed = 0f;
        Vector3 startPosition = visualRoot.localPosition;
        Quaternion startRotation = visualRoot.localRotation;
        Vector3 startScale = visualRoot.localScale;
        Vector2 startingVelocity = body != null
            ? body.velocity
            : Vector2.zero;

        while (elapsed < duration &&
               !deathStarted &&
               IsFirePressureActive())
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            float oscillation = Mathf.Sin(normalized * Mathf.PI * 5f);
            Vector2 jitter = Random.insideUnitCircle *
                profile.microConvulsionPositionStrength *
                envelope;
            float angle = oscillation *
                profile.microConvulsionRotationStrength *
                envelope;
            float scale = oscillation *
                profile.microConvulsionScaleStrength *
                envelope;

            if (visualRoot != transform)
            {
                visualRoot.localPosition = startPosition +
                    new Vector3(jitter.x, jitter.y, 0f);
                visualRoot.localRotation = startRotation *
                    Quaternion.Euler(0f, 0f, angle);
            }

            visualRoot.localScale = Vector3.Scale(
                startScale,
                new Vector3(1f + scale, 1f - scale * 0.70f, 1f)
            );

            if (body != null)
            {
                body.velocity = startingVelocity *
                    Mathf.Clamp(
                        profile.reactiveMovementSpeedMultiplier,
                        0.15f,
                        1f
                    );
            }

            yield return null;
        }

        visualRoot.localPosition = startPosition;
        visualRoot.localRotation = startRotation;
        visualRoot.localScale = startScale;

        if (body != null)
        {
            body.velocity = startingVelocity;
        }

        reactiveConvulsionRunning = false;
    }

    private bool IsFirePressureActive()
    {
        return profile != null &&
               Time.time - lastIncomingHitTime <=
               Mathf.Max(0.05f, profile.fireReleaseGracePeriod);
    }

    private IEnumerator RunFakeDeathRewardRoutine()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
            velocitySmoothReference = Vector2.zero;
        }

        TrySetAnimatorTrigger(profile.fakeDeathAnimatorTrigger);
        PlayEffect(
            profile.fakeDeathEffectPrefab,
            transform.position,
            profile.fakeDeathDuration,
            0.2f
        );
        PlayRandomClip(profile.fakeDeathSounds);

        float fakeReward = CalculateFakeRewardAmount();
        bool collectible = Random.value <= profile.fakeRewardCollectibleChance;

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager != null)
        {
            if (profile.showFakeRewardText &&
                gameManager.DisplayTextManagerScript != null)
            {
                string suffix =
                    !collectible && profile.revealVisualOnlyRewardWithQuestionMark
                        ? "?"
                        : string.Empty;

                gameManager.DisplayTextManagerScript.DisplayBigText(
                    "+" + fakeReward.ToString("0.##") + suffix,
                    transform.position
                );
            }

            if (gameManager.coinManager != null)
            {
                gameManager.coinManager.PlayCoinAnimations(
                    transform.position,
                    fakeReward,
                    lastShooterId,
                    Mathf.Clamp(profile.fakeRewardCoinAnimationCount, 1, 6),
                    Mathf.Max(0f, profile.fakeRewardCoinAnimationInterval),
                    Mathf.Max(0f, profile.fakeRewardCoinAnimationSpread)
                );
            }

            if (gameManager.animatiorManager != null)
            {
                if (profile.fakeRewardPlayBossCoinBurst)
                {
                    gameManager.animatiorManager.PlayMainBossCoinBurst(
                        transform.position,
                        profile.fakeRewardBossCoinBurstEffectIndex
                    );
                }

                if (profile.fakeRewardPlayNetBoom)
                {
                    gameManager.animatiorManager.PlayNetBoom(
                        transform.position,
                        lastShooterId,
                        owner != null ? owner.id : 0,
                        profile.fakeRewardNetBoomIndex
                    );
                }
            }

            if (collectible && fakeReward > 0f)
            {
                if (lastShooterId == 0)
                {
                    gameManager.CalulateTotalCoinWithCoinFish(fakeReward);
                }
                else
                {
                    gameManager.CalulateNPCCoinFish(
                        fakeReward,
                        lastShooterId
                    );
                }
            }
        }

        float wait = Mathf.Max(0.05f, profile.fakeDeathDuration);
        yield return new WaitForSeconds(wait);
    }

    private float CalculateFakeRewardAmount()
    {
        if (owner == null || profile == null)
        {
            return 0f;
        }

        float amount;

        if (profile.fakeRewardMode == EpicBossFakeRewardMode.FixedAmount)
        {
            amount = profile.fakeRewardFixedAmount;
        }
        else
        {
            amount = owner.CoinFish *
                Mathf.Clamp01(profile.fakeRewardPercentOfFinalReward);
        }

        if (profile.fakeRewardMaximum > 0f)
        {
            amount = Mathf.Min(amount, profile.fakeRewardMaximum);
        }

        return Mathf.Round(Mathf.Max(0f, amount) * 100f) / 100f;
    }

    private IEnumerator RandomSkillSchedulerRoutine()
    {
        yield return new WaitForSeconds(
            RandomRange(profile.firstSkillDelayRange, 5f, 0.1f)
        );

        while (configured && !deathStarted && gameObject.activeInHierarchy)
        {
            if (state == RuntimeState.Roaming)
            {
                int selectedIndex = SelectRandomSkillIndex();

                if (selectedIndex >= 0)
                {
                    yield return ExecuteSkillRoutine(
                        profile.randomSkills[selectedIndex]
                    );
                    lastSkillIndex = selectedIndex;
                    TryStartPendingPhaseSequence();
                }
            }

            yield return new WaitForSeconds(
                RandomRange(profile.skillIntervalRange, 7f, 0.1f)
            );
        }

        randomSkillRoutine = null;
    }

    private int SelectRandomSkillIndex()
    {
        EpicBossSkillEntry[] skills = profile.randomSkills;

        if (skills == null || skills.Length == 0)
        {
            return -1;
        }

        float totalWeight = 0f;
        int validCount = 0;

        for (int i = 0; i < skills.Length; i++)
        {
            EpicBossSkillEntry skill = skills[i];

            if (skill == null || !skill.enabled || skill.weight <= 0f)
            {
                continue;
            }

            if (profile.preventImmediateSkillRepeat &&
                skills.Length > 1 &&
                i == lastSkillIndex)
            {
                continue;
            }

            totalWeight += skill.weight;
            validCount++;
        }

        if (validCount == 0 || totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.value * totalWeight;

        for (int i = 0; i < skills.Length; i++)
        {
            EpicBossSkillEntry skill = skills[i];

            if (skill == null || !skill.enabled || skill.weight <= 0f)
            {
                continue;
            }

            if (profile.preventImmediateSkillRepeat &&
                skills.Length > 1 &&
                i == lastSkillIndex)
            {
                continue;
            }

            roll -= skill.weight;

            if (roll <= 0f)
            {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator ExecuteSkillRoutine(EpicBossSkillEntry skill)
    {
        if (skill == null || deathStarted)
        {
            yield break;
        }

        state = RuntimeState.Skill;
        SetAttackBehavioursEnabled(false);
        TrySetAnimatorTrigger(skill.animatorTrigger);
        PlayEffect(
            skill.effectPrefab,
            transform.position + skill.effectOffset,
            skill.effectVisibleDuration,
            skill.effectHideDelay
        );
        PlayRandomClip(skill.soundClips);

        if (body != null)
        {
            body.velocity = Vector2.zero;
        }

        if (skill.windUpDuration > 0f)
        {
            yield return new WaitForSeconds(skill.windUpDuration);
        }

        if (skill.cameraShakeStrength > 0f &&
            skill.cameraShakeDuration > 0f)
        {
            StartCameraShake(
                skill.cameraShakeDuration,
                skill.cameraShakeStrength
            );
        }

        switch (skill.skillType)
        {
            case EpicBossSkillType.AccelerationBurst:
                yield return RunChargeRoutine(skill, false);
                break;

            case EpicBossSkillType.AggressiveCharge:
                yield return RunChargeRoutine(skill, true);
                break;

            case EpicBossSkillType.HeavyRoar:
            case EpicBossSkillType.WaterShockwave:
            case EpicBossSkillType.Earthquake:
            case EpicBossSkillType.DramaticCameraShake:
                yield return new WaitForSeconds(
                    Mathf.Max(0.02f, skill.activeDuration)
                );
                break;
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
        }

        if (skill.recoveryDuration > 0f)
        {
            yield return new WaitForSeconds(skill.recoveryDuration);
        }

        if (!deathStarted)
        {
            EnterRoamingState();
        }
    }

    private IEnumerator RunChargeRoutine(
        EpicBossSkillEntry skill,
        bool aggressive
    )
    {
        float padding = Mathf.Clamp(profile.viewportBoundaryPadding, 0.08f, 0.30f);
        Vector3 currentViewport = GetViewportPosition(transform.position);
        float targetX = currentViewport.x < 0.5f
            ? 1f - padding
            : padding;
        float targetY = aggressive
            ? Random.Range(padding, 1f - padding)
            : Mathf.Clamp(currentViewport.y + Random.Range(-0.22f, 0.22f), padding, 1f - padding);

        Vector3 target = GetViewportWorldPoint(targetX, targetY);
        Vector2 direction = (target - transform.position).normalized;
        float turnTime = aggressive ? 0.22f : 0.38f;
        float turnElapsed = 0f;

        while (turnElapsed < turnTime && !deathStarted)
        {
            turnElapsed += Time.deltaTime;
            SmoothFaceDirection(
                direction,
                profile.smoothTurnDegreesPerSecond * 3f
            );
            yield return null;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.02f, skill.activeDuration);
        float speed = owner != null
            ? owner.MoveSpeed * Mathf.Max(0.1f, skill.movementSpeedMultiplier)
            : Mathf.Max(0.1f, skill.movementSpeedMultiplier);
        Vector2 chargeSmoothReference = Vector2.zero;

        while (elapsed < duration && !deathStarted)
        {
            elapsed += Time.deltaTime;

            if (body != null)
            {
                body.velocity = Vector2.SmoothDamp(
                    body.velocity,
                    direction * speed,
                    ref chargeSmoothReference,
                    0.08f,
                    Mathf.Infinity,
                    Time.deltaTime
                );

                if (body.velocity.sqrMagnitude > 0.0025f)
                {
                    SmoothFaceDirection(
                        body.velocity.normalized,
                        profile.smoothTurnDegreesPerSecond * 2f
                    );
                }
            }

            Vector3 viewport = GetViewportPosition(transform.position);

            if (viewport.x <= padding || viewport.x >= 1f - padding ||
                viewport.y <= padding || viewport.y >= 1f - padding)
            {
                break;
            }

            yield return null;
        }

        forceReturnToArena = true;
        roamTarget = GetSafeArenaRecoveryTarget();
    }

    private IEnumerator DeathSequenceRoutine()
    {
        TrySetAnimatorTrigger(profile.deathAnimatorTrigger);
        PlayEffect(
            profile.deathEffectPrefab,
            transform.position + profile.deathEffectOffset,
            profile.deathEffectVisibleDuration,
            profile.deathEffectHideDelay
        );
        PlayRandomClip(profile.deathSounds);

        if (profile.deathCameraShakeStrength > 0f &&
            profile.deathCameraShakeDuration > 0f)
        {
            StartCameraShake(
                profile.deathCameraShakeDuration,
                profile.deathCameraShakeStrength
            );
        }

        float duration = Mathf.Max(0.05f, profile.deathAnimationDuration);
        float elapsed = 0f;
        Vector3 startLocalPosition = visualRoot != null
            ? visualRoot.localPosition
            : Vector3.zero;
        Quaternion startLocalRotation = visualRoot != null
            ? visualRoot.localRotation
            : Quaternion.identity;
        Vector3 startLocalScale = visualRoot != null
            ? visualRoot.localScale
            : Vector3.one;
        float tumbleSign = Random.value < 0.5f ? -1f : 1f;
        Vector3 driftDirection = (
            Vector3.up * 0.78f +
            transform.right * Random.Range(-0.28f, 0.28f)
        ).normalized;
        int pulseTarget = Mathf.Clamp(
            profile.deathEffectPulseCount,
            1,
            5
        );
        int pulsesPlayed = profile.deathEffectPrefab != null ? 1 : 0;
        float nextPulseTime = Mathf.Max(
            0.05f,
            profile.deathEffectPulseInterval
        );
        bool finalShakePlayed = false;

        while (elapsed < duration &&
               gameObject.activeInHierarchy &&
               deathStarted)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float smooth = normalized * normalized *
                (3f - 2f * normalized);
            float impactProgress = Mathf.Clamp01(normalized / 0.20f);
            float impactEnvelope = Mathf.Sin(impactProgress * Mathf.PI);
            float fadeStart = Mathf.Clamp01(
                profile.deathFadeStartNormalized
            );
            float alpha = normalized <= fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(
                    fadeStart,
                    1f,
                    normalized
                );
            float scale =
                (1f +
                 (Mathf.Max(1f, profile.deathImpactScaleMultiplier) - 1f) *
                 impactEnvelope) *
                Mathf.Lerp(1f, 0.62f, 1f - alpha);

            if (visualRoot != null)
            {
                visualRoot.localScale = startLocalScale * scale;
                visualRoot.localPosition = startLocalPosition +
                    driftDirection *
                    Mathf.Max(0f, profile.deathDriftDistance) *
                    smooth +
                    Vector3.up *
                    Mathf.Sin(normalized * Mathf.PI) *
                    0.08f;
                visualRoot.localRotation = startLocalRotation *
                    Quaternion.Euler(
                        0f,
                        0f,
                        tumbleSign *
                        Mathf.Max(0f, profile.deathTumbleDegrees) *
                        smooth
                    );
            }

            SetVisualAlpha(alpha);

            if (profile.deathEffectPrefab != null &&
                pulsesPlayed < pulseTarget &&
                elapsed >= nextPulseTime)
            {
                PlayEffect(
                    profile.deathEffectPrefab,
                    transform.position +
                    (Vector3)Random.insideUnitCircle * 0.30f +
                    profile.deathEffectOffset,
                    profile.deathEffectVisibleDuration,
                    profile.deathEffectHideDelay
                );
                pulsesPlayed++;
                nextPulseTime += Mathf.Max(
                    0.05f,
                    profile.deathEffectPulseInterval
                );
            }

            if (!finalShakePlayed && normalized >= fadeStart)
            {
                finalShakePlayed = true;
                float finalStrength =
                    profile.deathCameraShakeStrength *
                    Mathf.Clamp01(profile.finalDeathShakeMultiplier);

                if (finalStrength > 0f)
                {
                    StartCameraShake(
                        Mathf.Max(
                            0.10f,
                            profile.deathCameraShakeDuration * 0.55f
                        ),
                        finalStrength
                    );
                }
            }

            yield return null;
        }

        deathRoutine = null;

        if (owner != null && gameObject.activeInHierarchy)
        {
            owner.CompleteEpicBossDeath(
                lastShooterId,
                lastGunLevel
            );
        }
    }

    private void PlayEffect(
        GameObject prefab,
        Vector3 position,
        float visibleDuration,
        float hideDelay
    )
    {
        if (prefab == null)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (gameManager != null && gameManager.animatiorManager != null)
        {
            gameManager.animatiorManager.PlayPooledEffect(
                prefab,
                position,
                visibleDuration,
                hideDelay
            );
        }
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        if (bossAudioSource == null)
        {
            bossAudioSource = GetComponent<AudioSource>();

            if (bossAudioSource == null)
            {
                bossAudioSource = gameObject.AddComponent<AudioSource>();
                bossAudioSource.playOnAwake = false;
                bossAudioSource.spatialBlend = 0f;
            }
        }

        int validCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return;
        }

        int selectedValid = Random.Range(0, validCount);
        AudioClip selected = null;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (selectedValid == 0)
            {
                selected = clips[i];
                break;
            }

            selectedValid--;
        }

        if (validCount > 1 && selected == lastPlayedClip)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i] != lastPlayedClip)
                {
                    selected = clips[i];
                    break;
                }
            }
        }

        if (selected != null)
        {
            lastPlayedClip = selected;
            bossAudioSource.PlayOneShot(selected);
        }
    }

    private void StartCameraShake(float duration, float strength)
    {
        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
            RestoreCameraAfterShake();
        }

        cameraShakeRoutine = StartCoroutine(
            CameraShakeRoutine(duration, strength)
        );
    }

    private IEnumerator CameraShakeRoutine(float duration, float strength)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            cameraShakeRoutine = null;
            yield break;
        }

        shakingCameraTransform = targetCamera.transform;
        cameraShakeBaseLocalPosition = shakingCameraTransform.localPosition;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.02f, duration);

        while (elapsed < safeDuration && shakingCameraTransform != null)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / safeDuration);
            Vector2 offset = Random.insideUnitCircle * strength * fade;

            shakingCameraTransform.localPosition =
                cameraShakeBaseLocalPosition +
                new Vector3(offset.x, offset.y, 0f);

            yield return null;
        }

        RestoreCameraAfterShake();
        cameraShakeRoutine = null;
    }

    private void RestoreCameraAfterShake()
    {
        if (shakingCameraTransform != null)
        {
            shakingCameraTransform.localPosition = cameraShakeBaseLocalPosition;
        }

        shakingCameraTransform = null;
    }

    private void TrySetAnimatorTrigger(string triggerName)
    {
        if (bossAnimator == null || !bossAnimator.isActiveAndEnabled ||
            string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = bossAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                parameters[i].name == triggerName)
            {
                bossAnimator.ResetTrigger(triggerName);
                bossAnimator.SetTrigger(triggerName);
                return;
            }
        }
    }

    private void TrySetAnimatorBool(string parameterName, bool value)
    {
        if (bossAnimator == null || !bossAnimator.isActiveAndEnabled ||
            string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = bossAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                parameters[i].name == parameterName)
            {
                bossAnimator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void SmoothFaceDirection(Vector2 direction, float degreesPerSecond)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float currentAngle = body != null
            ? body.rotation
            : transform.eulerAngles.z;
        float nextAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            Mathf.Max(1f, degreesPerSecond) * Time.deltaTime
        );

        if (body != null)
        {
            body.MoveRotation(nextAngle);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
        }
    }

    private Vector3 GetEntranceTarget()
    {
        Vector3 viewport = GetViewportPosition(transform.position);
        bool enteringFromLeft = viewport.x <= 0.5f;
        float horizontal = Mathf.Clamp(
            profile.entranceTargetHorizontalViewport,
            0.1f,
            0.45f
        );
        float x = enteringFromLeft ? horizontal : 1f - horizontal;
        float y = Mathf.Clamp(
            profile.entranceTargetVerticalViewport,
            0.15f,
            0.85f
        );

        return GetViewportWorldPoint(x, y);
    }

    private Vector3 GetViewportPosition(Vector3 worldPosition)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return new Vector3(0.5f, 0.5f, 0f);
        }

        return targetCamera.WorldToViewportPoint(worldPosition);
    }

    private Vector3 GetViewportWorldPoint(float x, float y)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return new Vector3(0f, 0f, transform.position.z);
        }

        float depth = Mathf.Abs(
            transform.position.z - targetCamera.transform.position.z
        );

        Vector3 world = targetCamera.ViewportToWorldPoint(
            new Vector3(x, y, depth)
        );
        world.z = transform.position.z;
        return world;
    }

    private bool IsInsideCentralArenaPocket(float x, float y)
    {
        if (profile == null || !profile.avoidCentralArenaPocket)
        {
            return false;
        }

        return Mathf.Abs(x - 0.5f) < Mathf.Clamp(
                   profile.centerAvoidanceHalfWidth,
                   0.06f,
                   0.30f
               ) &&
               Mathf.Abs(y - 0.5f) < Mathf.Clamp(
                   profile.centerAvoidanceHalfHeight,
                   0.06f,
                   0.30f
               );
    }

    private Vector3 GetSafeArenaRecoveryTarget()
    {
        Vector3 viewport = GetViewportPosition(transform.position);
        float padding = profile != null
            ? Mathf.Clamp(
                profile.viewportBoundaryPadding,
                0.08f,
                0.30f
            )
            : 0.15f;
        float halfHeight = profile != null
            ? Mathf.Clamp(
                profile.centerAvoidanceHalfHeight,
                0.06f,
                0.30f
            )
            : 0.18f;
        float x = viewport.x <= 0.5f ? 0.28f : 0.72f;
        float y = preferUpperArena
            ? 0.5f + halfHeight + 0.08f
            : 0.5f - halfHeight - 0.08f;

        x = Mathf.Clamp(x, padding, 1f - padding);
        y = Mathf.Clamp(y, padding, 1f - padding);
        return GetViewportWorldPoint(x, y);
    }

    private float GetViewportWorldWidth()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || !targetCamera.orthographic)
        {
            return 16f;
        }

        return targetCamera.orthographicSize *
            2f * targetCamera.aspect;
    }

    private static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cosine - vector.y * sine,
            vector.x * sine + vector.y * cosine
        );
    }

    private void EmergencyRecoverIfFarOutside()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(transform.position);
        float tolerance = Mathf.Clamp(
            profile.emergencyOutsideTolerance,
            0.01f,
            0.20f
        );

        bool farOutside =
            viewport.x < -tolerance || viewport.x > 1f + tolerance ||
            viewport.y < -tolerance || viewport.y > 1f + tolerance;

        if (!farOutside)
        {
            return;
        }

        // Never teleport an active boss. Give it an aggressive but smooth
        // off-center return vector so recovery does not create a predictable
        // splash-damage stack in the middle of the viewport.
        roamTarget = GetSafeArenaRecoveryTarget();
        forceReturnToArena = true;

        if (body != null && owner != null)
        {
            Vector2 returnDirection =
                (roamTarget - transform.position).normalized;
            body.velocity = Vector2.Lerp(
                body.velocity,
                returnDirection * owner.MoveSpeed *
                Mathf.Max(1f, profile.boundaryReturnSpeedMultiplier),
                0.35f
            );
        }
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bossAnimator == null)
        {
            bossAnimator = GetComponent<Animator>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (bossAudioSource == null)
        {
            bossAudioSource = GetComponent<AudioSource>();
        }

        if (bossColliders == null || bossColliders.Length == 0)
        {
            bossColliders = GetComponentsInChildren<Collider2D>(true);
        }

        targetCamera = Camera.main;
        gameManager = GameManager.Instance;
    }

    private void CaptureDefaults()
    {
        if (visualRoot != null && !visualDefaultsCaptured)
        {
            visualDefaultLocalPosition = visualRoot.localPosition;
            visualDefaultLocalRotation = visualRoot.localRotation;
            visualDefaultLocalScale = visualRoot.localScale;
            visualRenderers = visualRoot.GetComponentsInChildren<
                SpriteRenderer
            >(true);
            visualDefaultColors = new Color[visualRenderers.Length];

            for (int i = 0; i < visualRenderers.Length; i++)
            {
                visualDefaultColors[i] = visualRenderers[i] != null
                    ? visualRenderers[i].color
                    : Color.white;
            }

            visualDefaultsCaptured = true;
        }

        if (attackBehaviours != null &&
            (attackDefaultEnabled == null ||
             attackDefaultEnabled.Length != attackBehaviours.Length))
        {
            attackDefaultEnabled = new bool[attackBehaviours.Length];

            for (int i = 0; i < attackBehaviours.Length; i++)
            {
                attackDefaultEnabled[i] =
                    attackBehaviours[i] != null &&
                    attackBehaviours[i].enabled;
            }
        }

        if (bossColliders != null &&
            (colliderDefaultEnabled == null ||
             colliderDefaultEnabled.Length != bossColliders.Length))
        {
            colliderDefaultEnabled = new bool[bossColliders.Length];

            for (int i = 0; i < bossColliders.Length; i++)
            {
                colliderDefaultEnabled[i] =
                    bossColliders[i] != null &&
                    bossColliders[i].enabled;
            }
        }
    }

    private void RestoreVisualDefaults()
    {
        if (!visualDefaultsCaptured || visualRoot == null)
        {
            return;
        }

        // The one-click setup safely falls back to the prefab root when no
        // dedicated child VisualRoot exists. Never restore position/rotation
        // on that Rigidbody root during a live encounter: doing so would snap
        // a moving boss back to its original spawn point when fire stops.
        // Root pose is reset by SwapFishScript before the pooled fish is
        // re-enabled. A dedicated child visual can restore its authored pose.
        if (visualRoot != transform)
        {
            visualRoot.localPosition = visualDefaultLocalPosition;
            visualRoot.localRotation = visualDefaultLocalRotation;
        }

        visualRoot.localScale = visualDefaultLocalScale;
        SetVisualAlpha(1f);
    }

    private void SetVisualAlpha(float alpha)
    {
        if (visualRenderers == null || visualDefaultColors == null)
        {
            return;
        }

        int count = Mathf.Min(
            visualRenderers.Length,
            visualDefaultColors.Length
        );

        for (int i = 0; i < count; i++)
        {
            if (visualRenderers[i] == null)
            {
                continue;
            }

            Color color = visualDefaultColors[i];
            color.a *= Mathf.Clamp01(alpha);
            visualRenderers[i].color = color;
        }
    }

    private void SetAttackBehavioursEnabled(bool enabledValue)
    {
        if (attackBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < attackBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = attackBehaviours[i];

            if (behaviour == null ||
                behaviour == this ||
                behaviour == owner)
            {
                continue;
            }

            bool shouldEnable = enabledValue;

            if (enabledValue &&
                attackDefaultEnabled != null &&
                i < attackDefaultEnabled.Length)
            {
                shouldEnable = attackDefaultEnabled[i];
            }

            behaviour.enabled = shouldEnable;
        }
    }

    private void SetCollidersEnabled(bool enabledValue)
    {
        if (bossColliders == null)
        {
            return;
        }

        for (int i = 0; i < bossColliders.Length; i++)
        {
            if (bossColliders[i] != null)
            {
                bossColliders[i].enabled = enabledValue;
            }
        }
    }

    private void RestoreColliderDefaults()
    {
        if (bossColliders == null)
        {
            return;
        }

        for (int i = 0; i < bossColliders.Length; i++)
        {
            if (bossColliders[i] == null)
            {
                continue;
            }

            bool enabledValue = true;

            if (colliderDefaultEnabled != null &&
                i < colliderDefaultEnabled.Length)
            {
                enabledValue = colliderDefaultEnabled[i];
            }

            bossColliders[i].enabled = enabledValue;
        }
    }

    private void StopAllRuntimeCoroutines()
    {
        StopIntroductionRoutine();
        StopPhaseRoutine();
        StopRandomSkillRoutine();

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }

    private void StopIntroductionRoutine()
    {
        if (introductionRoutine != null)
        {
            StopCoroutine(introductionRoutine);
            introductionRoutine = null;
        }
    }

    private void StopPhaseRoutine()
    {
        if (phaseRoutine != null)
        {
            StopCoroutine(phaseRoutine);
            phaseRoutine = null;
        }
    }

    private void StopRandomSkillRoutine()
    {
        if (randomSkillRoutine != null)
        {
            StopCoroutine(randomSkillRoutine);
            randomSkillRoutine = null;
        }
    }

    private static float RandomRange(
        Vector2 range,
        float fallback,
        float minimum
    )
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));

        if (max <= 0f)
        {
            return Mathf.Max(minimum, fallback);
        }

        return Random.Range(min, max);
    }
}
