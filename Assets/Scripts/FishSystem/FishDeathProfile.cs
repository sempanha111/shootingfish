using UnityEngine;
using UnityEngine.Serialization;

public enum CenterScreenDeathPrefabSelectionMode
{
    RandomNoRepeat,
    Random,
    ExactIndex
}

public enum CenterScreenDeathPrefabSpace
{
    Auto,
    Canvas,
    World
}

public enum CenterScreenDeathPrefabOverlapMode
{
    IgnoreWhileVisible,
    ReplaceCurrent,
    AllowMultiple
}

public enum MainBossFrontGunSkillDeathTiming
{
    RewardBeat,
    DeathStart,
    ConvulsionStart
}

public enum DeathLatePhaseStartMode
{
    CinematicTimeline,
    AfterConvulsion,
    AfterConvulsionPrefabs,
    AfterDeathPrefabs,
    CustomDelay
}

public enum DeathAdditionalEffectStartMode
{
    // Keep CinematicStart at serialized value 0 so v13.13-v13.17
    // Additional Convulsion entries migrate without changing timing.
    CinematicStart,
    DeathStart
}

public enum DeathAdditionalEffectOffsetSpace
{
    // Backward-compatible v13.18 behavior. Position Offset is measured
    // directly in world units and does not rotate with the fish.
    World,

    // Position Offset is measured in world-unit distances along the fish's
    // current visual right/up axes.
    FishLocal,

    // Position Offset is normalized against the fish visual body size.
    // X = -1/+1 is approximately the left/right body edge from center and
    // Y = -1/+1 is approximately the lower/upper body edge from center.
    FishBodyNormalized
}

[System.Serializable]
public class DeathConvulsionEffectEntry
{
    [Tooltip("Prefab played as an additional death VFX. This entry no longer requires Death Convulsion to be enabled.")]
    public GameObject prefab;

    [Tooltip("Death Start plays from the first death frame. Cinematic Start plays when cinematic death motion begins; when convulsion is enabled this is the same frame the convulsion begins.")]
    public DeathAdditionalEffectStartMode startMode =
        DeathAdditionalEffectStartMode.CinematicStart;

    [Tooltip("Delay measured from the selected Start Mode. Additional death VFX are not clamped to the convulsion duration, so late/final shock rings can play after convulsion has ended.")]
    [Min(0f)]
    public float delay;

    [Tooltip("How Position Offset is interpreted. World preserves v13.18 behavior. Fish Local rotates the offset with the fish. Fish Body Normalized scales it by the fish visual half-width/half-height and is recommended for multiple rings along a long fish.")]
    public DeathAdditionalEffectOffsetSpace offsetSpace =
        DeathAdditionalEffectOffsetSpace.World;

    [Tooltip("Offset from the fish visual center. World = world units. Fish Local = world-unit distance along the fish visual axes. Fish Body Normalized = normalized body position where X +/-1 is approximately the left/right body edge and Y +/-1 is approximately the lower/upper body edge.")]
    public Vector2 positionOffset;

    [Tooltip("Keep this prefab attached to the fish visual center while active. Position Offset is combined with the selected Offset Space every frame, so Fish Local / Fish Body Normalized offsets rotate and travel with long fish instead of staying fixed in world space.")]
    public bool followFishCenter;

    [Tooltip("Random world-space scatter around Position Offset.")]
    [Min(0f)]
    public float scatterRadius;

    [Tooltip("Temporary X/Y scale multiplier for this prefab play.")]
    public Vector2 scale = Vector2.one;

    [Tooltip("Temporary Z rotation for this prefab play.")]
    public float rotationDegrees;

    [Tooltip("Seconds visible. Use 0 to detect Animator and ParticleSystem duration automatically.")]
    [Min(0f)]
    public float visibleDuration;

    [Tooltip("Extra delay after visible duration before the prefab returns to its pool.")]
    [Min(0f)]
    public float hideDelay = 0.15f;

    [Tooltip("Copies the fish Sorting Layer and applies Sorting Order Offset.")]
    public bool useFishSorting;

    [Tooltip("Negative renders under the fish; positive renders above it.")]
    [Range(-200, 200)]
    public int sortingOrderOffset = -10;
}

[CreateAssetMenu(
    menuName = "Fish Arcade/Fish Death Profile",
    fileName = "FishDeathProfile"
)]
public class FishDeathProfile : ScriptableObject
{
    [Header("Reward Calculation")]
    public FishRewardCalculationMode rewardCalculationMode =
        FishRewardCalculationMode.FixedFishCoin;

    [Min(0f)]
    public float rewardMultiplier = 1f;

    [Tooltip("Used only by Fish Coin With Small Shot Bonus. 0.10 means the shot can add at most 10% to the fish reward.")]
    [Range(0f, 1f)]
    public float maximumSmallShotBonusPercent = 0.10f;

    [Min(0f)]
    public float minimumReward;

    [Tooltip("0 means no maximum.")]
    [Min(0f)]
    public float maximumReward;

    [Header("Reward Text")]
    public RewardTextStyle rewardTextStyle = RewardTextStyle.Small;
    public GameObject customRewardTextPrefab;

    [Header("Standard Coin Animation")]
    [FormerlySerializedAs("playCoinAnimation")]
    public bool playStandardCoinAnimation = true;

    [Tooltip("How many separate coin bursts are played for this death. Keep normal fish at 1.")]
    [Range(1, 6)]
    public int coinAnimationPlayCount = 1;

    [Min(0f)]
    public float coinAnimationInterval = 0.10f;

    [Min(0f)]
    public float coinAnimationSpreadRadius = 0.25f;

    [Header("Main Boss Skill In Front Gun")]
    [Tooltip("Only used by MainBoss fish. A reward coin moves to the gun, then this skill animation plays there.")]
    public bool playMainBossSkillInFrontGun = true;

    [Tooltip("-1 randomly selects a valid skill prefab. 0 and above selects an exact prefab array index.")]
    public int mainBossFrontGunSkillIndex = -1;

    [Tooltip("Reward Beat keeps the old behavior. Death Start plays on the first death frame. Convulsion Start plays when the cinematic/convulsion begins; when convulsion is disabled it falls back to cinematic start.")]
    public MainBossFrontGunSkillDeathTiming mainBossFrontGunSkillTiming =
        MainBossFrontGunSkillDeathTiming.RewardBeat;

    [Tooltip("Optional delay from the selected timing point before the front-gun skill starts. Use 0 for immediate play.")]
    [Min(0f)]
    public float mainBossFrontGunSkillDelay;

    [Tooltip("Overrides AnimatiorManager Main Boss Coin Start Delay for this death profile. Use -1 to keep the manager default, or 0 to make the reward coin start moving to the gun immediately.")]
    [Range(-1f, 3f)]
    public float mainBossFrontGunCoinStartDelayOverride = -1f;

    [Header("Main Boss Front-Gun Skill Audio and Reward Text")]
    [Tooltip("Plays a dedicated sound when the front-gun skill prefab actually appears at the shooter gun.")]
    public bool playMainBossFrontGunSkillSound = true;

    [Tooltip("-1 randomly selects a valid Main Boss Front-Gun Skill sound. 0+ selects an exact SoundManager array index.")]
    public int mainBossFrontGunSkillSoundIndex = -1;

    [Tooltip("Shows the real calculated boss reward using the embedded RewardText inside the spawned PF_BossReward front-gun skill prefab.")]
    public bool showMainBossFrontGunRewardText = true;

    [Tooltip("Text placed before the reward amount. Example: + gives +25000.")]
    public string mainBossFrontGunRewardTextPrefix = "+";

    [Tooltip("Optional delay after the skill appears before the embedded reward TextMeshPro begins counting.")]
    [Min(0f)]
    public float mainBossFrontGunRewardTextDelay;

    [Header("Death Effects")]
    [FormerlySerializedAs("playNormalParticle")]
    [Tooltip("Boss-only coin burst particle at the fish death position.")]
    public bool playBossCoinBurst;

    [Tooltip("-1 randomly selects a valid Boss Coin Burst prefab. 0 and above select an exact array index from AnimatiorManager.")]
    public int bossCoinBurstEffectIndex = 0;

    public bool playNetBoom;

    [Min(0)]
    public int netBoomEffectIndex;

    [Tooltip("When enabled, Net Boom plays immediately when death starts instead of waiting for the reward beat.")]
    public bool playNetBoomImmediatelyAtDeathStart;

    [Tooltip("World-space offset applied to the configured Net Boom.")]
    public Vector2 netBoomPositionOffset;

    [Tooltip("Random world-space scatter around the Net Boom offset. Use 0 to place it exactly on the fish.")]
    [Min(0f)]
    public float netBoomScatterRadius = 1.2f;

    [Tooltip("Temporary scale multiplier for this profile's Net Boom play.")]
    [Min(0.01f)]
    public float netBoomScaleMultiplier = 1f;

    public GameObject customDeathEffect;

    [Tooltip("Delay from death start before the first custom prefab pulse. Use 0 to play it on the same frame as immediate Net Boom.")]
    [Min(0f)]
    public float customDeathEffectStartDelay;

    [Tooltip("World-space offset applied to every custom death prefab pulse.")]
    public Vector2 customDeathEffectPositionOffset;

    [Tooltip("Random world-space scatter added to the custom prefab offset.")]
    [Min(0f)]
    public float customDeathEffectScatterRadius = 0.18f;

    [Tooltip("Temporary X/Y scale multiplier applied to the custom prefab while it is visible.")]
    public Vector2 customDeathEffectScale = Vector2.one;

    [Tooltip("Temporary Z rotation applied to the custom prefab for this play.")]
    public float customDeathEffectRotationDegrees;

    [Tooltip("Seconds visible. Use 0 to detect Animator and ParticleSystem duration automatically.")]
    [Min(0f)]
    public float customDeathEffectVisibleDuration;

    [Header("Custom Death Effect Sorting")]
    [Tooltip("Copies the fish Sorting Layer and applies the order offset below. Use a negative offset to render the custom prefab under the fish.")]
    public bool customDeathEffectUseFishSorting;

    [Tooltip("Applied relative to the fish sorting order. Negative values render under the fish; positive values render above it.")]
    [Range(-200, 200)]
    public int customDeathEffectSortingOrderOffset = -20;

    [Min(0f)]
    public float customEffectHideDelay = 0.15f;

    [Header("Cinematic Death Motion")]
    [Tooltip("Adds a short impact, recoil, tumble, scale, and fade sequence even when the prefab has no custom death clip.")]
    public bool enableCinematicDeathMotion = true;

    [Tooltip("Delay before Animator trigger and cinematic fish motion begin. Use 0 to start together with immediate Net Boom and custom prefab.")]
    [Min(0f)]
    public float cinematicStartDelay;

    [Tooltip("Optional Animator trigger. It is ignored safely when the Animator has no matching trigger.")]
    public string deathAnimatorTrigger = "Death";

    [Header("Death Animator Speed")]
    [Tooltip("Overrides Animator.speed during the standard death cinematic. Useful when the fish's normal swim animation is too fast or too slow during death.")]
    public bool overrideDeathAnimatorSpeed;

    [Range(0.05f, 4f)]
    public float deathAnimatorSpeed = 1f;

    [Tooltip("Uses a separate Animator speed while the convulsion window is active, then returns to Death Animator Speed for the rest of the cinematic.")]
    public bool useSeparateConvulsionAnimatorSpeed;

    [Range(0.05f, 4f)]
    public float deathConvulsionAnimatorSpeed = 1.25f;

    [Min(0.05f)]
    public float deathDuration = 0.36f;

    [Tooltip("Normalized point in the death sequence when reward text, coins, sound, and configured effects fire.")]
    [Range(0f, 1f)]
    public float rewardBeatNormalized = 0.28f;

    [Tooltip("Plays the short impact-scale pulse at the beginning. Disable this when you want only the configured death zoom.")]
    public bool enableImpactScalePulse = true;

    [Tooltip("Impact scale can be smaller than 1 for a quick inward pulse, or larger than 1 for an outward pulse.")]
    [Range(0.05f, 5f)]
    public float impactScaleMultiplier = 1.10f;

    [Header("Cinematic Death Zoom")]
    [Tooltip("Gradually zooms the fish from its normal scale to Death Zoom End Scale during the cinematic.")]
    public bool enableDeathZoom = true;

    [Tooltip("Controls when the death zoom begins. After Convulsion Prefabs waits for convulsion VFX only; After Death Prefabs also includes the custom death-effect pulse sequence.")]
    public DeathLatePhaseStartMode deathZoomStartMode =
        DeathLatePhaseStartMode.CinematicTimeline;

    [Tooltip("Used by Cinematic Timeline mode. 0 starts immediately and 1 starts at the authored end of Death Duration.")]
    [Range(0f, 1f)]
    public float deathZoomStartNormalized;

    [Tooltip("Extra wait added after the selected zoom timing point. In Custom Delay mode this is the full delay from cinematic start.")]
    [Min(0f)]
    public float deathZoomAdditionalDelay;

    [Tooltip("Seconds used to move from normal scale to Death Zoom End Scale. 0 keeps the legacy behavior and uses the remaining authored Death Duration.")]
    [Min(0f)]
    public float deathZoomDuration;

    [Tooltip("Below 1 zooms smaller; 1 keeps the same size; above 1 zooms bigger.")]
    [Range(0.05f, 5f)]
    public float deathZoomEndScaleMultiplier = 0.68f;

    [Min(0f)]
    public float deathDriftDistance = 0.28f;

    [Tooltip("This is separate from Convulsion Rotation. Disable it to stop the normal cinematic tumble rotation.")]
    public bool enableDeathTumbleRotation = true;

    [Range(0f, 180f)]
    public float deathTumbleDegrees = 18f;

    [Range(0f, 1f)]
    public float fadeStartNormalized = 0.55f;

    [Header("Death Opacity Fade")]
    [Tooltip("Controls when opacity begins fading. After Convulsion Prefabs waits for convulsion VFX only; After Death Prefabs also includes the custom death-effect pulse sequence.")]
    public DeathLatePhaseStartMode deathOpacityStartMode =
        DeathLatePhaseStartMode.CinematicTimeline;

    [Tooltip("Extra wait added after the selected opacity timing point. In Custom Delay mode this is the full delay from cinematic start.")]
    [Min(0f)]
    public float deathOpacityAdditionalDelay;

    [Tooltip("Seconds used to fade from Death Opacity Start to End. 0 keeps the legacy behavior and uses the remaining authored Death Duration.")]
    [Min(0f)]
    public float deathOpacityFadeDuration;

    [Tooltip("When a late zoom/fade starts after the authored Death Duration, automatically extends the fish death lifetime so the late phase can finish instead of hiding the fish early.")]
    public bool extendDeathDurationForLatePhases = true;

    [Tooltip("When waiting for convulsion prefabs, include each prefab's Hide Delay after its visible animation/particles finish.")]
    public bool latePhaseWaitIncludesPrefabHideDelay;

    [Tooltip("Opacity held before the selected opacity start time. Use 1 for fully visible.")]
    [Range(0f, 1f)]
    public float deathOpacityStart = 1f;

    [Tooltip("Opacity reached at the end of the death cinematic before the pooled fish is hidden.")]
    [Range(0f, 1f)]
    public float deathOpacityEnd = 0f;

    [Tooltip("Controls how opacity moves from Start to End after Fade Start Normalized. X is fade progress and Y is blend amount.")]
    public AnimationCurve deathOpacityCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool fadeSpriteRenderers = true;
    public bool disableCollidersDuringDeath = true;

    [Header("Cinematic Convulsion")]
    [Tooltip("Adds a rapid position, rotation, and scale convulsion at the beginning of the cinematic motion.")]
    public bool enableDeathConvulsion;

    [Min(0.02f)]
    public float deathConvulsionDuration = 0.45f;

    [Min(0f)]
    public float deathConvulsionPositionStrength = 0.08f;

    [Range(0f, 45f)]
    public float deathConvulsionRotationDegrees = 10f;

    [Tooltip("Adds scale pulsing during convulsion. Disable it when the fish must keep the death-zoom scale without extra pulsing.")]
    public bool enableDeathConvulsionScalePulse = true;

    [Range(0f, 0.50f)]
    public float deathConvulsionScaleStrength = 0.08f;

    [Min(0.1f)]
    public float deathConvulsionFrequency = 18f;

    [Header("Convulsion Prefab and Earthquake")]
    [Tooltip("Plays the configured prefab while the cinematic convulsion is active.")]
    public bool playPrefabWithDeathConvulsion;

    [Tooltip("Optional pooled visual that begins during the convulsion. Leave empty to use earthquake only.")]
    public GameObject deathConvulsionEffectPrefab;

    [Tooltip("Delay measured from the start of convulsion. The runtime clamps this value so the presentation still begins before convulsion finishes.")]
    [Min(0f)]
    public float deathConvulsionPresentationDelay;

    [Tooltip("World-space offset from the fish position at the moment this convulsion prefab plays.")]
    public Vector2 deathConvulsionEffectPositionOffset;

    [Tooltip("Random world-space scatter added around the convulsion prefab offset.")]
    [Min(0f)]
    public float deathConvulsionEffectScatterRadius;

    [Tooltip("Temporary X/Y scale multiplier for the convulsion prefab.")]
    public Vector2 deathConvulsionEffectScale = Vector2.one;

    [Tooltip("Temporary Z rotation for the convulsion prefab.")]
    public float deathConvulsionEffectRotationDegrees;

    [Tooltip("Seconds visible. Use 0 to detect Animator and ParticleSystem duration automatically.")]
    [Min(0f)]
    public float deathConvulsionEffectVisibleDuration;

    [Tooltip("Extra delay after the detected or configured visible duration before the prefab returns to its pool.")]
    [Min(0f)]
    public float deathConvulsionEffectHideDelay = 0.15f;

    [Header("Convulsion Prefab Sorting")]
    [Tooltip("Copies the fish Sorting Layer for the convulsion prefab.")]
    public bool deathConvulsionEffectUseFishSorting;

    [Tooltip("Applied relative to the fish sorting order. Negative values render under the fish and positive values render above it.")]
    [Range(-200, 200)]
    public int deathConvulsionEffectSortingOrderOffset = -10;

    [Header("Additional Death Prefabs")]
    [FormerlySerializedAs("playAdditionalPrefabsWithDeathConvulsion")]
    [Tooltip("Plays every valid additional death VFX entry below. This works with or without Death Convulsion. Existing profiles using the old Additional Convulsion Prefabs toggle migrate automatically.")]
    public bool playAdditionalDeathPrefabs;

    [FormerlySerializedAs("deathConvulsionAdditionalEffects")]
    [Tooltip("Add GoldenRewardDeath, shock rings, auras, booms, or other death VFX here. Each entry can start at Death Start or Cinematic Start, follow the fish center, and has independent delay/scale/offset/rotation/lifetime/sorting.")]
    public DeathConvulsionEffectEntry[] deathAdditionalEffects;

    [Header("Convulsion Earthquake")]
    [Tooltip("Starts an earthquake on the exact same frame as the convulsion prefab presentation.")]
    public bool playEarthquakeWithDeathConvulsion;

    [Min(0f)]
    public float deathConvulsionEarthquakeStrength = 0.08f;

    [Min(0f)]
    public float deathConvulsionEarthquakeDuration = 0.35f;

    [Tooltip("Optional subtle camera response at the reward beat. Keep ordinary fish at zero to avoid constant screen shake.")]
    [Min(0f)]
    public float deathCameraShakeStrength;

    [Min(0f)]
    public float deathCameraShakeDuration;

    [Tooltip("When disabled, the custom death prefab plays exactly once. Net Boom and reward effects still play only once.")]
    public bool enableCustomEffectPulses = true;

    [Tooltip("Number of custom-death-effect plays when Custom Effect Pulses is enabled.")]
    [Range(1, 4)]
    public int customEffectPulseCount = 1;

    [Min(0.02f)]
    public float customEffectPulseInterval = 0.14f;

    [Header("Center-Screen Celebration Prefab")]
    [Tooltip("Plays one selected prefab at the visible screen position when this fish grants its death reward.")]
    public bool playCenterScreenDeathPrefab;

    [Tooltip("Add as many celebration prefabs as needed. Null entries are ignored safely.")]
    public GameObject[] centerScreenDeathPrefabs;

    [Tooltip("Random No Repeat avoids showing the same valid prefab twice in a row for this Death Profile.")]
    public CenterScreenDeathPrefabSelectionMode
        centerScreenDeathPrefabSelection =
            CenterScreenDeathPrefabSelectionMode.RandomNoRepeat;

    [Tooltip("Used only by Exact Index selection. The value is the array index above.")]
    [Min(0)]
    public int centerScreenDeathPrefabIndex;

    [Tooltip("Final chance to show the center-screen prefab after all other requirements pass.")]
    [Range(0f, 1f)]
    public float centerScreenDeathPrefabChance = 1f;

    [Tooltip("The earned arcade-point reward must be at least this value. Use 0 for no minimum.")]
    [Min(0f)]
    public float minimumCenterScreenDeathPrefabReward;

    [Tooltip("Auto uses Canvas placement for UI/RectTransform prefabs and world placement for Sprite/Particle prefabs.")]
    public CenterScreenDeathPrefabSpace centerScreenDeathPrefabSpace =
        CenterScreenDeathPrefabSpace.Auto;

    [Tooltip("Prevents Rocket or chain defeats from covering the screen with many celebration prefabs.")]
    public CenterScreenDeathPrefabOverlapMode
        centerScreenDeathPrefabOverlap =
            CenterScreenDeathPrefabOverlapMode.IgnoreWhileVisible;

    [Tooltip("Normalized visible-screen position. (0.5, 0.5) is the exact center on every aspect ratio.")]
    public Vector2 centerScreenDeathViewportPosition =
        new Vector2(0.5f, 0.5f);

    [Tooltip("Extra offset in Canvas pixels when the selected prefab is UI.")]
    public Vector2 centerScreenDeathCanvasOffset;

    [Tooltip("Extra offset in world units when the selected prefab is a Sprite/Particle effect.")]
    public Vector2 centerScreenDeathWorldOffset;

    [Tooltip("Multiplies the prefab's authored scale. Animate a child object when the profile multiplier must remain fixed.")]
    [Min(0.01f)]
    public float centerScreenDeathScaleMultiplier = 1f;

    [Tooltip("Seconds visible. Use 0 to detect Animator and ParticleSystem duration automatically. Set a value for looping clips.")]
    [Min(0f)]
    public float centerScreenDeathVisibleDuration;

    [Min(0f)]
    public float centerScreenDeathHideDelay = 0.15f;

    [Tooltip("Keeps the celebration timing correct while gameplay time is paused or slowed.")]
    public bool centerScreenDeathUseUnscaledTime = true;

    [Header("Sounds and Certificate")]
    public bool playBigWinSound;

    [Tooltip("-1 randomly selects a valid Big Win sound from SoundManager.")]
    public int bigWinSoundIndex = -1;

    [Range(0f, 1f)]
    public float certificateChance;

    [Min(0f)]
    public float minimumCertificateWin;

    [System.Obsolete("Use playStandardCoinAnimation instead.")]
    public bool playCoinAnimation
    {
        get { return playStandardCoinAnimation; }
        set { playStandardCoinAnimation = value; }
    }

    [System.Obsolete("Use playBossCoinBurst instead.")]
    public bool playNormalParticle
    {
        get { return playBossCoinBurst; }
        set { playBossCoinBurst = value; }
    }

}
