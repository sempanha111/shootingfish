using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class FishScript : MonoBehaviour
{
    public enum SwimStyle
    {
        LaneGlide, ArcSweep, ZigZagBurst, SchoolFollow, SpiralCross, VerticalDive, HorizontalRush,
        MiniBossHunter, MiniBossCharge, MiniBossOrbit, BossPatrol, BossCharge, BossOrbit,
        BossFigureEight, CriticalStagger, FastTideExit,
        // Legacy serialized values retained for scene compatibility; Wave is no longer selected.
        Straight, Wave, Curious, OrbitCenter, FollowLeader, FigureEight, BossDash, CuteDarting,
        BossArena, BossCuteDarting, BossWoundedConvulsion
    }

    public enum EscortRole
    {
        Orbit,
        Flank,
        Shield
    }

    public enum GimmickType
    {
        None,
        BombCrab,
        LightningChain
    }

    public enum ParadeParticipationMode
    {
        AutoByTier,
        AlwaysAllow,
        AlwaysBlock
    }

    public enum NaturalSpawnMode
    {
        Solo,
        Pair,
        SmallSchool,
        LargeSchool,
        EventOnly
    }

    public enum MainBossPresenceMode
    {
        StayUntilDefeated,
        TimedRetreat,
        RandomPerSpawn
    }

    private enum SmartLoopStage
    {
        None,
        Exiting,
        OutsideArc,
        Returning
    }

    private struct OrganicSwimTuning
    {
        public float speedMultiplier;
        public float turnMultiplier;
        public float smoothTimeMultiplier;
        public float wanderMultiplier;
        public float waypointDistanceMultiplier;
        public float waypointLifetimeMultiplier;
        public float loopChanceMultiplier;
        public bool favorVerticalTravel;
        public bool keepInsideArena;
    }

    [Header("Fish Classification and Profiles")]
    [SerializeField] private FishTier fishTier = FishTier.Small;
    [SerializeField] private bool useArrayIndexTierFallback = true;
    [SerializeField] private FishDeathProfile deathProfile;
    [SerializeField] private FishMovementProfile movementProfile;
    [SerializeField] private FishGameplayProfile gameplayProfile;
    [SerializeField] private bool useProfessionalMotionAgent = true;
    [SerializeField] private FishMotionAgent professionalMotionAgent;

    [Header("Professional Motion Speed Compatibility")]
    [Tooltip(
        "Scales gameplay-profile movement speeds so the professional motion " +
        "system matches the slower speed range used by the original game. " +
        "Use 1 for the raw profile speed. The recommended project default is 0.55."
    )]
    [SerializeField, Range(0.10f, 1.50f)]
    private float professionalProfileSpeedScale = 0.55f;

    [Tooltip("Optional reusable multi-stage death controller. Leave empty for ordinary fish.")]
    [SerializeField]
    private SpecialFishCinematicDeathController cinematicDeathController;

    [Tooltip(
        "Optional visual-only cinematic that runs after StandardDeathSequence " +
        "has already completed its normal reward/death work."
    )]
    [SerializeField]
    private FishCloneCinematicDeathController postDeathCloneCinematicController;

    [SerializeField, Range(0.25f, 0.5f)] private float healthEvaluationInterval = 0.35f;

    [Header("Parade Participation")]
    [Tooltip(
        "Auto By Tier allows only Small and Medium fish in normal parades. " +
        "Use Always Block for any individual fish that must never join a parade."
    )]
    [SerializeField]
    private ParadeParticipationMode paradeParticipation =
        ParadeParticipationMode.AutoByTier;

    [Tooltip(
        "Relative chance of this prefab being selected when SwapFishScript " +
        "uses Manual Parade Selection. Zero disables selection."
    )]
    [SerializeField, Min(0f)]
    private float paradeSelectionWeight = 1f;

    [Header("Natural Species Spawning")]
    [Tooltip("Controls ambient spawning only. Parade permission is configured separately above.")]
    [SerializeField] private NaturalSpawnMode naturalSpawnMode =
        NaturalSpawnMode.Solo;

    [Tooltip("Relative ambient selection weight. Zero prevents ordinary ambient spawning.")]
    [SerializeField, Min(0f)] private float ambientSpawnWeight = 1f;

    [Tooltip("Per-species ambient population limit. The spawn director checks this before selecting the prefab.")]
    [SerializeField, Range(1, 32)]
    private int maximumSimultaneousCount = 8;

    [Tooltip("Chance that this prefab uses its natural pair/school size. A failed roll spawns one fish.")]
    [SerializeField, Range(0f, 1f)] private float ambientGroupChance = 0.15f;

    [Tooltip("Extra room for long or tall artwork. 1 keeps the automatically measured sprite spacing.")]
    [SerializeField, Range(0.65f, 3f)] private float formationHorizontalMultiplier = 1f;
    [SerializeField, Range(0.65f, 3f)] private float formationVerticalMultiplier = 1f;

    [Tooltip("World-space safety padding added after sprite size is measured.")]
    [SerializeField, Min(0f)] private float formationPadding = 0.15f;

    [Header("Organic Schooling and Escort Motion")]
    [Tooltip("Small local movement that keeps a formation alive without destroying its readable shape.")]
    [SerializeField, Range(0f, 0.35f)] private float schoolSlotBreathing = 0.12f;
    [SerializeField, Range(0f, 0.15f)] private float schoolSpeedVariation = 0.055f;
    [SerializeField, Range(0f, 12f)] private float schoolTurnVariation = 4f;
    [SerializeField, Range(8f, 60f)] private float escortOrbitDegreesPerSecond = 24f;
    [SerializeField, Range(0.5f, 3f)] private float escortSlotCorrection = 1.9f;

    // Parade control is runtime-only. It never changes the authored movement
    // profile and is cleared every time this pooled fish begins a new life.
    private bool paradeControlled;
    private bool paradeFollowerMode;
    private Vector3 paradeExitTarget;
    private float paradeFollowerCorrection = 3.8f;
    private float paradeFollowerCatchUpMultiplier = 1.65f;
    private float paradeFollowerWobble = 0.035f;

    [Header("World Rendering Order")]
    [SerializeField] private bool applyAutomaticFishSorting = true;
    [SerializeField] private string fishSortingLayerName = "Fish";
    [SerializeField] private bool useSortingGroup = true;
    [SerializeField] private int smallSortingOrder = 100;
    [SerializeField] private int mediumSortingOrder = 400;
    [SerializeField] private int specialSortingOrder = 800;
    [SerializeField] private int miniBossSortingOrder = 1400;
    [SerializeField] private int mainBossSortingOrder = 2000;
    [SerializeField] private int epicBossSortingOrder = 2200;
    [SerializeField, Min(0)] private int normalFishSortingVariation = 40;

    [Header("Normal Main Boss Presence")]
    [SerializeField]
    private MainBossPresenceMode mainBossPresenceMode =
        MainBossPresenceMode.RandomPerSpawn;

    [SerializeField, Range(0f, 1f)]
    private float timedRetreatChance = 0.70f;

    [SerializeField]
    private Vector2 bossArenaStayDurationRange =
        new Vector2(18f, 30f);

    [SerializeField, Range(0.55f, 0.95f)]
    private float bossArenaWidthPercent = 0.88f;

    [SerializeField, Range(0.50f, 0.92f)]
    private float bossArenaHeightPercent = 0.80f;

    [SerializeField, Min(1f)]
    private float bossSmoothTurnDegreesPerSecond = 55f;

    [SerializeField, Range(0.5f, 2f)]
    private float bossRetreatSpeedMultiplier = 1.05f;

    [SerializeField, Min(0.5f)]
    private float bossRetreatOutsideDistance = 3f;

    [Header("Natural Boss Exit Safety")]
    [Tooltip("Minimum world speed used only when a boss failed to complete its normal visible retreat before the timeout grace period ended.")]
    [SerializeField, Min(0.5f)]
    private float forcedNaturalExitMinimumSpeed = 2.6f;

    [Tooltip("Multiplier applied to the boss runtime speed during the final visible exit fallback. The boss is never hidden while still inside the padded camera bounds.")]
    [SerializeField, Min(1f)]
    private float forcedNaturalExitSpeedMultiplier = 3.2f;

    [Tooltip("Turn speed used by the final visible exit fallback.")]
    [SerializeField, Min(30f)]
    private float forcedNaturalExitTurnDegreesPerSecond = 240f;

    [Tooltip("Additional world distance beyond the padded screen edge before the retreat is considered complete.")]
    [SerializeField, Min(0.25f)]
    private float forcedNaturalExitExtraDistance = 1.5f;

    [Header("Natural Level Transition Exit Safety")]
    [Tooltip("Minimum world speed used when a fish did not finish its normal tide-change exit in time.")]
    [SerializeField, Min(0.5f)]
    private float forcedLevelExitMinimumSpeed = 2.2f;

    [Tooltip("Multiplier applied during the final visible tide-change exit. The fish is never pooled while any visual bounds remain inside the padded screen.")]
    [SerializeField, Min(1f)]
    private float forcedLevelExitSpeedMultiplier = 2.4f;

    [Tooltip("Turn speed used by the final visible tide-change exit.")]
    [SerializeField, Min(30f)]
    private float forcedLevelExitTurnDegreesPerSecond = 220f;

    [Tooltip("Additional world distance beyond the padded screen edge before a level-transition exit is complete.")]
    [SerializeField, Min(0.25f)]
    private float forcedLevelExitExtraDistance = 1.0f;

    [Header("Fish Stats")]
    [SerializeField] public float Hp;
    [SerializeField] public float CoinFish;
    [SerializeField] public float MoveSpeed;
    public int id;

    [Header("Special Gimmicks")]
    public GimmickType gimmickType = GimmickType.None;
    [SerializeField] private float gimmickRadius = 4f;
    [SerializeField] private float gimmickDamage = 50f;

    [Header("Adaptive Boss Movement")]
    [SerializeField] private float woundedMoveSpeedMultiplier = 0.38f;
    [SerializeField] private float woundedTwitchStrength = 0.45f;
    [SerializeField] private float woundedTurnAngle = 11f;

    private float baseHp;
    private float baseCoinFish;
    private float baseMoveSpeed;
    private float profileReferenceSpeed;
    private float runtimeMaxHp;

    [SerializeField, HideInInspector]
    private int fallbackCombatBalanceVersion;

    private Rigidbody2D rb2d;
    private SpriteRenderer fishSprite;
    private SortingGroup sortingGroup;
    private Animator animator;
    private float defaultAnimatorSpeed = 1f;
    private GameManager gameManager;

    private Coroutine movementCoroutine;
    private Coroutine hitFlashCoroutine;
    private Coroutine reactiveHitCoroutine;
    private Coroutine externalExplosionReactionCoroutine;
    private Coroutine deathRoutine;
    private Coroutine bossBehaviorCoroutine;
    private Coroutine forcedNaturalBossExitCoroutine;
    private Coroutine forcedLevelTransitionExitCoroutine;
    private float forcedLevelTransitionSpeedRequest = 1f;
    private Transform leaderTransform;
    private Vector3 followOffset;

    private bool hasEnteredScreen;
    private bool forceBossReward;
    private bool persistentTargetBoss;
    private bool bossDefeatReported;
    private bool woundedBossMovementActive;
    private SwimStyle currentSwimStyle = SwimStyle.LaneGlide;
    private float nextHealthEvaluationTime;
    private SwapFishScript spawnDirector;
    private EpicBossController epicBossController;
    private bool deathPresentationResolved;
    private bool bossRetreatScheduled;
    private bool bossRetreating;
    private float bossRetreatAtTime;
    private bool forceFixedRewardForCurrentDeath;
    private bool timeoutCountsAsDefeat;
    private float timeoutPartialRewardPercent;

    private Collider2D[] cachedColliders;
    private bool[] cachedColliderEnabled;
    private SpriteRenderer[] visualRenderers;
    private Color[] visualDefaultColors;
    private Vector3 defaultLocalScale;

    private bool isDying;
    private bool deathRewardGranted;
    private bool mainBossFrontGunSkillTriggered;

    // Immutable ownership snapshot for the hit that actually reduced this
    // fish to 0 HP. Post-death cinematic damage must inherit this credit so a
    // later Boom cannot accidentally pay an NPC simply because another gun is
    // active while the cinematic is still running. Player = shooter 0; NPC
    // guns use their existing shooter IDs (1..3).
    private bool deathCreditCaptured;
    private int deathCreditBulletId;
    private int deathCreditGunLevel = 1;

    private float hitMovementMultiplier = 1f;
    private float lastIncomingHitTime = -100f;
    private int incomingHitChain;
    private float nextReactiveHitTime;

    // Temporary movement effects are applied after every movement controller
    // has written its velocity. This lets Freeze Wave work consistently with
    // organic fish, parade followers, escorts, and Epic Boss movement without
    // replacing any of their authored routes.
    private bool externalMovementSlowActive;
    private float externalMovementMultiplier = 1f;
    private float externalMovementSlowUntil = -1f;

    private FishHealthPhase currentHealthPhase = FishHealthPhase.Healthy;
    private bool hasEvaluatedHealthPhase;
    private float nextStyleChangeTime;

    private SmartLoopStage smartLoopStage;
    private int completedSmartLoops;
    private int smartLoopExitSide = 1;
    private float smartLoopDeadline;
    private float nextSmartLoopTime;
    private Vector3 smartMovementTarget;
    private float smartTargetDeadline;
    private bool forceSmartReturnToArena;
    private float smartNaturalExitTime;
    private bool finalSmartExitActive;
    private float smartTrafficLaneY = 0.30f;
    private float smartOrbitAnchorX = 0.27f;

    // Incremented whenever this pooled object begins a new life. Auto Shot
    // and Big Rocket use this token so they cannot hit a recycled fish that
    // happens to reuse the same GameObject instance.
    public int TargetLifeVersion { get; private set; }

    /// <summary>
    /// Authored/pool-restored scale used by visual-only post-death clones so
    /// they do not inherit the temporary death-zoom scale.
    /// </summary>
    public Vector3 DefaultLocalScale
    {
        get { return defaultLocalScale; }
    }

    public bool IsAliveTarget
    {
        get
        {
            return gameObject.activeInHierarchy &&
                   isActiveAndEnabled &&
                   Hp > 0f;
        }
    }

    public bool IsDeadOrDying
    {
        get { return Hp <= 0f || isDying; }
    }

    public bool IsCinematicDeathRunning
    {
        get
        {
            bool exclusiveCinematic =
                cinematicDeathController != null &&
                cinematicDeathController.IsRunning;
            bool postDeathCloneCinematic =
                postDeathCloneCinematicController != null &&
                postDeathCloneCinematicController.IsRunning;

            return exclusiveCinematic || postDeathCloneCinematic;
        }
    }

    public bool ShouldBlockLevelTransition
    {
        get
        {
            if (IsCinematicDeathRunning)
            {
                return true;
            }

            return gameObject.activeInHierarchy &&
                   isActiveAndEnabled &&
                   Hp > 0f &&
                   !isDying;
        }
    }

    public int FallbackCombatBalanceVersion
    {
        get { return fallbackCombatBalanceVersion; }
    }

    public bool ApplyFallbackCombatBalanceUpgrade(
        int targetVersion,
        float healthMultiplier,
        float rewardMultiplier
    )
    {
        if (gameplayProfile != null ||
            fallbackCombatBalanceVersion >= targetVersion)
        {
            return false;
        }

        Hp = Mathf.Max(1f, Hp * Mathf.Max(1f, healthMultiplier));
        CoinFish = Mathf.Max(0f, CoinFish * Mathf.Max(1f, rewardMultiplier));
        fallbackCombatBalanceVersion = targetVersion;
        return true;
    }

    public float CurrentHealth
    {
        get { return Mathf.Max(0f, Hp); }
    }

    /// <summary>
    /// True after the exact lethal hit has been captured for this pooled life.
    /// </summary>
    public bool HasDeathCredit
    {
        get { return deathCreditCaptured; }
    }

    /// <summary>
    /// Shooter that delivered the lethal hit. 0 is the local/player gun; the
    /// existing NPC guns use IDs 1..3.
    /// </summary>
    public int DeathCreditBulletId
    {
        get { return deathCreditBulletId; }
    }

    /// <summary>
    /// Gun level paired with DeathCreditBulletId on the lethal hit.
    /// </summary>
    public int DeathCreditGunLevel
    {
        get { return Mathf.Max(1, deathCreditGunLevel); }
    }

    public float MaximumHealth
    {
        get { return Mathf.Max(1f, runtimeMaxHp); }
    }

    public float CurrentHealthNormalized
    {
        get { return Mathf.Clamp01(CurrentHealth / MaximumHealth); }
    }

    public bool IsEpicBoss
    {
        get
        {
            return epicBossController != null &&
                   epicBossController.IsActive;
        }
    }

    public bool IsMovementSlowed
    {
        get
        {
            return externalMovementSlowActive &&
                   Time.time < externalMovementSlowUntil;
        }
    }

    public bool IsParadeControlled
    {
        get
        {
            return paradeControlled &&
                   gameObject.activeInHierarchy &&
                   !isDying;
        }
    }

    public Vector3 ParadeExitTarget
    {
        get { return paradeExitTarget; }
    }

    /// <summary>
    /// Temporarily gives the parade director exclusive movement ownership.
    /// HP/reward/profile data are not changed. MoveSpeed is runtime-only and
    /// returns to the normal pooled value on the next spawn.
    /// </summary>
    public void PrepareForParadeControl(
        float worldSpeed,
        float followerCorrection,
        float catchUpMultiplier,
        float slotWobble
    )
    {
        paradeControlled = true;
        paradeFollowerMode = false;
        paradeExitTarget = transform.position;
        paradeFollowerCorrection = Mathf.Max(0.5f, followerCorrection);
        paradeFollowerCatchUpMultiplier = Mathf.Max(1f, catchUpMultiplier);
        paradeFollowerWobble = Mathf.Max(0f, slotWobble);
        MoveSpeed = Mathf.Max(0.05f, worldSpeed);
    }

    private void OnValidate()
    {
        if (professionalProfileSpeedScale <= 0.01f)
        {
            professionalProfileSpeedScale = 0.55f;
        }

        professionalProfileSpeedScale = Mathf.Clamp(
            professionalProfileSpeedScale,
            0.10f,
            1.50f
        );
    }

    private void Awake()
    {
        if (gameplayProfile != null)
        {
            fishTier = gameplayProfile.fishTier;
            id = gameplayProfile.fishId;

            // v23: do not trust the ScriptableObject's old default 10/10
            // combat values unless this profile was generated by the current
            // balance model. Older or partially generated profiles keep all
            // of their movement/personality data, but HP/reward come from the
            // clean balance table using the fish ID.
            GetAuthoritativeCombatValues(
                out float authoritativeHealth,
                out _,
                out float authoritativeReward
            );
            Hp = Mathf.Max(1f, authoritativeHealth);
            CoinFish = Mathf.Max(0f, authoritativeReward);

            profileReferenceSpeed = Mathf.Max(
                0.05f,
                (gameplayProfile.minimumSpeed + gameplayProfile.maximumSpeed) * 0.5f
            );

            // The generated professional profiles use a larger world-speed
            // range than the original project. This compatibility multiplier
            // keeps every existing fish at the familiar arcade pace while
            // preserving profile-to-profile speed differences.
            professionalProfileSpeedScale = Mathf.Clamp(
                professionalProfileSpeedScale <= 0.01f
                    ? 0.55f
                    : professionalProfileSpeedScale,
                0.10f,
                1.50f
            );

            MoveSpeed = Mathf.Max(
                0.05f,
                profileReferenceSpeed * professionalProfileSpeedScale
            );
        }
        else
        {
            profileReferenceSpeed = Mathf.Max(0.05f, MoveSpeed);
        }

        baseHp = Mathf.Max(1f, Hp);
        baseCoinFish = Mathf.Max(0f, CoinFish);
        baseMoveSpeed = Mathf.Max(0.05f, MoveSpeed);
        runtimeMaxHp = baseHp;

        rb2d = GetComponent<Rigidbody2D>();
        fishSprite = GetComponent<SpriteRenderer>();

        sortingGroup = GetComponent<SortingGroup>();

        if (useSortingGroup && sortingGroup == null)
        {
            sortingGroup = gameObject.AddComponent<SortingGroup>();
        }

        animator = GetComponent<Animator>();
        if (animator != null)
        {
            defaultAnimatorSpeed = Mathf.Max(0.01f, animator.speed);
        }
        epicBossController = GetComponent<EpicBossController>();

        if (useProfessionalMotionAgent)
        {
            if (professionalMotionAgent == null)
            {
                professionalMotionAgent = GetComponent<FishMotionAgent>();
            }

            if (professionalMotionAgent == null)
            {
                professionalMotionAgent = gameObject.AddComponent<FishMotionAgent>();
            }

            professionalMotionAgent.Bind(this, gameplayProfile, movementProfile);
        }

        if (gameplayProfile != null)
        {
            SpriteShadow spriteShadow = GetComponent<SpriteShadow>();
            if (spriteShadow == null)
            {
                spriteShadow = GetComponentInChildren<SpriteShadow>(true);
            }
            if (spriteShadow != null)
            {
                spriteShadow.ApplyGameplayProfile(gameplayProfile);
            }
        }

        if (cinematicDeathController == null)
        {
            cinematicDeathController =
                GetComponent<SpecialFishCinematicDeathController>();
        }

        if (postDeathCloneCinematicController == null)
        {
            postDeathCloneCinematicController =
                GetComponent<FishCloneCinematicDeathController>();
        }

        defaultLocalScale = transform.localScale;
        CacheRuntimeVisualState();
    }

    private void OnEnable()
    {
        unchecked
        {
            TargetLifeVersion++;

            if (TargetLifeVersion == 0)
            {
                TargetLifeVersion = 1;
            }
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        ResetRuntimeSpawnState();

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.Bind(this, gameplayProfile, movementProfile);
        }

        if (fishSprite != null)
        {
            fishSprite.color = GetDefaultRendererColor(fishSprite);
            AutoAssignSortingOrder();
        }

        FishTier tier = GetFishTier();
        SetMovementStyle(GetDefaultMovementForTier(tier, FishHealthPhase.Healthy));
    }

    /// <summary>
    /// Returns the combined center of all enabled child hitboxes. This keeps
    /// Auto Shot and Big Rocket centered on large or multi-collider fish.
    /// </summary>
    public Vector3 GetTargetCenterWorld()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        bool hasBounds = false;
        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(candidate.bounds);
            }
        }

        if (hasBounds)
        {
            return combinedBounds.center;
        }

        return fishSprite != null
            ? fishSprite.bounds.center
            : transform.position;
    }

    public bool IsTargetVisibleTo(Camera targetCamera)
    {
        if (!IsAliveTarget || targetCamera == null)
        {
            return false;
        }

        Vector3 viewportPoint = targetCamera.WorldToViewportPoint(
            GetTargetCenterWorld()
        );
        const float padding = 0.04f;

        return viewportPoint.z > 0f &&
               viewportPoint.x >= -padding &&
               viewportPoint.x <= 1f + padding &&
               viewportPoint.y >= -padding &&
               viewportPoint.y <= 1f + padding;
    }

    private void OnDisable()
    {
        if (forcedNaturalBossExitCoroutine != null)
        {
            StopCoroutine(forcedNaturalBossExitCoroutine);
            forcedNaturalBossExitCoroutine = null;
        }

        if (forcedLevelTransitionExitCoroutine != null)
        {
            StopCoroutine(forcedLevelTransitionExitCoroutine);
            forcedLevelTransitionExitCoroutine = null;
        }

        forcedLevelTransitionSpeedRequest = 1f;

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.ResetForPool();
        }

        ClearExternalMovementSlow(false);
        StopBossBehaviorController();
        StopMovement();
        StopReactiveHitMotion(true);

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (externalExplosionReactionCoroutine != null)
        {
            StopCoroutine(externalExplosionReactionCoroutine);
            externalExplosionReactionCoroutine = null;
        }

        RestoreRuntimeVisualState();
        RestoreAnimatorSpeedToDefault();
        RestoreCachedColliders();

        if (gameManager != null && gameManager.fishInScreenList.Contains(this))
        {
            gameManager.fishInScreenList.Remove(this);
        }
    }

    private void Update()
    {
        // Legacy movement used OnBecameInvisible, which fires when the first
        // pixel leaves the camera and could hide fish/shadows too early.
        // Tide-change fish now remain active until their complete visual
        // bounds are beyond the padded screen.
        if (!isDying &&
            currentSwimStyle == SwimStyle.FastTideExit &&
            forcedLevelTransitionExitCoroutine == null &&
            IsFullyOutsidePaddedView())
        {
            gameObject.SetActive(false);
            return;
        }

        bool professionalFacing =
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority;

        if (!professionalFacing && fishSprite != null && rb2d != null &&
            Mathf.Abs(rb2d.velocity.x) > 0.08f)
        {
            fishSprite.flipY = rb2d.velocity.x < 0f;
        }
    }

    private void LateUpdate()
    {
        if (!externalMovementSlowActive)
        {
            return;
        }

        if (!IsAliveTarget || isDying ||
            Time.time >= externalMovementSlowUntil)
        {
            ClearExternalMovementSlow(true);
            return;
        }

        float movementMultiplier = Mathf.Clamp01(
            externalMovementMultiplier
        );

        if (rb2d != null)
        {
            rb2d.velocity *= movementMultiplier;
            rb2d.angularVelocity *= movementMultiplier;
        }

        if (animator != null && animator.isActiveAndEnabled)
        {
            float animationSpeedLimit = Mathf.Lerp(
                0.10f,
                1f,
                movementMultiplier
            );

            animator.speed = Mathf.Min(
                animator.speed,
                animationSpeedLimit
            );
        }
    }

    // Called whenever this object is enabled or reused from the pool.
    public void ResetRuntimeSpawnState()
    {
        if (forcedNaturalBossExitCoroutine != null)
        {
            StopCoroutine(forcedNaturalBossExitCoroutine);
            forcedNaturalBossExitCoroutine = null;
        }

        if (forcedLevelTransitionExitCoroutine != null)
        {
            StopCoroutine(forcedLevelTransitionExitCoroutine);
            forcedLevelTransitionExitCoroutine = null;
        }

        forcedLevelTransitionSpeedRequest = 1f;

        ClearExternalMovementSlow(false);
        StopBossBehaviorController();

        Hp = Mathf.Max(1f, baseHp);
        CoinFish = Mathf.Max(0f, baseCoinFish);
        MoveSpeed = baseMoveSpeed;
        runtimeMaxHp = Hp;

        hasEnteredScreen = false;
        forceBossReward = false;
        persistentTargetBoss = false;
        bossDefeatReported = false;
        woundedBossMovementActive = false;
        deathPresentationResolved = false;
        deathRewardGranted = false;
        mainBossFrontGunSkillTriggered = false;
        deathCreditCaptured = false;
        deathCreditBulletId = 0;
        deathCreditGunLevel = 1;
        isDying = false;
        forceFixedRewardForCurrentDeath = false;
        timeoutCountsAsDefeat = false;
        timeoutPartialRewardPercent = 0f;
        bossRetreatScheduled = false;
        bossRetreating = false;
        bossRetreatAtTime = 0f;
        currentSwimStyle = SwimStyle.LaneGlide;
        spawnDirector = null;
        leaderTransform = null;
        followOffset = Vector3.zero;
        paradeControlled = false;
        paradeFollowerMode = false;
        paradeExitTarget = Vector3.zero;
        paradeFollowerCorrection = 3.8f;
        paradeFollowerCatchUpMultiplier = 1.65f;
        paradeFollowerWobble = 0.035f;
        hitMovementMultiplier = 1f;
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;
        nextReactiveHitTime = 0f;
        hasEvaluatedHealthPhase = false;
        currentHealthPhase = FishHealthPhase.Healthy;
        nextStyleChangeTime = 0f;
        smartLoopStage = SmartLoopStage.None;
        completedSmartLoops = 0;
        smartLoopExitSide = 1;
        smartLoopDeadline = 0f;
        nextSmartLoopTime = Time.time + Random.Range(4f, 8f);
        smartMovementTarget = transform.position;
        smartTargetDeadline = 0f;
        forceSmartReturnToArena = false;
        smartNaturalExitTime = Time.time + RandomRangeSafe(
            movementProfile != null
                ? movementProfile.naturalLifetimeRange
                : new Vector2(16f, 28f),
            22f,
            5f
        );
        finalSmartExitActive = false;
        InitializeSmartTrafficRoute();

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.ResetForPool();
            professionalMotionAgent.Bind(this, gameplayProfile, movementProfile);
        }

        // Reset optional controllers before restoring the root visual state.
        // Unity does not guarantee component callback order, so a cinematic
        // controller can otherwise restore an uncached default Vector3.zero
        // during the first pooled activation.
        if (cinematicDeathController != null)
        {
            cinematicDeathController.ResetForPool();
        }

        if (postDeathCloneCinematicController != null)
        {
            postDeathCloneCinematicController.ResetForPool();
        }

        if (epicBossController != null)
        {
            epicBossController.ResetForPool();
        }

        StopReactiveHitMotion(true);
        RestoreRuntimeVisualState();
        RestoreAnimatorSpeedToDefault();
        RestoreCachedColliders();

        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
    }

    // Runtime scaling does not modify the prefab's original Inspector values.
    public void ApplyRuntimeScaling(float hpMultiplier, float rewardMultiplier, float speedMultiplier)
    {
        Hp = Mathf.Max(
            1f,
            baseHp * Mathf.Max(0.05f, hpMultiplier)
        );
        runtimeMaxHp = Hp;
        CoinFish = Mathf.Max(
            0f,
            baseCoinFish * Mathf.Max(0f, rewardMultiplier)
        );
        MoveSpeed = Mathf.Max(0.05f, baseMoveSpeed * Mathf.Max(0.05f, speedMultiplier));
    }

    /// <summary>
    /// Applies extra multipliers to the already level-scaled runtime stats.
    /// Used by EpicBossProfile after SwapFishScript has applied level scaling.
    /// </summary>
    public void MultiplyCurrentRuntimeStats(
        float hpMultiplier,
        float rewardMultiplier,
        float speedMultiplier
    )
    {
        Hp = Mathf.Max(1f, Hp * Mathf.Max(0.05f, hpMultiplier));
        runtimeMaxHp = Hp;
        CoinFish = Mathf.Max(0f, CoinFish * Mathf.Max(0f, rewardMultiplier));
        MoveSpeed = Mathf.Max(0.05f, MoveSpeed * Mathf.Max(0.05f, speedMultiplier));
    }

    public void ConfigureAsLevelBoss(SwapFishScript director)
    {
        spawnDirector = director;
        fishTier = FishTier.MainBoss;
        forceBossReward = true;
        persistentTargetBoss = true;
        bossDefeatReported = false;
        woundedBossMovementActive = false;
        deathPresentationResolved = false;
        runtimeMaxHp = Mathf.Max(1f, Hp);
        AutoAssignSortingOrder();

        if (epicBossController == null)
        {
            epicBossController = GetComponent<EpicBossController>();
        }

        if (epicBossController != null && epicBossController.HasProfile)
        {
            bossRetreatScheduled = false;
            StopBossBehaviorController();
            StopMovement();

            if (professionalMotionAgent != null)
            {
                professionalMotionAgent.SetExternalMovementAuthority(true);
            }

            epicBossController.ConfigureAsEpicBoss(this, director);
        }
        else
        {
            if (professionalMotionAgent != null)
            {
                professionalMotionAgent.SetExternalMovementAuthority(false);
                professionalMotionAgent.ConfigureBossMovement();
            }

            ScheduleNormalBossRetreat();
            StartBossBehaviorController();
        }
    }

    public void ConfigureAsMiniBoss()
    {
        spawnDirector = null;
        fishTier = FishTier.MiniBoss;
        forceBossReward = true;
        persistentTargetBoss = false;
        bossDefeatReported = false;
        bossRetreatScheduled = false;
        bossRetreating = false;
        AutoAssignSortingOrder();

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.SetExternalMovementAuthority(false);
        }

        // Apply the assigned movement profile immediately and continue
        // evaluating its Healthy/Aggressive/Critical phases.
        StartBossBehaviorController();
    }

    public void TakeDamage(SpriteRenderer targetSprite, float damage, int bulletId, int activeGunLevel)
    {
        if (!gameObject.activeInHierarchy || Hp <= 0f)
        {
            return;
        }

        float appliedDamage = Mathf.Max(0f, damage);

        // A cinematic profile can provide the fish-specific armor value.
        // Otherwise retain the professional gameplay-profile resistance.
        if (cinematicDeathController != null &&
            cinematicDeathController.HasProfile)
        {
            appliedDamage =
                cinematicDeathController.ModifyIncomingDamage(
                    appliedDamage
                );
        }
        else
        {
            float resistance = GetAuthoritativeDamageResistance();
            appliedDamage *= 1f - resistance;
        }

        bool lethalHit = Hp - appliedDamage <= 0f;

        if (lethalHit && !deathCreditCaptured)
        {
            // Capture reward ownership before any cinematic/death coroutine can
            // start. This value remains unchanged until the fish returns to the
            // pool, so all chained post-death effects inherit the real killer.
            deathCreditCaptured = true;
            deathCreditBulletId = bulletId;
            deathCreditGunLevel = Mathf.Max(1, activeGunLevel);
        }

        if (lethalHit && cinematicDeathController != null)
        {
            cinematicDeathController.CaptureFinalAttacker(
                deathCreditBulletId,
                deathCreditGunLevel
            );
        }

        Hp -= appliedDamage;

        if (Hp <= 0f)
        {
            // Never let a temporary Freeze Wave hold a pooled death animation
            // at a reduced Animator speed.
            ClearExternalMovementSlow(true);
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(FlashHitColor(targetSprite));

        if (epicBossController != null && epicBossController.IsActive)
        {
            if (Hp <= 0f)
            {
                if (epicBossController.TryBeginDeath(
                        bulletId,
                        activeGunLevel
                    ))
                {
                    return;
                }
            }
            else
            {
                epicBossController.NotifyHealthChanged(
                    Hp,
                    runtimeMaxHp,
                    bulletId,
                    activeGunLevel
                );
            }
        }

        if (Hp > 0f &&
            (epicBossController == null || !epicBossController.IsActive))
        {
            RegisterReactiveHit();
        }
        else if (Hp <= 0f)
        {
            StopReactiveHitMotion(true);
        }

        if (Hp > 0f && Time.time >= nextHealthEvaluationTime)
        {
            nextHealthEvaluationTime = Time.time + healthEvaluationInterval;
            EvaluateHealthMovement();
        }

        FishSystem(bulletId, activeGunLevel);
    }

    /// <summary>
    /// Lets target-aware weapons end the reaction on the exact frame that
    /// Auto Shot is cancelled or changes target. Untargeted/manual fire still
    /// falls back to the short hit-cadence grace timer.
    /// </summary>
    public void NotifyIncomingFireStopped()
    {
        lastIncomingHitTime = -100f;
        incomingHitChain = 0;

        if (epicBossController != null && epicBossController.IsActive)
        {
            epicBossController.NotifyIncomingFireStopped();
        }

        StopReactiveHitMotion(true);
    }

    /// <summary>
    /// Applies or refreshes a temporary movement slow. Repeated applications
    /// keep the strongest multiplier and the longest remaining duration.
    /// The effect changes presentation only; HP, rewards, target lifetime,
    /// and authored movement routes are untouched.
    /// </summary>
    public void ApplyExternalMovementSlow(
        float duration,
        float movementMultiplier
    )
    {
        if (!IsAliveTarget || isDying || duration <= 0f)
        {
            return;
        }

        float safeMultiplier = Mathf.Clamp(
            movementMultiplier,
            0.02f,
            1f
        );

        if (!externalMovementSlowActive)
        {
            externalMovementMultiplier = safeMultiplier;
        }
        else
        {
            externalMovementMultiplier = Mathf.Min(
                externalMovementMultiplier,
                safeMultiplier
            );
        }

        externalMovementSlowActive = true;
        externalMovementSlowUntil = Mathf.Max(
            externalMovementSlowUntil,
            Time.time + duration
        );
    }

    public void ClearExternalMovementSlow()
    {
        ClearExternalMovementSlow(true);
    }

    private void ClearExternalMovementSlow(bool restoreAnimatorSpeed)
    {
        bool wasActive = externalMovementSlowActive;
        externalMovementSlowActive = false;
        externalMovementMultiplier = 1f;
        externalMovementSlowUntil = -1f;

        if (wasActive && restoreAnimatorSpeed &&
            animator != null && animator.isActiveAndEnabled)
        {
            UpdateAnimationSpeed();
        }
    }

    /// <summary>
    /// Applies one Big Rocket direct or splash hit. A Rocket defeat always
    /// awards this fish's fixed CoinFish value exactly once.
    /// </summary>
    public void TakeRocketDamage(
        SpriteRenderer targetSprite,
        float damage,
        int bulletId,
        int activeGunLevel
    )
    {
        if (!IsAliveTarget)
        {
            return;
        }

        bool thisHitWillDefeat =
            Hp - Mathf.Max(0f, damage) <= 0f;

        if (thisHitWillDefeat)
        {
            forceFixedRewardForCurrentDeath = true;
        }

        TakeDamage(targetSprite, damage, bulletId, activeGunLevel);

        if (Hp > 0f)
        {
            forceFixedRewardForCurrentDeath = false;
        }
    }

    private IEnumerator FlashHitColor(SpriteRenderer targetSprite)
    {
        if (targetSprite == null)
        {
            yield break;
        }

        targetSprite.color = new Color(1f, 0.4f, 0.4f, 1f);
        yield return new WaitForSeconds(0.08f);

        if (targetSprite != null)
        {
            targetSprite.color = GetDefaultRendererColor(targetSprite);
        }
    }

    public void ResetFishColor(SpriteRenderer targetSprite)
    {
        if (targetSprite == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        StartCoroutine(ResetFishColorRoutine(targetSprite));
    }

    private IEnumerator ResetFishColorRoutine(SpriteRenderer targetSprite)
    {
        yield return new WaitForSeconds(0.1f);

        if (targetSprite != null)
        {
            targetSprite.color = GetDefaultRendererColor(targetSprite);
        }
    }

    /// <summary>
    /// Optional lightweight reaction requested by another fish's cinematic
    /// area hit. It is only used for survivors, so it never replaces the
    /// victim's own normal or special death sequence.
    /// </summary>
    public void PlayExternalExplosionReaction(
        float duration,
        float scaleStrength,
        float rotationDegrees,
        float redStrength
    )
    {
        if (!IsAliveTarget || isDying)
        {
            return;
        }

        if (externalExplosionReactionCoroutine != null)
        {
            StopCoroutine(externalExplosionReactionCoroutine);
        }

        externalExplosionReactionCoroutine = StartCoroutine(
            ExternalExplosionReactionRoutine(
                duration,
                scaleStrength,
                rotationDegrees,
                redStrength
            )
        );
    }

    private IEnumerator ExternalExplosionReactionRoutine(
        float duration,
        float scaleStrength,
        float rotationDegrees,
        float redStrength
    )
    {
        float safeDuration = Mathf.Max(0.02f, duration);
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < safeDuration &&
               gameObject.activeInHierarchy &&
               !isDying)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            float wave = Mathf.Sin(normalized * Mathf.PI * 5f);
            float scale = 1f + wave *
                Mathf.Max(0f, scaleStrength) * envelope;

            transform.localScale = startScale * scale;
            transform.rotation = startRotation * Quaternion.Euler(
                0f,
                0f,
                wave * rotationDegrees * envelope
            );

            float tintStrength = Mathf.Clamp01(redStrength * envelope);

            if (visualRenderers != null)
            {
                for (int i = 0; i < visualRenderers.Length; i++)
                {
                    SpriteRenderer renderer = visualRenderers[i];

                    if (renderer == null)
                    {
                        continue;
                    }

                    Color baseColor =
                        i < visualDefaultColors.Length
                            ? visualDefaultColors[i]
                            : Color.white;
                    Color redColor = new Color(
                        1f,
                        baseColor.g * 0.22f,
                        baseColor.b * 0.22f,
                        baseColor.a
                    );
                    renderer.color = Color.Lerp(
                        baseColor,
                        redColor,
                        tintStrength
                    );
                }
            }

            yield return null;
        }

        if (!isDying)
        {
            transform.localScale = startScale;
            transform.rotation = startRotation;
            RestoreRuntimeVisualState();
        }

        externalExplosionReactionCoroutine = null;
    }

    private void RegisterReactiveHit()
    {
        if (isDying || movementProfile == null ||
            !movementProfile.enableReactiveHitMotion ||
            (gameplayProfile != null &&
             gameplayProfile.ignoreReactiveHitMotion))
        {
            return;
        }

        float now = Time.time;
        float chainWindow = Mathf.Max(
            0.05f,
            movementProfile.sustainedFireWindow
        );

        incomingHitChain = now - lastIncomingHitTime <= chainWindow
            ? incomingHitChain + 1
            : 1;
        lastIncomingHitTime = now;

        bool sustained = incomingHitChain >= Mathf.Max(
            1,
            movementProfile.sustainedFireHitCount
        );

        bool shouldReact = sustained ||
            Random.value <= movementProfile.microConvulsionChancePerHit;

        if (!shouldReact || reactiveHitCoroutine != null ||
            now < nextReactiveHitTime)
        {
            return;
        }

        reactiveHitCoroutine = StartCoroutine(ReactiveHitRoutine());
    }

    private IEnumerator ReactiveHitRoutine()
    {
        SetAnimatorBoolSafe(
            movementProfile.sustainedFireAnimatorBool,
            true
        );

        bool firstPulse = true;

        while (gameObject.activeInHierarchy && Hp > 0f && !isDying)
        {
            if (!firstPulse && !IsIncomingFireActive())
            {
                break;
            }

            TrySetAnimatorTriggerSafe(
                movementProfile.microConvulsionAnimatorTrigger
            );

            float duration = RandomRangeSafe(
                movementProfile.microConvulsionDurationRange,
                0.09f,
                0.02f
            );
            float elapsed = 0f;

            while (elapsed < duration &&
                   gameObject.activeInHierarchy &&
                   Hp > 0f &&
                   !isDying)
            {
                if (!IsIncomingFireActive())
                {
                    break;
                }

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float envelope = Mathf.Sin(normalized * Mathf.PI);
                float oscillation = Mathf.Sin(normalized * Mathf.PI * 5f);
                float strength =
                    Mathf.Max(
                        0f,
                        movementProfile.microConvulsionScaleStrength
                    ) * envelope;

                transform.localScale = Vector3.Scale(
                    defaultLocalScale,
                    new Vector3(
                        1f + oscillation * strength,
                        1f - oscillation * strength * 0.72f,
                        1f
                    )
                );

                hitMovementMultiplier = Mathf.Lerp(
                    1f,
                    Mathf.Clamp(
                        movementProfile.reactionMovementSpeedMultiplier,
                        0.15f,
                        1f
                    ),
                    envelope
                );

                yield return null;
            }

            transform.localScale = defaultLocalScale;
            hitMovementMultiplier = 1f;
            firstPulse = false;

            bool sustained = incomingHitChain >= Mathf.Max(
                1,
                movementProfile.sustainedFireHitCount
            );

            if (!sustained || !IsIncomingFireActive() ||
                Random.value >
                movementProfile.repeatedMicroConvulsionChance)
            {
                break;
            }

            float recovery = RandomRangeSafe(
                movementProfile.microConvulsionRecoveryRange,
                0.12f,
                0.02f
            );
            float recoveryElapsed = 0f;

            while (recoveryElapsed < recovery &&
                   IsIncomingFireActive() &&
                   gameObject.activeInHierarchy &&
                   Hp > 0f &&
                   !isDying)
            {
                recoveryElapsed += Time.deltaTime;
                yield return null;
            }
        }

        transform.localScale = defaultLocalScale;
        hitMovementMultiplier = 1f;
        SetAnimatorBoolSafe(
            movementProfile != null
                ? movementProfile.sustainedFireAnimatorBool
                : string.Empty,
            false
        );

        nextReactiveHitTime = Time.time + 0.035f;
        reactiveHitCoroutine = null;
    }

    private bool IsIncomingFireActive()
    {
        if (movementProfile == null)
        {
            return false;
        }

        return Time.time - lastIncomingHitTime <= Mathf.Max(
            0.05f,
            movementProfile.fireReleaseGracePeriod
        );
    }

    private void StopReactiveHitMotion(bool restoreImmediately)
    {
        if (reactiveHitCoroutine != null)
        {
            StopCoroutine(reactiveHitCoroutine);
            reactiveHitCoroutine = null;
        }

        hitMovementMultiplier = 1f;

        if (restoreImmediately)
        {
            transform.localScale = defaultLocalScale;
        }

        SetAnimatorBoolSafe(
            movementProfile != null
                ? movementProfile.sustainedFireAnimatorBool
                : string.Empty,
            false
        );
    }

    private void CacheRuntimeVisualState()
    {
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        cachedColliderEnabled = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            cachedColliderEnabled[i] =
                cachedColliders[i] != null && cachedColliders[i].enabled;
        }

        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        visualDefaultColors = new Color[visualRenderers.Length];

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            visualDefaultColors[i] = visualRenderers[i] != null
                ? visualRenderers[i].color
                : Color.white;
        }
    }

    private void RestoreRuntimeVisualState()
    {
        transform.localScale = defaultLocalScale;

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
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].color = visualDefaultColors[i];
            }
        }
    }

    private Color GetDefaultRendererColor(SpriteRenderer renderer)
    {
        if (renderer != null && visualRenderers != null &&
            visualDefaultColors != null)
        {
            int count = Mathf.Min(
                visualRenderers.Length,
                visualDefaultColors.Length
            );

            for (int i = 0; i < count; i++)
            {
                if (visualRenderers[i] == renderer)
                {
                    return visualDefaultColors[i];
                }
            }
        }

        return Color.white;
    }

    private void SetCachedCollidersEnabled(bool value)
    {
        if (cachedColliders == null)
        {
            return;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = value;
            }
        }
    }

    private void RestoreCachedColliders()
    {
        if (cachedColliders == null || cachedColliderEnabled == null)
        {
            return;
        }

        int count = Mathf.Min(
            cachedColliders.Length,
            cachedColliderEnabled.Length
        );

        for (int i = 0; i < count; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = cachedColliderEnabled[i];
            }
        }
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

    private void RestoreAnimatorSpeedToDefault()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        animator.speed = Mathf.Max(0.01f, defaultAnimatorSpeed);
    }

    private void ApplyDeathAnimatorSpeed(
        FishDeathProfile profile,
        bool convulsionActive
    )
    {
        if (profile == null || !profile.overrideDeathAnimatorSpeed ||
            animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        float configuredSpeed = convulsionActive &&
            profile.useSeparateConvulsionAnimatorSpeed
                ? profile.deathConvulsionAnimatorSpeed
                : profile.deathAnimatorSpeed;

        animator.speed = Mathf.Clamp(configuredSpeed, 0.05f, 4f);
    }

    private void TrySetAnimatorTriggerSafe(string parameterName)
    {
        if (animator == null || !animator.isActiveAndEnabled ||
            string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                parameters[i].name == parameterName)
            {
                animator.ResetTrigger(parameterName);
                animator.SetTrigger(parameterName);
                return;
            }
        }
    }

    private void SetAnimatorBoolSafe(string parameterName, bool value)
    {
        if (animator == null || !animator.isActiveAndEnabled ||
            string.IsNullOrEmpty(parameterName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                parameters[i].name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void AutoAssignSortingOrder()
    {
        if (!applyAutomaticFishSorting)
        {
            return;
        }

        FishTier tier = GetFishTier();
        int order;

        if (tier == FishTier.MainBoss)
        {
            bool isEpic =
                epicBossController != null &&
                epicBossController.HasProfile;

            order = isEpic
                ? epicBossSortingOrder
                : mainBossSortingOrder;
        }
        else if (tier == FishTier.MiniBoss)
        {
            order = miniBossSortingOrder;
        }
        else if (tier == FishTier.Special)
        {
            order = specialSortingOrder;
        }
        else if (tier == FishTier.Medium)
        {
            order = mediumSortingOrder +
                Random.Range(
                    0,
                    Mathf.Max(1, normalFishSortingVariation + 1)
                );
        }
        else
        {
            order = smallSortingOrder +
                Random.Range(
                    0,
                    Mathf.Max(1, normalFishSortingVariation + 1)
                );
        }

        if (useSortingGroup)
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();

                if (sortingGroup == null)
                {
                    sortingGroup = gameObject.AddComponent<SortingGroup>();
                }
            }

            if (!string.IsNullOrEmpty(fishSortingLayerName))
            {
                sortingGroup.sortingLayerName = fishSortingLayerName;
            }

            sortingGroup.sortingOrder = order;
            return;
        }

        if (fishSprite == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(fishSortingLayerName))
        {
            fishSprite.sortingLayerName = fishSortingLayerName;
        }

        fishSprite.sortingOrder = order;
    }

    public void StopMovement()
    {
        if (useProfessionalMotionAgent &&
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            professionalMotionAgent.StopMotion();
        }

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
        }
    }

    public void SetMovementStyle(
        SwimStyle style,
        Transform leader = null,
        Vector3 offset = default(Vector3)
    )
    {
        StopMovement();

        if ((style == SwimStyle.SchoolFollow ||
             style == SwimStyle.FollowLeader) &&
            leader == null)
        {
            // A weighted profile may be shared by solo and school spawns.
            // Never start a leaderless follow coroutine that ends on its
            // first frame; use a continuous cruise until a real leader is
            // explicitly supplied by the formation director.
            style = SwimStyle.LaneGlide;
        }

        currentSwimStyle = style;

        if (useProfessionalMotionAgent &&
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            leaderTransform = leader;
            followOffset = offset;

            if (style == SwimStyle.SchoolFollow ||
                style == SwimStyle.FollowLeader)
            {
                if (paradeControlled)
                {
                    paradeFollowerMode = true;
                    paradeExitTarget = GetLeaderParadeExitTarget(leader);
                    professionalMotionAgent.ConfigureParadeFollow(
                        leader,
                        offset,
                        paradeExitTarget,
                        paradeFollowerCorrection,
                        paradeFollowerCatchUpMultiplier,
                        paradeFollowerWobble
                    );
                }
                else
                {
                    professionalMotionAgent.ConfigureFollow(leader, offset, false);
                }
            }
            else
            {
                professionalMotionAgent.ConfigureMovementStyle(style);
            }
            return;
        }

        if (style == SwimStyle.SchoolFollow ||
            style == SwimStyle.FollowLeader)
        {
            leaderTransform = leader;
            followOffset = offset;
            if (paradeControlled)
            {
                paradeFollowerMode = true;
                paradeExitTarget = GetLeaderParadeExitTarget(leader);
            }
            movementCoroutine = StartCoroutine(FollowLeaderRoutine());
            return;
        }

        if (style == SwimStyle.FastTideExit)
        {
            movementCoroutine = StartCoroutine(FastTideExitRoutine());
            return;
        }

        if (movementProfile != null &&
            !movementProfile.enableOrganicSteering)
        {
            StartLegacyMovementStyle(style);
            return;
        }

        // Every free-swimming style now shares the same professional steering
        // core. Style differences are expressed through tuning, not abrupt
        // Rigidbody velocity assignments, so changing styles remains fluid.
        movementCoroutine = StartCoroutine(OrganicSwimRoutine(style));
    }

    /// <summary>
    /// Runs one authored parade path from its real spawn edge to its real exit
    /// edge. The broad route remains certain while a restrained sinusoidal
    /// sweep prevents the fish from looking mechanically straight.
    /// </summary>
    public void SetParadeRoute(
        Vector3 routeTarget,
        SwimStyle style,
        float sweepAmplitude = 0f,
        float sweepFrequency = 1f
    )
    {
        StopMovement();
        leaderTransform = null;
        followOffset = Vector3.zero;
        currentSwimStyle = style;
        finalSmartExitActive = true;
        smartLoopStage = SmartLoopStage.None;
        if (paradeControlled)
        {
            paradeFollowerMode = false;
            paradeExitTarget = routeTarget;
        }

        if (useProfessionalMotionAgent &&
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            professionalMotionAgent.ConfigureScriptedRoute(
                routeTarget,
                style,
                sweepAmplitude,
                sweepFrequency
            );
            return;
        }

        movementCoroutine = StartCoroutine(
            ScriptedParadeRouteRoutine(
                routeTarget,
                style,
                Mathf.Max(0f, sweepAmplitude),
                Mathf.Max(0.25f, sweepFrequency)
            )
        );
    }

    private Vector3 GetLeaderParadeExitTarget(Transform leader)
    {
        if (leader != null &&
            leader.TryGetComponent<FishScript>(out FishScript leaderFish) &&
            leaderFish.paradeControlled)
        {
            return leaderFish.paradeExitTarget;
        }

        return transform.position + transform.right * 20f;
    }

    /// <summary>
    /// Attaches a collidable guard to a valuable fish or boss. Roles have
    /// visibly different motion: Orbit circles, Flank breathes fore/aft, and
    /// Shield holds a dense protective slot.
    /// </summary>
    public void SetEscortMovement(
        Transform escortedTarget,
        Vector3 localOffset,
        EscortRole role,
        float phaseOffset = 0f
    )
    {
        if (escortedTarget == null)
        {
            SetMovementStyle(SwimStyle.LaneGlide);
            return;
        }

        StopMovement();
        leaderTransform = escortedTarget;
        followOffset = localOffset;
        currentSwimStyle = SwimStyle.SchoolFollow;

        if (useProfessionalMotionAgent &&
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            professionalMotionAgent.ConfigureFollow(
                escortedTarget,
                localOffset,
                true
            );
            return;
        }

        movementCoroutine = StartCoroutine(
            EscortTargetRoutine(role, phaseOffset)
        );
    }

    private void StartLegacyMovementStyle(SwimStyle style)
    {
        if (style == SwimStyle.LaneGlide ||
            style == SwimStyle.Straight ||
            style == SwimStyle.HorizontalRush)
        {
            movementCoroutine = StartCoroutine(StraightRoutine());
        }
        else if (style == SwimStyle.ArcSweep)
        {
            movementCoroutine = StartCoroutine(ArcSweepRoutine());
        }
        else if (style == SwimStyle.ZigZagBurst)
        {
            movementCoroutine = StartCoroutine(ZigZagBurstRoutine());
        }
        else if (style == SwimStyle.SpiralCross ||
                 style == SwimStyle.BossFigureEight ||
                 style == SwimStyle.FigureEight)
        {
            movementCoroutine = StartCoroutine(FigureEightRoutine());
        }
        else if (style == SwimStyle.VerticalDive)
        {
            movementCoroutine = StartCoroutine(VerticalDiveRoutine());
        }
        else if (style == SwimStyle.MiniBossHunter ||
                 style == SwimStyle.BossPatrol ||
                 style == SwimStyle.BossOrbit ||
                 style == SwimStyle.MiniBossOrbit ||
                 style == SwimStyle.BossArena)
        {
            movementCoroutine = StartCoroutine(BossArenaRoutine());
        }
        else if (style == SwimStyle.MiniBossCharge ||
                 style == SwimStyle.BossCharge ||
                 style == SwimStyle.BossDash)
        {
            movementCoroutine = StartCoroutine(BossDashRoutine());
        }
        else if (style == SwimStyle.CriticalStagger ||
                 style == SwimStyle.BossWoundedConvulsion)
        {
            movementCoroutine = StartCoroutine(
                BossWoundedConvulsionRoutine()
            );
        }
        else if (style == SwimStyle.Wave)
        {
            movementCoroutine = StartCoroutine(WaveRoutine());
        }
        else if (style == SwimStyle.Curious)
        {
            movementCoroutine = StartCoroutine(CuriousRoutine());
        }
        else if (style == SwimStyle.OrbitCenter)
        {
            movementCoroutine = StartCoroutine(OrbitCenterRoutine());
        }
        else if (style == SwimStyle.CuteDarting)
        {
            movementCoroutine = StartCoroutine(CuteDartingRoutine());
        }
        else if (style == SwimStyle.BossCuteDarting)
        {
            movementCoroutine = StartCoroutine(
                BossCuteDartingRoutine()
            );
        }
        else
        {
            movementCoroutine = StartCoroutine(StraightRoutine());
        }
    }

    private IEnumerator OrganicSwimRoutine(SwimStyle style)
    {
        if (rb2d == null)
        {
            movementCoroutine = null;
            yield break;
        }

        OrganicSwimTuning tuning = GetOrganicSwimTuning(style);
        Vector2 velocitySmoothReference = Vector2.zero;
        float noiseSeed = Random.Range(0f, 1000f);
        float speedPhase = Random.Range(0f, Mathf.PI * 2f);
        float pulseDuration = RandomRangeSafe(
            movementProfile != null
                ? movementProfile.speedPulseDurationRange
                : new Vector2(2.8f, 5.4f),
            4f,
            0.5f
        );
        Vector3 lastStuckPosition = transform.position;
        float nextStuckCheck = Time.time + GetStuckCheckInterval();

        ChooseNextSmartMovementTarget(style, tuning, true);

        while (gameObject.activeInHierarchy && !isDying)
        {
            float distanceToTarget = Vector2.Distance(
                transform.position,
                smartMovementTarget
            );
            float reachDistance = movementProfile != null
                ? Mathf.Max(0.10f, movementProfile.waypointReachDistance)
                : 0.48f;

            if (forceSmartReturnToArena)
            {
                forceSmartReturnToArena = false;
                finalSmartExitActive = false;
                smartLoopStage = SmartLoopStage.Returning;
                smartLoopDeadline = Time.time + GetOffscreenReturnTimeout();
                smartMovementTarget = GetSafeSmartReturnPoint();
                smartTargetDeadline = Time.time + 4f;
            }

            if (smartLoopStage != SmartLoopStage.None &&
                Time.time >= smartLoopDeadline)
            {
                smartLoopStage = SmartLoopStage.Returning;
                smartMovementTarget = GetSafeSmartReturnPoint();
                smartTargetDeadline = Time.time + 3f;
            }

            if (distanceToTarget <= reachDistance ||
                Time.time >= smartTargetDeadline)
            {
                if (smartLoopStage != SmartLoopStage.None)
                {
                    AdvanceSmartLoop(style, tuning);
                }
                else if (!finalSmartExitActive &&
                         ShouldBeginFinalNaturalExit())
                {
                    BeginFinalSmartExit();
                }
                else if (!finalSmartExitActive &&
                         ShouldBeginSmartLoop(tuning))
                {
                    BeginSmartLoop();
                }
                else if (!finalSmartExitActive)
                {
                    ChooseNextSmartMovementTarget(style, tuning, false);
                }
                else
                {
                    // A final exit target can expire when the fish makes an
                    // especially broad turn. Push it farther in the same
                    // direction instead of snapping or stopping.
                    BeginFinalSmartExit();
                }
            }

            Vector2 toTarget = smartMovementTarget - transform.position;
            Vector2 desiredDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : (Vector2)transform.right;

            float wanderAngle = GetWanderAngle(
                style,
                tuning,
                noiseSeed
            );

            if (smartLoopStage != SmartLoopStage.None ||
                finalSmartExitActive)
            {
                wanderAngle *= 0.38f;
            }

            desiredDirection = RotateVector(
                desiredDirection,
                wanderAngle
            );

            float pulseMinimum = movementProfile != null
                ? Mathf.Min(
                    movementProfile.speedPulseMultiplierRange.x,
                    movementProfile.speedPulseMultiplierRange.y
                )
                : 0.90f;
            float pulseMaximum = movementProfile != null
                ? Mathf.Max(
                    movementProfile.speedPulseMultiplierRange.x,
                    movementProfile.speedPulseMultiplierRange.y
                )
                : 1.10f;
            float pulse = (Mathf.Sin(
                Time.time / Mathf.Max(0.5f, pulseDuration) *
                Mathf.PI * 2f + speedPhase
            ) + 1f) * 0.5f;
            pulse = pulse * pulse * (3f - 2f * pulse);

            float speedMultiplier =
                tuning.speedMultiplier *
                Mathf.Lerp(pulseMinimum, pulseMaximum, pulse) *
                hitMovementMultiplier;

            if (smartLoopStage == SmartLoopStage.OutsideArc ||
                smartLoopStage == SmartLoopStage.Returning)
            {
                speedMultiplier *= 1.28f;
            }

            if (finalSmartExitActive)
            {
                speedMultiplier *= 1.10f;
            }

            Vector2 desiredVelocity = desiredDirection *
                Mathf.Max(0.05f, MoveSpeed) *
                Mathf.Max(0.10f, speedMultiplier);

            float smoothTime = Mathf.Max(
                0.04f,
                (movementProfile != null
                    ? movementProfile.velocitySmoothTime
                    : 0.18f) *
                tuning.smoothTimeMultiplier
            );

            rb2d.velocity = Vector2.SmoothDamp(
                rb2d.velocity,
                desiredVelocity,
                ref velocitySmoothReference,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );

            Vector2 facingDirection = rb2d.velocity.sqrMagnitude > 0.0025f
                ? rb2d.velocity.normalized
                : desiredDirection;

            FaceDirectionSmooth(
                facingDirection,
                GetOrganicTurnSpeed() * tuning.turnMultiplier
            );

            if (Time.time >= nextStuckCheck)
            {
                float travelled = Vector2.Distance(
                    transform.position,
                    lastStuckPosition
                );
                float minimumTravel = movementProfile != null
                    ? Mathf.Max(
                        0.01f,
                        movementProfile.minimumStuckTravelDistance
                    )
                    : 0.10f;

                if (travelled < minimumTravel)
                {
                    Vector3 viewport = GetSmartViewportPosition(
                        transform.position
                    );
                    bool outside = viewport.x < -0.02f ||
                                   viewport.x > 1.02f ||
                                   viewport.y < -0.02f ||
                                   viewport.y > 1.02f;

                    if (outside)
                    {
                        forceSmartReturnToArena = true;
                    }
                    else
                    {
                        ChooseNextSmartMovementTarget(
                            style,
                            tuning,
                            true
                        );
                    }

                    float recoverySpeed = movementProfile != null
                        ? movementProfile.stuckRecoverySpeedMultiplier
                        : 0.85f;

                    rb2d.velocity = desiredDirection *
                        Mathf.Max(0.05f, MoveSpeed) *
                        Mathf.Max(0.25f, recoverySpeed);
                }

                lastStuckPosition = transform.position;
                nextStuckCheck = Time.time + GetStuckCheckInterval();
            }

            UpdateAnimationSpeed();
            yield return null;
        }

        movementCoroutine = null;
    }

    private OrganicSwimTuning GetOrganicSwimTuning(SwimStyle style)
    {
        OrganicSwimTuning tuning = new OrganicSwimTuning
        {
            speedMultiplier = 1f,
            turnMultiplier = 1f,
            smoothTimeMultiplier = 1f,
            wanderMultiplier = 1f,
            waypointDistanceMultiplier = 1f,
            waypointLifetimeMultiplier = 1f,
            loopChanceMultiplier = 1f,
            favorVerticalTravel = false,
            keepInsideArena = false
        };

        if (style == SwimStyle.LaneGlide || style == SwimStyle.Straight)
        {
            tuning.turnMultiplier = 0.78f;
            tuning.wanderMultiplier = 0.55f;
            tuning.waypointDistanceMultiplier = 1.25f;
            tuning.waypointLifetimeMultiplier = 1.18f;
        }
        else if (style == SwimStyle.ArcSweep || style == SwimStyle.Wave)
        {
            tuning.speedMultiplier = 0.96f;
            tuning.turnMultiplier = 0.72f;
            tuning.smoothTimeMultiplier = 1.15f;
            tuning.wanderMultiplier = 1.30f;
            tuning.waypointDistanceMultiplier = 1.18f;
            tuning.waypointLifetimeMultiplier = 1.25f;
        }
        else if (style == SwimStyle.ZigZagBurst)
        {
            tuning.speedMultiplier = 1.18f;
            tuning.turnMultiplier = 1.22f;
            tuning.smoothTimeMultiplier = 0.72f;
            tuning.wanderMultiplier = 1.55f;
            tuning.waypointDistanceMultiplier = 0.78f;
            tuning.waypointLifetimeMultiplier = 0.72f;
            tuning.loopChanceMultiplier = 0.72f;
        }
        else if (style == SwimStyle.SpiralCross ||
                 style == SwimStyle.FigureEight ||
                 style == SwimStyle.BossFigureEight ||
                 style == SwimStyle.OrbitCenter)
        {
            tuning.speedMultiplier = 0.92f;
            tuning.turnMultiplier = 0.88f;
            tuning.smoothTimeMultiplier = 1.12f;
            tuning.wanderMultiplier = 1.20f;
            tuning.waypointDistanceMultiplier = 0.90f;
        }
        else if (style == SwimStyle.VerticalDive)
        {
            tuning.speedMultiplier = 1.08f;
            tuning.turnMultiplier = 0.96f;
            tuning.wanderMultiplier = 0.72f;
            tuning.favorVerticalTravel = true;
        }
        else if (style == SwimStyle.HorizontalRush)
        {
            tuning.speedMultiplier = 1.30f;
            tuning.turnMultiplier = 0.68f;
            tuning.smoothTimeMultiplier = 0.78f;
            tuning.wanderMultiplier = 0.35f;
            tuning.waypointDistanceMultiplier = 1.42f;
            tuning.waypointLifetimeMultiplier = 0.88f;
        }
        else if (style == SwimStyle.Curious)
        {
            tuning.speedMultiplier = 0.82f;
            tuning.turnMultiplier = 0.75f;
            tuning.smoothTimeMultiplier = 1.25f;
            tuning.wanderMultiplier = 1.15f;
            tuning.waypointLifetimeMultiplier = 1.35f;
        }
        else if (style == SwimStyle.CuteDarting ||
                 style == SwimStyle.BossCuteDarting)
        {
            tuning.speedMultiplier = 1.22f;
            tuning.turnMultiplier = 1.18f;
            tuning.smoothTimeMultiplier = 0.75f;
            tuning.wanderMultiplier = 1.55f;
            tuning.waypointDistanceMultiplier = 0.80f;
            tuning.waypointLifetimeMultiplier = 0.72f;
        }
        else if (style == SwimStyle.MiniBossHunter ||
                 style == SwimStyle.BossPatrol ||
                 style == SwimStyle.BossArena)
        {
            tuning.speedMultiplier = 0.86f;
            tuning.turnMultiplier = 0.72f;
            tuning.smoothTimeMultiplier = 1.38f;
            tuning.wanderMultiplier = 0.48f;
            tuning.waypointDistanceMultiplier = 1.22f;
            tuning.waypointLifetimeMultiplier = 1.25f;
            tuning.loopChanceMultiplier = 0.38f;
            tuning.keepInsideArena = true;
        }
        else if (style == SwimStyle.BossOrbit ||
                 style == SwimStyle.MiniBossOrbit)
        {
            tuning.speedMultiplier = 0.78f;
            tuning.turnMultiplier = 0.82f;
            tuning.smoothTimeMultiplier = 1.42f;
            tuning.wanderMultiplier = 0.34f;
            tuning.waypointDistanceMultiplier = 0.92f;
            tuning.loopChanceMultiplier = 0.30f;
            tuning.keepInsideArena = true;
        }
        else if (style == SwimStyle.MiniBossCharge ||
                 style == SwimStyle.BossCharge ||
                 style == SwimStyle.BossDash)
        {
            tuning.speedMultiplier = 1.36f;
            tuning.turnMultiplier = 0.88f;
            tuning.smoothTimeMultiplier = 0.76f;
            tuning.wanderMultiplier = 0.28f;
            tuning.waypointDistanceMultiplier = 1.35f;
            tuning.waypointLifetimeMultiplier = 0.78f;
            tuning.loopChanceMultiplier = 0.30f;
            tuning.keepInsideArena = true;
        }
        else if (style == SwimStyle.CriticalStagger ||
                 style == SwimStyle.BossWoundedConvulsion)
        {
            // Critical health is now an evasive struggle, not a permanent
            // convulsion. Actual convulsion comes only from live fire hits.
            tuning.speedMultiplier = 0.80f;
            tuning.turnMultiplier = 1.12f;
            tuning.smoothTimeMultiplier = 1.08f;
            tuning.wanderMultiplier = 0.92f;
            tuning.waypointDistanceMultiplier = 0.82f;
            tuning.waypointLifetimeMultiplier = 0.80f;
            tuning.loopChanceMultiplier = 0.24f;
            tuning.keepInsideArena = true;
        }

        return tuning;
    }

    private void ChooseNextSmartMovementTarget(
        SwimStyle style,
        OrganicSwimTuning tuning,
        bool immediate
    )
    {
        smartMovementTarget = ChooseWideSmartTarget(style, tuning);

        Vector2 lifetimeRange = movementProfile != null
            ? movementProfile.waypointLifetimeRange
            : new Vector2(3.8f, 7.2f);

        float lifetime = RandomRangeSafe(
            lifetimeRange,
            5f,
            0.8f
        ) * tuning.waypointLifetimeMultiplier;

        smartTargetDeadline = Time.time +
            (immediate ? Mathf.Max(1.2f, lifetime * 0.72f) : lifetime);
    }

    private Vector3 ChooseWideSmartTarget(
        SwimStyle style,
        OrganicSwimTuning tuning
    )
    {
        Vector3 currentViewport = GetSmartViewportPosition(transform.position);
        float innerPadding = tuning.keepInsideArena ? 0.15f : 0.07f;

        if (currentViewport.x < 0f)
        {
            return GetSmartViewportWorldPoint(
                Random.Range(0.62f, 0.94f),
                GetTrafficLaneTargetY(innerPadding, false)
            );
        }

        if (currentViewport.x > 1f)
        {
            return GetSmartViewportWorldPoint(
                Random.Range(0.06f, 0.38f),
                GetTrafficLaneTargetY(innerPadding, false)
            );
        }

        if (IsOrbitStyle(style))
        {
            Vector2 orbitAnchor = new Vector2(
                smartOrbitAnchorX,
                GetTrafficLaneTargetY(innerPadding, false)
            );
            Vector2 fromAnchor = new Vector2(
                currentViewport.x - orbitAnchor.x,
                currentViewport.y - orbitAnchor.y
            );

            if (fromAnchor.sqrMagnitude < 0.008f)
            {
                fromAnchor = Vector2.right * 0.18f;
            }

            float orbitAngle = (id & 1) == 0
                ? Random.Range(62f, 96f)
                : Random.Range(-96f, -62f);
            Vector2 orbitPoint = RotateVector(
                fromAnchor.normalized * Random.Range(0.14f, 0.24f),
                orbitAngle
            );

            return GetSmartViewportWorldPoint(
                Mathf.Clamp(
                    orbitAnchor.x + orbitPoint.x,
                    innerPadding,
                    1f - innerPadding
                ),
                Mathf.Clamp(
                    orbitAnchor.y + orbitPoint.y,
                    innerPadding,
                    1f - innerPadding
                )
            );
        }

        float cameraWidth = GetSmartCameraWorldWidth();
        float minimumDistance = cameraWidth *
            (movementProfile != null
                ? movementProfile.minimumWaypointViewportDistance
                : 0.48f) *
            tuning.waypointDistanceMultiplier;
        Vector3 selected = GetSafeSmartReturnPoint();

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float targetX;
            float targetY;

            if (tuning.favorVerticalTravel)
            {
                targetX = Mathf.Clamp(
                    currentViewport.x + Random.Range(-0.28f, 0.28f),
                    innerPadding,
                    1f - innerPadding
                );

                if (movementProfile != null &&
                    movementProfile.spreadRoutesAcrossViewport &&
                    Mathf.Abs(targetX - 0.5f) < 0.17f)
                {
                    targetX = currentViewport.x < 0.5f
                        ? Mathf.Max(innerPadding, 0.28f)
                        : Mathf.Min(1f - innerPadding, 0.72f);
                }

                targetY = currentViewport.y < 0.5f
                    ? Random.Range(0.68f, 1f - innerPadding)
                    : Random.Range(innerPadding, 0.32f);
                smartTrafficLaneY = targetY;
            }
            else
            {
                targetX = currentViewport.x < 0.5f
                    ? Random.Range(0.66f, 1f - innerPadding)
                    : Random.Range(innerPadding, 0.34f);
                targetY = GetTrafficLaneTargetY(innerPadding, true);
            }

            Vector3 candidate = GetSmartViewportWorldPoint(targetX, targetY);
            selected = candidate;

            if (Vector2.Distance(transform.position, candidate) >=
                minimumDistance)
            {
                break;
            }
        }

        return selected;
    }

    private void InitializeSmartTrafficRoute()
    {
        Vector3 viewport = GetSmartViewportPosition(transform.position);

        if (movementProfile == null ||
            !movementProfile.spreadRoutesAcrossViewport)
        {
            smartTrafficLaneY = Mathf.Clamp(viewport.y, 0.10f, 0.90f);
            smartOrbitAnchorX = 0.5f;
            return;
        }

        float avoidance = Mathf.Clamp(
            movementProfile.centerAvoidanceHalfHeight,
            0.05f,
            0.28f
        );
        bool upperHalf;

        if (viewport.y > 0.5f + avoidance * 0.30f)
        {
            upperHalf = true;
        }
        else if (viewport.y < 0.5f - avoidance * 0.30f)
        {
            upperHalf = false;
        }
        else
        {
            upperHalf = ((id + TargetLifeVersion) & 1) == 0;
        }

        float halfMinimum = upperHalf
            ? 0.5f + avoidance
            : 0.08f;
        float halfMaximum = upperHalf
            ? 0.92f
            : 0.5f - avoidance;
        float outerLane = Mathf.Lerp(halfMinimum, halfMaximum, 0.28f);
        float innerLane = Mathf.Lerp(halfMinimum, halfMaximum, 0.74f);
        float sourceY = Mathf.Clamp(viewport.y, halfMinimum, halfMaximum);

        smartTrafficLaneY = Mathf.Abs(sourceY - outerLane) <=
            Mathf.Abs(sourceY - innerLane)
                ? outerLane
                : innerLane;

        float anchorInset = Mathf.Clamp(
            movementProfile.orbitAnchorViewportInset,
            0.16f,
            0.38f
        );
        smartOrbitAnchorX = viewport.x <= 0.5f
            ? anchorInset
            : 1f - anchorInset;
    }

    private float GetTrafficLaneTargetY(
        float innerPadding,
        bool allowHalfChange
    )
    {
        if (movementProfile == null ||
            !movementProfile.spreadRoutesAcrossViewport)
        {
            return Random.Range(innerPadding, 1f - innerPadding);
        }

        Vector3 viewport = GetSmartViewportPosition(transform.position);
        bool atHorizontalEdge = viewport.x <= 0.20f ||
                                viewport.x >= 0.80f;

        if (allowHalfChange && atHorizontalEdge &&
            Random.value <= Mathf.Clamp01(
                movementProfile.crossCenterLaneChangeChance
            ))
        {
            smartTrafficLaneY = 1f - smartTrafficLaneY;
            smartOrbitAnchorX = 1f - smartOrbitAnchorX;
        }

        float avoidance = Mathf.Clamp(
            movementProfile.centerAvoidanceHalfHeight,
            0.05f,
            0.28f
        );
        bool upperHalf = smartTrafficLaneY >= 0.5f;
        float halfMinimum = upperHalf
            ? Mathf.Max(innerPadding, 0.5f + avoidance)
            : innerPadding;
        float halfMaximum = upperHalf
            ? 1f - innerPadding
            : Mathf.Min(1f - innerPadding, 0.5f - avoidance);

        if (halfMinimum >= halfMaximum)
        {
            return upperHalf
                ? 1f - innerPadding
                : innerPadding;
        }

        float outerLane = Mathf.Lerp(halfMinimum, halfMaximum, 0.28f);
        float innerLane = Mathf.Lerp(halfMinimum, halfMaximum, 0.74f);
        float selectedLane = Random.value < 0.5f
            ? outerLane
            : innerLane;
        float jitter = Mathf.Min(
            movementProfile.trafficLaneJitter,
            Mathf.Max(0f, (halfMaximum - halfMinimum) * 0.18f)
        );

        smartTrafficLaneY = Mathf.Clamp(
            selectedLane + Random.Range(-jitter, jitter),
            halfMinimum,
            halfMaximum
        );
        return smartTrafficLaneY;
    }

    private Vector3 GetSafeSmartReturnPoint()
    {
        Vector3 viewport = GetSmartViewportPosition(transform.position);
        float returnX = viewport.x <= 0.5f ? 0.28f : 0.72f;
        float returnY = GetTrafficLaneTargetY(0.12f, false);

        return GetSmartViewportWorldPoint(returnX, returnY);
    }

    private bool ShouldBeginSmartLoop(OrganicSwimTuning tuning)
    {
        if (!hasEnteredScreen || finalSmartExitActive ||
            smartLoopStage != SmartLoopStage.None ||
            Time.time < nextSmartLoopTime ||
            bossRetreating || currentSwimStyle == SwimStyle.FastTideExit)
        {
            return false;
        }

        int maximumLoops = movementProfile != null
            ? movementProfile.maximumOffscreenLoops
            : 1;

        if (completedSmartLoops >= Mathf.Max(0, maximumLoops))
        {
            return false;
        }

        float chance = movementProfile != null
            ? movementProfile.offscreenLoopChance
            : 0.20f;

        return Random.value <= Mathf.Clamp01(
            chance * tuning.loopChanceMultiplier
        );
    }

    private void BeginSmartLoop()
    {
        Vector3 viewport = GetSmartViewportPosition(transform.position);
        Vector2 heading = rb2d != null && rb2d.velocity.sqrMagnitude > 0.01f
            ? rb2d.velocity.normalized
            : (Vector2)transform.right;

        smartLoopExitSide = Mathf.Abs(heading.x) > 0.18f
            ? (heading.x >= 0f ? 1 : -1)
            : (viewport.x >= 0.5f ? 1 : -1);

        float padding = GetOffscreenPadding();
        smartLoopStage = SmartLoopStage.Exiting;
        smartLoopDeadline = Time.time + GetOffscreenReturnTimeout();
        smartMovementTarget = GetSmartViewportWorldPoint(
            smartLoopExitSide > 0 ? 1f + padding : -padding,
            GetTrafficLaneTargetY(0.10f, false)
        );
        smartTargetDeadline = Time.time + 4f;
    }

    private void AdvanceSmartLoop(
        SwimStyle style,
        OrganicSwimTuning tuning
    )
    {
        float padding = GetOffscreenPadding();
        Vector3 viewport = GetSmartViewportPosition(transform.position);

        if (smartLoopStage == SmartLoopStage.Exiting)
        {
            smartLoopStage = SmartLoopStage.OutsideArc;
            float verticalShift = Random.value < 0.5f ? -0.34f : 0.34f;
            smartMovementTarget = GetSmartViewportWorldPoint(
                smartLoopExitSide > 0
                    ? 1f + padding * 1.85f
                    : -padding * 1.85f,
                Mathf.Clamp(
                    viewport.y + verticalShift,
                    -padding * 0.20f,
                    1f + padding * 0.20f
                )
            );
            smartTargetDeadline = Time.time + 3.2f;
            return;
        }

        if (smartLoopStage == SmartLoopStage.OutsideArc)
        {
            smartLoopStage = SmartLoopStage.Returning;
            smartMovementTarget = GetSmartViewportWorldPoint(
                smartLoopExitSide > 0 ? 0.72f : 0.28f,
                GetTrafficLaneTargetY(0.16f, false)
            );
            smartTargetDeadline = Time.time + 4f;
            return;
        }

        smartLoopStage = SmartLoopStage.None;
        completedSmartLoops++;
        nextSmartLoopTime = Time.time +
            (movementProfile != null
                ? Mathf.Max(1f, movementProfile.loopCooldown)
                : 7f);
        ChooseNextSmartMovementTarget(style, tuning, true);
    }

    private bool ShouldBeginFinalNaturalExit()
    {
        FishTier tier = GetFishTier();

        return Time.time >= smartNaturalExitTime &&
               !persistentTargetBoss &&
               tier != FishTier.MiniBoss &&
               tier != FishTier.MainBoss;
    }

    private void BeginFinalSmartExit()
    {
        Vector3 viewport = GetSmartViewportPosition(transform.position);
        Vector2 heading = rb2d != null && rb2d.velocity.sqrMagnitude > 0.01f
            ? rb2d.velocity.normalized
            : (Vector2)transform.right;
        int side = Mathf.Abs(heading.x) > 0.15f
            ? (heading.x >= 0f ? 1 : -1)
            : (viewport.x >= 0.5f ? 1 : -1);
        float padding = Mathf.Max(0.12f, GetOffscreenPadding() * 1.5f);

        smartLoopStage = SmartLoopStage.None;
        finalSmartExitActive = true;
        smartMovementTarget = GetSmartViewportWorldPoint(
            side > 0 ? 1f + padding : -padding,
            GetTrafficLaneTargetY(0.08f, false)
        );
        smartTargetDeadline = Time.time + 5f;
    }

    private float GetWanderAngle(
        SwimStyle style,
        OrganicSwimTuning tuning,
        float seed
    )
    {
        float strength = movementProfile != null
            ? movementProfile.wanderAngleDegrees
            : 9f;
        float frequency = movementProfile != null
            ? movementProfile.wanderFrequency
            : 0.32f;
        float noise = Mathf.PerlinNoise(
            seed,
            Time.time * Mathf.Max(0.02f, frequency)
        ) * 2f - 1f;
        float slowSweep = Mathf.Sin(
            Time.time * Mathf.Max(0.10f, frequency * 1.7f) + seed
        ) * 0.35f;

        return (noise + slowSweep) *
            Mathf.Max(0f, strength) *
            tuning.wanderMultiplier;
    }

    private float GetOrganicTurnSpeed()
    {
        if (movementProfile == null)
        {
            return currentHealthPhase == FishHealthPhase.Critical
                ? 122f
                : currentHealthPhase == FishHealthPhase.Aggressive
                    ? 102f
                    : 78f;
        }

        if (currentHealthPhase == FishHealthPhase.Critical)
        {
            return movementProfile.criticalTurnDegreesPerSecond;
        }

        if (currentHealthPhase == FishHealthPhase.Aggressive)
        {
            return movementProfile.aggressiveTurnDegreesPerSecond;
        }

        return movementProfile.healthyTurnDegreesPerSecond;
    }

    private float GetStuckCheckInterval()
    {
        return movementProfile != null
            ? Mathf.Max(0.25f, movementProfile.stuckCheckInterval)
            : 1.15f;
    }

    private float GetOffscreenPadding()
    {
        return movementProfile != null
            ? Mathf.Clamp(
                movementProfile.offscreenViewportPadding,
                FishScreenBounds.MinimumViewportPadding,
                0.30f
            )
            : 0.10f;
    }

    private float GetOffscreenReturnTimeout()
    {
        return movementProfile != null
            ? Mathf.Max(2f, movementProfile.offscreenReturnTimeout)
            : 9f;
    }

    private static bool IsOrbitStyle(SwimStyle style)
    {
        return style == SwimStyle.OrbitCenter ||
               style == SwimStyle.BossOrbit ||
               style == SwimStyle.MiniBossOrbit;
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

    private Vector3 GetSmartViewportPosition(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;

        return mainCamera != null
            ? mainCamera.WorldToViewportPoint(worldPosition)
            : new Vector3(0.5f, 0.5f, 0f);
    }

    private Vector3 GetSmartViewportWorldPoint(float x, float y)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return transform.position +
                (Vector3)((Vector2)transform.right * 5f);
        }

        float depth = Mathf.Abs(
            transform.position.z - mainCamera.transform.position.z
        );
        Vector3 world = mainCamera.ViewportToWorldPoint(
            new Vector3(x, y, depth)
        );
        world.z = transform.position.z;
        return world;
    }

    private float GetSmartCameraWorldWidth()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
        {
            return 16f;
        }

        return mainCamera.orthographicSize * 2f * mainCamera.aspect;
    }

    private IEnumerator ArcSweepRoutine()
    {
        float phase = Random.Range(0f, 6.28f);
        while (true)
        {
            Vector2 forward = transform.right * MoveSpeed;
            Vector2 arc = transform.up * Mathf.Sin(Time.time * 2.2f + phase) * MoveSpeed * 0.32f;
            rb2d.velocity = forward + arc; UpdateAnimationSpeed(); yield return null;
        }
    }

    private IEnumerator ZigZagBurstRoutine()
    {
        while (true)
        {
            float sign = Random.value < 0.5f ? -1f : 1f;
            float duration = Random.Range(0.35f, 0.7f);
            float end = Time.time + duration;
            while (Time.time < end) { rb2d.velocity = (Vector2)transform.right * MoveSpeed * 1.35f + (Vector2)transform.up * sign * MoveSpeed * 0.75f; UpdateAnimationSpeed(); yield return null; }
            yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
        }
    }

    private IEnumerator VerticalDiveRoutine()
    {
        Vector2 direction = transform.up * (Random.value < 0.5f ? -1f : 1f);
        while (true) { rb2d.velocity = direction * MoveSpeed * 1.25f + (Vector2)transform.right * MoveSpeed * 0.25f; UpdateAnimationSpeed(); yield return null; }
    }

    private IEnumerator StraightRoutine()
    {
        while (true)
        {
            Vector2 forwardVelocity = transform.right * MoveSpeed;
            Vector2 bobVelocity = transform.up * (Mathf.Sin(Time.time * 3f) * 0.4f);
            rb2d.velocity = forwardVelocity + bobVelocity;
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator WaveRoutine()
    {
        float waveSpeed = Random.Range(2.5f, 4.5f);
        float waveSize = Random.Range(1.8f, 3.2f);

        while (true)
        {
            Vector2 forwardVelocity = transform.right * MoveSpeed;
            Vector2 waveVelocity = transform.up * (Mathf.Sin(Time.time * waveSpeed) * waveSize);
            rb2d.velocity = forwardVelocity + waveVelocity;
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator CuriousRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            MoveSpeed = normalSpeed;
            rb2d.velocity = transform.right * MoveSpeed;
            UpdateAnimationSpeed();
            yield return new WaitForSeconds(Random.Range(1.5f, 3.5f));

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            float turnAmount = Random.Range(30f, 60f) * (Random.value > 0.5f ? 1f : -1f);
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z + turnAmount);
            float progress = 0f;

            while (progress < 1f)
            {
                progress += Time.deltaTime * 2.5f;
                transform.rotation = Quaternion.Lerp(startRotation, targetRotation, progress);
                yield return null;
            }
        }
    }

    private IEnumerator OrbitCenterRoutine()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 orbitCenter = GetSmartViewportWorldPoint(
            smartOrbitAnchorX,
            GetTrafficLaneTargetY(0.14f, false)
        );
        float radius = Vector3.Distance(transform.position, orbitCenter);

        if (radius < 1f)
        {
            radius = 2.25f;
        }

        while (true)
        {
            angle += MoveSpeed * 0.5f * Time.deltaTime;
            Vector3 targetPosition = orbitCenter +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle) * 0.72f,
                    0f
                ) * radius;
            targetPosition.z = transform.position.z;
            Vector3 direction = (targetPosition - transform.position).normalized;

            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator FigureEightRoutine()
    {
        float timer = Random.Range(0f, Mathf.PI * 2f);
        Vector3 routeCenter = GetSmartViewportWorldPoint(
            smartOrbitAnchorX,
            GetTrafficLaneTargetY(0.14f, false)
        );

        while (true)
        {
            timer += Time.deltaTime * Mathf.Max(0.4f, MoveSpeed * 0.2f);

            Vector3 targetPosition = routeCenter + new Vector3(
                Mathf.Sin(timer) * 3.1f,
                Mathf.Sin(timer * 2f) * 1.45f,
                0f
            );
            targetPosition.z = transform.position.z;

            Vector3 direction = (targetPosition - transform.position).normalized;
            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator BossDashRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 target = GetRandomArenaPoint(0.72f, 0.68f);
            Vector3 direction = (target - transform.position).normalized;

            MoveSpeed = normalSpeed * 0.55f;
            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);

            target = GetRandomArenaPoint(0.75f, 0.70f);
            direction = (target - transform.position).normalized;
            FaceDirection(direction);

            MoveSpeed = normalSpeed * 2.2f;
            float dashTimer = 0f;

            while (dashTimer < 0.8f)
            {
                rb2d.velocity = direction * MoveSpeed;
                dashTimer += Time.deltaTime;
                UpdateAnimationSpeed();
                yield return null;
            }

            MoveSpeed = normalSpeed;
        }
    }

    private IEnumerator CuteDartingRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            float randomAngle = Random.Range(0f, 360f);
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, randomAngle);
            float turnTime = 0f;

            while (turnTime < 0.4f)
            {
                turnTime += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, turnTime / 0.4f);
                yield return null;
            }

            MoveSpeed = normalSpeed * 2.8f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.35f);

            MoveSpeed = normalSpeed * 3.2f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.30f);

            MoveSpeed = normalSpeed * 0.5f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.80f);

            MoveSpeed = normalSpeed * 2.7f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.40f);

            MoveSpeed = normalSpeed * 0.3f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(1.20f);
        }
    }

    // New persistent movement for a target boss.
    // It selects safe points inside the camera and never intentionally exits the screen.
    private IEnumerator BossArenaRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 targetPosition = GetRandomArenaPoint(
                bossArenaWidthPercent,
                bossArenaHeightPercent
            );

            while (gameObject.activeInHierarchy &&
                   Vector3.Distance(transform.position, targetPosition) > 0.35f)
            {
                Vector3 direction =
                    (targetPosition - transform.position).normalized;

                FaceDirectionSmooth(
                    direction,
                    bossSmoothTurnDegreesPerSecond
                );

                rb2d.velocity =
                    (Vector2)transform.right *
                    normalSpeed;

                UpdateAnimationSpeed();
                yield return null;
            }

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(Random.Range(0.35f, 0.9f));

            if (Random.value < 0.30f)
            {
                Vector3 dashTarget = GetRandomArenaPoint(
                    Mathf.Min(0.94f, bossArenaWidthPercent + 0.04f),
                    Mathf.Min(0.92f, bossArenaHeightPercent + 0.04f)
                );

                float dashTimer = 0f;

                while (dashTimer < 0.55f)
                {
                    Vector3 dashDirection =
                        (dashTarget - transform.position).normalized;

                    FaceDirectionSmooth(
                        dashDirection,
                        bossSmoothTurnDegreesPerSecond * 1.35f
                    );

                    rb2d.velocity =
                        (Vector2)transform.right *
                        normalSpeed *
                        1.55f;

                    dashTimer += Time.deltaTime;
                    UpdateAnimationSpeed();
                    yield return null;
                }
            }
        }
    }

    private void StartBossBehaviorController()
    {
        StopBossBehaviorController();
        EvaluateHealthMovement();
        bossBehaviorCoroutine = StartCoroutine(BossBehaviorControllerRoutine());
    }

    private void StopBossBehaviorController()
    {
        if (bossBehaviorCoroutine != null)
        {
            StopCoroutine(bossBehaviorCoroutine);
            bossBehaviorCoroutine = null;
        }
    }

    private IEnumerator BossBehaviorControllerRoutine()
    {
        while (gameObject.activeInHierarchy &&
               (GetFishTier() == FishTier.MiniBoss ||
                GetFishTier() == FishTier.MainBoss))
        {
            if (GetFishTier() == FishTier.MainBoss &&
                persistentTargetBoss &&
                bossRetreatScheduled &&
                !bossRetreating &&
                Time.time >= bossRetreatAtTime)
            {
                bossBehaviorCoroutine = null;
                yield return StartCoroutine(BossRetreatRoutine());
                yield break;
            }

            EvaluateHealthMovement();
            yield return new WaitForSeconds(healthEvaluationInterval);
        }

        bossBehaviorCoroutine = null;
    }

    private IEnumerator BossCuteDartingRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 targetPosition = GetRandomArenaPoint(0.68f, 0.62f);
            Vector3 direction = (targetPosition - transform.position).normalized;

            FaceDirection(direction);

            float firstBurstDuration = Random.Range(0.22f, 0.42f);
            float timer = 0f;

            while (timer < firstBurstDuration)
            {
                rb2d.velocity = direction * normalSpeed * 1.75f;
                timer += Time.deltaTime;
                UpdateAnimationSpeed();
                yield return null;
            }

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(Random.Range(0.18f, 0.38f));

            targetPosition = GetRandomArenaPoint(0.70f, 0.64f);
            direction = (targetPosition - transform.position).normalized;
            FaceDirection(direction);

            float secondBurstDuration = Random.Range(0.18f, 0.34f);
            timer = 0f;

            while (timer < secondBurstDuration)
            {
                rb2d.velocity = direction * normalSpeed * 2.15f;
                timer += Time.deltaTime;
                UpdateAnimationSpeed();
                yield return null;
            }

            rb2d.velocity = direction * normalSpeed * 0.35f;
            yield return new WaitForSeconds(Random.Range(0.55f, 1.05f));
        }
    }

    private IEnumerator BossWoundedConvulsionRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 safeTarget = GetRandomArenaPoint(0.56f, 0.52f);
            Vector3 travelDirection =
                (safeTarget - transform.position).normalized;

            float twitchDuration = Random.Range(0.48f, 0.85f);
            float timer = 0f;

            while (timer < twitchDuration)
            {
                timer += Time.deltaTime;

                Vector2 twitch =
                    Random.insideUnitCircle * woundedTwitchStrength;

                rb2d.velocity =
                    (Vector2)travelDirection *
                    normalSpeed *
                    woundedMoveSpeedMultiplier +
                    twitch;

                FaceDirection(travelDirection);

                float twitchAngle =
                    Mathf.Sin(timer * 38f) * woundedTurnAngle;

                transform.Rotate(0f, 0f, twitchAngle);
                UpdateAnimationSpeed();
                yield return null;
            }

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(Random.Range(0.12f, 0.32f));
        }
    }

    private IEnumerator ScriptedParadeRouteRoutine(
        Vector3 routeTarget,
        SwimStyle style,
        float sweepAmplitude,
        float sweepFrequency
    )
    {
        if (rb2d == null)
        {
            movementCoroutine = null;
            yield break;
        }

        Vector3 routeStart = transform.position;
        Vector2 route = routeTarget - routeStart;
        float routeLength = Mathf.Max(0.5f, route.magnitude);
        Vector2 forward = route / routeLength;
        Vector2 perpendicular = new Vector2(-forward.y, forward.x);
        Vector2 velocitySmoothReference = Vector2.zero;
        float routeProgress = 0f;
        float phase = Mathf.Abs(GetInstanceID() % 997) * 0.037f;
        float routeSpeed = Mathf.Max(0.05f, MoveSpeed);
        float styleSpeed = style == SwimStyle.FastTideExit
            ? 1.65f
            : style == SwimStyle.HorizontalRush ||
              style == SwimStyle.ZigZagBurst
                ? 1.12f
                : style == SwimStyle.ArcSweep ||
                  style == SwimStyle.SpiralCross
                    ? 0.96f
                    : 1f;

        while (gameObject.activeInHierarchy && !isDying)
        {
            routeProgress += Time.deltaTime *
                routeSpeed * styleSpeed / routeLength;

            float authoredProgress = Mathf.Clamp01(routeProgress);
            float envelope = Mathf.Sin(authoredProgress * Mathf.PI);
            float sweep = Mathf.Sin(
                authoredProgress * Mathf.PI * 2f * sweepFrequency + phase
            ) * sweepAmplitude * envelope;
            float lookAhead = Mathf.Min(1.18f, routeProgress + 0.10f);
            Vector2 guidePoint = Vector2.LerpUnclamped(
                routeStart,
                routeTarget,
                lookAhead
            ) + perpendicular * sweep;
            Vector2 slotCorrection = Vector2.ClampMagnitude(
                (guidePoint - (Vector2)transform.position) * 2.4f,
                routeSpeed * 0.85f
            );
            Vector2 desiredVelocity =
                forward * routeSpeed * styleSpeed + slotCorrection;

            rb2d.velocity = Vector2.SmoothDamp(
                rb2d.velocity,
                desiredVelocity * hitMovementMultiplier,
                ref velocitySmoothReference,
                style == SwimStyle.ArcSweep ? 0.22f : 0.15f,
                Mathf.Infinity,
                Time.deltaTime
            );

            FaceDirectionSmooth(
                rb2d.velocity.sqrMagnitude > 0.0025f
                    ? rb2d.velocity.normalized
                    : forward,
                GetOrganicTurnSpeed() *
                (style == SwimStyle.ArcSweep ? 0.82f : 1f)
            );

            UpdateAnimationSpeed();

            // Route endpoints should sit beyond the visible edge. This
            // fallback prevents a mis-positioned scene marker from leaving a
            // pooled parade fish alive forever.
            if (routeProgress >= 1.32f)
            {
                gameObject.SetActive(false);
                yield break;
            }

            yield return null;
        }

        movementCoroutine = null;
    }

    private IEnumerator FollowLeaderRoutine()
    {
        Vector2 velocitySmoothReference = Vector2.zero;
        float phase = Mathf.Abs(GetInstanceID() % 997) * 0.071f;
        float speedBias = paradeFollowerMode
            ? 1f
            : 1f + Mathf.Sin(phase * 2.31f) * schoolSpeedVariation;

        while (leaderTransform != null && leaderTransform.gameObject.activeSelf)
        {
            Vector2 leaderVelocity = Vector2.zero;

            if (leaderTransform.TryGetComponent<Rigidbody2D>(
                    out Rigidbody2D leaderBody
                ))
            {
                leaderVelocity = leaderBody.velocity;
            }

            Vector2 leaderForward = leaderVelocity.sqrMagnitude > 0.0025f
                ? leaderVelocity.normalized
                : (Vector2)leaderTransform.right;
            Vector2 leaderPerpendicular = new Vector2(
                -leaderForward.y,
                leaderForward.x
            );
            float slotMotion = paradeFollowerMode
                ? paradeFollowerWobble
                : schoolSlotBreathing;
            float breathe = Mathf.Sin(Time.time * 1.65f + phase) *
                slotMotion;
            float drift = (Mathf.PerlinNoise(
                phase,
                Time.time * 0.28f
            ) - 0.5f) * slotMotion *
                (paradeFollowerMode ? 0.35f : 1f);
            Vector3 targetPosition =
                leaderTransform.position +
                leaderTransform.TransformDirection(followOffset) +
                (Vector3)(leaderPerpendicular * (breathe + drift)) +
                (Vector3)(leaderForward * breathe * 0.22f);
            Vector2 toSlot = targetPosition - transform.position;
            float correction = paradeFollowerMode
                ? paradeFollowerCorrection
                : 2.8f;
            float catchUp = paradeFollowerMode
                ? paradeFollowerCatchUpMultiplier
                : 1.5f;
            Vector2 desiredVelocity =
                leaderVelocity * speedBias +
                Vector2.ClampMagnitude(
                    toSlot * correction,
                    Mathf.Max(1f, MoveSpeed * catchUp)
                );

            if (paradeFollowerMode)
            {
                desiredVelocity = Vector2.ClampMagnitude(
                    desiredVelocity,
                    Mathf.Max(
                        MoveSpeed * paradeFollowerCatchUpMultiplier,
                        leaderVelocity.magnitude *
                        paradeFollowerCatchUpMultiplier
                    )
                );
            }
            float smoothTime = movementProfile != null
                ? Mathf.Max(0.06f, movementProfile.velocitySmoothTime * 0.82f)
                : 0.15f;

            if (rb2d != null)
            {
                rb2d.velocity = Vector2.SmoothDamp(
                    rb2d.velocity,
                    desiredVelocity * hitMovementMultiplier,
                    ref velocitySmoothReference,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );

                Vector2 face = rb2d.velocity.sqrMagnitude > 0.0025f
                    ? rb2d.velocity.normalized
                    : leaderForward;
                float turnBreathing = 1f + Mathf.Sin(
                    Time.time * 0.9f + phase
                ) * (schoolTurnVariation / 90f);

                FaceDirectionSmooth(
                    face,
                    GetOrganicTurnSpeed() * turnBreathing
                );
            }

            UpdateAnimationSpeed();
            yield return null;
        }

        movementCoroutine = null;

        if (gameObject.activeInHierarchy && !isDying)
        {
            Vector3 viewport = GetSmartViewportPosition(transform.position);

            if (hasEnteredScreen &&
                (viewport.x < -0.04f || viewport.x > 1.04f ||
                 viewport.y < -0.04f || viewport.y > 1.04f))
            {
                gameObject.SetActive(false);
                yield break;
            }

            if (paradeControlled && paradeFollowerMode)
            {
                paradeFollowerMode = false;
                SetParadeRoute(
                    paradeExitTarget,
                    SwimStyle.LaneGlide,
                    0f,
                    1f
                );
                yield break;
            }

            SetMovementStyle(SwimStyle.LaneGlide);
        }
    }

    private IEnumerator EscortTargetRoutine(
        EscortRole role,
        float phaseOffset
    )
    {
        Vector2 velocitySmoothReference = Vector2.zero;
        float phase = phaseOffset +
            Mathf.Abs(GetInstanceID() % 997) * 0.017f;
        float orbitDirection = Mathf.Sin(phase * 4.17f) >= 0f ? 1f : -1f;

        while (leaderTransform != null && leaderTransform.gameObject.activeSelf)
        {
            Vector2 leaderVelocity = Vector2.zero;

            if (leaderTransform.TryGetComponent<Rigidbody2D>(
                    out Rigidbody2D leaderBody
                ))
            {
                leaderVelocity = leaderBody.velocity;
            }

            Vector2 leaderForward = leaderVelocity.sqrMagnitude > 0.0025f
                ? leaderVelocity.normalized
                : (Vector2)leaderTransform.right;
            Vector2 leaderPerpendicular = new Vector2(
                -leaderForward.y,
                leaderForward.x
            );
            Vector2 baseOffset = leaderTransform.TransformDirection(
                followOffset
            );
            Vector2 animatedOffset = baseOffset;

            if (role == EscortRole.Orbit)
            {
                animatedOffset = RotateVector(
                    baseOffset,
                    Time.time * escortOrbitDegreesPerSecond *
                    orbitDirection + phase * 20f
                );
            }
            else if (role == EscortRole.Flank)
            {
                float stride = Mathf.Sin(Time.time * 1.25f + phase) *
                    schoolSlotBreathing * 1.35f;
                animatedOffset += leaderForward * stride +
                    leaderPerpendicular * stride * 0.30f;
            }
            else
            {
                float shieldPulse = Mathf.Sin(Time.time * 2.1f + phase) *
                    schoolSlotBreathing * 0.35f;
                animatedOffset += animatedOffset.sqrMagnitude > 0.001f
                    ? animatedOffset.normalized * shieldPulse
                    : Vector2.zero;
            }

            Vector2 targetPosition =
                (Vector2)leaderTransform.position + animatedOffset;
            Vector2 toSlot = targetPosition - (Vector2)transform.position;
            float correction = role == EscortRole.Shield
                ? escortSlotCorrection * 1.25f
                : escortSlotCorrection;
            Vector2 desiredVelocity = leaderVelocity +
                Vector2.ClampMagnitude(
                    toSlot * correction,
                    Mathf.Max(1f, MoveSpeed * 1.45f)
                );

            if (rb2d != null)
            {
                rb2d.velocity = Vector2.SmoothDamp(
                    rb2d.velocity,
                    desiredVelocity * hitMovementMultiplier,
                    ref velocitySmoothReference,
                    role == EscortRole.Shield ? 0.11f : 0.16f,
                    Mathf.Infinity,
                    Time.deltaTime
                );

                FaceDirectionSmooth(
                    rb2d.velocity.sqrMagnitude > 0.0025f
                        ? rb2d.velocity.normalized
                        : leaderForward,
                    GetOrganicTurnSpeed()
                );
            }

            UpdateAnimationSpeed();
            yield return null;
        }

        movementCoroutine = null;

        if (gameObject.activeInHierarchy && !isDying)
        {
            Vector3 viewport = GetSmartViewportPosition(transform.position);

            if (hasEnteredScreen &&
                (viewport.x < -0.05f || viewport.x > 1.05f ||
                 viewport.y < -0.05f || viewport.y > 1.05f))
            {
                gameObject.SetActive(false);
                yield break;
            }

            SetMovementStyle(SwimStyle.LaneGlide);
        }
    }

    private IEnumerator FastTideExitRoutine()
    {
        float exitSpeed = Mathf.Max(MoveSpeed, baseMoveSpeed) * 4f;
        Vector3 exitDirection = transform.right;
        Vector2 smoothReference = Vector2.zero;
        finalSmartExitActive = true;
        smartLoopStage = SmartLoopStage.None;

        while (true)
        {
            rb2d.velocity = Vector2.SmoothDamp(
                rb2d.velocity,
                exitDirection * exitSpeed,
                ref smoothReference,
                0.12f,
                Mathf.Infinity,
                Time.deltaTime
            );
            FaceDirectionSmooth(rb2d.velocity, 160f);
            UpdateAnimationSpeed();
            yield return null;
        }
    }





    private void FishSystem(int bulletId, int activeGunLevel)
    {
        if (Hp > 0f || gameManager == null || deathPresentationResolved)
        {
            return;
        }

        deathPresentationResolved = true;

        // From this point onward every reward/death/cinematic stage must use
        // the immutable lethal-hit owner, not a later transient gun value.
        int rewardBulletId = deathCreditCaptured
            ? deathCreditBulletId
            : bulletId;
        int rewardGunLevel = deathCreditCaptured
            ? Mathf.Max(1, deathCreditGunLevel)
            : Mathf.Max(1, activeGunLevel);

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.MarkDying();
        }

        float shotCost = GetShotBet(rewardBulletId, rewardGunLevel);
        float coinAmount = CalculateDeathReward(shotCost);

        if (cinematicDeathController != null &&
            cinematicDeathController.TryBeginCinematicDeath(
                coinAmount,
                rewardBulletId,
                rewardGunLevel
            ))
        {
            return;
        }

        if (gimmickType != GimmickType.None)
        {
            TriggerGimmickExplosion(rewardBulletId, rewardGunLevel);
        }

        deathRoutine = StartCoroutine(
            StandardDeathSequence(
                coinAmount,
                rewardBulletId,
                rewardGunLevel
            )
        );
    }

    /// <summary>
    /// Gives a reusable cinematic controller exclusive ownership of this
    /// fish until it explicitly resolves reward and returns the pooled object.
    /// </summary>
    public void PrepareForCinematicDeath(bool disableColliders)
    {
        isDying = true;
        StopBossBehaviorController();
        StopReactiveHitMotion(true);
        ClearExternalMovementSlow(true);

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (externalExplosionReactionCoroutine != null)
        {
            StopCoroutine(externalExplosionReactionCoroutine);
            externalExplosionReactionCoroutine = null;
        }

        RestoreRuntimeVisualState();
        StopMovement();

        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        if (disableColliders)
        {
            SetCachedCollidersEnabled(false);
        }
    }

    public void ResolveCinematicDeathReward(
        float coinAmount,
        int bulletId
    )
    {
        ResolveDeathRewardAndPresentation(coinAmount, bulletId);
    }

    public void FinishCinematicDeath(
        float coinAmount,
        int bulletId
    )
    {
        if (!deathRewardGranted)
        {
            ResolveDeathRewardAndPresentation(coinAmount, bulletId);
        }

        deathRoutine = null;

        if (gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called by EpicBossController after the configured custom death
    /// animation finishes. Final reward, reporting, and pooling remain
    /// centralized in FishSystem.
    /// </summary>
    public void CompleteEpicBossDeath(
        int bulletId,
        int activeGunLevel
    )
    {
        if (!gameObject.activeInHierarchy || Hp > 0f)
        {
            return;
        }

        if (deathRewardGranted)
        {
            return;
        }

        deathPresentationResolved = true;
        float shotCost = GetShotBet(bulletId, activeGunLevel);
        float coinAmount = CalculateDeathReward(shotCost);
        ResolveDeathRewardAndPresentation(coinAmount, bulletId);
        gameObject.SetActive(false);
    }

    private IEnumerator StandardDeathSequence(
        float coinAmount,
        int bulletId,
        int activeGunLevel
    )
    {
        isDying = true;
        StopBossBehaviorController();
        StopReactiveHitMotion(true);

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        Vector2 previousVelocity = rb2d != null
            ? rb2d.velocity
            : (Vector2)transform.right * Mathf.Max(0.05f, MoveSpeed);
        StopMovement();

        FishDeathProfile profile = deathProfile;

        if (profile == null || profile.disableCollidersDuringDeath)
        {
            SetCachedCollidersEnabled(false);
        }

        // Stage 0: Net Boom is intentionally invoked first. When the
        // custom-prefab delay is zero, StartCoroutine executes its first
        // pulse on this same frame immediately after Net Boom.
        PlayImmediateDeathNetBoom(profile, bulletId);
        TryScheduleMainBossFrontGunSkill(
            profile,
            bulletId,
            coinAmount,
            MainBossFrontGunSkillDeathTiming.DeathStart
        );

        if (profile != null && profile.customDeathEffect != null)
        {
            StartCoroutine(PlayCustomDeathEffectSequence(profile));
        }

        // v13.18: Additional Death Prefabs are independent from convulsion.
        // DeathStart entries can begin on the first death frame even when
        // cinematic convulsion is completely disabled.
        PlayAdditionalDeathEffects(
            profile,
            DeathAdditionalEffectStartMode.DeathStart
        );

        float cinematicDelay = profile != null
            ? Mathf.Max(0f, profile.cinematicStartDelay)
            : 0f;
        float delayElapsed = 0f;

        while (delayElapsed < cinematicDelay &&
               gameObject.activeInHierarchy)
        {
            delayElapsed += Time.deltaTime;
            yield return null;
        }

        if (!gameObject.activeInHierarchy)
        {
            deathRoutine = null;
            yield break;
        }

        if (profile != null)
        {
            TrySetAnimatorTriggerSafe(profile.deathAnimatorTrigger);
        }

        ApplyDeathAnimatorSpeed(profile, false);

        // CinematicStart is intentionally the same frame as convulsion start
        // when convulsion is enabled, but it also works normally without it.
        PlayAdditionalDeathEffects(
            profile,
            DeathAdditionalEffectStartMode.CinematicStart
        );

        float baseDuration = profile != null &&
                             profile.enableCinematicDeathMotion
            ? Mathf.Max(0.05f, profile.deathDuration)
            : GetDefaultDeathDuration(GetFishTier());
        float rewardBeat = baseDuration *
            (profile != null
                ? Mathf.Clamp01(profile.rewardBeatNormalized)
                : 0.25f);
        bool enableImpactScalePulse = profile == null ||
                                      profile.enableImpactScalePulse;
        float impactScale = profile != null
            ? Mathf.Clamp(profile.impactScaleMultiplier, 0.05f, 5f)
            : 1.10f;
        bool enableDeathZoom = profile == null ||
                               profile.enableDeathZoom;
        float deathZoomEndScale = profile != null
            ? Mathf.Clamp(
                profile.deathZoomEndScaleMultiplier,
                0.05f,
                5f
            )
            : 0.68f;
        float driftDistance = profile != null
            ? Mathf.Max(0f, profile.deathDriftDistance)
            : 0.25f;
        bool enableTumbleRotation = profile == null ||
                                    profile.enableDeathTumbleRotation;
        float tumbleDegrees = enableTumbleRotation
            ? (profile != null
                ? Mathf.Max(0f, profile.deathTumbleDegrees)
                : 16f)
            : 0f;
        float fadeStart = profile != null
            ? Mathf.Clamp01(profile.fadeStartNormalized)
            : 0.55f;
        bool fadeSprites = profile == null ||
                           profile.fadeSpriteRenderers;
        float deathOpacityStart = profile != null
            ? Mathf.Clamp01(profile.deathOpacityStart)
            : 1f;
        float deathOpacityEnd = profile != null
            ? Mathf.Clamp01(profile.deathOpacityEnd)
            : 0f;
        AnimationCurve deathOpacityCurve = profile != null
            ? profile.deathOpacityCurve
            : null;

        bool enableConvulsion = profile != null &&
                                profile.enableDeathConvulsion;
        float convulsionDuration = enableConvulsion
            ? Mathf.Max(0.02f, profile.deathConvulsionDuration)
            : 0f;
        float convulsionPositionStrength = enableConvulsion
            ? Mathf.Max(0f, profile.deathConvulsionPositionStrength)
            : 0f;
        float convulsionRotationDegrees = enableConvulsion
            ? Mathf.Max(0f, profile.deathConvulsionRotationDegrees)
            : 0f;
        bool enableConvulsionScalePulse = enableConvulsion &&
            profile.enableDeathConvulsionScalePulse;
        float convulsionScaleStrength = enableConvulsionScalePulse
            ? Mathf.Clamp(profile.deathConvulsionScaleStrength, 0f, 0.50f)
            : 0f;
        float convulsionFrequency = enableConvulsion
            ? Mathf.Max(0.1f, profile.deathConvulsionFrequency)
            : 0f;

        float convulsionPrefabFinishTime =
            CalculateDeathConvulsionPrefabFinishTime(
                profile,
                convulsionDuration
            );
        float deathPrefabFinishTime =
            CalculateDeathPrefabFinishTime(
                profile,
                convulsionDuration,
                convulsionPrefabFinishTime,
                cinematicDelay
            );

        float zoomStartTime = ResolveDeathLatePhaseStartTime(
            profile != null
                ? profile.deathZoomStartMode
                : DeathLatePhaseStartMode.CinematicTimeline,
            profile != null
                ? Mathf.Clamp01(profile.deathZoomStartNormalized)
                : 0f,
            profile != null
                ? Mathf.Max(0f, profile.deathZoomAdditionalDelay)
                : 0f,
            baseDuration,
            convulsionDuration,
            convulsionPrefabFinishTime,
            deathPrefabFinishTime
        );
        float opacityStartTime = ResolveDeathLatePhaseStartTime(
            profile != null
                ? profile.deathOpacityStartMode
                : DeathLatePhaseStartMode.CinematicTimeline,
            fadeStart,
            profile != null
                ? Mathf.Max(0f, profile.deathOpacityAdditionalDelay)
                : 0f,
            baseDuration,
            convulsionDuration,
            convulsionPrefabFinishTime,
            deathPrefabFinishTime
        );

        float zoomDuration = ResolveDeathLatePhaseDuration(
            profile != null ? profile.deathZoomDuration : 0f,
            zoomStartTime,
            baseDuration,
            0.35f
        );
        float opacityFadeDuration = ResolveDeathLatePhaseDuration(
            profile != null ? profile.deathOpacityFadeDuration : 0f,
            opacityStartTime,
            baseDuration,
            0.45f
        );

        float duration = baseDuration;
        bool extendLatePhases = profile != null &&
                                profile.extendDeathDurationForLatePhases;

        if (extendLatePhases)
        {
            if (enableDeathZoom)
            {
                duration = Mathf.Max(
                    duration,
                    zoomStartTime + zoomDuration
                );
            }

            if (fadeSprites)
            {
                duration = Mathf.Max(
                    duration,
                    opacityStartTime + opacityFadeDuration
                );
            }
        }

        TryScheduleMainBossFrontGunSkill(
            profile,
            bulletId,
            coinAmount,
            MainBossFrontGunSkillDeathTiming.ConvulsionStart
        );

        if (enableConvulsion && profile != null &&
            (profile.playPrefabWithDeathConvulsion ||
             profile.playEarthquakeWithDeathConvulsion))
        {
            StartCoroutine(
                PlayDeathConvulsionPresentation(
                    profile,
                    convulsionDuration
                )
            );
        }

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = defaultLocalScale;
        Vector2 driftDirection = previousVelocity.sqrMagnitude > 0.0025f
            ? previousVelocity.normalized
            : (Vector2)transform.right;
        driftDirection = (
            driftDirection * 0.72f +
            Vector2.up * Random.Range(0.18f, 0.38f)
        ).normalized;
        float tumbleSign = Random.value < 0.5f ? -1f : 1f;
        bool deathShakePlayed = false;
        float elapsed = 0f;

        while (elapsed < duration && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(
                elapsed / Mathf.Max(0.05f, baseDuration)
            );
            float smooth = normalized * normalized *
                (3f - 2f * normalized);
            float impactProgress = Mathf.Clamp01(normalized / 0.24f);
            float impactEnvelope = Mathf.Sin(impactProgress * Mathf.PI);

            float fadeProgress = elapsed <= opacityStartTime
                ? 0f
                : Mathf.Clamp01(
                    (elapsed - opacityStartTime) /
                    Mathf.Max(0.02f, opacityFadeDuration)
                );
            float fadeCurveValue = deathOpacityCurve != null &&
                                   deathOpacityCurve.length > 0
                ? Mathf.Clamp01(deathOpacityCurve.Evaluate(fadeProgress))
                : fadeProgress;
            float visualAlpha = Mathf.Lerp(
                deathOpacityStart,
                deathOpacityEnd,
                fadeCurveValue
            );

            float impactScalePulse = enableImpactScalePulse
                ? 1f + (impactScale - 1f) * impactEnvelope
                : 1f;

            float zoomProgress = elapsed <= zoomStartTime
                ? 0f
                : Mathf.Clamp01(
                    (elapsed - zoomStartTime) /
                    Mathf.Max(0.02f, zoomDuration)
                );
            float zoomSmooth = zoomProgress * zoomProgress *
                (3f - 2f * zoomProgress);
            float deathZoomScale = enableDeathZoom
                ? Mathf.Lerp(1f, deathZoomEndScale, zoomSmooth)
                : 1f;
            float scaleMultiplier =
                impactScalePulse * deathZoomScale;

            Vector3 convulsionOffset = Vector3.zero;
            float convulsionRotation = 0f;
            float convulsionScale = 1f;

            bool convulsionActive =
                enableConvulsion && elapsed < convulsionDuration;
            ApplyDeathAnimatorSpeed(profile, convulsionActive);

            if (convulsionActive)
            {
                float convulsionNormalized = Mathf.Clamp01(
                    elapsed / convulsionDuration
                );
                float envelope = 1f - convulsionNormalized;
                float phase = elapsed * convulsionFrequency *
                    Mathf.PI * 2f;

                convulsionOffset = new Vector3(
                    Mathf.Sin(phase * 1.13f),
                    Mathf.Cos(phase * 0.91f),
                    0f
                ) * convulsionPositionStrength * envelope;
                convulsionRotation =
                    Mathf.Sin(phase * 1.37f) *
                    convulsionRotationDegrees * envelope;
                if (enableConvulsionScalePulse)
                {
                    convulsionScale = Mathf.Max(
                        0.05f,
                        1f + Mathf.Sin(phase * 1.71f) *
                        convulsionScaleStrength * envelope
                    );
                }
            }

            transform.localScale = startScale *
                scaleMultiplier * convulsionScale;
            transform.position = startPosition +
                (Vector3)(driftDirection * driftDistance * smooth) +
                Vector3.up * Mathf.Sin(normalized * Mathf.PI) *
                driftDistance * 0.12f +
                convulsionOffset;
            transform.rotation = startRotation * Quaternion.Euler(
                0f,
                0f,
                tumbleSign * tumbleDegrees * smooth +
                convulsionRotation
            );

            if (fadeSprites)
            {
                SetVisualAlpha(visualAlpha);
            }

            if (!deathRewardGranted && elapsed >= rewardBeat)
            {
                if (!deathShakePlayed && profile != null &&
                    profile.deathCameraShakeStrength > 0f &&
                    profile.deathCameraShakeDuration > 0f &&
                    gameManager != null)
                {
                    deathShakePlayed = true;
                    gameManager.PlayEarthquake(
                        profile.deathCameraShakeDuration,
                        profile.deathCameraShakeStrength
                    );
                }

                ResolveDeathRewardAndPresentation(coinAmount, bulletId);
            }

            yield return null;
        }

        if (!deathRewardGranted)
        {
            ResolveDeathRewardAndPresentation(coinAmount, bulletId);
        }

        // The real fish reward/death result is now fully resolved. Any clone
        // cinematic below is visual-only and cannot grant another reward.
        RestoreAnimatorSpeedToDefault();

        if (postDeathCloneCinematicController != null &&
            postDeathCloneCinematicController.CanRunForCurrentFish())
        {
            if (postDeathCloneCinematicController
                .WaitForExistingDeathPresentation)
            {
                float remainingPresentation = Mathf.Max(
                    0f,
                    deathPrefabFinishTime - elapsed
                );

                while (remainingPresentation > 0f &&
                       gameObject.activeInHierarchy)
                {
                    remainingPresentation -=
                        postDeathCloneCinematicController.UsesUnscaledTime
                            ? Time.unscaledDeltaTime
                            : Time.deltaTime;
                    yield return null;
                }
            }

            float cloneStartDelay = postDeathCloneCinematicController
                .AdditionalStartDelay;

            while (cloneStartDelay > 0f && gameObject.activeInHierarchy)
            {
                cloneStartDelay -=
                    postDeathCloneCinematicController.UsesUnscaledTime
                        ? Time.unscaledDeltaTime
                        : Time.deltaTime;
                yield return null;
            }

            if (gameObject.activeInHierarchy &&
                postDeathCloneCinematicController
                    .TryBeginPostDeathCinematic(
                        bulletId,
                        activeGunLevel,
                        startRotation.eulerAngles.z
                    ))
            {
                while (gameObject.activeInHierarchy &&
                       postDeathCloneCinematicController.IsRunning)
                {
                    yield return null;
                }
            }
        }

        deathRoutine = null;

        if (gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }

    private void TryScheduleMainBossFrontGunSkill(
        FishDeathProfile profile,
        int bulletId,
        float coinAmount,
        MainBossFrontGunSkillDeathTiming timing
    )
    {
        if (profile == null ||
            GetFishTier() != FishTier.MainBoss ||
            !profile.playMainBossSkillInFrontGun ||
            profile.mainBossFrontGunSkillTiming != timing ||
            mainBossFrontGunSkillTriggered)
        {
            return;
        }

        float delay = Mathf.Max(0f, profile.mainBossFrontGunSkillDelay);
        mainBossFrontGunSkillTriggered = true;

        if (delay <= 0f)
        {
            PlayMainBossFrontGunSkillNow(profile, bulletId, coinAmount);
            return;
        }

        StartCoroutine(
            PlayMainBossFrontGunSkillAfterDelay(
                profile,
                bulletId,
                coinAmount,
                delay
            )
        );
    }

    private IEnumerator PlayMainBossFrontGunSkillAfterDelay(
        FishDeathProfile profile,
        int bulletId,
        float coinAmount,
        float delay
    )
    {
        float elapsed = 0f;

        while (elapsed < delay && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!gameObject.activeInHierarchy)
        {
            yield break;
        }

        PlayMainBossFrontGunSkillNow(profile, bulletId, coinAmount);
    }

    private void PlayMainBossFrontGunSkillNow(
        FishDeathProfile profile,
        int bulletId,
        float coinAmount
    )
    {
        if (gameManager == null ||
            gameManager.animatiorManager == null ||
            GetFishTier() != FishTier.MainBoss)
        {
            return;
        }

        int skillIndex = profile != null
            ? profile.mainBossFrontGunSkillIndex
            : -1;
        Vector3 coinStartPosition = transform.position +
            (Vector3)Random.insideUnitCircle * 1.5f;

        float coinStartDelayOverride = profile != null
            ? profile.mainBossFrontGunCoinStartDelayOverride
            : -1f;

        bool playSkillSound = profile != null &&
            profile.playMainBossFrontGunSkillSound;
        int skillSoundIndex = profile != null
            ? profile.mainBossFrontGunSkillSoundIndex
            : -1;
        bool showRewardText = profile != null &&
            profile.showMainBossFrontGunRewardText;
        string rewardTextPrefix = profile != null
            ? profile.mainBossFrontGunRewardTextPrefix
            : "+";
        float rewardTextDelay = profile != null
            ? Mathf.Max(0f, profile.mainBossFrontGunRewardTextDelay)
            : 0f;

        gameManager.animatiorManager.PlayMainBossDeathSkillInFrontGun(
            coinStartPosition,
            bulletId,
            skillIndex,
            coinStartDelayOverride,
            coinAmount,
            playSkillSound,
            skillSoundIndex,
            showRewardText,
            rewardTextPrefix,
            rewardTextDelay
        );
    }

    private void ResolveDeathRewardAndPresentation(
        float coinAmount,
        int bulletId
    )
    {
        if (deathRewardGranted || gameManager == null)
        {
            return;
        }

        deathRewardGranted = true;
        PlayDeathPresentation(coinAmount, bulletId);

        // v23 safety net: custom/alternate death flows can skip the exact
        // configured DeathStart/ConvulsionStart hook. Reward resolution is
        // guaranteed to run exactly once, so a Main Boss front-gun skill that
        // has not fired yet is launched here instead of silently disappearing.
        if (GetFishTier() == FishTier.MainBoss &&
            deathProfile != null &&
            deathProfile.playMainBossSkillInFrontGun &&
            !mainBossFrontGunSkillTriggered)
        {
            mainBossFrontGunSkillTriggered = true;
            PlayMainBossFrontGunSkillNow(
                deathProfile,
                bulletId,
                coinAmount
            );
        }

        if (bulletId == 0)
        {
            gameManager.CalulateTotalCoinWithCoinFish(coinAmount);

            if (gameManager.SoundManager != null)
            {
                gameManager.SoundManager.PlaySoundCoin1();
            }
        }
        else
        {
            gameManager.CalulateNPCCoinFish(coinAmount, bulletId);
        }

        if (persistentTargetBoss &&
            !bossDefeatReported &&
            spawnDirector != null)
        {
            bossDefeatReported = true;
            spawnDirector.NotifyLevelBossDefeated(this);
        }
    }

    private void PlayImmediateDeathNetBoom(
        FishDeathProfile profile,
        int bulletId
    )
    {
        if (profile == null ||
            !profile.playNetBoom ||
            !profile.playNetBoomImmediatelyAtDeathStart)
        {
            return;
        }

        PlayConfiguredDeathNetBoom(profile, bulletId);
    }

    private void PlayConfiguredDeathNetBoom(
        FishDeathProfile profile,
        int bulletId
    )
    {
        if (gameManager == null || gameManager.animatiorManager == null)
        {
            return;
        }

        int effectIndex = profile != null
            ? profile.netBoomEffectIndex
            : 0;
        Vector2 configuredOffset = profile != null
            ? profile.netBoomPositionOffset
            : Vector2.zero;
        float scatterRadius = profile != null
            ? Mathf.Max(0f, profile.netBoomScatterRadius)
            : 1.2f;
        float scale = profile != null
            ? Mathf.Max(0.01f, profile.netBoomScaleMultiplier)
            : 1f;
        Vector3 randomOffset =
            (Vector3)Random.insideUnitCircle * scatterRadius;
        Vector3 position = transform.position +
            (Vector3)configuredOffset + randomOffset;

        gameManager.animatiorManager
            .PlayNetBoomAdvancedAndGetDuration(
                position,
                bulletId,
                id,
                effectIndex,
                new Vector3(scale, scale, 1f)
            );
    }

    private IEnumerator PlayCustomDeathEffectSequence(
        FishDeathProfile profile
    )
    {
        if (profile == null || profile.customDeathEffect == null)
        {
            yield break;
        }

        float startDelay = Mathf.Max(
            0f,
            profile.customDeathEffectStartDelay
        );
        float delayElapsed = 0f;

        while (delayElapsed < startDelay &&
               gameObject.activeInHierarchy)
        {
            delayElapsed += Time.deltaTime;
            yield return null;
        }

        if (!gameObject.activeInHierarchy)
        {
            yield break;
        }

        int pulseCount = profile.enableCustomEffectPulses
            ? Mathf.Clamp(
                profile.customEffectPulseCount,
                1,
                4
            )
            : 1;
        float pulseInterval = Mathf.Max(
            0.02f,
            profile.customEffectPulseInterval
        );

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            PlayCustomDeathEffectPulse(profile);

            if (pulse + 1 >= pulseCount)
            {
                break;
            }

            float intervalElapsed = 0f;

            while (intervalElapsed < pulseInterval &&
                   gameObject.activeInHierarchy)
            {
                intervalElapsed += Time.deltaTime;
                yield return null;
            }

            if (!gameObject.activeInHierarchy)
            {
                yield break;
            }
        }
    }

    private void PlayCustomDeathEffectPulse(FishDeathProfile profile)
    {
        if (profile == null || profile.customDeathEffect == null ||
            gameManager == null || gameManager.animatiorManager == null)
        {
            return;
        }

        Vector3 randomOffset =
            (Vector3)Random.insideUnitCircle *
            Mathf.Max(0f, profile.customDeathEffectScatterRadius);
        Vector3 position = transform.position +
            (Vector3)profile.customDeathEffectPositionOffset +
            randomOffset;
        Vector2 configuredScale = profile.customDeathEffectScale;
        Vector3 scaleMultiplier = new Vector3(
            Mathf.Max(0.01f, configuredScale.x),
            Mathf.Max(0.01f, configuredScale.y),
            1f
        );

        int sortingLayerId = int.MinValue;
        int sortingOrder = int.MinValue;

        if (profile.customDeathEffectUseFishSorting)
        {
            TryGetFishVisualSorting(
                out sortingLayerId,
                out sortingOrder
            );

            if (sortingOrder != int.MinValue)
            {
                sortingOrder +=
                    profile.customDeathEffectSortingOrderOffset;
            }
        }

        gameManager.animatiorManager.PlayPooledEffectAdvanced(
            profile.customDeathEffect,
            position,
            profile.customDeathEffectRotationDegrees,
            scaleMultiplier,
            profile.customDeathEffectVisibleDuration,
            profile.customEffectHideDelay,
            sortingOrder,
            sortingLayerId
        );
    }

    private float ResolveDeathLatePhaseStartTime(
        DeathLatePhaseStartMode mode,
        float cinematicNormalized,
        float additionalDelay,
        float baseDuration,
        float convulsionDuration,
        float convulsionPrefabFinishTime,
        float deathPrefabFinishTime
    )
    {
        float startTime;

        switch (mode)
        {
            case DeathLatePhaseStartMode.AfterConvulsion:
                startTime = Mathf.Max(0f, convulsionDuration);
                break;

            case DeathLatePhaseStartMode.AfterConvulsionPrefabs:
                startTime = Mathf.Max(
                    Mathf.Max(0f, convulsionDuration),
                    Mathf.Max(0f, convulsionPrefabFinishTime)
                );
                break;

            case DeathLatePhaseStartMode.AfterDeathPrefabs:
                startTime = Mathf.Max(
                    Mathf.Max(0f, convulsionDuration),
                    Mathf.Max(0f, deathPrefabFinishTime)
                );
                break;

            case DeathLatePhaseStartMode.CustomDelay:
                startTime = 0f;
                break;

            default:
                startTime =
                    Mathf.Max(0.05f, baseDuration) *
                    Mathf.Clamp01(cinematicNormalized);
                break;
        }

        return Mathf.Max(0f, startTime + Mathf.Max(0f, additionalDelay));
    }

    private static float ResolveDeathLatePhaseDuration(
        float configuredDuration,
        float startTime,
        float baseDuration,
        float fallbackDuration
    )
    {
        if (configuredDuration > 0f)
        {
            return Mathf.Max(0.02f, configuredDuration);
        }

        float remaining = Mathf.Max(0f, baseDuration - startTime);

        return remaining > 0.02f
            ? remaining
            : Mathf.Max(0.02f, fallbackDuration);
    }

    private float CalculateDeathConvulsionPrefabFinishTime(
        FishDeathProfile profile,
        float convulsionDuration
    )
    {
        if (profile == null || !profile.enableDeathConvulsion)
        {
            return 0f;
        }

        float longestFinish = Mathf.Max(0f, convulsionDuration);
        AnimatiorManager manager = gameManager != null
            ? gameManager.animatiorManager
            : null;

        if (profile.playPrefabWithDeathConvulsion &&
            profile.deathConvulsionEffectPrefab != null)
        {
            float delay = ClampConvulsionPresentationDelay(
                profile.deathConvulsionPresentationDelay,
                convulsionDuration
            );
            float visibleDuration = EstimateDeathPresentationDuration(
                manager,
                profile.deathConvulsionEffectPrefab,
                profile.deathConvulsionEffectVisibleDuration
            );
            float hideDelay =
                profile.latePhaseWaitIncludesPrefabHideDelay
                    ? Mathf.Max(
                        0f,
                        profile.deathConvulsionEffectHideDelay
                    )
                    : 0f;

            longestFinish = Mathf.Max(
                longestFinish,
                delay + visibleDuration + hideDelay
            );
        }



        return longestFinish;
    }

    private float CalculateDeathPrefabFinishTime(
        FishDeathProfile profile,
        float convulsionDuration,
        float convulsionPrefabFinishTime,
        float cinematicStartDelay
    )
    {
        float longestFinish = Mathf.Max(
            Mathf.Max(0f, convulsionDuration),
            Mathf.Max(0f, convulsionPrefabFinishTime)
        );

        if (profile == null)
        {
            return longestFinish;
        }

        AnimatiorManager manager = gameManager != null
            ? gameManager.animatiorManager
            : null;

        // Additional Death Prefabs are part of the full death-prefab window
        // even when convulsion is disabled. DeathStart entries are converted
        // into cinematic-relative time because the main death-motion timer
        // begins after cinematicStartDelay.
        if (profile.playAdditionalDeathPrefabs &&
            profile.deathAdditionalEffects != null)
        {
            for (int i = 0; i < profile.deathAdditionalEffects.Length; i++)
            {
                DeathConvulsionEffectEntry entry =
                    profile.deathAdditionalEffects[i];

                if (entry == null || entry.prefab == null)
                {
                    continue;
                }

                float visibleDuration = EstimateDeathPresentationDuration(
                    manager,
                    entry.prefab,
                    entry.visibleDuration
                );
                float hideDelay = profile.latePhaseWaitIncludesPrefabHideDelay
                    ? Mathf.Max(0f, entry.hideDelay)
                    : 0f;
                float finish;

                if (entry.startMode == DeathAdditionalEffectStartMode.DeathStart)
                {
                    float finishFromDeathStart =
                        Mathf.Max(0f, entry.delay) +
                        visibleDuration + hideDelay;
                    finish = Mathf.Max(
                        0f,
                        finishFromDeathStart -
                        Mathf.Max(0f, cinematicStartDelay)
                    );
                }
                else
                {
                    finish = Mathf.Max(0f, entry.delay) +
                        visibleDuration + hideDelay;
                }

                longestFinish = Mathf.Max(longestFinish, finish);
            }
        }

        if (profile.customDeathEffect != null)
        {
            float visibleDuration = EstimateDeathPresentationDuration(
                manager,
                profile.customDeathEffect,
                profile.customDeathEffectVisibleDuration
            );
            int pulseCount = profile.enableCustomEffectPulses
                ? Mathf.Clamp(profile.customEffectPulseCount, 1, 4)
                : 1;
            float pulseInterval = Mathf.Max(
                0.02f,
                profile.customEffectPulseInterval
            );
            float customFinishFromDeathStart =
                Mathf.Max(0f, profile.customDeathEffectStartDelay) +
                Mathf.Max(0, pulseCount - 1) * pulseInterval +
                visibleDuration +
                (profile.latePhaseWaitIncludesPrefabHideDelay
                    ? Mathf.Max(0f, profile.customEffectHideDelay)
                    : 0f);

            float customFinishFromCinematicStart = Mathf.Max(
                0f,
                customFinishFromDeathStart -
                Mathf.Max(0f, cinematicStartDelay)
            );

            longestFinish = Mathf.Max(
                longestFinish,
                customFinishFromCinematicStart
            );
        }

        return longestFinish;
    }

    private static float ClampConvulsionPresentationDelay(
        float configuredDelay,
        float convulsionDuration
    )
    {
        float maximumDelay = Mathf.Max(
            0f,
            convulsionDuration - 0.001f
        );

        return Mathf.Clamp(
            Mathf.Max(0f, configuredDelay),
            0f,
            maximumDelay
        );
    }

    private static float EstimateDeathPresentationDuration(
        AnimatiorManager manager,
        GameObject prefab,
        float configuredDuration
    )
    {
        if (configuredDuration > 0f)
        {
            return configuredDuration;
        }

        if (manager != null)
        {
            return manager.EstimatePooledEffectVisibleDuration(
                prefab,
                configuredDuration
            );
        }

        return 0.1f;
    }

    private IEnumerator PlayDeathConvulsionPresentation(
        FishDeathProfile profile,
        float convulsionDuration
    )
    {
        if (profile == null || !profile.enableDeathConvulsion)
        {
            yield break;
        }



        float maximumDelay = Mathf.Max(
            0f,
            convulsionDuration - 0.001f
        );
        float delay = Mathf.Clamp(
            profile.deathConvulsionPresentationDelay,
            0f,
            maximumDelay
        );
        float elapsed = 0f;

        while (elapsed < delay && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!gameObject.activeInHierarchy)
        {
            yield break;
        }

        if (profile.playPrefabWithDeathConvulsion &&
            profile.deathConvulsionEffectPrefab != null)
        {
            PlayDeathConvulsionEffect(profile);
        }

        if (profile.playEarthquakeWithDeathConvulsion &&
            profile.deathConvulsionEarthquakeStrength > 0f &&
            profile.deathConvulsionEarthquakeDuration > 0f &&
            gameManager != null)
        {
            gameManager.PlayEarthquake(
                profile.deathConvulsionEarthquakeDuration,
                profile.deathConvulsionEarthquakeStrength
            );
        }
    }

    private void PlayDeathConvulsionEffect(
        FishDeathProfile profile
    )
    {
        if (profile == null ||
            profile.deathConvulsionEffectPrefab == null ||
            gameManager == null ||
            gameManager.animatiorManager == null)
        {
            return;
        }

        Vector3 randomOffset =
            (Vector3)Random.insideUnitCircle *
            Mathf.Max(
                0f,
                profile.deathConvulsionEffectScatterRadius
            );
        Vector3 position = transform.position +
            (Vector3)profile.deathConvulsionEffectPositionOffset +
            randomOffset;
        Vector2 configuredScale =
            profile.deathConvulsionEffectScale;
        Vector3 scaleMultiplier = new Vector3(
            Mathf.Max(0.01f, configuredScale.x),
            Mathf.Max(0.01f, configuredScale.y),
            1f
        );

        int sortingLayerId = int.MinValue;
        int sortingOrder = int.MinValue;

        if (profile.deathConvulsionEffectUseFishSorting)
        {
            TryGetFishVisualSorting(
                out sortingLayerId,
                out sortingOrder
            );

            if (sortingOrder != int.MinValue)
            {
                sortingOrder +=
                    profile.deathConvulsionEffectSortingOrderOffset;
            }
        }

        gameManager.animatiorManager.PlayPooledEffectAdvanced(
            profile.deathConvulsionEffectPrefab,
            position,
            profile.deathConvulsionEffectRotationDegrees,
            scaleMultiplier,
            profile.deathConvulsionEffectVisibleDuration,
            profile.deathConvulsionEffectHideDelay,
            sortingOrder,
            sortingLayerId
        );
    }

    private void PlayAdditionalDeathEffects(
        FishDeathProfile profile,
        DeathAdditionalEffectStartMode startMode
    )
    {
        if (profile == null ||
            !profile.playAdditionalDeathPrefabs ||
            profile.deathAdditionalEffects == null)
        {
            return;
        }

        for (int i = 0; i < profile.deathAdditionalEffects.Length; i++)
        {
            DeathConvulsionEffectEntry entry =
                profile.deathAdditionalEffects[i];

            if (entry == null ||
                entry.prefab == null ||
                entry.startMode != startMode)
            {
                continue;
            }

            StartCoroutine(PlayAdditionalDeathEffect(entry));
        }
    }

    private IEnumerator PlayAdditionalDeathEffect(
        DeathConvulsionEffectEntry entry
    )
    {
        if (entry == null || entry.prefab == null)
        {
            yield break;
        }

        float delay = Mathf.Max(0f, entry.delay);
        float elapsed = 0f;

        while (elapsed < delay && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!gameObject.activeInHierarchy)
        {
            yield break;
        }

        PlayAdditionalDeathEffectNow(entry);
    }

    private void PlayAdditionalDeathEffectNow(
        DeathConvulsionEffectEntry entry
    )
    {
        if (entry == null || entry.prefab == null ||
            gameManager == null || gameManager.animatiorManager == null)
        {
            return;
        }

        Vector3 randomWorldOffset =
            (Vector3)Random.insideUnitCircle *
            Mathf.Max(0f, entry.scatterRadius);
        Vector3 anchorPosition = entry.followFishCenter
            ? GetTargetCenterWorld()
            : transform.position;
        Vector3 position = anchorPosition +
            ResolveAdditionalDeathEffectOffset(
                entry,
                randomWorldOffset
            );
        Vector2 configuredScale = entry.scale;
        Vector3 scaleMultiplier = new Vector3(
            Mathf.Max(0.01f, configuredScale.x),
            Mathf.Max(0.01f, configuredScale.y),
            1f
        );

        int sortingLayerId = int.MinValue;
        int sortingOrder = int.MinValue;

        if (entry.useFishSorting)
        {
            TryGetFishVisualSorting(
                out sortingLayerId,
                out sortingOrder
            );

            if (sortingOrder != int.MinValue)
            {
                sortingOrder += entry.sortingOrderOffset;
            }
        }

        GameObject instance =
            gameManager.animatiorManager.PlayPooledEffectAdvanced(
                entry.prefab,
                position,
                entry.rotationDegrees,
                scaleMultiplier,
                entry.visibleDuration,
                entry.hideDelay,
                sortingOrder,
                sortingLayerId
            );

        if (entry.followFishCenter && instance != null)
        {
            PooledEffectToken token =
                instance.GetComponent<PooledEffectToken>();
            int playVersion = token != null ? token.playVersion : -1;

            StartCoroutine(
                FollowAdditionalDeathEffectCenter(
                    instance,
                    playVersion,
                    entry,
                    randomWorldOffset
                )
            );
        }
    }

    private IEnumerator FollowAdditionalDeathEffectCenter(
        GameObject effectInstance,
        int playVersion,
        DeathConvulsionEffectEntry entry,
        Vector3 randomWorldOffset
    )
    {
        while (gameObject.activeInHierarchy &&
               effectInstance != null &&
               effectInstance.activeInHierarchy)
        {
            PooledEffectToken token =
                effectInstance.GetComponent<PooledEffectToken>();

            if (token != null &&
                playVersion >= 0 &&
                token.playVersion != playVersion)
            {
                yield break;
            }

            // v13.19: center tracking and Position Offset work together.
            // FishLocal / FishBodyNormalized offsets are re-resolved every
            // frame, so several rings can stay distributed along a long fish
            // even while the fish gently turns during the death sequence.
            effectInstance.transform.position =
                GetTargetCenterWorld() +
                ResolveAdditionalDeathEffectOffset(
                    entry,
                    randomWorldOffset
                );

            yield return new WaitForEndOfFrame();
        }
    }

    private Vector3 ResolveAdditionalDeathEffectOffset(
        DeathConvulsionEffectEntry entry,
        Vector3 randomWorldOffset
    )
    {
        if (entry == null)
        {
            return randomWorldOffset;
        }

        Vector2 configuredOffset = entry.positionOffset;

        if (entry.offsetSpace == DeathAdditionalEffectOffsetSpace.World)
        {
            return new Vector3(
                configuredOffset.x,
                configuredOffset.y,
                0f
            ) + randomWorldOffset;
        }

        Transform visualTransform = fishSprite != null
            ? fishSprite.transform
            : transform;

        Vector3 localRight = visualTransform.right.normalized;
        Vector3 localUp = visualTransform.up.normalized;

        float offsetX = configuredOffset.x;
        float offsetY = configuredOffset.y;

        if (entry.offsetSpace ==
            DeathAdditionalEffectOffsetSpace.FishBodyNormalized)
        {
            Vector2 halfSize = GetDeathEffectVisualHalfSize();
            offsetX *= halfSize.x;
            offsetY *= halfSize.y;
        }

        return localRight * offsetX +
               localUp * offsetY +
               randomWorldOffset;
    }

    private Vector2 GetDeathEffectVisualHalfSize()
    {
        // Prefer the actual sprite local dimensions so normalized offsets do
        // not change simply because the long fish rotates on screen.
        if (fishSprite != null && fishSprite.sprite != null)
        {
            Vector3 spriteExtents = fishSprite.sprite.bounds.extents;
            Vector3 worldScale = fishSprite.transform.lossyScale;

            return new Vector2(
                Mathf.Max(0.01f,
                    Mathf.Abs(spriteExtents.x * worldScale.x)),
                Mathf.Max(0.01f,
                    Mathf.Abs(spriteExtents.y * worldScale.y))
            );
        }

        // Fallback for collider-driven fish that have no root SpriteRenderer.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        bool hasBounds = false;
        Bounds combinedBounds =
            new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(candidate.bounds);
            }
        }

        if (hasBounds)
        {
            return new Vector2(
                Mathf.Max(0.01f, combinedBounds.extents.x),
                Mathf.Max(0.01f, combinedBounds.extents.y)
            );
        }

        return Vector2.one;
    }

    private void TryGetFishVisualSorting(
        out int sortingLayerId,
        out int sortingOrder
    )
    {
        sortingLayerId = int.MinValue;
        sortingOrder = int.MinValue;

        if (sortingGroup != null)
        {
            sortingLayerId = sortingGroup.sortingLayerID;
            sortingOrder = sortingGroup.sortingOrder;
            return;
        }

        if (fishSprite != null)
        {
            sortingLayerId = fishSprite.sortingLayerID;
            sortingOrder = fishSprite.sortingOrder;
            return;
        }

        Renderer fallbackRenderer =
            GetComponentInChildren<Renderer>(true);

        if (fallbackRenderer != null)
        {
            sortingLayerId = fallbackRenderer.sortingLayerID;
            sortingOrder = fallbackRenderer.sortingOrder;
        }
    }

    private static float GetDefaultDeathDuration(FishTier tier)
    {
        if (tier == FishTier.MainBoss) return 1.25f;
        if (tier == FishTier.MiniBoss) return 0.90f;
        if (tier == FishTier.Special) return 0.68f;
        if (tier == FishTier.Medium) return 0.38f;
        return 0.25f;
    }

    private float GetShotBet(int bulletId, int activeGunLevel)
    {
        float cost;

        if (gameManager.TryGetShotCost(
                bulletId,
                activeGunLevel,
                out cost
            ))
        {
            return cost;
        }

        return 1f;
    }

    private void GetAuthoritativeCombatValues(
        out float health,
        out float resistance,
        out float reward
    )
    {
        bool profileHasCurrentBalance =
            gameplayProfile != null &&
            gameplayProfile.CombatBalanceVersion >=
                FishArcadeBalanceModel.BalanceVersion &&
            gameplayProfile.health > 0f &&
            gameplayProfile.rewardValue > 0f;

        if (profileHasCurrentBalance)
        {
            health = Mathf.Max(1f, gameplayProfile.health);
            resistance = Mathf.Clamp01(gameplayProfile.damageResistance);
            reward = Mathf.Max(0f, gameplayProfile.rewardValue);
            return;
        }

        if (id >= 0 && id <= 45)
        {
            FishArcadeBalanceModel.GetFishCombatPreset(
                id,
                out health,
                out resistance,
                out reward,
                out _,
                out _,
                out _
            );
            return;
        }

        health = Mathf.Max(1f, Hp);
        resistance = gameplayProfile != null
            ? Mathf.Clamp01(gameplayProfile.damageResistance)
            : 0f;
        reward = Mathf.Max(0f, CoinFish);
    }

    private float GetAuthoritativeBaseReward()
    {
        GetAuthoritativeCombatValues(
            out _,
            out _,
            out float reward
        );

        // Preserve intentional runtime reward scaling (level/endless mode)
        // while still replacing the stale serialized default-10 source.
        float runtimeScale = baseCoinFish > 0.0001f
            ? Mathf.Max(0f, CoinFish) / baseCoinFish
            : 1f;

        return Mathf.Max(0f, reward * runtimeScale);
    }

    private float GetAuthoritativeDamageResistance()
    {
        GetAuthoritativeCombatValues(
            out _,
            out float resistance,
            out _
        );
        return Mathf.Clamp01(resistance);
    }

    private float CalculateDeathReward(float shotCost)
    {
        // v23 uses one authoritative reward source. This fixes the common
        // failure where an old/un-generated FishGameplayProfile keeps its
        // ScriptableObject default Reward Value = 10 and every fish pays 10.
        float fishReward = GetAuthoritativeBaseReward();
        FishDeathProfile profile = deathProfile;
        float reward;

        if (forceFixedRewardForCurrentDeath)
        {
            return Mathf.Round(Mathf.Max(0f, fishReward) * 100f) / 100f;
        }

        if (profile == null ||
            profile.rewardCalculationMode ==
            FishRewardCalculationMode.FixedFishCoin)
        {
            reward = fishReward;
        }
        else if (profile.rewardCalculationMode ==
                 FishRewardCalculationMode.LegacyMultiplyByShotCost)
        {
            reward = fishReward * Mathf.Max(0f, shotCost);
        }
        else
        {
            // A capped, gentle increase. Even a very expensive shot cannot
            // add more than Maximum Small Shot Bonus Percent.
            float normalizedShotCost = Mathf.Clamp01(
                Mathf.Max(0f, shotCost) /
                Mathf.Max(1f, fishReward)
            );

            float bonusPercent =
                normalizedShotCost *
                Mathf.Clamp01(
                    profile.maximumSmallShotBonusPercent
                );

            reward = fishReward * (1f + bonusPercent);
        }

        if (profile != null)
        {
            reward *= Mathf.Max(0f, profile.rewardMultiplier);
            reward = Mathf.Max(profile.minimumReward, reward);

            if (profile.maximumReward > 0f)
            {
                reward = Mathf.Min(profile.maximumReward, reward);
            }
        }

        return Mathf.Round(Mathf.Max(0f, reward) * 100f) / 100f;
    }

    private void PlayDeathPresentation(float coinAmount, int bulletId)
    {
        FishTier tier = GetFishTier();
        FishDeathProfile profile = deathProfile;

        RewardTextStyle textStyle = profile != null
            ? profile.rewardTextStyle
            : GetDefaultTextStyle(tier);

        bool playStandardCoins = profile != null
            ? profile.playStandardCoinAnimation
            : tier != FishTier.MainBoss;

        bool playBossCoinBurst = profile != null
            ? profile.playBossCoinBurst
            : tier == FishTier.MainBoss;

        bool playNetBoom = profile != null
            ? profile.playNetBoom
            : tier == FishTier.Medium ||
              tier == FishTier.Special ||
              tier == FishTier.MiniBoss;
        bool playNetBoomAtRewardBeat = playNetBoom &&
            (profile == null ||
             !profile.playNetBoomImmediatelyAtDeathStart);

        bool playBigWin = profile != null
            ? profile.playBigWinSound
            : tier == FishTier.MiniBoss ||
              tier == FishTier.MainBoss;

        bool playMainBossFrontGunSkill =
            tier == FishTier.MainBoss &&
            (profile == null ||
             (profile.playMainBossSkillInFrontGun &&
              profile.mainBossFrontGunSkillTiming ==
                  MainBossFrontGunSkillDeathTiming.RewardBeat));

        int bossCoinBurstIndex = profile != null
            ? profile.bossCoinBurstEffectIndex
            : 0;

        int coinPlayCount = profile != null
            ? Mathf.Clamp(profile.coinAnimationPlayCount, 1, 6)
            : 1;

        float coinInterval = profile != null
            ? Mathf.Max(0f, profile.coinAnimationInterval)
            : 0f;

        float coinSpread = profile != null
            ? Mathf.Max(0f, profile.coinAnimationSpreadRadius)
            : 0f;

        int bigWinSoundIndex = profile != null
            ? profile.bigWinSoundIndex
            : -1;

        string rewardText = "+" + coinAmount.ToString("0.##");
        Vector3 deathPosition = transform.position;

        if (textStyle == RewardTextStyle.Small &&
            gameManager.DisplayTextManagerScript != null)
        {
            gameManager.DisplayTextManagerScript.DisplaySmallText(
                rewardText,
                deathPosition
            );
        }
        else if ((textStyle == RewardTextStyle.Big ||
                  textStyle == RewardTextStyle.Custom) &&
                 gameManager.DisplayTextManagerScript != null)
        {
            gameManager.DisplayTextManagerScript.DisplayBigText(
                rewardText,
                deathPosition
            );
        }

        if (playStandardCoins && gameManager.coinManager != null)
        {
            gameManager.coinManager.PlayCoinAnimations(
                deathPosition,
                coinAmount,
                bulletId,
                coinPlayCount,
                coinInterval,
                coinSpread
            );
        }

        if (playNetBoomAtRewardBeat)
        {
            PlayConfiguredDeathNetBoom(profile, bulletId);
        }

        if (playBossCoinBurst && gameManager.animatiorManager != null)
        {
            gameManager.animatiorManager.PlayMainBossCoinBurst(
                deathPosition,
                bossCoinBurstIndex
            );
        }

        if (playMainBossFrontGunSkill)
        {
            if (profile == null)
            {
                PlayMainBossFrontGunSkillNow(null, bulletId, coinAmount);
            }
            else
            {
                TryScheduleMainBossFrontGunSkill(
                    profile,
                    bulletId,
                    coinAmount,
                    MainBossFrontGunSkillDeathTiming.RewardBeat
                );
            }
        }

        if (playBigWin && gameManager.SoundManager != null)
        {
            gameManager.SoundManager.PlayBigWinSound(
                bigWinSoundIndex
            );
        }

        float certificateChance = profile != null
            ? profile.certificateChance
            : GetDefaultCertificateChance(tier);

        float minimumCertificateWin = profile != null
            ? profile.minimumCertificateWin
            : tier == FishTier.Medium ? 25f : 0f;

        if (coinAmount >= minimumCertificateWin &&
            Random.value <= certificateChance &&
            gameManager.animatiorManager != null)
        {
            gameManager.animatiorManager.PlayCertificateText(
                deathPosition
            );
        }

        if (profile != null &&
            gameManager.animatiorManager != null)
        {
            gameManager.animatiorManager.PlayCenterScreenDeathPrefab(
                profile,
                coinAmount
            );
        }
    }

    private static RewardTextStyle GetDefaultTextStyle(FishTier tier)
    {
        if (tier == FishTier.Small)
        {
            return RewardTextStyle.Small;
        }

        if (tier == FishTier.MainBoss)
        {
            return RewardTextStyle.None;
        }

        return RewardTextStyle.Big;
    }

    private static float GetDefaultCertificateChance(FishTier tier)
    {
        if (tier == FishTier.Medium)
        {
            return 0.08f;
        }

        if (tier == FishTier.Special)
        {
            return 0.35f;
        }

        if (tier == FishTier.MiniBoss)
        {
            return 0.75f;
        }

        if (tier == FishTier.MainBoss)
        {
            return 1f;
        }

        return 0f;
    }

    private void TriggerGimmickExplosion(int bulletId, int activeGunLevel)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, gimmickRadius);
        int chainCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
            {
                continue;
            }

            if (!hit.TryGetComponent<FishScript>(out FishScript victim))
            {
                continue;
            }

            if (gimmickType == GimmickType.BombCrab)
            {
                victim.TakeDamage(victim.fishSprite, gimmickDamage, bulletId, activeGunLevel);
            }
            else if (gimmickType == GimmickType.LightningChain && chainCount < 5)
            {
                victim.TakeDamage(victim.fishSprite, gimmickDamage * 1.5f, bulletId, activeGunLevel);
                chainCount++;
            }
        }
    }

    public FishTier GetFishTier()
    {
        if (gameplayProfile != null) return gameplayProfile.fishTier;
        if (!useArrayIndexTierFallback) return fishTier;
        if (id >= 0 && id <= 11) return FishTier.Small;
        if (id >= 12 && id <= 30) return FishTier.Medium;
        if (id >= 31 && id <= 35) return FishTier.Special;
        if (id >= 36 && id <= 38) return FishTier.MiniBoss;
        if (id >= 39 && id <= 45) return FishTier.MainBoss;
        return fishTier;
    }

    public bool CanSpawnAmbiently()
    {
        FishTier tier = GetFishTier();

        return naturalSpawnMode != NaturalSpawnMode.EventOnly &&
               ambientSpawnWeight > 0f &&
               (tier == FishTier.Small ||
                tier == FishTier.Medium ||
                tier == FishTier.Special);
    }

    public float GetAmbientSpawnWeight()
    {
        if (!CanSpawnAmbiently())
        {
            return 0f;
        }

        return gameplayProfile != null
            ? Mathf.Max(0f, gameplayProfile.spawnWeight)
            : Mathf.Max(0f, ambientSpawnWeight);
    }

    public int GetMaximumSimultaneousCount()
    {
        return Mathf.Clamp(maximumSimultaneousCount, 1, 32);
    }

    public SwimStyle ChooseNaturalSpawnStyle()
    {
        FishTier tier = GetFishTier();

        if (movementProfile == null)
        {
            return GetDefaultMovementForTier(tier, FishHealthPhase.Healthy);
        }

        return movementProfile.Select(
            FishHealthPhase.Healthy,
            currentSwimStyle,
            tier
        );
    }

    public int GetNaturalAmbientGroupSize()
    {
        if (!CanSpawnAmbiently())
        {
            return 1;
        }

        if (gameplayProfile != null)
        {
            if (!gameplayProfile.canJoinSchool ||
                Random.value > ambientGroupChance)
            {
                return 1;
            }

            return Random.Range(
                Mathf.Max(1, gameplayProfile.minimumGroupSize),
                Mathf.Max(
                    gameplayProfile.minimumGroupSize,
                    gameplayProfile.maximumGroupSize
                ) + 1
            );
        }

        if (Random.value > ambientGroupChance)
        {
            return 1;
        }

        switch (naturalSpawnMode)
        {
            case NaturalSpawnMode.Pair:
                return 2;

            case NaturalSpawnMode.SmallSchool:
                return Random.Range(2, 5);

            case NaturalSpawnMode.LargeSchool:
                return Random.Range(4, 8);

            default:
                return 1;
        }
    }

    public float GetFormationHorizontalSpacing(float baseSpacing)
    {
        Vector2 footprint = GetVisualFootprint();
        float measured = footprint.x * 1.08f + formationPadding;
        return Mathf.Max(baseSpacing, measured) * formationHorizontalMultiplier;
    }

    public float GetFormationVerticalSpacing(float baseSpacing)
    {
        Vector2 footprint = GetVisualFootprint();
        float measured = footprint.y * 1.10f + formationPadding;
        return Mathf.Max(baseSpacing, measured) * formationVerticalMultiplier;
    }

    public float GetParadeSafeHorizontalSpacing(
        float visualGapMultiplier,
        float minimumPadding
    )
    {
        Vector2 footprint = GetVisualFootprint();
        float measured = footprint.x * Mathf.Max(1f, visualGapMultiplier) +
            Mathf.Max(0f, minimumPadding);
        return Mathf.Max(0.1f, measured);
    }

    public float GetParadeSafeVerticalSpacing(
        float visualGapMultiplier,
        float minimumPadding
    )
    {
        Vector2 footprint = GetVisualFootprint();
        float measured = footprint.y * Mathf.Max(1f, visualGapMultiplier) +
            Mathf.Max(0f, minimumPadding);
        return Mathf.Max(0.1f, measured);
    }

    private Vector2 GetVisualFootprint()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        float width = 0f;
        float height = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null || renderer.sprite == null ||
                renderer.gameObject.name == "Shadow")
            {
                continue;
            }

            Vector3 spriteSize = renderer.sprite.bounds.size;
            Vector3 scale = renderer.transform.lossyScale;

            width = Mathf.Max(width, spriteSize.x * Mathf.Abs(scale.x));
            height = Mathf.Max(height, spriteSize.y * Mathf.Abs(scale.y));
        }

        return new Vector2(
            Mathf.Max(0.25f, width),
            Mathf.Max(0.20f, height)
        );
    }

    [ContextMenu("Apply Recommended 46-Fish Prefab Defaults")]
    public void ApplyRecommended46FishPrefabDefaults()
    {
        ApplyRecommended46FishPrefabDefaultsForId(id);
    }

    /// <summary>
    /// Applies the complete recommended data preset for one entry in the
    /// 46-fish director array. The editor bulk-setup tool calls this method
    /// with the array index, so a forgotten prefab ID cannot apply Fish 0's
    /// values to several prefabs.
    /// </summary>
    public bool ApplyRecommended46FishPrefabDefaultsForId(
        int recommendedId
    )
    {
        if (recommendedId < 0 || recommendedId > 45)
        {
            Debug.LogError(
                "Cannot apply the 46-fish preset to " + name +
                ": ID must be between 0 and 45, but it is " +
                recommendedId + ".",
                this
            );

            return false;
        }

        float[] speedValues =
        {
            1.24f, 1.18f, 1.12f, 0.98f, 1.20f, 1.06f, 1.10f, 1.30f,
            1.22f, 1.08f, 1.15f, 1.02f,
            0.86f, 1.14f, 0.92f, 0.78f, 0.82f, 0.80f, 0.95f, 0.98f,
            0.88f, 0.76f, 1.06f, 0.74f, 0.70f, 0.84f, 0.82f, 1.14f,
            1.10f, 0.72f, 0.68f,
            0.72f, 0.78f, 0.74f, 0.68f, 0.62f,
            0.64f, 0.60f, 0.54f,
            0.52f, 0.46f, 0.50f, 0.42f,
            0.38f, 0.44f, 0.40f
        };

        int safeId = recommendedId;
        id = safeId;

        // Clean v20 uses the same absolute combat model in both generated
        // profiles and prefab fallback values. This prevents an old prefab
        // HP/reward table from fighting the regenerated profile.
        FishArcadeBalanceModel.GetFishCombatPreset(
            safeId,
            out float cleanHealth,
            out _,
            out float cleanReward,
            out _,
            out _,
            out _
        );
        Hp = cleanHealth;
        CoinFish = cleanReward;
        MoveSpeed = speedValues[safeId];

        fishTier = safeId <= 11
            ? FishTier.Small
            : safeId <= 30
                ? FishTier.Medium
                : safeId <= 35
                    ? FishTier.Special
                    : safeId <= 38
                        ? FishTier.MiniBoss
                        : FishTier.MainBoss;

        useArrayIndexTierFallback = false;
        healthEvaluationInterval = 0.35f;
        formationHorizontalMultiplier = 1f;
        formationVerticalMultiplier = 1f;
        formationPadding = 0.15f;
        maximumSimultaneousCount =
            fishTier == FishTier.Small
                ? 10
                : fishTier == FishTier.Medium
                    ? 5
                    : 1;
        schoolSlotBreathing = fishTier == FishTier.Small
            ? 0.14f
            : fishTier == FishTier.Medium
                ? 0.11f
                : 0.08f;
        schoolSpeedVariation = fishTier == FishTier.Small
            ? 0.065f
            : 0.045f;
        schoolTurnVariation = fishTier == FishTier.Small
            ? 5f
            : 3.5f;
        escortOrbitDegreesPerSecond = fishTier == FishTier.Small
            ? 28f
            : 22f;
        escortSlotCorrection = 1.9f;

        applyAutomaticFishSorting = true;
        fishSortingLayerName = "Fish";
        useSortingGroup = true;
        smallSortingOrder = 100;
        mediumSortingOrder = 400;
        specialSortingOrder = 800;
        miniBossSortingOrder = 1400;
        mainBossSortingOrder = 2000;
        epicBossSortingOrder = 2200;
        normalFishSortingVariation = 40;

        gimmickType = GimmickType.None;
        gimmickRadius = 4f;
        gimmickDamage = 50f;

        woundedMoveSpeedMultiplier = 0.38f;
        woundedTwitchStrength = 0.45f;
        woundedTurnAngle = 11f;

        mainBossPresenceMode = MainBossPresenceMode.RandomPerSpawn;
        timedRetreatChance = 0.60f;
        bossArenaStayDurationRange = new Vector2(28f, 42f);
        bossArenaWidthPercent = 0.84f;
        bossArenaHeightPercent = 0.74f;
        bossSmoothTurnDegreesPerSecond = 48f;
        bossRetreatSpeedMultiplier = 1f;
        bossRetreatOutsideDistance = 3f;

        bool paradeFish =
            safeId == 1 || safeId == 4 || safeId == 11 ||
            safeId == 12 || safeId == 14 || safeId == 20 ||
            safeId == 23 || safeId == 28;

        paradeParticipation = paradeFish
            ? ParadeParticipationMode.AlwaysAllow
            : ParadeParticipationMode.AlwaysBlock;

        paradeSelectionWeight = paradeFish
            ? safeId <= 11 ? 0.8f : 0.45f
            : 0f;

        if (fishTier == FishTier.Small)
        {
            naturalSpawnMode = NaturalSpawnMode.Solo;
            ambientSpawnWeight = 1.35f;
            ambientGroupChance = 0f;

            if (safeId == 5 || safeId == 6)
            {
                naturalSpawnMode = NaturalSpawnMode.Pair;
                ambientGroupChance = 0.22f;
            }
            else if (safeId == 11)
            {
                naturalSpawnMode = NaturalSpawnMode.SmallSchool;
                ambientGroupChance = 0.30f;
            }
        }
        else if (fishTier == FishTier.Medium)
        {
            naturalSpawnMode = NaturalSpawnMode.Solo;
            ambientSpawnWeight = 0.75f;
            ambientGroupChance = 0f;

            if (safeId == 12 || safeId == 20 || safeId == 28)
            {
                naturalSpawnMode = NaturalSpawnMode.SmallSchool;
                ambientGroupChance = 0.35f;
            }
            else if (safeId == 14 || safeId == 23 ||
                     safeId == 25 || safeId == 26)
            {
                naturalSpawnMode = NaturalSpawnMode.Pair;
                ambientGroupChance = 0.28f;
            }

            if (safeId == 14 || safeId == 24 ||
                safeId == 25 || safeId == 26)
            {
                formationHorizontalMultiplier = 1.20f;
                formationVerticalMultiplier = 1.45f;
                formationPadding = 0.20f;
            }

            if (safeId == 29 || safeId == 30)
            {
                ambientSpawnWeight = 0.30f;
                formationHorizontalMultiplier = 1.25f;
                formationVerticalMultiplier = 1.25f;
                formationPadding = 0.22f;
            }
        }
        else
        {
            naturalSpawnMode = NaturalSpawnMode.EventOnly;
            ambientSpawnWeight = 0f;
            ambientGroupChance = 0f;
            paradeParticipation = ParadeParticipationMode.AlwaysBlock;
            paradeSelectionWeight = 0f;
        }

        // Artwork-specific spacing. These overrides are deterministic and
        // complement the automatic SpriteRenderer footprint measurement.
        if (safeId == 3)
        {
            formationHorizontalMultiplier = 1.15f;
            formationVerticalMultiplier = 1.25f;
            formationPadding = 0.18f;
        }
        else if (safeId == 5 || safeId == 6)
        {
            formationHorizontalMultiplier = 1.20f;
            formationVerticalMultiplier = 1.35f;
            formationPadding = 0.20f;
        }
        else if (safeId == 7 || safeId == 13 || safeId == 22)
        {
            formationHorizontalMultiplier = 1.25f;
            formationVerticalMultiplier = 1f;
            formationPadding = 0.18f;
        }
        else if (safeId == 23)
        {
            formationHorizontalMultiplier = 1.20f;
            formationVerticalMultiplier = 1.25f;
            formationPadding = 0.20f;
        }
        else if (safeId == 27)
        {
            formationHorizontalMultiplier = 1.15f;
            formationVerticalMultiplier = 1.10f;
            formationPadding = 0.20f;
        }
        else if (safeId == 38 || safeId == 42 || safeId >= 43)
        {
            formationHorizontalMultiplier = 1.40f;
            formationVerticalMultiplier = 1.55f;
            formationPadding = 0.25f;
        }

        if (safeId >= 36 && safeId <= 38)
        {
            woundedMoveSpeedMultiplier = 0.45f;
            woundedTwitchStrength = 0.22f;
            woundedTurnAngle = 7f;
        }

        if (safeId == 30)
        {
            maximumSimultaneousCount = 1;
            gimmickType = GimmickType.BombCrab;
            gimmickRadius = 2.5f;
            gimmickDamage = 1200f;
        }
        else if (safeId == 34)
        {
            gimmickType = GimmickType.LightningChain;
            gimmickRadius = 3f;
            gimmickDamage = 1500f;
        }

        Debug.Log(
            "Applied 46-fish prefab defaults to " + name +
            " (ID " + safeId + ", " + fishTier + ").",
            this
        );

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        return true;
    }

    public bool CanJoinParade()
    {
        if (paradeParticipation ==
            ParadeParticipationMode.AlwaysAllow)
        {
            return true;
        }

        if (paradeParticipation ==
            ParadeParticipationMode.AlwaysBlock)
        {
            return false;
        }

        FishTier tier = GetFishTier();

        // Auto mode intentionally keeps parade selection to ordinary
        // Small and Medium fish. Special, MiniBoss, MainBoss, and Epic
        // bosses remain independent encounters.
        return tier == FishTier.Small ||
               tier == FishTier.Medium;
    }

    public bool IsExplicitlyAllowedInParade()
    {
        return paradeParticipation ==
               ParadeParticipationMode.AlwaysAllow &&
               paradeSelectionWeight > 0f;
    }

    public float GetParadeSelectionWeight()
    {
        return Mathf.Max(0f, paradeSelectionWeight);
    }

    public FishGameplayProfile GetGameplayProfile()
    {
        return gameplayProfile;
    }

    /// <summary>
    /// Returns the runtime multiplier that converts raw gameplay-profile
    /// speeds into the fish's current effective speed. It includes the
    /// compatibility scale and any level or boss runtime scaling.
    /// </summary>
    public float GetProfessionalRuntimeSpeedMultiplier()
    {
        if (gameplayProfile == null)
        {
            return 1f;
        }

        float reference = Mathf.Max(0.05f, profileReferenceSpeed);
        return Mathf.Clamp(MoveSpeed / reference, 0.05f, 4f);
    }

    public FishRuntimeState GetRuntimeMovementState()
    {
        return professionalMotionAgent != null
            ? professionalMotionAgent.RuntimeState
            : isDying
                ? FishRuntimeState.Dying
                : FishRuntimeState.Swimming;
    }

    public float GetRuntimeSpacingRadius()
    {
        return professionalMotionAgent != null
            ? professionalMotionAgent.SpacingRadius
            : Mathf.Max(0.25f, GetVisualFootprint().magnitude * 0.35f);
    }

    public bool IsFullyOutsidePaddedView()
    {
        if (professionalMotionAgent != null)
        {
            return professionalMotionAgent.IsFullyOutsidePaddedView();
        }

        Camera targetCamera = Camera.main;
        return targetCamera != null && FishScreenBounds.IsFullyOutside(
            targetCamera,
            gameObject,
            Mathf.Max(FishScreenBounds.MinimumViewportPadding, GetOffscreenPadding())
        );
    }

    public void RequestLevelTransitionExit(float randomizedDelay)
    {
        // A cinematic death owns position, rotation, scale, timing, rewards,
        // and final pool return. Tide-change movement must never interrupt it.
        if (isDying || IsCinematicDeathRunning)
        {
            return;
        }

        if (professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            professionalMotionAgent.RequestLevelTransitionExit(randomizedDelay);
            return;
        }

        StartCoroutine(DelayedLegacyTransitionExit(randomizedDelay));
    }

    private IEnumerator DelayedLegacyTransitionExit(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (gameObject.activeInHierarchy && !isDying)
        {
            SetMovementStyle(SwimStyle.FastTideExit);
        }
    }

    /// <summary>
    /// Takes temporary movement authority only when a fish failed to finish
    /// its normal tide-change route. It moves smoothly to a natural screen
    /// edge and pools the fish only after all visual/collider bounds are
    /// outside the padded camera area.
    /// </summary>
    public void ForceLevelTransitionNaturalExit(float speedMultiplier)
    {
        if (!gameObject.activeInHierarchy || isDying ||
            IsCinematicDeathRunning)
        {
            return;
        }

        if (IsFullyOutsidePaddedView())
        {
            gameObject.SetActive(false);
            return;
        }

        forcedLevelTransitionSpeedRequest = Mathf.Max(
            forcedLevelTransitionSpeedRequest,
            Mathf.Max(1f, speedMultiplier)
        );

        if (forcedLevelTransitionExitCoroutine != null)
        {
            return;
        }

        forcedLevelTransitionExitCoroutine = StartCoroutine(
            ForcedLevelTransitionExitRoutine()
        );
    }

    private IEnumerator ForcedLevelTransitionExitRoutine()
    {
        Camera mainCamera = Camera.main;
        FishScreenSide exitSide =
            GetNaturalEmergencyExitSide(mainCamera);
        float lane = GetEmergencyExitLane(
            mainCamera,
            exitSide
        );

        StopBossBehaviorController();
        StopMovement();
        currentSwimStyle = SwimStyle.FastTideExit;
        bossRetreatScheduled = false;

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.SetExternalMovementAuthority(true);
            professionalMotionAgent.StopMotion();
        }

        if (epicBossController != null &&
            epicBossController.IsActive)
        {
            epicBossController.PrepareForForcedNaturalExit();
        }

        RigidbodyConstraints2D originalConstraints =
            rb2d != null
                ? rb2d.constraints
                : RigidbodyConstraints2D.None;

        if (rb2d != null)
        {
            rb2d.constraints &= ~(
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY
            );
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        float turnSpeed = Mathf.Max(
            90f,
            forcedLevelExitTurnDegreesPerSecond
        );

        Vector3 fallbackStart = transform.position;
        float fallbackDistance = 30f;

        while (gameObject.activeInHierarchy)
        {
            bool outside = mainCamera != null
                ? IsFullyOutsidePaddedView()
                : Vector3.Distance(
                    fallbackStart,
                    transform.position
                ) >= fallbackDistance;

            if (outside)
            {
                break;
            }

            float speed = Mathf.Max(
                forcedLevelExitMinimumSpeed,
                Mathf.Max(0.05f, MoveSpeed) *
                Mathf.Max(
                    forcedLevelExitSpeedMultiplier,
                    forcedLevelTransitionSpeedRequest
                )
            );

            Vector3 target;

            if (mainCamera != null)
            {
                Bounds visualBounds =
                    FishScreenBounds.GetCombinedVisualBounds(
                        gameObject
                    );

                float visualRadius = Mathf.Max(
                    0.25f,
                    visualBounds.extents.magnitude
                );

                target = FishScreenBounds.GetEdgePoint(
                    mainCamera,
                    exitSide,
                    lane,
                    Mathf.Max(
                        FishScreenBounds.MinimumViewportPadding,
                        GetOffscreenPadding()
                    ),
                    visualRadius +
                    forcedLevelExitExtraDistance,
                    transform.position.z
                );
            }
            else
            {
                target =
                    fallbackStart +
                    GetEmergencyExitDirection(exitSide) *
                    fallbackDistance;
            }

            Vector3 direction = target - transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction =
                    GetEmergencyExitDirection(exitSide);
            }
            else
            {
                direction.Normalize();
            }

            FaceDirectionSmooth(direction, turnSpeed);

            if (rb2d != null)
            {
                Vector2 nextPosition = Vector2.MoveTowards(
                    rb2d.position,
                    target,
                    speed * Time.fixedDeltaTime
                );

                rb2d.MovePosition(nextPosition);
                rb2d.velocity = Vector2.zero;
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    speed * Time.fixedDeltaTime
                );
            }

            UpdateAnimationSpeed();
            yield return new WaitForFixedUpdate();
        }

        if (rb2d != null)
        {
            rb2d.constraints = originalConstraints;
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        forcedLevelTransitionExitCoroutine = null;
        forcedLevelTransitionSpeedRequest = 1f;

        if (gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }

    public void BeginBossTimeoutWarning(float forcedCenterRouteChance)
    {
        if (epicBossController != null && epicBossController.IsActive)
        {
            epicBossController.BeginTimeoutWarning(forcedCenterRouteChance);
            return;
        }

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.BeginBossWarning(forcedCenterRouteChance);
        }
    }

    public void RequestBossTimeoutEscape(float delay, float speedMultiplier)
    {
        RequestBossTimeoutEscape(delay, speedMultiplier, false, 0f);
    }

    public void RequestBossTimeoutEscape(
        float delay,
        float speedMultiplier,
        bool countAsDefeat,
        float partialRewardPercent
    )
    {
        if (!gameObject.activeInHierarchy || isDying)
        {
            return;
        }

        timeoutCountsAsDefeat = countAsDefeat;
        timeoutPartialRewardPercent = Mathf.Clamp01(partialRewardPercent);
        bossRetreating = true;
        bossRetreatScheduled = false;

        if (epicBossController != null && epicBossController.IsActive)
        {
            epicBossController.BeginTimeoutEscape(speedMultiplier);
            return;
        }

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.RequestBossEscape(delay, speedMultiplier);
            return;
        }

        if (bossBehaviorCoroutine != null)
        {
            StopCoroutine(bossBehaviorCoroutine);
            bossBehaviorCoroutine = null;
        }
        bossBehaviorCoroutine = StartCoroutine(BossRetreatRoutine());
    }

    public void ApplyBossTimeoutHealthDrain(float normalizedMaximumHealthAmount)
    {
        if (!gameObject.activeInHierarchy || isDying || Hp <= 1f)
        {
            return;
        }

        float drain = Mathf.Max(0f, normalizedMaximumHealthAmount) *
            Mathf.Max(1f, runtimeMaxHp);
        Hp = Mathf.Max(1f, Hp - drain);

        if (epicBossController != null && epicBossController.IsActive)
        {
            epicBossController.NotifyHealthChanged(Hp, runtimeMaxHp, 0, 1);
        }
    }

    public void CompleteBossTimeoutEscape()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        // Never resolve a retreat while any part of the boss is still inside
        // the padded camera bounds. This prevents the timeout safety path from
        // making a boss disappear in the center of the screen.
        if (!IsFullyOutsidePaddedView())
        {
            BeginForcedNaturalBossExit(true);
            return;
        }

        ResolveBossTimeoutEscapeNow();
    }

    private void ResolveBossTimeoutEscapeNow()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        forcedNaturalBossExitCoroutine = null;
        bossRetreating = false;
        persistentTargetBoss = false;

        if (timeoutPartialRewardPercent > 0f && gameManager != null)
        {
            gameManager.CalulateTotalCoinWithCoinFish(
                CoinFish * timeoutPartialRewardPercent
            );
        }

        if (spawnDirector != null)
        {
            if (timeoutCountsAsDefeat)
            {
                spawnDirector.NotifyLevelBossDefeated(this);
            }
            else
            {
                spawnDirector.NotifyLevelBossRetreated(this);
            }
        }

        gameObject.SetActive(false);
    }

    public void ForceResolveBossTimeoutEscape()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (IsFullyOutsidePaddedView())
        {
            ResolveBossTimeoutEscapeNow();
            return;
        }

        BeginForcedNaturalBossExit(true);
    }

    public bool IsBoss() { FishTier tier = GetFishTier(); return forceBossReward || tier == FishTier.MiniBoss || tier == FishTier.MainBoss; }

    public static bool IsStyleAllowedForTier(SwimStyle style, FishTier tier)
    {
        if (tier == FishTier.MainBoss) return style == SwimStyle.BossPatrol || style == SwimStyle.BossCharge || style == SwimStyle.BossOrbit || style == SwimStyle.BossFigureEight || style == SwimStyle.CriticalStagger || style == SwimStyle.FastTideExit;
        if (tier == FishTier.MiniBoss) return style == SwimStyle.MiniBossHunter || style == SwimStyle.MiniBossCharge || style == SwimStyle.MiniBossOrbit || style == SwimStyle.ZigZagBurst || style == SwimStyle.CriticalStagger || style == SwimStyle.BossCuteDarting || style == SwimStyle.CuteDarting || style == SwimStyle.FastTideExit;
        return style == SwimStyle.LaneGlide || style == SwimStyle.ArcSweep || style == SwimStyle.ZigZagBurst || style == SwimStyle.SchoolFollow || style == SwimStyle.SpiralCross || style == SwimStyle.VerticalDive || style == SwimStyle.HorizontalRush || style == SwimStyle.FastTideExit;
    }

    public static SwimStyle GetDefaultMovementForTier(FishTier tier, FishHealthPhase phase)
    {
        if (tier == FishTier.MainBoss) return phase == FishHealthPhase.Healthy ? SwimStyle.BossPatrol : phase == FishHealthPhase.Aggressive ? SwimStyle.BossCharge : SwimStyle.CriticalStagger;
        if (tier == FishTier.MiniBoss) return phase == FishHealthPhase.Healthy ? SwimStyle.MiniBossOrbit : phase == FishHealthPhase.Aggressive ? SwimStyle.MiniBossCharge : SwimStyle.CriticalStagger;
        return phase == FishHealthPhase.Healthy ? SwimStyle.LaneGlide : phase == FishHealthPhase.Aggressive ? SwimStyle.ZigZagBurst : SwimStyle.HorizontalRush;
    }

    private void ScheduleNormalBossRetreat()
    {
        bossRetreatScheduled = false;
        bossRetreating = false;

        if (mainBossPresenceMode ==
            MainBossPresenceMode.StayUntilDefeated)
        {
            return;
        }

        if (mainBossPresenceMode ==
                MainBossPresenceMode.RandomPerSpawn &&
            Random.value > timedRetreatChance)
        {
            return;
        }

        float minimumStay = Mathf.Max(
            3f,
            Mathf.Min(
                bossArenaStayDurationRange.x,
                bossArenaStayDurationRange.y
            )
        );

        float maximumStay = Mathf.Max(
            minimumStay,
            Mathf.Max(
                bossArenaStayDurationRange.x,
                bossArenaStayDurationRange.y
            )
        );

        bossRetreatScheduled = true;
        bossRetreatAtTime =
            Time.time +
            Random.Range(minimumStay, maximumStay);
    }

    private IEnumerator BossRetreatRoutine()
    {
        bossRetreating = true;
        StopMovement();

        Camera mainCamera = Camera.main;
        Vector3 cameraCenter =
            mainCamera != null
                ? mainCamera.transform.position
                : Vector3.zero;

        float halfWidth =
            mainCamera != null && mainCamera.orthographic
                ? mainCamera.orthographicSize *
                  mainCamera.aspect
                : 8f;

        float halfHeight =
            mainCamera != null && mainCamera.orthographic
                ? mainCamera.orthographicSize
                : 5f;

        bool exitRight =
            transform.position.x >= cameraCenter.x;

        Vector3 exitTarget = new Vector3(
            cameraCenter.x +
            (exitRight ? 1f : -1f) *
            (halfWidth + bossRetreatOutsideDistance),
            cameraCenter.y +
            Mathf.Clamp(
                transform.position.y - cameraCenter.y,
                -halfHeight * 0.75f,
                halfHeight * 0.75f
            ),
            transform.position.z
        );

        float retreatSpeed =
            Mathf.Max(0.05f, MoveSpeed) *
            bossRetreatSpeedMultiplier;

        float timeout = 10f;
        float elapsed = 0f;

        while (gameObject.activeInHierarchy &&
               elapsed < timeout)
        {
            if (IsFullyOutsidePaddedView())
            {
                bossBehaviorCoroutine = null;
                CompleteBossRetreat();
                yield break;
            }

            Vector3 direction =
                (exitTarget - transform.position).normalized;

            FaceDirectionSmooth(
                direction,
                bossSmoothTurnDegreesPerSecond
            );

            rb2d.velocity =
                (Vector2)transform.right *
                retreatSpeed;

            UpdateAnimationSpeed();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (gameObject.activeInHierarchy)
        {
            // The normal route did not finish in time. Continue with a faster
            // visible exit instead of hiding the boss at its current position.
            bossBehaviorCoroutine = null;
            BeginForcedNaturalBossExit(false);
        }
    }

    private void CompleteBossRetreat()
    {
        if (!bossRetreating || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (!IsFullyOutsidePaddedView())
        {
            BeginForcedNaturalBossExit(false);
            return;
        }

        ResolveBossRetreatNow();
    }

    private void ResolveBossRetreatNow()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        forcedNaturalBossExitCoroutine = null;
        bossRetreating = false;
        persistentTargetBoss = false;

        if (spawnDirector != null)
        {
            spawnDirector.NotifyLevelBossRetreated(this);
        }

        gameObject.SetActive(false);
    }

    private void BeginForcedNaturalBossExit(bool timeoutEscape)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        bossRetreating = true;
        bossRetreatScheduled = false;

        if (forcedNaturalBossExitCoroutine != null)
        {
            return;
        }

        forcedNaturalBossExitCoroutine = StartCoroutine(
            ForcedNaturalBossExitRoutine(timeoutEscape)
        );
    }

    private IEnumerator ForcedNaturalBossExitRoutine(bool timeoutEscape)
    {
        StopBossBehaviorController();
        StopMovement();

        if (professionalMotionAgent != null)
        {
            professionalMotionAgent.SetExternalMovementAuthority(true);
            professionalMotionAgent.StopMotion();
        }

        if (epicBossController != null && epicBossController.IsActive)
        {
            epicBossController.PrepareForForcedNaturalExit();
        }

        Camera mainCamera = Camera.main;
        RigidbodyConstraints2D originalConstraints =
            rb2d != null
                ? rb2d.constraints
                : RigidbodyConstraints2D.None;

        if (rb2d != null)
        {
            rb2d.constraints &= ~(
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY
            );
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        FishScreenSide exitSide = GetNaturalEmergencyExitSide(mainCamera);
        float lane = GetEmergencyExitLane(mainCamera, exitSide);
        float speed = Mathf.Max(
            forcedNaturalExitMinimumSpeed,
            Mathf.Max(0.05f, MoveSpeed) *
            forcedNaturalExitSpeedMultiplier
        );
        float turnSpeed = Mathf.Max(
            bossSmoothTurnDegreesPerSecond,
            forcedNaturalExitTurnDegreesPerSecond
        );

        Vector3 fallbackStart = transform.position;
        float fallbackDistance = Mathf.Max(
            20f,
            bossRetreatOutsideDistance * 4f
        );

        while (gameObject.activeInHierarchy)
        {
            bool outside = mainCamera != null
                ? IsFullyOutsidePaddedView()
                : Vector3.Distance(fallbackStart, transform.position) >=
                  fallbackDistance;

            if (outside)
            {
                break;
            }

            Vector3 target = GetEmergencyExitTarget(
                mainCamera,
                exitSide,
                lane
            );
            Vector3 direction = target - transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = GetEmergencyExitDirection(exitSide);
            }
            else
            {
                direction.Normalize();
            }

            FaceDirectionSmooth(direction, turnSpeed);

            if (rb2d != null)
            {
                Vector2 nextPosition = Vector2.MoveTowards(
                    rb2d.position,
                    target,
                    speed * Time.fixedDeltaTime
                );
                rb2d.MovePosition(nextPosition);
                rb2d.velocity = Vector2.zero;
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    speed * Time.fixedDeltaTime
                );
            }

            UpdateAnimationSpeed();
            yield return new WaitForFixedUpdate();
        }

        if (rb2d != null)
        {
            rb2d.constraints = originalConstraints;
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        forcedNaturalBossExitCoroutine = null;

        if (!gameObject.activeInHierarchy)
        {
            yield break;
        }

        if (timeoutEscape)
        {
            ResolveBossTimeoutEscapeNow();
        }
        else
        {
            ResolveBossRetreatNow();
        }
    }

    private FishScreenSide GetNaturalEmergencyExitSide(Camera targetCamera)
    {
        if (rb2d != null && rb2d.velocity.sqrMagnitude > 0.04f)
        {
            Vector2 velocity = rb2d.velocity;

            if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y))
            {
                return velocity.x >= 0f
                    ? FishScreenSide.Right
                    : FishScreenSide.Left;
            }

            return velocity.y >= 0f
                ? FishScreenSide.Top
                : FishScreenSide.Bottom;
        }

        return targetCamera != null
            ? FishScreenBounds.GetNearestSide(
                targetCamera,
                transform.position
            )
            : FishScreenSide.Right;
    }

    private float GetEmergencyExitLane(
        Camera targetCamera,
        FishScreenSide side
    )
    {
        if (targetCamera == null)
        {
            return 0.5f;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(
            transform.position
        );

        return side == FishScreenSide.Left ||
               side == FishScreenSide.Right
            ? Mathf.Clamp(viewport.y, 0.08f, 0.92f)
            : Mathf.Clamp(viewport.x, 0.08f, 0.92f);
    }

    private Vector3 GetEmergencyExitTarget(
        Camera targetCamera,
        FishScreenSide side,
        float lane
    )
    {
        if (targetCamera == null)
        {
            return transform.position +
                   GetEmergencyExitDirection(side) * 30f;
        }

        Bounds visualBounds =
            FishScreenBounds.GetCombinedVisualBounds(gameObject);
        float visualRadius = Mathf.Max(
            0.5f,
            visualBounds.extents.magnitude
        );

        return FishScreenBounds.GetEdgePoint(
            targetCamera,
            side,
            lane,
            Mathf.Max(
                FishScreenBounds.MinimumViewportPadding,
                GetOffscreenPadding()
            ),
            visualRadius +
            bossRetreatOutsideDistance +
            forcedNaturalExitExtraDistance,
            transform.position.z
        );
    }

    private static Vector3 GetEmergencyExitDirection(
        FishScreenSide side
    )
    {
        switch (side)
        {
            case FishScreenSide.Left:
                return Vector3.left;
            case FishScreenSide.Right:
                return Vector3.right;
            case FishScreenSide.Top:
                return Vector3.up;
            default:
                return Vector3.down;
        }
    }

    private void EvaluateHealthMovement()
    {
        if (epicBossController != null && epicBossController.IsActive)
        {
            return;
        }

        if (runtimeMaxHp <= 0f || Hp <= 0f || isDying) return;
        float ratio = Mathf.Clamp01(Hp / runtimeMaxHp);
        FishHealthPhase phase = ratio > 0.65f ? FishHealthPhase.Healthy : ratio > 0.30f ? FishHealthPhase.Aggressive : FishHealthPhase.Critical;
        FishTier tier = GetFishTier();
        bool phaseChanged = !hasEvaluatedHealthPhase ||
                            phase != currentHealthPhase;

        currentHealthPhase = phase;
        hasEvaluatedHealthPhase = true;
        woundedBossMovementActive = phase == FishHealthPhase.Critical;

        if (gameplayProfile != null &&
            gameplayProfile.ignoreReactiveHitMotion)
        {
            // Huge long-bodied bosses keep their authored route and velocity
            // while being shot. Health can still change normally, but a hit
            // never calls SetMovementStyle -> StopMovement -> route rebuild.
            return;
        }

        if (!phaseChanged && Time.time < nextStyleChangeTime)
        {
            return;
        }

        if (!phaseChanged && movementProfile != null &&
            Random.value > movementProfile.styleChangeChanceAfterHold)
        {
            nextStyleChangeTime = Time.time + RandomRangeSafe(
                movementProfile.styleHoldDurationRange,
                6f,
                0.5f
            );
            return;
        }

        SwimStyle selected = movementProfile != null ? movementProfile.Select(phase, currentSwimStyle, tier) : GetDefaultMovementForTier(tier, phase);

        nextStyleChangeTime = Time.time + RandomRangeSafe(
            movementProfile != null
                ? movementProfile.styleHoldDurationRange
                : new Vector2(4.5f, 8f),
            6f,
            0.5f
        );

        if (selected != currentSwimStyle)
        {
            SetMovementStyle(selected);
        }
    }

    private void UpdateAnimationSpeed()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        float referenceSpeed = Mathf.Max(0.05f, MoveSpeed);
        float actualSpeed = rb2d != null
            ? rb2d.velocity.magnitude
            : referenceSpeed;
        float normalizedSpeed = actualSpeed / referenceSpeed;

        animator.speed = Mathf.Clamp(
            0.58f + normalizedSpeed * 0.52f,
            0.52f,
            2.35f
        );
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FaceDirectionSmooth(
        Vector3 direction,
        float degreesPerSecond
    )
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float targetAngle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        float currentAngle =
            transform.eulerAngles.z;

        float smoothAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            Mathf.Max(1f, degreesPerSecond) *
            Time.deltaTime
        );

        transform.rotation =
            Quaternion.Euler(0f, 0f, smoothAngle);
    }

    private Vector3 GetRandomArenaPoint(float widthPercent, float heightPercent)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
        {
            return new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-3f, 3f),
                transform.position.z
            );
        }

        float viewportHalfWidth = Mathf.Clamp01(widthPercent) * 0.5f;
        float viewportHalfHeight = Mathf.Clamp01(heightPercent) * 0.5f;
        float minimumX = 0.5f - viewportHalfWidth;
        float maximumX = 0.5f + viewportHalfWidth;
        float minimumY = 0.5f - viewportHalfHeight;
        float maximumY = 0.5f + viewportHalfHeight;
        float avoidance = movementProfile != null
            ? Mathf.Clamp(
                movementProfile.centerAvoidanceHalfHeight,
                0.05f,
                0.28f
            )
            : 0.14f;
        float selectedX = 0.28f;
        float selectedY = smartTrafficLaneY;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            selectedX = Random.Range(minimumX, maximumX);
            selectedY = Random.Range(minimumY, maximumY);

            bool insideCenterPocket =
                Mathf.Abs(selectedX - 0.5f) < 0.17f &&
                Mathf.Abs(selectedY - 0.5f) < avoidance;

            if (movementProfile == null ||
                !movementProfile.spreadRoutesAcrossViewport ||
                !insideCenterPocket)
            {
                break;
            }
        }

        if (movementProfile != null &&
            movementProfile.spreadRoutesAcrossViewport &&
            Mathf.Abs(selectedX - 0.5f) < 0.17f &&
            Mathf.Abs(selectedY - 0.5f) < avoidance)
        {
            selectedY = GetTrafficLaneTargetY(minimumY, false);
            selectedY = Mathf.Clamp(selectedY, minimumY, maximumY);
        }

        return GetSmartViewportWorldPoint(selectedX, selectedY);
    }

    private void OnBecameVisible()
    {
        hasEnteredScreen = true;

        if (smartLoopStage == SmartLoopStage.Returning)
        {
            smartLoopStage = SmartLoopStage.None;
            completedSmartLoops++;
            nextSmartLoopTime = Time.time +
                (movementProfile != null
                    ? Mathf.Max(1f, movementProfile.loopCooldown)
                    : 7f);
        }

        if (gameManager != null && !gameManager.fishInScreenList.Contains(this))
        {
            gameManager.fishInScreenList.Add(this);
        }
    }

    private void OnBecameInvisible()
    {
        if (useProfessionalMotionAgent &&
            professionalMotionAgent != null &&
            professionalMotionAgent.HasMovementAuthority)
        {
            // Renderer callbacks occur as soon as the first pixel leaves the
            // view. FishMotionAgent waits for the full sprite, shadow, effects,
            // and the required 5% padded boundary before pooling or returning.
            return;
        }

        if (!hasEnteredScreen || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (gameManager != null &&
            gameManager.fishInScreenList.Contains(this))
        {
            gameManager.fishInScreenList.Remove(this);
        }

        if (isDying)
        {
            return;
        }

        if (bossRetreating)
        {
            CompleteBossRetreat();
            return;
        }

        if (smartLoopStage != SmartLoopStage.None)
        {
            // The organic steering routine owns this intentional excursion
            // and will bring the same pooled fish back without teleporting.
            return;
        }

        if (leaderTransform != null &&
            leaderTransform.gameObject.activeInHierarchy)
        {
            // A school member remains pooled while its leader performs a
            // broad return arc, preventing the formation from breaking.
            return;
        }

        if (persistentTargetBoss)
        {
            if (epicBossController != null && epicBossController.IsActive)
            {
                epicBossController.RequestReturnToArena();
                return;
            }

            forceSmartReturnToArena = true;
            return;
        }

        if (forcedLevelTransitionExitCoroutine != null ||
            currentSwimStyle == SwimStyle.FastTideExit)
        {
            // OnBecameInvisible means only that the renderer stopped being
            // visible. Keep moving until the sprite, shadow, effects, and
            // colliders are fully beyond the padded screen.
            return;
        }

        if (!finalSmartExitActive)
        {
            forceSmartReturnToArena = true;
            return;
        }

        if (IsFullyOutsidePaddedView())
        {
            gameObject.SetActive(false);
        }
    }

    private static float RandomRangeSafe(
        Vector2 range,
        float fallback,
        float minimum
    )
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float high = Mathf.Max(low, Mathf.Max(range.x, range.y));

        if (high <= 0f)
        {
            return Mathf.Max(minimum, fallback);
        }

        return Random.Range(low, high);
    }
}
