using System;
using UnityEngine;

public enum FishCloneFormationRadiusSpace
{
    ViewportHeight,
    WorldUnits
}

public enum FishCloneFacingMode
{
    KeepOriginalRotation,
    FaceCenter,
    FaceAwayFromCenter,
    TangentClockwise,
    TangentCounterClockwise,
    FaceMovementDirection
}

public enum FishCloneAnimatorStartMode
{
    CopyCurrentState,
    RestartDefaultState,
    PlaySpecificState
}

public enum FishCloneTargetMode
{
    NormalizedViewport,
    WorldPosition
}

public enum FishCloneBackgroundPlayTiming
{
    WholeCinematic,
    FinalBoomOnly
}

public enum FishCloneBackgroundAnchorMode
{
    FormationCenter,
    FinalCenter,
    NormalizedViewport,
    WorldPosition
}

[Serializable]
public class FishCloneCinematicSlot
{
    [Header("Formation Override")]
    public bool overrideFormationAngle;
    public float formationAngleDegrees;

    [Min(0.01f)]
    public float scaleMultiplier = 1f;

    [Tooltip("Use -1 to use the profile's global individual rotation speed.")]
    public float individualRotationSpeedOverride = -1f;

    [Header("Separation Destination")]
    public FishCloneTargetMode destinationMode =
        FishCloneTargetMode.NormalizedViewport;

    [Tooltip("Normalized viewport target. (0,0) is bottom-left and (1,1) is top-right.")]
    public Vector2 destinationViewport = new Vector2(0.5f, 0.75f);

    public Vector3 destinationWorldPosition;

    [Tooltip("Use -1 to use Global Separation Speed.")]
    public float movementSpeedOverride = -1f;

    [Tooltip("Use -1 to use Global Separation Duration Override. A duration > 0 wins over speed.")]
    public float movementDurationOverride = -1f;

    [Min(0f)]
    public float destinationWaitDuration = 0.25f;

    [Header("Final Merge Override")]
    [Tooltip("Use -1 to use Final Center Movement Speed.")]
    public float finalMovementSpeedOverride = -1f;
}

[Serializable]
public class FishCloneCinematicFinalEffect
{
    public GameObject prefab;
    public Vector2 positionOffset;
    public Vector2 scale = Vector2.one;
    public float rotationDegrees;

    [Tooltip("0 detects Animator/ParticleSystem lifetime using AnimatiorManager.")]
    [Min(0f)]
    public float visibleDuration;

    [Min(0f)]
    public float hideDelay = 0.15f;

    public bool useFishSorting = true;

    [Range(-500, 500)]
    public int sortingOrderOffset = 120;
}

[CreateAssetMenu(
    menuName = "Fish Arcade/Fish Clone Cinematic Death Profile",
    fileName = "FishCloneCinematicDeathProfile"
)]
public class FishCloneCinematicDeathProfile : ScriptableObject
{
    [Header("General")]
    public bool enableCustomCloneDeathEffect = true;

    [Tooltip("-1 allows any fish using this profile. Crystal Whale should use 41.")]
    public int requiredFishId = 41;

    [Tooltip("Wait for the StandardDeathSequence's known death-prefab window before clones begin.")]
    public bool waitForExistingDeathPresentation = true;

    [Min(0f)]
    public float additionalStartDelay = 0.05f;

    [Tooltip("Use unscaled time for clone movement/spin. Usually leave disabled so the effect follows gameplay time.")]
    public bool useUnscaledTime;

    [Tooltip("Hide the already-dead real fish while the visual clones are active.")]
    public bool hideOriginalFishDuringCinematic = true;

    [Header("Cinematic Background Effect (Runs With Clone Effect)")]
    [Tooltip("Enable the optional background VFX layer. The same assigned prefab from older profiles is preserved.")]
    public bool enableBackgroundEffect = true;

    [Tooltip("Whole Cinematic starts the background when the visual clones begin and keeps it alive through formation, spin, dashes, separation, merge, and final Boom. Final Boom Only keeps the old behavior.")]
    public FishCloneBackgroundPlayTiming backgroundPlayTiming =
        FishCloneBackgroundPlayTiming.WholeCinematic;

    public GameObject finalBackgroundEffectPrefab;

    [Tooltip("Where the long-running background effect is anchored while the clone cinematic is active.")]
    public FishCloneBackgroundAnchorMode backgroundAnchorMode =
        FishCloneBackgroundAnchorMode.FormationCenter;

    [Tooltip("Used only when Background Anchor Mode is Normalized Viewport.")]
    public Vector2 backgroundViewportPosition = new Vector2(0.5f, 0.5f);

    [Tooltip("Used only when Background Anchor Mode is World Position.")]
    public Vector3 backgroundWorldPosition;

    [Tooltip("Keep the background attached to its selected anchor every frame. Useful when the formation/final center is a moving Transform override.")]
    public bool backgroundFollowAnchor = true;

    public Vector2 finalBackgroundEffectOffset;
    public Vector2 finalBackgroundEffectScale = Vector2.one;

    [Tooltip("Initial Z rotation applied when the background starts.")]
    public float finalBackgroundEffectRotationDegrees;

    [Header("Background Unlimited Rotation")]
    public bool backgroundRotationEnabled = true;

    [Min(0f)]
    public float backgroundRotationSpeed = 45f;

    public bool backgroundRotationClockwise;

    [Tooltip("Recommended ON. Rotation has no turn limit and continues for the complete clone cinematic until cleanup begins.")]
    public bool continueBackgroundRotationUntilCinematicEnds = true;

    [Tooltip("Used only when Continue Until Cinematic Ends is OFF. 0 means no timed rotation.")]
    [Min(0f)]
    public float backgroundRotationDuration;

    [Tooltip("Leave OFF for unlimited turns. Enable only if you intentionally want a maximum number of rotations.")]
    public bool limitBackgroundRotationByTurns;

    [Min(0f)]
    public float maximumBackgroundTurns = 1f;

    [Tooltip("For Final Boom Only mode, 0 detects Animator/ParticleSystem lifetime. Whole Cinematic mode ignores this because the controller explicitly keeps the pooled object alive until cleanup.")]
    [Min(0f)]
    public float finalBackgroundEffectVisibleDuration;

    [Tooltip("Used by Final Boom Only mode. Whole Cinematic mode stops the persistent background during normal cinematic cleanup.")]
    [Min(0f)]
    public float finalBackgroundEffectHideDelay = 0.15f;

    [Range(-500, 500)]
    public int finalBackgroundSortingOrderOffset = 80;

    [Header("Visual Clone Safety / Appearance")]
    [Tooltip("The runtime clones only Transform, SpriteRenderer, SortingGroup, and Animator components. Gameplay scripts/colliders/HP are never cloned.")]
    [Min(0.01f)]
    public float baseCloneScaleMultiplier = 1f;

    [Range(0f, 1f)]
    public float cloneOpacity = 1f;

    [Tooltip("Usually false so the normal fish shadow is not copied into the four-whale cinematic.")]
    public bool includeShadowVisuals;

    [Tooltip("Absolute alpha used by copied clone shadows. Kept separate from body Clone Opacity so four overlapping shadows never become solid/dark.")]
    [Range(0f, 1f)]
    public float cloneShadowOpacity = 0.18f;

    public bool useFishSorting = true;

    [Range(-500, 500)]
    public int cloneSortingOrderOffset = 30;

    [Range(0, 20)]
    public int cloneSortingOrderStep = 1;

    [Header("Animator")]
    public bool keepAnimatorPlaying = true;
    public FishCloneAnimatorStartMode animatorStartMode =
        FishCloneAnimatorStartMode.CopyCurrentState;

    [Tooltip("Used only by Play Specific State. Use the Animator state name or full path accepted by Animator.Play.")]
    public string specificAnimatorStateName;

    [Min(0)]
    public int specificAnimatorLayer;

    [Range(0f, 4f)]
    public float specificAnimatorNormalizedTime;

    [Tooltip("1 = normal playback. The clone uses this as its independent playback speed rather than inheriting the slowed death Animator.speed.")]
    [Range(0.01f, 4f)]
    public float animatorSpeedMultiplier = 1f;

    public bool forceAnimatorAlwaysAnimate = true;
    public bool keepAnimatorPlayingDuringFormation = true;
    public bool keepAnimatorPlayingDuringSpin = true;
    public bool keepAnimatorPlayingDuringSeparation = true;
    public bool keepAnimatorPlayingAtDestination = true;
    public bool keepAnimatorPlayingDuringFinalMerge = true;

    [Header("Formation")]
    [Range(1, 16)]
    public int cloneCount = 4;

    [Tooltip("Used when no Formation Center Transform Override is assigned on the controller.")]
    public Vector2 formationCenterViewport = new Vector2(0.5f, 0.5f);

    public FishCloneFormationRadiusSpace formationRadiusSpace =
        FishCloneFormationRadiusSpace.ViewportHeight;

    [Tooltip("Viewport Height mode: 0.15 means 15% of screen height. World Units mode: direct world distance.")]
    [Min(0f)]
    public float formationRadius = 0.15f;

    [Tooltip("90 degrees puts the first clone at the top.")]
    public float formationStartingAngle = 90f;

    public bool autoDistributeClonesAroundCircle = true;

    [Min(0f)]
    public float formationBuildDuration = 0.25f;

    public AnimationCurve formationBuildCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public FishCloneFacingMode formationFacing =
        FishCloneFacingMode.FaceAwayFromCenter;

    [Tooltip("Angle of the artwork's head/forward direction in its local sprite space. 0 means the head points local +X, 90 means local +Y.")]
    public float spriteForwardAngleDegrees;

    [Header("Formation Spin")]
    public bool formationRotationEnabled = true;

    [Min(0f)]
    public float formationRotationSpeed = 135f;

    [Tooltip("Initial Z rotation of the complete formation before spinning begins. Leave 0 to keep Clone 1 at the authored Formation Starting Angle.")]
    public float formationRotationStartingAngle;

    public bool formationClockwise;

    [Tooltip("How long the stage waits before separation can start.")]
    [Min(0f)]
    public float spinWaitDuration = 1.4f;

    [Tooltip("Used when Continue Rotation Until Next Stage is disabled. 0 means no timed rotation unless Continue is enabled.")]
    [Min(0f)]
    public float formationRotationDuration = 1.4f;

    [Tooltip("When enabled, formation rotation keeps running for the complete spin/wait stage.")]
    public bool continueFormationRotationUntilNextStage = true;

    [Tooltip("Leave disabled for unlimited turns. Enable only when you intentionally want a maximum turn count.")]
    public bool limitFormationRotationByTurns;

    [Min(0f)]
    public float maximumFormationTurns = 1f;

    [Header("Individual Fish Rotation")]
    public bool individualFishRotationEnabled;

    [Min(0f)]
    public float individualFishRotationSpeed = 90f;

    public bool individualFishRotationClockwise;
    public bool continueIndividualRotationDuringSeparation;
    public bool continueIndividualRotationAtDestination;
    public bool continueIndividualRotationDuringFinalMerge;

    [Header("Aggressive Out-And-Back Dash")]
    [Tooltip("After the formation spin, every clone lunges off-screen and snaps back to its formation position before the normal custom-destination stage.")]
    public bool aggressiveOutAndBackEnabled = true;

    [Tooltip("How many off-screen -> return cycles are played. Crystal Whale recommended value: 2.")]
    [Range(1, 8)]
    public int aggressiveOutAndBackRepeats = 2;

    [Tooltip("Extra distance past the viewport edge. The clone's own visual bounds can also be included so the complete long whale exits the screen.")]
    [Range(0f, 0.50f)]
    public float aggressiveOutScreenViewportPadding = 0.10f;

    [Tooltip("When enabled, off-screen targets include the clone visual bounds, not only its pivot. Recommended for Crystal Whale's long body.")]
    public bool aggressiveEnsureFullyOffscreen = true;

    [Tooltip("Small pull-back toward the center before each outward dash. This creates a bold, angry lunge instead of a soft glide.")]
    [Range(0f, 0.60f)]
    public float aggressiveWindUpFormationRadiusMultiplier = 0.16f;

    [Min(0f)]
    public float aggressiveWindUpDuration = 0.07f;

    [Min(0.01f)]
    public float aggressiveOutboundSpeed = 16f;

    [Min(0.01f)]
    public float aggressiveReturnSpeed = 19f;

    [Tooltip("Tiny hold while the clone is fully beyond the screen edge before it attacks back inward.")]
    [Min(0f)]
    public float aggressiveOutsideHoldDuration = 0.035f;

    [Tooltip("Each repeat can be slightly faster than the previous repeat. 1.12 = 12% faster per cycle.")]
    [Range(1f, 2f)]
    public float aggressiveRepeatSpeedMultiplier = 1.12f;

    [Tooltip("Hard launch / hard settle curve for an angry, decisive stride.")]
    public AnimationCurve aggressiveOutboundCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 4.8f, 4.8f),
        new Keyframe(0.72f, 0.93f, 0.65f, 0.65f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    public AnimationCurve aggressiveReturnCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 3.8f, 3.8f),
        new Keyframe(0.78f, 0.95f, 0.55f, 0.55f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Tooltip("Temporarily stretches the whale while it lunges, then restores its authored clone scale.")]
    [Range(0f, 0.35f)]
    public float aggressiveLungeScaleStrength = 0.12f;

    public bool aggressiveFaceMovementDirection = true;
    public bool aggressiveContinueIndividualRotation;
    public bool keepAnimatorPlayingDuringAggressiveDash = true;

    [Tooltip("Animator playback multiplier only during the aggressive out-and-back stage.")]
    [Range(0.1f, 3f)]
    public float aggressiveAnimatorSpeedMultiplier = 1.20f;

    [Header("Separation Movement")]
    [Min(0.01f)]
    public float globalSeparationSpeed = 10f;

    [Tooltip("0 uses speed. A value > 0 makes all clones use this duration unless a clone has its own duration override.")]
    [Min(0f)]
    public float globalSeparationDurationOverride;

    public AnimationCurve separationMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool rotateWhileMoving = true;

    [Tooltip("If true, each clone's Destination Wait is honored and the final stage begins when all clone holds finish. If false, per-clone waits are ignored and Delay After Last Clone Arrives is used instead.")]
    public bool waitForAllClones = true;

    [Min(0f)]
    public float delayAfterAllClonesReady = 0.2f;

    [Min(0f)]
    public float delayAfterLastCloneArrives = 0.35f;

    [Header("Final Center Merge")]
    public FishCloneTargetMode finalCenterMode =
        FishCloneTargetMode.NormalizedViewport;

    public Vector2 finalCenterViewport = new Vector2(0.5f, 0.5f);
    public Vector3 finalCenterWorldPosition;

    [Min(0.01f)]
    public float finalCenterMovementSpeed = 15f;

    [Tooltip("0 uses speed. A value > 0 synchronizes the merge duration for all clones.")]
    [Min(0f)]
    public float finalCenterDurationOverride;

    public AnimationCurve finalCenterMovementCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.4f),
        new Keyframe(1f, 1f, 2.6f, 0f)
    );

    public bool faceFinalCenterWhileMoving = true;

    public bool finalRotationEnabled;

    [Min(0f)]
    public float finalRotationSpeed = 180f;

    public bool finalRotationClockwise;

    [Min(0.01f)]
    public float finalScaleMultiplier = 0.88f;

    public AnimationCurve finalScaleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Final Merge Color")]
    public bool enableFinalMergeColorTint;
    public Color finalMergeTintColor = Color.white;

    [Range(0f, 1f)]
    public float finalMergeTintStrength = 0.35f;

    public AnimationCurve finalMergeTintCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Final Boom - Existing Indexed Net Boom")]
    public bool playIndexedNetBoom;

    [Min(0)]
    public int finalNetBoomEffectIndex;

    [Min(0.01f)]
    public float finalNetBoomScaleMultiplier = 1.4f;

    [Range(-500, 500)]
    public int finalNetBoomSortingOrderOffset = 160;

    [Header("Final Boom - Direct Prefab")]
    public GameObject finalBoomPrefab;
    public Vector2 finalBoomSpawnOffset;
    public Vector2 finalBoomScale = new Vector2(1.4f, 1.4f);
    public float finalBoomRotationDegrees;

    [Min(0f)]
    public float finalBoomVisibleDuration;

    [Min(0f)]
    public float finalBoomHideDelay = 0.15f;

    [Range(-500, 500)]
    public int finalBoomSortingOrderOffset = 180;

    [Header("Additional Final Prefabs")]
    public FishCloneCinematicFinalEffect[] additionalFinalEffects;

    [Header("Final Sound / Camera")]
    public bool playFinalNetBoomSound;

    [Tooltip("-1 uses SoundManager random/fallback Net Boom selection.")]
    public int finalNetBoomSoundIndex = -1;

    public bool playFinalCameraShake = true;

    [Min(0f)]
    public float finalCameraShakeStrength = 0.32f;

    [Min(0f)]
    public float finalCameraShakeDuration = 0.35f;

    [Header("Final Area Damage")]
    [Tooltip("Applied once at the shared center on the same frame as the final Boom. Uses the existing FishScript.TakeDamage path so other fish keep their normal HP/reward/death rules.")]
    public CinematicAreaDamageSettings finalAreaDamage =
        new CinematicAreaDamageSettings
        {
            enabled = true,
            damage = 1200f,
            radius = 3.2f,
            useDistanceFalloff = true,
            minimumFalloffMultiplier = 0.35f,
            maximumTargets = 16,
            smallMultiplier = 1.20f,
            mediumMultiplier = 0.70f,
            specialMultiplier = 0.42f,
            miniBossMultiplier = 0.18f,
            mainBossMultiplier = 0f,
            mainBossImmune = true,
            specialFishImmune = false,
            requestReaction = true,
            reactionDuration = 0.16f,
            reactionScaleStrength = 0.10f,
            reactionRotationDegrees = 42f,
            reactionRedStrength = 0.70f
        };

    [Header("Cleanup")]
    [Tooltip("Delay from the final Boom frame before the four visual clones are hidden.")]
    [Min(0f)]
    public float hideClonesAfterBoomDelay = 0.06f;

    [Tooltip("Extra time after clones hide before StandardDeathSequence returns the real fish to its existing pool.")]
    [Min(0f)]
    public float cleanupDelay = 0.45f;

    [Header("Per Clone Settings")]
    public FishCloneCinematicSlot[] clones =
    {
        new FishCloneCinematicSlot
        {
            destinationViewport = new Vector2(0.50f, 0.80f)
        },
        new FishCloneCinematicSlot
        {
            destinationViewport = new Vector2(0.80f, 0.50f)
        },
        new FishCloneCinematicSlot
        {
            destinationViewport = new Vector2(0.50f, 0.20f)
        },
        new FishCloneCinematicSlot
        {
            destinationViewport = new Vector2(0.20f, 0.50f)
        }
    };

    public FishCloneCinematicSlot GetCloneSettings(int index)
    {
        EnsureCloneSettingsCount();

        return index >= 0 && index < clones.Length
            ? clones[index]
            : null;
    }

    public void EnsureCloneSettingsCount()
    {
        cloneCount = Mathf.Clamp(cloneCount, 1, 16);

        if (clones == null)
        {
            clones = Array.Empty<FishCloneCinematicSlot>();
        }

        if (clones.Length < cloneCount)
        {
            int oldLength = clones.Length;
            Array.Resize(ref clones, cloneCount);

            for (int i = oldLength; i < clones.Length; i++)
            {
                float angle = formationStartingAngle -
                    (360f / Mathf.Max(1, cloneCount)) * i;
                float radians = angle * Mathf.Deg2Rad;

                clones[i] = new FishCloneCinematicSlot
                {
                    destinationViewport = new Vector2(
                        0.5f + Mathf.Cos(radians) * 0.30f,
                        0.5f + Mathf.Sin(radians) * 0.30f
                    )
                };
            }
        }

        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] == null)
            {
                clones[i] = new FishCloneCinematicSlot();
            }

            clones[i].scaleMultiplier = Mathf.Max(
                0.01f,
                clones[i].scaleMultiplier
            );
            clones[i].destinationWaitDuration = Mathf.Max(
                0f,
                clones[i].destinationWaitDuration
            );
        }
    }

    private void OnValidate()
    {
        cloneCount = Mathf.Clamp(cloneCount, 1, 16);
        baseCloneScaleMultiplier = Mathf.Max(0.01f, baseCloneScaleMultiplier);
        animatorSpeedMultiplier = Mathf.Max(0.01f, animatorSpeedMultiplier);
        formationRadius = Mathf.Max(0f, formationRadius);
        formationBuildDuration = Mathf.Max(0f, formationBuildDuration);
        formationRotationSpeed = Mathf.Max(0f, formationRotationSpeed);
        spinWaitDuration = Mathf.Max(0f, spinWaitDuration);
        formationRotationDuration = Mathf.Max(0f, formationRotationDuration);
        maximumFormationTurns = Mathf.Max(0f, maximumFormationTurns);
        individualFishRotationSpeed = Mathf.Max(0f, individualFishRotationSpeed);
        cloneShadowOpacity = Mathf.Clamp01(cloneShadowOpacity);
        aggressiveOutAndBackRepeats = Mathf.Clamp(aggressiveOutAndBackRepeats, 1, 8);
        aggressiveOutScreenViewportPadding = Mathf.Clamp(aggressiveOutScreenViewportPadding, 0f, 0.50f);
        aggressiveWindUpFormationRadiusMultiplier = Mathf.Clamp(aggressiveWindUpFormationRadiusMultiplier, 0f, 0.60f);
        aggressiveWindUpDuration = Mathf.Max(0f, aggressiveWindUpDuration);
        aggressiveOutboundSpeed = Mathf.Max(0.01f, aggressiveOutboundSpeed);
        aggressiveReturnSpeed = Mathf.Max(0.01f, aggressiveReturnSpeed);
        aggressiveOutsideHoldDuration = Mathf.Max(0f, aggressiveOutsideHoldDuration);
        aggressiveRepeatSpeedMultiplier = Mathf.Clamp(aggressiveRepeatSpeedMultiplier, 1f, 2f);
        aggressiveLungeScaleStrength = Mathf.Clamp(aggressiveLungeScaleStrength, 0f, 0.35f);
        aggressiveAnimatorSpeedMultiplier = Mathf.Clamp(aggressiveAnimatorSpeedMultiplier, 0.1f, 3f);
        globalSeparationSpeed = Mathf.Max(0.01f, globalSeparationSpeed);
        globalSeparationDurationOverride = Mathf.Max(0f, globalSeparationDurationOverride);
        finalCenterMovementSpeed = Mathf.Max(0.01f, finalCenterMovementSpeed);
        finalCenterDurationOverride = Mathf.Max(0f, finalCenterDurationOverride);
        finalScaleMultiplier = Mathf.Max(0.01f, finalScaleMultiplier);
        finalNetBoomScaleMultiplier = Mathf.Max(0.01f, finalNetBoomScaleMultiplier);
        cleanupDelay = Mathf.Max(0f, cleanupDelay);
        hideClonesAfterBoomDelay = Mathf.Max(0f, hideClonesAfterBoomDelay);
        additionalStartDelay = Mathf.Max(0f, additionalStartDelay);
        EnsureCloneSettingsCount();
    }
}
