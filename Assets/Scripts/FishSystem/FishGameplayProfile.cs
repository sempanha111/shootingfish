using System;
using UnityEngine;

public enum FishRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Boss
}

public enum FishSizeClass
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge,
    Boss
}

public enum FishSwimmingPattern
{
    SlowAndSteady,
    FastStraightSwimmer,
    SmoothCurvedSwimmer,
    ZigzagSwimmer,
    SShapedSwimmer,
    StopAndGoSwimmer,
    ShortBurstSwimmer,
    WideRoamingSwimmer,
    CircularSwimmer,
    SchoolingSwimmer,
    HunterChasingSwimmer,
    TimidRetreatingSwimmer,
    BottomAreaSwimmer,
    SurfaceAreaSwimmer,
    UnpredictableRareFish,
    HeavyBossMovement
}

[Flags]
public enum FishBehaviorFlags
{
    None = 0,
    Calm = 1 << 0,
    Curious = 1 << 1,
    Timid = 1 << 2,
    Aggressive = 1 << 3,
    Social = 1 << 4,
    Solitary = 1 << 5,
    Territorial = 1 << 6,
    Evasive = 1 << 7,
    PlayerApproaching = 1 << 8,
    PlayerAvoiding = 1 << 9,
    GroupFollowing = 1 << 10,
    LeaderFollowing = 1 << 11,
    RandomRoaming = 1 << 12,
    CenterSeeking = 1 << 13,
    EdgePreferring = 1 << 14,
    EscapeAtLowHealth = 1 << 15
}

public enum FishFormationType
{
    Line,
    V,
    Diamond,
    Arc,
    Ring,
    Column,
    LooseSchool,
    LeaderAndFollowers
}

public enum FishPreferredDirection
{
    Any,
    Left,
    Right,
    Top,
    Bottom,
    Horizontal,
    Vertical,
    OppositeEntry
}

public enum FishRoutePattern
{
    StraightCrossing,
    DiagonalCrossing,
    CenterCrossing,
    WideCurvedRoute,
    EdgeRoute,
    SShapedRoute,
    LoopingRoute,
    GroupRoute,
    PlayerApproachRoute,
    EnterAndRetreatRoute
}

[Serializable]
public struct WeightedFishRoute
{
    public FishRoutePattern route;
    [Min(0f)] public float weight;
}

[CreateAssetMenu(
    menuName = "Fish Arcade/Fish Gameplay Profile",
    fileName = "FishGameplayProfile"
)]
public sealed class FishGameplayProfile : ScriptableObject
{
    [Header("Basic Information")]
    public int fishId;
    public string displayName = "Fish";
    public string fishCategory = "Ambient";
    public FishTier fishTier = FishTier.Small;
    public FishRarity rarity = FishRarity.Common;
    public FishSizeClass sizeClass = FishSizeClass.Small;
    [Min(0f)] public float rewardValue = 10f;
    [Min(1f)] public float health = 10f;
    [Range(0f, 0.95f)] public float damageResistance;
    [Min(0f)] public float spawnWeight = 1f;
    [Range(1, 100)] public int minimumLevel = 1;
    [Range(1, 64)] public int maximumSimultaneousCount = 8;

    [SerializeField, HideInInspector]
    private int combatBalanceVersion;

    [SerializeField, HideInInspector]
    private int movementBalanceVersion;

    public int MovementBalanceVersion
    {
        get { return movementBalanceVersion; }
    }

    public int CombatBalanceVersion
    {
        get { return combatBalanceVersion; }
    }

    /// <summary>
    /// Editor/profile migration hook. The authored health and reward values
    /// are upgraded once in the profile asset, so pooled runtime fish do not
    /// need a separate per-cannon balance layer.
    /// </summary>
    public bool ApplyCombatBalanceUpgrade(
        int targetVersion,
        float healthMultiplier,
        float rewardMultiplier
    )
    {
        if (combatBalanceVersion >= targetVersion)
        {
            return false;
        }

        health = Mathf.Max(1f, health * Mathf.Max(1f, healthMultiplier));
        rewardValue = Mathf.Max(0f, rewardValue * Mathf.Max(1f, rewardMultiplier));
        combatBalanceVersion = targetVersion;
        return true;
    }

    public bool ApplyMovementBalanceUpgrade(
        int targetVersion,
        float minimumSpeedMultiplier,
        float maximumSpeedMultiplier,
        float accelerationMultiplier,
        float turnSpeedMultiplier,
        float animatorSpeedMultiplierValue
    )
    {
        if (movementBalanceVersion >= targetVersion)
        {
            return false;
        }

        minimumSpeed = Mathf.Max(
            0.05f,
            minimumSpeed * Mathf.Max(0.05f, minimumSpeedMultiplier)
        );
        maximumSpeed = Mathf.Max(
            minimumSpeed,
            maximumSpeed * Mathf.Max(0.05f, maximumSpeedMultiplier)
        );
        acceleration = Mathf.Max(
            0.05f,
            acceleration * Mathf.Max(0.05f, accelerationMultiplier)
        );
        turnSpeed = Mathf.Max(
            1f,
            turnSpeed * Mathf.Max(0.05f, turnSpeedMultiplier)
        );
        animatorSpeedMultiplier = Mathf.Max(
            0.05f,
            animatorSpeedMultiplier *
            Mathf.Max(0.05f, animatorSpeedMultiplierValue)
        );

        if (useBossMovement)
        {
            bossMinimumSpeed = Mathf.Max(
                0.05f,
                bossMinimumSpeed *
                Mathf.Max(0.05f, minimumSpeedMultiplier)
            );
            bossMaximumSpeed = Mathf.Max(
                bossMinimumSpeed,
                bossMaximumSpeed *
                Mathf.Max(0.05f, maximumSpeedMultiplier)
            );
            bossAcceleration = Mathf.Max(
                0.05f,
                bossAcceleration *
                Mathf.Max(0.05f, accelerationMultiplier)
            );
            bossTurnSpeed = Mathf.Max(
                1f,
                bossTurnSpeed * Mathf.Max(0.05f, turnSpeedMultiplier)
            );
        }

        movementBalanceVersion = targetVersion;
        return true;
    }

    public void MarkMovementBalanceVersion(int version)
    {
        movementBalanceVersion = Mathf.Max(
            movementBalanceVersion,
            version
        );
    }

    /// <summary>
    /// Clean-generation hook. Unlike the old multiplicative migrations, this
    /// writes absolute combat values and replaces the old balance version.
    /// Re-running the generator therefore cannot stack HP or reward multipliers.
    /// </summary>
    public void SetCombatBalanceAbsolute(
        int version,
        float healthValue,
        float resistanceValue,
        float rewardValueAmount
    )
    {
        health = Mathf.Max(1f, healthValue);
        damageResistance = Mathf.Clamp(resistanceValue, 0f, 0.95f);
        rewardValue = Mathf.Max(0f, rewardValueAmount);
        combatBalanceVersion = Mathf.Max(0, version);
    }

    [Header("Movement Characteristics")]
    [Min(0.05f)] public float minimumSpeed = 1.6f;
    [Min(0.05f)] public float maximumSpeed = 2.4f;
    [Min(0.05f)] public float acceleration = 3.5f;
    [Min(0.05f)] public float deceleration = 4.2f;
    [Min(1f)] public float turnSpeed = 100f;
    [Min(0.05f)] public float turnFrequency = 1.4f;
    public Vector2 routeLengthRange = new Vector2(0.65f, 1.65f);
    [Range(0f, 1f)] public float preferredSwimmingDepth = 0.5f;
    [Range(0f, 1f)] public float horizontalMovementPreference = 0.75f;
    [Range(0f, 1f)] public float verticalMovementPreference = 0.25f;
    [Range(0f, 1f)] public float centerCrossingChance = 0.48f;
    [Range(0f, 1f)] public float edgeRouteChance = 0.14f;
    [Range(0f, 1f)] public float curveStrength = 0.35f;
    [Range(0f, 1f)] public float wobbleAmount = 0.10f;
    [Min(0.02f)] public float wobbleFrequency = 0.75f;
    [Range(0f, 1f)] public float pauseChance = 0.06f;
    public Vector2 pauseDurationRange = new Vector2(0.15f, 0.65f);
    [Range(0f, 1f)] public float burstSpeedChance = 0.10f;
    [Min(1f)] public float burstSpeedMultiplier = 1.35f;
    public Vector2 offScreenWaitingTimeRange = new Vector2(1.5f, 4.5f);
    [Range(0f, 1f)] public float returnChance = 0.18f;
    public FishPreferredDirection preferredEntryDirection = FishPreferredDirection.Any;
    public FishPreferredDirection preferredExitDirection = FishPreferredDirection.OppositeEntry;

    [Header("Swimming Pattern")]
    public FishSwimmingPattern swimmingPattern = FishSwimmingPattern.SmoothCurvedSwimmer;
    public WeightedFishRoute[] routeWeights =
    {
        new WeightedFishRoute { route = FishRoutePattern.StraightCrossing, weight = 1f },
        new WeightedFishRoute { route = FishRoutePattern.DiagonalCrossing, weight = 0.75f },
        new WeightedFishRoute { route = FishRoutePattern.CenterCrossing, weight = 1.1f },
        new WeightedFishRoute { route = FishRoutePattern.WideCurvedRoute, weight = 0.8f },
        new WeightedFishRoute { route = FishRoutePattern.EdgeRoute, weight = 0.2f }
    };

    [Header("Behavior Characteristics")]
    public FishBehaviorFlags behavior =
        FishBehaviorFlags.Calm |
        FishBehaviorFlags.RandomRoaming;
    [Range(0f, 1f)] public float lowHealthEscapeThreshold = 0.20f;
    [Range(0f, 1f)] public float evasionStrength = 0.25f;
    [Range(0f, 1f)] public float playerApproachStrength = 0.20f;

    [Header("Group Behavior")]
    public bool canJoinSchool = true;
    [Range(1, 20)] public int minimumGroupSize = 2;
    [Range(1, 30)] public int maximumGroupSize = 5;
    [Range(0f, 1f)] public float leaderChance = 0.20f;
    public FishFormationType formationType = FishFormationType.LooseSchool;
    [Min(0.05f)] public float followDistance = 0.75f;
    [Min(0.05f)] public float separationDistance = 0.50f;
    [Range(0f, 2f)] public float alignmentStrength = 0.55f;
    [Range(0f, 2f)] public float cohesionStrength = 0.45f;
    [Range(0f, 0.5f)] public float groupSpeedVariation = 0.06f;
    public Vector2 groupEntryDelayRange = new Vector2(0.05f, 0.22f);
    public bool groupExitsTogether = true;

    [Header("Spacing and Collision Avoidance")]
    [Min(0.05f)] public float minimumSpawnDistance = 0.75f;
    [Min(0.05f)] public float minimumSwimmingDistance = 0.55f;
    [Min(0.05f)] public float overlapCheckInterval = 0.30f;
    [Range(0f, 2f)] public float routeAdjustmentStrength = 0.42f;
    [Range(1, 12)] public int maximumRouteCorrectionAttempts = 5;
    [Min(0.1f)] public float fishSizeSpacingMultiplier = 1f;
    [Range(0.1f, 1f)] public float schoolingSpacingMultiplier = 0.58f;

    [Header("Visual and Animation Settings")]
    [Min(0.05f)] public float animatorSpeedMultiplier = 1f;
    [Min(0.05f)] public float tailMovementSpeed = 1f;
    [Range(0f, 0.5f)] public float bodySwayAmount = 0.08f;
    public bool flipDirection = true;
    public Vector2 shadowOffset = new Vector2(0.15f, -0.22f);
    [Min(0.05f)] public float shadowScale = 1f;
    [Range(0f, 1f)] public float shadowOpacity = 0.45f;
    public int shadowSortingOrder = -1;
    public string turnAnimation = "Turn";
    public string hitAnimation = "HitFlinch";
    public string deathAnimation = "Death";
    public GameObject entryEffect;
    public GameObject exitEffect;
    public GameObject bossEffect;
    public GameObject rareFishEffect;

    [Header("Boss Movement")]
    public bool useBossMovement;
    [Min(0.05f)] public float bossMinimumSpeed = 0.75f;
    [Min(0.05f)] public float bossMaximumSpeed = 1.45f;
    [Min(0.05f)] public float bossAcceleration = 1.45f;
    [Min(0.05f)] public float bossDeceleration = 1.8f;
    [Min(1f)] public float bossTurnSpeed = 55f;
    [Range(0f, 1f)] public float bossEnterNearPlayerChance = 0.28f;
    [Range(0f, 1f)] public float bossCrossCenterChance = 0.62f;
    [Range(0f, 1f)] public float bossWideRouteChance = 0.58f;
    public Vector2 bossRouteDistanceRange = new Vector2(1.15f, 2.60f);
    public Vector2 bossPauseDurationRange = new Vector2(0.15f, 1.10f);
    [Range(0.10f, 0.5f)] public float screenEdgePadding = 0.10f;
    public Vector2 bossReturnDelayRange = new Vector2(1.4f, 4.2f);
    [Range(0f, 1f)] public float bossEnterAndRetreatChance = 0.18f;
    [Range(0f, 1f)] public float bossLoopRouteChance = 0.18f;

    [Header("Long Body Boss Steering")]
    [Tooltip("Limits the actual movement heading as well as the sprite rotation. Useful for very long bosses that should carve a wide smooth turn instead of snapping toward the next waypoint like a small fish.")]
    public bool bossUseLongBodySteering;

    [Tooltip("Maximum heading change while the boss is swimming. Crystal Whale uses a deliberately small value so the full body sweeps through turns smoothly.")]
    [Range(2f, 90f)]
    public float bossSteeringDegreesPerSecond = 28f;

    [Tooltip("When Enter And Retreat is selected, leave through a far side first and perform the return outside the visible screen. This prevents a long boss from making an obvious 180-degree U-turn in front of the players.")]
    public bool bossRetreatOutsideOnly;

    [Tooltip("How long the boss keeps a selected cruise speed before smoothly accelerating/decelerating toward another random speed.")]
    public Vector2 bossSpeedChangeIntervalRange = new Vector2(1.4f, 3.6f);

    [Tooltip("Chance to pause when a boss reaches a route waypoint. Long-bodied fast-pass bosses normally use a very low value.")]
    [Range(0f, 1f)]
    public float bossPauseChance = 0.22f;

    [Tooltip("Optional hard cap for normal in-arena boss movement after runtime/level speed scaling. Zero disables the cap. Escape/level-transition movement may still exceed it so the boss can leave cleanly.")]
    [Min(0f)]
    public float bossNormalWorldSpeedCap;

    [Tooltip("Ignores survivor hit-flinch/micro-convulsion movement reactions from ordinary bullets. The fish can still flash on hit and take damage, but its swim speed/path stays normal. Useful for huge long-bodied bosses such as Crystal Whale.")]
    public bool ignoreReactiveHitMotion;

    public float GetRandomSpeed(bool boss)
    {
        float minimum = boss ? bossMinimumSpeed : minimumSpeed;
        float maximum = boss ? bossMaximumSpeed : maximumSpeed;
        return UnityEngine.Random.Range(
            Mathf.Max(0.05f, Mathf.Min(minimum, maximum)),
            Mathf.Max(0.05f, Mathf.Max(minimum, maximum))
        );
    }

    public float GetAcceleration(bool boss)
    {
        return Mathf.Max(0.05f, boss ? bossAcceleration : acceleration);
    }

    public float GetDeceleration(bool boss)
    {
        return Mathf.Max(0.05f, boss ? bossDeceleration : deceleration);
    }

    public float GetTurnSpeed(bool boss)
    {
        return Mathf.Max(1f, boss ? bossTurnSpeed : turnSpeed);
    }

    public FishRoutePattern SelectRoute(bool forceCenter)
    {
        if (forceCenter)
        {
            return FishRoutePattern.CenterCrossing;
        }

        if (routeWeights == null || routeWeights.Length == 0)
        {
            return FishRoutePattern.StraightCrossing;
        }

        float total = 0f;
        for (int i = 0; i < routeWeights.Length; i++)
        {
            total += Mathf.Max(0f, routeWeights[i].weight);
        }

        if (total <= 0f)
        {
            return FishRoutePattern.StraightCrossing;
        }

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < routeWeights.Length; i++)
        {
            roll -= Mathf.Max(0f, routeWeights[i].weight);
            if (roll <= 0f)
            {
                return routeWeights[i].route;
            }
        }

        return routeWeights[routeWeights.Length - 1].route;
    }

    private void OnValidate()
    {
        health = Mathf.Max(1f, health);
        rewardValue = Mathf.Max(0f, rewardValue);
        spawnWeight = Mathf.Max(0f, spawnWeight);
        minimumLevel = Mathf.Max(1, minimumLevel);
        maximumSimultaneousCount = Mathf.Clamp(maximumSimultaneousCount, 1, 64);

        maximumSpeed = Mathf.Max(minimumSpeed, maximumSpeed);
        acceleration = Mathf.Max(0.05f, acceleration);
        deceleration = Mathf.Max(0.05f, deceleration);
        turnSpeed = Mathf.Max(1f, turnSpeed);
        routeLengthRange = SortPositiveRange(routeLengthRange, 0.1f);
        pauseDurationRange = SortPositiveRange(pauseDurationRange, 0f);
        offScreenWaitingTimeRange = SortPositiveRange(offScreenWaitingTimeRange, 0f);
        groupEntryDelayRange = SortPositiveRange(groupEntryDelayRange, 0f);

        maximumGroupSize = Mathf.Max(minimumGroupSize, maximumGroupSize);
        minimumSpawnDistance = Mathf.Max(0.05f, minimumSpawnDistance);
        minimumSwimmingDistance = Mathf.Max(0.05f, minimumSwimmingDistance);
        overlapCheckInterval = Mathf.Max(0.05f, overlapCheckInterval);
        screenEdgePadding = Mathf.Clamp(
            screenEdgePadding,
            FishScreenBounds.MinimumViewportPadding,
            0.5f
        );

        bossMaximumSpeed = Mathf.Max(bossMinimumSpeed, bossMaximumSpeed);
        bossAcceleration = Mathf.Max(0.05f, bossAcceleration);
        bossDeceleration = Mathf.Max(0.05f, bossDeceleration);
        bossTurnSpeed = Mathf.Max(1f, bossTurnSpeed);
        bossRouteDistanceRange = SortPositiveRange(bossRouteDistanceRange, 0.25f);
        bossPauseDurationRange = SortPositiveRange(bossPauseDurationRange, 0f);
        bossReturnDelayRange = SortPositiveRange(bossReturnDelayRange, 0f);
        bossSteeringDegreesPerSecond = Mathf.Clamp(
            bossSteeringDegreesPerSecond,
            2f,
            90f
        );
        bossSpeedChangeIntervalRange = SortPositiveRange(
            bossSpeedChangeIntervalRange,
            0.20f
        );
        bossNormalWorldSpeedCap = Mathf.Max(0f, bossNormalWorldSpeedCap);
        screenEdgePadding = Mathf.Max(
            FishScreenBounds.MinimumViewportPadding,
            screenEdgePadding
        );
    }

    private static Vector2 SortPositiveRange(Vector2 value, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
        float high = Mathf.Max(low, Mathf.Max(value.x, value.y));
        return new Vector2(low, high);
    }
}
