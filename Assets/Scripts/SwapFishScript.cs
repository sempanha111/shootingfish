using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    private const int CurrentBossSupportSettingsVersion = 1;
    private const int CurrentLevelTransitionSettingsVersion = 2;

    private readonly List<GameObject> activeTargetBosses =
        new List<GameObject>();
    private bool openingParadePresentationRunning;
    private readonly HashSet<FishScript> activeParadeFish =
        new HashSet<FishScript>();
    private ParadeState paradeState = ParadeState.Completed;
    private bool paradeSessionRunning;
    private bool paradeSpawningComplete;
    private bool paradeHasBeenVisible;
    private bool paradeTimeoutExitRequested;
    private float paradeSessionStartedAt;
    private float paradeTimeoutExitRequestedAt;
    private string activeParadeReason = string.Empty;
    private Coroutine periodicParadeCoroutine;
    public enum GameLevel
    {
        Level1_Beginner,
        Level2_School,
        Level3_Circle,
        Level4_Cross,
        Level5_Festival,
        Level6_BossOcean
    }

    public enum LevelPhase
    {
        Opening,
        FeatureBuildUp,
        PreBossParade,
        BossWarning,
        BossBattle,
        Recovery,
        TideChange
    }

    public enum ParadeState
    {
        Preparing,
        Spawning,
        Entering,
        Active,
        Exiting,
        Completed
    }

    private enum ParadeEntrySide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private enum OrganizedParadePattern
    {
        StraightLine,
        Diagonal,
        Wave,
        Circle,
        Arrow,
        Grid,
        Diamond,
        TwinColumn,
        LeaderAndFollowers,
        VerticalLine,
        VFormation,
        Spiral,
        MixedFormation,
        ProtectedCenter
    }

    private enum BossGuardFormationStyle
    {
        HalfCircleArcs,
        RoyalEscort,
        Arrowhead,
        Random
    }

    public enum BossArrivalEventType
    {
        TidalSurge, Earthquake, LightningStorm, GoldenFrenzy, AbyssGate, TreasureCurrent
    }

    public enum BossTimeoutBehavior
    {
        EscapeAndProgress,
        GraduallyReduceHealthThenEscape,
        FinalVisiblePhaseThenEscape,
        ForceCompleteAfterGrace
    }

    public enum BossTimeoutOutcome
    {
        Escaped,
        DefeatedWithoutFullReward,
        PartialReward
    }

    [System.Serializable]
    public struct WeightedBossArrivalEvent
    {
        public BossArrivalEventType eventType;
        [Min(0f)] public float weight;
    }

    [Header("Background Settings")]
    public SpriteRenderer backgroundRenderer;
    public Sprite[] levelBackgrounds;

    [Header("Setup References")]
    public GameObject[] Fish;
    public Transform FishSpawnPosition;
    public Transform LeftPos;
    public Transform RightPos;
    public Transform TopPos;
    public Transform BottomPos;

    [Header("Professional Fish Spawn Director")]
    [SerializeField] private FishSpawnDirectorService spawnDirectorService;

    [Header("Per-Level Population Mix")]
    [SerializeField] private FishLevelPopulationProfile[] levelPopulationProfiles;

    [Header("Gimmick Prefabs")]
    public GameObject BombCrabPrefab;
    public GameObject LightningChainPrefab;

    [Header("Array Index Rules - Match Your Inspector")]
    [SerializeField] private int bigBossStartIndex = 39;
    [SerializeField] private int bigBossEndIndex = 45;

    [SerializeField] private int smallFishStartIndex = 0;
    [SerializeField] private int smallFishEndIndex = 11;

    [SerializeField] private int mediumFishStartIndex = 12;
    [SerializeField] private int mediumFishEndIndex = 30;

    [SerializeField] private int miniBossStartIndex = 36;
    [SerializeField] private int miniBossEndIndex = 38;

    [SerializeField] private int extraNormalFishStartIndex = 46;

    [SerializeField]
    private int[] level1BossTargets =
{
    39
};

    [SerializeField]
    private int[] level2BossTargets =
    {
    39,
    40
};

    [SerializeField]
    private int[] level3BossTargets =
    {
    40,
    41
};

    [SerializeField]
    private int[] level4BossTargets =
    {
    41,
    42
};

    [SerializeField]
    private int[] level5BossTargets =
    {
    42,
    43
};

    [SerializeField]
    private int[] level6BossTargets =
    {
    43,
    44,
    45
};



    [Header("Multiple Boss Battle")]
    [SerializeField, Range(1, 3)]
    private int globalMaximumSimultaneousBosses = 2;

    [Tooltip("Maximum simultaneous bosses for Level 1-6.")]
    [SerializeField]
    private int[] maximumSimultaneousBossesPerLevel =
    {
    1,  // Level 1
    1,  // Level 2
    1,  // Level 3
    2,  // Level 4
    2,  // Level 5
    2   // Level 6
};

    [Tooltip("Chance that the next batch contains two bosses.")]
    [SerializeField]
    private float[] doubleBossChancePerLevel =
    {
    0.00f,
    0.00f,
    0.05f,
    0.12f,
    0.20f,
    0.30f
};

    [Tooltip("Chance that the next batch contains three bosses.")]
    [SerializeField]
    private float[] tripleBossChancePerLevel =
    {
    0.00f,
    0.00f,
    0.00f,
    0.00f,
    0.00f,
    0.00f
};

    [Tooltip(
        "Reduces each boss HP slightly when several bosses appear together."
    )]
    [SerializeField, Range(0f, 0.25f)]
    private float simultaneousBossHpReductionPerExtraBoss = 0.12f;

    [SerializeField]
    private float simultaneousBossVerticalSpacing = 3.2f;

    [Header("Professional Level Flow")]
    [Tooltip("Opening + feature time before the first target boss.")]
    [SerializeField] private float level1Warmup = 10f;
    [SerializeField] private float warmupIncreasePerLevel = 2f;

    [Tooltip("Boss warning gets a little longer every level.")]
    [SerializeField] private float bossWarningBaseDelay = 1.4f;
    [SerializeField] private float bossWarningIncreasePerLevel = 0.15f;

    [Header("Main Boss Presence / Occupancy")]
    [Tooltip("Prioritizes a real target Main Boss early in each level instead of stacking many small/special crowd waves first.")]
    [SerializeField] private bool prioritizeMainBossPresence = true;

    [Tooltip("Maximum opening+feature warm-up before the first Main Boss sequence starts.")]
    [SerializeField, Min(1f)] private float firstMainBossWarmupCap = 4.5f;

    [Tooltip("Maximum pre-boss parade waves before the first Main Boss. One keeps the entrance exciting without making players wait through repeated small fish.")]
    [SerializeField, Range(0, 2)] private int firstMainBossParadeWaveCap = 1;

    [Tooltip("Maximum wait after the first Main Boss parade wave.")]
    [SerializeField, Min(0f)] private float firstMainBossParadeGapCap = 0.65f;

    [Tooltip("Skip mini-boss heralds before the first real Main Boss so the Main Boss becomes visible early.")]
    [SerializeField] private bool skipMiniBossBeforeFirstMainBoss = true;

    [Tooltip("Maximum warning pause before the first Main Boss.")]
    [SerializeField, Min(0f)] private float firstMainBossWarningDelayCap = 0.75f;

    [Tooltip("Maximum arrival-event cinematic duration before the first Main Boss.")]
    [SerializeField, Min(0f)] private float firstMainBossArrivalEventDurationCap = 0.75f;

    [Tooltip("Do not spawn the large small/special rush immediately before the first Main Boss. Later bosses can still use it.")]
    [SerializeField] private bool skipFirstMainBossRushFish = true;

    [Tooltip("Do not stack crowd-build-up waves before the first Main Boss. Later boss batches keep the richer crowd presentation.")]
    [SerializeField] private bool skipFirstMainBossCrowdBuildUp = true;

    [Tooltip("Used only on levels with two target bosses.")]
    [SerializeField] private float betweenTargetBossBaseDelay = 2f;
    [SerializeField] private float betweenTargetBossIncreasePerLevel = 0.3f;

    [SerializeField] private float postBossRecoveryBaseDelay = 0.8f;
    [SerializeField] private float postBossRecoveryIncreasePerLevel = 0.1f;

    [Header("Spawn Pacing")]
    [SerializeField] private float ambientDelayMin = 0.45f;
    [SerializeField] private float ambientDelayMax = 0.85f;
    [SerializeField] private float paradeDelayMin = 9f;
    [SerializeField] private float paradeDelayMax = 14f;
    [SerializeField] private int maxActiveFishBase = 26;
    [SerializeField] private int maxActiveFishPerLevel = 2;

    [Header("Natural School Parade Formation")]
    [Tooltip(
        "Uses one species, a clear front leader, aligned followers, and " +
        "size-aware separation for Level 1 and large schooling parades."
    )]
    [SerializeField]
    private bool useNaturalSchoolFormation = true;

    [SerializeField, Range(6, 24)]
    private int naturalSchoolMinimumLargeCount = 8;

    [SerializeField, Range(2, 6)]
    private int naturalSchoolMaximumFishPerRow = 4;

    [SerializeField, Range(1f, 1.8f)]
    private float naturalSchoolLongitudinalSpacingMultiplier = 1.18f;

    [SerializeField, Range(1f, 1.8f)]
    private float naturalSchoolLateralSpacingMultiplier = 1.12f;

    [SerializeField, Range(0f, 0.8f)]
    private float naturalSchoolRowStagger = 0.35f;

    [Header("Organized Parade Pattern Library")]
    [SerializeField] private bool useOrganizedParadePatterns = true;

    [SerializeField] private OrganizedParadePattern[] beginnerParadePatterns =
    {
        OrganizedParadePattern.StraightLine,
        OrganizedParadePattern.Diagonal,
        OrganizedParadePattern.LeaderAndFollowers
    };

    [SerializeField] private OrganizedParadePattern[] largeParadePatterns =
    {
        OrganizedParadePattern.Arrow,
        OrganizedParadePattern.Grid,
        OrganizedParadePattern.Wave,
        OrganizedParadePattern.Circle,
        OrganizedParadePattern.LeaderAndFollowers
    };

    [SerializeField, Range(0.9f, 1.8f)]
    private float organizedParadeSpacingMultiplier = 1.18f;

    [SerializeField, Range(0f, 0.20f)]
    private float organizedParadeSlotBreathing = 0.035f;

    [Header("Balanced Ambient Screen Traffic")]
    [Tooltip("Spawns ambient groups through separated upper/lower lanes instead of sending many diagonal paths through the exact screen center.")]
    [SerializeField, Range(2, 6)] private int ambientTrafficLaneCount = 4;
    [SerializeField, Range(0.10f, 0.22f)] private float ambientViewportPadding = 0.10f;
    [SerializeField, Range(0.05f, 0.25f)] private float ambientCenterAvoidanceHalfHeight = 0.14f;
    [SerializeField, Range(0f, 0.08f)] private float ambientLaneJitter = 0.025f;
    [SerializeField, Range(0f, 0.12f)] private float ambientTargetVerticalDrift = 0.045f;
    [Tooltip("Scales the current level's vertical-route chance for ordinary ambient schools.")]
    [SerializeField, Range(0f, 1f)] private float ambientVerticalRouteMultiplier = 0.42f;

    [Header("Minimum Screen Population")]
    [Tooltip("Ambient fish are refilled toward this floor so players always have targets.")]
    [SerializeField] private int minimumActiveFishBase = 18;
    [SerializeField] private int minimumActiveFishPerLevel = 1;
    [SerializeField, Range(1, 6)] private int minimumPopulationRefillBurst = 3;
    [SerializeField, Min(0.05f)] private float minimumPopulationRefillInterval = 0.22f;

    [Header("Level Opening Standard Parade")]
    [Tooltip("Plays a clean organized parade immediately after every new level begins, including Level 1.")]
    [SerializeField] private bool playStandardParadeAtEveryLevelStart = true;

    [SerializeField, Min(0f)]
    private float levelOpeningParadeDelay = 0.20f;

    [SerializeField, Range(1, 3)]
    private int levelOpeningParadeWaveCount = 1;

    [SerializeField, Min(0.1f)]
    private float levelOpeningParadeWaveGap = 0.65f;

    [SerializeField, Range(5, 24)]
    private int levelOpeningParadeFishCountBase = 10;

    [SerializeField, Range(0, 4)]
    private int levelOpeningParadeFishPerLevel = 1;

    [SerializeField, Range(0.45f, 1.4f)]
    private float levelOpeningParadeSpeedMultiplier = 0.88f;

    [SerializeField] private OrganizedParadePattern[] levelOpeningParadePatterns =
    {
        OrganizedParadePattern.StraightLine,
        OrganizedParadePattern.Arrow,
        OrganizedParadePattern.VFormation,
        OrganizedParadePattern.Grid,
        OrganizedParadePattern.Wave
    };

    [Header("Clean Parade State / Route Priority")]
    [Tooltip("When OFF, ordinary fish keep spawning and swimming normally while a Parade is active.")]
    [SerializeField] private bool pauseNormalSpawningDuringParade = false;

    [Tooltip("When OFF, ordinary fish already on screen are never forced to fast-exit just because a Parade starts.")]
    [SerializeField] private bool clearNormalFishBeforeParade = false;

    [Tooltip("Keeps the opening Parade as the first visible event, then allows ordinary fish to mix in as soon as the Parade becomes visible.")]
    [SerializeField] private bool allowAmbientAfterOpeningParadeVisible = true;

    [Tooltip("Extra temporary capacity reserved so a Parade can still spawn when ordinary fish already fill the normal population target.")]
    [SerializeField, Range(0, 16)] private int mixedParadeExtraFishCapacity = 8;

    [SerializeField, Range(6, 28)] private int maximumParadeFishCount = 18;
    [SerializeField, Min(4f)] private float paradeCompletionTimeout = 18f;
    [SerializeField, Min(0.25f)] private float paradeAmbientClearTimeout = 1.8f;
    [SerializeField, Min(1f)] private float paradeAmbientExitSpeedMultiplier = 1.8f;
    [SerializeField, Min(0.25f)] private float paradeTimeoutCleanupGrace = 2.5f;
    [SerializeField, Range(0f, 0.1f)] private float paradeVisibilityPadding = 0.01f;

    [Header("Parade Size-Aware Spacing")]
    [Tooltip("Visual footprint multiplier. 1.01 means about a 1% gap between Small fish before minimum padding.")]
    [SerializeField, Range(1f, 1.30f)] private float smallParadeVisualGap = 1.01f;
    [SerializeField, Range(1f, 1.35f)] private float mediumParadeVisualGap = 1.02f;
    [SerializeField, Range(1f, 1.45f)] private float largeParadeVisualGap = 1.05f;
    [SerializeField, Range(1f, 1.60f)] private float specialParadeVisualGap = 1.08f;
    [SerializeField, Range(0f, 0.30f)] private float paradeMinimumGapPadding = 0.01f;

    [Header("Stable Parade Leader / Followers")]
    [SerializeField, Min(0.2f)] private float paradeWorldSpeed = 2.35f;
    [SerializeField, Min(0f)] private float paradeWorldSpeedPerLevel = 0.04f;
    [SerializeField, Min(0.5f)] private float paradeFollowerCorrection = 3.8f;
    [SerializeField, Range(1f, 3f)] private float paradeFollowerCatchUpMultiplier = 1.65f;
    [SerializeField, Range(0f, 0.15f)] private float paradeFollowerSlotWobble = 0.035f;

    [Header("Standalone Feature Parade - No Boss Required")]
    [Tooltip("Allows a showcase parade during Feature Build Up even when no boss is entering.")]
    [SerializeField] private bool enableStandaloneFeatureParade = true;

    [SerializeField, Range(0f, 1f)]
    private float standaloneFeatureParadeChance = 0.72f;

    [SerializeField, Min(0f)]
    private float standaloneFeatureParadeDelay = 0.45f;

    [SerializeField, Range(1, 3)]
    private int standaloneFeatureParadeWaveCount = 1;

    [SerializeField, Min(0.1f)]
    private float standaloneFeatureParadeWaveGap = 0.75f;

    [SerializeField, Range(6, 28)]
    private int standaloneFeatureParadeFishCountBase = 12;

    [SerializeField, Range(0, 4)]
    private int standaloneFeatureParadeFishPerLevel = 1;

    [Tooltip("Chance that a no-boss showcase becomes a valuable special-fish royal escort convoy.")]
    [SerializeField, Range(0f, 1f)]
    private float standaloneRoyalEscortChance = 0.45f;

    [SerializeField] private OrganizedParadePattern[] standaloneFeatureParadePatterns =
    {
        OrganizedParadePattern.Diamond,
        OrganizedParadePattern.TwinColumn,
        OrganizedParadePattern.Arrow,
        OrganizedParadePattern.Circle,
        OrganizedParadePattern.Wave
    };

    [Header("Pre-Boss Royal Parade")]
    [SerializeField, Range(1, 3)]
    private int earlyLevelParadeWaves = 2;

    [SerializeField, Range(1, 3)]
    private int advancedLevelParadeWaves = 2;

    [SerializeField]
    private float preBossParadeBaseGap = 2.8f;

    [SerializeField]
    private float preBossParadeGapPerLevel = 0.20f;

    [SerializeField]
    private float preBossFinalPauseBase = 0.50f;

    [SerializeField]
    private float preBossFinalPausePerLevel = 0.05f;

    [SerializeField, Range(0f, 1f)]
    private float preBossMiniBossBaseChance = 0.12f;

    [SerializeField, Range(0f, 1f)]
    private float preBossMiniBossChancePerLevel = 0.05f;

    [SerializeField]
    private float preBossMiniBossLeadBaseDelay = 4.5f;

    [SerializeField]
    private float preBossMiniBossLeadPerLevel = 0.55f;

    [SerializeField]
    private float preBossSpaceWaitTimeout = 6f;

    [Header("Concurrent Parade Groups")]
    [SerializeField] private int preBossExtraFishCapacity = 8;

    [SerializeField] private int paradeFishPerGroupBase = 4;
    [SerializeField] private int paradeFishPerGroupPerLevel = 0;
    [SerializeField] private int paradeFishPerGroupMaximum = 5;

    [SerializeField] private float paradeLaneSpacing = 2.3f;
    [SerializeField] private float paradeFishHorizontalGap = 1.15f;

    [SerializeField] private float paradeSpeedMultiplierBase = 1.05f;
    [SerializeField] private float paradeSpeedMultiplierPerLevel = 0.015f;

    [Header("Four-Direction Pre-Boss Parade")]
    [Tooltip("Chance that a parade group enters from Top or Bottom. Left/Right is used otherwise.")]
    [SerializeField, Range(0f, 1f)]
    private float verticalPreBossParadeChance = 0.30f;

    [Tooltip("Pre-boss parade speed relative to each fish prefab speed.")]
    [SerializeField, Range(0.35f, 1f)]
    private float preBossParadeSpeedBase = 0.68f;

    [SerializeField, Range(0f, 0.08f)]
    private float preBossParadeSpeedPerLevel = 0.015f;

    [Header("Six-Level Signature Parade Direction")]
    [Tooltip("Each level owns one recognizable formation family instead of randomly reusing every pattern.")]
    [SerializeField] private bool useDistinctLevelParadeSignatures = true;

    [Tooltip("Top/Bottom route probability for Level 1 through Level 6.")]
    [SerializeField]
    private float[] verticalRouteChancePerLevel =
    {
        0.08f, 0.18f, 0.32f, 0.58f, 0.44f, 0.52f
    };

    [SerializeField, Range(0f, 1.2f)]
    private float paradeRouteSweepAmplitude = 0.34f;

    [SerializeField, Range(0.25f, 2.5f)]
    private float paradeRouteSweepFrequency = 0.85f;

    [SerializeField, Range(2, 8)]
    private int specialEscortGuardCount = 5;

    [SerializeField, Range(0.8f, 4f)]
    private float specialEscortInnerRadius = 1.65f;

    [Header("Manual Parade Selection")]
    [Tooltip(
        "When enabled, ordinary parade fish are selected only from prefabs " +
        "whose FishScript Parade Participation is Always Allow. FishTier is ignored."
    )]
    [SerializeField] private bool useManualParadeSelection = true;

    [Tooltip(
        "When manual selection has no valid Always Allow prefab, use the old " +
        "Small/Medium tier selection instead of cancelling the parade."
    )]
    [SerializeField] private bool fallbackToTierSelection = false;

    [Header("High Boss / Epic Boss Guard Rules")]
    [Tooltip("When enabled, mini bosses and early main bosses enter without body guards. Only high-index or Epic bosses receive the royal guard parade.")]
    [SerializeField] private bool bodyGuardsOnlyForHighBosses = true;

    [Tooltip("First Fish array index treated as a high boss for royal body guards. The recommended 46-fish setup uses 43.")]
    [SerializeField] private int minimumBossIndexForRoyalGuards = 43;

    [SerializeField] private bool alwaysGuardEpicBoss = true;

    [SerializeField, Range(0f, 1f)]
    private float highBossBodyGuardChance = 1f;

    [SerializeField] private BossGuardFormationStyle highBossGuardFormation =
        BossGuardFormationStyle.Random;

    [SerializeField, Range(6, 24)]
    private int highBossRoyalGuardCount = 12;

    [SerializeField, Range(0, 3)]
    private int highBossRoyalGuardCountPerLevel = 1;

    [SerializeField, Range(0.55f, 1.8f)]
    private float highBossRoyalGuardSpacingMultiplier = 1f;

    [Tooltip("Epic boss introduction parade uses a royal convoy instead of a generic pre-boss wave.")]
    [SerializeField] private bool epicBossIntroductionUsesRoyalConvoy = true;

    [Header("Boss Body Guard Formation")]
    [Tooltip("Sometimes the boss arrives without body guards.")]
    [SerializeField, Range(0f, 1f)]
    private float noBossBodyGuardChance = 0.28f;

    [Tooltip("Each boss randomly receives two or three half-circle guard arcs.")]
    [SerializeField, Range(2, 3)]
    private int minimumBodyGuardHalfArcs = 2;

    [SerializeField, Range(2, 3)]
    private int maximumBodyGuardHalfArcs = 3;

    [SerializeField, Range(2, 8)]
    private int bodyGuardFishPerHalfArc = 4;

    [SerializeField] private float bodyGuardInnerRadius = 2.35f;
    [SerializeField] private float bodyGuardArcRadiusSpacing = 0.78f;

    [SerializeField, Range(100f, 180f)]
    private float bodyGuardHalfArcDegrees = 155f;

    [Tooltip("All guards around one boss use exactly one fish prefab index.")]
    [SerializeField, Range(0f, 1f)]
    private float bodyGuardUseMediumFishChance = 0.18f;

    [SerializeField, Range(0.35f, 1.2f)]
    private float bodyGuardSpeedMultiplier = 0.82f;

    [Header("High Population Battle")]
    [SerializeField]
    private int absoluteMaximumActiveFish = 56;

    [SerializeField]
    private int bossBattleExtraFishCapacity = 14;

    [Header("Boss Battle Companion Traffic")]
    [Tooltip("Prevents a boss from being left alone. These are ordinary ambient fish, not Parade followers.")]
    [SerializeField] private bool keepAmbientFishWithBoss = true;

    [SerializeField, Range(1, 24)]
    private int minimumAmbientFishDuringBossBattle = 8;

    [SerializeField, Range(1, 5)]
    private int bossAmbientRefillBurst = 2;

    [SerializeField, Min(0.10f)]
    private float bossAmbientRefillInterval = 0.35f;

    private float nextBossAmbientRefillTime;

    [SerializeField, Range(1, 3)]
    private int bossArrivalRushFishMultiplier = 2;

    [SerializeField, Range(1, 3)]
    private int bossEntranceGuardMultiplier = 2;

    [Header("Boss Support Reinforcements")]
    [Tooltip(
        "Chance for a support wave to grow beyond its normal size. " +
        "A batch can still use only Maximum Double Support Waves Per Batch."
    )]
    [SerializeField, Range(0f, 0.25f)]
    private float bossSupportDoubleChance = 0.18f;

    [SerializeField]
    private int bossSupportBaseFish = 4;

    [SerializeField]
    private int bossSupportFishPerLevel = 1;

    [SerializeField]
    private int bossSupportFishPerExtraBoss = 2;

    [Tooltip(
        "Maximum successful reinforcement waves during one target-boss batch. " +
        "Set to 0 to disable repeating boss support."
    )]
    [SerializeField, Range(0, 4)]
    private int maximumBossSupportWavesPerBatch = 2;

    [Tooltip(
        "Hard cap after every level, extra-boss, and double-wave modifier."
    )]
    [SerializeField, Range(1, 12)]
    private int maximumBossSupportFishPerWave = 8;

    [Tooltip(
        "Prevents a nearly full battlefield from repeatedly adding tiny " +
        "one-fish support waves."
    )]
    [SerializeField, Range(1, 8)]
    private int minimumBossSupportFishPerWave = 3;

    [Tooltip(
        "A double support event may happen only this many times per boss batch."
    )]
    [SerializeField, Range(0, 1)]
    private int maximumDoubleSupportWavesPerBatch = 1;

    [Tooltip(
        "For a multi-boss batch, stop new reinforcement waves after the first " +
        "boss is defeated or retreats. Existing support fish leave normally."
    )]
    [SerializeField]
    private bool stopSupportAfterBossCountDrops = true;

    [SerializeField, Min(1f)]
    private float bossSupportFirstDelayMin = 12f;

    [SerializeField, Min(1f)]
    private float bossSupportFirstDelayMax = 18f;

    [SerializeField, Min(1f)]
    private float bossSupportRepeatDelayMin = 22f;

    [SerializeField, Min(1f)]
    private float bossSupportRepeatDelayMax = 30f;

    [Tooltip(
        "Retry delay when the screen is too full. Failed attempts do not use a wave."
    )]
    [SerializeField, Min(1f)]
    private float bossSupportFullScreenRetryDelay = 4f;

    [SerializeField, HideInInspector]
    private int bossSupportSettingsVersion;

    [Header("Boss Arrival Events")]
    [SerializeField] private bool enableBossArrivalEvents = true;
    [SerializeField] private WeightedBossArrivalEvent[] weightedBossArrivalEvents;
    private BossArrivalEventType lastBossArrivalEvent = (BossArrivalEventType)(-1);

    [Tooltip("Assign a camera parent or Main Camera. When empty, Camera.main is used.")]
    [SerializeField] private Transform cameraShakeTarget;

    [SerializeField] private float bossEventBaseDuration = 1.3f;
    [SerializeField] private float bossEventDurationPerLevel = 0.25f;

    [SerializeField] private float bossEventBaseShakeStrength = 0.07f;
    [SerializeField] private float bossEventShakeStrengthPerLevel = 0.035f;

    [SerializeField, Range(0f, 1f)]
    private float bossEventBackgroundTintStrength = 0.28f;

    [Tooltip("Optional pooled event effects. Index 0-5 matches TidalSurge, Earthquake, LightningStorm, GoldenFrenzy, AbyssGate, TreasureCurrent.")]
    [SerializeField] private GameObject[] bossArrivalEventEffects;

    [SerializeField] private Transform bossEventEffectSpawnPoint;

    [Header("Boss Arrival Crowd Build-Up")]
    [SerializeField] private bool enableBossArrivalCrowdBuildUp = true;

    [SerializeField, Range(1, 4)]
    private int bossArrivalCrowdBaseWaves = 2;

    [SerializeField, Range(1, 5)]
    private int bossArrivalCrowdMaximumWaves = 4;

    [SerializeField, Min(4)]
    private int bossArrivalCrowdBaseFishPerWave = 16;

    [SerializeField, Min(0)]
    private int bossArrivalCrowdFishPerLevel = 4;

    [SerializeField, Min(0.1f)]
    private float bossArrivalCrowdWaveGap = 0.9f;

    [SerializeField, Range(0f, 1f)]
    private float bossArrivalSpecialChanceBase = 0.10f;

    [SerializeField, Range(0f, 0.2f)]
    private float bossArrivalSpecialChancePerLevel = 0.035f;

    [Tooltip("Optional explicit Fish array indexes for Special fish. When empty, FishTier.Special prefabs are detected automatically.")]
    [SerializeField] private int[] specialFishIndices =
    {
        31, 32, 33, 34, 35
    };

    [Header("Showcase Parade Styles")]
    [SerializeField, Min(0.5f)]
    private float treasureRingInnerRadius = 1.35f;

    [SerializeField, Min(0.2f)]
    private float treasureRingRadiusSpacing = 0.9f;

    [SerializeField, Range(2, 5)]
    private int mirroredFleetRows = 3;

    [Header("Boss Clear Battlefield Delay")]
    [SerializeField] private float bossClearInitialDelay = 0.5f;
    [SerializeField] private int remainingFishBeforeLevelChange = 18;
    [SerializeField] private float battlefieldClearMaximumWait = 2.5f;

    [Tooltip("After every target boss is defeated, play one or two final parade waves before the tide transition.")]
    [SerializeField, Range(0, 2)]
    private int postBossParadeMinimum = 0;

    [SerializeField, Range(0, 2)]
    private int postBossParadeMaximum = 1;

    [SerializeField, Min(0.1f)]
    private float postBossParadeGap = 0.6f;

    [Header("Natural Level Transition")]
    [Tooltip(
        "When enabled, the tide, background, and next level wait until every " +
        "active special-fish cinematic death sequence has fully completed."
    )]
    [SerializeField]
    private bool waitForCinematicDeathsBeforeLevelChange = true;

    [Tooltip(
        "How often a status message may be printed while a cinematic blocks " +
        "the level transition. This is rate-limited and never logs per frame."
    )]
    [SerializeField, Min(1f)]
    private float cinematicTransitionLogInterval = 5f;

    [SerializeField] private Vector2 levelTransitionDelayRange =
        new Vector2(0.30f, 0.70f);
    [SerializeField, Min(0.02f)] private float fishExitInterval = 0.06f;

    [Tooltip("Time allowed for the first natural exit route before the faster exit route is requested.")]
    [SerializeField, Min(0.5f)] private float maximumFishExitWait = 3.25f;

    [Tooltip("Extra time for fast natural exits. The next level may start after this without hiding visible fish.")]
    [SerializeField, Min(0.5f)] private float emergencyNaturalExitWait = 3.50f;

    [Tooltip("Rate-limited warning interval for fish that continue exiting after the next level starts.")]
    [SerializeField, Min(1f)] private float forceCleanupTimeout = 8f;

    [Tooltip("Speed multiplier used only for fish that did not finish their first natural exit route.")]
    [SerializeField, Min(1f)] private float emergencyLevelExitSpeedMultiplier = 3.4f;

    [Tooltip("Begin background preparation while old fish are already leaving.")]
    [SerializeField, Min(0f)] private float transitionPreparationLeadTime = 0.45f;

    [Tooltip("Maximum total blocking time. Remaining live fish keep exiting naturally in the new level and are not hidden.")]
    [SerializeField, Min(1f)] private float maximumBlockingTransitionTime = 7.0f;

    [SerializeField, Min(0.1f)] private float backgroundTransitionFadeDuration = 0.70f;

    [Tooltip("Small pause after the blocking exit phase finishes.")]
    [SerializeField, Min(0f)] private float fullyOutsideSettleDelay = 0.12f;

    [SerializeField, Min(0f)] private float nextLevelSpawnPreparationDelay = 0.25f;
    [SerializeField, Min(0.05f)] private float initialSpawnIntervalForNewLevel = 0.35f;
    [SerializeField, Min(0.5f)] private float newLevelSpawnRampDuration = 3.5f;

    [SerializeField, HideInInspector]
    private int levelTransitionSettingsVersion;

    [Header("Boss Timeout and Forced Progression")]
    [SerializeField, Min(10f)] private float maximumBossDuration = 75f;
    [SerializeField, Min(1f)] private float bossTimeoutWarningTime = 15f;
    [SerializeField, Min(1f)] private float bossFinalGracePeriod = 9f;
    [SerializeField, Range(0f, 1f)] private float forcedBossCenterRouteChance = 0.88f;
    [SerializeField, Min(1f)] private float bossEscapeSpeedMultiplier = 1.55f;
    [SerializeField] private BossTimeoutBehavior bossTimeoutBehavior = BossTimeoutBehavior.EscapeAndProgress;
    [SerializeField] private BossTimeoutOutcome bossTimeoutOutcome = BossTimeoutOutcome.Escaped;
    [SerializeField, Range(0f, 1f)] private float timeoutPartialRewardPercent = 0.25f;
    [SerializeField, Range(0.001f, 0.10f)] private float timeoutHealthDrainPerSecond = 0.025f;

    [Header("Longer Boss Arrival Timing")]
    [SerializeField]
    private float bossArrivalExtraBaseDelay = 0f;

    [SerializeField]
    private float bossArrivalExtraPerLevel = 0f;

    [Header("Gentle Difficulty Scaling")]
    [SerializeField] private float normalHpPerLevel = 0f;
    [SerializeField] private float normalRewardPerLevel = 0f;
    [SerializeField] private float normalSpeedPerLevel = 0f;

    [SerializeField] private float miniBossBaseHpMultiplier = 1f;
    [SerializeField] private float miniBossHpPerLevel = 0f;
    [SerializeField] private float miniBossRewardPerLevel = 0f;
    [SerializeField] private float miniBossSpeedPerLevel = 0f;

    [SerializeField] private float bossBaseHpMultiplier = 1f;
    [SerializeField] private float bossHpPerLevel = 0f;
    [SerializeField] private float bossRewardPerLevel = 0f;
    [SerializeField] private float bossSpeedPerLevel = 0f;

    [SerializeField] private float endlessHpPerLoop = 0.05f;
    [SerializeField] private float endlessRewardPerLoop = 0.05f;
    [SerializeField] private float endlessSpeedPerLoop = 0.01f;
    [SerializeField] private int maxScalingLoops = 5;


    [Header("Pre-Boss Mini Boss Groups")]
    [SerializeField, Range(1, 3)]
    private int maximumPreBossMiniBossCount = 3;

    [SerializeField, Range(0f, 1f)]
    private float secondMiniBossChance = 0.45f;

    [SerializeField, Range(0f, 1f)]
    private float thirdMiniBossChance = 0.18f;

    [SerializeField, Min(0.1f)]
    private float delayBetweenMiniBosses = 1.2f;

    [Header("Runtime Debug")]
    public GameLevel currentLevelState = GameLevel.Level1_Beginner;
    public LevelPhase currentPhase = LevelPhase.Opening;
    public bool isEndlessMode;
    public int loopMultiplier;

    [SerializeField] private float levelTimer;
    [SerializeField] private int targetBossNumber;
    [SerializeField] private int targetBossTotal;
    [SerializeField] private int defeatedBossCount;
    [SerializeField] private int resolvedBossCount;

    private int currentBatchRealDefeats;
    private int currentBatchRetreats;
    private int bossSupportWavesSpawnedThisBatch;
    private int doubleSupportWavesSpawnedThisBatch;
    private int bossSupportStartingBossCount;

    private float ambientSpawnTimer;
    private float paradeCooldownTimer;
    private float nextMinimumPopulationRefillTime;
    private bool isTideChanging;
    private int orderLayer;
    private int ambientTrafficLaneCursor;
    private float levelSpawnRampStartedAt;
    private float levelSpawnRampUntil;
    private bool bossTimeoutEscapeRequestedThisBatch;
    private float currentBossBatchStartedAt = -1f;

    private Coroutine gameLoopCoroutine;

    private readonly Dictionary<int, List<GameObject>> fishPool =
        new Dictionary<int, List<GameObject>>();

    private readonly List<GameObject> spawnedGimmicks =
        new List<GameObject>();
    private Coroutine gimmickCleanupCoroutine;

    private readonly List<List<GameObject>> bossEventEffectPools =
        new List<List<GameObject>>();


    public LevelPhase CurrentPhase
    {
        get { return currentPhase; }
    }

    public ParadeState CurrentParadeState
    {
        get { return paradeState; }
    }

    public bool IsParadeRunning
    {
        get { return paradeSessionRunning; }
    }

    public int ActiveParadeFishCount
    {
        get
        {
            PruneInactiveParadeFish();
            return activeParadeFish.Count;
        }
    }

    public bool IsTideChanging
    {
        get { return isTideChanging; }
    }

    public float CurrentBossTimeoutRemaining
    {
        get
        {
            if (currentBossBatchStartedAt < 0f || ActiveTargetBossCount <= 0)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                maximumBossDuration - (Time.time - currentBossBatchStartedAt)
            );
        }
    }

    public float CurrentBossGraceRemaining
    {
        get
        {
            if (currentBossBatchStartedAt < 0f || ActiveTargetBossCount <= 0)
            {
                return 0f;
            }

            float elapsed = Time.time - currentBossBatchStartedAt;
            if (elapsed < maximumBossDuration)
            {
                return bossFinalGracePeriod;
            }

            return Mathf.Max(
                0f,
                maximumBossDuration + bossFinalGracePeriod - elapsed
            );
        }
    }


    public int PooledFishInstanceCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
            {
                count += entry.Value != null ? entry.Value.Count : 0;
            }
            return count;
        }
    }

    public int InactivePooledFishCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
            {
                List<GameObject> pool = entry.Value;
                if (pool == null)
                {
                    continue;
                }

                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && !pool[i].activeSelf)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }

    public int CurrentLevelNumber
    {
        get { return Mathf.Clamp((int)currentLevelState + 1, 1, 6); }
    }

    public float CurrentLevelElapsedSeconds
    {
        get { return Mathf.Max(0f, levelTimer); }
    }

    public int CurrentBossTargetTotal
    {
        get { return Mathf.Max(0, targetBossTotal); }
    }

    public int CurrentBossTargetsResolved
    {
        get { return Mathf.Clamp(resolvedBossCount, 0, CurrentBossTargetTotal); }
    }

    public int ActiveTargetBossCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < activeTargetBosses.Count; i++)
            {
                GameObject boss = activeTargetBosses[i];

                if (boss != null && boss.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public float CurrentBossProgressNormalized
    {
        get
        {
            int total = CurrentBossTargetTotal;

            if (total <= 0)
            {
                return 0f;
            }

            float progress = CurrentBossTargetsResolved;

            if (currentPhase == LevelPhase.BossBattle)
            {
                progress += Mathf.Max(
                    0,
                    currentBatchRealDefeats + currentBatchRetreats
                );

                for (int i = 0; i < activeTargetBosses.Count; i++)
                {
                    GameObject bossObject = activeTargetBosses[i];
                    FishScript boss = bossObject != null
                        ? bossObject.GetComponent<FishScript>()
                        : null;

                    if (boss != null && boss.IsAliveTarget)
                    {
                        progress += 1f - boss.CurrentHealthNormalized;
                    }
                }
            }

            return Mathf.Clamp01(progress / total);
        }
    }

    public FishScript CurrentPrimaryTargetBoss
    {
        get
        {
            FishScript fallback = null;

            for (int i = 0; i < activeTargetBosses.Count; i++)
            {
                GameObject bossObject = activeTargetBosses[i];
                FishScript boss = bossObject != null
                    ? bossObject.GetComponent<FishScript>()
                    : null;

                if (boss == null || !boss.IsAliveTarget)
                {
                    continue;
                }

                if (boss.IsEpicBoss)
                {
                    return boss;
                }

                if (fallback == null)
                {
                    fallback = boss;
                }
            }

            return fallback;
        }
    }

    public string CurrentLevelDisplayName
    {
        get
        {
            switch (currentLevelState)
            {
                case GameLevel.Level1_Beginner:
                    return "Ribbon Current";
                case GameLevel.Level2_School:
                    return "Arrowhead School";
                case GameLevel.Level3_Circle:
                    return "Living Halo";
                case GameLevel.Level4_Cross:
                    return "Cardinal Cross";
                case GameLevel.Level5_Festival:
                    return "Twin Current";
                case GameLevel.Level6_BossOcean:
                    return "Royal Armada";
                default:
                    return "Ocean Stage";
            }
        }
    }

    public string CurrentPhaseDisplayName
    {
        get
        {
            switch (currentPhase)
            {
                case LevelPhase.Opening:
                    return "Opening Tide";
                case LevelPhase.FeatureBuildUp:
                    return "Formation Wave";
                case LevelPhase.PreBossParade:
                    return "Royal Parade";
                case LevelPhase.BossWarning:
                    return "Boss Approaching";
                case LevelPhase.BossBattle:
                    return "Boss Battle";
                case LevelPhase.Recovery:
                    return "Clear Water";
                case LevelPhase.TideChange:
                    return "Tide Changing";
                default:
                    return currentPhase.ToString();
            }
        }
    }

    private void Start()
    {
        ValidateAndRepairConfiguration();

        if (spawnDirectorService == null)
        {
            spawnDirectorService = GetComponent<FishSpawnDirectorService>();
        }

        if (spawnDirectorService == null)
        {
            spawnDirectorService = gameObject.AddComponent<FishSpawnDirectorService>();
        }

        if (!enabled)
        {
            return;
        }

        FitSpriteToScreen(backgroundRenderer);
        BuildPoolDictionary();
        EnsureBossEventEffectPools();

        ambientTrafficLaneCursor = Random.Range(
            0,
            Mathf.Max(2, ambientTrafficLaneCount)
        );
        ambientSpawnTimer = 0.5f;
        paradeCooldownTimer = 0f;
        paradeState = ParadeState.Completed;
        paradeSessionRunning = false;
        activeParadeFish.Clear();
        gameLoopCoroutine = StartCoroutine(GameLoopRoutine());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        bool changed = false;

        if (bossSupportSettingsVersion <
            CurrentBossSupportSettingsVersion)
        {
            ApplyRecommendedBossSupportSettings();
            changed = true;
        }

        if (levelTransitionSettingsVersion <
            CurrentLevelTransitionSettingsVersion)
        {
            ApplyRecommendedLevelTransitionSettings();
            changed = true;
        }

        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    private void Update()
    {
        if (isTideChanging)
        {
            return;
        }

        UpdateParadeStateMachine();

        levelTimer += Time.deltaTime;
        ambientSpawnTimer -= Time.deltaTime;
        paradeCooldownTimer -= Time.deltaTime;

        // The warning itself is intentionally clean and short. The opening
        // parade also gets a clean presentation before ambient traffic starts.
        bool holdForOpeningParade =
            openingParadePresentationRunning &&
            (!allowAmbientAfterOpeningParadeVisible || !paradeHasBeenVisible);

        if (currentPhase == LevelPhase.BossWarning ||
            holdForOpeningParade ||
            (pauseNormalSpawningDuringParade && paradeSessionRunning))
        {
            return;
        }

        MaintainMinimumPopulation();

        if (currentPhase == LevelPhase.BossBattle)
        {
            MaintainBossCompanionPopulation();
        }

        if (ambientSpawnTimer <= 0f)
        {
            SpawnAmbientNormalFish();
            ambientSpawnTimer = GetNextAmbientDelay();
        }

        if (paradeCooldownTimer <= 0f)
        {
            if (currentPhase == LevelPhase.BossBattle)
            {
                bool spawnedSupport = SpawnBossSupportWave();

                if (!CanSpawnAnotherBossSupportWave())
                {
                    // A new boss batch explicitly replaces this value.
                    paradeCooldownTimer = float.PositiveInfinity;
                }
                else
                {
                    paradeCooldownTimer = spawnedSupport
                        ? GetBossSupportDelay()
                        : bossSupportFullScreenRetryDelay;
                }
            }
            else
            {
                if (periodicParadeCoroutine == null &&
                    !paradeSessionRunning)
                {
                    periodicParadeCoroutine = StartCoroutine(
                        RunPeriodicParadeRoutine()
                    );
                    paradeCooldownTimer = GetNextParadeDelay();
                }
            }
        }
    }

    private bool BeginParadeSession(string reason)
    {
        if (paradeSessionRunning)
        {
            return false;
        }

        activeParadeFish.Clear();
        paradeSessionRunning = true;
        paradeSpawningComplete = false;
        paradeHasBeenVisible = false;
        paradeTimeoutExitRequested = false;
        paradeSessionStartedAt = Time.time;
        paradeTimeoutExitRequestedAt = -1f;
        activeParadeReason = string.IsNullOrEmpty(reason)
            ? "Parade"
            : reason;
        paradeState = ParadeState.Preparing;

        if (clearNormalFishBeforeParade)
        {
            RequestNormalFishExitForParade(
                paradeAmbientExitSpeedMultiplier
            );
        }

        return true;
    }

    private void RegisterParadeFish(FishScript fish)
    {
        if (fish == null || !fish.gameObject.activeInHierarchy)
        {
            return;
        }

        bool implicitSession = false;
        if (!paradeSessionRunning)
        {
            implicitSession = BeginParadeSession("Periodic Parade");
        }

        PruneInactiveParadeFish();

        if (!activeParadeFish.Contains(fish) &&
            activeParadeFish.Count >= Mathf.Max(1, maximumParadeFishCount))
        {
            fish.gameObject.SetActive(false);
            return;
        }

        activeParadeFish.Add(fish);
        paradeState = ParadeState.Spawning;

        if (implicitSession)
        {
            // Periodic groups are spawned synchronously in one update pass.
            // They can finish naturally as soon as their active fish exit.
            paradeSpawningComplete = true;
        }
    }

    private void MarkParadeSpawningComplete()
    {
        if (!paradeSessionRunning)
        {
            return;
        }

        paradeSpawningComplete = true;
        if (activeParadeFish.Count > 0 &&
            paradeState == ParadeState.Preparing)
        {
            paradeState = ParadeState.Entering;
        }
    }

    private void UpdateParadeStateMachine()
    {
        if (!paradeSessionRunning)
        {
            return;
        }

        PruneInactiveParadeFish();

        bool anyVisible = false;
        Camera mainCamera = Camera.main;

        foreach (FishScript fish in activeParadeFish)
        {
            if (fish == null || !fish.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsParadeFishInVisibleBand(fish, mainCamera))
            {
                anyVisible = true;
                break;
            }
        }

        if (activeParadeFish.Count == 0)
        {
            if (paradeSpawningComplete)
            {
                ForceCompleteParadeSession(false);
            }
            return;
        }

        if (anyVisible)
        {
            paradeHasBeenVisible = true;
            paradeState = ParadeState.Active;
        }
        else if (!paradeHasBeenVisible)
        {
            paradeState = ParadeState.Entering;
        }
        else
        {
            paradeState = ParadeState.Exiting;
        }

        float elapsed = Time.time - paradeSessionStartedAt;
        if (!paradeTimeoutExitRequested &&
            elapsed >= Mathf.Max(4f, paradeCompletionTimeout))
        {
            paradeTimeoutExitRequested = true;
            paradeTimeoutExitRequestedAt = Time.time;
            paradeState = ParadeState.Exiting;

            foreach (FishScript fish in activeParadeFish)
            {
                if (fish != null && fish.gameObject.activeInHierarchy &&
                    !fish.IsDeadOrDying)
                {
                    fish.ForceLevelTransitionNaturalExit(2.25f);
                }
            }

            Debug.LogWarning(
                "[PARADE TIMEOUT] " + activeParadeReason +
                " exceeded " + paradeCompletionTimeout.ToString("0.0") +
                "s. Requesting natural fast exits.",
                this
            );
        }

        if (paradeTimeoutExitRequested &&
            Time.time - paradeTimeoutExitRequestedAt >=
                Mathf.Max(0.25f, paradeTimeoutCleanupGrace))
        {
            foreach (FishScript fish in activeParadeFish)
            {
                if (fish != null && fish.gameObject.activeInHierarchy)
                {
                    fish.gameObject.SetActive(false);
                }
            }

            activeParadeFish.Clear();
            ForceCompleteParadeSession(true);
        }
    }

    private bool IsParadeFishInVisibleBand(
        FishScript fish,
        Camera targetCamera
    )
    {
        if (fish == null || !fish.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (targetCamera == null)
        {
            return true;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(
            fish.GetTargetCenterWorld()
        );
        float padding = Mathf.Clamp(paradeVisibilityPadding, 0f, 0.20f);
        return viewport.z > 0f &&
               viewport.x >= -padding &&
               viewport.x <= 1f + padding &&
               viewport.y >= -padding &&
               viewport.y <= 1f + padding;
    }

    private void PruneInactiveParadeFish()
    {
        activeParadeFish.RemoveWhere(
            fish => fish == null || !fish.gameObject.activeInHierarchy
        );
    }

    private void ForceCompleteParadeSession(bool timedOut)
    {
        if (!paradeSessionRunning && activeParadeFish.Count == 0)
        {
            paradeState = ParadeState.Completed;
            return;
        }

        activeParadeFish.Clear();
        paradeSessionRunning = false;
        paradeSpawningComplete = true;
        paradeState = ParadeState.Completed;
        paradeHasBeenVisible = false;
        paradeTimeoutExitRequested = false;
        activeParadeReason = string.Empty;

        if (timedOut)
        {
            Debug.LogWarning(
                "[PARADE] Timeout cleanup completed. Normal spawn flow restored.",
                this
            );
        }
    }

    private IEnumerator WaitForParadeCompletionRoutine()
    {
        float maximumWait = Mathf.Max(4f, paradeCompletionTimeout) +
            Mathf.Max(0.25f, paradeTimeoutCleanupGrace) + 1f;
        float elapsed = 0f;

        while (paradeSessionRunning && elapsed < maximumWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (paradeSessionRunning)
        {
            ForceCompleteParadeSession(true);
        }
    }

    private IEnumerator PrepareParadeStageRoutine(bool openingParade)
    {
        if (!clearNormalFishBeforeParade)
        {
            yield break;
        }

        // At a level opening, a parade-controlled fish from the previous level
        // is stale because the old parade session was already completed/reset.
        // Treat it like ambient traffic so the new parade remains the first
        // visible fish event. During an active parade, currently registered
        // parade fish are never touched.
        RequestNormalFishExitForParade(
            paradeAmbientExitSpeedMultiplier,
            openingParade
        );

        float elapsed = 0f;
        float wait = Mathf.Max(0.25f, paradeAmbientClearTimeout);
        while (CountVisibleNonParadeNormalFish(openingParade) > 0 &&
               elapsed < wait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (CountVisibleNonParadeNormalFish(openingParade) <= 0)
        {
            yield break;
        }

        RequestNormalFishExitForParade(
            paradeAmbientExitSpeedMultiplier * 1.35f,
            openingParade
        );

        float grace = 0f;
        while (CountVisibleNonParadeNormalFish(openingParade) > 0 &&
               grace < 0.55f)
        {
            grace += Time.deltaTime;
            yield return null;
        }

        if (!openingParade ||
            CountVisibleNonParadeNormalFish(true) <= 0)
        {
            yield break;
        }

        // Opening parade must be the first visible fish event of a new level.
        // Any leftover ordinary/stale-parade fish that ignored both natural
        // exit requests are returned to their existing pool as a final fallback.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        Camera mainCamera = Camera.main;
        for (int i = gm.fishInScreenList.Count - 1; i >= 0; i--)
        {
            FishScript fish = gm.fishInScreenList[i];
            if (!ShouldClearForParade(fish, includeStaleParadeFish: true))
            {
                continue;
            }

            if (mainCamera == null || fish.IsTargetVisibleTo(mainCamera))
            {
                fish.gameObject.SetActive(false);
            }
        }
    }

    private int CountVisibleNonParadeNormalFish(
        bool includeStaleParadeFish = false
    )
    {
        GameManager gm = GameManager.Instance;
        Camera mainCamera = Camera.main;
        if (gm == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < gm.fishInScreenList.Count; i++)
        {
            FishScript fish = gm.fishInScreenList[i];
            if (!ShouldClearForParade(fish, includeStaleParadeFish))
            {
                continue;
            }

            if (mainCamera == null || fish.IsTargetVisibleTo(mainCamera))
            {
                count++;
            }
        }
        return count;
    }

    private bool ShouldClearForParade(
        FishScript fish,
        bool includeStaleParadeFish
    )
    {
        if (fish == null || !fish.gameObject.activeInHierarchy ||
            fish.IsBoss() || fish.IsDeadOrDying)
        {
            return false;
        }

        if (!fish.IsParadeControlled)
        {
            return true;
        }

        if (!includeStaleParadeFish)
        {
            return false;
        }

        // A registered parade fish belongs to the currently running parade and
        // must never be cleared by route-priority cleanup. Anything else is a
        // leftover from a completed/previous-level parade.
        return !activeParadeFish.Contains(fish);
    }

    private void RequestNormalFishExitForParade(
        float speedMultiplier,
        bool includeStaleParadeFish = false
    )
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        for (int i = 0; i < gm.fishInScreenList.Count; i++)
        {
            FishScript fish = gm.fishInScreenList[i];
            if (!ShouldClearForParade(fish, includeStaleParadeFish))
            {
                continue;
            }

            fish.ForceLevelTransitionNaturalExit(
                Mathf.Max(1f, speedMultiplier)
            );
        }
    }

    private IEnumerator RunPeriodicParadeRoutine()
    {
        if (!BeginParadeSession("Periodic Level Parade"))
        {
            periodicParadeCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(PrepareParadeStageRoutine(false));
        TriggerLevelSpecificSpawn();
        MarkParadeSpawningComplete();
        yield return StartCoroutine(WaitForParadeCompletionRoutine());
        periodicParadeCoroutine = null;
    }

    private IEnumerator GameLoopRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(RunLevelRoutine(currentLevelState));

            GameLevel nextLevel = GetNextLevel(currentLevelState);
            yield return StartCoroutine(TideChangeRoutine(nextLevel));

            currentLevelState = nextLevel;
        }
    }

    private IEnumerator RunLevelRoutine(GameLevel level)
    {
        levelTimer = 0f;
        openingParadePresentationRunning = false;
        ForceCompleteParadeSession(false);
        defeatedBossCount = 0;
        resolvedBossCount = 0;
        currentBatchRealDefeats = 0;
        currentBatchRetreats = 0;
        targetBossNumber = 0;
        ResetBossSupportState();

        activeTargetBosses.Clear();

        int[] bossTargets = GetBossTargetsForLevel(level);

        if (bossTargets == null || bossTargets.Length == 0)
        {
            Debug.LogError(
                "No boss targets configured for " + level
            );

            yield break;
        }

        targetBossTotal = bossTargets.Length;

        currentPhase = LevelPhase.Opening;
        paradeCooldownTimer = 0f;
        ambientSpawnTimer = 0.5f;

        float warmupDuration = GetWarmupDuration(level);
        if (prioritizeMainBossPresence)
        {
            warmupDuration = Mathf.Min(
                warmupDuration,
                Mathf.Max(1f, firstMainBossWarmupCap)
            );
        }

        float openingDuration = warmupDuration * 0.42f;
        float featureDuration =
            warmupDuration - openingDuration;

        Debug.Log(
            "<color=#4FC3F7>[LEVEL]</color> " +
            level + " opening started with " +
            targetBossTotal + " total bosses."
        );

        if (playStandardParadeAtEveryLevelStart)
        {
            yield return StartCoroutine(
                RunLevelOpeningParadeRoutine(level)
            );
        }

        yield return new WaitForSeconds(
            openingDuration
        );

        currentPhase = LevelPhase.FeatureBuildUp;
        paradeCooldownTimer = 0f;

        Debug.Log(
            "<color=#FFD54F>[FEATURE]</color> " +
            "Special formations enabled."
        );

        if (enableStandaloneFeatureParade &&
            Random.value <= standaloneFeatureParadeChance)
        {
            yield return StartCoroutine(
                RunStandaloneFeatureParadeRoutine(level)
            );
        }

        yield return new WaitForSeconds(
            featureDuration
        );

        int targetCursor = 0;
        int bossBatchNumber = 0;

        while (targetCursor < bossTargets.Length)
        {
            bossBatchNumber++;

            int remainingBosses =
                bossTargets.Length - targetCursor;

            int batchSize = GetBossBatchSize(
                level,
                remainingBosses
            );

            targetBossNumber = targetCursor + 1;

            currentPhase =
                LevelPhase.PreBossParade;

            yield return StartCoroutine(
                RunPreBossRoyalParadeRoutine(
                    level,
                    bossBatchNumber - 1
                )
            );

            currentPhase =
                LevelPhase.BossWarning;

            Debug.Log(
                "<color=#FF8A65>[BOSS WARNING]</color> " +
                batchSize + " boss" +
                (batchSize > 1 ? "es" : "") +
                " approaching together. Progress: " +
                resolvedBossCount + "/" +
                targetBossTotal
            );

            bool isFirstMainBossBatch = bossBatchNumber == 1;

            float warningDelay = GetBossWarningDelay(level);
            if (prioritizeMainBossPresence && isFirstMainBossBatch)
            {
                warningDelay = Mathf.Min(
                    warningDelay,
                    Mathf.Max(0f, firstMainBossWarningDelayCap)
                );
            }

            yield return new WaitForSeconds(warningDelay);

            yield return StartCoroutine(
                RunBossArrivalEvent(level, isFirstMainBossBatch)
            );

            if (!(prioritizeMainBossPresence &&
                  isFirstMainBossBatch &&
                  skipFirstMainBossCrowdBuildUp))
            {
                yield return StartCoroutine(
                    RunBossArrivalCrowdBuildUpRoutine(level)
                );
            }

            currentBatchRealDefeats = 0;
            currentBatchRetreats = 0;
            bossTimeoutEscapeRequestedThisBatch = false;
            ResetBossSupportState();

            int spawnedBossCount =
                SpawnTargetBossBatch(
                    bossTargets,
                    targetCursor,
                    batchSize
                );

            if (spawnedBossCount <= 0)
            {
                Debug.LogError(
                    "Boss batch failed to spawn."
                );

                targetCursor += batchSize;
                continue;
            }

            currentPhase =
                LevelPhase.BossBattle;

            bossSupportStartingBossCount =
                spawnedBossCount;

            paradeCooldownTimer =
                GetBossSupportFirstDelay();

            ambientSpawnTimer =
                GetNextAmbientDelay();
            nextBossAmbientRefillTime = 0f;
            MaintainBossCompanionPopulation();

            Debug.Log(
                "<color=#EF5350>[BOSS BATTLE]</color> " +
                spawnedBossCount +
                " target bosses currently active."
            );

            // Wait until every boss in this batch is defeated or resolves
            // through the configurable timeout flow. The boss always receives
            // a visible warning and an exit opportunity before force cleanup.
            currentBossBatchStartedAt = Time.time;
            bool timeoutWarningIssued = false;
            bool timeoutResolutionStarted = false;
            bool emergencyNaturalExitIssued = false;

            while (activeTargetBosses.Count > 0)
            {
                activeTargetBosses.RemoveAll(
                    boss =>
                        boss == null ||
                        !boss.activeSelf
                );

                float bossElapsed = Time.time - currentBossBatchStartedAt;
                float warningAt = Mathf.Max(
                    0f,
                    maximumBossDuration - bossTimeoutWarningTime
                );

                if (!timeoutWarningIssued && bossElapsed >= warningAt)
                {
                    timeoutWarningIssued = true;
                    BeginBossTimeoutWarningForActiveBosses();
                }

                if (!timeoutResolutionStarted &&
                    bossElapsed >= maximumBossDuration)
                {
                    timeoutResolutionStarted = true;
                    BeginBossTimeoutResolution();
                }

                if (timeoutResolutionStarted)
                {
                    if (bossTimeoutBehavior ==
                        BossTimeoutBehavior.GraduallyReduceHealthThenEscape)
                    {
                        DrainActiveBossHealthForTimeout(Time.deltaTime);

                        if (bossElapsed >= maximumBossDuration +
                            bossFinalGracePeriod * 0.45f)
                        {
                            RequestActiveBossTimeoutEscape();
                        }
                    }

                    if (!emergencyNaturalExitIssued &&
                        bossElapsed >=
                        maximumBossDuration + bossFinalGracePeriod)
                    {
                        emergencyNaturalExitIssued = true;
                        RequestEmergencyNaturalBossExits();
                    }
                }

                yield return null;
            }

            currentBossBatchStartedAt = -1f;

            defeatedBossCount +=
                currentBatchRealDefeats;

            resolvedBossCount +=
                spawnedBossCount;

            targetCursor +=
                batchSize;

            currentPhase =
                LevelPhase.Recovery;

            Debug.Log(
                "<color=#BA68C8>[BATCH CLEAR]</color> " +
                resolvedBossCount + "/" +
                targetBossTotal +
                " targets resolved | defeated: " +
                currentBatchRealDefeats +
                " | retreated: " +
                currentBatchRetreats
            );

            if (currentBatchRealDefeats > 0 &&
                GameManager.Instance != null &&
                GameManager.Instance.SoundManager != null)
            {
                GameManager.Instance.SoundManager.PlayBossDefeatSound();
            }

            if (targetCursor < bossTargets.Length)
            {
                yield return new WaitForSeconds(
                    GetBetweenBossDelay(level)
                );
            }
        }

        currentPhase =
            LevelPhase.Recovery;

        Debug.Log(
            "<color=#81C784>[LEVEL CLEAR]</color> " +
            "All " + resolvedBossCount +
            " boss targets resolved. Defeated: " +
            defeatedBossCount + "."
        );

        yield return new WaitForSeconds(
            Mathf.Max(0f, GetPostBossRecoveryDelay(level))
        );
        yield return StartCoroutine(WaitForBattlefieldClear());
    }
    // Called only from FishScript after a real target-boss defeat.

    private int GetBossBatchSize(
    GameLevel level,
    int remainingBosses
)
    {
        int levelIndex = (int)level;

        int levelMaximum =
            GetPerLevelIntValue(
                maximumSimultaneousBossesPerLevel,
                levelIndex,
                1
            );

        int maximumBatchSize = Mathf.Clamp(
            Mathf.Min(
                levelMaximum,
                globalMaximumSimultaneousBosses,
                remainingBosses
            ),
            1,
            3
        );

        if (maximumBatchSize <= 1)
        {
            return 1;
        }

        float doubleChance =
            GetPerLevelFloatValue(
                doubleBossChancePerLevel,
                levelIndex,
                0f
            );

        float tripleChance =
            GetPerLevelFloatValue(
                tripleBossChancePerLevel,
                levelIndex,
                0f
            );

        float roll = Random.value;

        if (maximumBatchSize >= 3 &&
            roll <= tripleChance)
        {
            return 3;
        }

        if (maximumBatchSize >= 2 &&
            roll <= tripleChance + doubleChance)
        {
            return 2;
        }

        return 1;
    }

    private int GetPerLevelIntValue(
    int[] values,
    int levelIndex,
    int fallback
)
    {
        if (values == null ||
            levelIndex < 0 ||
            levelIndex >= values.Length)
        {
            return fallback;
        }

        return values[levelIndex];
    }

    private float GetPerLevelFloatValue(
    float[] values,
    int levelIndex,
    float fallback
)
    {
        if (values == null ||
            levelIndex < 0 ||
            levelIndex >= values.Length)
        {
            return fallback;
        }

        return values[levelIndex];
    }

    /// <summary>
    /// Called by FishScript after a persistent target boss is defeated.
    /// </summary>
    public void NotifyLevelBossDefeated(FishScript defeatedBoss)
    {
        if (defeatedBoss == null)
        {
            return;
        }

        GameObject defeatedBossObject = defeatedBoss.gameObject;

        if (!activeTargetBosses.Remove(defeatedBossObject))
        {
            return;
        }

        currentBatchRealDefeats++;

        Debug.Log(
            "<color=#BA68C8>[TARGET BOSS DEFEATED]</color> " +
            "Boss: " + defeatedBossObject.name +
            " | Remaining in batch: " + activeTargetBosses.Count
        );
    }

    /// <summary>
    /// Called when a normal target boss completes a configured cinematic
    /// retreat. No reward is paid and it is not counted as defeated, but
    /// the current boss sequence is allowed to continue.
    /// </summary>
    public void NotifyLevelBossRetreated(FishScript retreatedBoss)
    {
        if (retreatedBoss == null)
        {
            return;
        }

        GameObject bossObject = retreatedBoss.gameObject;

        if (!activeTargetBosses.Remove(bossObject))
        {
            return;
        }

        currentBatchRetreats++;

        Debug.Log(
            "<color=#90A4AE>[TARGET BOSS RETREATED]</color> " +
            "Boss: " + bossObject.name +
            " | Remaining in batch: " +
            activeTargetBosses.Count
        );
    }

    private IEnumerator TideChangeRoutine(GameLevel newLevel)
    {
        isTideChanging = true;
        currentPhase = LevelPhase.TideChange;

        // Stop spawning immediately, but keep the current scene and fish
        // untouched until every running cinematic has completed its effects,
        // rewards, cleanup, and pool return.
        yield return WaitForCinematicDeathsBeforeLevelChange(
            "before tide exit"
        );

        Debug.Log(
            "<color=#26C6DA>[TIDE CHANGE]</color> " +
            currentLevelState + " -> " + newLevel +
            " | fast natural exit and next-level preparation started."
        );

        RequestStaggeredLevelTransitionExits();
        float transitionStartedAt = Time.time;

        if (transitionPreparationLeadTime > 0f)
        {
            yield return new WaitForSeconds(transitionPreparationLeadTime);
        }

        Coroutine backgroundTransition = null;
        if (backgroundRenderer != null && levelBackgrounds != null)
        {
            int backgroundIndex = (int)newLevel;
            if (backgroundIndex >= 0 &&
                backgroundIndex < levelBackgrounds.Length)
            {
                backgroundTransition = StartCoroutine(
                    TransitionBackground(
                        levelBackgrounds[backgroundIndex]
                    )
                );
            }
        }

        while (CountFishStillInsidePaddedBounds() > 0 &&
               Time.time - transitionStartedAt < maximumFishExitWait)
        {
            DisableFishAlreadyFullyOutside();
            yield return new WaitForSeconds(0.08f);
        }

        if (CountFishStillInsidePaddedBounds() > 0)
        {
            RequestEmergencyLevelTransitionExits(
                emergencyLevelExitSpeedMultiplier
            );
        }

        float emergencyStartedAt = Time.time;
        float nextReassertAt = Time.time + 0.75f;
        while (CountFishStillInsidePaddedBounds() > 0 &&
               Time.time - transitionStartedAt <
                   maximumBlockingTransitionTime)
        {
            DisableFishAlreadyFullyOutside();

            if (Time.time >= nextReassertAt)
            {
                float emergencyElapsed =
                    Time.time - emergencyStartedAt;
                float speedBoost = emergencyElapsed >=
                    emergencyNaturalExitWait
                    ? 1.40f
                    : 1f;

                RequestEmergencyLevelTransitionExits(
                    emergencyLevelExitSpeedMultiplier * speedBoost
                );
                nextReassertAt = Time.time + 0.75f;
            }

            yield return new WaitForSeconds(0.08f);
        }

        int remainingVisibleFish =
            CountFishStillInsidePaddedBounds();
        if (remainingVisibleFish > 0)
        {
            Debug.LogWarning(
                "[TIDE CHANGE] " + remainingVisibleFish +
                " live fish are still completing fast natural exits. " +
                "The next level will start without force-hiding them."
            );
            RequestEmergencyLevelTransitionExits(
                emergencyLevelExitSpeedMultiplier * 1.55f
            );
        }

        DisableFishAlreadyFullyOutside();

        if (backgroundTransition != null)
        {
            yield return backgroundTransition;
        }

        if (fullyOutsideSettleDelay > 0f)
        {
            yield return new WaitForSeconds(fullyOutsideSettleDelay);
        }

        float transitionDelay = Random.Range(
            Mathf.Min(
                levelTransitionDelayRange.x,
                levelTransitionDelayRange.y
            ),
            Mathf.Max(
                levelTransitionDelayRange.x,
                levelTransitionDelayRange.y
            )
        );

        if (transitionDelay > 0f)
        {
            yield return new WaitForSeconds(transitionDelay);
        }

        // A fish can begin a cinematic while the other fish are exiting.
        // Re-check immediately before committing the new level so no active
        // sequence is cut off by target cleanup, gimmick cleanup, or pooling.
        yield return WaitForCinematicDeathsBeforeLevelChange(
            "before level commit"
        );

        DisableFishAlreadyFullyOutside();
        activeTargetBosses.Clear();
        ResetBossSupportState();
        ClearSpawnedGimmicks();

        if (currentLevelState ==
                GameLevel.Level6_BossOcean &&
            newLevel == GameLevel.Level1_Beginner)
        {
            loopMultiplier++;
            isEndlessMode = true;

            Debug.Log(
                "<color=#EC407A>[ENDLESS CYCLE]</color> Cycle " +
                (loopMultiplier + 1) + " started."
            );
        }

        if (nextLevelSpawnPreparationDelay > 0f)
        {
            yield return new WaitForSeconds(
                nextLevelSpawnPreparationDelay
            );
        }

        isTideChanging = false;
        currentPhase = LevelPhase.Opening;
        paradeCooldownTimer = Mathf.Max(
            0.75f,
            initialSpawnIntervalForNewLevel * 2.25f
        );
        ambientSpawnTimer = initialSpawnIntervalForNewLevel;
        levelSpawnRampStartedAt = Time.time;
        levelSpawnRampUntil =
            Time.time + Mathf.Max(0.5f, newLevelSpawnRampDuration);
    }

    private IEnumerator WaitForCinematicDeathsBeforeLevelChange(
        string transitionStage
    )
    {
        if (!waitForCinematicDeathsBeforeLevelChange)
        {
            yield break;
        }

        int stableClearFrames = 0;
        bool loggedWaiting = false;
        float nextStatusLogAt = Time.unscaledTime;

        // Require two consecutive clear frames. This catches chained cinematic
        // deaths started by the final area-damage pulse of another sequence.
        while (stableClearFrames < 2)
        {
            int activeCount =
                SpecialFishCinematicDeathController.ActiveSequenceCount;

            if (activeCount <= 0)
            {
                stableClearFrames++;
                yield return null;
                continue;
            }

            stableClearFrames = 0;

            if (!loggedWaiting || Time.unscaledTime >= nextStatusLogAt)
            {
                loggedWaiting = true;
                nextStatusLogAt = Time.unscaledTime + Mathf.Max(
                    1f,
                    cinematicTransitionLogInterval
                );

                Debug.Log(
                    "<color=#FFB74D>[CINEMATIC GATE]</color> Waiting for " +
                    activeCount + " active cinematic death sequence(s) " +
                    transitionStage + "."
                );
            }

            yield return null;
        }

        if (loggedWaiting)
        {
            Debug.Log(
                "<color=#81C784>[CINEMATIC GATE CLEAR]</color> All cinematic " +
                "death sequences completed; level transition may continue."
            );
        }
    }

    private void RequestStaggeredLevelTransitionExits()
    {
        int sequence = 0;
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject fishObject = pool[i];
                if (fishObject == null || !fishObject.activeSelf)
                {
                    continue;
                }

                FishScript fish = fishObject.GetComponent<FishScript>();
                if (fish == null)
                {
                    fishObject.SetActive(false);
                    continue;
                }

                if (!fish.ShouldBlockLevelTransition ||
                    fish.IsCinematicDeathRunning)
                {
                    continue;
                }

                float delay = sequence * fishExitInterval +
                    Random.Range(0f, fishExitInterval * 0.75f);
                fish.RequestLevelTransitionExit(delay);
                sequence++;
            }
        }

        for (int i = 0; i < spawnedGimmicks.Count; i++)
        {
            GameObject gimmick = spawnedGimmicks[i];
            if (gimmick == null || !gimmick.activeSelf ||
                !gimmick.TryGetComponent<FishScript>(out FishScript fish) ||
                !fish.ShouldBlockLevelTransition ||
                fish.IsCinematicDeathRunning)
            {
                continue;
            }

            float delay = sequence * fishExitInterval +
                Random.Range(0f, fishExitInterval * 0.75f);
            fish.RequestLevelTransitionExit(delay);
            sequence++;
        }
    }

    private void RequestEmergencyLevelTransitionExits(
        float speedMultiplier
    )
    {
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;

            for (int i = 0; i < pool.Count; i++)
            {
                GameObject fishObject = pool[i];
                if (fishObject == null || !fishObject.activeSelf)
                {
                    continue;
                }

                FishScript fish = fishObject.GetComponent<FishScript>();
                if (fish == null)
                {
                    fishObject.SetActive(false);
                    continue;
                }

                if (!fish.ShouldBlockLevelTransition ||
                    fish.IsCinematicDeathRunning)
                {
                    continue;
                }

                if (fish.IsFullyOutsidePaddedView())
                {
                    fishObject.SetActive(false);
                    continue;
                }

                fish.ForceLevelTransitionNaturalExit(speedMultiplier);
            }
        }

        for (int i = 0; i < spawnedGimmicks.Count; i++)
        {
            GameObject gimmick = spawnedGimmicks[i];
            if (gimmick == null || !gimmick.activeSelf ||
                !gimmick.TryGetComponent<FishScript>(out FishScript fish) ||
                !fish.ShouldBlockLevelTransition ||
                fish.IsCinematicDeathRunning)
            {
                continue;
            }

            if (fish.IsFullyOutsidePaddedView())
            {
                Destroy(gimmick);
                continue;
            }

            fish.ForceLevelTransitionNaturalExit(speedMultiplier);
        }
    }

    private void DisableFishAlreadyFullyOutside()
    {
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;

            for (int i = 0; i < pool.Count; i++)
            {
                GameObject fishObject = pool[i];
                if (fishObject == null || !fishObject.activeSelf)
                {
                    continue;
                }

                FishScript fish = fishObject.GetComponent<FishScript>();
                if (fish == null)
                {
                    fishObject.SetActive(false);
                    continue;
                }

                if (!fish.IsCinematicDeathRunning &&
                    fish.IsFullyOutsidePaddedView())
                {
                    fishObject.SetActive(false);
                }
            }
        }

        for (int i = spawnedGimmicks.Count - 1; i >= 0; i--)
        {
            GameObject gimmick = spawnedGimmicks[i];
            if (gimmick == null)
            {
                spawnedGimmicks.RemoveAt(i);
                continue;
            }

            if (!gimmick.activeSelf)
            {
                Destroy(gimmick);
                spawnedGimmicks.RemoveAt(i);
                continue;
            }

            FishScript fish = gimmick.GetComponent<FishScript>();
            if (fish != null && !fish.IsCinematicDeathRunning &&
                fish.IsFullyOutsidePaddedView())
            {
                Destroy(gimmick);
                spawnedGimmicks.RemoveAt(i);
            }
        }

        RemoveInactiveTargetBossReferences();
    }

    private void RemoveInactiveTargetBossReferences()
    {
        for (int i = activeTargetBosses.Count - 1; i >= 0; i--)
        {
            GameObject boss = activeTargetBosses[i];

            if (boss == null || !boss.activeInHierarchy)
            {
                activeTargetBosses.RemoveAt(i);
            }
        }
    }

    private int CountFishStillInsidePaddedBounds()
    {
        int count = 0;
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject fishObject = pool[i];
                if (fishObject == null || !fishObject.activeSelf)
                {
                    continue;
                }

                FishScript fish = fishObject.GetComponent<FishScript>();
                if (fish == null)
                {
                    fishObject.SetActive(false);
                    continue;
                }

                if (fish.IsCinematicDeathRunning)
                {
                    count++;
                }
                else if (fish.ShouldBlockLevelTransition &&
                         !fish.IsFullyOutsidePaddedView())
                {
                    count++;
                }
            }
        }

        for (int i = 0; i < spawnedGimmicks.Count; i++)
        {
            GameObject gimmick = spawnedGimmicks[i];
            if (gimmick == null || !gimmick.activeSelf ||
                !gimmick.TryGetComponent<FishScript>(out FishScript fish))
            {
                continue;
            }

            if (fish.IsCinematicDeathRunning)
            {
                count++;
            }
            else if (fish.ShouldBlockLevelTransition &&
                     !fish.IsFullyOutsidePaddedView())
            {
                count++;
            }
        }

        return count;
    }

    private void BeginBossTimeoutWarningForActiveBosses()
    {
        Debug.LogWarning(
            "[Boss Timeout] Warning phase started. Boss routes now favor the center."
        );

        for (int i = 0; i < activeTargetBosses.Count; i++)
        {
            GameObject bossObject = activeTargetBosses[i];
            FishScript boss = bossObject != null
                ? bossObject.GetComponent<FishScript>()
                : null;
            if (boss != null && boss.IsAliveTarget)
            {
                boss.BeginBossTimeoutWarning(forcedBossCenterRouteChance);
            }
        }
    }

    private void BeginBossTimeoutResolution()
    {
        Debug.LogWarning(
            "[Boss Timeout] Maximum duration reached. Behavior: " +
            bossTimeoutBehavior
        );

        if (bossTimeoutBehavior == BossTimeoutBehavior.FinalVisiblePhaseThenEscape)
        {
            BeginBossTimeoutWarningForActiveBosses();
        }

        if (bossTimeoutBehavior == BossTimeoutBehavior.GraduallyReduceHealthThenEscape)
        {
            // Give the health-drain phase part of the grace period before
            // requesting a visible escape. The hard timeout below remains a
            // safety net only.
            return;
        }

        // EscapeAndProgress, FinalVisiblePhaseThenEscape, and
        // ForceCompleteAfterGrace all attempt a visible route first. The final
        // grace timeout only force-resolves a boss that genuinely cannot exit.
        RequestActiveBossTimeoutEscape();
    }

    private void DrainActiveBossHealthForTimeout(float deltaTime)
    {
        bool anyAboveMinimum = false;
        for (int i = 0; i < activeTargetBosses.Count; i++)
        {
            GameObject bossObject = activeTargetBosses[i];
            FishScript boss = bossObject != null
                ? bossObject.GetComponent<FishScript>()
                : null;
            if (boss == null || !boss.IsAliveTarget)
            {
                continue;
            }

            boss.ApplyBossTimeoutHealthDrain(
                timeoutHealthDrainPerSecond * Mathf.Max(0f, deltaTime)
            );
            anyAboveMinimum |= boss.CurrentHealth > 1.01f;
        }

        if (!anyAboveMinimum)
        {
            RequestActiveBossTimeoutEscape();
        }
    }

    private void RequestActiveBossTimeoutEscape()
    {
        if (bossTimeoutEscapeRequestedThisBatch)
        {
            return;
        }

        bossTimeoutEscapeRequestedThisBatch = true;

        bool countAsDefeat =
            bossTimeoutOutcome != BossTimeoutOutcome.Escaped;
        float partialReward =
            bossTimeoutOutcome == BossTimeoutOutcome.PartialReward
                ? timeoutPartialRewardPercent
                : 0f;

        for (int i = 0; i < activeTargetBosses.Count; i++)
        {
            GameObject bossObject = activeTargetBosses[i];
            FishScript boss = bossObject != null
                ? bossObject.GetComponent<FishScript>()
                : null;
            if (boss != null && boss.IsAliveTarget)
            {
                boss.RequestBossTimeoutEscape(
                    Random.Range(0f, 0.75f),
                    bossEscapeSpeedMultiplier,
                    countAsDefeat,
                    partialReward
                );
            }
        }
    }

    private void RequestEmergencyNaturalBossExits()
    {
        bossTimeoutEscapeRequestedThisBatch = true;

        Debug.LogWarning(
            "[Boss Timeout] Grace period ended. Remaining bosses are now " +
            "using a faster visible exit and will resolve only after their " +
            "full visual bounds are outside the padded screen."
        );

        float emergencySpeed = Mathf.Max(
            bossEscapeSpeedMultiplier,
            bossEscapeSpeedMultiplier * 1.75f
        );

        for (int i = activeTargetBosses.Count - 1; i >= 0; i--)
        {
            GameObject bossObject = activeTargetBosses[i];
            FishScript boss = bossObject != null
                ? bossObject.GetComponent<FishScript>()
                : null;

            if (boss == null || !boss.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool countAsDefeat =
                bossTimeoutOutcome != BossTimeoutOutcome.Escaped;
            float partialReward =
                bossTimeoutOutcome == BossTimeoutOutcome.PartialReward
                    ? timeoutPartialRewardPercent
                    : 0f;

            boss.RequestBossTimeoutEscape(
                0f,
                emergencySpeed,
                countAsDefeat,
                partialReward
            );

            // This no longer deactivates the boss immediately. FishScript
            // converts it to a deterministic natural exit and reports the
            // retreat only after the entire boss is beyond padded bounds.
            boss.ForceResolveBossTimeoutEscape();
        }
    }

    private IEnumerator TransitionBackground(Sprite newSprite)
    {
        if (backgroundRenderer == null || newSprite == null)
        {
            yield break;
        }

        GameObject temporaryBackground = new GameObject("TempBG_Fade");
        temporaryBackground.transform.position = backgroundRenderer.transform.position;
        temporaryBackground.transform.rotation = backgroundRenderer.transform.rotation;
        temporaryBackground.transform.localScale = backgroundRenderer.transform.localScale;

        SpriteRenderer temporaryRenderer =
            temporaryBackground.AddComponent<SpriteRenderer>();

        temporaryRenderer.sprite = backgroundRenderer.sprite;
        temporaryRenderer.sortingLayerID = backgroundRenderer.sortingLayerID;
        temporaryRenderer.sortingOrder = backgroundRenderer.sortingOrder + 1;
        temporaryRenderer.color = backgroundRenderer.color;

        backgroundRenderer.sprite = newSprite;
        FitSpriteToScreen(backgroundRenderer);

        float fadeDuration = Mathf.Max(0.10f, backgroundTransitionFadeDuration);
        float timer = 0f;
        Color startColor = temporaryRenderer.color;

        while (timer < fadeDuration)
        {
            if (waitForCinematicDeathsBeforeLevelChange &&
                SpecialFishCinematicDeathController.AnySequenceRunning)
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

            temporaryRenderer.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                alpha
            );

            yield return null;
        }

        Destroy(temporaryBackground);
    }

    private IEnumerator RunLevelOpeningParadeRoutine(GameLevel level)
    {
        openingParadePresentationRunning = true;

        if (!BeginParadeSession("Opening Parade - " + level))
        {
            openingParadePresentationRunning = false;
            yield break;
        }

        // Remove old-level ambient traffic first so the opening parade is the
        // first visible fish event of the new level.
        yield return StartCoroutine(PrepareParadeStageRoutine(true));

        yield return new WaitForSeconds(
            Mathf.Max(0f, levelOpeningParadeDelay)
        );

        int waveCount = Mathf.Clamp(
            levelOpeningParadeWaveCount,
            1,
            3
        );

        Debug.Log(
            "<color=#80DEEA>[LEVEL OPENING PARADE]</color> " +
            waveCount + " organized opening wave" +
            (waveCount > 1 ? "s" : "") + "."
        );

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            SpawnLevelOpeningParadeWave(level, waveIndex);

            if (waveIndex < waveCount - 1)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0.1f, levelOpeningParadeWaveGap)
                );
            }
        }

        MarkParadeSpawningComplete();
        yield return StartCoroutine(WaitForParadeCompletionRoutine());

        openingParadePresentationRunning = false;
        ambientSpawnTimer = Mathf.Max(ambientSpawnTimer, 0.35f);
        paradeCooldownTimer = Mathf.Max(paradeCooldownTimer, 2.0f);
    }

    private void SpawnLevelOpeningParadeWave(
        GameLevel level,
        int waveIndex
    )
    {
        int available = Mathf.Max(
            0,
            GetMixedParadeFishCapacityLimit() - CountActiveFish()
        );

        if (available <= 0)
        {
            return;
        }

        int fishIndex = SelectCrowdFishIndex(level, false);
        if (fishIndex < 0)
        {
            fishIndex = GetSmallFishIndex();
        }

        int requested = Mathf.Clamp(
            levelOpeningParadeFishCountBase +
            (int)level * levelOpeningParadeFishPerLevel,
            5,
            24
        );
        int fishCount = Mathf.Min(requested, available);
        if (fishCount <= 0)
        {
            return;
        }

        ParadeEntrySide entrySide;
        int sideCycle = (waveIndex + (int)level) % 4;
        switch (sideCycle)
        {
            case 1:
                entrySide = ParadeEntrySide.Right;
                break;
            case 2:
                entrySide = ParadeEntrySide.Top;
                break;
            case 3:
                entrySide = ParadeEntrySide.Bottom;
                break;
            default:
                entrySide = ParadeEntrySide.Left;
                break;
        }

        bool vertical = entrySide == ParadeEntrySide.Top ||
                        entrySide == ParadeEntrySide.Bottom;
        float lane = vertical
            ? Mathf.Lerp(-1.15f, 1.15f, (waveIndex + 1f) / (GetSafeWaveCount(levelOpeningParadeWaveCount) + 1f))
            : Mathf.Lerp(-1.25f, 1.25f, (waveIndex + 1f) / (GetSafeWaveCount(levelOpeningParadeWaveCount) + 1f));

        OrganizedParadePattern pattern = SelectOrganizedPattern(
            levelOpeningParadePatterns
        );

        SpawnOrganizedParadeGroup(
            fishIndex,
            entrySide,
            lane,
            fishCount,
            pattern,
            false,
            Mathf.Clamp(levelOpeningParadeSpeedMultiplier, 0.45f, 1.4f)
        );
    }

    private static int GetSafeWaveCount(int value)
    {
        return Mathf.Max(1, value);
    }

    private IEnumerator RunStandaloneFeatureParadeRoutine(GameLevel level)
    {
        if (!BeginParadeSession("Standalone Feature Parade"))
        {
            yield break;
        }

        yield return StartCoroutine(PrepareParadeStageRoutine(false));

        yield return new WaitForSeconds(
            Mathf.Max(0f, standaloneFeatureParadeDelay)
        );

        int waveCount = Mathf.Clamp(
            standaloneFeatureParadeWaveCount,
            1,
            3
        );

        Debug.Log(
            "<color=#FFD180>[STANDALONE PARADE]</color> " +
            "Showcase parade playing without a boss."
        );

        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            SpawnStandaloneFeatureParadeWave(level, waveIndex);

            if (waveIndex < waveCount - 1)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0.1f, standaloneFeatureParadeWaveGap)
                );
            }
        }

        MarkParadeSpawningComplete();
        yield return StartCoroutine(WaitForParadeCompletionRoutine());
    }

    private void SpawnStandaloneFeatureParadeWave(
        GameLevel level,
        int waveIndex
    )
    {
        int available = Mathf.Max(
            0,
            GetMixedParadeFishCapacityLimit() - CountActiveFish()
        );

        if (available <= 0)
        {
            return;
        }

        int requested = Mathf.Clamp(
            standaloneFeatureParadeFishCountBase +
            (int)level * standaloneFeatureParadeFishPerLevel,
            6,
            28
        );
        int fishCount = Mathf.Min(requested, available);

        if (fishCount <= 0)
        {
            return;
        }

        if (Random.value <= standaloneRoyalEscortChance && fishCount >= 6)
        {
            SpawnRoyalArmadaSignature(
                fishCount,
                waveIndex + (int)level
            );
            return;
        }

        int fishIndex = SelectCrowdFishIndex(level, true);
        if (fishIndex < 0)
        {
            fishIndex = GetSmallFishIndex();
        }

        ParadeEntrySide entrySide = GetRandomPreBossParadeEntrySide(level);
        bool vertical = entrySide == ParadeEntrySide.Top ||
                        entrySide == ParadeEntrySide.Bottom;
        float lane = KeepParadeLaneOutsideCenter(
            Random.Range(-1.5f, 1.5f),
            vertical,
            fishIndex + waveIndex
        );

        SpawnOrganizedParadeGroup(
            fishIndex,
            entrySide,
            lane,
            fishCount,
            SelectOrganizedPattern(standaloneFeatureParadePatterns),
            false,
            0.92f
        );
    }

    [ContextMenu("Play Standalone Showcase Parade (Play Mode)")]
    public void PlayStandaloneShowcaseParade()
    {
        if (!Application.isPlaying ||
            !gameObject.activeInHierarchy ||
            !ReferencesAreReady())
        {
            return;
        }

        StartCoroutine(
            RunStandaloneFeatureParadeRoutine(currentLevelState)
        );
    }

    private void TriggerLevelSpecificSpawn()
    {
        if (CountActiveFish() >= GetMaxActiveFish())
        {
            return;
        }

        int smallFishIndex = GetSmallFishIndex();

        switch (currentLevelState)
        {
            case GameLevel.Level1_Beginner:
                // A beginner parade now reads as one coherent school: one
                // species, one leader, shared heading, and size-aware spacing.
                SpawnLevelNaturalSchool(smallFishIndex, false);
                break;

            case GameLevel.Level2_School:
                // A larger polarized school replaces the rigid V-only layout.
                SpawnLevelNaturalSchool(smallFishIndex, true);
                TrySpawnFeatureMiniBoss(0.12f);
                break;

            case GameLevel.Level3_Circle:
                // Signature: a moving halo instead of a static world-center orbit.
                SpawnTreasureRingParade(
                    smallFishIndex,
                    Mathf.Min(
                        10 + GetFormationGrowth(),
                        GetMaxActiveFish() - CountActiveFish()
                    ),
                    Random.value < 0.5f
                );
                TrySpawnGimmick(0.18f);
                TrySpawnFeatureMiniBoss(0.16f);
                break;

            case GameLevel.Level4_Cross:
                // Signature: synchronized attacks from all four edges.
                SpawnCrossAttackParade();
                TrySpawnGimmick(0.20f);
                TrySpawnFeatureMiniBoss(0.18f);
                break;

            case GameLevel.Level5_Festival:
                // Signature: two mirrored currents weaving across one another.
                SpawnTwinCurrentSignature(
                    Mathf.Min(20, GetMaxActiveFish() - CountActiveFish()),
                    Random.Range(0, 2)
                );

                TrySpawnGimmick(0.35f);
                TrySpawnFeatureMiniBoss(0.22f);
                break;

            case GameLevel.Level6_BossOcean:
                // Signature: a valuable royal leader protected by live guards.
                SpawnRoyalArmadaSignature(
                    Mathf.Min(24, GetMaxActiveFish() - CountActiveFish()),
                    Random.Range(0, 2)
                );

                TrySpawnGimmick(0.30f);
                TrySpawnFeatureMiniBoss(0.25f);
                break;
        }
    }

    private IEnumerator RunPreBossRoyalParadeRoutine(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        if (!BeginParadeSession(
                "Pre-Boss Parade " + (bossSequenceIndex + 1)
            ))
        {
            yield break;
        }

        yield return StartCoroutine(PrepareParadeStageRoutine(false));

        int waveCount = GetPreBossParadeWaveCount(
            level,
            bossSequenceIndex
        );

        bool isFirstMainBossSequence = bossSequenceIndex == 0;
        if (prioritizeMainBossPresence && isFirstMainBossSequence)
        {
            waveCount = Mathf.Min(
                waveCount,
                Mathf.Clamp(firstMainBossParadeWaveCap, 0, 2)
            );
        }

        Debug.Log(
            "<color=#CE93D8>[ROYAL PARADE]</color> " +
            waveCount + " parade waves before boss target " +
            (bossSequenceIndex + 1) + "."
        );

        for (int waveIndex = 0;
             waveIndex < waveCount;
             waveIndex++)
        {
            yield return StartCoroutine(
                WaitForPreBossParadeSpace()
            );

            SpawnPreBossParadeWave(
                level,
                waveIndex
            );

            float paradeGap = GetPreBossParadeGap(
                level,
                waveIndex
            );

            if (prioritizeMainBossPresence && isFirstMainBossSequence)
            {
                paradeGap = Mathf.Min(
                    paradeGap,
                    Mathf.Max(0f, firstMainBossParadeGapCap)
                );
            }

            yield return new WaitForSeconds(paradeGap);
        }

        MarkParadeSpawningComplete();
        yield return StartCoroutine(WaitForParadeCompletionRoutine());

        if (!(prioritizeMainBossPresence &&
              isFirstMainBossSequence &&
              skipMiniBossBeforeFirstMainBoss) &&
            ShouldSpawnPreBossMiniBoss(
                level,
                bossSequenceIndex
            ))
        {
            int miniBossCount =
                GetPreBossMiniBossCount(level);

            Debug.Log(
                "<color=#BA68C8>[MINI BOSS HERALD]</color> " +
                miniBossCount +
                " mini boss" +
                (miniBossCount > 1 ? "es" : "") +
                " entering before the main boss."
            );

            for (int miniBossNumber = 0;
                 miniBossNumber < miniBossCount;
                 miniBossNumber++)
            {
                int miniBossIndex =
                    GetRandomMiniBossIndex();

                if (miniBossIndex >= 0)
                {
                    SpawnMiniBoss(
                        miniBossIndex
                    );
                }

                if (miniBossNumber <
                    miniBossCount - 1)
                {
                    yield return new WaitForSeconds(
                        delayBetweenMiniBosses
                    );
                }
            }

            yield return new WaitForSeconds(
                GetPreBossMiniBossLeadDelay(level)
            );
        }

        float finalPause = Mathf.Max(0f, GetPreBossFinalPause(level));
        if (prioritizeMainBossPresence && isFirstMainBossSequence)
        {
            finalPause = Mathf.Min(finalPause, 0.20f);
        }

        yield return new WaitForSeconds(finalPause);
    }

    private IEnumerator WaitForPreBossParadeSpace()
    {
        float elapsed = 0f;

        float maximumWait = Mathf.Min(
            preBossSpaceWaitTimeout,
            1.5f
        );

        int paradeActiveLimit =
            GetMaxActiveFish() +
            preBossExtraFishCapacity;

        while (CountActiveFish() >= paradeActiveLimit &&
               elapsed < maximumWait)
        {
            elapsed += 0.25f;

            yield return new WaitForSeconds(0.25f);
        }
    }


    private IEnumerator RunBossArrivalEvent(
        GameLevel level,
        bool isFirstMainBossBatch
    )
    {
        if (!enableBossArrivalEvents)
        {
            yield break;
        }

        BossArrivalEventType selectedEvent = SelectBossArrivalEvent();

        Transform shakeTransform = cameraShakeTarget;

        if (shakeTransform == null &&
            Camera.main != null)
        {
            shakeTransform = Camera.main.transform;
        }

        Vector3 originalCameraLocalPosition =
            shakeTransform != null
                ? shakeTransform.localPosition
                : Vector3.zero;

        Color originalBackgroundColor =
            backgroundRenderer != null
                ? backgroundRenderer.color
                : Color.white;

        Color eventColor = GetBossArrivalEventColor(selectedEvent);

        eventColor.a =
            originalBackgroundColor.a;

        float duration =
            bossEventBaseDuration +
            (int)level *
            bossEventDurationPerLevel;

        if (prioritizeMainBossPresence && isFirstMainBossBatch)
        {
            duration = Mathf.Min(
                duration,
                Mathf.Max(0.05f, firstMainBossArrivalEventDurationCap)
            );
        }

        float shakeStrength =
            bossEventBaseShakeStrength +
            (int)level *
            bossEventShakeStrengthPerLevel;

        Debug.Log(
            "<color=#FF5252>[BOSS EVENT]</color> " +
            selectedEvent.ToString()
        );

        if (GameManager.Instance != null &&
            GameManager.Instance.SoundManager != null)
        {
            GameManager.Instance.SoundManager.PlayBossArrivalSound(
                (int)selectedEvent
            );
        }

        if (!(prioritizeMainBossPresence &&
              isFirstMainBossBatch &&
              skipFirstMainBossRushFish))
        {
            SpawnBossEventRushFish(level, selectedEvent);
        }

        SpawnOptionalBossEventEffect(selectedEvent, duration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsed / duration
            );

            float remainingStrength =
                1f - normalizedTime;

            float pulse =
                (Mathf.Sin(elapsed * 18f) + 1f) * 0.5f;

            if (shakeTransform != null)
            {
                Vector2 randomShake =
                    Random.insideUnitCircle *
                    shakeStrength *
                    remainingStrength;

                shakeTransform.localPosition =
                    originalCameraLocalPosition +
                    new Vector3(
                        randomShake.x,
                        randomShake.y,
                        0f
                    );
            }

            if (backgroundRenderer != null)
            {
                float tintAmount =
                    pulse *
                    bossEventBackgroundTintStrength *
                    remainingStrength;

                backgroundRenderer.color = Color.Lerp(
                    originalBackgroundColor,
                    eventColor,
                    tintAmount
                );
            }

            yield return null;
        }

        if (shakeTransform != null)
        {
            shakeTransform.localPosition =
                originalCameraLocalPosition;
        }

        if (backgroundRenderer != null)
        {
            backgroundRenderer.color =
                originalBackgroundColor;
        }
    }

    private Color GetBossArrivalEventColor(BossArrivalEventType eventType)
    {
        switch (eventType)
        {
            case BossArrivalEventType.TidalSurge: return new Color(0.1f, 0.55f, 1f, 1f);
            case BossArrivalEventType.Earthquake: return new Color(0.75f, 0.7f, 0.55f, 1f);
            case BossArrivalEventType.LightningStorm: return new Color(0.65f, 0.45f, 1f, 1f);
            case BossArrivalEventType.GoldenFrenzy: return new Color(1f, 0.78f, 0.18f, 1f);
            case BossArrivalEventType.AbyssGate: return new Color(0.3f, 0.08f, 0.42f, 1f);
            case BossArrivalEventType.TreasureCurrent: return new Color(0.15f, 0.85f, 0.72f, 1f);
            default: return Color.white;
        }
    }

    private BossArrivalEventType SelectBossArrivalEvent()
    {
        if (weightedBossArrivalEvents == null || weightedBossArrivalEvents.Length == 0)
        {
            int count = System.Enum.GetValues(typeof(BossArrivalEventType)).Length;
            int pick = Random.Range(0, count);
            if (count > 1 && pick == (int)lastBossArrivalEvent) pick = (pick + 1) % count;
            lastBossArrivalEvent = (BossArrivalEventType)pick;
            return lastBossArrivalEvent;
        }
        float total = 0f;
        for (int i = 0; i < weightedBossArrivalEvents.Length; i++)
        {
            if (weightedBossArrivalEvents[i].eventType == lastBossArrivalEvent && weightedBossArrivalEvents.Length > 1) continue;
            total += Mathf.Max(0f, weightedBossArrivalEvents[i].weight);
        }
        if (total <= 0f) { lastBossArrivalEvent = BossArrivalEventType.TidalSurge; return lastBossArrivalEvent; }
        float roll = Random.value * total;
        for (int i = 0; i < weightedBossArrivalEvents.Length; i++)
        {
            WeightedBossArrivalEvent item = weightedBossArrivalEvents[i];
            if (item.eventType == lastBossArrivalEvent && weightedBossArrivalEvents.Length > 1) continue;
            roll -= Mathf.Max(0f, item.weight);
            if (roll <= 0f) { lastBossArrivalEvent = item.eventType; return item.eventType; }
        }
        lastBossArrivalEvent = weightedBossArrivalEvents[0].eventType;
        return lastBossArrivalEvent;
    }

    private void SpawnBossEventRushFish(
        GameLevel level,
        BossArrivalEventType eventType
    )
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int levelIndex = (int)level;
        int baseFishTotal = 10 + levelIndex * 4;
        int desiredFishTotal =
            baseFishTotal * bossArrivalRushFishMultiplier;

        int availableSlots = Mathf.Max(
            0,
            GetBossFishLimit() - CountActiveFish()
        );

        int totalFish = Mathf.Min(
            desiredFishTotal,
            availableSlots
        );

        if (totalFish <= 0)
        {
            return;
        }

        Debug.Log(
            "<color=#EF5350>[BOSS PANIC RUSH]</color> " +
            totalFish +
            " mixed fish rushing before the boss."
        );

        if (eventType == BossArrivalEventType.GoldenFrenzy ||
            eventType == BossArrivalEventType.TreasureCurrent)
        {
            SpawnTreasureRingParade(
                SelectCrowdFishIndex(level, false),
                totalFish,
                Random.value < 0.5f
            );

            return;
        }

        if (eventType == BossArrivalEventType.LightningStorm ||
            eventType == BossArrivalEventType.AbyssGate)
        {
            SpawnMirroredFleetParade(
                SelectCrowdFishIndex(level, false),
                SelectCrowdFishIndex(level, false),
                totalFish
            );

            return;
        }

        int groupCount = Mathf.Clamp(
            Mathf.CeilToInt(totalFish / 8f),
            2,
            7
        );

        int remainingFish = totalFish;

        for (int groupIndex = 0;
             groupIndex < groupCount;
             groupIndex++)
        {
            int remainingGroups = groupCount - groupIndex;
            int fishInGroup = Mathf.CeilToInt(
                remainingFish / (float)remainingGroups
            );

            bool fromLeft = groupIndex % 2 == 0;
            int fishIndex = SelectCrowdFishIndex(level, true);

            float laneY = GetParadeGroupLaneY(
                groupIndex,
                groupCount,
                fishIndex
            );

            SpawnFastLineParadeGroup(
                fishIndex,
                laneY,
                fromLeft,
                fishInGroup,
                groupIndex % 2 == 0
                    ? FishScript.SwimStyle.FastTideExit
                    : FishScript.SwimStyle.ZigZagBurst
            );

            remainingFish -= fishInGroup;
        }
    }

    private void SpawnOptionalBossEventEffect(
        BossArrivalEventType eventType,
        float eventDuration
    )
    {
        int effectIndex = (int)eventType;
        EnsureBossEventEffectPools();

        if (bossArrivalEventEffects == null ||
            effectIndex < 0 ||
            effectIndex >= bossArrivalEventEffects.Length ||
            bossArrivalEventEffects[effectIndex] == null)
        {
            return;
        }

        GameObject eventEffect = GetBossEventEffect(effectIndex);

        if (eventEffect == null)
        {
            return;
        }

        PooledEffectToken token =
            eventEffect.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = eventEffect.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(eventEffect.transform);
        token.poolIndex = effectIndex;
        int version = ++token.playVersion;

        token.RestoreDefaultTransform(eventEffect.transform);
        eventEffect.transform.position =
            bossEventEffectSpawnPoint != null
                ? bossEventEffectSpawnPoint.position
                : Vector3.zero;

        eventEffect.SetActive(true);

        float effectDuration = RestartBossEventEffect(eventEffect);

        StartCoroutine(
            ReturnBossEventEffectToPool(
                eventEffect,
                version,
                Mathf.Max(eventDuration, effectDuration) + 0.25f
            )
        );
    }

    private GameObject GetBossEventEffect(int effectIndex)
    {
        List<GameObject> pool = bossEventEffectPools[effectIndex];

        for (int i = 0; i < pool.Count; i++)
        {
            GameObject candidate = pool[i];

            if (candidate != null && !candidate.activeSelf)
            {
                return candidate;
            }
        }

        Transform parent = bossEventEffectSpawnPoint != null
            ? bossEventEffectSpawnPoint
            : transform;

        GameObject instance = Instantiate(
            bossArrivalEventEffects[effectIndex],
            parent
        );

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        instance.SetActive(false);
        pool.Add(instance);
        return instance;
    }

    private IEnumerator ReturnBossEventEffectToPool(
        GameObject eventEffect,
        int version,
        float delay
    )
    {
        yield return new WaitForSeconds(Mathf.Max(0.02f, delay));

        if (eventEffect == null)
        {
            yield break;
        }

        PooledEffectToken token =
            eventEffect.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            ParticleSystem[] particles =
                eventEffect.GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            eventEffect.SetActive(false);
        }
    }

    private static float RestartBossEventEffect(GameObject root)
    {
        float longestDuration = 0.1f;
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);

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

            if (clips.Length > 0 && clips[0].clip != null)
            {
                longestDuration = Mathf.Max(
                    longestDuration,
                    clips[0].clip.length /
                    Mathf.Max(0.01f, animator.speed)
                );
            }
        }

        ParticleSystem[] particles =
            root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particle.Play(true);

            ParticleSystem.MainModule main = particle.main;
            longestDuration = Mathf.Max(
                longestDuration,
                main.duration + main.startLifetime.constantMax
            );
        }

        return longestDuration;
    }

    private void EnsureBossEventEffectPools()
    {
        int count = bossArrivalEventEffects == null
            ? 0
            : bossArrivalEventEffects.Length;

        while (bossEventEffectPools.Count < count)
        {
            bossEventEffectPools.Add(new List<GameObject>());
        }
    }

    private IEnumerator RunBossArrivalCrowdBuildUpRoutine(
        GameLevel level
    )
    {
        if (!enableBossArrivalCrowdBuildUp)
        {
            yield break;
        }

        int waveCount = Mathf.Clamp(
            bossArrivalCrowdBaseWaves + (int)level / 2,
            1,
            bossArrivalCrowdMaximumWaves
        );

        for (int waveIndex = 0;
             waveIndex < waveCount;
             waveIndex++)
        {
            int availableSlots = Mathf.Max(
                0,
                GetBossFishLimit() - CountActiveFish()
            );

            if (availableSlots <= 0)
            {
                yield break;
            }

            int desiredFish =
                bossArrivalCrowdBaseFishPerWave +
                (int)level * bossArrivalCrowdFishPerLevel;

            int reservedEscortSlots = Mathf.Min(
                6,
                Mathf.Max(0, availableSlots / 4)
            );

            int mainWaveFish = Mathf.Min(
                desiredFish,
                Mathf.Max(1, availableSlots - reservedEscortSlots)
            );

            SpawnBossArrivalCrowdWave(
                level,
                waveIndex,
                mainWaveFish
            );

            TrySpawnBossArrivalSpecialEscort(
                level,
                waveIndex
            );

            // Also wait after the final crowd wave so the fish are visible
            // before the main boss enters.
            yield return new WaitForSeconds(
                bossArrivalCrowdWaveGap
            );
        }
    }

    private void TrySpawnBossArrivalSpecialEscort(
        GameLevel level,
        int waveIndex
    )
    {
        float specialChance = Mathf.Clamp01(
            bossArrivalSpecialChanceBase +
            (int)level * bossArrivalSpecialChancePerLevel
        );

        if (Random.value > specialChance)
        {
            return;
        }

        int specialFishIndex = GetSpecialFishIndex(true);

        if (specialFishIndex < 0)
        {
            return;
        }

        int availableSlots = Mathf.Max(
            0,
            GetBossFishLimit() - CountActiveFish()
        );

        int escortCount = Mathf.Min(
            3 + (int)level / 2,
            availableSlots
        );

        if (escortCount <= 0)
        {
            return;
        }

        SpawnRoyalArmadaSignature(
            escortCount + 1,
            waveIndex
        );
    }

    private void SpawnBossArrivalCrowdWave(
        GameLevel level,
        int waveIndex,
        int totalFish
    )
    {
        if (totalFish <= 0)
        {
            return;
        }

        int pattern = waveIndex % 3;

        if (pattern == 0)
        {
            SpawnMirroredFleetParade(
                SelectCrowdFishIndex(level, false),
                SelectCrowdFishIndex(level, false),
                totalFish
            );
        }
        else if (pattern == 1)
        {
            SpawnTreasureRingParade(
                SelectCrowdFishIndex(level, false),
                totalFish,
                Random.value < 0.5f
            );
        }
        else
        {
            int groupCount = Mathf.Clamp(
                Mathf.CeilToInt(totalFish / 8f),
                2,
                5
            );

            int remaining = totalFish;

            for (int groupIndex = 0;
                 groupIndex < groupCount;
                 groupIndex++)
            {
                int groupsLeft = groupCount - groupIndex;
                int groupFish = Mathf.CeilToInt(
                    remaining / (float)groupsLeft
                );

                int fishIndex = SelectCrowdFishIndex(level, true);
                bool fromLeft = groupIndex % 2 == 0;
                float laneY = GetParadeGroupLaneY(
                    groupIndex,
                    groupCount,
                    fishIndex
                );

                if (groupIndex % 2 == 0)
                {
                    SpawnFastVParadeGroup(
                        fishIndex,
                        laneY,
                        fromLeft,
                        groupFish
                    );
                }
                else
                {
                    SpawnFastSchoolParadeGroup(
                        fishIndex,
                        laneY,
                        fromLeft,
                        groupFish
                    );
                }

                remaining -= groupFish;
            }
        }
    }

    // Image-inspired parade: one moving leader with fish arranged in
    // circular treasure rings around it.
    private void SpawnTreasureRingParade(
        int fishIndex,
        int totalFish,
        bool fromLeft
    )
    {
        if (!ReferencesAreReady() || totalFish <= 0)
        {
            return;
        }

        float laneY = KeepParadeLaneOutsideCenter(
            Random.Range(-1.8f, 1.8f),
            false,
            fishIndex + totalFish
        );

        SpawnDirectionalTreasureRingParade(
            fishIndex,
            totalFish,
            fromLeft
                ? ParadeEntrySide.Left
                : ParadeEntrySide.Right,
            laneY,
            true
        );
    }

    // Image-inspired parade: two organized fleets enter from opposite
    // sides in rows and cross through the battlefield.
    private void SpawnMirroredFleetParade(
        int leftFishIndex,
        int rightFishIndex,
        int totalFish
    )
    {
        if (!ReferencesAreReady() || totalFish <= 0)
        {
            return;
        }

        int leftCount = Mathf.CeilToInt(totalFish * 0.5f);
        int rightCount = totalFish - leftCount;

        SpawnMirroredFleetSide(
            leftFishIndex,
            leftCount,
            true
        );

        SpawnMirroredFleetSide(
            rightFishIndex,
            rightCount,
            false
        );
    }

    private void SpawnMirroredFleetSide(
        int fishIndex,
        int fishCount,
        bool fromLeft
    )
    {
        if (fishCount <= 0)
        {
            return;
        }

        int rows = Mathf.Clamp(
            mirroredFleetRows,
            2,
            Mathf.Max(2, fishCount)
        );

        Vector3 sidePosition =
            fromLeft ? LeftPos.position : RightPos.position;

        Vector3 targetPosition =
            fromLeft ? RightPos.position : LeftPos.position;

        Vector3 trailingDirection =
            fromLeft ? Vector3.left : Vector3.right;
        float rowHalfSpan = (rows - 1) * 0.5f * 1.05f;
        float innerFleetEdge = KeepParadeLaneOutsideCenter(
            fromLeft ? 0.01f : -0.01f,
            false,
            fishIndex + fishCount + (fromLeft ? 0 : 1)
        );
        float fleetLaneCenter = innerFleetEdge +
            Mathf.Sign(innerFleetEdge) * rowHalfSpan;
        targetPosition += new Vector3(0f, fleetLaneCenter, 0f);

        for (int i = 0; i < fishCount; i++)
        {
            int row = i % rows;
            int column = i / rows;

            float centeredRow =
                row - (rows - 1) * 0.5f;

            Vector3 spawnPosition =
                sidePosition +
                trailingDirection *
                (column * paradeFishHorizontalGap) +
                new Vector3(
                    0f,
                    fleetLaneCenter + centeredRow * 1.05f,
                    0f
                );

            SpawnFastParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                row % 2 == 0
                    ? FishScript.SwimStyle.LaneGlide
                    : FishScript.SwimStyle.ArcSweep
            );
        }
    }

    private void SpawnPreBossParadeWave(
        GameLevel level,
        int waveIndex
    )
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int paradeActiveLimit = Mathf.Min(
            GetMaxActiveFish() + preBossExtraFishCapacity,
            absoluteMaximumActiveFish
        );

        int availableSlots = Mathf.Max(
            0,
            paradeActiveLimit - CountActiveFish()
        );

        if (availableSlots <= 0)
        {
            return;
        }

        int targetFishPerGroup = GetParadeFishPerGroup(level);

        if (useDistinctLevelParadeSignatures)
        {
            SpawnSignaturePreBossParade(
                level,
                waveIndex,
                availableSlots,
                targetFishPerGroup
            );
            return;
        }

        int showcasePattern = Random.Range(0, 6);

        // Two extra image-inspired parade styles.
        if (showcasePattern == 4)
        {
            SpawnTreasureRingParade(
                SelectCrowdFishIndex(level, waveIndex > 0),
                Mathf.Min(
                    availableSlots,
                    targetFishPerGroup * 2
                ),
                Random.value < 0.5f
            );

            return;
        }

        if (showcasePattern == 5)
        {
            SpawnMirroredFleetParade(
                SelectCrowdFishIndex(level, waveIndex > 0),
                SelectCrowdFishIndex(level, true),
                Mathf.Min(
                    availableSlots,
                    targetFishPerGroup * 2
                )
            );

            return;
        }

        int groupCount = GetConcurrentParadeGroupCount(
            level,
            waveIndex
        );

        groupCount = Mathf.Min(
            groupCount,
            Mathf.Max(1, availableSlots / 3)
        );

        int fishPerGroup = Mathf.Clamp(
            availableSlots / groupCount,
            3,
            targetFishPerGroup
        );

        Debug.Log(
            "<color=#80DEEA>[4-DIRECTION PARADE]</color> " +
            groupCount + " mixed groups, " +
            fishPerGroup + " fish per group."
        );

        for (int groupIndex = 0;
             groupIndex < groupCount;
             groupIndex++)
        {
            ParadeEntrySide entrySide =
                GetRandomPreBossParadeEntrySide(level);

            // Every group uses one selected fish type, but different groups
            // can be small, medium, or occasional special fish.
            int fishIndex = SelectCrowdFishIndex(
                level,
                waveIndex > 0
            );

            float laneOffset = GetParadeGroupLaneOffset(
                groupIndex,
                groupCount,
                fishIndex,
                entrySide
            );

            int pattern = Random.Range(0, 4);

            switch (pattern)
            {
                case 0:
                    SpawnDirectionalLineParadeGroup(
                        fishIndex,
                        entrySide,
                        laneOffset,
                        fishPerGroup,
                        FishScript.SwimStyle.ArcSweep
                    );
                    break;

                case 1:
                    SpawnDirectionalVParadeGroup(
                        fishIndex,
                        entrySide,
                        laneOffset,
                        fishPerGroup
                    );
                    break;

                case 2:
                    SpawnDirectionalSchoolParadeGroup(
                        fishIndex,
                        entrySide,
                        laneOffset,
                        fishPerGroup
                    );
                    break;

                default:
                    SpawnDirectionalSnakeParadeGroup(
                        fishIndex,
                        entrySide,
                        laneOffset,
                        fishPerGroup
                    );
                    break;
            }
        }
    }

    private ParadeEntrySide GetRandomPreBossParadeEntrySide(
        GameLevel level
    )
    {
        bool verticalAvailable =
            TopPos != null && BottomPos != null;

        int levelIndex = Mathf.Clamp((int)level, 0, 5);
        float verticalChance = verticalPreBossParadeChance;

        if (verticalRouteChancePerLevel != null &&
            levelIndex < verticalRouteChancePerLevel.Length)
        {
            verticalChance = Mathf.Clamp01(
                verticalRouteChancePerLevel[levelIndex]
            );
        }

        if (verticalAvailable &&
            Random.value <= verticalChance)
        {
            return Random.value < 0.5f
                ? ParadeEntrySide.Top
                : ParadeEntrySide.Bottom;
        }

        return Random.value < 0.5f
            ? ParadeEntrySide.Left
            : ParadeEntrySide.Right;
    }

    private void SpawnSignaturePreBossParade(
        GameLevel level,
        int waveIndex,
        int availableSlots,
        int targetFishPerGroup
    )
    {
        int fishIndex = SelectCrowdFishIndex(
            level,
            (int)level >= (int)GameLevel.Level5_Festival
        );
        int signatureCount = Mathf.Min(
            availableSlots,
            Mathf.Max(3, targetFishPerGroup * 2)
        );

        switch (level)
        {
            case GameLevel.Level1_Beginner:
            {
                ParadeEntrySide side = waveIndex % 2 == 0
                    ? ParadeEntrySide.Left
                    : ParadeEntrySide.Right;
                float lane = KeepParadeLaneOutsideCenter(
                    waveIndex % 2 == 0 ? -1.25f : 1.25f,
                    false,
                    fishIndex + waveIndex
                );
                SpawnDirectionalNaturalSchoolGroup(
                    fishIndex,
                    side,
                    lane,
                    signatureCount,
                    true
                );
                break;
            }

            case GameLevel.Level2_School:
            {
                ParadeEntrySide side = GetRandomPreBossParadeEntrySide(level);
                SpawnDirectionalNaturalSchoolGroup(
                    fishIndex,
                    side,
                    KeepParadeLaneOutsideCenter(
                        waveIndex % 2 == 0 ? -0.9f : 0.9f,
                        side == ParadeEntrySide.Top ||
                        side == ParadeEntrySide.Bottom,
                        fishIndex + waveIndex
                    ),
                    signatureCount,
                    true
                );
                break;
            }

            case GameLevel.Level3_Circle:
            {
                SpawnDirectionalTreasureRingParade(
                    fishIndex,
                    signatureCount,
                    GetRandomPreBossParadeEntrySide(level),
                    waveIndex % 2 == 0 ? -1.25f : 1.25f,
                    true
                );
                break;
            }

            case GameLevel.Level4_Cross:
                SpawnCardinalCrossSignature(
                    signatureCount,
                    waveIndex
                );
                break;

            case GameLevel.Level5_Festival:
                SpawnTwinCurrentSignature(
                    signatureCount,
                    waveIndex
                );
                break;

            case GameLevel.Level6_BossOcean:
                SpawnRoyalArmadaSignature(
                    signatureCount,
                    waveIndex
                );
                break;
        }
    }

    private void SpawnDirectionalTreasureRingParade(
        int fishIndex,
        int totalFish,
        ParadeEntrySide entrySide,
        float laneOffset,
        bool enforceParadeParticipation
    )
    {
        if (totalFish <= 0 ||
            !TryGetDirectionalParadeRoute(
                entrySide,
                laneOffset,
                out Vector3 leaderPosition,
                out Vector3 targetPosition,
                out Vector3 trailingDirection,
                out Vector3 perpendicularDirection
            ))
        {
            return;
        }

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            return;
        }

        if (enforceParadeParticipation &&
            !leaderScript.CanJoinParade())
        {
            leader.SetActive(false);
            return;
        }

        ApplyParadeScaling(leaderScript);
        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.ArcSweep,
            paradeRouteSweepAmplitude * 0.70f,
            paradeRouteSweepFrequency * 0.72f
        );

        int followerCount = totalFish - 1;

        for (int followerIndex = 0;
             followerIndex < followerCount;
             followerIndex++)
        {
            int ring = 0;
            int indexInsideRing = followerIndex;
            int capacity = 8;

            while (indexInsideRing >= capacity)
            {
                indexInsideRing -= capacity;
                ring++;
                capacity += 4;
            }

            float radius = treasureRingInnerRadius +
                ring * treasureRingRadiusSpacing;
            float angle =
                indexInsideRing * 360f / capacity +
                ring * 18f;
            Vector3 localOffset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                0f
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                localOffset,
                targetPosition
            );
        }
    }

    private void SpawnCardinalCrossSignature(
        int totalFish,
        int waveIndex
    )
    {
        if (totalFish <= 0 || TopPos == null || BottomPos == null)
        {
            return;
        }

        ParadeEntrySide[] sides =
        {
            ParadeEntrySide.Left,
            ParadeEntrySide.Right,
            ParadeEntrySide.Top,
            ParadeEntrySide.Bottom
        };
        int remaining = totalFish;

        for (int sideIndex = 0;
             sideIndex < sides.Length && remaining > 0;
             sideIndex++)
        {
            int groupsLeft = sides.Length - sideIndex;
            int groupFish = Mathf.Max(
                1,
                Mathf.CeilToInt(remaining / (float)groupsLeft)
            );
            int fishIndex = SelectCrowdFishIndex(
                GameLevel.Level4_Cross,
                false
            );
            bool vertical = sides[sideIndex] == ParadeEntrySide.Top ||
                            sides[sideIndex] == ParadeEntrySide.Bottom;
            float lane = KeepParadeLaneOutsideCenter(
                ((sideIndex + waveIndex) & 1) == 0 ? -1.15f : 1.15f,
                vertical,
                fishIndex + sideIndex + waveIndex
            );

            SpawnDirectionalLineParadeGroup(
                fishIndex,
                sides[sideIndex],
                lane,
                groupFish,
                vertical
                    ? FishScript.SwimStyle.VerticalDive
                    : FishScript.SwimStyle.HorizontalRush
            );

            remaining -= groupFish;
        }
    }

    private void SpawnTwinCurrentSignature(
        int totalFish,
        int variant
    )
    {
        if (totalFish <= 0)
        {
            return;
        }

        bool vertical = TopPos != null && BottomPos != null &&
            (variant % 2 != 0 ||
             Random.value < GetLevelVerticalRouteChance(
                 GameLevel.Level5_Festival
             ));
        ParadeEntrySide firstSide = vertical
            ? ParadeEntrySide.Top
            : ParadeEntrySide.Left;
        ParadeEntrySide secondSide = vertical
            ? ParadeEntrySide.Bottom
            : ParadeEntrySide.Right;
        int firstCount = Mathf.CeilToInt(totalFish * 0.5f);
        int secondCount = totalFish - firstCount;
        int firstFish = SelectCrowdFishIndex(
            GameLevel.Level5_Festival,
            false
        );
        int secondFish = SelectCrowdFishIndex(
            GameLevel.Level5_Festival,
            false
        );

        SpawnDirectionalSnakeParadeGroup(
            firstFish,
            firstSide,
            -1.25f,
            firstCount
        );

        if (secondCount > 0)
        {
            SpawnDirectionalSnakeParadeGroup(
                secondFish,
                secondSide,
                1.25f,
                secondCount
            );
        }
    }

    private void SpawnRoyalArmadaSignature(
        int totalFish,
        int variant
    )
    {
        if (totalFish <= 0)
        {
            return;
        }

        int leaderFishIndex = GetSpecialFishIndex(true);

        if (leaderFishIndex < 0)
        {
            leaderFishIndex = GetMediumFishIndex();
        }

        ParadeEntrySide entrySide = GetRandomPreBossParadeEntrySide(
            GameLevel.Level6_BossOcean
        );
        bool vertical = entrySide == ParadeEntrySide.Top ||
                        entrySide == ParadeEntrySide.Bottom;
        float lane = KeepParadeLaneOutsideCenter(
            variant % 2 == 0 ? -1.1f : 1.1f,
            vertical,
            leaderFishIndex + variant
        );

        if (!TryGetDirectionalParadeRoute(
            entrySide,
            lane,
            out Vector3 leaderPosition,
            out Vector3 targetPosition,
            out Vector3 trailingDirection,
            out Vector3 perpendicularDirection
        ))
        {
            return;
        }

        GameObject leader = GetFishFromPool(
            leaderFishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            return;
        }

        ApplyParadeScaling(leaderScript);
        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.ArcSweep,
            paradeRouteSweepAmplitude * 0.55f,
            paradeRouteSweepFrequency * 0.65f
        );

        int guardCount = Mathf.Clamp(
            Mathf.Min(totalFish - 1, specialEscortGuardCount + variant),
            0,
            10
        );
        int shieldCount = Mathf.Max(2, Mathf.CeilToInt(guardCount * 0.45f));
        int guardFishIndex = GetSingleBossBodyGuardFishIndex();

        for (int guardIndex = 0;
             guardIndex < guardCount;
             guardIndex++)
        {
            FishScript.EscortRole role;
            Vector3 localOffset;

            if (guardIndex < shieldCount)
            {
                role = FishScript.EscortRole.Shield;
                float shieldPosition = shieldCount <= 1
                    ? 0f
                    : guardIndex / (float)(shieldCount - 1) - 0.5f;
                localOffset = new Vector3(
                    specialEscortInnerRadius * 0.58f,
                    shieldPosition * specialEscortInnerRadius * 1.75f,
                    0f
                );
            }
            else if (guardIndex < shieldCount + 2)
            {
                role = FishScript.EscortRole.Flank;
                localOffset = new Vector3(
                    -specialEscortInnerRadius * 0.18f,
                    (guardIndex - shieldCount == 0 ? -1f : 1f) *
                    specialEscortInnerRadius,
                    0f
                );
            }
            else
            {
                role = FishScript.EscortRole.Orbit;
                float angle = guardIndex * 360f /
                    Mathf.Max(1, guardCount);
                localOffset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad),
                    0f
                ) * (specialEscortInnerRadius * 1.18f);
            }

            Vector3 worldOffset = leader.transform.TransformDirection(
                localOffset
            );
            GameObject guard = GetFishFromPool(
                guardFishIndex,
                leader.transform.position + worldOffset,
                targetPosition,
                true
            );

            if (!TryGetFishScript(guard, out FishScript guardScript))
            {
                continue;
            }

            ApplyBossBodyGuardScaling(guardScript);
            guardScript.SetEscortMovement(
                leader.transform,
                localOffset,
                role,
                guardIndex * 0.73f
            );
        }
    }

    private float GetLevelVerticalRouteChance(GameLevel level)
    {
        int levelIndex = Mathf.Clamp((int)level, 0, 5);

        if (verticalRouteChancePerLevel != null &&
            levelIndex < verticalRouteChancePerLevel.Length)
        {
            return Mathf.Clamp01(
                verticalRouteChancePerLevel[levelIndex]
            );
        }

        return Mathf.Clamp01(verticalPreBossParadeChance);
    }

    private float GetParadeGroupLaneOffset(
        int groupIndex,
        int groupCount,
        int fishIndex,
        ParadeEntrySide entrySide
    )
    {
        float centeredIndex =
            groupIndex -
            (groupCount - 1) * 0.5f;
        float requestedOffset =
            centeredIndex * GetFishVerticalSpacing(fishIndex) +
            Random.Range(-0.25f, 0.25f);
        bool offsetAlongX =
            entrySide == ParadeEntrySide.Top ||
            entrySide == ParadeEntrySide.Bottom;

        return KeepParadeLaneOutsideCenter(
            requestedOffset,
            offsetAlongX,
            fishIndex + groupIndex
        );
    }

    private bool TryGetDirectionalParadeRoute(
        ParadeEntrySide entrySide,
        float laneOffset,
        out Vector3 startPosition,
        out Vector3 targetPosition,
        out Vector3 trailingDirection,
        out Vector3 perpendicularDirection
    )
    {
        startPosition = Vector3.zero;
        targetPosition = Vector3.zero;
        trailingDirection = Vector3.left;
        perpendicularDirection = Vector3.up;

        switch (entrySide)
        {
            case ParadeEntrySide.Left:
                if (LeftPos == null || RightPos == null)
                {
                    return false;
                }

                startPosition =
                    LeftPos.position +
                    new Vector3(0f, laneOffset, 0f);

                targetPosition =
                    RightPos.position +
                    new Vector3(0f, laneOffset, 0f);

                trailingDirection = Vector3.left;
                perpendicularDirection = Vector3.up;
                return true;

            case ParadeEntrySide.Right:
                if (LeftPos == null || RightPos == null)
                {
                    return false;
                }

                startPosition =
                    RightPos.position +
                    new Vector3(0f, laneOffset, 0f);

                targetPosition =
                    LeftPos.position +
                    new Vector3(0f, laneOffset, 0f);

                trailingDirection = Vector3.right;
                perpendicularDirection = Vector3.up;
                return true;

            case ParadeEntrySide.Top:
                if (TopPos == null || BottomPos == null)
                {
                    return false;
                }

                startPosition =
                    TopPos.position +
                    new Vector3(laneOffset, 0f, 0f);

                targetPosition =
                    BottomPos.position +
                    new Vector3(laneOffset, 0f, 0f);

                trailingDirection = Vector3.up;
                perpendicularDirection = Vector3.right;
                return true;

            case ParadeEntrySide.Bottom:
                if (TopPos == null || BottomPos == null)
                {
                    return false;
                }

                startPosition =
                    BottomPos.position +
                    new Vector3(laneOffset, 0f, 0f);

                targetPosition =
                    TopPos.position +
                    new Vector3(laneOffset, 0f, 0f);

                trailingDirection = Vector3.down;
                perpendicularDirection = Vector3.right;
                return true;
        }

        return false;
    }

    private void SpawnDirectionalLineParadeGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount,
        FishScript.SwimStyle style
    )
    {
        if (!TryGetDirectionalParadeRoute(
            entrySide,
            laneOffset,
            out Vector3 startPosition,
            out Vector3 targetPosition,
            out Vector3 trailingDirection,
            out Vector3 perpendicularDirection
        ))
        {
            return;
        }

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);

        for (int i = 0; i < fishCount; i++)
        {
            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (i * horizontalSpacing);

            SpawnSlowPreBossParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                style
            );
        }
    }

    private void SpawnDirectionalVParadeGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount
    )
    {
        if (!TryGetDirectionalParadeRoute(
            entrySide,
            laneOffset,
            out Vector3 leaderPosition,
            out Vector3 targetPosition,
            out Vector3 trailingDirection,
            out Vector3 perpendicularDirection
        ))
        {
            return;
        }

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            leader,
            out FishScript leaderScript
        ))
        {
            return;
        }

        ApplyPreBossParadeScaling(leaderScript);
        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.ArcSweep,
            paradeRouteSweepAmplitude * 0.75f,
            paradeRouteSweepFrequency
        );

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);
        float verticalSpacing = GetFishVerticalSpacing(fishIndex);
        int remainingFish = fishCount - 1;
        int pairCount = remainingFish / 2;

        for (int pair = 1;
             pair <= pairCount;
             pair++)
        {
            Vector3 upperOffset = new Vector3(
                -horizontalSpacing * pair,
                verticalSpacing * 0.5f * pair,
                0f
            );

            Vector3 lowerOffset = new Vector3(
                -horizontalSpacing * pair,
                -verticalSpacing * 0.5f * pair,
                0f
            );

            SpawnSlowPreBossFollower(
                fishIndex,
                leader,
                upperOffset,
                targetPosition
            );

            SpawnSlowPreBossFollower(
                fishIndex,
                leader,
                lowerOffset,
                targetPosition
            );
        }

        if (remainingFish % 2 != 0)
        {
            SpawnSlowPreBossFollower(
                fishIndex,
                leader,
                new Vector3(
                    -horizontalSpacing * (pairCount + 1),
                    0f,
                    0f
                ),
                targetPosition
            );
        }
    }

    private void SpawnDirectionalSchoolParadeGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount
    )
    {
        if (useOrganizedParadePatterns)
        {
            SpawnOrganizedParadeGroup(
                fishIndex,
                entrySide,
                laneOffset,
                fishCount,
                SelectOrganizedPattern(largeParadePatterns),
                true
            );
            return;
        }

        SpawnDirectionalNaturalSchoolGroup(
            fishIndex,
            entrySide,
            laneOffset,
            fishCount,
            true
        );
    }

    private void SpawnLevelNaturalSchool(
        int fishIndex,
        bool largeSchool
    )
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int available = Mathf.Max(
            0,
            GetMaxActiveFish() - CountActiveFish()
        );
        if (available <= 0)
        {
            return;
        }

        int baseCount = largeSchool
            ? Mathf.Max(naturalSchoolMinimumLargeCount, 10)
            : 7;
        int requested = Mathf.Clamp(
            baseCount + GetFormationGrowth(),
            largeSchool ? 8 : 6,
            largeSchool ? 18 : 12
        );
        int fishCount = Mathf.Min(requested, available);
        if (fishCount <= 0)
        {
            return;
        }

        ParadeEntrySide side = Random.value < 0.5f
            ? ParadeEntrySide.Left
            : ParadeEntrySide.Right;
        float requestedLane = Random.Range(-1.65f, 1.65f);
        float lane = KeepParadeLaneOutsideCenter(
            requestedLane,
            false,
            fishIndex + fishCount
        );

        OrganizedParadePattern pattern =
            largeSchool
                ? SelectOrganizedPattern(largeParadePatterns)
                : SelectOrganizedPattern(beginnerParadePatterns);

        SpawnOrganizedParadeGroup(
            fishIndex,
            side,
            lane,
            fishCount,
            pattern,
            false
        );
    }

    private OrganizedParadePattern SelectOrganizedPattern(
        OrganizedParadePattern[] choices
    )
    {
        if (!useOrganizedParadePatterns ||
            choices == null || choices.Length == 0)
        {
            return OrganizedParadePattern.LeaderAndFollowers;
        }

        return choices[Random.Range(0, choices.Length)];
    }

    private void SpawnOrganizedParadeGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount,
        OrganizedParadePattern pattern,
        bool slowPreBossScaling,
        float additionalSpeedMultiplier = 1f
    )
    {
        fishCount = Mathf.Clamp(
            fishCount,
            1,
            Mathf.Max(1, maximumParadeFishCount)
        );

        if (pattern == OrganizedParadePattern.MixedFormation ||
            pattern == OrganizedParadePattern.ProtectedCenter)
        {
            SpawnMixedOrganizedParadeGroup(
                fishIndex,
                entrySide,
                laneOffset,
                fishCount,
                pattern == OrganizedParadePattern.ProtectedCenter,
                slowPreBossScaling,
                additionalSpeedMultiplier
            );
            return;
        }

        if (!TryGetDirectionalParadeRoute(
                entrySide,
                laneOffset,
                out Vector3 leaderPosition,
                out Vector3 targetPosition,
                out Vector3 trailingDirection,
                out Vector3 perpendicularDirection
            ))
        {
            return;
        }

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript) ||
            !leaderScript.CanJoinParade())
        {
            if (leader != null) leader.SetActive(false);
            return;
        }

        if (slowPreBossScaling) ApplyPreBossParadeScaling(leaderScript);
        else ApplyParadeScaling(leaderScript);

        if (!Mathf.Approximately(additionalSpeedMultiplier, 1f))
        {
            leaderScript.MultiplyCurrentRuntimeStats(
                1f,
                1f,
                Mathf.Max(0.05f, additionalSpeedMultiplier)
            );
        }

        FishScript.SwimStyle leaderStyle =
            pattern == OrganizedParadePattern.Wave
                ? FishScript.SwimStyle.ArcSweep
                : FishScript.SwimStyle.LaneGlide;
        float sweep = pattern == OrganizedParadePattern.Wave
            ? paradeRouteSweepAmplitude * 0.55f
            : paradeRouteSweepAmplitude * 0.16f;

        leaderScript.SetParadeRoute(
            targetPosition,
            leaderStyle,
            sweep,
            paradeRouteSweepFrequency * 0.75f
        );

        float horizontalSpacing =
            GetFishHorizontalSpacing(fishIndex) *
            organizedParadeSpacingMultiplier;
        float verticalSpacing =
            GetFishVerticalSpacing(fishIndex) *
            organizedParadeSpacingMultiplier;

        for (int index = 1; index < fishCount; index++)
        {
            Vector3 localOffset = GetOrganizedParadeOffset(
                pattern,
                index,
                fishCount,
                horizontalSpacing,
                verticalSpacing
            );

            if (organizedParadeSlotBreathing > 0f)
            {
                float phase = (fishIndex * 0.37f + index * 1.73f);
                localOffset.y += Mathf.Sin(phase) *
                    organizedParadeSlotBreathing;
            }

            Vector3 worldOffset =
                leader.transform.TransformDirection(localOffset);
            GameObject follower = GetFishFromPool(
                fishIndex,
                leader.transform.position + worldOffset,
                targetPosition,
                true
            );

            if (!TryGetFishScript(follower, out FishScript followerScript) ||
                !followerScript.CanJoinParade())
            {
                if (follower != null) follower.SetActive(false);
                continue;
            }

            if (slowPreBossScaling)
                ApplyPreBossParadeScaling(followerScript);
            else
                ApplyParadeScaling(followerScript);

            if (!Mathf.Approximately(additionalSpeedMultiplier, 1f))
            {
                followerScript.MultiplyCurrentRuntimeStats(
                    1f,
                    1f,
                    Mathf.Max(0.05f, additionalSpeedMultiplier)
                );
            }

            followerScript.SetMovementStyle(
                FishScript.SwimStyle.SchoolFollow,
                leader.transform,
                localOffset
            );
        }
    }

    private void SpawnMixedOrganizedParadeGroup(
        int fallbackFishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount,
        bool protectedCenter,
        bool slowPreBossScaling,
        float additionalSpeedMultiplier
    )
    {
        if (!TryGetDirectionalParadeRoute(
                entrySide,
                laneOffset,
                out Vector3 leaderPosition,
                out Vector3 targetPosition,
                out Vector3 trailingDirection,
                out Vector3 perpendicularDirection
            ))
        {
            return;
        }

        int leaderFishIndex = fallbackFishIndex;
        if (protectedCenter)
        {
            int special = GetSpecialFishIndex(true);
            if (special >= 0)
            {
                leaderFishIndex = special;
            }
        }

        GameObject leader = GetFishFromPool(
            leaderFishIndex,
            leaderPosition,
            targetPosition
        );
        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            if (leader != null) leader.SetActive(false);
            return;
        }

        if (!protectedCenter && !leaderScript.CanJoinParade())
        {
            leader.SetActive(false);
            return;
        }

        if (slowPreBossScaling) ApplyPreBossParadeScaling(leaderScript);
        else ApplyParadeScaling(leaderScript);

        if (!Mathf.Approximately(additionalSpeedMultiplier, 1f))
        {
            leaderScript.MultiplyCurrentRuntimeStats(
                1f,
                1f,
                Mathf.Max(0.05f, additionalSpeedMultiplier)
            );
        }

        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.LaneGlide,
            protectedCenter ? paradeRouteSweepAmplitude * 0.10f :
                paradeRouteSweepAmplitude * 0.18f,
            paradeRouteSweepFrequency
        );

        float horizontalSpacing = GetFishHorizontalSpacing(leaderFishIndex);
        float verticalSpacing = GetFishVerticalSpacing(leaderFishIndex);

        for (int index = 1; index < fishCount; index++)
        {
            int followerFishIndex;
            if (protectedCenter)
            {
                followerFishIndex = (index & 1) == 0
                    ? GetSmallFishIndex()
                    : GetMediumFishIndex();
            }
            else
            {
                int mode = index % 3;
                followerFishIndex = mode == 0
                    ? fallbackFishIndex
                    : mode == 1
                        ? GetSmallFishIndex()
                        : GetMediumFishIndex();
            }

            if (followerFishIndex < 0)
            {
                followerFishIndex = fallbackFishIndex;
            }

            float followerHorizontal = GetFishHorizontalSpacing(
                followerFishIndex
            );
            float followerVertical = GetFishVerticalSpacing(
                followerFishIndex
            );
            float slotHorizontal = Mathf.Max(
                horizontalSpacing,
                followerHorizontal
            );
            float slotVertical = Mathf.Max(
                verticalSpacing,
                followerVertical
            );

            Vector3 localOffset = GetOrganizedParadeOffset(
                protectedCenter
                    ? OrganizedParadePattern.ProtectedCenter
                    : OrganizedParadePattern.LeaderAndFollowers,
                index,
                fishCount,
                slotHorizontal,
                slotVertical
            );

            Vector3 worldOffset =
                leader.transform.TransformDirection(localOffset);
            GameObject follower = GetFishFromPool(
                followerFishIndex,
                leader.transform.position + worldOffset,
                targetPosition,
                true
            );

            if (!TryGetFishScript(follower, out FishScript followerScript) ||
                !followerScript.CanJoinParade())
            {
                if (follower != null) follower.SetActive(false);
                continue;
            }

            if (slowPreBossScaling) ApplyPreBossParadeScaling(followerScript);
            else ApplyParadeScaling(followerScript);

            if (!Mathf.Approximately(additionalSpeedMultiplier, 1f))
            {
                followerScript.MultiplyCurrentRuntimeStats(
                    1f,
                    1f,
                    Mathf.Max(0.05f, additionalSpeedMultiplier)
                );
            }

            followerScript.SetMovementStyle(
                FishScript.SwimStyle.SchoolFollow,
                leader.transform,
                localOffset
            );
        }
    }

    private Vector3 GetOrganizedParadeOffset(
        OrganizedParadePattern pattern,
        int index,
        int totalCount,
        float horizontalSpacing,
        float verticalSpacing
    )
    {
        switch (pattern)
        {
            case OrganizedParadePattern.StraightLine:
                return new Vector3(
                    -horizontalSpacing * index,
                    0f,
                    0f
                );

            case OrganizedParadePattern.Diagonal:
                return new Vector3(
                    -horizontalSpacing * index,
                    verticalSpacing * 0.38f * index *
                    ((totalCount & 1) == 0 ? -1f : 1f),
                    0f
                );

            case OrganizedParadePattern.Wave:
                return new Vector3(
                    -horizontalSpacing * index,
                    Mathf.Sin(index * 0.82f) *
                    verticalSpacing * 1.15f,
                    0f
                );

            case OrganizedParadePattern.Circle:
            {
                int ringCount = Mathf.Max(1, totalCount - 1);
                float angle = (index - 1) / (float)ringCount *
                    Mathf.PI * 2f;
                float radius = Mathf.Max(
                    horizontalSpacing,
                    verticalSpacing
                ) * Mathf.Max(
                    1.55f,
                    ringCount / (Mathf.PI * 2f)
                );
                return new Vector3(
                    Mathf.Cos(angle) * radius - horizontalSpacing * 0.3f,
                    Mathf.Sin(angle) * radius,
                    0f
                );
            }

            case OrganizedParadePattern.Arrow:
            {
                int rank = (index + 1) / 2;
                float side = (index & 1) == 0 ? -1f : 1f;
                return new Vector3(
                    -horizontalSpacing * rank,
                    side * verticalSpacing * 0.62f * rank,
                    0f
                );
            }

            case OrganizedParadePattern.Grid:
            {
                int columns = Mathf.Clamp(
                    Mathf.CeilToInt(Mathf.Sqrt(totalCount)),
                    2,
                    naturalSchoolMaximumFishPerRow
                );
                int followerIndex = index - 1;
                int row = followerIndex / columns + 1;
                int column = followerIndex % columns;
                float centered = column - (columns - 1) * 0.5f;
                return new Vector3(
                    -horizontalSpacing * row,
                    centered * verticalSpacing,
                    0f
                );
            }

            case OrganizedParadePattern.Diamond:
            {
                int followerIndex = index - 1;
                int rank = followerIndex / 4 + 1;
                int point = followerIndex % 4;
                float x = -horizontalSpacing * rank;
                float y = 0f;

                switch (point)
                {
                    case 0:
                        y = verticalSpacing * rank;
                        break;
                    case 1:
                        y = -verticalSpacing * rank;
                        break;
                    case 2:
                        x -= horizontalSpacing * 0.65f;
                        break;
                    default:
                        x += horizontalSpacing * 0.45f;
                        break;
                }

                return new Vector3(x, y, 0f);
            }

            case OrganizedParadePattern.TwinColumn:
            {
                int followerIndex = index - 1;
                int row = followerIndex / 2 + 1;
                float side = (followerIndex & 1) == 0 ? -1f : 1f;
                return new Vector3(
                    -horizontalSpacing * row,
                    side * verticalSpacing * 0.72f,
                    0f
                );
            }

            case OrganizedParadePattern.VerticalLine:
            {
                int rank = (index + 1) / 2;
                float side = (index & 1) == 0 ? -1f : 1f;
                return new Vector3(
                    -horizontalSpacing * 0.18f * rank,
                    side * verticalSpacing * rank,
                    0f
                );
            }

            case OrganizedParadePattern.VFormation:
            {
                int rank = (index + 1) / 2;
                float side = (index & 1) == 0 ? -1f : 1f;
                return new Vector3(
                    -horizontalSpacing * rank,
                    side * verticalSpacing * 0.82f * rank,
                    0f
                );
            }

            case OrganizedParadePattern.Spiral:
            {
                float angle = index * 1.12f;
                float radius = Mathf.Max(
                    horizontalSpacing,
                    verticalSpacing
                ) * (0.55f + index * 0.18f);
                return new Vector3(
                    Mathf.Cos(angle) * radius - horizontalSpacing * 0.55f,
                    Mathf.Sin(angle) * radius,
                    0f
                );
            }

            case OrganizedParadePattern.MixedFormation:
                return GetOrganizedParadeOffset(
                    OrganizedParadePattern.LeaderAndFollowers,
                    index,
                    totalCount,
                    horizontalSpacing,
                    verticalSpacing
                );

            case OrganizedParadePattern.ProtectedCenter:
            {
                int ringCount = Mathf.Max(1, totalCount - 1);
                float angle = (index - 1) / (float)ringCount *
                    Mathf.PI * 2f;
                float radius = Mathf.Max(
                    horizontalSpacing,
                    verticalSpacing
                ) * Mathf.Max(1.25f, ringCount / (Mathf.PI * 2f));
                return new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );
            }

            default:
            {
                int remainingIndex = index - 1;
                int row = 1;
                int capacity = Mathf.Min(
                    naturalSchoolMaximumFishPerRow,
                    row + 1
                );

                while (remainingIndex >= capacity)
                {
                    remainingIndex -= capacity;
                    row++;
                    capacity = Mathf.Min(
                        naturalSchoolMaximumFishPerRow,
                        row + 1
                    );
                }

                float centered =
                    remainingIndex - (capacity - 1) * 0.5f;
                float stagger = (row & 1) == 0
                    ? naturalSchoolRowStagger
                    : -naturalSchoolRowStagger;
                return new Vector3(
                    -horizontalSpacing * row,
                    (centered + stagger) * verticalSpacing,
                    0f
                );
            }
        }
    }

    private void SpawnDirectionalNaturalSchoolGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount,
        bool slowPreBossScaling
    )
    {
        if (!useNaturalSchoolFormation)
        {
            SpawnDirectionalLineParadeGroup(
                fishIndex,
                entrySide,
                laneOffset,
                fishCount,
                FishScript.SwimStyle.LaneGlide
            );
            return;
        }

        if (!TryGetDirectionalParadeRoute(
            entrySide,
            laneOffset,
            out Vector3 leaderPosition,
            out Vector3 targetPosition,
            out Vector3 trailingDirection,
            out Vector3 perpendicularDirection
        ))
        {
            return;
        }

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            return;
        }

        if (!leaderScript.CanJoinParade())
        {
            leader.SetActive(false);
            return;
        }

        if (slowPreBossScaling)
        {
            ApplyPreBossParadeScaling(leaderScript);
        }
        else
        {
            ApplyParadeScaling(leaderScript);
        }

        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.LaneGlide,
            paradeRouteSweepAmplitude * 0.20f,
            paradeRouteSweepFrequency * 0.72f
        );

        int remaining = Mathf.Max(0, fishCount - 1);
        int rowIndex = 1;
        float longitudinalSpacing =
            GetFishHorizontalSpacing(fishIndex) *
            naturalSchoolLongitudinalSpacingMultiplier;
        float lateralSpacing =
            GetFishVerticalSpacing(fishIndex) *
            naturalSchoolLateralSpacingMultiplier;

        while (remaining > 0)
        {
            int wideningCapacity = Mathf.Min(
                naturalSchoolMaximumFishPerRow,
                rowIndex + 1
            );
            int rowCount = Mathf.Min(remaining, wideningCapacity);
            float stagger = (rowIndex & 1) == 0
                ? naturalSchoolRowStagger
                : -naturalSchoolRowStagger;

            for (int slot = 0; slot < rowCount; slot++)
            {
                float centeredSlot = slot - (rowCount - 1) * 0.5f;
                Vector3 localOffset = new Vector3(
                    -longitudinalSpacing * rowIndex,
                    (centeredSlot + stagger) * lateralSpacing,
                    0f
                );

                SpawnNaturalSchoolFollower(
                    fishIndex,
                    leader,
                    localOffset,
                    targetPosition,
                    slowPreBossScaling
                );
            }

            remaining -= rowCount;
            rowIndex++;
        }
    }

    private void SpawnNaturalSchoolFollower(
        int fishIndex,
        GameObject leader,
        Vector3 localOffset,
        Vector3 targetPosition,
        bool slowPreBossScaling
    )
    {
        if (leader == null)
        {
            return;
        }

        Vector3 worldOffset = leader.transform.TransformDirection(
            localOffset
        );
        GameObject follower = GetFishFromPool(
            fishIndex,
            leader.transform.position + worldOffset,
            targetPosition,
            true
        );

        if (!TryGetFishScript(follower, out FishScript followerScript))
        {
            return;
        }

        if (!followerScript.CanJoinParade())
        {
            follower.SetActive(false);
            return;
        }

        if (slowPreBossScaling)
        {
            ApplyPreBossParadeScaling(followerScript);
        }
        else
        {
            ApplyParadeScaling(followerScript);
        }

        followerScript.SetMovementStyle(
            FishScript.SwimStyle.SchoolFollow,
            leader.transform,
            localOffset
        );
    }

    // New random parade: a long snake-shaped group.
    private void SpawnDirectionalSnakeParadeGroup(
        int fishIndex,
        ParadeEntrySide entrySide,
        float laneOffset,
        int fishCount
    )
    {
        if (!TryGetDirectionalParadeRoute(
            entrySide,
            laneOffset,
            out Vector3 startPosition,
            out Vector3 targetPosition,
            out Vector3 trailingDirection,
            out Vector3 perpendicularDirection
        ))
        {
            return;
        }

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);
        float verticalSpacing = GetFishVerticalSpacing(fishIndex);

        for (int i = 0; i < fishCount; i++)
        {
            float waveOffset =
                Mathf.Sin(i * 0.9f) * verticalSpacing * 0.65f;

            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (i * horizontalSpacing) +
                perpendicularDirection * waveOffset;

            SpawnSlowPreBossParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                FishScript.SwimStyle.ArcSweep
            );
        }
    }

    private void SpawnSlowPreBossFollower(
        int fishIndex,
        GameObject leader,
        Vector3 localOffset,
        Vector3 targetPosition
    )
    {
        if (leader == null)
        {
            return;
        }

        Vector3 worldOffset =
            leader.transform.TransformDirection(
                localOffset
            );

        GameObject follower = GetFishFromPool(
            fishIndex,
            leader.transform.position + worldOffset,
            targetPosition,
            true
        );

        if (!TryGetFishScript(
            follower,
            out FishScript followerScript
        ))
        {
            return;
        }

        ApplyPreBossParadeScaling(followerScript);

        followerScript.SetMovementStyle(
            FishScript.SwimStyle.SchoolFollow,
            leader.transform,
            localOffset
        );
    }

    private void SpawnSlowPreBossParadeFish(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        FishScript.SwimStyle style
    )
    {
        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            fish,
            out FishScript script
        ))
        {
            return;
        }

        ApplyPreBossParadeScaling(script);
        float sweep = style == FishScript.SwimStyle.LaneGlide
            ? paradeRouteSweepAmplitude * 0.32f
            : style == FishScript.SwimStyle.VerticalDive
                ? paradeRouteSweepAmplitude * 0.58f
                : paradeRouteSweepAmplitude;
        script.SetParadeRoute(
            targetPosition,
            style,
            sweep,
            paradeRouteSweepFrequency
        );
    }

    private void ApplyPreBossParadeScaling(
        FishScript script
    )
    {
        if (script == null)
        {
            return;
        }

        // Parade is a temporary movement role only. Combat stats keep the
        // exact same scaling the fish would receive as an ordinary spawn.
        ApplyNormalScaling(script);
        float targetSpeed = GetStableParadeWorldSpeed(true);

        script.PrepareForParadeControl(
            targetSpeed,
            paradeFollowerCorrection,
            paradeFollowerCatchUpMultiplier,
            paradeFollowerSlotWobble
        );
        RegisterParadeFish(script);
    }

    private int GetConcurrentParadeGroupCount(
        GameLevel level,
        int waveIndex
    )
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return 2;

            case GameLevel.Level2_School:
                return Random.Range(2, 4);

            case GameLevel.Level3_Circle:
                return 3;

            case GameLevel.Level4_Cross:
                return Random.Range(3, 5);

            case GameLevel.Level5_Festival:
                return 4;

            case GameLevel.Level6_BossOcean:
                return Random.Range(4, 6);

            default:
                return 2;
        }
    }

    private float GetFishHorizontalSpacing(int fishIndex)
    {
        if (TryGetPrefabFishScript(fishIndex, out FishScript script))
        {
            float gap = GetParadeVisualGap(script);
            return script.GetParadeSafeHorizontalSpacing(
                gap,
                paradeMinimumGapPadding
            );
        }

        return paradeFishHorizontalGap;
    }

    private float GetFishVerticalSpacing(int fishIndex)
    {
        if (TryGetPrefabFishScript(fishIndex, out FishScript script))
        {
            float gap = GetParadeVisualGap(script);
            return script.GetParadeSafeVerticalSpacing(
                gap,
                paradeMinimumGapPadding
            );
        }

        return Mathf.Max(0.35f, paradeLaneSpacing * 0.45f);
    }

    private bool TryGetPrefabFishScript(
        int fishIndex,
        out FishScript script
    )
    {
        script = null;
        return Fish != null &&
               fishIndex >= 0 &&
               fishIndex < Fish.Length &&
               Fish[fishIndex] != null &&
               Fish[fishIndex].TryGetComponent(out script);
    }

    private float GetParadeVisualGap(FishScript script)
    {
        if (script == null)
        {
            return mediumParadeVisualGap;
        }

        FishGameplayProfile profile = script.GetGameplayProfile();
        if (script.GetFishTier() == FishTier.Special)
        {
            return specialParadeVisualGap;
        }

        if (profile != null)
        {
            switch (profile.sizeClass)
            {
                case FishSizeClass.Tiny:
                case FishSizeClass.Small:
                    return smallParadeVisualGap;

                case FishSizeClass.Medium:
                    return mediumParadeVisualGap;

                case FishSizeClass.Large:
                case FishSizeClass.Huge:
                case FishSizeClass.Boss:
                    return largeParadeVisualGap;
            }
        }

        return script.GetFishTier() == FishTier.Small
            ? smallParadeVisualGap
            : mediumParadeVisualGap;
    }

    private float GetParadeGroupLaneY(
        int groupIndex,
        int groupCount,
        int fishIndex
    )
    {
        float centeredIndex =
            groupIndex -
            (groupCount - 1) * 0.5f;
        float requestedOffset =
            centeredIndex * GetFishVerticalSpacing(fishIndex) +
            Random.Range(-0.25f, 0.25f);

        return KeepParadeLaneOutsideCenter(
            requestedOffset,
            false,
            fishIndex + groupIndex
        );
    }

    private float KeepParadeLaneOutsideCenter(
        float requestedOffset,
        bool offsetAlongX,
        int variationSeed
    )
    {
        Camera targetCamera = Camera.main;
        float minimumDistance = 1.25f;
        float maximumDistance = 3.5f;

        if (targetCamera != null && targetCamera.orthographic)
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            float halfExtent = offsetAlongX ? halfWidth : halfHeight;
            minimumDistance = halfExtent * (
                offsetAlongX ? 0.17f : 0.15f
            );
            maximumDistance = halfExtent * 0.78f;
        }

        minimumDistance = Mathf.Max(0.75f, minimumDistance);
        maximumDistance = Mathf.Max(
            minimumDistance,
            maximumDistance
        );

        if (Mathf.Abs(requestedOffset) >= minimumDistance)
        {
            return Mathf.Clamp(
                requestedOffset,
                -maximumDistance,
                maximumDistance
            );
        }

        float side = (variationSeed & 1) == 0 ? -1f : 1f;
        float separatedOffset = minimumDistance + Random.Range(
            0.10f,
            Mathf.Max(0.12f, minimumDistance * 0.24f)
        );

        return side * Mathf.Min(separatedOffset, maximumDistance);
    }

    private void SpawnFastLineParadeGroup(
    int fishIndex,
    float laneY,
    bool fromLeft,
    int fishCount,
    FishScript.SwimStyle style
)
    {
        Vector3 startPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 trailingDirection =
            fromLeft
                ? Vector3.left
                : Vector3.right;

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);

        for (int i = 0; i < fishCount; i++)
        {
            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (i * horizontalSpacing);

            SpawnFastParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                style
            );
        }
    }

    private void SpawnFastVParadeGroup(
        int fishIndex,
        float laneY,
        bool fromLeft,
        int fishCount
    )
    {
        Vector3 leaderPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            leader,
            out FishScript leaderScript
        ))
        {
            return;
        }

        if (!leaderScript.CanJoinParade())
        {
            leader.SetActive(false);
            return;
        }

        ApplyParadeScaling(leaderScript);

        leaderScript.SetParadeRoute(
            targetPosition,
            FishScript.SwimStyle.ArcSweep,
            paradeRouteSweepAmplitude * 0.72f,
            paradeRouteSweepFrequency
        );

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);
        float verticalSpacing = GetFishVerticalSpacing(fishIndex);
        int remainingFish = fishCount - 1;
        int pairCount = remainingFish / 2;

        for (int pair = 1; pair <= pairCount; pair++)
        {
            Vector3 upperLocalOffset = new Vector3(
                -horizontalSpacing * pair,
                verticalSpacing * 0.5f * pair,
                0f
            );

            Vector3 lowerLocalOffset = new Vector3(
                -horizontalSpacing * pair,
                -verticalSpacing * 0.5f * pair,
                0f
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                upperLocalOffset,
                targetPosition
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                lowerLocalOffset,
                targetPosition
            );
        }

        // Add one center follower when fishCount is even.
        if (remainingFish % 2 != 0)
        {
            Vector3 centerOffset = new Vector3(
                -horizontalSpacing * (pairCount + 1),
                0f,
                0f
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                centerOffset,
                targetPosition
            );
        }
    }

    private void SpawnFastParadeFollower(
        int fishIndex,
        GameObject leader,
        Vector3 localOffset,
        Vector3 targetPosition
    )
    {
        if (leader == null)
        {
            return;
        }

        Vector3 worldOffset =
            leader.transform.TransformDirection(
                localOffset
            );

        GameObject follower = GetFishFromPool(
            fishIndex,
            leader.transform.position + worldOffset,
            targetPosition,
            true
        );

        if (!TryGetFishScript(
            follower,
            out FishScript followerScript
        ))
        {
            return;
        }

        if (!followerScript.CanJoinParade())
        {
            follower.SetActive(false);
            return;
        }

        ApplyParadeScaling(followerScript);

        followerScript.SetMovementStyle(
            FishScript.SwimStyle.SchoolFollow,
            leader.transform,
            localOffset
        );
    }


    private void SpawnFastSchoolParadeGroup(
        int fishIndex,
        float laneY,
        bool fromLeft,
        int fishCount
    )
    {
        Vector3 startPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 trailingDirection =
            fromLeft
                ? Vector3.left
                : Vector3.right;

        float horizontalSpacing = GetFishHorizontalSpacing(fishIndex);
        float verticalSpacing = GetFishVerticalSpacing(fishIndex);

        for (int i = 0; i < fishCount; i++)
        {
            int column = i / 2;
            int row = i % 2;

            float rowY = row == 0
                ? -verticalSpacing * 0.5f
                : verticalSpacing * 0.5f;

            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (column * horizontalSpacing) +
                new Vector3(0f, rowY, 0f);

            SpawnFastParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                FishScript.SwimStyle.LaneGlide
            );
        }
    }

    private void SpawnFastParadeFish(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        FishScript.SwimStyle style
    )
    {
        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            fish,
            out FishScript script
        ))
        {
            return;
        }

        if (!script.CanJoinParade())
        {
            fish.SetActive(false);
            return;
        }

        ApplyParadeScaling(script);
        float sweep = style == FishScript.SwimStyle.LaneGlide ||
                      style == FishScript.SwimStyle.FastTideExit
            ? paradeRouteSweepAmplitude * 0.25f
            : paradeRouteSweepAmplitude * 0.78f;
        script.SetParadeRoute(
            targetPosition,
            style,
            sweep,
            paradeRouteSweepFrequency * 1.08f
        );
    }

    private void ApplyParadeScaling(
        FishScript script
    )
    {
        if (script == null)
        {
            return;
        }

        // Parade is a temporary movement role only. Do not rewrite the fish's
        // personality/profile or give it a special HP/reward identity.
        ApplyNormalScaling(script);
        float targetSpeed = GetStableParadeWorldSpeed(false);

        script.PrepareForParadeControl(
            targetSpeed,
            paradeFollowerCorrection,
            paradeFollowerCatchUpMultiplier,
            paradeFollowerSlotWobble
        );
        RegisterParadeFish(script);
    }

    private float GetStableParadeWorldSpeed(bool preBoss)
    {
        float levelBonus = (int)currentLevelState * paradeWorldSpeedPerLevel;
        float speed = Mathf.Max(0.2f, paradeWorldSpeed + levelBonus);

        if (preBoss)
        {
            speed *= Mathf.Clamp(
                preBossParadeSpeedBase +
                (int)currentLevelState * preBossParadeSpeedPerLevel,
                0.65f,
                1.15f
            );
        }

        return speed;
    }

    private void MaintainMinimumPopulation()
    {
        if (Time.time < nextMinimumPopulationRefillTime)
        {
            return;
        }

        int activeCount = CountActiveFish();
        int targetMinimum = GetRampedMinimumActiveFish();

        if (activeCount >= targetMinimum)
        {
            return;
        }

        int refillBurst = Time.time < levelSpawnRampUntil
            ? 1
            : minimumPopulationRefillBurst;
        int refillCount = Mathf.Min(
            refillBurst,
            targetMinimum - activeCount
        );

        for (int i = 0; i < refillCount; i++)
        {
            SpawnAmbientNormalFish();
        }

        nextMinimumPopulationRefillTime =
            Time.time + minimumPopulationRefillInterval;
    }

    private void MaintainBossCompanionPopulation()
    {
        if (!keepAmbientFishWithBoss ||
            currentPhase != LevelPhase.BossBattle ||
            ActiveTargetBossCount <= 0 ||
            Time.time < nextBossAmbientRefillTime)
        {
            return;
        }

        int currentCompanions = CountActiveAmbientCompanionFish();
        int desiredCompanions = Mathf.Max(
            1,
            minimumAmbientFishDuringBossBattle
        );

        if (currentCompanions >= desiredCompanions)
        {
            nextBossAmbientRefillTime =
                Time.time + Mathf.Max(0.10f, bossAmbientRefillInterval);
            return;
        }

        int capacity = Mathf.Max(
            0,
            GetBossFishLimit() - CountActiveFish()
        );
        int refillCount = Mathf.Min(
            Mathf.Max(1, bossAmbientRefillBurst),
            desiredCompanions - currentCompanions,
            capacity
        );

        for (int i = 0; i < refillCount; i++)
        {
            SpawnAmbientNormalFish();
        }

        nextBossAmbientRefillTime =
            Time.time + Mathf.Max(0.10f, bossAmbientRefillInterval);
    }

    private int CountActiveAmbientCompanionFish()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.fishInScreenList == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < gm.fishInScreenList.Count; i++)
        {
            FishScript fish = gm.fishInScreenList[i];
            if (fish == null ||
                !fish.gameObject.activeInHierarchy ||
                fish.IsDeadOrDying ||
                fish.IsBoss() ||
                fish.IsParadeControlled)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private int GetMixedParadeFishCapacityLimit()
    {
        return Mathf.Min(
            GetMaxActiveFish() + Mathf.Max(0, mixedParadeExtraFishCapacity),
            Mathf.Max(1, absoluteMaximumActiveFish)
        );
    }

    private int GetAmbientFishCapacityLimit()
    {
        return currentPhase == LevelPhase.BossBattle
            ? GetBossFishLimit()
            : GetMaxActiveFish();
    }

    private void SpawnAmbientNormalFish()
    {
        if (!ReferencesAreReady() ||
            CountActiveFish() >= GetAmbientFishCapacityLimit())
        {
            return;
        }

        FishLevelPopulationProfile population =
            GetCurrentLevelPopulationProfile();
        int fishIndex = SelectBalancedAmbientFishIndex(population);

        if (fishIndex < 0 || Fish[fishIndex] == null ||
            !Fish[fishIndex].TryGetComponent<FishScript>(out FishScript prefabScript))
        {
            return;
        }

        FishGameplayProfile gameplayProfile = prefabScript.GetGameplayProfile();
        if (gameplayProfile != null)
        {
            if (CurrentLevelNumber < gameplayProfile.minimumLevel ||
                (spawnDirectorService != null &&
                 spawnDirectorService.CountActiveProfile(gameplayProfile) >=
                 gameplayProfile.maximumSimultaneousCount))
            {
                return;
            }

            if (population != null &&
                spawnDirectorService != null &&
                gameplayProfile.fishTier != FishTier.MainBoss &&
                (int)gameplayProfile.sizeClass >= (int)FishSizeClass.Large &&
                spawnDirectorService.CountActiveSizeClass(FishSizeClass.Large) >=
                    population.maximumLargeFish)
            {
                return;
            }
        }

        bool allowVertical = TopPos != null && BottomPos != null &&
            Random.value <= GetLevelVerticalRouteChance(currentLevelState) *
            ambientVerticalRouteMultiplier;

        if (spawnDirectorService != null && population != null)
        {
            spawnDirectorService.SetLevelTuning(
                population.complexRouteChance,
                population.evasiveBehaviorChance
            );
        }

        FishSpawnPlan plan = default;
        bool planned = spawnDirectorService != null &&
            spawnDirectorService.TryBuildAmbientPlan(
                gameplayProfile,
                LeftPos,
                RightPos,
                TopPos,
                BottomPos,
                allowVertical,
                out plan
            );

        if (!planned)
        {
            bool fromLeft = Random.value < 0.5f;
            plan = new FishSpawnPlan
            {
                entrySide = fromLeft ? FishScreenSide.Left : FishScreenSide.Right,
                exitSide = fromLeft ? FishScreenSide.Right : FishScreenSide.Left,
                routePattern = FishRoutePattern.StraightCrossing,
                spawnPosition = fromLeft ? LeftPos.position : RightPos.position,
                targetPosition = fromLeft ? RightPos.position : LeftPos.position,
                crossesCenter = true,
                verticalRoute = false
            };
        }

        int availableCapacity = Mathf.Max(
            0,
            GetAmbientFishCapacityLimit() - CountActiveFish()
        );
        int groupSize = Mathf.Clamp(
            prefabScript.GetNaturalAmbientGroupSize(),
            1,
            availableCapacity
        );
        if (groupSize > 1 && population != null &&
            spawnDirectorService != null &&
            spawnDirectorService.CountActiveGroups() >= population.maximumGroups)
        {
            groupSize = 1;
        }

        float horizontalSpacing = prefabScript.GetFormationHorizontalSpacing(
            paradeFishHorizontalGap
        );
        float verticalSpacing = prefabScript.GetFormationVerticalSpacing(0.35f);
        GameObject leader = null;
        FishScript.SwimStyle leaderStyle = MapRoutePatternToSwimStyle(
            plan.routePattern,
            plan.verticalRoute
        );

        for (int i = 0; i < groupSize; i++)
        {
            float side = i == 0
                ? 0f
                : (i % 2 == 0 ? -0.22f : 0.22f) * verticalSpacing;
            Vector3 localFollowOffset = new Vector3(
                -i * horizontalSpacing,
                side,
                0f
            );
            Vector3 memberPosition = leader == null
                ? plan.spawnPosition
                : leader.transform.position +
                  leader.transform.TransformDirection(localFollowOffset);

            GameObject fish = GetFishFromPool(
                fishIndex,
                memberPosition,
                plan.targetPosition,
                i > 0
            );

            if (!TryGetFishScript(fish, out FishScript script))
            {
                continue;
            }

            ApplyNormalScaling(script);

            if (i == 0)
            {
                leader = fish;
                float sweep = plan.routePattern == FishRoutePattern.StraightCrossing
                    ? paradeRouteSweepAmplitude * 0.22f
                    : plan.routePattern == FishRoutePattern.SShapedRoute
                        ? paradeRouteSweepAmplitude * 1.15f
                        : paradeRouteSweepAmplitude * 0.70f;
                script.SetParadeRoute(
                    plan.targetPosition,
                    leaderStyle,
                    sweep,
                    paradeRouteSweepFrequency
                );
            }
            else if (leader != null)
            {
                script.SetMovementStyle(
                    FishScript.SwimStyle.SchoolFollow,
                    leader.transform,
                    localFollowOffset
                );
            }
            else
            {
                script.SetParadeRoute(
                    plan.targetPosition,
                    leaderStyle,
                    paradeRouteSweepAmplitude * 0.45f,
                    paradeRouteSweepFrequency
                );
            }
        }

        if (spawnDirectorService != null)
        {
            spawnDirectorService.RememberFishIndex(fishIndex);
        }
    }

    private FishScript.SwimStyle MapRoutePatternToSwimStyle(
        FishRoutePattern routePattern,
        bool verticalRoute
    )
    {
        if (verticalRoute)
        {
            return FishScript.SwimStyle.VerticalDive;
        }

        switch (routePattern)
        {
            case FishRoutePattern.WideCurvedRoute:
                return FishScript.SwimStyle.ArcSweep;
            case FishRoutePattern.SShapedRoute:
                return FishScript.SwimStyle.ZigZagBurst;
            case FishRoutePattern.LoopingRoute:
                return FishScript.SwimStyle.SpiralCross;
            case FishRoutePattern.PlayerApproachRoute:
                return FishScript.SwimStyle.Curious;
            case FishRoutePattern.DiagonalCrossing:
            case FishRoutePattern.CenterCrossing:
                return FishScript.SwimStyle.ArcSweep;
            default:
                return FishScript.SwimStyle.LaneGlide;
        }
    }

    private int GetRampedMinimumActiveFish()
    {
        int fullTarget = GetMinimumActiveFish();
        if (Time.time >= levelSpawnRampUntil ||
            levelSpawnRampUntil <= levelSpawnRampStartedAt)
        {
            return fullTarget;
        }

        float progress = Mathf.InverseLerp(
            levelSpawnRampStartedAt,
            levelSpawnRampUntil,
            Time.time
        );
        return Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Lerp(2f, fullTarget, progress)),
            1,
            fullTarget
        );
    }

    private float GetNextAmbientTrafficLaneViewportY()
    {
        int laneCount = Mathf.Clamp(ambientTrafficLaneCount, 2, 6);
        int laneIndex = Mathf.Abs(ambientTrafficLaneCursor) % laneCount;
        ambientTrafficLaneCursor = (laneIndex + 1) % laneCount;

        bool upperHalf = (laneIndex & 1) == 1;
        int laneInHalf = laneIndex / 2;
        int lanesInHalf = upperHalf
            ? laneCount / 2
            : (laneCount + 1) / 2;
        float normalizedLane =
            (laneInHalf + 0.5f) / Mathf.Max(1, lanesInHalf);
        float padding = Mathf.Clamp(
            ambientViewportPadding,
            FishScreenBounds.MinimumViewportPadding,
            0.22f
        );
        float centerAvoidance = Mathf.Clamp(
            ambientCenterAvoidanceHalfHeight,
            0.05f,
            0.25f
        );
        float halfMinimum = upperHalf
            ? 0.5f + centerAvoidance
            : padding;
        float halfMaximum = upperHalf
            ? 1f - padding
            : 0.5f - centerAvoidance;
        float laneY = Mathf.Lerp(
            halfMinimum,
            halfMaximum,
            normalizedLane
        );

        laneY += Random.Range(
            -ambientLaneJitter,
            ambientLaneJitter
        );

        return Mathf.Clamp(laneY, halfMinimum, halfMaximum);
    }

    private float GetAmbientTargetViewportY(float sourceLaneY)
    {
        float padding = Mathf.Clamp(
            ambientViewportPadding,
            FishScreenBounds.MinimumViewportPadding,
            0.22f
        );
        float centerAvoidance = Mathf.Clamp(
            ambientCenterAvoidanceHalfHeight,
            0.05f,
            0.25f
        );
        bool upperHalf = sourceLaneY >= 0.5f;
        float minimum = upperHalf
            ? 0.5f + centerAvoidance
            : padding;
        float maximum = upperHalf
            ? 1f - padding
            : 0.5f - centerAvoidance;

        return Mathf.Clamp(
            sourceLaneY + Random.Range(
                -ambientTargetVerticalDrift,
                ambientTargetVerticalDrift
            ),
            minimum,
            maximum
        );
    }

    private static float ViewportYToWorldY(
        float viewportY,
        float worldZ
    )
    {
        Camera targetCamera = Camera.main;

        if (targetCamera == null)
        {
            return 0f;
        }

        float depth = Mathf.Abs(
            worldZ - targetCamera.transform.position.z
        );
        Vector3 worldPoint = targetCamera.ViewportToWorldPoint(
            new Vector3(0.5f, viewportY, depth)
        );
        return worldPoint.y;
    }

    private static float ViewportXToWorldX(
        float viewportX,
        float worldZ
    )
    {
        Camera targetCamera = Camera.main;

        if (targetCamera == null)
        {
            return 0f;
        }

        float depth = Mathf.Abs(
            worldZ - targetCamera.transform.position.z
        );
        Vector3 worldPoint = targetCamera.ViewportToWorldPoint(
            new Vector3(viewportX, 0.5f, depth)
        );
        return worldPoint.x;
    }

    private void SpawnCrossAttackParade()
    {
        if (!ReferencesAreReady() || TopPos == null || BottomPos == null)
        {
            return;
        }

        int horizontalFishIndex = GetSmallFishIndex();
        int verticalFishIndex = GetSmallFishIndex();
        int lineCount = Mathf.Clamp(
            3 + GetFormationGrowth() / 2,
            3,
            7
        );
        float horizontalSpacing = GetFishHorizontalSpacing(
            horizontalFishIndex
        );
        float verticalSpacing = GetFishVerticalSpacing(
            verticalFishIndex
        );

        for (int i = 0; i < lineCount; i++)
        {
            SpawnScaledNormalFish(
                horizontalFishIndex,
                LeftPos.position + new Vector3(
                    -i * horizontalSpacing,
                    2f,
                    0f
                ),
                RightPos.position,
                FishScript.SwimStyle.HorizontalRush
            );

            SpawnScaledNormalFish(
                horizontalFishIndex,
                RightPos.position + new Vector3(
                    i * horizontalSpacing,
                    -2f,
                    0f
                ),
                LeftPos.position,
                FishScript.SwimStyle.HorizontalRush
            );

            SpawnScaledNormalFish(
                verticalFishIndex,
                TopPos.position + new Vector3(
                    -2f,
                    i * verticalSpacing,
                    0f
                ),
                BottomPos.position,
                FishScript.SwimStyle.VerticalDive
            );

            SpawnScaledNormalFish(
                verticalFishIndex,
                BottomPos.position + new Vector3(
                    2f,
                    -i * verticalSpacing,
                    0f
                ),
                TopPos.position,
                FishScript.SwimStyle.VerticalDive
            );
        }
    }

    private int GetPreBossParadeWaveCount(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        int count = (int)level < 2
            ? earlyLevelParadeWaves
            : advancedLevelParadeWaves;

        // The second target boss still receives a parade,
        // but the sequence is slightly shorter.
        if (bossSequenceIndex > 0)
        {
            count--;
        }

        return Mathf.Clamp(count, 1, 3);
    }

    private float GetPreBossParadeGap(
        GameLevel level,
        int waveIndex
    )
    {
        return preBossParadeBaseGap +
               (int)level * preBossParadeGapPerLevel +
               waveIndex * 0.35f;
    }

    private bool ShouldSpawnPreBossMiniBoss(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        float chance = preBossMiniBossBaseChance + (int)level * preBossMiniBossChancePerLevel;

        // A later main-boss batch still has a high chance,
        // but slightly lower than the first batch.
        if (bossSequenceIndex > 0)
        {
            chance *= 0.90f;
        }

        return Random.value <= Mathf.Clamp(
            chance,
            0f,
            0.85f
        );
    }

    private float GetPreBossMiniBossLeadDelay(
        GameLevel level
    )
    {
        return preBossMiniBossLeadBaseDelay +
               (int)level *
               preBossMiniBossLeadPerLevel;
    }

    private float GetPreBossFinalPause(
        GameLevel level
    )
    {
        return preBossFinalPauseBase +
               (int)level *
               preBossFinalPausePerLevel;
    }

    private int GetParadeFishPerGroup(
        GameLevel level
    )
    {
        int levelIndex =
            (int)level;

        return Mathf.Clamp(
            paradeFishPerGroupBase +
            levelIndex *
            paradeFishPerGroupPerLevel,
            3,
            Mathf.Max(3, paradeFishPerGroupMaximum)
        );
    }
    private int SpawnTargetBossBatch(
        int[] bossTargets,
        int startIndex,
        int batchSize
    )
    {
        activeTargetBosses.Clear();

        int spawnedCount = 0;

        for (int slotIndex = 0;
             slotIndex < batchSize;
             slotIndex++)
        {
            int arrayPosition =
                startIndex + slotIndex;

            if (arrayPosition < 0 ||
                arrayPosition >= bossTargets.Length)
            {
                continue;
            }

            GameObject boss =
                SpawnTargetBoss(
                    bossTargets[arrayPosition],
                    slotIndex,
                    batchSize
                );

            if (boss == null)
            {
                continue;
            }

            activeTargetBosses.Add(boss);
            spawnedCount++;
        }

        return spawnedCount;
    }

    private GameObject SpawnTargetBoss(
    int bossIndex,
    int slotIndex,
    int totalBossCount
)
    {
        if (!ReferencesAreReady())
        {
            return null;
        }

        if (!IsValidBigBossIndex(bossIndex))
        {
            Debug.LogError(
                "Boss index " + bossIndex +
                " is invalid. Big bosses must use " +
                bigBossStartIndex + " to " +
                bigBossEndIndex + "."
            );

            return null;
        }

        float centeredSlot =
            slotIndex -
            (totalBossCount - 1) * 0.5f;

        float laneY =
            centeredSlot *
            simultaneousBossVerticalSpacing;

        bool spawnFromLeft =
            slotIndex % 2 == 0;

        Vector3 spawnPosition =
            spawnFromLeft
                ? LeftPos.position
                : RightPos.position;

        spawnPosition += new Vector3(
            0f,
            laneY,
            0f
        );

        Vector3 targetPosition =
            new Vector3(
                0f,
                laneY * 0.35f,
                0f
            );

        // Bosses are progression-critical. A crowded entry lane must not
        // prevent the boss batch from spawning after the pre-boss rush.
        GameObject boss = GetFishFromPool(
            bossIndex,
            spawnPosition,
            targetPosition,
            false,
            true
        );

        if (!TryGetFishScript(
            boss,
            out FishScript bossScript
        ))
        {
            return null;
        }

        ApplyBossScaling(
            bossScript,
            totalBossCount
        );

        // ConfigureAsLevelBoss now selects ID 11's cute boss movement,
        // starts random boss style changes, and handles low-HP movement.
        bossScript.ConfigureAsLevelBoss(this);

        SpawnBossEntranceGuards(
            boss.transform,
            bossIndex,
            bossScript
        );

        return boss;
    }
    private void SpawnBossEntranceGuards(
        Transform bossTransform,
        int bossIndex,
        FishScript bossScript
    )
    {
        if (bossTransform == null || bossScript == null)
        {
            return;
        }

        EpicBossController epicBoss =
            bossScript.GetComponent<EpicBossController>();
        bool isEpicBoss = bossScript.IsEpicBoss ||
                          (epicBoss != null && epicBoss.HasProfile);
        bool isHighBoss = bossIndex >= minimumBossIndexForRoyalGuards;

        if (bodyGuardsOnlyForHighBosses &&
            !isHighBoss &&
            !(alwaysGuardEpicBoss && isEpicBoss))
        {
            Debug.Log(
                "<color=#B0BEC5>[BOSS GUARD]</color> " +
                "Early or mini boss entered without royal guards."
            );
            return;
        }

        float guardChance = isEpicBoss && alwaysGuardEpicBoss
            ? 1f
            : Mathf.Clamp01(highBossBodyGuardChance);

        if (Random.value > guardChance)
        {
            Debug.Log(
                "<color=#B0BEC5>[BOSS GUARD]</color> " +
                "High boss entered alone by configured chance."
            );
            return;
        }

        // Retain the old independent no-guard chance only when the new
        // high-boss-only rule is disabled for backward compatibility.
        if (!bodyGuardsOnlyForHighBosses &&
            Random.value <= noBossBodyGuardChance)
        {
            return;
        }

        int availableSlots = Mathf.Max(
            0,
            GetBossFishLimit() - CountActiveFish()
        );

        if (availableSlots <= 0)
        {
            return;
        }

        int guardFishIndex = GetSingleBossBodyGuardFishIndex();
        BossGuardFormationStyle formation = ResolveBossGuardFormation();

        if (formation == BossGuardFormationStyle.RoyalEscort)
        {
            SpawnRoyalBossEscortFormation(
                bossTransform,
                guardFishIndex,
                availableSlots
            );
            return;
        }

        if (formation == BossGuardFormationStyle.Arrowhead)
        {
            SpawnArrowheadBossGuardFormation(
                bossTransform,
                guardFishIndex,
                availableSlots
            );
            return;
        }

        SpawnHalfCircleBossGuardFormation(
            bossTransform,
            guardFishIndex,
            availableSlots
        );
    }

    private BossGuardFormationStyle ResolveBossGuardFormation()
    {
        if (highBossGuardFormation != BossGuardFormationStyle.Random)
        {
            return highBossGuardFormation;
        }

        return (BossGuardFormationStyle)Random.Range(
            (int)BossGuardFormationStyle.HalfCircleArcs,
            (int)BossGuardFormationStyle.Arrowhead + 1
        );
    }

    private int GetDesiredHighBossGuardCount(int availableSlots)
    {
        int desired = highBossRoyalGuardCount +
                      (int)currentLevelState *
                      highBossRoyalGuardCountPerLevel;

        return Mathf.Clamp(
            desired,
            0,
            Mathf.Min(availableSlots, 24)
        );
    }

    private void SpawnRoyalBossEscortFormation(
        Transform bossTransform,
        int guardFishIndex,
        int availableSlots
    )
    {
        int guardCount = GetDesiredHighBossGuardCount(availableSlots);
        if (guardCount <= 0)
        {
            return;
        }

        float spacing = Mathf.Max(
            0.35f,
            highBossRoyalGuardSpacingMultiplier
        );
        int frontShieldCount = Mathf.Min(5, Mathf.Max(3, guardCount / 3));
        int flankCount = Mathf.Min(4, Mathf.Max(2, (guardCount - frontShieldCount) / 2));

        for (int index = 0; index < guardCount; index++)
        {
            Vector3 localOffset;
            FishScript.EscortRole role;

            if (index < frontShieldCount)
            {
                float normalized = frontShieldCount <= 1
                    ? 0.5f
                    : index / (float)(frontShieldCount - 1);
                localOffset = new Vector3(
                    bodyGuardInnerRadius * 0.72f * spacing,
                    Mathf.Lerp(-1f, 1f, normalized) *
                    bodyGuardInnerRadius * 0.95f * spacing,
                    0f
                );
                role = FishScript.EscortRole.Shield;
            }
            else if (index < frontShieldCount + flankCount)
            {
                int flankIndex = index - frontShieldCount;
                int row = flankIndex / 2;
                float side = (flankIndex & 1) == 0 ? -1f : 1f;
                localOffset = new Vector3(
                    -bodyGuardArcRadiusSpacing * row * 0.45f * spacing,
                    side * (bodyGuardInnerRadius +
                    row * bodyGuardArcRadiusSpacing) * spacing,
                    0f
                );
                role = FishScript.EscortRole.Flank;
            }
            else
            {
                int rearIndex = index - frontShieldCount - flankCount;
                int rearCount = Mathf.Max(1, guardCount - frontShieldCount - flankCount);
                float angle = Mathf.Lerp(120f, 240f,
                    rearCount <= 1 ? 0.5f : rearIndex / (float)(rearCount - 1));
                float radians = angle * Mathf.Deg2Rad;
                float radius = (bodyGuardInnerRadius + bodyGuardArcRadiusSpacing) * spacing;
                localOffset = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f
                );
                role = FishScript.EscortRole.Orbit;
            }

            SpawnBossGuardAtOffset(
                bossTransform,
                guardFishIndex,
                localOffset,
                role,
                index * 0.67f
            );
        }

        Debug.Log(
            "<color=#FFD54F>[ROYAL ESCORT]</color> " +
            guardCount + " body guards entered with the high boss."
        );
    }

    private void SpawnArrowheadBossGuardFormation(
        Transform bossTransform,
        int guardFishIndex,
        int availableSlots
    )
    {
        int guardCount = GetDesiredHighBossGuardCount(availableSlots);
        if (guardCount <= 0)
        {
            return;
        }

        float spacing = Mathf.Max(
            0.35f,
            highBossRoyalGuardSpacingMultiplier
        );

        for (int index = 0; index < guardCount; index++)
        {
            int rank = index / 2 + 1;
            float side = (index & 1) == 0 ? -1f : 1f;
            Vector3 localOffset = new Vector3(
                bodyGuardInnerRadius * 0.55f -
                rank * bodyGuardArcRadiusSpacing * 0.72f * spacing,
                side * rank * bodyGuardArcRadiusSpacing * 0.82f * spacing,
                0f
            );

            FishScript.EscortRole role = rank <= 2
                ? FishScript.EscortRole.Shield
                : FishScript.EscortRole.Flank;

            SpawnBossGuardAtOffset(
                bossTransform,
                guardFishIndex,
                localOffset,
                role,
                index * 0.59f
            );
        }

        Debug.Log(
            "<color=#FFD54F>[ARROWHEAD ESCORT]</color> " +
            guardCount + " body guards entered in formation."
        );
    }

    private void SpawnHalfCircleBossGuardFormation(
        Transform bossTransform,
        int guardFishIndex,
        int availableSlots
    )
    {
        int minArcs = Mathf.Clamp(minimumBodyGuardHalfArcs, 2, 3);
        int maxArcs = Mathf.Clamp(maximumBodyGuardHalfArcs, minArcs, 3);
        int halfArcCount = Random.Range(minArcs, maxArcs + 1);

        if (availableSlots < halfArcCount)
        {
            return;
        }

        int fishPerArc = Mathf.Clamp(
            bodyGuardFishPerHalfArc + (int)currentLevelState / 2,
            2,
            10
        );
        fishPerArc *= Mathf.Max(1, bossEntranceGuardMultiplier);
        fishPerArc = Mathf.Min(fishPerArc, availableSlots / halfArcCount);
        if (fishPerArc <= 0)
        {
            return;
        }

        float halfArcDegrees = Mathf.Clamp(
            bodyGuardHalfArcDegrees,
            100f,
            180f
        );

        for (int arcIndex = 0; arcIndex < halfArcCount; arcIndex++)
        {
            float radius = bodyGuardInnerRadius +
                           arcIndex * bodyGuardArcRadiusSpacing;
            float centerAngle = arcIndex % 2 == 0 ? 0f : 180f;
            float startAngle = centerAngle - halfArcDegrees * 0.5f;

            for (int fishIndexInArc = 0;
                 fishIndexInArc < fishPerArc;
                 fishIndexInArc++)
            {
                float normalized = fishPerArc <= 1
                    ? 0.5f
                    : fishIndexInArc / (float)(fishPerArc - 1);
                float angle = Mathf.Lerp(
                    startAngle,
                    startAngle + halfArcDegrees,
                    normalized
                ) * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f
                ) * radius;

                FishScript.EscortRole role = arcIndex == 0
                    ? FishScript.EscortRole.Shield
                    : arcIndex == 1
                        ? FishScript.EscortRole.Flank
                        : FishScript.EscortRole.Orbit;

                SpawnBossGuardAtOffset(
                    bossTransform,
                    guardFishIndex,
                    localOffset,
                    role,
                    normalized * Mathf.PI * 2f + arcIndex
                );
            }
        }
    }

    private void SpawnBossGuardAtOffset(
        Transform bossTransform,
        int guardFishIndex,
        Vector3 localOffset,
        FishScript.EscortRole role,
        float phase
    )
    {
        Vector3 worldOffset = bossTransform.TransformDirection(localOffset);
        GameObject guard = GetFishFromPool(
            guardFishIndex,
            bossTransform.position + worldOffset,
            bossTransform.position,
            true
        );

        if (!TryGetFishScript(guard, out FishScript guardScript))
        {
            return;
        }

        ApplyBossBodyGuardScaling(guardScript);
        guardScript.SetEscortMovement(
            bossTransform,
            localOffset,
            role,
            phase
        );
    }

    private int GetSingleBossBodyGuardFishIndex()
    {
        bool canUseMediumFish =
            mediumFishStartIndex >= 0 &&
            mediumFishEndIndex >=
                mediumFishStartIndex &&
            mediumFishStartIndex < Fish.Length;

        if (canUseMediumFish &&
            Random.value <=
            bodyGuardUseMediumFishChance)
        {
            return GetMediumFishIndex();
        }

        return GetSmallFishIndex();
    }

    private void ApplyBossBodyGuardScaling(
        FishScript script
    )
    {
        int levelIndex =
            (int)currentLevelState;

        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        float hpMultiplier =
            1f +
            levelIndex * normalHpPerLevel +
            effectiveLoop * endlessHpPerLoop;

        float rewardMultiplier =
            1f +
            levelIndex * normalRewardPerLevel +
            effectiveLoop * endlessRewardPerLoop;

        float speedMultiplier =
            (
                1f +
                levelIndex * normalSpeedPerLevel +
                effectiveLoop * endlessSpeedPerLoop
            ) * bodyGuardSpeedMultiplier;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private bool SpawnBossSupportWave()
    {
        if (!ReferencesAreReady() ||
            !CanSpawnAnotherBossSupportWave())
        {
            return false;
        }

        int activeBossCount =
            activeTargetBosses.Count;

        int normalFishCount =
            bossSupportBaseFish +
            (int)currentLevelState *
            bossSupportFishPerLevel +
            Mathf.Max(
                0,
                activeBossCount - 1
            ) *
            bossSupportFishPerExtraBoss;

        normalFishCount = Mathf.Clamp(
            normalFishCount,
            1,
            maximumBossSupportFishPerWave
        );

        int availableSlots = Mathf.Max(
            0,
            GetBossFishLimit() -
            CountActiveFish()
        );

        if (availableSlots <
            minimumBossSupportFishPerWave)
        {
            return false;
        }

        bool requestedDoubleSupport =
            doubleSupportWavesSpawnedThisBatch <
                maximumDoubleSupportWavesPerBatch &&
            normalFishCount <
                maximumBossSupportFishPerWave &&
            Random.value <=
            bossSupportDoubleChance;

        int requestedFishCount =
            requestedDoubleSupport
                ? Mathf.Min(
                    normalFishCount * 2,
                    maximumBossSupportFishPerWave
                )
                : normalFishCount;

        int supportCount = Mathf.Min(
            requestedFishCount,
            availableSlots
        );

        if (supportCount <
            minimumBossSupportFishPerWave)
        {
            return false;
        }

        bool doubleSupportWave =
            requestedDoubleSupport &&
            supportCount > normalFishCount;

        int groupCount = Mathf.Clamp(
            Mathf.CeilToInt(
                supportCount / 8f
            ),
            1,
            6
        );

        int remainingFish =
            supportCount;

        for (int groupIndex = 0;
             groupIndex < groupCount;
             groupIndex++)
        {
            int remainingGroups =
                groupCount - groupIndex;

            int fishInGroup =
                Mathf.CeilToInt(
                    remainingFish /
                    (float)remainingGroups
                );

            bool fromLeft =
                groupIndex % 2 == 0;

            int supportFishIndex = SelectCrowdFishIndex(
                currentLevelState,
                true
            );

            float laneY =
                GetParadeGroupLaneY(
                    groupIndex,
                    groupCount,
                    supportFishIndex
                );

            int supportPattern = groupIndex % 3;

            if (supportPattern == 0)
            {
                SpawnFastLineParadeGroup(
                    supportFishIndex,
                    laneY,
                    fromLeft,
                    fishInGroup,
                    FishScript.SwimStyle.ArcSweep
                );
            }
            else if (supportPattern == 1)
            {
                SpawnFastVParadeGroup(
                    supportFishIndex,
                    laneY,
                    fromLeft,
                    fishInGroup
                );
            }
            else
            {
                SpawnFastSchoolParadeGroup(
                    supportFishIndex,
                    laneY,
                    fromLeft,
                    fishInGroup
                );
            }

            remainingFish -=
                fishInGroup;
        }

        bossSupportWavesSpawnedThisBatch++;

        if (doubleSupportWave)
        {
            doubleSupportWavesSpawnedThisBatch++;
        }

        string supportLabel = doubleSupportWave
            ? "<color=#FFB74D>[DOUBLE SUPPORT]</color> "
            : "<color=#80CBC4>[BOSS SUPPORT]</color> ";

        Debug.Log(
            supportLabel +
            supportCount +
            " support fish entering | wave " +
            bossSupportWavesSpawnedThisBatch +
            "/" +
            maximumBossSupportWavesPerBatch +
            "."
        );

        return true;
    }

    private bool CanSpawnAnotherBossSupportWave()
    {
        activeTargetBosses.RemoveAll(
            boss =>
                boss == null ||
                !boss.activeSelf
        );

        if (activeTargetBosses.Count == 0 ||
            maximumBossSupportWavesPerBatch <= 0 ||
            bossSupportWavesSpawnedThisBatch >=
                maximumBossSupportWavesPerBatch)
        {
            return false;
        }

        return !stopSupportAfterBossCountDrops ||
               bossSupportStartingBossCount <= 1 ||
               activeTargetBosses.Count >=
                   bossSupportStartingBossCount;
    }

    private void ResetBossSupportState()
    {
        bossSupportWavesSpawnedThisBatch = 0;
        doubleSupportWavesSpawnedThisBatch = 0;
        bossSupportStartingBossCount = 0;
    }

    private void ApplyRecommendedBossSupportSettings()
    {
        bossSupportDoubleChance = 0.18f;
        bossSupportBaseFish = 4;
        bossSupportFishPerLevel = 1;
        bossSupportFishPerExtraBoss = 2;
        maximumBossSupportWavesPerBatch = 2;
        maximumBossSupportFishPerWave = 8;
        minimumBossSupportFishPerWave = 3;
        maximumDoubleSupportWavesPerBatch = 1;
        stopSupportAfterBossCountDrops = true;
        bossSupportFirstDelayMin = 12f;
        bossSupportFirstDelayMax = 18f;
        bossSupportRepeatDelayMin = 22f;
        bossSupportRepeatDelayMax = 30f;
        bossSupportFullScreenRetryDelay = 4f;
        bossSupportSettingsVersion =
            CurrentBossSupportSettingsVersion;
    }

    private int GetBossFishLimit()
    {
        int desiredLimit =
            GetMaxActiveFish() +
            bossBattleExtraFishCapacity;

        return Mathf.Min(
            desiredLimit,
            absoluteMaximumActiveFish
        );
    }
    private void TrySpawnFeatureMiniBoss(float chance)
    {
        if (currentPhase != LevelPhase.FeatureBuildUp ||
            Random.value > chance)
        {
            return;
        }

        SpawnMiniBoss(GetMiniBossIndexForLevel(currentLevelState));
    }

    private void SpawnMiniBoss(int miniBossIndex)
    {
        if (!ReferencesAreReady() ||
            !IsValidMiniBossIndex(miniBossIndex))
        {
            return;
        }

        bool spawnFromLeft = Random.value > 0.5f;
        Vector3 spawnPosition = spawnFromLeft
            ? LeftPos.position
            : RightPos.position;

        Vector3 targetPosition = spawnFromLeft
            ? RightPos.position
            : LeftPos.position;

        spawnPosition += new Vector3(
            0f,
            Random.Range(-3f, 3f),
            0f
        );

        GameObject miniBoss = GetFishFromPool(
            miniBossIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(miniBoss, out FishScript script))
        {
            return;
        }

        ApplyMiniBossScaling(script);

        // ConfigureAsMiniBoss now starts the assigned movement profile.
        // Do not overwrite it with a random normal-fish parade style.
        script.ConfigureAsMiniBoss();
        SpawnBossEntranceGuards(
            miniBoss.transform,
            miniBossIndex,
            script
        );
    }

    private void TrySpawnGimmick(float chance)
    {
        if (Random.value <= chance)
        {
            SpawnGimmickFish();
        }
    }

    private void SpawnGimmickFish()
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        GameObject selectedPrefab = Random.value < 0.5f
            ? BombCrabPrefab
            : LightningChainPrefab;

        if (selectedPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-4f, 4f),
            0f
        );

        Vector3 targetPosition = RightPos.position + new Vector3(
            0f,
            spawnPosition.y - LeftPos.position.y,
            0f
        );

        GameObject gimmick = Instantiate(
            selectedPrefab,
            spawnPosition,
            Quaternion.identity,
            FishSpawnPosition
        );

        spawnedGimmicks.Add(gimmick);

        float angle = Mathf.Atan2(
            targetPosition.y - spawnPosition.y,
            targetPosition.x - spawnPosition.x
        ) * Mathf.Rad2Deg;

        gimmick.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        gimmick.SetActive(true);

        if (gimmick.TryGetComponent<FishScript>(out FishScript script))
        {
            ApplyNormalScaling(script);
            script.SetMovementStyle(FishScript.SwimStyle.ArcSweep);
        }
    }

    private void SpawnScaledNormalFish(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        FishScript.SwimStyle style
    )
    {
        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(fish, out FishScript script))
        {
            return;
        }

        ApplyNormalScaling(script);
        float sweep = style == FishScript.SwimStyle.LaneGlide
            ? paradeRouteSweepAmplitude * 0.30f
            : paradeRouteSweepAmplitude;
        script.SetParadeRoute(
            targetPosition,
            style,
            sweep,
            paradeRouteSweepFrequency
        );
    }

    private void ApplyNormalScaling(FishScript script)
    {
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        // Clean v20 balance is absolute. Normal levels change fish mix rather
        // than multiplying the same fish HP/reward again. Endless loops may
        // still add the small explicit progression configured below.
        float hpMultiplier = 1f + effectiveLoop * endlessHpPerLoop;
        float rewardMultiplier = 1f + effectiveLoop * endlessRewardPerLoop;
        float speedMultiplier = 1f + effectiveLoop * endlessSpeedPerLoop;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private void ApplyMiniBossScaling(FishScript script)
    {
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        script.ApplyRuntimeScaling(
            1f + effectiveLoop * endlessHpPerLoop,
            1f + effectiveLoop * endlessRewardPerLoop,
            1f + effectiveLoop * endlessSpeedPerLoop
        );
    }

    private void ApplyBossScaling(
        FishScript script,
        int simultaneousBossCount = 1
    )
    {
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        int extraBossCount = Mathf.Max(0, simultaneousBossCount - 1);
        float simultaneousHpFactor = Mathf.Clamp(
            1f - extraBossCount * simultaneousBossHpReductionPerExtraBoss,
            0.72f,
            1f
        );

        script.ApplyRuntimeScaling(
            (1f + effectiveLoop * endlessHpPerLoop) * simultaneousHpFactor,
            1f + effectiveLoop * endlessRewardPerLoop,
            1f + effectiveLoop * endlessSpeedPerLoop
        );
    }

    private GameObject GetFishFromPool(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        bool schoolingMember = false,
        bool allowCrowdedCriticalSpawn = false
    )
    {
        if (Fish == null || Fish.Length == 0)
        {
            return null;
        }

        fishIndex = Mathf.Clamp(fishIndex, 0, Fish.Length - 1);

        if (Fish[fishIndex] == null)
        {
            Debug.LogError(
                "Fish array element " + fishIndex + " is empty."
            );
            return null;
        }

        if (!fishPool.ContainsKey(fishIndex))
        {
            fishPool[fishIndex] = new List<GameObject>();
        }

        FishGameplayProfile gameplayProfile = null;
        if (Fish[fishIndex].TryGetComponent<FishScript>(out FishScript prefabFishScript))
        {
            gameplayProfile = prefabFishScript.GetGameplayProfile();
        }

        if (spawnDirectorService != null &&
            !spawnDirectorService.TryResolveSpawnPosition(
                gameplayProfile,
                spawnPosition,
                schoolingMember,
                out spawnPosition
            ))
        {
            if (!allowCrowdedCriticalSpawn)
            {
                return null;
            }

            // The pre-boss panic rush can temporarily occupy every normal
            // correction lane. Push the critical fish farther outside the
            // viewport instead of cancelling the boss and falsely clearing
            // the level. It will still swim in naturally from off-screen.
            spawnPosition = GetCriticalOffScreenFallbackPosition(
                spawnPosition,
                gameplayProfile
            );

            Debug.LogWarning(
                "<color=#FFB74D>[BOSS SPAWN FALLBACK]</color> " +
                "Entry lanes were crowded, so fish index " +
                fishIndex +
                " was moved farther off-screen before spawning.",
                this
            );
        }

        GameObject fishInstance = null;
        List<GameObject> pool = fishPool[fishIndex];
        float angle = Mathf.Atan2(
            targetPosition.y - spawnPosition.y,
            targetPosition.x - spawnPosition.x
        ) * Mathf.Rad2Deg;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeSelf)
            {
                fishInstance = pool[i];
                break;
            }
        }

        if (fishInstance == null)
        {
            fishInstance = Instantiate(
                Fish[fishIndex],
                spawnPosition,
                spawnRotation,
                FishSpawnPosition
            );

            pool.Add(fishInstance);
        }
        else
        {
            fishInstance.transform.position = spawnPosition;
            fishInstance.transform.rotation = spawnRotation;
        }

        fishInstance.SetActive(true);

        if (fishInstance.TryGetComponent<SpriteRenderer>(out SpriteRenderer renderer))
        {
            renderer.sortingOrder = orderLayer;
        }

        orderLayer += 2;

        if (orderLayer > 30000)
        {
            orderLayer = 0;
        }

        return fishInstance;
    }

    private Vector3 GetCriticalOffScreenFallbackPosition(
        Vector3 desiredPosition,
        FishGameplayProfile gameplayProfile
    )
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return desiredPosition;
        }

        float depth = Mathf.Abs(
            desiredPosition.z - gameplayCamera.transform.position.z
        );
        Vector3 screenCenter = gameplayCamera.ViewportToWorldPoint(
            new Vector3(0.5f, 0.5f, depth)
        );
        screenCenter.z = desiredPosition.z;

        Vector2 outwardDirection =
            (Vector2)(desiredPosition - screenCenter);

        if (outwardDirection.sqrMagnitude < 0.0001f)
        {
            outwardDirection = Vector2.left;
        }
        else
        {
            outwardDirection.Normalize();
        }

        float profileSpacing = gameplayProfile != null
            ? gameplayProfile.minimumSpawnDistance *
              gameplayProfile.fishSizeSpacingMultiplier
            : 2.5f;

        float extraDistance = Mathf.Max(
            2.5f,
            profileSpacing * 1.25f
        );

        Vector3 fallback = desiredPosition +
            (Vector3)(outwardDirection * extraDistance);
        fallback.z = desiredPosition.z;
        return fallback;
    }

    private int[] GetBossTargetsForLevel(GameLevel level)
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return level1BossTargets;

            case GameLevel.Level2_School:
                return level2BossTargets;

            case GameLevel.Level3_Circle:
                return level3BossTargets;

            case GameLevel.Level4_Cross:
                return level4BossTargets;

            case GameLevel.Level5_Festival:
                return level5BossTargets;

            case GameLevel.Level6_BossOcean:
                return level6BossTargets;

            default:
                return level1BossTargets;
        }
    }

    private int GetMiniBossIndexForLevel(GameLevel level)
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
            case GameLevel.Level2_School:
                return miniBossStartIndex;

            case GameLevel.Level3_Circle:
                return Random.Range(
                    miniBossStartIndex,
                    Mathf.Min(miniBossStartIndex + 2, miniBossEndIndex + 1)
                );

            case GameLevel.Level4_Cross:
                return Mathf.Clamp(
                    miniBossStartIndex + 1,
                    miniBossStartIndex,
                    miniBossEndIndex
                );

            case GameLevel.Level5_Festival:
                return Random.Range(
                    Mathf.Clamp(
                        miniBossStartIndex + 1,
                        miniBossStartIndex,
                        miniBossEndIndex
                    ),
                    miniBossEndIndex + 1
                );

            case GameLevel.Level6_BossOcean:
                return miniBossEndIndex;

            default:
                return miniBossStartIndex;
        }
    }

    private int GetSmallFishIndex()
    {
        return GetRandomParadeEligibleIndexInRange(
            smallFishStartIndex,
            smallFishEndIndex
        );
    }

    private int GetMediumFishIndex()
    {
        bool hasExtraNormalFish =
            extraNormalFishStartIndex >= 0 &&
            extraNormalFishStartIndex < Fish.Length;

        if (hasExtraNormalFish && Random.value < 0.12f)
        {
            // Select only extra fish explicitly classified as Medium.
            int selectedExtra = -1;
            int validExtraCount = 0;

            for (int i = extraNormalFishStartIndex;
                 i < Fish.Length;
                 i++)
            {
                if (Fish[i] == null ||
                    !Fish[i].TryGetComponent<FishScript>(
                        out FishScript extraScript
                    ) ||
                    extraScript.GetFishTier() != FishTier.Medium ||
                    !extraScript.CanJoinParade())
                {
                    continue;
                }

                validExtraCount++;

                if (Random.Range(0, validExtraCount) == 0)
                {
                    selectedExtra = i;
                }
            }

            if (selectedExtra >= 0)
            {
                return selectedExtra;
            }
        }

        return GetRandomParadeEligibleIndexInRange(
            mediumFishStartIndex,
            mediumFishEndIndex
        );
    }

    private int GetRandomParadeEligibleIndexInRange(
        int startIndex,
        int endIndex
    )
    {
        if (Fish == null || Fish.Length == 0)
        {
            return 0;
        }

        int safeStart = Mathf.Clamp(
            startIndex,
            0,
            Fish.Length - 1
        );

        int safeEnd = Mathf.Clamp(
            endIndex,
            safeStart,
            Fish.Length - 1
        );

        int selectedIndex = -1;
        int validCount = 0;

        for (int i = safeStart; i <= safeEnd; i++)
        {
            if (!IsFishEligibleForParade(i))
            {
                continue;
            }

            validCount++;

            if (Random.Range(0, validCount) == 0)
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex >= 0)
        {
            return selectedIndex;
        }

        Debug.LogWarning(
            "No parade-eligible fish found in range " +
            safeStart + "-" + safeEnd +
            ". Falling back to the first range element.",
            this
        );

        return safeStart;
    }

    private bool IsFishEligibleForParade(int fishIndex)
    {
        if (Fish == null ||
            fishIndex < 0 ||
            fishIndex >= Fish.Length ||
            Fish[fishIndex] == null)
        {
            return false;
        }

        if (!Fish[fishIndex].TryGetComponent<FishScript>(
                out FishScript script
            ))
        {
            return false;
        }

        return script.CanJoinParade();
    }

    private int SelectCrowdFishIndex(
        GameLevel level,
        bool allowSpecial
    )
    {
        if (useManualParadeSelection)
        {
            int manualIndex = SelectManualParadeFishIndex();

            if (manualIndex >= 0)
            {
                return manualIndex;
            }

            if (!fallbackToTierSelection)
            {
                return -1;
            }
        }

        if (allowSpecial)
        {
            float specialChance = Mathf.Clamp01(
                bossArrivalSpecialChanceBase +
                (int)level * bossArrivalSpecialChancePerLevel
            );

            if (Random.value <= specialChance)
            {
                int specialIndex = GetSpecialFishIndex(true);

                if (specialIndex >= 0)
                {
                    return specialIndex;
                }
            }
        }

        return Random.value < GetSmallFishWeight(level)
            ? GetSmallFishIndex()
            : GetMediumFishIndex();
    }

    private int SelectManualParadeFishIndex()
    {
        if (Fish == null || Fish.Length == 0)
        {
            return -1;
        }

        float totalWeight = 0f;

        for (int i = 0; i < Fish.Length; i++)
        {
            GameObject candidate = Fish[i];

            if (candidate == null ||
                !candidate.TryGetComponent<FishScript>(
                    out FishScript script
                ) ||
                !script.IsExplicitlyAllowedInParade())
            {
                continue;
            }

            totalWeight += script.GetParadeSelectionWeight();
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.value * totalWeight;

        for (int i = 0; i < Fish.Length; i++)
        {
            GameObject candidate = Fish[i];

            if (candidate == null ||
                !candidate.TryGetComponent<FishScript>(
                    out FishScript script
                ) ||
                !script.IsExplicitlyAllowedInParade())
            {
                continue;
            }

            roll -= script.GetParadeSelectionWeight();

            if (roll <= 0f)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetSpecialFishIndex(
        bool requireParadeEligibility
    )
    {
        if (specialFishIndices != null &&
            specialFishIndices.Length > 0)
        {
            int start = Random.Range(0, specialFishIndices.Length);

            for (int offset = 0;
                 offset < specialFishIndices.Length;
                 offset++)
            {
                int arrayIndex =
                    (start + offset) % specialFishIndices.Length;

                int fishIndex = specialFishIndices[arrayIndex];

                if (fishIndex < 0 ||
                    Fish == null ||
                    fishIndex >= Fish.Length ||
                    Fish[fishIndex] == null)
                {
                    continue;
                }

                if (requireParadeEligibility &&
                    !IsFishEligibleForParade(fishIndex))
                {
                    continue;
                }

                return fishIndex;
            }
        }

        // Inspector list is optional. Detect FishTier.Special without
        // allocating a temporary list.
        int selectedIndex = -1;
        int validCount = 0;

        if (Fish == null)
        {
            return -1;
        }

        for (int i = 0; i < Fish.Length; i++)
        {
            if (Fish[i] == null ||
                !Fish[i].TryGetComponent<FishScript>(
                    out FishScript script
                ) ||
                script.GetFishTier() != FishTier.Special ||
                (requireParadeEligibility &&
                 !script.CanJoinParade()))
            {
                continue;
            }

            validCount++;

            if (Random.Range(0, validCount) == 0)
            {
                selectedIndex = i;
            }
        }

        return selectedIndex;
    }

    private int SelectBalancedAmbientFishIndex(
        FishLevelPopulationProfile population
    )
    {
        if (Fish == null || Fish.Length == 0)
        {
            return -1;
        }

        if (spawnDirectorService != null)
        {
            spawnDirectorService.RefreshPopulationSnapshot();
        }

        float totalWeight = 0f;
        for (int i = 0; i < Fish.Length; i++)
        {
            float weight = GetBalancedAmbientCandidateWeight(
                i,
                population
            );
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < Fish.Length; i++)
        {
            roll -= GetBalancedAmbientCandidateWeight(
                i,
                population
            );

            if (roll <= 0f)
            {
                return i;
            }
        }

        return -1;
    }

    private float GetBalancedAmbientCandidateWeight(
        int fishIndex,
        FishLevelPopulationProfile population
    )
    {
        if (Fish == null ||
            fishIndex < 0 ||
            fishIndex >= Fish.Length ||
            Fish[fishIndex] == null ||
            !Fish[fishIndex].TryGetComponent<FishScript>(
                out FishScript script
            ) ||
            !script.CanSpawnAmbiently() ||
            !IsAmbientSpeciesBelowLimit(script))
        {
            return 0f;
        }

        FishTier tier = script.GetFishTier();
        if (tier != FishTier.Small &&
            tier != FishTier.Medium &&
            tier != FishTier.Special)
        {
            return 0f;
        }

        FishGameplayProfile profile = script.GetGameplayProfile();
        if (profile != null)
        {
            if (CurrentLevelNumber < profile.minimumLevel ||
                (spawnDirectorService != null &&
                 spawnDirectorService.CountActiveProfile(profile) >=
                 profile.maximumSimultaneousCount))
            {
                return 0f;
            }

            if (population != null &&
                spawnDirectorService != null &&
                (int)profile.sizeClass >= (int)FishSizeClass.Large &&
                spawnDirectorService.CountActiveSizeClass(
                    FishSizeClass.Large
                ) >= population.maximumLargeFish)
            {
                return 0f;
            }
        }

        float repeatMultiplier = spawnDirectorService != null
            ? spawnDirectorService.GetFishRepeatWeightMultiplier(fishIndex)
            : 1f;
        float populationMultiplier =
            spawnDirectorService != null
                ? spawnDirectorService
                    .GetProfilePopulationBalanceMultiplier(
                        profile,
                        population
                    )
                : 1f;

        if (profile == null)
        {
            float smallShare = GetSmallFishWeight(currentLevelState);
            populationMultiplier *= tier == FishTier.Small
                ? Mathf.Max(0.15f, smallShare)
                : tier == FishTier.Medium
                    ? Mathf.Max(0.15f, 1f - smallShare)
                    : 0.08f;
        }

        return Mathf.Max(0f, script.GetAmbientSpawnWeight()) *
               Mathf.Max(0f, repeatMultiplier) *
               Mathf.Max(0f, populationMultiplier);
    }

    private int SelectAmbientFishIndex(FishTier requestedTier)
    {
        if (Fish == null || Fish.Length == 0)
        {
            return -1;
        }

        int rangeStart = requestedTier == FishTier.Small
            ? smallFishStartIndex
            : mediumFishStartIndex;

        int rangeEnd = requestedTier == FishTier.Small
            ? smallFishEndIndex
            : mediumFishEndIndex;

        int safeStart = Mathf.Clamp(rangeStart, 0, Fish.Length - 1);
        int safeEnd = Mathf.Clamp(rangeEnd, safeStart, Fish.Length - 1);
        float totalWeight = 0f;

        for (int i = safeStart; i <= safeEnd; i++)
        {
            if (Fish[i] == null ||
                !Fish[i].TryGetComponent<FishScript>(
                    out FishScript script
                ) ||
                script.GetFishTier() != requestedTier ||
                !script.CanSpawnAmbiently() ||
                !IsAmbientSpeciesBelowLimit(script))
            {
                continue;
            }

            FishGameplayProfile profile = script.GetGameplayProfile();
            if (profile != null &&
                (CurrentLevelNumber < profile.minimumLevel ||
                 (spawnDirectorService != null &&
                  spawnDirectorService.CountActiveProfile(profile) >=
                  profile.maximumSimultaneousCount)))
            {
                continue;
            }

            float repeatMultiplier = spawnDirectorService != null
                ? spawnDirectorService.GetFishRepeatWeightMultiplier(i)
                : 1f;
            FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
            float rarityMultiplier = population != null && profile != null
                ? population.GetRarityWeight(profile.rarity)
                : 1f;
            float sizeMultiplier = population != null && profile != null
                ? population.GetSizeWeight(profile.sizeClass)
                : 1f;
            totalWeight += script.GetAmbientSpawnWeight() *
                repeatMultiplier * rarityMultiplier * sizeMultiplier;
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = Random.value * totalWeight;

        for (int i = safeStart; i <= safeEnd; i++)
        {
            if (Fish[i] == null ||
                !Fish[i].TryGetComponent<FishScript>(
                    out FishScript script
                ) ||
                script.GetFishTier() != requestedTier ||
                !script.CanSpawnAmbiently() ||
                !IsAmbientSpeciesBelowLimit(script))
            {
                continue;
            }

            FishGameplayProfile profile = script.GetGameplayProfile();
            if (profile != null &&
                (CurrentLevelNumber < profile.minimumLevel ||
                 (spawnDirectorService != null &&
                  spawnDirectorService.CountActiveProfile(profile) >=
                  profile.maximumSimultaneousCount)))
            {
                continue;
            }

            float repeatMultiplier = spawnDirectorService != null
                ? spawnDirectorService.GetFishRepeatWeightMultiplier(i)
                : 1f;
            FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
            float rarityMultiplier = population != null && profile != null
                ? population.GetRarityWeight(profile.rarity)
                : 1f;
            float sizeMultiplier = population != null && profile != null
                ? population.GetSizeWeight(profile.sizeClass)
                : 1f;
            roll -= script.GetAmbientSpawnWeight() *
                repeatMultiplier * rarityMultiplier * sizeMultiplier;

            if (roll <= 0f)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsAmbientSpeciesBelowLimit(
        FishScript prefabScript
    )
    {
        if (prefabScript == null)
        {
            return false;
        }

        GameManager manager = GameManager.Instance;

        if (manager == null || manager.fishInScreenList == null)
        {
            return true;
        }

        int maximum = prefabScript.GetMaximumSimultaneousCount();
        int activeCount = 0;
        int speciesId = prefabScript.id;

        for (int i = 0; i < manager.fishInScreenList.Count; i++)
        {
            FishScript activeFish = manager.fishInScreenList[i];

            if (activeFish == null ||
                !activeFish.gameObject.activeInHierarchy ||
                activeFish.id != speciesId)
            {
                continue;
            }

            activeCount++;

            if (activeCount >= maximum)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidBigBossIndex(int index)
    {
        return index >= bigBossStartIndex &&
               index <= bigBossEndIndex &&
               index < Fish.Length;
    }

    private bool IsValidMiniBossIndex(int index)
    {
        return index >= miniBossStartIndex &&
               index <= miniBossEndIndex &&
               index < Fish.Length;
    }

    private GameLevel GetNextLevel(GameLevel level)
    {
        if (level == GameLevel.Level6_BossOcean)
        {
            return GameLevel.Level1_Beginner;
        }

        return (GameLevel)((int)level + 1);
    }

    private FishLevelPopulationProfile GetCurrentLevelPopulationProfile()
    {
        if (levelPopulationProfiles == null || levelPopulationProfiles.Length == 0)
        {
            return null;
        }

        int levelIndex = Mathf.Clamp(
            (int)currentLevelState,
            0,
            levelPopulationProfiles.Length - 1
        );
        return levelPopulationProfiles[levelIndex];
    }

    private float GetWarmupDuration(GameLevel level)
    {
        FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
        if (population != null && population.bossSpawnTiming > 0f)
        {
            return population.bossSpawnTiming;
        }

        return level1Warmup +
               (int)level * warmupIncreasePerLevel;
    }

    private float GetBossWarningDelay(GameLevel level)
    {
        return bossWarningBaseDelay +
               bossArrivalExtraBaseDelay +
               (int)level * (
                   bossWarningIncreasePerLevel +
                   bossArrivalExtraPerLevel
               );
    }

    private float GetBetweenBossDelay(GameLevel level)
    {
        return betweenTargetBossBaseDelay +
               (int)level * betweenTargetBossIncreasePerLevel;
    }

    private float GetPostBossRecoveryDelay(GameLevel level)
    {
        return postBossRecoveryBaseDelay +
               (int)level * postBossRecoveryIncreasePerLevel;
    }

    private float GetNextAmbientDelay()
    {
        float levelAcceleration =
            (int)currentLevelState * 0.025f;

        float phaseMultiplier =
            currentPhase == LevelPhase.BossBattle ? 0.85f : 1f;

        float delay = Mathf.Max(
            0.20f,
            Random.Range(ambientDelayMin, ambientDelayMax) *
            phaseMultiplier -
            levelAcceleration
        );
        float performanceMultiplier = spawnDirectorService != null
            ? Mathf.Max(0.25f, spawnDirectorService.SpawnRateMultiplier)
            : 1f;
        return delay / performanceMultiplier;
    }

    private float GetNextParadeDelay()
    {
        float levelDelay = (int)currentLevelState * 0.25f;

        return Random.Range(
            paradeDelayMin,
            paradeDelayMax
        ) + levelDelay;
    }

    private float GetBossSupportDelay()
    {
        int activeBossCount = Mathf.Max(
            1,
            activeTargetBosses.Count
        );

        float baseDelay =
            Random.Range(
                bossSupportRepeatDelayMin,
                bossSupportRepeatDelayMax
            ) +
            (int)currentLevelState *
            0.45f;

        float multipleBossReduction =
            1f +
            Mathf.Max(
                0,
                activeBossCount - 1
            ) * 0.08f;

        return Mathf.Max(
            bossSupportRepeatDelayMin,
            baseDelay / multipleBossReduction
        );
    }

    private float GetBossSupportFirstDelay()
    {
        return Random.Range(
            bossSupportFirstDelayMin,
            bossSupportFirstDelayMax
        );
    }

    private int GetFormationGrowth()
    {
        int levelGrowth = (int)currentLevelState;
        int loopGrowth = Mathf.Clamp(loopMultiplier, 0, 4);
        return levelGrowth + loopGrowth;
    }

    private int GetMaxActiveFish()
    {
        FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
        int desired = population != null
            ? population.maximumActiveFish
            : maxActiveFishBase +
              (int)currentLevelState * maxActiveFishPerLevel;

        return Mathf.Min(
            Mathf.Max(1, desired),
            Mathf.Max(1, absoluteMaximumActiveFish)
        );
    }

    private int GetMinimumActiveFish()
    {
        FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
        int desired = population != null
            ? population.minimumActiveFish
            : minimumActiveFishBase +
              (int)currentLevelState * minimumActiveFishPerLevel;

        return Mathf.Clamp(
            desired,
            0,
            GetMaxActiveFish()
        );
    }

    private float GetSmallFishWeight(GameLevel level)
    {
        FishLevelPopulationProfile population = GetCurrentLevelPopulationProfile();
        if (population != null)
        {
            return population.GetSmallFishSelectionWeight();
        }

        switch (level)
        {
            case GameLevel.Level1_Beginner: return 0.58f;
            case GameLevel.Level2_School: return 0.56f;
            case GameLevel.Level3_Circle: return 0.54f;
            case GameLevel.Level4_Cross: return 0.52f;
            case GameLevel.Level5_Festival: return 0.50f;
            default: return 0.48f;
        }
    }

    /// <summary>
    /// Optional extra parade requested by EpicBossController during its
    /// introduction. It reuses the existing fish pools and active-fish
    /// limits; no normal fish are instantiated directly.
    /// </summary>
    public void PlayEpicBossIntroductionParade(
        int waveCount = 1,
        float waveGap = 0.65f
    )
    {
        if (!gameObject.activeInHierarchy || !ReferencesAreReady())
        {
            return;
        }

        StartCoroutine(
            EpicBossIntroductionParadeRoutine(
                Mathf.Clamp(waveCount, 0, 2),
                Mathf.Max(0.1f, waveGap)
            )
        );
    }

    private IEnumerator EpicBossIntroductionParadeRoutine(
        int waveCount,
        float waveGap
    )
    {
        for (int i = 0; i < waveCount; i++)
        {
            if (epicBossIntroductionUsesRoyalConvoy)
            {
                int totalFish = Mathf.Min(
                    16 + (int)currentLevelState * 2,
                    Mathf.Max(0, GetBossFishLimit() - CountActiveFish())
                );

                SpawnRoyalArmadaSignature(
                    totalFish,
                    100 + i
                );
            }
            else
            {
                SpawnPreBossParadeWave(
                    currentLevelState,
                    100 + i
                );
            }

            if (i < waveCount - 1)
            {
                yield return new WaitForSeconds(waveGap);
            }
        }
    }

    private IEnumerator WaitForBattlefieldClear()
    {
        // Keep the battlefield alive for a moment after the final target
        // boss presentation instead of instantly clearing normal fish.
        yield return new WaitForSeconds(
            Mathf.Max(0f, bossClearInitialDelay)
        );

        int minimumParades = Mathf.Clamp(
            postBossParadeMinimum,
            0,
            2
        );

        int maximumParades = Mathf.Clamp(
            postBossParadeMaximum,
            minimumParades,
            2
        );

        int paradeCount = Random.Range(
            minimumParades,
            maximumParades + 1
        );

        for (int paradeIndex = 0;
             paradeIndex < paradeCount;
             paradeIndex++)
        {
            SpawnPostBossCelebrationParade(paradeIndex);

            if (paradeIndex < paradeCount - 1)
            {
                yield return new WaitForSeconds(
                    postBossParadeGap
                );
            }
        }

        float elapsed = 0f;
        int targetRemainingFish = Mathf.Max(
            0,
            remainingFishBeforeLevelChange
        );

        while (CountActiveNonBossFish() > targetRemainingFish &&
               elapsed < battlefieldClearMaximumWait)
        {
            elapsed += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void SpawnPostBossCelebrationParade(int paradeIndex)
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int celebrationLimit = Mathf.Min(
            GetMaxActiveFish() + preBossExtraFishCapacity,
            absoluteMaximumActiveFish
        );

        int availableSlots = Mathf.Max(
            0,
            celebrationLimit - CountActiveFish()
        );

        int desiredFish = 12 +
            (int)currentLevelState * 2;

        int fishCount = Mathf.Min(
            desiredFish,
            availableSlots
        );

        if (fishCount <= 0)
        {
            return;
        }

        if (paradeIndex % 2 == 0)
        {
            SpawnMirroredFleetParade(
                SelectCrowdFishIndex(
                    currentLevelState,
                    true
                ),
                SelectCrowdFishIndex(
                    currentLevelState,
                    true
                ),
                fishCount
            );
        }
        else
        {
            SpawnTreasureRingParade(
                SelectCrowdFishIndex(
                    currentLevelState,
                    true
                ),
                fishCount,
                Random.value < 0.5f
            );
        }
    }

    private int CountActiveNonBossFish()
    {
        int count = 0;
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject obj = pool[i];
                if (obj == null || !obj.activeSelf) continue;
                FishScript fish = obj.GetComponent<FishScript>();
                if (fish == null || !fish.IsBoss()) count++;
            }
        }
        return count;
    }

    private void SetAllActiveFishMovement(FishScript.SwimStyle style)
    {
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject obj = pool[i];
                if (obj == null || !obj.activeSelf) continue;
                FishScript fish = obj.GetComponent<FishScript>();
                if (fish != null) fish.SetMovementStyle(style);
            }
        }
    }

    private int CountActiveFish()
    {
        int count = 0;

        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].activeSelf)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void BuildPoolDictionary()
    {
        fishPool.Clear();

        for (int i = 0; i < Fish.Length; i++)
        {
            fishPool[i] = new List<GameObject>();
        }
    }

    private void ClearSpawnedGimmicks()
    {
        for (int i = spawnedGimmicks.Count - 1; i >= 0; i--)
        {
            GameObject gimmick = spawnedGimmicks[i];
            if (gimmick == null)
            {
                spawnedGimmicks.RemoveAt(i);
                continue;
            }

            FishScript fish = gimmick.GetComponent<FishScript>();
            bool canRemoveNow =
                !gimmick.activeSelf ||
                fish == null ||
                fish.IsDeadOrDying ||
                fish.IsFullyOutsidePaddedView();

            if (canRemoveNow)
            {
                Destroy(gimmick);
                spawnedGimmicks.RemoveAt(i);
                continue;
            }

            fish.ForceLevelTransitionNaturalExit(
                emergencyLevelExitSpeedMultiplier * 1.55f
            );
        }

        if (spawnedGimmicks.Count > 0 &&
            gimmickCleanupCoroutine == null)
        {
            gimmickCleanupCoroutine =
                StartCoroutine(CleanupGimmicksAfterExit());
        }
    }

    private IEnumerator CleanupGimmicksAfterExit()
    {
        float nextReassertAt = Time.time + 1f;
        float nextLogAt = Time.time + forceCleanupTimeout;

        while (spawnedGimmicks.Count > 0)
        {
            for (int i = spawnedGimmicks.Count - 1; i >= 0; i--)
            {
                GameObject gimmick = spawnedGimmicks[i];
                if (gimmick == null)
                {
                    spawnedGimmicks.RemoveAt(i);
                    continue;
                }

                FishScript fish = gimmick.GetComponent<FishScript>();
                if (!gimmick.activeSelf || fish == null ||
                    fish.IsDeadOrDying ||
                    fish.IsFullyOutsidePaddedView())
                {
                    Destroy(gimmick);
                    spawnedGimmicks.RemoveAt(i);
                    continue;
                }

                if (Time.time >= nextReassertAt)
                {
                    fish.ForceLevelTransitionNaturalExit(
                        emergencyLevelExitSpeedMultiplier * 1.55f
                    );
                }
            }

            if (Time.time >= nextReassertAt)
            {
                nextReassertAt = Time.time + 1f;
            }

            if (spawnedGimmicks.Count > 0 &&
                Time.time >= nextLogAt)
            {
                Debug.LogWarning(
                    "[TIDE CHANGE] Waiting for " +
                    spawnedGimmicks.Count +
                    " event fish to finish natural off-screen exits."
                );
                nextLogAt = Time.time + forceCleanupTimeout;
            }

            yield return new WaitForSeconds(0.20f);
        }

        gimmickCleanupCoroutine = null;
    }

    private bool TryGetFishScript(
        GameObject fish,
        out FishScript script
    )
    {
        script = null;

        if (fish == null)
        {
            return false;
        }

        if (!fish.TryGetComponent<FishScript>(out script))
        {
            Debug.LogError(
                fish.name + " does not contain FishScript."
            );
            fish.SetActive(false);
            return false;
        }

        return true;
    }

    private bool ReferencesAreReady()
    {
        return FishSpawnPosition != null &&
               LeftPos != null &&
               RightPos != null;
    }

    private void FitSpriteToScreen(SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null ||
            targetRenderer.sprite == null ||
            Camera.main == null)
        {
            return;
        }

        float screenHeight = Camera.main.orthographicSize * 2f;
        float screenWidth = screenHeight * Screen.width / Screen.height;
        float spriteWidth = targetRenderer.sprite.bounds.size.x;
        float spriteHeight = targetRenderer.sprite.bounds.size.y;

        if (spriteWidth <= 0f || spriteHeight <= 0f)
        {
            return;
        }

        float scaleX = screenWidth / spriteWidth;
        float scaleY = screenHeight / spriteHeight;

        targetRenderer.transform.localScale = new Vector3(
            scaleX,
            scaleY,
            1f
        );
    }

    [ContextMenu("Apply v21 Mixed Parade + Boss Companion Preset")]
    public void ApplyV21MixedParadePreset()
    {
        pauseNormalSpawningDuringParade = false;
        clearNormalFishBeforeParade = false;
        allowAmbientAfterOpeningParadeVisible = true;
        mixedParadeExtraFishCapacity = 8;

        smallParadeVisualGap = 1.01f;
        mediumParadeVisualGap = 1.02f;
        largeParadeVisualGap = 1.05f;
        specialParadeVisualGap = 1.08f;
        paradeMinimumGapPadding = 0.01f;
        organizedParadeSpacingMultiplier = 1f;
        organizedParadeSlotBreathing = 0.015f;
        naturalSchoolLongitudinalSpacingMultiplier = 1.03f;
        naturalSchoolLateralSpacingMultiplier = 1.04f;
        naturalSchoolRowStagger = 0.16f;

        keepAmbientFishWithBoss = true;
        minimumAmbientFishDuringBossBattle = 8;
        bossAmbientRefillBurst = 2;
        bossAmbientRefillInterval = 0.35f;

        ValidateAndRepairConfiguration();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Apply Clean Parade Preset")]
    public void ApplyCleanParadePreset()
    {
        playStandardParadeAtEveryLevelStart = true;
        levelOpeningParadeDelay = 0.25f;
        levelOpeningParadeWaveCount = 1;
        levelOpeningParadeWaveGap = 0.65f;
        levelOpeningParadeFishCountBase = 10;
        levelOpeningParadeFishPerLevel = 1;
        levelOpeningParadeSpeedMultiplier = 1f;
        levelOpeningParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.StraightLine,
            OrganizedParadePattern.VFormation,
            OrganizedParadePattern.Arrow,
            OrganizedParadePattern.Wave,
            OrganizedParadePattern.Grid
        };

        pauseNormalSpawningDuringParade = false;
        clearNormalFishBeforeParade = false;
        allowAmbientAfterOpeningParadeVisible = true;
        mixedParadeExtraFishCapacity = 8;
        maximumParadeFishCount = 18;
        paradeCompletionTimeout = 18f;
        paradeAmbientClearTimeout = 1.8f;
        paradeAmbientExitSpeedMultiplier = 1.8f;
        paradeTimeoutCleanupGrace = 2.5f;
        paradeVisibilityPadding = 0.01f;

        smallParadeVisualGap = 1.01f;
        mediumParadeVisualGap = 1.02f;
        largeParadeVisualGap = 1.05f;
        specialParadeVisualGap = 1.08f;
        paradeMinimumGapPadding = 0.01f;
        organizedParadeSpacingMultiplier = 1f;
        organizedParadeSlotBreathing = 0.02f;
        naturalSchoolLongitudinalSpacingMultiplier = 1.03f;
        naturalSchoolLateralSpacingMultiplier = 1.04f;
        naturalSchoolRowStagger = 0.16f;

        keepAmbientFishWithBoss = true;
        minimumAmbientFishDuringBossBattle = 8;
        bossAmbientRefillBurst = 2;
        bossAmbientRefillInterval = 0.35f;

        paradeWorldSpeed = 2.35f;
        paradeWorldSpeedPerLevel = 0.04f;
        paradeFollowerCorrection = 3.8f;
        paradeFollowerCatchUpMultiplier = 1.65f;
        paradeFollowerSlotWobble = 0.035f;
        preBossParadeSpeedBase = 0.92f;
        preBossParadeSpeedPerLevel = 0.01f;
        paradeRouteSweepAmplitude = 0.22f;
        paradeRouteSweepFrequency = 0.82f;

        paradeDelayMin = 12f;
        paradeDelayMax = 18f;
        standaloneFeatureParadeChance = 0.35f;
        standaloneFeatureParadeDelay = 0.35f;
        standaloneFeatureParadeWaveCount = 1;
        standaloneFeatureParadeWaveGap = 0.75f;
        standaloneFeatureParadeFishCountBase = 11;
        standaloneFeatureParadeFishPerLevel = 1;
        standaloneFeatureParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.Diamond,
            OrganizedParadePattern.Spiral,
            OrganizedParadePattern.MixedFormation,
            OrganizedParadePattern.ProtectedCenter,
            OrganizedParadePattern.Wave
        };

        beginnerParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.StraightLine,
            OrganizedParadePattern.Diagonal,
            OrganizedParadePattern.VFormation,
            OrganizedParadePattern.LeaderAndFollowers
        };
        largeParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.Arrow,
            OrganizedParadePattern.Grid,
            OrganizedParadePattern.Wave,
            OrganizedParadePattern.Circle,
            OrganizedParadePattern.Spiral,
            OrganizedParadePattern.LeaderAndFollowers
        };

        // Clean balance is absolute. Avoid stacking level HP/reward inflation
        // on top of the generated fish profiles.
        normalHpPerLevel = 0f;
        normalRewardPerLevel = 0f;
        normalSpeedPerLevel = 0f;
        miniBossBaseHpMultiplier = 1f;
        miniBossHpPerLevel = 0f;
        miniBossRewardPerLevel = 0f;
        miniBossSpeedPerLevel = 0f;
        bossBaseHpMultiplier = 1f;
        bossHpPerLevel = 0f;
        bossRewardPerLevel = 0f;
        bossSpeedPerLevel = 0f;

        // Endless loops remain the one explicit optional scaling layer.
        endlessHpPerLoop = 0.05f;
        endlessRewardPerLoop = 0.05f;
        endlessSpeedPerLoop = 0.01f;
        maxScalingLoops = 5;

        ValidateAndRepairConfiguration();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Apply Recommended 46-Fish Director Preset")]
    public void ApplyRecommended46FishDirectorPreset()
    {
        bigBossStartIndex = 39;
        bigBossEndIndex = 45;
        smallFishStartIndex = 0;
        smallFishEndIndex = 11;
        mediumFishStartIndex = 12;
        mediumFishEndIndex = 30;
        miniBossStartIndex = 36;
        miniBossEndIndex = 38;
        extraNormalFishStartIndex = 46;
        specialFishIndices = new int[] { 31, 32, 33, 34, 35 };

        level1BossTargets = new int[] { 39 };
        level2BossTargets = new int[] { 39, 40 };
        level3BossTargets = new int[] { 40, 41 };
        level4BossTargets = new int[] { 41, 42 };
        level5BossTargets = new int[] { 42, 43 };
        level6BossTargets = new int[] { 43, 44, 45 };

        globalMaximumSimultaneousBosses = 2;
        maximumSimultaneousBossesPerLevel =
            new int[] { 1, 1, 1, 2, 2, 2 };
        doubleBossChancePerLevel =
            new float[] { 0f, 0f, 0.05f, 0.12f, 0.20f, 0.30f };
        tripleBossChancePerLevel =
            new float[] { 0f, 0f, 0f, 0f, 0f, 0f };
        simultaneousBossHpReductionPerExtraBoss = 0.12f;
        simultaneousBossVerticalSpacing = 3.2f;

        level1Warmup = 10f;
        warmupIncreasePerLevel = 2f;
        bossWarningBaseDelay = 1.4f;
        bossWarningIncreasePerLevel = 0.15f;
        betweenTargetBossBaseDelay = 2f;
        betweenTargetBossIncreasePerLevel = 0.3f;
        postBossRecoveryBaseDelay = 0.8f;
        postBossRecoveryIncreasePerLevel = 0.1f;
        bossArrivalExtraBaseDelay = 0f;
        bossArrivalExtraPerLevel = 0f;

        ambientDelayMin = 0.45f;
        ambientDelayMax = 0.85f;
        paradeDelayMin = 9f;
        paradeDelayMax = 14f;
        maxActiveFishBase = 26;
        maxActiveFishPerLevel = 2;
        ambientTrafficLaneCount = 4;
        ambientViewportPadding = 0.10f;
        ambientCenterAvoidanceHalfHeight = 0.14f;
        ambientLaneJitter = 0.025f;
        ambientTargetVerticalDrift = 0.045f;
        ambientVerticalRouteMultiplier = 0.42f;
        minimumActiveFishBase = 18;
        minimumActiveFishPerLevel = 1;
        minimumPopulationRefillBurst = 3;
        minimumPopulationRefillInterval = 0.22f;

        playStandardParadeAtEveryLevelStart = true;
        levelOpeningParadeDelay = 0.20f;
        levelOpeningParadeWaveCount = 1;
        levelOpeningParadeWaveGap = 0.65f;
        levelOpeningParadeFishCountBase = 10;
        levelOpeningParadeFishPerLevel = 1;
        levelOpeningParadeSpeedMultiplier = 0.88f;
        levelOpeningParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.StraightLine,
            OrganizedParadePattern.Arrow,
            OrganizedParadePattern.Diamond,
            OrganizedParadePattern.TwinColumn,
            OrganizedParadePattern.Wave
        };
        enableStandaloneFeatureParade = true;
        standaloneFeatureParadeChance = 0.72f;
        standaloneFeatureParadeDelay = 0.45f;
        standaloneFeatureParadeWaveCount = 1;
        standaloneFeatureParadeWaveGap = 0.75f;
        standaloneFeatureParadeFishCountBase = 12;
        standaloneFeatureParadeFishPerLevel = 1;
        standaloneRoyalEscortChance = 0.45f;
        standaloneFeatureParadePatterns = new OrganizedParadePattern[]
        {
            OrganizedParadePattern.Diamond,
            OrganizedParadePattern.TwinColumn,
            OrganizedParadePattern.Arrow,
            OrganizedParadePattern.Circle,
            OrganizedParadePattern.Wave
        };

        absoluteMaximumActiveFish = 56;
        bossBattleExtraFishCapacity = 14;

        earlyLevelParadeWaves = 2;
        advancedLevelParadeWaves = 2;
        preBossParadeBaseGap = 2.8f;
        preBossParadeGapPerLevel = 0.20f;
        preBossFinalPauseBase = 0.50f;
        preBossFinalPausePerLevel = 0.05f;
        preBossExtraFishCapacity = 8;
        paradeFishPerGroupBase = 4;
        paradeFishPerGroupPerLevel = 0;
        paradeFishPerGroupMaximum = 5;
        paradeLaneSpacing = 2.3f;
        paradeFishHorizontalGap = 1.15f;
        paradeSpeedMultiplierBase = 1.05f;
        paradeSpeedMultiplierPerLevel = 0.015f;
        verticalPreBossParadeChance = 0.30f;
        preBossParadeSpeedBase = 0.68f;
        preBossParadeSpeedPerLevel = 0.01f;
        useDistinctLevelParadeSignatures = true;
        verticalRouteChancePerLevel = new float[]
        {
            0.08f, 0.18f, 0.32f, 0.58f, 0.44f, 0.52f
        };
        paradeRouteSweepAmplitude = 0.34f;
        paradeRouteSweepFrequency = 0.85f;
        specialEscortGuardCount = 5;
        specialEscortInnerRadius = 1.65f;
        useManualParadeSelection = true;
        fallbackToTierSelection = false;

        bodyGuardsOnlyForHighBosses = true;
        minimumBossIndexForRoyalGuards = 43;
        alwaysGuardEpicBoss = true;
        highBossBodyGuardChance = 1f;
        highBossGuardFormation = BossGuardFormationStyle.Random;
        highBossRoyalGuardCount = 12;
        highBossRoyalGuardCountPerLevel = 1;
        highBossRoyalGuardSpacingMultiplier = 1f;
        epicBossIntroductionUsesRoyalConvoy = true;

        noBossBodyGuardChance = 0.18f;
        minimumBodyGuardHalfArcs = 2;
        maximumBodyGuardHalfArcs = 3;
        bodyGuardFishPerHalfArc = 3;
        bodyGuardInnerRadius = 2.5f;
        bodyGuardArcRadiusSpacing = 0.9f;
        bodyGuardHalfArcDegrees = 150f;
        bodyGuardUseMediumFishChance = 0.20f;
        bodyGuardSpeedMultiplier = 0.70f;

        ApplyRecommendedBossSupportSettings();

        bossClearInitialDelay = 0.5f;
        remainingFishBeforeLevelChange = 18;
        battlefieldClearMaximumWait = 2.5f;
        postBossParadeMinimum = 0;
        postBossParadeMaximum = 1;
        postBossParadeGap = 0.6f;

        normalHpPerLevel = 0.02f;
        normalRewardPerLevel = 0.018f;
        normalSpeedPerLevel = 0.005f;
        miniBossBaseHpMultiplier = 1.05f;
        miniBossHpPerLevel = 0.05f;
        miniBossRewardPerLevel = 0.045f;
        miniBossSpeedPerLevel = 0.005f;
        bossBaseHpMultiplier = 1.08f;
        bossHpPerLevel = 0.07f;
        bossRewardPerLevel = 0.06f;
        bossSpeedPerLevel = 0.005f;
        endlessHpPerLoop = 0.05f;
        endlessRewardPerLoop = 0.045f;
        endlessSpeedPerLoop = 0.005f;
        maxScalingLoops = 5;

        ApplyCleanParadePreset();
        ValidateAndRepairConfiguration();

        Debug.Log(
            "Applied the recommended 46-fish director preset. " +
            "Fish prefab IDs and profile assets must still be assigned.",
            this
        );

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void ApplyRecommendedLevelTransitionSettings()
    {
        levelTransitionDelayRange = new Vector2(0.30f, 0.70f);
        fishExitInterval = 0.06f;
        maximumFishExitWait = 3.25f;
        emergencyNaturalExitWait = 3.50f;
        forceCleanupTimeout = 8f;
        emergencyLevelExitSpeedMultiplier = 3.4f;
        transitionPreparationLeadTime = 0.45f;
        maximumBlockingTransitionTime = 7.0f;
        backgroundTransitionFadeDuration = 0.70f;
        fullyOutsideSettleDelay = 0.12f;
        nextLevelSpawnPreparationDelay = 0.25f;
        initialSpawnIntervalForNewLevel = 0.35f;
        newLevelSpawnRampDuration = 3.5f;
        levelTransitionSettingsVersion =
            CurrentLevelTransitionSettingsVersion;
    }

    private void ValidateAndRepairConfiguration()
    {
        if (Fish == null || Fish.Length == 0)
        {
            Debug.LogError("SwapFishScript: Fish array is empty.");
            enabled = false;
            return;
        }

        if (bossSupportSettingsVersion <
            CurrentBossSupportSettingsVersion)
        {
            ApplyRecommendedBossSupportSettings();
        }

        if (levelTransitionSettingsVersion <
            CurrentLevelTransitionSettingsVersion)
        {
            ApplyRecommendedLevelTransitionSettings();
        }

        levelOpeningParadeDelay = Mathf.Max(0f, levelOpeningParadeDelay);
        levelOpeningParadeWaveCount = Mathf.Clamp(levelOpeningParadeWaveCount, 1, 3);
        levelOpeningParadeWaveGap = Mathf.Max(0.1f, levelOpeningParadeWaveGap);
        levelOpeningParadeFishCountBase = Mathf.Clamp(levelOpeningParadeFishCountBase, 5, 24);
        levelOpeningParadeFishPerLevel = Mathf.Clamp(levelOpeningParadeFishPerLevel, 0, 4);
        levelOpeningParadeSpeedMultiplier = Mathf.Clamp(levelOpeningParadeSpeedMultiplier, 0.45f, 1.4f);

        mixedParadeExtraFishCapacity = Mathf.Clamp(mixedParadeExtraFishCapacity, 0, 16);
        maximumParadeFishCount = Mathf.Clamp(maximumParadeFishCount, 6, 28);
        paradeCompletionTimeout = Mathf.Max(4f, paradeCompletionTimeout);
        paradeAmbientClearTimeout = Mathf.Max(0.25f, paradeAmbientClearTimeout);
        paradeAmbientExitSpeedMultiplier = Mathf.Max(1f, paradeAmbientExitSpeedMultiplier);
        paradeTimeoutCleanupGrace = Mathf.Max(0.25f, paradeTimeoutCleanupGrace);
        paradeVisibilityPadding = Mathf.Clamp(paradeVisibilityPadding, 0f, 0.20f);
        smallParadeVisualGap = Mathf.Clamp(smallParadeVisualGap, 1f, 1.30f);
        mediumParadeVisualGap = Mathf.Clamp(mediumParadeVisualGap, 1f, 1.40f);
        largeParadeVisualGap = Mathf.Clamp(largeParadeVisualGap, 1f, 1.60f);
        specialParadeVisualGap = Mathf.Clamp(specialParadeVisualGap, 1f, 1.80f);
        paradeMinimumGapPadding = Mathf.Clamp(paradeMinimumGapPadding, 0f, 0.50f);
        paradeWorldSpeed = Mathf.Max(0.20f, paradeWorldSpeed);
        paradeWorldSpeedPerLevel = Mathf.Max(0f, paradeWorldSpeedPerLevel);
        paradeFollowerCorrection = Mathf.Max(0.50f, paradeFollowerCorrection);
        paradeFollowerCatchUpMultiplier = Mathf.Clamp(
            paradeFollowerCatchUpMultiplier,
            1f,
            3f
        );
        paradeFollowerSlotWobble = Mathf.Clamp(
            paradeFollowerSlotWobble,
            0f,
            0.20f
        );
        minimumAmbientFishDuringBossBattle = Mathf.Clamp(
            minimumAmbientFishDuringBossBattle,
            1,
            24
        );
        bossAmbientRefillBurst = Mathf.Clamp(bossAmbientRefillBurst, 1, 5);
        bossAmbientRefillInterval = Mathf.Max(0.10f, bossAmbientRefillInterval);

        standaloneFeatureParadeChance = Mathf.Clamp01(standaloneFeatureParadeChance);
        standaloneFeatureParadeDelay = Mathf.Max(0f, standaloneFeatureParadeDelay);
        standaloneFeatureParadeWaveCount = Mathf.Clamp(standaloneFeatureParadeWaveCount, 1, 3);
        standaloneFeatureParadeWaveGap = Mathf.Max(0.1f, standaloneFeatureParadeWaveGap);
        standaloneFeatureParadeFishCountBase = Mathf.Clamp(standaloneFeatureParadeFishCountBase, 6, 28);
        standaloneFeatureParadeFishPerLevel = Mathf.Clamp(standaloneFeatureParadeFishPerLevel, 0, 4);
        standaloneRoyalEscortChance = Mathf.Clamp01(standaloneRoyalEscortChance);
        minimumBossIndexForRoyalGuards = Mathf.Max(0, minimumBossIndexForRoyalGuards);
        highBossBodyGuardChance = Mathf.Clamp01(highBossBodyGuardChance);
        highBossRoyalGuardCount = Mathf.Clamp(highBossRoyalGuardCount, 6, 24);
        highBossRoyalGuardCountPerLevel = Mathf.Clamp(highBossRoyalGuardCountPerLevel, 0, 3);
        highBossRoyalGuardSpacingMultiplier = Mathf.Clamp(highBossRoyalGuardSpacingMultiplier, 0.55f, 1.8f);

        bigBossStartIndex = Mathf.Clamp(
            bigBossStartIndex,
            0,
            Fish.Length - 1
        );

        bigBossEndIndex = Mathf.Clamp(
            bigBossEndIndex,
            bigBossStartIndex,
            Fish.Length - 1
        );

        smallFishStartIndex = Mathf.Clamp(
            smallFishStartIndex,
            0,
            Fish.Length - 1
        );

        smallFishEndIndex = Mathf.Clamp(
            smallFishEndIndex,
            smallFishStartIndex,
            Fish.Length - 1
        );

        mediumFishStartIndex = Mathf.Clamp(
            mediumFishStartIndex,
            0,
            Fish.Length - 1
        );

        mediumFishEndIndex = Mathf.Clamp(
            mediumFishEndIndex,
            mediumFishStartIndex,
            Fish.Length - 1
        );

        miniBossStartIndex = Mathf.Clamp(
            miniBossStartIndex,
            0,
            Fish.Length - 1
        );

        miniBossEndIndex = Mathf.Clamp(
            miniBossEndIndex,
            miniBossStartIndex,
            Fish.Length - 1
        );

        extraNormalFishStartIndex = Mathf.Clamp(
            extraNormalFishStartIndex,
            0,
            Fish.Length
        );

        absoluteMaximumActiveFish = Mathf.Max(
            1,
            absoluteMaximumActiveFish
        );

        maxActiveFishBase = Mathf.Clamp(
            maxActiveFishBase,
            1,
            absoluteMaximumActiveFish
        );

        minimumActiveFishBase = Mathf.Clamp(
            minimumActiveFishBase,
            0,
            maxActiveFishBase
        );

        minimumPopulationRefillBurst = Mathf.Clamp(
            minimumPopulationRefillBurst,
            1,
            6
        );

        minimumPopulationRefillInterval = Mathf.Max(
            0.05f,
            minimumPopulationRefillInterval
        );

        bossSupportDoubleChance = Mathf.Clamp(
            bossSupportDoubleChance,
            0f,
            0.25f
        );

        maximumBossSupportWavesPerBatch = Mathf.Clamp(
            maximumBossSupportWavesPerBatch,
            0,
            4
        );

        maximumBossSupportFishPerWave = Mathf.Clamp(
            maximumBossSupportFishPerWave,
            1,
            12
        );

        minimumBossSupportFishPerWave = Mathf.Clamp(
            minimumBossSupportFishPerWave,
            1,
            maximumBossSupportFishPerWave
        );

        bossSupportBaseFish = Mathf.Clamp(
            bossSupportBaseFish,
            1,
            maximumBossSupportFishPerWave
        );

        bossSupportFishPerLevel = Mathf.Clamp(
            bossSupportFishPerLevel,
            0,
            maximumBossSupportFishPerWave
        );

        bossSupportFishPerExtraBoss = Mathf.Clamp(
            bossSupportFishPerExtraBoss,
            0,
            maximumBossSupportFishPerWave
        );

        maximumDoubleSupportWavesPerBatch = Mathf.Clamp(
            maximumDoubleSupportWavesPerBatch,
            0,
            1
        );

        bossSupportFirstDelayMin = Mathf.Max(
            1f,
            bossSupportFirstDelayMin
        );

        bossSupportFirstDelayMax = Mathf.Max(
            bossSupportFirstDelayMin,
            bossSupportFirstDelayMax
        );

        bossSupportRepeatDelayMin = Mathf.Max(
            1f,
            bossSupportRepeatDelayMin
        );

        bossSupportRepeatDelayMax = Mathf.Max(
            bossSupportRepeatDelayMin,
            bossSupportRepeatDelayMax
        );

        bossSupportFullScreenRetryDelay = Mathf.Max(
            1f,
            bossSupportFullScreenRetryDelay
        );

        maximumFishExitWait = Mathf.Max(
            0.5f,
            maximumFishExitWait
        );
        emergencyNaturalExitWait = Mathf.Max(
            3f,
            emergencyNaturalExitWait
        );
        forceCleanupTimeout = Mathf.Max(
            8f,
            forceCleanupTimeout
        );
        emergencyLevelExitSpeedMultiplier = Mathf.Max(
            1.2f,
            emergencyLevelExitSpeedMultiplier
        );
        transitionPreparationLeadTime = Mathf.Max(
            0f,
            transitionPreparationLeadTime
        );
        maximumBlockingTransitionTime = Mathf.Max(
            maximumFishExitWait,
            maximumBlockingTransitionTime
        );
        backgroundTransitionFadeDuration = Mathf.Max(
            0.10f,
            backgroundTransitionFadeDuration
        );
        fullyOutsideSettleDelay = Mathf.Max(
            0f,
            fullyOutsideSettleDelay
        );
        nextLevelSpawnPreparationDelay = Mathf.Max(
            0f,
            nextLevelSpawnPreparationDelay
        );
        initialSpawnIntervalForNewLevel = Mathf.Max(
            0.05f,
            initialSpawnIntervalForNewLevel
        );

        ambientTrafficLaneCount = Mathf.Clamp(
            ambientTrafficLaneCount,
            2,
            6
        );
        ambientViewportPadding = Mathf.Clamp(
            ambientViewportPadding,
            FishScreenBounds.MinimumViewportPadding,
            0.22f
        );
        ambientCenterAvoidanceHalfHeight = Mathf.Clamp(
            ambientCenterAvoidanceHalfHeight,
            0.05f,
            Mathf.Max(0.05f, 0.48f - ambientViewportPadding)
        );
        ambientLaneJitter = Mathf.Clamp(
            ambientLaneJitter,
            0f,
            0.08f
        );
        ambientTargetVerticalDrift = Mathf.Clamp(
            ambientTargetVerticalDrift,
            0f,
            0.12f
        );
        ambientVerticalRouteMultiplier = Mathf.Clamp01(
            ambientVerticalRouteMultiplier
        );

        if (verticalRouteChancePerLevel == null ||
            verticalRouteChancePerLevel.Length != 6)
        {
            verticalRouteChancePerLevel = new float[]
            {
                0.08f, 0.18f, 0.32f, 0.58f, 0.44f, 0.52f
            };
        }

        for (int i = 0; i < verticalRouteChancePerLevel.Length; i++)
        {
            verticalRouteChancePerLevel[i] = Mathf.Clamp01(
                verticalRouteChancePerLevel[i]
            );
        }

        paradeRouteSweepAmplitude = Mathf.Clamp(
            paradeRouteSweepAmplitude,
            0f,
            1.2f
        );
        paradeRouteSweepFrequency = Mathf.Clamp(
            paradeRouteSweepFrequency,
            0.25f,
            2.5f
        );
        specialEscortGuardCount = Mathf.Clamp(
            specialEscortGuardCount,
            2,
            8
        );
        specialEscortInnerRadius = Mathf.Clamp(
            specialEscortInnerRadius,
            0.8f,
            4f
        );

        bossArrivalCrowdBaseWaves = Mathf.Clamp(
            bossArrivalCrowdBaseWaves,
            1,
            4
        );

        bossArrivalCrowdMaximumWaves = Mathf.Clamp(
            bossArrivalCrowdMaximumWaves,
            bossArrivalCrowdBaseWaves,
            5
        );

        bossArrivalCrowdBaseFishPerWave = Mathf.Max(
            4,
            bossArrivalCrowdBaseFishPerWave
        );

        bossArrivalCrowdWaveGap = Mathf.Max(
            0.1f,
            bossArrivalCrowdWaveGap
        );

        postBossParadeMinimum = Mathf.Clamp(
            postBossParadeMinimum,
            0,
            2
        );

        postBossParadeMaximum = Mathf.Clamp(
            postBossParadeMaximum,
            postBossParadeMinimum,
            2
        );

        level1BossTargets = RepairBossTargetArray(
            level1BossTargets,
            new int[] { 39 }
        );

        level2BossTargets = RepairBossTargetArray(
            level2BossTargets,
            new int[] { 39, 40 }
        );

        level3BossTargets = RepairBossTargetArray(
            level3BossTargets,
            new int[] { 40, 41 }
        );

        level4BossTargets = RepairBossTargetArray(
            level4BossTargets,
            new int[] { 41, 42 }
        );

        level5BossTargets = RepairBossTargetArray(
            level5BossTargets,
            new int[] { 42, 43 }
        );

        level6BossTargets = RepairBossTargetArray(
            level6BossTargets,
            new int[] { 43, 44, 45 }
        );
    }

    private int[] RepairBossTargetArray(
        int[] source,
        int[] fallback
    )
    {
        if (source == null || source.Length == 0)
        {
            source = fallback;
        }

        if (source == null || source.Length == 0)
        {
            source = new int[] { bigBossStartIndex };
        }

        // Preserve every configured target. Earlier code incorrectly
        // truncated levels to one or two bosses.
        int[] repaired = new int[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            repaired[i] = Mathf.Clamp(
                source[i],
                bigBossStartIndex,
                bigBossEndIndex
            );
        }

        return repaired;
    }

    private int GetPreBossMiniBossCount(
    GameLevel level
)
    {
        int count = 1;
        int levelIndex = (int)level;

        float adjustedSecondChance =
            secondMiniBossChance +
            levelIndex * 0.04f;

        float adjustedThirdChance =
            thirdMiniBossChance +
            levelIndex * 0.025f;

        if (maximumPreBossMiniBossCount >= 2 &&
            Random.value <= Mathf.Clamp01(
                adjustedSecondChance
            ))
        {
            count++;
        }

        if (maximumPreBossMiniBossCount >= 3 &&
            count >= 2 &&
            Random.value <= Mathf.Clamp01(
                adjustedThirdChance
            ))
        {
            count++;
        }

        return Mathf.Clamp(
            count,
            1,
            maximumPreBossMiniBossCount
        );
    }

    private int GetRandomMiniBossIndex()
    {
        if (Fish == null ||
            Fish.Length == 0)
        {
            return -1;
        }

        int safeStart = Mathf.Clamp(
            miniBossStartIndex,
            0,
            Fish.Length - 1
        );

        int safeEnd = Mathf.Clamp(
            miniBossEndIndex,
            safeStart,
            Fish.Length - 1
        );

        // The maximum integer value in Random.Range is exclusive.
        return Random.Range(
            safeStart,
            safeEnd + 1
        );
    }



}
