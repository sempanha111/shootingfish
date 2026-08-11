using System;
using UnityEngine;

public enum CinematicRouteMode
{
    Automatic,
    ManualViewportPoints,
    Mixed
}

public enum CinematicTargetType
{
    CustomViewport,
    ScreenCenter,
    RandomViewport,
    DensestFishArea,
    FinalAttacker
}

public enum CinematicCertificateTiming
{
    Disabled,
    RouteStart,
    AfterRoutePoint,
    BeforeFinalCharge,
    FinalExplosion
}

public enum CinematicRewardTiming
{
    FinalExplosion,
    SequenceEnd
}

public enum CinematicChargePresentationOrder
{
    Simultaneous,
    NetBoomThenChargeEffects
}

[Serializable]
public sealed class CinematicRoutePoint
{
    public CinematicTargetType targetType =
        CinematicTargetType.CustomViewport;

    [Tooltip("Normalized camera position. X/Y values from 0 to 1 work on every resolution and aspect ratio.")]
    public Vector2 viewportPosition = new Vector2(0.82f, 0.82f);

    [Min(0.1f)]
    public float speedMultiplier = 1f;

    [Min(0f)]
    public float pauseDuration = 0.14f;

    public bool triggerArrivalExplosion = true;
}

[Serializable]
public sealed class CinematicAreaDamageSettings
{
    public bool enabled = true;

    [Min(0f)]
    public float damage = 850f;

    [Min(0.1f)]
    public float radius = 2.4f;

    public bool useDistanceFalloff = true;

    [Range(0f, 1f)]
    public float minimumFalloffMultiplier = 0.35f;

    [Range(1, 64)]
    public int maximumTargets = 12;

    public LayerMask fishLayers = ~0;

    [Header("Tier Multipliers")]
    [Min(0f)] public float smallMultiplier = 1.20f;
    [Min(0f)] public float mediumMultiplier = 0.58f;
    [Min(0f)] public float specialMultiplier = 0.38f;
    [Min(0f)] public float miniBossMultiplier = 0.18f;
    [Min(0f)] public float mainBossMultiplier = 0.05f;

    public bool mainBossImmune = true;
    public bool specialFishImmune;

    [Header("Optional Non-Lethal Reaction")]
    public bool requestReaction = true;

    [Min(0.02f)]
    public float reactionDuration = 0.16f;

    [Range(0f, 0.35f)]
    public float reactionScaleStrength = 0.10f;

    [Range(0f, 360f)]
    public float reactionRotationDegrees = 42f;

    [Range(0f, 1f)]
    public float reactionRedStrength = 0.70f;
}

[CreateAssetMenu(
    menuName = "Fish Arcade/Special Fish Cinematic Death Profile",
    fileName = "SpecialFishCinematicDeathProfile"
)]
public sealed class SpecialFishCinematicDeathProfile : ScriptableObject
{
    [Header("Identity and Core Rules")]
    public string displayName = "Armored Crab";

    [Tooltip("0.25 means the fish receives 25% less damage while alive.")]
    [Range(0f, 0.90f)]
    public float damageResistance = 0.25f;

    [Tooltip("Runs cinematic timing in real time so optional slow motion does not stall the sequence.")]
    public bool useUnscaledTime = true;

    public bool disableCollidersDuringSequence = true;

    [Tooltip("Disabled by default. When enabled, the existing GameManager.SpawnNet background is also played at major cinematic impacts.")]
    public bool allowNormalNetBackgroundEffect;

    public CinematicRewardTiming rewardTiming =
        CinematicRewardTiming.FinalExplosion;

    [Header("Stage 1 - Convulsion and Final Breath")]
    public bool enableConvulsion = true;

    [Min(0.05f)]
    public float convulsionDuration = 0.62f;

    [Min(0f)]
    public float convulsionPositionStrength = 0.085f;

    [Range(0f, 45f)]
    public float convulsionRotationDegrees = 12f;

    [Range(0f, 0.35f)]
    public float convulsionScaleStrength = 0.085f;

    [Range(0f, 1f)]
    public float convulsionRedStrength = 0.48f;

    [Min(0.1f)]
    public float convulsionFrequency = 18f;

    public string convulsionAnimatorTrigger = "ArmorBreak";
    public AudioClip convulsionSound;
    public GameObject convulsionEffectPrefab;

    [Min(0f)]
    public float convulsionEffectVisibleDuration;

    [Header("Stage 2 - Main Charge Motion")]
    public bool enableMainCharge = true;

    [Min(0.05f)]
    public float mainChargeDuration = 1.25f;

    [Min(0.1f)]
    public float mainChargeScale = 1.48f;

    [Range(0f, 0.30f)]
    public float mainChargePulseStrength = 0.075f;

    [Tooltip("Pulse frequency used during both Stage 2A (Net Boom prelude) and Stage 2B (main charge).")]
    [Min(0.1f)]
    public float mainChargePulseFrequency = 8f;

    [Min(0f)]
    public float mainChargeRotationSpeed = 95f;

    [Range(0f, 1f)]
    public float mainChargeRedStrength = 0.72f;

    public string mainDeathAnimatorTrigger = "Death";
    public AudioClip mainChargeSound;

    [Header("Stage 2A - Net Boom Prelude")]
    [Tooltip("Simultaneous starts all Stage 2 presentation effects together. Net Boom Then Charge Effects starts the complete main charge motion immediately, plays the Net Boom during Stage 2A, then adds the charge prefabs and earthquake in Stage 2B without restarting the charge motion.")]
    public CinematicChargePresentationOrder chargePresentationOrder =
        CinematicChargePresentationOrder.NetBoomThenChargeEffects;

    public bool playNetBoomAtCharge = true;

    [Min(0)]
    public int chargeNetBoomIndex;

    [Range(1, 8)]
    public int chargeNetBoomCount = 1;

    [Min(0f)]
    public float chargeNetBoomInterval = 0.08f;

    [Tooltip("Minimum time after the final Stage 2A Net Boom spawn before Stage 2B begins. During Stage 2A the complete main charge motion is already active: scale growth, pulse, rotation, and red tint. Set this close to the visible duration of your Net Boom animation.")]
    [Min(0f)]
    public float chargeNetBoomReadDuration = 0.65f;

    [Tooltip("Optional extra pause after the Net Boom read duration. The complete main charge motion continues during this pause, then the charge prefabs and earthquake start together.")]
    [Min(0f)]
    public float chargeEffectDelayAfterNetBoom = 0.10f;

    [Header("Stage 2B - Charge Effects and Earthquake")]
    [Tooltip("Minimum time Stage 2B remains active after the charge prefabs and earthquake begin. This prevents a long Net Boom from consuming the entire Main Charge Duration.")]
    [Min(0f)]
    public float chargeStage2BMinimumDuration = 0.65f;

    public GameObject[] chargeEffectPrefabs;

    [Tooltip("World-space scatter around the Armored Crab when the charge prefabs are spawned.")]
    [Min(0f)]
    public float chargeEffectScatterRadius = 0.18f;

    [Tooltip("World-space X/Y offset added to the Armored Crab position when Stage 2B charge prefabs are spawned.")]
    public Vector2 chargeEffectOffset = Vector2.zero;

    [Tooltip("Uniform scale multiplier applied to every Stage 2B charge prefab for this play.")]
    [Min(0.01f)]
    public float chargeEffectScale = 1f;

    [Min(0f)]
    public float chargeEffectVisibleDuration;

    [Tooltip("When enabled, the earthquake starts on the same frame as the charge prefabs. When disabled, it starts with the Net Boom prelude for legacy timing.")]
    public bool startChargeEarthquakeWithEffects = true;

    [Min(0f)]
    public float chargeCameraShakeStrength = 0.075f;

    [Min(0f)]
    public float chargeCameraShakeDuration = 0.45f;

    [Header("Stage 3 - Full Screen Movement")]
    public bool enableFullScreenMovement = true;
    public CinematicRouteMode routeMode = CinematicRouteMode.Mixed;

    [Range(2, 10)]
    public int automaticRoutePointCount = 5;

    public CinematicRoutePoint[] manualRoutePoints;

    [Range(0.04f, 0.30f)]
    public float viewportEdgePadding = 0.12f;

    [Min(0.1f)]
    public float movementSpeed = 7.2f;

    [Min(0.01f)]
    public float movementArrivalDistance = 0.08f;

    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(
        0f,
        0f,
        1f,
        1f
    );

    [Min(0.1f)]
    public float movementScale = 1.95f;

    [Min(0f)]
    public float movementStartRotationSpeed = 145f;

    [Min(0f)]
    public float movementEndRotationSpeed = 320f;

    [Range(0f, 1f)]
    public float movementRedStrength = 0.82f;

    [Range(0f, 0.25f)]
    public float movementPulseStrength = 0.055f;

    public AudioClip movementSound;

    [Header("Continuous Movement Shake")]
    public bool enableContinuousMovementShake = true;

    [Min(0f)]
    public float continuousMovementShakeStrength = 0.035f;

    [Min(0.05f)]
    public float continuousMovementShakeDuration = 0.32f;

    [Min(0.05f)]
    public float continuousMovementShakeInterval = 0.48f;

    [Header("Route Arrival Effects")]
    public bool playNetBoomAtRoutePoints = true;

    [Min(0)]
    public int movementNetBoomIndex;

    [Range(1, 8)]
    public int movementNetBoomCount = 1;

    [Min(0f)]
    public float movementExplosionInterval = 0.07f;

    [Min(0f)]
    public float movementExplosionScatterRadius = 0.28f;

    [Header("Route Effect Transform and Armored Crab Sorting")]
    [Tooltip("Places Stage 3 Net Boom and arrival prefabs on the Armored Crab's current Sorting Layer. Sorting-order offsets keep the effects just above the crab while preserving pooling defaults on the next use.")]
    public bool movementEffectsMatchOwnerSorting = true;

    [Tooltip("World-space X/Y offset added to each Stage 3 Net Boom arrival position.")]
    public Vector2 movementNetBoomOffset = Vector2.zero;

    [Tooltip("Uniform scale multiplier applied to Stage 3 Net Boom effects.")]
    [Min(0.01f)]
    public float movementNetBoomScale = 1f;

    [Tooltip("Sorting-order offset relative to the Armored Crab. Positive values render the Net Boom above the crab.")]
    [Range(-100, 100)]
    public int movementNetBoomSortingOrderOffset = 3;

    public GameObject[] movementArrivalEffectPrefabs;

    [Tooltip("World-space X/Y offset added to custom Stage 3 arrival-effect prefabs.")]
    public Vector2 movementArrivalEffectOffset = Vector2.zero;

    [Tooltip("Uniform scale multiplier applied to every custom Stage 3 arrival-effect prefab.")]
    [Min(0.01f)]
    public float movementArrivalEffectScale = 1f;

    [Tooltip("Sorting-order offset relative to the Armored Crab for custom arrival prefabs.")]
    [Range(-100, 100)]
    public int movementArrivalEffectSortingOrderOffset = 4;

    [Min(0f)]
    public float movementArrivalEffectVisibleDuration;

    public AudioClip movementArrivalSound;

    [Min(0f)]
    public float movementArrivalShakeStrength = 0.08f;

    [Min(0f)]
    public float movementArrivalShakeDuration = 0.18f;

    public CinematicAreaDamageSettings movementAreaDamage =
        new CinematicAreaDamageSettings();

    [Header("Stage 5 - Certificate / Reward Prefab")]
    public CinematicCertificateTiming certificateTiming =
        CinematicCertificateTiming.RouteStart;

    [Tooltip("When enabled, uses CertificateTextManager pooling. -1 selects a random certificate; 0+ selects an exact prefab index.")]
    public bool useExistingCertificateSystem = true;

    public int certificatePrefabIndex = -1;
    public GameObject directCertificatePrefab;

    [Range(0, 9)]
    public int certificateAfterRoutePoint = 2;

    public CinematicTargetType certificatePosition =
        CinematicTargetType.ScreenCenter;

    public Vector2 certificateCustomViewport =
        new Vector2(0.5f, 0.5f);

    [Min(0f)]
    public float certificateSpawnDelay;

    [Min(0f)]
    public float certificateVisibleDuration = 2.2f;

    [Min(0.01f)]
    public float certificateScale = 1f;

    public float certificateRotation;
    public int certificateSortingOrder = 3400;
    public AudioClip certificateSound;

    [Header("Stage 6 - Approach Final Attacker")]
    public bool approachFinalAttacker = true;

    public Vector2 attackerViewportOffset = new Vector2(0f, 0.12f);

    [Min(0.1f)]
    public float attackerApproachSpeed = 8f;

    [Min(0f)]
    public float attackerPauseDuration = 0.18f;

    [Header("Stage 7 - Final Dramatic Position")]
    public CinematicTargetType finalTarget =
        CinematicTargetType.DensestFishArea;

    public Vector2 finalCustomViewport = new Vector2(0.5f, 0.5f);

    [Min(0.1f)]
    public float finalMoveSpeed = 6.2f;

    [Min(0.05f)]
    public float finalChargeDuration = 0.82f;

    [Min(0.1f)]
    public float finalChargeScale = 2.35f;

    [Min(0f)]
    public float finalChargeRotationSpeed = 430f;

    [Range(0f, 1f)]
    public float finalChargeRedStrength = 0.96f;

    [Range(0f, 0.35f)]
    public float finalChargePulseStrength = 0.10f;

    public AudioClip finalChargeSound;

    [Min(0f)]
    public float finalChargeShakeStrength = 0.17f;

    [Min(0f)]
    public float finalChargeShakeDuration = 0.70f;

    [Header("Stage 8 - Final Bomb Explosion")]
    public GameObject mainBombExplosionPrefab;

    [Min(0f)]
    public float mainBombVisibleDuration;

    [Min(0.01f)]
    public float mainBombScale = 1f;

    public int mainBombSortingOrder = 3600;
    public AudioClip finalExplosionSound;

    [Range(0, 24)]
    public int backgroundNetBoomCount = 8;

    [Min(0)]
    public int finalNetBoomIndex;

    [Min(0f)]
    public float backgroundExplosionRadius = 2.7f;

    [Min(0f)]
    public float backgroundExplosionInterval = 0.055f;

    [InspectorName("Background Net Boom Scale Range")]
    [Tooltip(
        "Random scale multiplier applied to each Stage 8 background " +
        "Net Boom and to any optional companion explosion spawned at " +
        "the same position."
    )]
    public Vector2 backgroundExplosionScaleRange =
        new Vector2(0.72f, 1.28f);

    public GameObject[] additionalFinalExplosionPrefabs;

    [Min(0f)]
    public float finalEffectVisibleDuration;

    [Min(0f)]
    public float finalExplosionShakeStrength = 0.28f;

    [Min(0f)]
    public float finalExplosionShakeDuration = 0.90f;

    public CinematicAreaDamageSettings finalAreaDamage =
        new CinematicAreaDamageSettings
        {
            damage = 1550f,
            radius = 3.4f,
            smallMultiplier = 1.25f,
            mediumMultiplier = 0.70f,
            specialMultiplier = 0.42f,
            miniBossMultiplier = 0.20f,
            mainBossMultiplier = 0.05f,
            mainBossImmune = true,
            maximumTargets = 18
        };

    [Header("Optional Slow Motion")]
    public bool enableSlowMotion = true;

    [Range(0.05f, 1f)]
    public float slowMotionTimeScale = 0.35f;

    [Min(0.02f)]
    public float slowMotionDuration = 0.24f;

    [Header("Stage 10 - Main Boss Skill Visual")]
    public bool enableBossSkill = true;

    [Tooltip("-1 skips safely. 0+ is an exact index in AnimatiorManager.mainBossFrontGunSkillPrefabs.")]
    public int bossSkillIndex = -1;

    [Min(0f)]
    public float bossSkillDelay = 0.12f;

    public CinematicTargetType bossSkillPosition =
        CinematicTargetType.ScreenCenter;

    public Vector2 bossSkillCustomViewport =
        new Vector2(0.5f, 0.5f);

    [Min(0.01f)]
    public float bossSkillScale = 1f;

    public float bossSkillRotation;

    [Min(0f)]
    public float bossSkillVisibleDuration;

    public int bossSkillSortingOrder = 3700;
    public AudioClip bossSkillSound;
    public bool bossSkillVisualOnly = true;
    public CinematicAreaDamageSettings bossSkillAreaDamage =
        new CinematicAreaDamageSettings
        {
            enabled = false,
            damage = 500f,
            radius = 2.5f,
            mainBossImmune = true
        };

    [Header("Stage 11 - Optional Direct Final Prefab")]
    public GameObject optionalFinalDeathPrefab;

    [Min(0f)]
    public float optionalFinalPrefabDelay;

    [Min(0f)]
    public float optionalFinalPrefabVisibleDuration;

    [Min(0.01f)]
    public float optionalFinalPrefabScale = 1f;

    public float optionalFinalPrefabRotation;
    public int optionalFinalPrefabSortingOrder = 3800;
    public AudioClip optionalFinalPrefabSound;

    [Header("Final Cleanup")]
    [Min(0f)]
    public float hideBodyAfterExplosionDelay = 0.10f;

    [Min(0f)]
    public float finalCleanupDelay = 0.65f;

    public bool stopCameraShakeOnCleanup = true;
    public bool restoreTimeScaleOnCleanup = true;

    private void OnValidate()
    {
        convulsionDuration = Mathf.Max(0.05f, convulsionDuration);
        mainChargeDuration = Mathf.Max(0.05f, mainChargeDuration);
        movementSpeed = Mathf.Max(0.1f, movementSpeed);
        movementArrivalDistance = Mathf.Max(0.01f, movementArrivalDistance);
        finalMoveSpeed = Mathf.Max(0.1f, finalMoveSpeed);
        finalChargeDuration = Mathf.Max(0.05f, finalChargeDuration);
        certificateScale = Mathf.Max(0.01f, certificateScale);
        mainBombScale = Mathf.Max(0.01f, mainBombScale);
        bossSkillScale = Mathf.Max(0.01f, bossSkillScale);
        optionalFinalPrefabScale = Mathf.Max(0.01f, optionalFinalPrefabScale);
        slowMotionTimeScale = Mathf.Clamp(slowMotionTimeScale, 0.05f, 1f);

        float minimumExplosionScale = Mathf.Max(
            0.01f,
            Mathf.Min(
                backgroundExplosionScaleRange.x,
                backgroundExplosionScaleRange.y
            )
        );
        float maximumExplosionScale = Mathf.Max(
            minimumExplosionScale,
            Mathf.Max(
                backgroundExplosionScaleRange.x,
                backgroundExplosionScaleRange.y
            )
        );
        backgroundExplosionScaleRange = new Vector2(
            minimumExplosionScale,
            maximumExplosionScale
        );
    }
}
