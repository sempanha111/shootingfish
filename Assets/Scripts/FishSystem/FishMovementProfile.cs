using UnityEngine;

[CreateAssetMenu(
    menuName = "Fish Arcade/Fish Movement Profile",
    fileName = "FishMovementProfile"
)]
public class FishMovementProfile : ScriptableObject
{
    public WeightedSwimStyle[] healthyStyles;
    public WeightedSwimStyle[] aggressiveStyles;
    public WeightedSwimStyle[] criticalStyles;

    [Header("Professional Organic Steering")]
    [Tooltip("Uses acceleration-limited steering, wide waypoints, speed breathing, and stuck recovery instead of abrupt velocity changes.")]
    public bool enableOrganicSteering = true;

    [Tooltip("Lower values react faster. Values around 0.14-0.24 feel fluid without looking heavy.")]
    [Range(0.05f, 0.50f)]
    public float velocitySmoothTime = 0.18f;

    [Min(10f)] public float healthyTurnDegreesPerSecond = 78f;
    [Min(10f)] public float aggressiveTurnDegreesPerSecond = 102f;
    [Min(10f)] public float criticalTurnDegreesPerSecond = 122f;

    [Tooltip("Minimum distance between broad path targets, measured as a fraction of the visible camera width.")]
    [Range(0.20f, 0.90f)]
    public float minimumWaypointViewportDistance = 0.48f;

    [Min(0.10f)]
    public float waypointReachDistance = 0.48f;

    public Vector2 waypointLifetimeRange = new Vector2(3.8f, 7.2f);

    [Tooltip("Small Perlin-noise steering angle layered over the broad route.")]
    [Range(0f, 28f)]
    public float wanderAngleDegrees = 9f;

    [Min(0.02f)]
    public float wanderFrequency = 0.32f;

    [Tooltip("Smooth slow-fast breathing applied to cruise speed.")]
    public Vector2 speedPulseMultiplierRange = new Vector2(0.90f, 1.10f);

    public Vector2 speedPulseDurationRange = new Vector2(2.8f, 5.4f);

    [Tooltip("How long a selected style is kept before another style can be considered. A health-phase change may switch immediately.")]
    public Vector2 styleHoldDurationRange = new Vector2(4.5f, 8f);

    [Range(0f, 1f)]
    public float styleChangeChanceAfterHold = 0.45f;

    [Header("Balanced Screen Traffic")]
    [Tooltip("Keeps most long routes in separated upper/lower traffic bands so fish do not repeatedly stack in the viewport center.")]
    public bool spreadRoutesAcrossViewport = true;

    [Tooltip("Half-height of the soft no-target band around viewport Y = 0.5. Fish may cross it naturally, but do not repeatedly select it as a destination.")]
    [Range(0.05f, 0.28f)]
    public float centerAvoidanceHalfHeight = 0.14f;

    [Tooltip("Small normalized vertical variation inside a traffic band. Keep this below the center avoidance size.")]
    [Range(0f, 0.16f)]
    public float trafficLaneJitter = 0.055f;

    [Tooltip("Chance to change between the upper and lower half at a screen edge. Low values keep center crossings occasional instead of constant.")]
    [Range(0f, 0.35f)]
    public float crossCenterLaneChangeChance = 0.08f;

    [Tooltip("Horizontal anchor used by orbit-like movement. Off-center anchors prevent several orbiting fish from collecting around the same middle point.")]
    [Range(0.16f, 0.38f)]
    public float orbitAnchorViewportInset = 0.27f;

    [Header("Natural Off-Screen Loop")]
    [Range(0f, 1f)]
    public float offscreenLoopChance = 0.20f;

    [Range(0, 3)]
    public int maximumOffscreenLoops = 1;

    [Range(0.03f, 0.30f)]
    public float offscreenViewportPadding = 0.10f;

    [Min(2f)]
    public float offscreenReturnTimeout = 9f;

    [Min(1f)]
    public float loopCooldown = 7f;

    [Tooltip("Ordinary fish choose a final natural exit after this amount of active time. Boss presence rules remain authoritative for bosses.")]
    public Vector2 naturalLifetimeRange = new Vector2(16f, 28f);

    [Header("Anti-Stuck Recovery")]
    [Min(0.25f)]
    public float stuckCheckInterval = 1.15f;

    [Min(0.01f)]
    public float minimumStuckTravelDistance = 0.10f;

    [Range(0.25f, 2f)]
    public float stuckRecoverySpeedMultiplier = 0.85f;

    [Header("Fire-Reactive Micro Convulsion")]
    public bool enableReactiveHitMotion = true;

    [Range(0f, 1f)]
    public float microConvulsionChancePerHit = 0.32f;

    [Range(0f, 1f)]
    public float repeatedMicroConvulsionChance = 0.68f;

    [Range(1, 8)]
    public int sustainedFireHitCount = 2;

    [Min(0.05f)]
    public float sustainedFireWindow = 0.55f;

    [Tooltip("No new hit for this long means firing stopped. The visual reaction is restored immediately when this timer expires.")]
    [Min(0.05f)]
    public float fireReleaseGracePeriod = 0.34f;

    public Vector2 microConvulsionDurationRange = new Vector2(0.055f, 0.13f);
    public Vector2 microConvulsionRecoveryRange = new Vector2(0.07f, 0.18f);

    [Range(0f, 0.20f)]
    public float microConvulsionScaleStrength = 0.045f;

    [Range(0.15f, 1f)]
    public float reactionMovementSpeedMultiplier = 0.78f;

    [Tooltip("Optional trigger used for each brief flinch. Missing Animator parameters are ignored safely.")]
    public string microConvulsionAnimatorTrigger = "HitFlinch";

    [Tooltip("Optional Animator bool held only while fire pressure is active.")]
    public string sustainedFireAnimatorBool = "UnderFire";

    public FishScript.SwimStyle Select(
        FishHealthPhase phase,
        FishScript.SwimStyle previous,
        FishTier tier
    )
    {
        WeightedSwimStyle[] source;

        if (phase == FishHealthPhase.Healthy)
        {
            source = healthyStyles;
        }
        else if (phase == FishHealthPhase.Aggressive)
        {
            source = aggressiveStyles;
        }
        else
        {
            source = criticalStyles;
        }

        if (source == null || source.Length == 0)
        {
            return FishScript.GetDefaultMovementForTier(tier, phase);
        }

        float totalWeight = 0f;
        int validCount = 0;

        for (int i = 0; i < source.Length; i++)
        {
            WeightedSwimStyle entry = source[i];

            if (entry.weight <= 0f ||
                !FishScript.IsStyleAllowedForTier(entry.style, tier))
            {
                continue;
            }

            float weight = entry.weight;

            if (entry.style == previous && source.Length > 1)
            {
                weight *= 0.08f;
            }

            totalWeight += weight;
            validCount++;
        }

        if (validCount == 0 || totalWeight <= 0f)
        {
            return FishScript.GetDefaultMovementForTier(tier, phase);
        }

        float randomValue = Random.value * totalWeight;

        for (int i = 0; i < source.Length; i++)
        {
            WeightedSwimStyle entry = source[i];

            if (entry.weight <= 0f ||
                !FishScript.IsStyleAllowedForTier(entry.style, tier))
            {
                continue;
            }

            float weight = entry.weight;

            if (entry.style == previous && source.Length > 1)
            {
                weight *= 0.08f;
            }

            randomValue -= weight;

            if (randomValue <= 0f)
            {
                return entry.style;
            }
        }

        return FishScript.GetDefaultMovementForTier(tier, phase);
    }
}
