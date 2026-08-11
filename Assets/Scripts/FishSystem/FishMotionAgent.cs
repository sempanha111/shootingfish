using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class FishMotionAgent : MonoBehaviour
{
    private const int MaximumRoutePoints = 8;
    private const float MinimumDirectionSqrMagnitude = 0.0001f;

    // A spacing radius is already measured from the complete visual bounds.
    // Using most of the sum of two radii prevents stacked sprites without
    // making a school look unnaturally sparse.
    private const float PairSpacingFactor = 0.95f;
    private const float MinimumSchoolSpacingMultiplier = 0.74f;
    private const float MaximumSpacingCorrection = 1.15f;

    private static readonly List<FishMotionAgent> ActiveAgentsInternal =
        new List<FishMotionAgent>(96);

    [Header("Runtime State")]
    [SerializeField] private FishRuntimeState runtimeState = FishRuntimeState.Inactive;
    [SerializeField] private FishRoutePattern currentRoutePattern = FishRoutePattern.StraightCrossing;
    [SerializeField] private bool externalMovementAuthority;

    [Header("Optional Overrides")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer primaryRenderer;
    [SerializeField] private Animator animator;

    private FishScript owner;
    private FishGameplayProfile profile;
    private FishMovementProfile legacyMovementProfile;
    private Camera targetCamera;
    private Renderer[] cachedVisualRenderers;
    private Collider2D[] cachedVisualColliders;
    private float cachedVisualRadius = 0.5f;

    private readonly Vector3[] routePoints = new Vector3[MaximumRoutePoints];
    private int routePointCount;
    private int routePointIndex;
    private bool routeEndsOutside;
    private bool bossMode;
    private bool bossWarning;
    private bool bossEscaping;
    private bool transitionExitScheduled;
    private float transitionExitAt;
    private FishRuntimeState scheduledExitState = FishRuntimeState.LevelTransitionExit;
    private float scheduledEscapeSpeedMultiplier = 1f;

    private Transform followTarget;
    private Vector3 followLocalOffset;
    private bool escortFollow;

    private Vector2 desiredVelocity;
    private Vector2 spacingCorrection;
    private float nextSpacingCheckTime;
    private float nextSpeedChangeTime;
    private float currentCruiseSpeed;
    private float burstUntil;
    private float pauseUntil;
    private FishRuntimeState stateBeforePause;
    private float offscreenWaitUntil;
    private float routeNoiseSeed;
    private float routeStartedAt;
    private float lastProgressCheckTime;
    private Vector3 lastProgressPosition;
    private float nextRateLimitedLogTime;
    private float scriptedSweepAmplitude;
    private float scriptedSweepFrequency = 1f;
    private bool scriptedRoute;
    private bool hasEnteredVisibleView;
    private FishScreenSide lastEntrySide = FishScreenSide.Left;
    private FishScreenSide plannedExitSide = FishScreenSide.Right;

    public FishRuntimeState RuntimeState
    {
        get { return runtimeState; }
    }

    public FishRoutePattern CurrentRoutePattern
    {
        get { return currentRoutePattern; }
    }

    public bool HasMovementAuthority
    {
        get { return !externalMovementAuthority; }
    }

    public bool IsBossMode
    {
        get { return bossMode; }
    }

    public Transform FollowTarget
    {
        get { return followTarget; }
    }

    public float SpacingRadius
    {
        get
        {
            float configured = profile != null
                ? profile.minimumSwimmingDistance * profile.fishSizeSpacingMultiplier
                : GetDefaultSpacingForTier();
            return Mathf.Max(configured, GetVisualRadius() * 0.88f);
        }
    }

    public static int ActiveAgentCount
    {
        get { return ActiveAgentsInternal.Count; }
    }

    public static FishMotionAgent GetActiveAgent(int index)
    {
        return index >= 0 && index < ActiveAgentsInternal.Count
            ? ActiveAgentsInternal[index]
            : null;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        RefreshCachedVisualRadius();
        RegisterActiveAgent();
        hasEnteredVisibleView = false;
        nextSpacingCheckTime = Time.time + Random.Range(0.05f, 0.30f);
        lastProgressCheckTime = Time.time;
        lastProgressPosition = transform.position;
    }

    private void OnDisable()
    {
        UnregisterActiveAgent();
        ResetForPool();
    }

    private void FixedUpdate()
    {
        if (externalMovementAuthority || owner == null || body == null)
        {
            return;
        }

        if (!owner.IsAliveTarget || runtimeState == FishRuntimeState.Dying ||
            runtimeState == FishRuntimeState.ReturningToPool ||
            runtimeState == FishRuntimeState.Inactive)
        {
            body.velocity = Vector2.zero;
            return;
        }

        if (transitionExitScheduled && Time.time >= transitionExitAt)
        {
            transitionExitScheduled = false;
            BuildExitRoute(
                scheduledExitState,
                scheduledExitState == FishRuntimeState.BossEscaping
                    ? scheduledEscapeSpeedMultiplier
                    : 1.20f
            );
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null &&
            FishScreenBounds.IsInsideVisibleView(targetCamera, transform.position))
        {
            hasEnteredVisibleView = true;
        }

        if (runtimeState == FishRuntimeState.WaitingOffScreen)
        {
            DecelerateToStop(Time.fixedDeltaTime);

            if (Time.time >= offscreenWaitUntil)
            {
                BuildReturnRouteFromOutside();
            }

            return;
        }

        if (runtimeState == FishRuntimeState.Pausing)
        {
            DecelerateToStop(Time.fixedDeltaTime);

            if (Time.time >= pauseUntil)
            {
                runtimeState = stateBeforePause;
                AdvanceRoutePoint();
            }

            return;
        }

        if (runtimeState == FishRuntimeState.GroupFollowing)
        {
            UpdateFollowMovement(Time.fixedDeltaTime);
            return;
        }

        if (runtimeState == FishRuntimeState.BossEscaping ||
            runtimeState == FishRuntimeState.LevelTransitionExit ||
            runtimeState == FishRuntimeState.Exiting)
        {
            if (IsFullyOutsidePaddedView())
            {
                CompleteOutsideExit();
                return;
            }
        }

        if (routePointCount <= 0 || routePointIndex >= routePointCount)
        {
            HandleRouteComplete();
            return;
        }

        UpdateSpacingCorrection();
        UpdateRouteMovement(Time.fixedDeltaTime);
        CheckForStuckFish();
    }

    public void Bind(
        FishScript fishOwner,
        FishGameplayProfile gameplayProfile,
        FishMovementProfile movementProfile
    )
    {
        owner = fishOwner;
        profile = gameplayProfile;
        legacyMovementProfile = movementProfile;
        CacheReferences();
        RefreshCachedVisualRadius();

        if (profile != null && animator != null)
        {
            animator.speed = Mathf.Max(0.05f, profile.animatorSpeedMultiplier);
        }
    }

    public void SetExternalMovementAuthority(bool value)
    {
        externalMovementAuthority = value;

        if (value)
        {
            routePointCount = 0;
            routePointIndex = 0;
            runtimeState = FishRuntimeState.BossActive;
        }
    }

    public void ResetForPool()
    {
        runtimeState = FishRuntimeState.Inactive;
        currentRoutePattern = FishRoutePattern.StraightCrossing;
        externalMovementAuthority = false;
        routePointCount = 0;
        routePointIndex = 0;
        routeEndsOutside = false;
        bossMode = false;
        bossWarning = false;
        bossEscaping = false;
        transitionExitScheduled = false;
        transitionExitAt = 0f;
        scheduledExitState = FishRuntimeState.LevelTransitionExit;
        scheduledEscapeSpeedMultiplier = 1f;
        followTarget = null;
        followLocalOffset = Vector3.zero;
        escortFollow = false;
        desiredVelocity = Vector2.zero;
        spacingCorrection = Vector2.zero;
        scriptedSweepAmplitude = 0f;
        scriptedSweepFrequency = 1f;
        scriptedRoute = false;
        hasEnteredVisibleView = false;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    public void StopMotion()
    {
        routePointCount = 0;
        routePointIndex = 0;
        transitionExitScheduled = false;
        followTarget = null;
        desiredVelocity = Vector2.zero;
        spacingCorrection = Vector2.zero;

        if (runtimeState != FishRuntimeState.Dying)
        {
            runtimeState = FishRuntimeState.Preparing;
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    public void ConfigureMovementStyle(FishScript.SwimStyle style)
    {
        if (externalMovementAuthority)
        {
            return;
        }

        bossMode = owner != null && owner.GetFishTier() == FishTier.MainBoss;
        bossEscaping = false;
        transitionExitScheduled = false;
        followTarget = null;
        scriptedRoute = false;
        routeNoiseSeed = Random.Range(0f, 1000f);

        if (style == FishScript.SwimStyle.FastTideExit)
        {
            BuildExitRoute(FishRuntimeState.LevelTransitionExit, 1.20f);
            return;
        }

        if (bossMode || IsBossStyle(style))
        {
            ConfigureBossMovement();
            return;
        }

        currentRoutePattern = MapStyleToRoute(style);
        BuildNormalRoute(currentRoutePattern);
    }

    public void ConfigureScriptedRoute(
        Vector3 target,
        FishScript.SwimStyle style,
        float sweepAmplitude,
        float sweepFrequency
    )
    {
        if (externalMovementAuthority)
        {
            return;
        }

        bossMode = owner != null && owner.GetFishTier() == FishTier.MainBoss;
        bossEscaping = false;
        transitionExitScheduled = false;
        followTarget = null;
        scriptedRoute = true;
        scriptedSweepAmplitude = Mathf.Max(0f, sweepAmplitude);
        scriptedSweepFrequency = Mathf.Max(0.25f, sweepFrequency);
        routeNoiseSeed = Random.Range(0f, 1000f);
        currentRoutePattern = MapStyleToRoute(style);
        ClearRoute();

        Vector3 start = transform.position;
        Vector3 direction = target - start;
        float distance = direction.magnitude;

        if (distance > 1f && scriptedSweepAmplitude > 0.01f)
        {
            Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward);
            AddRoutePoint(Vector3.Lerp(start, target, 0.35f) +
                          perpendicular * scriptedSweepAmplitude);
            AddRoutePoint(Vector3.Lerp(start, target, 0.68f) -
                          perpendicular * scriptedSweepAmplitude * 0.65f);
        }

        AddRoutePoint(target);
        routeEndsOutside = IsPointOutsidePaddedView(target, 0.01f);
        routePointIndex = 0;
        runtimeState = FishRuntimeState.Entering;
        routeStartedAt = Time.time;
        SelectNewCruiseSpeed();
    }

    public void ConfigureFollow(
        Transform leader,
        Vector3 localOffset,
        bool isEscort
    )
    {
        if (externalMovementAuthority)
        {
            return;
        }

        followTarget = leader;
        followLocalOffset = localOffset;
        escortFollow = isEscort;
        routePointCount = 0;
        routePointIndex = 0;
        routeEndsOutside = false;
        transitionExitScheduled = false;
        bossEscaping = false;
        runtimeState = FishRuntimeState.GroupFollowing;
        SelectNewCruiseSpeed();
    }

    public void ConfigureBossMovement()
    {
        if (externalMovementAuthority)
        {
            return;
        }

        bossMode = true;
        bossEscaping = false;
        bossWarning = false;
        transitionExitScheduled = false;
        followTarget = null;
        scriptedRoute = false;
        runtimeState = FishRuntimeState.BossIntro;
        BuildBossPassRoute(false);
    }

    public void BeginBossWarning(float forcedCenterRouteChance)
    {
        if (externalMovementAuthority || !bossMode)
        {
            return;
        }

        bossWarning = true;
        runtimeState = FishRuntimeState.BossWarning;

        if (Random.value <= Mathf.Clamp01(forcedCenterRouteChance))
        {
            BuildBossPassRoute(true);
        }
    }

    public void RequestBossEscape(float delay, float speedMultiplier)
    {
        if (externalMovementAuthority)
        {
            return;
        }

        bossMode = true;
        bossEscaping = true;
        scheduledEscapeSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        transitionExitScheduled = delay > 0f;
        transitionExitAt = Time.time + Mathf.Max(0f, delay);
        scheduledExitState = FishRuntimeState.BossEscaping;

        if (!transitionExitScheduled)
        {
            BuildExitRoute(FishRuntimeState.BossEscaping, scheduledEscapeSpeedMultiplier);
        }
        else
        {
            runtimeState = FishRuntimeState.BossWarning;
        }
    }

    public void RequestLevelTransitionExit(float delay)
    {
        if (externalMovementAuthority || runtimeState == FishRuntimeState.Dying)
        {
            return;
        }

        transitionExitScheduled = true;
        transitionExitAt = Time.time + Mathf.Max(0f, delay);
        scheduledExitState = FishRuntimeState.LevelTransitionExit;
    }

    /// <summary>
    /// Immediately replaces the current route with a faster visible exit.
    /// The fish is still pooled only after its full visual/collider bounds
    /// are outside the padded screen.
    /// </summary>
    public void ForceLevelTransitionExit(float speedMultiplier)
    {
        if (externalMovementAuthority ||
            runtimeState == FishRuntimeState.Dying ||
            runtimeState == FishRuntimeState.ReturningToPool ||
            runtimeState == FishRuntimeState.Inactive)
        {
            return;
        }

        transitionExitScheduled = false;
        scheduledExitState = FishRuntimeState.LevelTransitionExit;
        BuildExitRoute(
            FishRuntimeState.LevelTransitionExit,
            Mathf.Max(1.20f, speedMultiplier)
        );
    }

    public void MarkDying()
    {
        runtimeState = FishRuntimeState.Dying;
        routePointCount = 0;
        routePointIndex = 0;
        followTarget = null;
        transitionExitScheduled = false;
        desiredVelocity = Vector2.zero;

        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    public bool IsFullyOutsidePaddedView()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera != null && FishScreenBounds.IsFullyOutside(
            targetCamera,
            transform,
            cachedVisualRenderers,
            cachedVisualColliders,
            GetViewportPadding()
        );
    }

    public int CopyRoutePoints(Vector3[] destination)
    {
        if (destination == null)
        {
            return 0;
        }

        int count = Mathf.Min(destination.Length, routePointCount);
        for (int i = 0; i < count; i++)
        {
            destination[i] = routePoints[i];
        }

        return count;
    }

    public int CurrentRoutePointIndex
    {
        get { return routePointIndex; }
    }

    private void UpdateRouteMovement(float deltaTime)
    {
        Vector3 target = routePoints[routePointIndex];
        Vector2 toTarget = target - transform.position;
        float reachDistance = GetWaypointReachDistance();

        // A very long boss can physically pass a waypoint before its slowly
        // turning heading lines up with the exact point. Do not make it orbit
        // backward around a missed interior waypoint like a small fish.
        if (ShouldSkipMissedLongBodyWaypoint(toTarget, reachDistance))
        {
            AdvanceRoutePoint();
            return;
        }

        if (toTarget.sqrMagnitude <= reachDistance * reachDistance)
        {
            if (ShouldPauseAtWaypoint())
            {
                stateBeforePause = runtimeState;
                runtimeState = FishRuntimeState.Pausing;
                pauseUntil = Time.time + GetPauseDuration();
                return;
            }

            AdvanceRoutePoint();
            return;
        }

        Vector2 direction = toTarget.normalized;
        float wobble = GetWobbleAngle();
        direction = Rotate(direction, wobble);

        if (scriptedRoute && scriptedSweepAmplitude > 0.01f)
        {
            float sweep = Mathf.Sin(
                (Time.time - routeStartedAt) * scriptedSweepFrequency * Mathf.PI * 2f
            );
            direction += Rotate(direction, 90f) *
                sweep * scriptedSweepAmplitude * 0.08f;
            direction.Normalize();
        }

        direction += spacingCorrection;
        if (direction.sqrMagnitude > MinimumDirectionSqrMagnitude)
        {
            direction.Normalize();
        }

        direction = ApplyBossLongBodySteering(direction, deltaTime);

        UpdateCruiseSpeed();
        float speed = currentCruiseSpeed;

        if (bossEscaping ||
            runtimeState == FishRuntimeState.BossEscaping ||
            runtimeState == FishRuntimeState.LevelTransitionExit)
        {
            speed *= scheduledEscapeSpeedMultiplier;
        }

        if (Time.time < burstUntil)
        {
            speed *= profile != null
                ? Mathf.Max(1f, profile.burstSpeedMultiplier)
                : 1.30f;
        }

        if (bossMode && profile != null &&
            profile.bossNormalWorldSpeedCap > 0f &&
            !bossEscaping &&
            runtimeState != FishRuntimeState.BossEscaping &&
            runtimeState != FishRuntimeState.LevelTransitionExit)
        {
            speed = Mathf.Min(speed, profile.bossNormalWorldSpeedCap);
        }

        desiredVelocity = direction * speed;
        ApplyVelocityAndFacing(deltaTime);
    }

    private void UpdateFollowMovement(float deltaTime)
    {
        if (followTarget == null || !followTarget.gameObject.activeInHierarchy)
        {
            followTarget = null;
            BuildNormalRoute(FishRoutePattern.GroupRoute);
            return;
        }

        Vector3 desiredPosition = followTarget.TransformPoint(followLocalOffset);
        Vector2 toSlot = desiredPosition - transform.position;
        Vector2 leaderVelocity = Vector2.zero;
        Rigidbody2D leaderBody = followTarget.GetComponent<Rigidbody2D>();
        if (leaderBody != null)
        {
            leaderVelocity = leaderBody.velocity;
        }

        float correction = escortFollow ? 2.1f : 1.5f;
        desiredVelocity = leaderVelocity + toSlot * correction;

        float maximum = Mathf.Max(
            owner != null ? owner.MoveSpeed * 1.35f : 3f,
            currentCruiseSpeed
        );
        desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, maximum);

        UpdateSpacingCorrection(true);
        desiredVelocity += spacingCorrection * maximum * 0.35f;
        ApplyVelocityAndFacing(deltaTime);

        if (FishScreenBounds.IsFullyOutside(
                targetCamera != null ? targetCamera : Camera.main,
                followTarget.gameObject,
                GetViewportPadding()) &&
            IsFullyOutsidePaddedView())
        {
            CompleteOutsideExit();
        }
    }

    private bool ShouldSkipMissedLongBodyWaypoint(
        Vector2 toTarget,
        float reachDistance
    )
    {
        if (!bossMode || profile == null ||
            !profile.bossUseLongBodySteering ||
            body == null ||
            routePointIndex >= routePointCount - 1 ||
            body.velocity.sqrMagnitude <= 0.04f ||
            toTarget.sqrMagnitude <= reachDistance * reachDistance)
        {
            return false;
        }

        Vector2 heading = body.velocity.normalized;
        Vector2 targetDirection = toTarget.normalized;
        float dot = Vector2.Dot(heading, targetDirection);

        // dot < -0.15 means the waypoint is clearly behind the whale.
        // Skipping only interior points preserves the final off-screen exit.
        return dot < -0.15f;
    }

    private Vector2 ApplyBossLongBodySteering(
        Vector2 requestedDirection,
        float deltaTime
    )
    {
        if (!bossMode || profile == null ||
            !profile.bossUseLongBodySteering ||
            requestedDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude ||
            body == null || body.velocity.sqrMagnitude <= 0.01f)
        {
            return requestedDirection;
        }

        Vector2 currentHeading = body.velocity.normalized;
        Vector2 requestedHeading = requestedDirection.normalized;
        float signedDelta = Vector2.SignedAngle(
            currentHeading,
            requestedHeading
        );
        float maximumDelta = Mathf.Max(
            2f,
            profile.bossSteeringDegreesPerSecond
        ) * Mathf.Max(0f, deltaTime);
        float clampedDelta = Mathf.Clamp(
            signedDelta,
            -maximumDelta,
            maximumDelta
        );

        return Rotate(currentHeading, clampedDelta).normalized;
    }

    private void ApplyVelocityAndFacing(float deltaTime)
    {
        float acceleration = profile != null
            ? profile.GetAcceleration(bossMode)
            : GetDefaultAcceleration();
        float deceleration = profile != null
            ? profile.GetDeceleration(bossMode)
            : acceleration * 1.25f;

        float rate = desiredVelocity.sqrMagnitude > body.velocity.sqrMagnitude
            ? acceleration
            : deceleration;

        body.velocity = Vector2.MoveTowards(
            body.velocity,
            desiredVelocity,
            Mathf.Max(0.05f, rate) * deltaTime
        );

        if (body.velocity.sqrMagnitude <= 0.0025f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(body.velocity.y, body.velocity.x) * Mathf.Rad2Deg;
        float turnSpeed = profile != null
            ? profile.GetTurnSpeed(bossMode)
            : GetDefaultTurnSpeed();
        float nextAngle = Mathf.MoveTowardsAngle(
            body.rotation,
            targetAngle,
            turnSpeed * deltaTime
        );
        body.MoveRotation(nextAngle);

        if (primaryRenderer != null &&
            (profile == null || profile.flipDirection))
        {
            primaryRenderer.flipY = body.velocity.x < -0.02f;
        }

        if (animator != null && animator.isActiveAndEnabled)
        {
            float baseSpeed = profile != null
                ? profile.animatorSpeedMultiplier
                : 1f;
            animator.speed = Mathf.Clamp(
                baseSpeed * (0.70f + body.velocity.magnitude /
                    Mathf.Max(0.1f, currentCruiseSpeed) * 0.35f),
                0.35f,
                2.5f
            );
        }
    }

    private void DecelerateToStop(float deltaTime)
    {
        desiredVelocity = Vector2.zero;
        float deceleration = profile != null
            ? profile.GetDeceleration(bossMode)
            : GetDefaultAcceleration() * 1.25f;
        body.velocity = Vector2.MoveTowards(
            body.velocity,
            Vector2.zero,
            deceleration * deltaTime
        );
    }

    private void AdvanceRoutePoint()
    {
        routePointIndex++;

        if (routePointIndex >= routePointCount)
        {
            HandleRouteComplete();
        }
        else
        {
            runtimeState = bossMode
                ? (bossWarning ? FishRuntimeState.BossWarning : FishRuntimeState.BossActive)
                : (hasEnteredVisibleView ? FishRuntimeState.Swimming : FishRuntimeState.Entering);
        }
    }

    private void HandleRouteComplete()
    {
        if (routeEndsOutside)
        {
            if (IsFullyOutsidePaddedView())
            {
                CompleteOutsideExit();
                return;
            }

            BuildExitRoute(
                bossEscaping
                    ? FishRuntimeState.BossEscaping
                    : runtimeState == FishRuntimeState.LevelTransitionExit
                        ? FishRuntimeState.LevelTransitionExit
                        : FishRuntimeState.Exiting,
                bossEscaping ? scheduledEscapeSpeedMultiplier : 1f
            );
            return;
        }

        if (bossMode)
        {
            BuildBossPassRoute(bossWarning);
        }
        else
        {
            BuildNormalRoute(profile != null
                ? profile.SelectRoute(false)
                : FishRoutePattern.WideCurvedRoute);
        }
    }

    private void CompleteOutsideExit()
    {
        if (bossEscaping || runtimeState == FishRuntimeState.BossEscaping)
        {
            runtimeState = FishRuntimeState.ReturningToPool;
            if (owner != null)
            {
                owner.CompleteBossTimeoutEscape();
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (runtimeState == FishRuntimeState.LevelTransitionExit)
        {
            runtimeState = FishRuntimeState.ReturningToPool;
            gameObject.SetActive(false);
            return;
        }

        if (bossMode)
        {
            runtimeState = FishRuntimeState.WaitingOffScreen;
            offscreenWaitUntil = Time.time + GetBossReturnDelay();
            body.velocity = Vector2.zero;
            return;
        }

        bool shouldReturn = profile != null &&
            Random.value <= Mathf.Clamp01(profile.returnChance);

        if (shouldReturn)
        {
            runtimeState = FishRuntimeState.WaitingOffScreen;
            offscreenWaitUntil = Time.time + RandomRange(
                profile.offScreenWaitingTimeRange,
                2.5f,
                0f
            );
            body.velocity = Vector2.zero;
        }
        else
        {
            runtimeState = FishRuntimeState.ReturningToPool;
            gameObject.SetActive(false);
        }
    }

    private void BuildNormalRoute(FishRoutePattern routePattern)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        currentRoutePattern = routePattern;
        routeEndsOutside = true;
        scriptedRoute = false;
        ClearRoute();
        routeNoiseSeed = Random.Range(0f, 1000f);

        FishScreenSide entry = FishScreenBounds.GetNearestSide(targetCamera, transform.position);
        FishScreenSide exit = SelectExitSide(entry, routePattern);
        lastEntrySide = entry;
        plannedExitSide = exit;

        float entryLane = GetCurrentLane(entry);
        float exitLane = Mathf.Clamp01(entryLane + Random.Range(-0.35f, 0.35f));
        bool forceCenter = routePattern == FishRoutePattern.CenterCrossing ||
            (profile != null && Random.value <= profile.centerCrossingChance);

        if (forceCenter)
        {
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.42f, 0.58f),
                Random.Range(0.38f, 0.62f)
            ));
        }
        else if (routePattern == FishRoutePattern.WideCurvedRoute ||
                 routePattern == FishRoutePattern.SShapedRoute ||
                 routePattern == FishRoutePattern.LoopingRoute)
        {
            AddCurvedInteriorPoints(entry, exit, entryLane, exitLane, routePattern);
        }
        else if (routePattern == FishRoutePattern.EdgeRoute)
        {
            AddRoutePoint(GetEdgeInteriorPoint(entry, entryLane));
            AddRoutePoint(GetEdgeInteriorPoint(exit, exitLane));
        }
        else if (routePattern == FishRoutePattern.PlayerApproachRoute)
        {
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.34f, 0.66f),
                Random.Range(0.28f, 0.72f)
            ));
        }
        else
        {
            AddRoutePoint(GetDiagonalInteriorPoint(entry, entryLane, 0.35f));
            AddRoutePoint(GetDiagonalInteriorPoint(exit, exitLane, 0.68f));
        }

        AddRoutePoint(GetOutsidePoint(exit, exitLane, 0.20f));
        routePointIndex = 0;
        runtimeState = hasEnteredVisibleView
            ? FishRuntimeState.Swimming
            : FishRuntimeState.Entering;
        routeStartedAt = Time.time;
        SelectNewCruiseSpeed();
    }

    private void BuildBossPassRoute(bool forceCenter)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        bossMode = true;
        routeEndsOutside = true;
        scriptedRoute = false;
        ClearRoute();
        routeNoiseSeed = Random.Range(0f, 1000f);

        FishScreenSide entry = FishScreenBounds.GetNearestSide(targetCamera, transform.position);
        FishScreenSide exit = SelectBossExitSide(entry);
        lastEntrySide = entry;
        plannedExitSide = exit;
        float entryLane = GetCurrentLane(entry);
        float exitLane = Mathf.Clamp01(Random.Range(0.14f, 0.86f));

        bool useCenter = forceCenter || Random.value <= GetBossCenterChance();
        bool useWide = Random.value <= GetBossWideRouteChance();
        bool useLoop = Random.value <= GetBossLoopRouteChance();
        bool enterNearPlayer = Random.value <= GetBossEnterNearPlayerChance();
        bool enterAndRetreat = Random.value <= GetBossEnterRetreatChance();

        if (enterAndRetreat)
        {
            currentRoutePattern = FishRoutePattern.EnterAndRetreatRoute;

            if (profile != null && profile.bossRetreatOutsideOnly)
            {
                // Long-bodied bosses must never perform the classic same-edge
                // 180-degree retreat while visible. Continue forward through
                // the arena, exit completely, then the existing off-screen
                // return corridor can reposition the boss while players cannot
                // see the large turn.
                exit = FishScreenBounds.Opposite(entry);
                exitLane = Mathf.Clamp01(
                    entryLane + Random.Range(-0.20f, 0.20f)
                );
                plannedExitSide = exit;

                AddRoutePoint(GetDiagonalInteriorPoint(
                    entry,
                    entryLane,
                    0.28f
                ));

                if (useCenter)
                {
                    AddRoutePoint(GetViewportPoint(
                        Random.Range(0.43f, 0.57f),
                        Mathf.Clamp(
                            Mathf.Lerp(entryLane, exitLane, 0.5f) +
                            Random.Range(-0.08f, 0.08f),
                            0.24f,
                            0.76f
                        )
                    ));
                }

                AddRoutePoint(GetDiagonalInteriorPoint(
                    exit,
                    exitLane,
                    0.74f
                ));
            }
            else
            {
                AddRoutePoint(GetDiagonalInteriorPoint(entry, entryLane, 0.30f));
                AddRoutePoint(GetViewportPoint(
                    Mathf.Clamp(Random.Range(0.30f, 0.70f), 0.20f, 0.80f),
                    Mathf.Clamp(Random.Range(0.22f, 0.78f), 0.18f, 0.82f)
                ));
                exit = entry;
                plannedExitSide = exit;
            }
        }
        else if (useLoop)
        {
            currentRoutePattern = FishRoutePattern.LoopingRoute;
            AddCurvedInteriorPoints(
                entry,
                exit,
                entryLane,
                exitLane,
                FishRoutePattern.LoopingRoute
            );
            if (useCenter && routePointCount < MaximumRoutePoints - 1)
            {
                InsertCenterPointNearMiddle();
            }
        }
        else if (useWide)
        {
            currentRoutePattern = FishRoutePattern.WideCurvedRoute;
            AddCurvedInteriorPoints(entry, exit, entryLane, exitLane,
                FishRoutePattern.WideCurvedRoute);

            if (useCenter && routePointCount < MaximumRoutePoints - 1)
            {
                InsertCenterPointNearMiddle();
            }
        }
        else if (enterNearPlayer)
        {
            currentRoutePattern = FishRoutePattern.PlayerApproachRoute;
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.35f, 0.65f),
                Random.Range(0.30f, 0.70f)
            ));
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.43f, 0.57f),
                Random.Range(0.38f, 0.62f)
            ));
            AddRoutePoint(GetDiagonalInteriorPoint(exit, exitLane, 0.74f));
        }
        else if (useCenter)
        {
            currentRoutePattern = FishRoutePattern.CenterCrossing;
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.38f, 0.62f),
                Random.Range(0.34f, 0.66f)
            ));
            AddRoutePoint(GetDiagonalInteriorPoint(exit, exitLane, 0.72f));
        }
        else
        {
            currentRoutePattern = FishRoutePattern.DiagonalCrossing;
            AddRoutePoint(GetDiagonalInteriorPoint(entry, entryLane, 0.32f));
            AddRoutePoint(GetDiagonalInteriorPoint(exit, exitLane, 0.70f));
        }

        AddRoutePoint(GetOutsidePoint(exit, exitLane, GetBossOutsideDistance()));
        routePointIndex = 0;
        runtimeState = bossWarning
            ? FishRuntimeState.BossWarning
            : hasEnteredVisibleView
                ? FishRuntimeState.BossActive
                : FishRuntimeState.BossIntro;
        routeStartedAt = Time.time;
        SelectNewCruiseSpeed();
    }

    private void BuildReturnRouteFromOutside()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        FishScreenSide currentSide = FishScreenBounds.GetNearestSide(
            targetCamera,
            transform.position
        );
        FishScreenSide entrySide = SelectDifferentSide(currentSide);
        float lane = Random.Range(0.12f, 0.88f);
        ClearRoute();

        // Move around the outside corridor first. The object never teleports.
        AddRoutePoint(GetOutsidePoint(entrySide, lane, bossMode ? 0.65f : 0.35f));
        AddRoutePoint(GetEdgeInteriorPoint(entrySide, lane));
        routePointIndex = 0;
        routeEndsOutside = false;
        runtimeState = bossMode ? FishRuntimeState.BossActive : FishRuntimeState.Entering;
        routeStartedAt = Time.time;
        SelectNewCruiseSpeed();

        if (bossMode)
        {
            if (profile != null && profile.bossRetreatOutsideOnly)
            {
                // Re-enter on a broad pass and continue to the opposite side.
                // Any large heading correction happened in the outside corridor.
                FishScreenSide exit = FishScreenBounds.Opposite(entrySide);
                float exitLane = Mathf.Clamp01(
                    lane + Random.Range(-0.20f, 0.20f)
                );

                if (profile.bossUseLongBodySteering)
                {
                    // Crystal Whale should visibly cross the playable center
                    // instead of tracing the outer border after every return.
                    AddRoutePoint(GetViewportPoint(
                        Random.Range(0.44f, 0.56f),
                        Random.Range(0.38f, 0.62f)
                    ));
                }

                AddRoutePoint(GetDiagonalInteriorPoint(
                    exit,
                    exitLane,
                    0.72f
                ));
                AddRoutePoint(GetOutsidePoint(
                    exit,
                    exitLane,
                    GetBossOutsideDistance()
                ));
                routeEndsOutside = true;
                plannedExitSide = exit;
            }
            else
            {
                AddRoutePoint(GetViewportPoint(
                    Random.Range(0.28f, 0.72f),
                    Random.Range(0.22f, 0.78f)
                ));
                FishScreenSide exit = SelectBossExitSide(entrySide);
                float exitLane = Random.Range(0.12f, 0.88f);
                AddRoutePoint(GetOutsidePoint(exit, exitLane, GetBossOutsideDistance()));
                routeEndsOutside = true;
                plannedExitSide = exit;
            }
        }
        else
        {
            FishScreenSide exit = SelectExitSide(entrySide, currentRoutePattern);
            float exitLane = Random.Range(0.10f, 0.90f);
            AddRoutePoint(GetViewportPoint(
                Random.Range(0.34f, 0.66f),
                Random.Range(0.28f, 0.72f)
            ));
            AddRoutePoint(GetOutsidePoint(exit, exitLane, 0.20f));
            routeEndsOutside = true;
            plannedExitSide = exit;
        }
    }

    private void BuildExitRoute(FishRuntimeState exitState, float speedMultiplier)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        scheduledEscapeSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        FishScreenSide exitSide = ChooseNaturalExitSide();
        float lane = GetCurrentLane(exitSide);
        ClearRoute();

        Vector3 viewport = targetCamera != null
            ? targetCamera.WorldToViewportPoint(transform.position)
            : new Vector3(0.5f, 0.5f, 0f);

        // Preserve current direction where possible so a transition does not
        // make every fish turn around at the same instant.
        if (body != null && body.velocity.sqrMagnitude > 0.04f)
        {
            FishScreenSide headingSide = DirectionToSide(body.velocity.normalized);
            if (IsReasonableExitSide(viewport, headingSide))
            {
                exitSide = headingSide;
                lane = GetCurrentLane(exitSide);
            }
        }

        AddRoutePoint(GetOutsidePoint(
            exitSide,
            Mathf.Clamp01(lane + Random.Range(-0.10f, 0.10f)),
            bossMode ? GetBossOutsideDistance() : 0.25f
        ));
        routePointIndex = 0;
        routeEndsOutside = true;
        runtimeState = exitState;
        bossEscaping = exitState == FishRuntimeState.BossEscaping;
        routeStartedAt = Time.time;
        SelectNewCruiseSpeed();
    }

    private void AddCurvedInteriorPoints(
        FishScreenSide entry,
        FishScreenSide exit,
        float entryLane,
        float exitLane,
        FishRoutePattern pattern
    )
    {
        Vector3 first = GetDiagonalInteriorPoint(entry, entryLane, 0.28f);
        Vector3 second = GetDiagonalInteriorPoint(exit, exitLane, 0.72f);
        Vector2 direction = second - first;
        Vector2 perpendicular = Rotate(direction.normalized, 90f);
        float worldWidth = GetWorldWidth();
        float curve = profile != null ? profile.curveStrength : 0.35f;
        float amplitude = worldWidth * Mathf.Lerp(0.035f, 0.14f, curve);

        if (pattern == FishRoutePattern.SShapedRoute)
        {
            AddRoutePoint(first + (Vector3)(perpendicular * amplitude));
            AddRoutePoint(Vector3.Lerp(first, second, 0.52f) -
                          (Vector3)(perpendicular * amplitude));
            AddRoutePoint(second + (Vector3)(perpendicular * amplitude * 0.45f));
        }
        else if (pattern == FishRoutePattern.LoopingRoute)
        {
            Vector3 center = GetViewportPoint(
                Random.Range(0.38f, 0.62f),
                Random.Range(0.32f, 0.68f)
            );
            float radius = worldWidth * 0.08f;
            AddRoutePoint(center + Vector3.up * radius);
            AddRoutePoint(center + Vector3.right * radius);
            AddRoutePoint(center + Vector3.down * radius);
            AddRoutePoint(center + Vector3.left * radius);
            AddRoutePoint(second);
        }
        else
        {
            AddRoutePoint(first + (Vector3)(perpendicular * amplitude));
            AddRoutePoint(Vector3.Lerp(first, second, 0.55f) +
                          (Vector3)(perpendicular * amplitude * 1.25f));
            AddRoutePoint(second - (Vector3)(perpendicular * amplitude * 0.35f));
        }
    }

    private void InsertCenterPointNearMiddle()
    {
        if (routePointCount >= MaximumRoutePoints)
        {
            return;
        }

        Vector3 center = GetViewportPoint(
            Random.Range(0.42f, 0.58f),
            Random.Range(0.38f, 0.62f)
        );
        int insertIndex = Mathf.Clamp(routePointCount / 2, 0, routePointCount);

        for (int i = routePointCount; i > insertIndex; i--)
        {
            routePoints[i] = routePoints[i - 1];
        }

        routePoints[insertIndex] = center;
        routePointCount++;
    }

    private void UpdateSpacingCorrection()
    {
        UpdateSpacingCorrection(false);
    }

    private void UpdateSpacingCorrection(bool schooling)
    {
        if (bossMode && profile != null && profile.bossUseLongBodySteering)
        {
            // A long boss owns its lane. Smaller fish should flow around it;
            // steering the whale away from every neighbour creates the exact
            // quick zig-zag motion this profile is designed to avoid.
            spacingCorrection = Vector2.zero;
            return;
        }

        if (Time.time < nextSpacingCheckTime)
        {
            spacingCorrection = Vector2.Lerp(
                spacingCorrection,
                Vector2.zero,
                0.08f
            );
            return;
        }

        float interval = profile != null
            ? Mathf.Max(0.05f, profile.overlapCheckInterval)
            : 0.32f;
        interval = Mathf.Clamp(interval, 0.08f, 0.35f);
        nextSpacingCheckTime = Time.time + interval + Random.Range(0f, interval * 0.25f);

        Vector2 separation = Vector2.zero;
        int neighborCount = 0;
        float ownRadius = SpacingRadius;

        for (int i = 0; i < ActiveAgentsInternal.Count; i++)
        {
            FishMotionAgent other = ActiveAgentsInternal[i];
            if (other == null || other == this ||
                !other.gameObject.activeInHierarchy ||
                other.runtimeState == FishRuntimeState.Dying ||
                other.runtimeState == FishRuntimeState.ReturningToPool)
            {
                continue;
            }

            bool sameSchool = followTarget != null &&
                (other.followTarget == followTarget || other.transform == followTarget);
            float schoolMultiplier = sameSchool || schooling
                ? GetSchoolingSpacingMultiplier()
                : 1f;
            float minimumDistance =
                (ownRadius + other.SpacingRadius) *
                PairSpacingFactor *
                schoolMultiplier;
            Vector2 delta = (Vector2)transform.position -
                (Vector2)other.transform.position;
            float sqrDistance = delta.sqrMagnitude;
            float minimumSqr = minimumDistance * minimumDistance;

            if (sqrDistance >= minimumSqr)
            {
                continue;
            }

            // Two pooled fish can occasionally begin at the exact same
            // coordinate. Never skip that case: assign a stable opposite
            // direction so they separate smoothly on the next movement tick.
            if (sqrDistance <= 0.0001f)
            {
                int hash = GetInstanceID() ^ (other.GetInstanceID() * 397);
                float angle = Mathf.Abs(hash % 360) * Mathf.Deg2Rad;
                delta = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.02f;
                sqrDistance = delta.sqrMagnitude;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            float strength = 1f - distance / Mathf.Max(0.01f, minimumDistance);
            separation += delta / distance * strength;
            neighborCount++;
        }

        if (neighborCount > 0)
        {
            separation /= neighborCount;
            float strength = profile != null
                ? profile.routeAdjustmentStrength
                : 0.40f;
            spacingCorrection = Vector2.ClampMagnitude(
                separation * strength * 1.35f,
                MaximumSpacingCorrection
            );
        }
        else
        {
            spacingCorrection = Vector2.zero;
        }
    }

    private void CheckForStuckFish()
    {
        float interval = legacyMovementProfile != null
            ? Mathf.Max(0.25f, legacyMovementProfile.stuckCheckInterval)
            : 1.2f;

        if (Time.time < lastProgressCheckTime + interval)
        {
            return;
        }

        float travelled = Vector2.Distance(transform.position, lastProgressPosition);
        float minimum = legacyMovementProfile != null
            ? Mathf.Max(0.01f, legacyMovementProfile.minimumStuckTravelDistance)
            : 0.08f;

        if (travelled < minimum && routePointCount > 0)
        {
            Vector2 direction = (routePoints[routePointIndex] - transform.position).normalized;

            if (bossMode && profile != null && profile.bossUseLongBodySteering)
            {
                Vector2 recoveryHeading = body.velocity.sqrMagnitude > 0.01f
                    ? body.velocity.normalized
                    : direction;
                body.velocity = recoveryHeading * Mathf.Max(
                    0.55f,
                    currentCruiseSpeed * 0.82f
                );
                spacingCorrection = Vector2.zero;
            }
            else
            {
                body.velocity = direction * Mathf.Max(0.4f, currentCruiseSpeed * 0.75f);
                spacingCorrection = Rotate(
                    direction,
                    Random.value < 0.5f ? 30f : -30f
                ) * 0.25f;
            }

            if (Time.time >= nextRateLimitedLogTime)
            {
                Debug.LogWarning(
                    "[FishMotionAgent] Recovered a stuck fish: " + gameObject.name,
                    this
                );
                nextRateLimitedLogTime = Time.time + 8f;
            }
        }

        lastProgressPosition = transform.position;
        lastProgressCheckTime = Time.time;
    }

    private void SelectNewCruiseSpeed()
    {
        if (profile != null)
        {
            float runtimeScale = owner != null
                ? owner.GetProfessionalRuntimeSpeedMultiplier()
                : 1f;

            currentCruiseSpeed = Mathf.Max(
                0.05f,
                profile.GetRandomSpeed(bossMode) * runtimeScale
            );
        }
        else
        {
            currentCruiseSpeed = Mathf.Max(
                0.05f,
                owner != null ? owner.MoveSpeed : 1.5f
            );
        }

        if (bossMode && profile != null)
        {
            nextSpeedChangeTime = Time.time + RandomRange(
                profile.bossSpeedChangeIntervalRange,
                2.4f,
                0.20f
            );
        }
        else
        {
            nextSpeedChangeTime = Time.time + Random.Range(1.4f, 3.6f);
        }

        if (profile != null && Random.value <= profile.burstSpeedChance)
        {
            burstUntil = Time.time + Random.Range(0.25f, 0.75f);
        }
        else
        {
            burstUntil = 0f;
        }
    }

    private void UpdateCruiseSpeed()
    {
        if (Time.time < nextSpeedChangeTime)
        {
            return;
        }

        SelectNewCruiseSpeed();
    }

    private bool ShouldPauseAtWaypoint()
    {
        if (bossEscaping || runtimeState == FishRuntimeState.LevelTransitionExit ||
            runtimeState == FishRuntimeState.BossEscaping)
        {
            return false;
        }

        float chance = bossMode
            ? profile != null
                ? profile.bossPauseChance
                : 0.22f
            : profile != null
                ? profile.pauseChance
                : 0.05f;

        chance = Mathf.Clamp01(chance);
        if (chance <= 0.0001f)
        {
            return false;
        }

        return Random.value < chance;
    }

    private float GetPauseDuration()
    {
        if (bossMode)
        {
            return profile != null
                ? RandomRange(profile.bossPauseDurationRange, 0.5f, 0f)
                : Random.Range(0.15f, 0.8f);
        }

        return profile != null
            ? RandomRange(profile.pauseDurationRange, 0.3f, 0f)
            : Random.Range(0.10f, 0.45f);
    }

    private float GetBossReturnDelay()
    {
        if (bossWarning)
        {
            return profile != null
                ? Mathf.Min(1.2f, RandomRange(profile.bossReturnDelayRange, 1f, 0f))
                : 0.8f;
        }

        return profile != null
            ? RandomRange(profile.bossReturnDelayRange, 2.5f, 0f)
            : Random.Range(1.5f, 4f);
    }

    private float GetWobbleAngle()
    {
        float amount = profile != null ? profile.wobbleAmount : 0.08f;
        float frequency = profile != null ? profile.wobbleFrequency : 0.7f;
        return (Mathf.PerlinNoise(routeNoiseSeed, Time.time * frequency) * 2f - 1f) *
            amount * 20f;
    }

    private float GetWaypointReachDistance()
    {
        float visual = Mathf.Max(0.15f, GetVisualRadius() * 0.35f);
        if (legacyMovementProfile != null)
        {
            visual = Mathf.Max(visual, legacyMovementProfile.waypointReachDistance);
        }
        return visual;
    }

    private float GetViewportPadding()
    {
        float configured = profile != null
            ? profile.screenEdgePadding
            : legacyMovementProfile != null
                ? legacyMovementProfile.offscreenViewportPadding
                : FishScreenBounds.MinimumViewportPadding;
        return Mathf.Max(FishScreenBounds.MinimumViewportPadding, configured);
    }

    private float GetVisualRadius()
    {
        return Mathf.Max(0.15f, cachedVisualRadius);
    }

    private float GetBossOutsideDistance()
    {
        float routeMultiplier = profile != null
            ? RandomRange(profile.bossRouteDistanceRange, 1.5f, 0.25f)
            : 1.5f;
        return GetVisualRadius() + GetWorldWidth() * 0.05f * routeMultiplier;
    }

    private Vector3 GetOutsidePoint(
        FishScreenSide side,
        float lane,
        float extraDistance
    )
    {
        return FishScreenBounds.GetEdgePoint(
            targetCamera,
            side,
            lane,
            GetViewportPadding(),
            GetVisualRadius() + Mathf.Max(0f, extraDistance),
            transform.position.z
        );
    }

    private Vector3 GetViewportPoint(float x, float y)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return transform.position + transform.right * 4f;
        }

        float depth = Mathf.Abs(transform.position.z - targetCamera.transform.position.z);
        Vector3 world = targetCamera.ViewportToWorldPoint(
            new Vector3(Mathf.Clamp01(x), Mathf.Clamp01(y), depth)
        );
        world.z = transform.position.z;
        return world;
    }

    private Vector3 GetEdgeInteriorPoint(FishScreenSide side, float lane)
    {
        switch (side)
        {
            case FishScreenSide.Left: return GetViewportPoint(0.14f, lane);
            case FishScreenSide.Right: return GetViewportPoint(0.86f, lane);
            case FishScreenSide.Top: return GetViewportPoint(lane, 0.86f);
            default: return GetViewportPoint(lane, 0.14f);
        }
    }

    private Vector3 GetDiagonalInteriorPoint(
        FishScreenSide side,
        float lane,
        float progress
    )
    {
        float p = Mathf.Clamp01(progress);
        switch (side)
        {
            case FishScreenSide.Left: return GetViewportPoint(Mathf.Lerp(0.12f, 0.82f, p), lane);
            case FishScreenSide.Right: return GetViewportPoint(Mathf.Lerp(0.88f, 0.18f, p), lane);
            case FishScreenSide.Top: return GetViewportPoint(lane, Mathf.Lerp(0.88f, 0.18f, p));
            default: return GetViewportPoint(lane, Mathf.Lerp(0.12f, 0.82f, p));
        }
    }

    private float GetCurrentLane(FishScreenSide side)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return Random.Range(0.15f, 0.85f);
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(transform.position);
        return side == FishScreenSide.Left || side == FishScreenSide.Right
            ? Mathf.Clamp(viewport.y, 0.08f, 0.92f)
            : Mathf.Clamp(viewport.x, 0.08f, 0.92f);
    }

    private FishScreenSide SelectExitSide(FishScreenSide entry, FishRoutePattern route)
    {
        if (route == FishRoutePattern.EnterAndRetreatRoute)
        {
            return entry;
        }

        if (route == FishRoutePattern.DiagonalCrossing ||
            route == FishRoutePattern.SShapedRoute)
        {
            if (entry == FishScreenSide.Left || entry == FishScreenSide.Right)
            {
                return Random.value < 0.5f ? FishScreenSide.Top : FishScreenSide.Bottom;
            }

            return Random.value < 0.5f ? FishScreenSide.Left : FishScreenSide.Right;
        }

        return FishScreenBounds.Opposite(entry);
    }

    private FishScreenSide SelectBossExitSide(FishScreenSide entry)
    {
        if (profile != null && profile.bossUseLongBodySteering)
        {
            // Long-bodied bosses read best as full screen passes. Their broad
            // turning is handled outside the visible arena between passes.
            return FishScreenBounds.Opposite(entry);
        }

        if (Random.value < 0.62f)
        {
            return FishScreenBounds.Opposite(entry);
        }

        return SelectDifferentSide(entry);
    }

    private FishScreenSide SelectDifferentSide(FishScreenSide excluded)
    {
        FishScreenSide selected = excluded;
        for (int attempt = 0; attempt < 5 && selected == excluded; attempt++)
        {
            selected = (FishScreenSide)Random.Range(0, 4);
        }
        return selected == excluded ? FishScreenBounds.Opposite(excluded) : selected;
    }

    private FishScreenSide ChooseNaturalExitSide()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Vector3 viewport = targetCamera != null
            ? targetCamera.WorldToViewportPoint(transform.position)
            : new Vector3(0.5f, 0.5f, 0f);

        float left = viewport.x;
        float right = 1f - viewport.x;
        float bottom = viewport.y;
        float top = 1f - viewport.y;
        float minimum = Mathf.Min(left, right, bottom, top);

        if (minimum == left) return FishScreenSide.Left;
        if (minimum == right) return FishScreenSide.Right;
        if (minimum == top) return FishScreenSide.Top;
        return FishScreenSide.Bottom;
    }

    private static FishScreenSide DirectionToSide(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return direction.x >= 0f ? FishScreenSide.Right : FishScreenSide.Left;
        }

        return direction.y >= 0f ? FishScreenSide.Top : FishScreenSide.Bottom;
    }

    private static bool IsReasonableExitSide(Vector3 viewport, FishScreenSide side)
    {
        switch (side)
        {
            case FishScreenSide.Left: return viewport.x <= 0.75f;
            case FishScreenSide.Right: return viewport.x >= 0.25f;
            case FishScreenSide.Top: return viewport.y >= 0.25f;
            default: return viewport.y <= 0.75f;
        }
    }

    private bool IsPointOutsidePaddedView(Vector3 point, float extra)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return false;
        }

        Vector3 viewport = targetCamera.WorldToViewportPoint(point);
        float padding = GetViewportPadding() + Mathf.Max(0f, extra);
        return viewport.x < -padding || viewport.x > 1f + padding ||
               viewport.y < -padding || viewport.y > 1f + padding;
    }

    private void ClearRoute()
    {
        routePointCount = 0;
        routePointIndex = 0;
        routeEndsOutside = false;
    }

    private void AddRoutePoint(Vector3 point)
    {
        if (routePointCount >= MaximumRoutePoints)
        {
            return;
        }

        point.z = transform.position.z;
        routePoints[routePointCount++] = point;
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

        if (primaryRenderer == null)
        {
            primaryRenderer = GetComponent<SpriteRenderer>();
            if (primaryRenderer == null)
            {
                primaryRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (cachedVisualRenderers == null)
        {
            cachedVisualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (cachedVisualColliders == null)
        {
            cachedVisualColliders = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void RefreshCachedVisualRadius()
    {
        Bounds bounds = FishScreenBounds.GetCombinedVisualBounds(
            transform,
            cachedVisualRenderers,
            cachedVisualColliders
        );
        cachedVisualRadius = Mathf.Max(0.15f, bounds.extents.magnitude);
    }

    private void RegisterActiveAgent()
    {
        if (!ActiveAgentsInternal.Contains(this))
        {
            ActiveAgentsInternal.Add(this);
        }
    }

    private void UnregisterActiveAgent()
    {
        ActiveAgentsInternal.Remove(this);
    }

    private static FishRoutePattern MapStyleToRoute(FishScript.SwimStyle style)
    {
        switch (style)
        {
            case FishScript.SwimStyle.ArcSweep:
            case FishScript.SwimStyle.Curious:
                return FishRoutePattern.WideCurvedRoute;
            case FishScript.SwimStyle.ZigZagBurst:
            case FishScript.SwimStyle.CuteDarting:
            case FishScript.SwimStyle.BossCuteDarting:
                return FishRoutePattern.SShapedRoute;
            case FishScript.SwimStyle.SpiralCross:
            case FishScript.SwimStyle.FigureEight:
            case FishScript.SwimStyle.BossFigureEight:
            case FishScript.SwimStyle.OrbitCenter:
            case FishScript.SwimStyle.BossOrbit:
            case FishScript.SwimStyle.MiniBossOrbit:
                return FishRoutePattern.LoopingRoute;
            case FishScript.SwimStyle.VerticalDive:
                return FishRoutePattern.DiagonalCrossing;
            case FishScript.SwimStyle.SchoolFollow:
            case FishScript.SwimStyle.FollowLeader:
                return FishRoutePattern.GroupRoute;
            case FishScript.SwimStyle.MiniBossHunter:
            case FishScript.SwimStyle.MiniBossCharge:
            case FishScript.SwimStyle.BossPatrol:
            case FishScript.SwimStyle.BossCharge:
            case FishScript.SwimStyle.BossDash:
            case FishScript.SwimStyle.BossArena:
                return FishRoutePattern.PlayerApproachRoute;
            default:
                return FishRoutePattern.StraightCrossing;
        }
    }

    private static bool IsBossStyle(FishScript.SwimStyle style)
    {
        return style == FishScript.SwimStyle.BossPatrol ||
               style == FishScript.SwimStyle.BossCharge ||
               style == FishScript.SwimStyle.BossOrbit ||
               style == FishScript.SwimStyle.BossFigureEight ||
               style == FishScript.SwimStyle.BossDash ||
               style == FishScript.SwimStyle.BossArena ||
               style == FishScript.SwimStyle.BossCuteDarting ||
               style == FishScript.SwimStyle.BossWoundedConvulsion;
    }

    private float GetDefaultSpacingForTier()
    {
        if (owner == null)
        {
            return 0.65f;
        }

        switch (owner.GetFishTier())
        {
            case FishTier.Small: return 0.50f;
            case FishTier.Medium: return 0.80f;
            case FishTier.Special: return 1.00f;
            case FishTier.MiniBoss: return 1.50f;
            default: return 2.10f;
        }
    }

    private float GetSchoolingSpacingMultiplier()
    {
        float configured = profile != null
            ? profile.schoolingSpacingMultiplier
            : 0.78f;
        return Mathf.Clamp(
            Mathf.Max(MinimumSchoolSpacingMultiplier, configured),
            MinimumSchoolSpacingMultiplier,
            1f
        );
    }

    private float GetDefaultAcceleration()
    {
        if (owner == null)
        {
            return 3f;
        }

        switch (owner.GetFishTier())
        {
            case FishTier.Small: return 5f;
            case FishTier.Medium: return 3.5f;
            case FishTier.Special: return 4f;
            case FishTier.MiniBoss: return 2f;
            default: return 1.35f;
        }
    }

    private float GetDefaultTurnSpeed()
    {
        if (owner == null)
        {
            return 90f;
        }

        switch (owner.GetFishTier())
        {
            case FishTier.Small: return 135f;
            case FishTier.Medium: return 95f;
            case FishTier.Special: return 115f;
            case FishTier.MiniBoss: return 65f;
            default: return 50f;
        }
    }

    private float GetBossCenterChance()
    {
        float chance = profile != null
            ? profile.bossCrossCenterChance
            : 0.62f;
        return bossWarning ? Mathf.Max(chance, 0.85f) : chance;
    }

    private float GetBossWideRouteChance()
    {
        return profile != null ? profile.bossWideRouteChance : 0.58f;
    }

    private float GetBossEnterRetreatChance()
    {
        return profile != null ? profile.bossEnterAndRetreatChance : 0.18f;
    }

    private float GetBossLoopRouteChance()
    {
        return profile != null ? profile.bossLoopRouteChance : 0.18f;
    }

    private float GetBossEnterNearPlayerChance()
    {
        return profile != null ? profile.bossEnterNearPlayerChance : 0.28f;
    }

    private float GetWorldWidth()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || !targetCamera.orthographic)
        {
            return 16f;
        }

        return targetCamera.orthographicSize * 2f * targetCamera.aspect;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            vector.x * cosine - vector.y * sine,
            vector.x * sine + vector.y * cosine
        );
    }

    private static float RandomRange(Vector2 range, float fallback, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float high = Mathf.Max(low, Mathf.Max(range.x, range.y));
        if (high <= minimum)
        {
            return Mathf.Max(minimum, fallback);
        }
        return Random.Range(low, high);
    }
}

