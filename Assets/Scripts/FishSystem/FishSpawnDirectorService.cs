using UnityEngine;

public struct FishSpawnPlan
{
    public FishScreenSide entrySide;
    public FishScreenSide exitSide;
    public FishRoutePattern routePattern;
    public Vector3 spawnPosition;
    public Vector3 targetPosition;
    public bool crossesCenter;
    public bool verticalRoute;
}

[DisallowMultipleComponent]
public sealed class FishSpawnDirectorService : MonoBehaviour
{
    private const int CurrentDirectorSettingsVersion = 13;
    [Header("Population Distribution")]
    [SerializeField, Range(0.20f, 0.85f)]
    private float minimumCenterCrossingPercentage = 0.46f;
    [SerializeField, Range(0f, 3f)] private float sideBalanceStrength = 1.35f;
    [SerializeField, Range(0f, 2f)] private float emptySideBonus = 0.85f;
    [SerializeField, Range(0f, 1f)] private float verticalEntryChance = 0.22f;
    [SerializeField, Range(0.05f, 0.45f)] private float centerAreaHalfWidth = 0.22f;
    [SerializeField, Range(0.05f, 0.45f)] private float centerAreaHalfHeight = 0.22f;
    [SerializeField, Range(2, 12)] private int recentRouteHistorySize = 6;
    [SerializeField, Range(4, 12)] private int recentFishHistorySize = 10;
    [SerializeField, Range(0f, 1f)] private float repeatedFishWeightMultiplier = 0.12f;

    [Header("Species and Category Variety")]
    [SerializeField, Range(0f, 1f)]
    private float immediateRepeatWeightMultiplier = 0.02f;

    [SerializeField, Range(1, 4)]
    private int maximumConsecutiveSameSpecies = 2;

    [SerializeField, Range(0f, 3f)]
    private float categoryDeficitStrength = 1.65f;

    [SerializeField, Range(0.05f, 1f)]
    private float overrepresentedCategoryMultiplier = 0.28f;

    [SerializeField, Range(0f, 0.35f)]
    private float minimumLargeFishShare = 0.10f;

    [SerializeField, Range(0f, 0.25f)]
    private float minimumSpecialFishShare = 0.04f;

    [SerializeField, Range(0f, 0.30f)]
    private float minimumRareFishShare = 0.08f;

    [Header("Spawn Spacing")]
    [SerializeField, Min(0.05f)] private float fallbackMinimumSpawnDistance = 0.85f;
    [SerializeField, Range(1, 12)] private int maximumSpawnCorrectionAttempts = 6;
    [SerializeField, Range(0.05f, 0.5f)] private float spawnCorrectionViewportStep = 0.07f;

    [Tooltip("Global boost applied after each fish profile's spawn distance.")]
    [SerializeField, Range(1f, 1.75f)]
    private float globalSpawnSpacingMultiplier = 1.35f;

    [Tooltip(
        "School members may be close, but this floor prevents two or three " +
        "sprites from occupying the same position."
    )]
    [SerializeField, Range(0.65f, 1f)]
    private float minimumSchoolingSpawnSpacingMultiplier = 0.88f;

    [SerializeField, Range(0.1f, 1f)]
    private float schoolingSpawnSpacingMultiplier = 0.88f;

    [Tooltip("How much of an existing fish's measured radius blocks a new spawn.")]
    [SerializeField, Range(0.35f, 1f)]
    private float existingFishRadiusContribution = 0.75f;

    [Header("Performance Adaptation")]
    [SerializeField] private bool adaptSpawnRateToPerformance = true;
    [SerializeField, Range(20, 60)] private int lowFrameRateThreshold = 36;
    [SerializeField, Range(0.25f, 1f)] private float lowPerformanceSpawnRateMultiplier = 0.62f;
    [SerializeField, Min(0.25f)] private float performanceSampleWindow = 1.5f;

    [Header("Debug Snapshot")]
    [SerializeField] private int activeFish;
    [SerializeField] private int leftSideFish;
    [SerializeField] private int rightSideFish;
    [SerializeField] private int topSideFish;
    [SerializeField] private int bottomSideFish;
    [SerializeField] private int centerFish;
    [SerializeField] private int enteringFish;
    [SerializeField] private int exitingFish;
    [SerializeField] private int activeSmallFish;
    [SerializeField] private int activeMediumFish;
    [SerializeField] private int activeSpecialFish;
    [SerializeField] private int activeLargeFish;
    [SerializeField] private int activeRareFish;
    [SerializeField] private float measuredFrameRate = 60f;

    [SerializeField, HideInInspector]
    private int directorSettingsVersion;

    private readonly FishRoutePattern[] recentRoutes = new FishRoutePattern[12];
    private readonly bool[] recentRouteCrossesCenter = new bool[12];
    private readonly int[] recentFishIndices = new int[12];
    private int recentRouteCount;
    private int recentRouteCursor;
    private int recentFishCount;
    private int recentFishCursor;
    private int lastRememberedFishIndex = -1;
    private int consecutiveSameSpecies;
    private float performanceElapsed;
    private int performanceFrames;
    private float nextPopulationSampleTime;
    private Camera targetCamera;
    private float runtimeComplexRouteChance;
    private float runtimeEvasiveRouteChance;

    public float SpawnRateMultiplier
    {
        get
        {
            if (!adaptSpawnRateToPerformance || measuredFrameRate >= lowFrameRateThreshold)
            {
                return 1f;
            }

            return Mathf.Clamp(lowPerformanceSpawnRateMultiplier, 0.25f, 1f);
        }
    }

    public int ActiveFishCount { get { return activeFish; } }
    public int CenterFishCount { get { return centerFish; } }
    public int EnteringFishCount { get { return enteringFish; } }
    public int ExitingFishCount { get { return exitingFish; } }
    public int LeftSideFishCount { get { return leftSideFish; } }
    public int RightSideFishCount { get { return rightSideFish; } }
    public int TopSideFishCount { get { return topSideFish; } }
    public int BottomSideFishCount { get { return bottomSideFish; } }
    public float MeasuredFrameRate { get { return measuredFrameRate; } }

    private void Awake()
    {
        ApplyV13DirectorSettingsIfNeeded();
        targetCamera = Camera.main;
        for (int i = 0; i < recentFishIndices.Length; i++)
        {
            recentFishIndices[i] = -1;
        }
    }

    private void OnValidate()
    {
        ApplyV13DirectorSettingsIfNeeded();
    }

    private void ApplyV13DirectorSettingsIfNeeded()
    {
        if (directorSettingsVersion >= CurrentDirectorSettingsVersion)
        {
            return;
        }

        recentFishHistorySize = 10;
        repeatedFishWeightMultiplier = 0.12f;
        immediateRepeatWeightMultiplier = 0.02f;
        maximumConsecutiveSameSpecies = 2;
        categoryDeficitStrength = 1.65f;
        overrepresentedCategoryMultiplier = 0.28f;
        minimumLargeFishShare = 0.10f;
        minimumSpecialFishShare = 0.04f;
        minimumRareFishShare = 0.08f;
        globalSpawnSpacingMultiplier = 1.35f;
        minimumSchoolingSpawnSpacingMultiplier = 0.88f;
        schoolingSpawnSpacingMultiplier = 0.88f;
        existingFishRadiusContribution = 0.75f;
        directorSettingsVersion = CurrentDirectorSettingsVersion;
    }

    private void Update()
    {
        performanceElapsed += Time.unscaledDeltaTime;
        performanceFrames++;

        if (performanceElapsed >= Mathf.Max(0.25f, performanceSampleWindow))
        {
            measuredFrameRate = performanceElapsed > 0f
                ? performanceFrames / performanceElapsed
                : 60f;
            performanceElapsed = 0f;
            performanceFrames = 0;
        }

        if (Time.time >= nextPopulationSampleTime)
        {
            RefreshPopulationSnapshot();
            nextPopulationSampleTime = Time.time + 0.35f;
        }
    }


    public void SetLevelTuning(float complexRouteChance, float evasiveRouteChance)
    {
        runtimeComplexRouteChance = Mathf.Clamp01(complexRouteChance);
        runtimeEvasiveRouteChance = Mathf.Clamp01(evasiveRouteChance);
    }

    public bool TryBuildAmbientPlan(
        FishGameplayProfile profile,
        Transform left,
        Transform right,
        Transform top,
        Transform bottom,
        bool allowVertical,
        out FishSpawnPlan plan
    )
    {
        plan = new FishSpawnPlan();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || left == null || right == null)
        {
            return false;
        }

        RefreshPopulationSnapshot();

        FishScreenSide entry = SelectBalancedEntrySide(
            allowVertical && top != null && bottom != null,
            profile
        );
        bool vertical = entry == FishScreenSide.Top || entry == FishScreenSide.Bottom;
        FishRoutePattern route = SelectBalancedRoute(profile);
        FishScreenSide exit = SelectExitSide(entry, route, profile);
        bool forceCenter = ShouldForceCenterRoute(profile);

        if (forceCenter)
        {
            route = FishRoutePattern.CenterCrossing;
            exit = FishScreenBounds.Opposite(entry);
        }

        float entryLane = SelectEntryLane(entry, forceCenter, profile);
        float exitLane = SelectExitLane(entryLane, route, forceCenter);
        float padding = profile != null
            ? Mathf.Max(FishScreenBounds.MinimumViewportPadding, profile.screenEdgePadding)
            : 0.08f;
        float extra = GetProfileSpawnRadius(profile);

        Vector3 spawn = FishScreenBounds.GetEdgePoint(
            targetCamera,
            entry,
            entryLane,
            padding,
            extra,
            left.position.z
        );
        Vector3 target = FishScreenBounds.GetEdgePoint(
            targetCamera,
            exit,
            exitLane,
            padding,
            extra,
            left.position.z
        );

        plan.entrySide = entry;
        plan.exitSide = exit;
        plan.routePattern = route;
        plan.spawnPosition = spawn;
        plan.targetPosition = target;
        plan.crossesCenter = forceCenter || RouteNaturallyCrossesCenter(entry, exit, route);
        plan.verticalRoute = vertical;

        RememberRoute(route, plan.crossesCenter);
        return true;
    }

    public bool TryResolveSpawnPosition(
        FishGameplayProfile profile,
        Vector3 desiredPosition,
        bool schoolingMember,
        out Vector3 resolvedPosition
    )
    {
        resolvedPosition = desiredPosition;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        float profileDistance = profile != null
            ? Mathf.Max(0.05f, profile.minimumSpawnDistance) *
              Mathf.Max(0.1f, profile.fishSizeSpacingMultiplier)
            : fallbackMinimumSpawnDistance;
        profileDistance *= Mathf.Max(1f, globalSpawnSpacingMultiplier);

        float configuredSchoolMultiplier = profile != null
            ? profile.schoolingSpacingMultiplier
            : schoolingSpawnSpacingMultiplier;
        float schoolMultiplier = Mathf.Clamp(
            Mathf.Max(
                minimumSchoolingSpawnSpacingMultiplier,
                configuredSchoolMultiplier
            ),
            0.65f,
            1f
        );
        float spacing = schoolingMember
            ? profileDistance * schoolMultiplier
            : profileDistance;

        if (IsSpawnPositionClear(resolvedPosition, spacing))
        {
            return true;
        }

        int attempts = profile != null
            ? Mathf.Max(1, profile.maximumRouteCorrectionAttempts)
            : maximumSpawnCorrectionAttempts;
        attempts = Mathf.Min(attempts, maximumSpawnCorrectionAttempts);

        Vector3 viewport = targetCamera != null
            ? targetCamera.WorldToViewportPoint(desiredPosition)
            : new Vector3(0.5f, 0.5f, 0f);
        bool horizontalEdge = viewport.x < 0f || viewport.x > 1f;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            float direction = (attempt & 1) == 0 ? -1f : 1f;
            float step = Mathf.Ceil(attempt * 0.5f) * spawnCorrectionViewportStep;
            Vector3 candidateViewport = viewport;

            if (horizontalEdge)
            {
                candidateViewport.y = Mathf.Clamp(viewport.y + direction * step, -0.05f, 1.05f);
            }
            else
            {
                candidateViewport.x = Mathf.Clamp(viewport.x + direction * step, -0.05f, 1.05f);
            }

            if (targetCamera != null)
            {
                float depth = Mathf.Abs(desiredPosition.z - targetCamera.transform.position.z);
                Vector3 candidate = targetCamera.ViewportToWorldPoint(
                    new Vector3(candidateViewport.x, candidateViewport.y, depth)
                );
                candidate.z = desiredPosition.z;
                resolvedPosition = candidate;
            }
            else
            {
                resolvedPosition = desiredPosition +
                    (horizontalEdge ? Vector3.up : Vector3.right) * direction * step * 8f;
            }

            if (IsSpawnPositionClear(resolvedPosition, spacing))
            {
                return true;
            }
        }

        return false;
    }

    public void RememberFishIndex(int fishIndex)
    {
        if (fishIndex == lastRememberedFishIndex)
        {
            consecutiveSameSpecies++;
        }
        else
        {
            lastRememberedFishIndex = fishIndex;
            consecutiveSameSpecies = 1;
        }

        if (recentFishHistorySize <= 0)
        {
            return;
        }

        int capacity = Mathf.Min(
            recentFishHistorySize,
            recentFishIndices.Length
        );
        recentFishIndices[recentFishCursor] = fishIndex;
        recentFishCursor = (recentFishCursor + 1) % capacity;
        recentFishCount = Mathf.Min(recentFishCount + 1, capacity);
    }

    public float GetFishRepeatWeightMultiplier(int fishIndex)
    {
        if (fishIndex == lastRememberedFishIndex &&
            consecutiveSameSpecies >= maximumConsecutiveSameSpecies)
        {
            return Mathf.Clamp01(immediateRepeatWeightMultiplier);
        }

        int capacity = Mathf.Min(
            recentFishHistorySize,
            recentFishIndices.Length
        );
        int count = Mathf.Min(recentFishCount, capacity);

        for (int age = 0; age < count; age++)
        {
            int index = recentFishCursor - 1 - age;
            while (index < 0)
            {
                index += capacity;
            }

            if (recentFishIndices[index] != fishIndex)
            {
                continue;
            }

            if (age == 0)
            {
                return Mathf.Clamp01(immediateRepeatWeightMultiplier);
            }

            float recovery = count > 1
                ? age / (float)(count - 1)
                : 0f;
            return Mathf.Lerp(
                Mathf.Clamp01(repeatedFishWeightMultiplier),
                0.85f,
                recovery
            );
        }

        return 1f;
    }

    public float GetProfilePopulationBalanceMultiplier(
        FishGameplayProfile profile,
        FishLevelPopulationProfile population
    )
    {
        if (profile == null)
        {
            return 1f;
        }

        int capacity = population != null
            ? Mathf.Max(1, population.maximumActiveFish)
            : Mathf.Max(24, activeFish + 1);

        float specialShare = population != null
            ? Mathf.Max(
                minimumSpecialFishShare,
                population.specialFishPercentage
            )
            : minimumSpecialFishShare;
        float largeShare = population != null
            ? Mathf.Max(
                minimumLargeFishShare,
                population.largeFishPercentage
            )
            : minimumLargeFishShare;
        float rareShare = population != null
            ? Mathf.Max(
                minimumRareFishShare,
                population.rarePercentage + population.epicPercentage
            )
            : minimumRareFishShare;

        float ordinaryShare = Mathf.Clamp01(1f - specialShare);
        float smallShare = population != null
            ? Mathf.Clamp01(population.smallFishPercentage) * ordinaryShare
            : 0.54f * ordinaryShare;
        float mediumShare = population != null
            ? Mathf.Clamp01(population.mediumFishPercentage) * ordinaryShare
            : 0.46f * ordinaryShare;

        float multiplier = 1f;
        switch (profile.fishTier)
        {
            case FishTier.Small:
                multiplier *= GetCategoryDeficitMultiplier(
                    activeSmallFish,
                    capacity * smallShare
                );
                break;
            case FishTier.Medium:
                multiplier *= GetCategoryDeficitMultiplier(
                    activeMediumFish,
                    capacity * mediumShare
                );
                break;
            case FishTier.Special:
                multiplier *= GetCategoryDeficitMultiplier(
                    activeSpecialFish,
                    capacity * specialShare
                );
                break;
            default:
                return 0f;
        }

        if (profile.sizeClass == FishSizeClass.Large ||
            profile.sizeClass == FishSizeClass.Huge)
        {
            multiplier *= GetCategoryDeficitMultiplier(
                activeLargeFish,
                capacity * largeShare
            );
        }

        if (profile.rarity == FishRarity.Rare ||
            profile.rarity == FishRarity.Epic ||
            profile.rarity == FishRarity.Legendary)
        {
            multiplier *= GetCategoryDeficitMultiplier(
                activeRareFish,
                capacity * rareShare
            );
        }

        return Mathf.Clamp(multiplier, 0.03f, 8f);
    }

    public int CountActiveProfile(FishGameplayProfile profile)
    {
        if (profile == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < FishMotionAgent.ActiveAgentCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy)
            {
                continue;
            }

            FishScript fish = agent.GetComponent<FishScript>();
            if (fish != null && fish.GetGameplayProfile() == profile)
            {
                count++;
            }
        }

        return count;
    }


    public int CountActiveSizeClass(FishSizeClass minimumSize)
    {
        int count = 0;
        for (int i = 0; i < FishMotionAgent.ActiveAgentCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy ||
                agent.RuntimeState == FishRuntimeState.Dying ||
                agent.RuntimeState == FishRuntimeState.ReturningToPool)
            {
                continue;
            }

            FishScript fish = agent.GetComponent<FishScript>();
            FishGameplayProfile activeProfile = fish != null
                ? fish.GetGameplayProfile()
                : null;
            if (activeProfile != null && (int)activeProfile.sizeClass >= (int)minimumSize)
            {
                count++;
            }
        }
        return count;
    }

    public int CountActiveGroups()
    {
        int groupCount = 0;
        for (int i = 0; i < FishMotionAgent.ActiveAgentCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy ||
                agent.RuntimeState == FishRuntimeState.Dying ||
                agent.RuntimeState == FishRuntimeState.ReturningToPool)
            {
                continue;
            }

            Transform leader = agent.FollowTarget;
            if (leader == null)
            {
                continue;
            }

            bool alreadyCounted = false;
            for (int earlier = 0; earlier < i; earlier++)
            {
                FishMotionAgent earlierAgent = FishMotionAgent.GetActiveAgent(earlier);
                if (earlierAgent != null && earlierAgent.FollowTarget == leader)
                {
                    alreadyCounted = true;
                    break;
                }
            }

            if (!alreadyCounted)
            {
                groupCount++;
            }
        }
        return groupCount;
    }

    public void RefreshPopulationSnapshot()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        activeFish = 0;
        leftSideFish = 0;
        rightSideFish = 0;
        topSideFish = 0;
        bottomSideFish = 0;
        centerFish = 0;
        enteringFish = 0;
        exitingFish = 0;
        activeSmallFish = 0;
        activeMediumFish = 0;
        activeSpecialFish = 0;
        activeLargeFish = 0;
        activeRareFish = 0;

        for (int i = 0; i < FishMotionAgent.ActiveAgentCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy)
            {
                continue;
            }

            FishRuntimeState state = agent.RuntimeState;
            if (state == FishRuntimeState.Inactive ||
                state == FishRuntimeState.Dying ||
                state == FishRuntimeState.ReturningToPool)
            {
                continue;
            }

            activeFish++;

            FishScript activeScript = agent.GetComponent<FishScript>();
            FishGameplayProfile activeProfile = activeScript != null
                ? activeScript.GetGameplayProfile()
                : null;
            FishTier activeTier = activeScript != null
                ? activeScript.GetFishTier()
                : FishTier.Small;

            if (activeTier == FishTier.Small) activeSmallFish++;
            else if (activeTier == FishTier.Medium) activeMediumFish++;
            else if (activeTier == FishTier.Special) activeSpecialFish++;

            if (activeProfile != null)
            {
                if (activeProfile.sizeClass == FishSizeClass.Large ||
                    activeProfile.sizeClass == FishSizeClass.Huge)
                {
                    activeLargeFish++;
                }

                if (activeProfile.rarity == FishRarity.Rare ||
                    activeProfile.rarity == FishRarity.Epic ||
                    activeProfile.rarity == FishRarity.Legendary)
                {
                    activeRareFish++;
                }
            }

            if (state == FishRuntimeState.Entering || state == FishRuntimeState.BossIntro)
            {
                enteringFish++;
            }
            else if (state == FishRuntimeState.Exiting ||
                     state == FishRuntimeState.LevelTransitionExit ||
                     state == FishRuntimeState.BossEscaping)
            {
                exitingFish++;
            }

            if (targetCamera == null)
            {
                continue;
            }

            Vector3 viewport = targetCamera.WorldToViewportPoint(agent.transform.position);
            if (viewport.x < 0.5f) leftSideFish++; else rightSideFish++;
            if (viewport.y < 0.5f) bottomSideFish++; else topSideFish++;

            if (Mathf.Abs(viewport.x - 0.5f) <= centerAreaHalfWidth &&
                Mathf.Abs(viewport.y - 0.5f) <= centerAreaHalfHeight)
            {
                centerFish++;
            }
        }
    }

    private FishScreenSide SelectBalancedEntrySide(
        bool allowVertical,
        FishGameplayProfile profile
    )
    {
        int horizontalMinimum = Mathf.Min(leftSideFish, rightSideFish);
        int verticalMinimum = Mathf.Min(topSideFish, bottomSideFish);
        float leftWeight = CalculateSideWeight(leftSideFish, horizontalMinimum);
        float rightWeight = CalculateSideWeight(rightSideFish, horizontalMinimum);
        float topWeight = allowVertical
            ? CalculateSideWeight(topSideFish, verticalMinimum) * verticalEntryChance
            : 0f;
        float bottomWeight = allowVertical
            ? CalculateSideWeight(bottomSideFish, verticalMinimum) * verticalEntryChance
            : 0f;

        ApplyPreferredEntryWeights(
            profile != null ? profile.preferredEntryDirection : FishPreferredDirection.Any,
            ref leftWeight,
            ref rightWeight,
            ref topWeight,
            ref bottomWeight
        );

        float total = leftWeight + rightWeight + topWeight + bottomWeight;
        if (total <= 0f)
        {
            return Random.value < 0.5f ? FishScreenSide.Left : FishScreenSide.Right;
        }

        float roll = Random.value * total;
        roll -= leftWeight;
        if (roll <= 0f) return FishScreenSide.Left;
        roll -= rightWeight;
        if (roll <= 0f) return FishScreenSide.Right;
        roll -= topWeight;
        if (roll <= 0f) return FishScreenSide.Top;
        return FishScreenSide.Bottom;
    }

    private float CalculateSideWeight(int sideCount, int minimum)
    {
        float imbalance = Mathf.Max(0, sideCount - minimum);
        float weight = 1f / (1f + imbalance * Mathf.Max(0f, sideBalanceStrength));
        if (sideCount == 0)
        {
            weight += emptySideBonus;
        }
        return Mathf.Max(0.01f, weight);
    }

    private FishRoutePattern SelectBalancedRoute(FishGameplayProfile profile)
    {
        bool forceCenter = ShouldForceCenterRoute(profile);
        FishRoutePattern selected = profile != null
            ? profile.SelectRoute(forceCenter)
            : forceCenter
                ? FishRoutePattern.CenterCrossing
                : (FishRoutePattern)Random.Range(0, 6);

        if (!forceCenter && Random.value <= runtimeEvasiveRouteChance)
        {
            selected = FishRoutePattern.EnterAndRetreatRoute;
        }
        else if (!forceCenter && Random.value <= runtimeComplexRouteChance)
        {
            int complexIndex = Random.Range(0, 4);
            selected = complexIndex == 0
                ? FishRoutePattern.WideCurvedRoute
                : complexIndex == 1
                    ? FishRoutePattern.SShapedRoute
                    : complexIndex == 2
                        ? FishRoutePattern.LoopingRoute
                        : FishRoutePattern.CenterCrossing;
        }

        int capacity = Mathf.Min(recentRouteHistorySize, recentRoutes.Length);
        for (int i = 0; i < Mathf.Min(recentRouteCount, capacity); i++)
        {
            if (recentRoutes[i] == selected && Random.value < 0.72f)
            {
                selected = forceCenter
                    ? FishRoutePattern.CenterCrossing
                    : (FishRoutePattern)Random.Range(0, 6);
                break;
            }
        }

        return selected;
    }

    private bool ShouldForceCenterRoute(FishGameplayProfile profile)
    {
        float desired = Mathf.Max(
            minimumCenterCrossingPercentage,
            profile != null ? profile.centerCrossingChance : 0f
        );
        float centerOccupancy = activeFish > 0 ? centerFish / (float)activeFish : 0f;
        float centerRouteRatio = GetRecentCenterRouteRatio();
        float occupancyDeficit = Mathf.Clamp01(desired - centerOccupancy);
        float routeDeficit = Mathf.Clamp01(desired - centerRouteRatio);
        float probability = desired * 0.30f +
            occupancyDeficit * 0.75f +
            routeDeficit * 1.15f;
        return Random.value <= Mathf.Clamp01(probability);
    }

    private float GetRecentCenterRouteRatio()
    {
        int capacity = Mathf.Min(recentRouteHistorySize, recentRoutes.Length);
        int count = Mathf.Min(recentRouteCount, capacity);
        if (count <= 0)
        {
            return 0f;
        }

        int centerCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (recentRouteCrossesCenter[i])
            {
                centerCount++;
            }
        }
        return centerCount / (float)count;
    }

    private float SelectEntryLane(
        FishScreenSide side,
        bool forceCenter,
        FishGameplayProfile profile
    )
    {
        if (forceCenter)
        {
            return Random.Range(0.24f, 0.76f);
        }

        int laneBand = Random.Range(0, 5);
        float lane = Mathf.Lerp(
            0.10f,
            0.90f,
            (laneBand + Random.Range(0.15f, 0.85f)) / 5f
        );

        if (profile != null &&
            (side == FishScreenSide.Left || side == FishScreenSide.Right))
        {
            lane = Mathf.Lerp(
                lane,
                Mathf.Clamp01(profile.preferredSwimmingDepth),
                0.45f
            );
        }

        return Mathf.Clamp(lane, 0.06f, 0.94f);
    }

    private float SelectExitLane(float entryLane, FishRoutePattern route, bool forceCenter)
    {
        if (forceCenter || route == FishRoutePattern.DiagonalCrossing ||
            route == FishRoutePattern.SShapedRoute)
        {
            return Mathf.Clamp01(1f - entryLane + Random.Range(-0.16f, 0.16f));
        }

        if (route == FishRoutePattern.EdgeRoute)
        {
            return Mathf.Clamp(entryLane + Random.Range(-0.12f, 0.12f), 0.06f, 0.94f);
        }

        return Mathf.Clamp(entryLane + Random.Range(-0.32f, 0.32f), 0.06f, 0.94f);
    }

    private static FishScreenSide SelectExitSide(
        FishScreenSide entry,
        FishRoutePattern route,
        FishGameplayProfile profile
    )
    {
        if (route == FishRoutePattern.EnterAndRetreatRoute)
        {
            return entry;
        }

        FishPreferredDirection preferred = profile != null
            ? profile.preferredExitDirection
            : FishPreferredDirection.OppositeEntry;
        switch (preferred)
        {
            case FishPreferredDirection.Left: return FishScreenSide.Left;
            case FishPreferredDirection.Right: return FishScreenSide.Right;
            case FishPreferredDirection.Top: return FishScreenSide.Top;
            case FishPreferredDirection.Bottom: return FishScreenSide.Bottom;
            case FishPreferredDirection.Horizontal:
                return entry == FishScreenSide.Left
                    ? FishScreenSide.Right
                    : entry == FishScreenSide.Right
                        ? FishScreenSide.Left
                        : Random.value < 0.5f
                            ? FishScreenSide.Left
                            : FishScreenSide.Right;
            case FishPreferredDirection.Vertical:
                return entry == FishScreenSide.Top
                    ? FishScreenSide.Bottom
                    : entry == FishScreenSide.Bottom
                        ? FishScreenSide.Top
                        : Random.value < 0.5f
                            ? FishScreenSide.Top
                            : FishScreenSide.Bottom;
            case FishPreferredDirection.OppositeEntry:
                return FishScreenBounds.Opposite(entry);
        }

        if (route == FishRoutePattern.DiagonalCrossing)
        {
            if (entry == FishScreenSide.Left || entry == FishScreenSide.Right)
            {
                return Random.value < 0.5f ? FishScreenSide.Top : FishScreenSide.Bottom;
            }
            return Random.value < 0.5f ? FishScreenSide.Left : FishScreenSide.Right;
        }

        return FishScreenBounds.Opposite(entry);
    }

    private static void ApplyPreferredEntryWeights(
        FishPreferredDirection preferred,
        ref float left,
        ref float right,
        ref float top,
        ref float bottom
    )
    {
        const float preferredBoost = 2.35f;
        const float nonPreferredMultiplier = 0.32f;

        switch (preferred)
        {
            case FishPreferredDirection.Left:
                left *= preferredBoost;
                right *= nonPreferredMultiplier;
                break;
            case FishPreferredDirection.Right:
                right *= preferredBoost;
                left *= nonPreferredMultiplier;
                break;
            case FishPreferredDirection.Top:
                top *= preferredBoost;
                bottom *= nonPreferredMultiplier;
                break;
            case FishPreferredDirection.Bottom:
                bottom *= preferredBoost;
                top *= nonPreferredMultiplier;
                break;
            case FishPreferredDirection.Horizontal:
                left *= preferredBoost;
                right *= preferredBoost;
                top *= nonPreferredMultiplier;
                bottom *= nonPreferredMultiplier;
                break;
            case FishPreferredDirection.Vertical:
                top *= preferredBoost;
                bottom *= preferredBoost;
                left *= nonPreferredMultiplier;
                right *= nonPreferredMultiplier;
                break;
        }
    }

    private static bool RouteNaturallyCrossesCenter(
        FishScreenSide entry,
        FishScreenSide exit,
        FishRoutePattern route
    )
    {
        return route == FishRoutePattern.CenterCrossing ||
               route == FishRoutePattern.DiagonalCrossing ||
               route == FishRoutePattern.SShapedRoute ||
               FishScreenBounds.Opposite(entry) == exit;
    }

    private bool IsSpawnPositionClear(Vector3 position, float minimumDistance)
    {
        float minimumSqr = minimumDistance * minimumDistance;

        for (int i = 0; i < FishMotionAgent.ActiveAgentCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy ||
                agent.RuntimeState == FishRuntimeState.Dying ||
                agent.RuntimeState == FishRuntimeState.ReturningToPool)
            {
                continue;
            }

            float combined = minimumDistance +
                agent.SpacingRadius *
                Mathf.Clamp(existingFishRadiusContribution, 0.35f, 1f);
            if (((Vector2)(position - agent.transform.position)).sqrMagnitude <
                Mathf.Max(minimumSqr, combined * combined))
            {
                return false;
            }
        }

        return true;
    }

    private void RememberRoute(FishRoutePattern route, bool crossesCenter)
    {
        int capacity = Mathf.Min(recentRouteHistorySize, recentRoutes.Length);
        if (capacity <= 0)
        {
            return;
        }

        recentRoutes[recentRouteCursor] = route;
        recentRouteCrossesCenter[recentRouteCursor] = crossesCenter;
        recentRouteCursor = (recentRouteCursor + 1) % capacity;
        recentRouteCount = Mathf.Min(recentRouteCount + 1, capacity);
    }

    private float GetCategoryDeficitMultiplier(
        int currentCount,
        float desiredCount
    )
    {
        desiredCount = Mathf.Max(0.5f, desiredCount);
        float ratio = currentCount / desiredCount;

        if (ratio < 1f)
        {
            return 1f +
                (1f - ratio) * Mathf.Max(0f, categoryDeficitStrength);
        }

        if (ratio <= 1.15f)
        {
            return 1f;
        }

        float overAmount = Mathf.Clamp01((ratio - 1.15f) / 1.35f);
        return Mathf.Lerp(
            1f,
            Mathf.Clamp01(overrepresentedCategoryMultiplier),
            overAmount
        );
    }

    private static float GetProfileSpawnRadius(FishGameplayProfile profile)
    {
        if (profile == null)
        {
            return 0.45f;
        }

        return Mathf.Max(0.20f, profile.minimumSpawnDistance * 0.5f);
    }
}
