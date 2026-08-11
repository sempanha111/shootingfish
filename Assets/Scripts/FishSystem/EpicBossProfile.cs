using System;
using UnityEngine;

public enum EpicBossSkillType
{
    Earthquake,
    HeavyRoar,
    AccelerationBurst,
    AggressiveCharge,
    WaterShockwave,
    DramaticCameraShake
}

public enum EpicBossFakeRewardMode
{
    FixedAmount,
    PercentageOfFinalReward
}

[Serializable]
public sealed class EpicBossSkillEntry
{
    public bool enabled = true;
    public EpicBossSkillType skillType;

    [Min(0f)]
    public float weight = 1f;

    [Tooltip("Optional Animator trigger. Leave empty to use only procedural movement/VFX.")]
    public string animatorTrigger;

    [Min(0f)]
    public float windUpDuration = 0.25f;

    [Min(0.02f)]
    public float activeDuration = 0.8f;

    [Min(0f)]
    public float recoveryDuration = 0.35f;

    [Min(0.05f)]
    public float movementSpeedMultiplier = 2.2f;

    [Min(0f)]
    public float cameraShakeStrength = 0.12f;

    [Min(0f)]
    public float cameraShakeDuration = 0.6f;

    public GameObject effectPrefab;
    public Vector3 effectOffset;

    [Tooltip("0 uses the longest Animator or ParticleSystem duration on the effect prefab.")]
    [Min(0f)]
    public float effectVisibleDuration;

    [Min(0f)]
    public float effectHideDelay = 0.15f;

    public AudioClip[] soundClips;
}

[CreateAssetMenu(
    menuName = "Fish Arcade/Epic Boss Profile",
    fileName = "EpicBossProfile"
)]
public sealed class EpicBossProfile : ScriptableObject
{
    [Header("Optional Extra Stat Scaling")]
    public bool applyAdditionalStatMultipliers;

    [Min(0.1f)]
    public float hpMultiplier = 3f;

    [Min(0f)]
    public float rewardMultiplier = 1.25f;

    [Min(0.1f)]
    public float movementSpeedMultiplier = 0.85f;

    [Header("Entrance and Introduction")]
    [Min(0.1f)]
    public float entranceDuration = 4f;

    [Min(0.05f)]
    public float entranceSpeedMultiplier = 0.45f;

    [Range(0.1f, 0.45f)]
    public float entranceTargetHorizontalViewport = 0.28f;

    [Range(0.15f, 0.85f)]
    public float entranceTargetVerticalViewport = 0.50f;

    [Min(0f)]
    public float introductionHoldDuration = 1.2f;

    [Tooltip("Requests additional pooled fish parade waves from SwapFishScript during this boss introduction.")]
    public bool requestDirectorIntroductionParade = true;

    [Range(0, 2)]
    public int introductionParadeWaveCount = 1;

    [Min(0.1f)]
    public float introductionParadeWaveGap = 0.65f;

    public string introductionAnimatorTrigger = "BossIntro";
    public GameObject introductionEffectPrefab;
    public Vector3 introductionEffectOffset;

    [Min(0f)]
    public float introductionEffectVisibleDuration;

    [Min(0f)]
    public float introductionEffectHideDelay = 0.2f;

    public AudioClip[] introductionSounds;

    [Header("Heavy Majestic Movement")]
    [Min(0.05f)]
    public float slowSpeedMultiplier = 0.35f;

    [Min(0.05f)]
    public float fastSpeedMultiplier = 0.90f;

    [Tooltip("Random duration used for one complete slow-fast-slow cycle.")]
    public Vector2 speedCycleDurationRange = new Vector2(4.5f, 7.5f);

    [Min(1f)]
    public float smoothTurnDegreesPerSecond = 42f;

    [Tooltip("Acceleration smoothing for the Rigidbody velocity. Higher values feel heavier; lower values react faster.")]
    [Range(0.08f, 0.80f)]
    public float velocitySmoothTime = 0.32f;

    [Tooltip("Subtle steering noise that keeps long paths alive without producing zig-zag robot turns.")]
    [Range(0f, 18f)]
    public float roamWanderAngleDegrees = 4.5f;

    [Min(0.02f)]
    public float roamWanderFrequency = 0.18f;

    [Range(0.02f, 0.35f)]
    public float viewportBoundaryPadding = 0.13f;

    [Range(0.01f, 0.20f)]
    public float emergencyOutsideTolerance = 0.08f;

    [Min(0.05f)]
    public float targetReachDistance = 0.55f;

    public Vector2 targetChangeDelayRange = new Vector2(2.8f, 5.8f);
    public Vector2 targetPauseRange = new Vector2(0.10f, 0.45f);

    [Range(0f, 1f)]
    public float largeTurnChance = 0.65f;

    [Range(10f, 170f)]
    public float minimumLargeTurnAngle = 50f;

    [Tooltip("New arena targets are rejected until they are this far away as a fraction of the camera width.")]
    [Range(0.20f, 0.85f)]
    public float minimumTargetViewportDistance = 0.42f;

    [Header("Balanced Arena Occupancy")]
    [Tooltip("Avoids repeatedly parking the Epic Boss in the same central splash-damage pocket as normal fish.")]
    public bool avoidCentralArenaPocket = true;

    [Range(0.06f, 0.30f)]
    public float centerAvoidanceHalfWidth = 0.16f;

    [Range(0.06f, 0.30f)]
    public float centerAvoidanceHalfHeight = 0.18f;

    [Tooltip("Small chance to use the center for cinematic variety. Zero fully blocks center destinations.")]
    [Range(0f, 0.30f)]
    public float centerTargetChance = 0.08f;

    [Min(0.1f)]
    public float boundaryReturnSpeedMultiplier = 1.15f;

    [Header("Adaptive Pressure Movement")]
    [Range(0.05f, 0.95f)]
    public float aggressiveMovementHealthThreshold = 0.58f;

    [Range(0.01f, 0.90f)]
    public float criticalMovementHealthThreshold = 0.24f;

    [Range(1f, 2f)]
    public float aggressiveMovementSpeedMultiplier = 1.12f;

    [Range(1f, 2.5f)]
    public float criticalMovementSpeedMultiplier = 1.24f;

    [Range(1f, 2f)]
    public float criticalTurnSpeedMultiplier = 1.25f;

    [Min(0.25f)]
    public float stuckCheckInterval = 1.25f;

    [Min(0.01f)]
    public float minimumStuckTravelDistance = 0.08f;

    [Header("Optional Cinematic Off-Screen Pass")]
    [Tooltip("The boss briefly exits, re-enters from the opposite side, and continues the same encounter.")]
    public bool enableCinematicOffscreenPasses = true;

    [Range(0f, 1f)]
    public float cinematicPassChance = 0.30f;

    [Tooltip("A new random opportunity is scheduled after each check.")]
    public Vector2 cinematicPassIntervalRange =
        new Vector2(10f, 18f);

    [Range(0.02f, 0.25f)]
    public float cinematicPassOutsideViewportPadding = 0.10f;

    [Min(0.1f)]
    public float cinematicPassSpeedMultiplier = 1.05f;

    [Tooltip("Extra speed used only while the boss performs the invisible outer part of the return arc.")]
    [Min(0.25f)]
    public float cinematicOutsideArcSpeedMultiplier = 2.1f;

    [Range(0.12f, 0.48f)]
    public float cinematicReturnArcVerticalShift = 0.30f;

    [Min(2f)]
    public float cinematicPassMaximumDuration = 10f;

    [Header("HP Phase Thresholds")]
    [Range(0.01f, 0.99f)]
    public float firstConvulsionThreshold = 0.80f;

    [Range(0.01f, 0.99f)]
    public float fakeDeathThreshold = 0.70f;

    [Range(0.01f, 0.99f)]
    public float strongConvulsionThreshold = 0.50f;

    [Header("First Convulsion")]
    public string firstConvulsionAnimatorTrigger = "BossConvulsion";

    [Min(0.05f)]
    public float firstConvulsionDuration = 0.55f;

    [Min(0f)]
    public float firstConvulsionPositionStrength = 0.10f;

    [Min(0f)]
    public float firstConvulsionRotationStrength = 7f;

    public GameObject firstConvulsionEffectPrefab;
    public AudioClip[] firstConvulsionSounds;

    [Header("Fake Death Reward at 70 Percent")]
    public string fakeDeathAnimatorTrigger = "BossFakeDeath";

    [Min(0.05f)]
    public float fakeDeathDuration = 1.25f;

    public EpicBossFakeRewardMode fakeRewardMode =
        EpicBossFakeRewardMode.FixedAmount;

    [Min(0f)]
    public float fakeRewardFixedAmount = 25f;

    [Range(0f, 1f)]
    public float fakeRewardPercentOfFinalReward = 0.08f;

    [Min(0f)]
    public float fakeRewardMaximum = 100f;

    [Range(0f, 1f)]
    public float fakeRewardCollectibleChance = 0.45f;

    public bool showFakeRewardText = true;

    [Tooltip("When enabled, visual-only fake rewards show a question mark after the amount.")]
    public bool revealVisualOnlyRewardWithQuestionMark;

    [Range(1, 6)]
    public int fakeRewardCoinAnimationCount = 2;

    [Min(0f)]
    public float fakeRewardCoinAnimationInterval = 0.12f;

    [Min(0f)]
    public float fakeRewardCoinAnimationSpread = 0.45f;

    public bool fakeRewardPlayBossCoinBurst = true;

    [Tooltip("-1 randomly selects a valid Boss Coin Burst prefab. 0 and above select an exact array index from AnimatiorManager.")]
    public int fakeRewardBossCoinBurstEffectIndex = 0;

    public bool fakeRewardPlayNetBoom = true;

    [Min(0)]
    public int fakeRewardNetBoomIndex;

    public GameObject fakeDeathEffectPrefab;
    public AudioClip[] fakeDeathSounds;

    [Header("Strong Convulsion at 50 Percent")]
    public string strongConvulsionAnimatorTrigger = "BossStrongConvulsion";

    [Min(0.05f)]
    public float strongConvulsionDuration = 0.95f;

    [Min(0f)]
    public float strongConvulsionPositionStrength = 0.18f;

    [Min(0f)]
    public float strongConvulsionRotationStrength = 13f;

    public GameObject strongConvulsionEffectPrefab;
    public AudioClip[] strongConvulsionSounds;

    [Header("Fire-Reactive Convulsion")]
    [Tooltip("Brief flinches react to actual incoming hit cadence. They end as soon as fire pressure expires and never hold the boss in a permanent stun.")]
    public bool enableReactiveFireConvulsions = true;

    [Range(1, 8)]
    public int sustainedFireHitCount = 2;

    [Min(0.05f)]
    public float sustainedFireWindow = 0.55f;

    [Min(0.05f)]
    public float fireReleaseGracePeriod = 0.34f;

    [Range(0f, 1f)]
    public float microConvulsionChancePerHit = 0.42f;

    [Range(0f, 1f)]
    public float repeatMicroConvulsionChance = 0.72f;

    public Vector2 microConvulsionDurationRange = new Vector2(0.07f, 0.15f);
    public Vector2 microConvulsionRecoveryRange = new Vector2(0.08f, 0.18f);

    [Range(0f, 0.20f)]
    public float microConvulsionPositionStrength = 0.025f;

    [Range(0f, 0.20f)]
    public float microConvulsionScaleStrength = 0.05f;

    [Range(0f, 15f)]
    public float microConvulsionRotationStrength = 2.5f;

    [Range(0.15f, 1f)]
    public float reactiveMovementSpeedMultiplier = 0.72f;

    public string reactiveConvulsionAnimatorTrigger = "BossHitFlinch";
    public string sustainedFireAnimatorBool = "BossUnderFire";

    [Header("Random Skills")]
    public bool enableRandomSkills = true;
    public Vector2 firstSkillDelayRange = new Vector2(4f, 8f);
    public Vector2 skillIntervalRange = new Vector2(5f, 12f);
    public bool preventImmediateSkillRepeat = true;

    public EpicBossSkillEntry[] randomSkills =
    {
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.Earthquake,
            weight = 1f,
            activeDuration = 0.8f,
            cameraShakeStrength = 0.12f,
            cameraShakeDuration = 0.8f
        },
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.HeavyRoar,
            weight = 1f,
            activeDuration = 0.7f,
            cameraShakeStrength = 0.08f,
            cameraShakeDuration = 0.45f
        },
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.AccelerationBurst,
            weight = 1f,
            activeDuration = 0.9f,
            movementSpeedMultiplier = 1.8f
        },
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.AggressiveCharge,
            weight = 1f,
            activeDuration = 0.75f,
            movementSpeedMultiplier = 2.4f,
            cameraShakeStrength = 0.06f,
            cameraShakeDuration = 0.4f
        },
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.WaterShockwave,
            weight = 1f,
            activeDuration = 0.65f,
            cameraShakeStrength = 0.10f,
            cameraShakeDuration = 0.55f
        },
        new EpicBossSkillEntry
        {
            skillType = EpicBossSkillType.DramaticCameraShake,
            weight = 0.75f,
            activeDuration = 0.65f,
            cameraShakeStrength = 0.16f,
            cameraShakeDuration = 0.65f
        }
    };

    [Header("Final Death")]
    public string deathAnimatorTrigger = "BossDeath";

    [Min(0.05f)]
    public float deathAnimationDuration = 2.5f;

    public bool disableCollidersDuringDeath = true;
    public GameObject deathEffectPrefab;
    public Vector3 deathEffectOffset;

    [Tooltip("0 uses the longest Animator or ParticleSystem duration on the effect prefab.")]
    [Min(0f)]
    public float deathEffectVisibleDuration;

    [Min(0f)]
    public float deathEffectHideDelay = 0.3f;

    public AudioClip[] deathSounds;

    [Min(0f)]
    public float deathCameraShakeStrength = 0.12f;

    [Min(0f)]
    public float deathCameraShakeDuration = 0.8f;

    [Header("Procedural Final Death Polish")]
    [Range(1f, 1.5f)]
    public float deathImpactScaleMultiplier = 1.16f;

    [Range(0f, 360f)]
    public float deathTumbleDegrees = 52f;

    [Min(0f)]
    public float deathDriftDistance = 0.70f;

    [Range(0f, 1f)]
    public float deathFadeStartNormalized = 0.62f;

    [Range(1, 5)]
    public int deathEffectPulseCount = 3;

    [Min(0.05f)]
    public float deathEffectPulseInterval = 0.38f;

    [Tooltip("Adds a smaller final shake near the reward beat, after the initial impact shake.")]
    [Range(0f, 1f)]
    public float finalDeathShakeMultiplier = 0.55f;
}
