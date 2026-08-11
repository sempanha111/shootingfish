#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Editor-only, idempotent setup for the recommended 46-fish array.
/// Fish[] is the source of truth: array index becomes prefab ID.
/// </summary>
public static class FishArcade46Setup
{
    private const int RequiredFishCount = 46;
    private const string DataRoot =
        "Assets/FishArcadeGenerated/Recommended46";
    private const string MovementRoot = DataRoot + "/MovementProfiles";
    private const string DeathRoot = DataRoot + "/DeathProfiles";
    private const string EpicRoot = DataRoot + "/EpicBossProfiles";

    private sealed class GeneratedProfiles
    {
        public FishMovementProfile smallCruiser;
        public FishMovementProfile smallDarter;
        public FishMovementProfile rayGlider;
        public FishMovementProfile pulseCreature;
        public FishMovementProfile oceanHunter;
        public FishMovementProfile miniBoss;
        public FishMovementProfile mainBoss;

        public FishDeathProfile smallDeath;
        public FishDeathProfile mediumDeath;
        public FishDeathProfile specialDeath;
        public FishDeathProfile miniBossDeath;
        public FishDeathProfile mainBossDeath;
        public FishDeathProfile epicBossDeath;

        public readonly EpicBossProfile[] epicBossProfiles =
            new EpicBossProfile[3];
    }

    private enum DeathPreset
    {
        Small,
        Medium,
        Special,
        MiniBoss,
        MainBoss,
        EpicBoss
    }

    [MenuItem(
        "Tools/Fish Arcade/Apply Complete Recommended 46-Fish Setup"
    )]
    private static void ApplyFromToolsMenu()
    {
        ApplyCompleteSetupFromSelection();
    }

    public static void ApplyCompleteSetupFromSelection()
    {
        SwapFishScript director = FindSelectedOrSceneDirector();

        if (director == null)
        {
            Debug.LogError(
                "No SwapFishScript was found. Select the director object " +
                "in the Hierarchy and run the command again."
            );
            return;
        }

        ApplyCompleteSetup(director);
    }

    [MenuItem(
        "CONTEXT/SwapFishScript/Apply Complete Recommended 46-Fish Setup"
    )]
    private static void ApplyFromComponentMenu(MenuCommand command)
    {
        ApplyCompleteSetup(command.context as SwapFishScript);
    }

    [MenuItem("CONTEXT/SwapFishScript/Validate Recommended 46-Fish Setup")]
    private static void ValidateFromComponentMenu(MenuCommand command)
    {
        ValidateSetup(command.context as SwapFishScript, true);
    }

    public static void ApplyCompleteSetup(SwapFishScript director)
    {
        string validationError;

        if (!TryValidateSourceArray(director, out validationError))
        {
            Debug.LogError(validationError, director);
            EditorUtility.DisplayDialog(
                "46-Fish Setup Stopped",
                validationError,
                "OK"
            );
            return;
        }

        int configuredCount = 0;

        try
        {
            EnsureFolder(DataRoot);
            EnsureFolder(MovementRoot);
            EnsureFolder(DeathRoot);
            EnsureFolder(EpicRoot);
            EnsureSortingLayer("Fish");

            GeneratedProfiles profiles = CreateOrUpdateProfiles();

            Undo.RecordObject(
                director,
                "Apply Complete Recommended 46-Fish Setup"
            );

            director.ApplyRecommended46FishDirectorPreset();

            for (int i = 0; i < RequiredFishCount; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Applying Recommended 46-Fish Setup",
                    "Configuring Fish[" + i + "] " +
                    director.Fish[i].name,
                    i / (float)RequiredFishCount
                );

                ConfigurePrefab(
                    director.Fish[i],
                    i,
                    profiles
                );

                configuredCount++;
            }

            // The recommended gimmick species are already part of Fish[].
            // Reusing those prefab references avoids a second manual lookup.
            director.BombCrabPrefab = director.Fish[30];
            director.LightningChainPrefab = director.Fish[34];
            EditorUtility.SetDirty(director);

            if (director.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    director.gameObject.scene
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateSetup(director, false);

            Debug.Log(
                "Complete 46-fish setup finished: " + configuredCount +
                " prefab IDs/defaults applied, movement/death profiles " +
                "assigned, three Epic profiles assigned, director preset " +
                "applied, and existing player point settings preserved.",
                director
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, director);
            Debug.LogError(
                "46-fish setup stopped after " + configuredCount +
                " prefabs. Fix the reported error and run the same command " +
                "again; the setup is safe to rerun.",
                director
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ConfigurePrefab(
        GameObject prefab,
        int fishId,
        GeneratedProfiles profiles
    )
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            prefabPath
        );

        try
        {
            FishScript fish = prefabRoot.GetComponent<FishScript>();

            if (fish == null)
            {
                throw new InvalidOperationException(
                    prefabPath +
                    " has no FishScript on its root GameObject."
                );
            }

            if (!fish.ApplyRecommended46FishPrefabDefaultsForId(fishId))
            {
                throw new InvalidOperationException(
                    "Recommended values were rejected for Fish[" +
                    fishId + "]."
                );
            }

            SerializedObject serializedFish = new SerializedObject(fish);
            SetObjectReference(
                serializedFish,
                "movementProfile",
                SelectMovementProfile(profiles, fishId)
            );
            SetObjectReference(
                serializedFish,
                "deathProfile",
                SelectDeathProfile(profiles, fishId)
            );
            serializedFish.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fish);

            if (prefabRoot.GetComponent<SortingGroup>() == null)
            {
                prefabRoot.AddComponent<SortingGroup>();
            }

            if (fishId >= 43)
            {
                ConfigureEpicBoss(
                    prefabRoot,
                    profiles.epicBossProfiles[fishId - 43]
                );
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureEpicBoss(
        GameObject prefabRoot,
        EpicBossProfile profile
    )
    {
        EpicBossController controller =
            prefabRoot.GetComponent<EpicBossController>();

        if (controller == null)
        {
            controller = prefabRoot.AddComponent<EpicBossController>();
        }

        SerializedObject serializedController =
            new SerializedObject(controller);

        SetObjectReference(serializedController, "profile", profile);

        SetObjectReferenceIfEmpty(
            serializedController,
            "body",
            prefabRoot.GetComponent<Rigidbody2D>()
        );
        SetObjectReferenceIfEmpty(
            serializedController,
            "bossAnimator",
            prefabRoot.GetComponent<Animator>()
        );
        SetObjectReferenceIfEmpty(
            serializedController,
            "visualRoot",
            prefabRoot.transform
        );
        SetObjectReferenceIfEmpty(
            serializedController,
            "bossAudioSource",
            prefabRoot.GetComponent<AudioSource>()
        );

        SerializedProperty collidersProperty =
            serializedController.FindProperty("bossColliders");

        if (collidersProperty != null &&
            (collidersProperty.arraySize == 0 ||
             HasOnlyNullReferences(collidersProperty)))
        {
            Collider2D[] colliders =
                prefabRoot.GetComponentsInChildren<Collider2D>(true);

            collidersProperty.arraySize = colliders.Length;

            for (int i = 0; i < colliders.Length; i++)
            {
                collidersProperty.GetArrayElementAtIndex(i)
                    .objectReferenceValue = colliders[i];
            }
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static bool HasOnlyNullReferences(
        SerializedProperty arrayProperty
    )
    {
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i)
                    .objectReferenceValue != null)
            {
                return false;
            }
        }

        return true;
    }

    private static GeneratedProfiles CreateOrUpdateProfiles()
    {
        GeneratedProfiles result = new GeneratedProfiles();

        result.smallCruiser = CreateOrUpdateMovement(
            "SmallCruiser",
            new[]
            {
                W(FishScript.SwimStyle.LaneGlide, 50f),
                W(FishScript.SwimStyle.ArcSweep, 30f),
                W(FishScript.SwimStyle.SchoolFollow, 20f)
            },
            new[]
            {
                W(FishScript.SwimStyle.ArcSweep, 30f),
                W(FishScript.SwimStyle.ZigZagBurst, 30f),
                W(FishScript.SwimStyle.HorizontalRush, 25f),
                W(FishScript.SwimStyle.VerticalDive, 15f)
            },
            new[]
            {
                W(FishScript.SwimStyle.ZigZagBurst, 45f),
                W(FishScript.SwimStyle.HorizontalRush, 35f),
                W(FishScript.SwimStyle.VerticalDive, 20f)
            }
        );

        result.smallDarter = CreateOrUpdateMovement(
            "SmallDarter",
            new[]
            {
                W(FishScript.SwimStyle.LaneGlide, 35f),
                W(FishScript.SwimStyle.ZigZagBurst, 30f),
                W(FishScript.SwimStyle.HorizontalRush, 20f),
                W(FishScript.SwimStyle.ArcSweep, 15f)
            },
            new[]
            {
                W(FishScript.SwimStyle.ZigZagBurst, 40f),
                W(FishScript.SwimStyle.HorizontalRush, 35f),
                W(FishScript.SwimStyle.VerticalDive, 25f)
            },
            new[]
            {
                W(FishScript.SwimStyle.HorizontalRush, 45f),
                W(FishScript.SwimStyle.ZigZagBurst, 40f),
                W(FishScript.SwimStyle.VerticalDive, 15f)
            }
        );

        result.rayGlider = CreateOrUpdateMovement(
            "RayGlider",
            new[]
            {
                W(FishScript.SwimStyle.ArcSweep, 50f),
                W(FishScript.SwimStyle.SpiralCross, 25f),
                W(FishScript.SwimStyle.LaneGlide, 15f),
                W(FishScript.SwimStyle.VerticalDive, 10f)
            },
            new[]
            {
                W(FishScript.SwimStyle.ArcSweep, 30f),
                W(FishScript.SwimStyle.SpiralCross, 30f),
                W(FishScript.SwimStyle.VerticalDive, 25f),
                W(FishScript.SwimStyle.ZigZagBurst, 15f)
            },
            new[]
            {
                W(FishScript.SwimStyle.SpiralCross, 35f),
                W(FishScript.SwimStyle.VerticalDive, 30f),
                W(FishScript.SwimStyle.ZigZagBurst, 25f),
                W(FishScript.SwimStyle.ArcSweep, 10f)
            }
        );

        result.pulseCreature = CreateOrUpdateMovement(
            "PulseCreature",
            new[]
            {
                W(FishScript.SwimStyle.VerticalDive, 45f),
                W(FishScript.SwimStyle.ArcSweep, 30f),
                W(FishScript.SwimStyle.SpiralCross, 25f)
            },
            new[]
            {
                W(FishScript.SwimStyle.VerticalDive, 35f),
                W(FishScript.SwimStyle.SpiralCross, 35f),
                W(FishScript.SwimStyle.ZigZagBurst, 30f)
            },
            new[]
            {
                W(FishScript.SwimStyle.VerticalDive, 40f),
                W(FishScript.SwimStyle.ZigZagBurst, 35f),
                W(FishScript.SwimStyle.SpiralCross, 25f)
            }
        );

        result.oceanHunter = CreateOrUpdateMovement(
            "OceanHunter",
            new[]
            {
                W(FishScript.SwimStyle.LaneGlide, 35f),
                W(FishScript.SwimStyle.HorizontalRush, 30f),
                W(FishScript.SwimStyle.ArcSweep, 20f),
                W(FishScript.SwimStyle.ZigZagBurst, 15f)
            },
            new[]
            {
                W(FishScript.SwimStyle.HorizontalRush, 45f),
                W(FishScript.SwimStyle.ZigZagBurst, 30f),
                W(FishScript.SwimStyle.VerticalDive, 15f),
                W(FishScript.SwimStyle.ArcSweep, 10f)
            },
            new[]
            {
                W(FishScript.SwimStyle.HorizontalRush, 50f),
                W(FishScript.SwimStyle.ZigZagBurst, 35f),
                W(FishScript.SwimStyle.VerticalDive, 15f)
            }
        );

        result.miniBoss = CreateOrUpdateMovement(
            "MiniBossProfile",
            new[]
            {
                W(FishScript.SwimStyle.MiniBossHunter, 50f),
                W(FishScript.SwimStyle.MiniBossOrbit, 30f),
                W(FishScript.SwimStyle.ZigZagBurst, 20f)
            },
            new[]
            {
                W(FishScript.SwimStyle.MiniBossCharge, 45f),
                W(FishScript.SwimStyle.MiniBossHunter, 35f),
                W(FishScript.SwimStyle.ZigZagBurst, 20f)
            },
            new[]
            {
                W(FishScript.SwimStyle.CriticalStagger, 60f),
                W(FishScript.SwimStyle.MiniBossCharge, 25f),
                W(FishScript.SwimStyle.MiniBossHunter, 15f)
            }
        );

        result.mainBoss = CreateOrUpdateMovement(
            "MainBossProfile",
            new[]
            {
                W(FishScript.SwimStyle.BossPatrol, 55f),
                W(FishScript.SwimStyle.BossFigureEight, 30f),
                W(FishScript.SwimStyle.BossOrbit, 15f)
            },
            new[]
            {
                W(FishScript.SwimStyle.BossCharge, 40f),
                W(FishScript.SwimStyle.BossPatrol, 35f),
                W(FishScript.SwimStyle.BossFigureEight, 25f)
            },
            new[]
            {
                W(FishScript.SwimStyle.CriticalStagger, 60f),
                W(FishScript.SwimStyle.BossCharge, 20f),
                W(FishScript.SwimStyle.BossPatrol, 20f)
            }
        );

        result.smallDeath = CreateOrUpdateDeath(
            "SmallDeath",
            DeathPreset.Small
        );
        result.mediumDeath = CreateOrUpdateDeath(
            "MediumDeath",
            DeathPreset.Medium
        );
        result.specialDeath = CreateOrUpdateDeath(
            "SpecialDeath",
            DeathPreset.Special
        );
        result.miniBossDeath = CreateOrUpdateDeath(
            "MiniBossDeath",
            DeathPreset.MiniBoss
        );
        result.mainBossDeath = CreateOrUpdateDeath(
            "MainBossDeath",
            DeathPreset.MainBoss
        );
        result.epicBossDeath = CreateOrUpdateDeath(
            "EpicBossDeath",
            DeathPreset.EpicBoss
        );

        for (int i = 0; i < result.epicBossProfiles.Length; i++)
        {
            result.epicBossProfiles[i] =
                CreateOrUpdateEpicBossProfile(43 + i);
        }

        return result;
    }

    private static FishMovementProfile CreateOrUpdateMovement(
        string assetName,
        WeightedSwimStyle[] healthy,
        WeightedSwimStyle[] aggressive,
        WeightedSwimStyle[] critical
    )
    {
        string path = MovementRoot + "/" + assetName + ".asset";
        FishMovementProfile profile = LoadOrCreate<
            FishMovementProfile
        >(path);

        profile.healthyStyles = healthy;
        profile.aggressiveStyles = aggressive;
        profile.criticalStyles = critical;
        ConfigureOrganicMovement(profile, assetName);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ConfigureOrganicMovement(
        FishMovementProfile profile,
        string assetName
    )
    {
        bool miniBoss = assetName == "MiniBossProfile";
        bool mainBoss = assetName == "MainBossProfile";
        bool boss = miniBoss || mainBoss;
        bool darter = assetName == "SmallDarter";
        bool ray = assetName == "RayGlider";
        bool pulse = assetName == "PulseCreature";
        bool hunter = assetName == "OceanHunter";

        profile.enableOrganicSteering = true;
        profile.velocitySmoothTime = mainBoss
            ? 0.30f
            : miniBoss
                ? 0.25f
                : ray || pulse
                    ? 0.21f
                    : darter || hunter
                        ? 0.14f
                        : 0.18f;

        profile.healthyTurnDegreesPerSecond = mainBoss
            ? 46f
            : miniBoss
                ? 58f
                : ray || pulse
                    ? 66f
                    : darter
                        ? 112f
                        : hunter
                            ? 92f
                            : 78f;
        profile.aggressiveTurnDegreesPerSecond =
            profile.healthyTurnDegreesPerSecond * 1.24f;
        profile.criticalTurnDegreesPerSecond =
            profile.healthyTurnDegreesPerSecond * 1.45f;

        profile.minimumWaypointViewportDistance = boss
            ? 0.56f
            : ray
                ? 0.54f
                : darter
                    ? 0.42f
                    : 0.48f;
        profile.waypointReachDistance = boss ? 0.70f : 0.48f;
        profile.waypointLifetimeRange = boss
            ? new Vector2(5f, 8.5f)
            : darter
                ? new Vector2(2.8f, 5f)
                : new Vector2(3.8f, 7.2f);

        profile.wanderAngleDegrees = mainBoss
            ? 4f
            : miniBoss
                ? 5.5f
                : ray
                    ? 6.5f
                    : pulse
                        ? 11f
                        : darter
                            ? 12f
                            : 8.5f;
        profile.wanderFrequency = boss
            ? 0.18f
            : ray
                ? 0.24f
                : darter
                    ? 0.42f
                    : 0.32f;
        profile.speedPulseMultiplierRange = boss
            ? new Vector2(0.90f, 1.07f)
            : pulse
                ? new Vector2(0.84f, 1.14f)
                : new Vector2(0.90f, 1.10f);
        profile.speedPulseDurationRange = boss
            ? new Vector2(4.8f, 7.8f)
            : pulse
                ? new Vector2(2.1f, 4.2f)
                : new Vector2(2.8f, 5.4f);

        profile.styleHoldDurationRange = boss
            ? new Vector2(7f, 11f)
            : darter
                ? new Vector2(3.6f, 6.2f)
                : new Vector2(4.5f, 8f);
        profile.styleChangeChanceAfterHold = boss ? 0.34f : 0.45f;

        profile.spreadRoutesAcrossViewport = true;
        profile.centerAvoidanceHalfHeight = boss ? 0.17f : 0.14f;
        profile.trafficLaneJitter = boss ? 0.035f : 0.055f;
        profile.crossCenterLaneChangeChance = boss ? 0.04f : 0.08f;
        profile.orbitAnchorViewportInset = boss ? 0.30f : 0.27f;

        profile.offscreenLoopChance = mainBoss
            ? 0.08f
            : miniBoss
                ? 0.10f
                : ray
                    ? 0.26f
                    : darter
                        ? 0.18f
                        : hunter
                            ? 0.16f
                            : 0.22f;
        profile.maximumOffscreenLoops = 1;
        profile.offscreenViewportPadding = boss ? 0.14f : 0.10f;
        profile.offscreenReturnTimeout = boss ? 12f : 9f;
        profile.loopCooldown = boss ? 12f : 7f;
        profile.naturalLifetimeRange = boss
            ? new Vector2(34f, 50f)
            : new Vector2(16f, 28f);

        profile.stuckCheckInterval = boss ? 1.35f : 1.15f;
        profile.minimumStuckTravelDistance = boss ? 0.07f : 0.10f;
        profile.stuckRecoverySpeedMultiplier = boss ? 0.95f : 0.85f;

        profile.enableReactiveHitMotion = true;
        profile.microConvulsionChancePerHit = boss
            ? 0.42f
            : darter
                ? 0.25f
                : 0.32f;
        profile.repeatedMicroConvulsionChance = boss ? 0.72f : 0.68f;
        profile.sustainedFireHitCount = 2;
        profile.sustainedFireWindow = 0.55f;
        profile.fireReleaseGracePeriod = 0.34f;
        profile.microConvulsionDurationRange = boss
            ? new Vector2(0.07f, 0.15f)
            : new Vector2(0.055f, 0.13f);
        profile.microConvulsionRecoveryRange =
            new Vector2(0.07f, 0.18f);
        profile.microConvulsionScaleStrength = boss ? 0.05f : 0.035f;
        profile.reactionMovementSpeedMultiplier = boss ? 0.72f : 0.78f;
        profile.microConvulsionAnimatorTrigger = "HitFlinch";
        profile.sustainedFireAnimatorBool = "UnderFire";
    }

    private static FishDeathProfile CreateOrUpdateDeath(
        string assetName,
        DeathPreset preset
    )
    {
        string path = DeathRoot + "/" + assetName + ".asset";
        FishDeathProfile profile = LoadOrCreate<FishDeathProfile>(path);

        profile.rewardCalculationMode =
            FishRewardCalculationMode.FixedFishCoin;
        profile.rewardMultiplier = 1f;
        profile.maximumSmallShotBonusPercent = 0f;
        profile.minimumReward = 0f;
        profile.maximumReward = 0f;
        profile.mainBossFrontGunSkillIndex = -1;
        profile.bigWinSoundIndex = -1;

        if (preset == DeathPreset.Small)
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.Small,
                true, 1, 0f, 0.10f,
                false, false, 0,
                false, 0,
                false, 0f, 0f, 0.10f
            );
        }
        else if (preset == DeathPreset.Medium)
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.Big,
                true, 1, 0f, 0.16f,
                false, false, 0,
                true, 0,
                false, 0.04f, 5000f, 0.12f
            );
        }
        else if (preset == DeathPreset.Special)
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.Big,
                true, 2, 0.08f, 0.30f,
                false, false, 0,
                true, 1,
                true, 0.25f, 20000f, 0.18f
            );
        }
        else if (preset == DeathPreset.MiniBoss)
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.Big,
                true, 3, 0.10f, 0.45f,
                false, false, 0,
                true, 2,
                true, 0.60f, 60000f, 0.22f
            );
        }
        else if (preset == DeathPreset.MainBoss)
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.None,
                false, 1, 0f, 0f,
                true, true, -1,
                false, 0,
                true, 1f, 140000f, 0.30f
            );
        }
        else
        {
            ConfigureDeath(
                profile,
                RewardTextStyle.None,
                false, 1, 0f, 0f,
                true, true, -1,
                true, 3,
                true, 1f, 450000f, 0.35f
            );
        }

        ConfigureCinematicDeath(profile, preset);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ConfigureDeath(
        FishDeathProfile profile,
        RewardTextStyle textStyle,
        bool standardCoins,
        int coinCount,
        float coinInterval,
        float coinSpread,
        bool frontGunSkill,
        bool bossCoinBurst,
        int bossCoinBurstIndex,
        bool netBoom,
        int netBoomIndex,
        bool bigWin,
        float certificateChance,
        float certificateMinimum,
        float effectHideDelay
    )
    {
        profile.rewardTextStyle = textStyle;
        profile.playStandardCoinAnimation = standardCoins;
        profile.coinAnimationPlayCount = coinCount;
        profile.coinAnimationInterval = coinInterval;
        profile.coinAnimationSpreadRadius = coinSpread;
        profile.playMainBossSkillInFrontGun = frontGunSkill;
        profile.playBossCoinBurst = bossCoinBurst;
        profile.bossCoinBurstEffectIndex = bossCoinBurstIndex;
        profile.playNetBoom = netBoom;
        profile.netBoomEffectIndex = netBoomIndex;
        profile.playBigWinSound = bigWin;
        profile.certificateChance = certificateChance;
        profile.minimumCertificateWin = certificateMinimum;
        profile.customEffectHideDelay = effectHideDelay;
    }

    private static void ConfigureCinematicDeath(
        FishDeathProfile profile,
        DeathPreset preset
    )
    {
        profile.enableCinematicDeathMotion = true;
        profile.deathAnimatorTrigger = "Death";
        profile.fadeSpriteRenderers = true;
        profile.disableCollidersDuringDeath = true;

        if (preset == DeathPreset.Small)
        {
            profile.deathDuration = 0.24f;
            profile.rewardBeatNormalized = 0.22f;
            profile.impactScaleMultiplier = 1.07f;
            profile.deathDriftDistance = 0.16f;
            profile.deathTumbleDegrees = 10f;
            profile.fadeStartNormalized = 0.58f;
            profile.deathCameraShakeStrength = 0f;
            profile.deathCameraShakeDuration = 0f;
            profile.customEffectPulseCount = 1;
            profile.customEffectPulseInterval = 0.10f;
        }
        else if (preset == DeathPreset.Medium)
        {
            profile.deathDuration = 0.38f;
            profile.rewardBeatNormalized = 0.25f;
            profile.impactScaleMultiplier = 1.10f;
            profile.deathDriftDistance = 0.25f;
            profile.deathTumbleDegrees = 18f;
            profile.fadeStartNormalized = 0.55f;
            profile.deathCameraShakeStrength = 0f;
            profile.deathCameraShakeDuration = 0f;
            profile.customEffectPulseCount = 1;
            profile.customEffectPulseInterval = 0.12f;
        }
        else if (preset == DeathPreset.Special)
        {
            profile.deathDuration = 0.68f;
            profile.rewardBeatNormalized = 0.28f;
            profile.impactScaleMultiplier = 1.14f;
            profile.deathDriftDistance = 0.38f;
            profile.deathTumbleDegrees = 28f;
            profile.fadeStartNormalized = 0.60f;
            profile.deathCameraShakeStrength = 0.02f;
            profile.deathCameraShakeDuration = 0.16f;
            profile.customEffectPulseCount = 2;
            profile.customEffectPulseInterval = 0.16f;
        }
        else if (preset == DeathPreset.MiniBoss)
        {
            profile.deathDuration = 0.95f;
            profile.rewardBeatNormalized = 0.32f;
            profile.impactScaleMultiplier = 1.18f;
            profile.deathDriftDistance = 0.52f;
            profile.deathTumbleDegrees = 38f;
            profile.fadeStartNormalized = 0.64f;
            profile.deathCameraShakeStrength = 0.045f;
            profile.deathCameraShakeDuration = 0.25f;
            profile.customEffectPulseCount = 3;
            profile.customEffectPulseInterval = 0.18f;
        }
        else
        {
            profile.deathDuration = preset == DeathPreset.EpicBoss
                ? 1.50f
                : 1.35f;
            profile.rewardBeatNormalized = 0.38f;
            profile.impactScaleMultiplier = 1.20f;
            profile.deathDriftDistance = 0.68f;
            profile.deathTumbleDegrees = 48f;
            profile.fadeStartNormalized = 0.68f;
            profile.deathCameraShakeStrength =
                preset == DeathPreset.EpicBoss ? 0.09f : 0.07f;
            profile.deathCameraShakeDuration =
                preset == DeathPreset.EpicBoss ? 0.50f : 0.38f;
            profile.customEffectPulseCount = 3;
            profile.customEffectPulseInterval = 0.22f;
        }
    }

    private static EpicBossProfile CreateOrUpdateEpicBossProfile(
        int fishId
    )
    {
        string path = EpicRoot + "/EpicBoss_" + fishId + ".asset";
        EpicBossProfile profile = LoadOrCreate<EpicBossProfile>(path);

        profile.applyAdditionalStatMultipliers = false;
        profile.hpMultiplier = 1f;
        profile.rewardMultiplier = 1f;
        profile.movementSpeedMultiplier = 1f;

        profile.entranceDuration = 3.2f;
        profile.entranceSpeedMultiplier = 0.50f;
        profile.entranceTargetHorizontalViewport = 0.28f;
        profile.entranceTargetVerticalViewport = 0.50f;
        profile.introductionHoldDuration = 0.80f;
        profile.requestDirectorIntroductionParade = true;
        profile.introductionParadeWaveCount = 1;
        profile.introductionParadeWaveGap = 0.75f;
        profile.introductionAnimatorTrigger = "BossIntro";
        profile.introductionEffectHideDelay = 0.20f;

        profile.slowSpeedMultiplier = 0.38f;
        profile.fastSpeedMultiplier = 0.85f;
        profile.speedCycleDurationRange = new Vector2(5f, 8f);
        profile.smoothTurnDegreesPerSecond = 38f;
        profile.velocitySmoothTime = 0.30f;
        profile.roamWanderAngleDegrees = 4.5f;
        profile.roamWanderFrequency = 0.18f;
        profile.viewportBoundaryPadding = 0.15f;
        profile.emergencyOutsideTolerance = 0.07f;
        profile.targetReachDistance = 0.65f;
        profile.targetChangeDelayRange = new Vector2(3f, 6f);
        profile.targetPauseRange = new Vector2(0.15f, 0.40f);
        profile.largeTurnChance = 0.55f;
        profile.minimumLargeTurnAngle = 60f;
        profile.minimumTargetViewportDistance = 0.42f;
        profile.avoidCentralArenaPocket = true;
        profile.centerAvoidanceHalfWidth = 0.16f;
        profile.centerAvoidanceHalfHeight = 0.18f;
        profile.centerTargetChance = 0.08f;
        profile.boundaryReturnSpeedMultiplier = 1.05f;

        profile.aggressiveMovementHealthThreshold = 0.58f;
        profile.criticalMovementHealthThreshold = 0.24f;
        profile.aggressiveMovementSpeedMultiplier = 1.12f;
        profile.criticalMovementSpeedMultiplier = 1.24f;
        profile.criticalTurnSpeedMultiplier = 1.25f;
        profile.stuckCheckInterval = 1.25f;
        profile.minimumStuckTravelDistance = 0.08f;

        profile.enableCinematicOffscreenPasses = true;
        profile.cinematicPassChance = 0.16f;
        profile.cinematicPassIntervalRange = new Vector2(16f, 25f);
        profile.cinematicPassOutsideViewportPadding = 0.10f;
        profile.cinematicPassSpeedMultiplier = 0.95f;
        profile.cinematicOutsideArcSpeedMultiplier = 2.10f;
        profile.cinematicReturnArcVerticalShift = 0.30f;
        profile.cinematicPassMaximumDuration = 10f;

        profile.firstConvulsionThreshold = 0.80f;
        profile.fakeDeathThreshold = 0.62f;
        profile.strongConvulsionThreshold = 0.32f;
        profile.firstConvulsionAnimatorTrigger = "BossConvulsion";
        profile.firstConvulsionDuration = 0.45f;
        profile.firstConvulsionPositionStrength = 0.06f;
        profile.firstConvulsionRotationStrength = 4f;

        profile.fakeDeathAnimatorTrigger = "BossFakeDeath";
        profile.fakeDeathDuration = 1.10f;
        profile.fakeRewardMode =
            EpicBossFakeRewardMode.PercentageOfFinalReward;
        profile.fakeRewardPercentOfFinalReward = 0.02f;
        profile.fakeRewardMaximum = 8000f;
        profile.fakeRewardCollectibleChance = 0.35f;
        profile.showFakeRewardText = true;
        profile.revealVisualOnlyRewardWithQuestionMark = true;
        profile.fakeRewardCoinAnimationCount = 2;
        profile.fakeRewardCoinAnimationInterval = 0.12f;
        profile.fakeRewardCoinAnimationSpread = 0.45f;
        profile.fakeRewardPlayBossCoinBurst = true;
        profile.fakeRewardBossCoinBurstEffectIndex = 0;
        profile.fakeRewardPlayNetBoom = true;
        profile.fakeRewardNetBoomIndex = 0;

        profile.strongConvulsionAnimatorTrigger =
            "BossStrongConvulsion";
        profile.strongConvulsionDuration = 0.75f;
        profile.strongConvulsionPositionStrength = 0.10f;
        profile.strongConvulsionRotationStrength = 7f;

        profile.enableReactiveFireConvulsions = true;
        profile.sustainedFireHitCount = 2;
        profile.sustainedFireWindow = 0.55f;
        profile.fireReleaseGracePeriod = 0.34f;
        profile.microConvulsionChancePerHit = 0.42f;
        profile.repeatMicroConvulsionChance = 0.72f;
        profile.microConvulsionDurationRange =
            new Vector2(0.07f, 0.15f);
        profile.microConvulsionRecoveryRange =
            new Vector2(0.08f, 0.18f);
        profile.microConvulsionPositionStrength = 0.025f;
        profile.microConvulsionScaleStrength = 0.05f;
        profile.microConvulsionRotationStrength = 2.5f;
        profile.reactiveMovementSpeedMultiplier = 0.72f;
        profile.reactiveConvulsionAnimatorTrigger = "BossHitFlinch";
        profile.sustainedFireAnimatorBool = "BossUnderFire";

        profile.enableRandomSkills = true;
        profile.firstSkillDelayRange = new Vector2(7f, 11f);
        profile.skillIntervalRange = new Vector2(10f, 16f);
        profile.preventImmediateSkillRepeat = true;
        profile.randomSkills = BuildEpicSkills(
            fishId,
            profile.randomSkills
        );

        profile.deathAnimatorTrigger = "BossDeath";
        profile.deathAnimationDuration = 2.4f;
        profile.disableCollidersDuringDeath = true;
        profile.deathEffectVisibleDuration = 2f;
        profile.deathEffectHideDelay = 0.25f;
        profile.deathCameraShakeStrength = 0.10f;
        profile.deathCameraShakeDuration = 0.70f;
        profile.deathImpactScaleMultiplier = 1.16f;
        profile.deathTumbleDegrees = 52f;
        profile.deathDriftDistance = 0.70f;
        profile.deathFadeStartNormalized = 0.62f;
        profile.deathEffectPulseCount = 3;
        profile.deathEffectPulseInterval = 0.38f;
        profile.finalDeathShakeMultiplier = 0.55f;

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static EpicBossSkillEntry[] BuildEpicSkills(
        int fishId,
        EpicBossSkillEntry[] existing
    )
    {
        EpicBossSkillType[] allTypes =
        {
            EpicBossSkillType.Earthquake,
            EpicBossSkillType.HeavyRoar,
            EpicBossSkillType.AccelerationBurst,
            EpicBossSkillType.AggressiveCharge,
            EpicBossSkillType.WaterShockwave,
            EpicBossSkillType.DramaticCameraShake
        };

        EpicBossSkillEntry[] result =
            new EpicBossSkillEntry[allTypes.Length];

        for (int i = 0; i < allTypes.Length; i++)
        {
            EpicBossSkillEntry entry = FindSkill(existing, allTypes[i]);

            if (entry == null)
            {
                entry = new EpicBossSkillEntry();
            }

            entry.skillType = allTypes[i];
            entry.enabled = false;
            entry.weight = 0f;
            entry.windUpDuration = 0.40f;
            entry.recoveryDuration = 0.50f;
            ApplySkillActionDefaults(entry);
            result[i] = entry;
        }

        if (fishId == 43)
        {
            EnableSkill(result, EpicBossSkillType.Earthquake, 2f);
            EnableSkill(result, EpicBossSkillType.HeavyRoar, 1f);
            EnableSkill(result, EpicBossSkillType.WaterShockwave, 1f);
        }
        else if (fishId == 44)
        {
            EnableSkill(
                result,
                EpicBossSkillType.AccelerationBurst,
                2f
            );
            EnableSkill(
                result,
                EpicBossSkillType.AggressiveCharge,
                1.5f
            );
            EnableSkill(result, EpicBossSkillType.WaterShockwave, 1f);
        }
        else
        {
            EnableSkill(result, EpicBossSkillType.WaterShockwave, 2f);
            EnableSkill(
                result,
                EpicBossSkillType.DramaticCameraShake,
                1.5f
            );
            EnableSkill(result, EpicBossSkillType.HeavyRoar, 1f);
            EnableSkill(result, EpicBossSkillType.Earthquake, 1f);
        }

        return result;
    }

    private static EpicBossSkillEntry FindSkill(
        EpicBossSkillEntry[] entries,
        EpicBossSkillType type
    )
    {
        if (entries == null)
        {
            return null;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].skillType == type)
            {
                return entries[i];
            }
        }

        return null;
    }

    private static void ApplySkillActionDefaults(EpicBossSkillEntry skill)
    {
        if (skill.skillType == EpicBossSkillType.AccelerationBurst)
        {
            skill.activeDuration = 0.90f;
            skill.movementSpeedMultiplier = 1.80f;
        }
        else if (skill.skillType == EpicBossSkillType.AggressiveCharge)
        {
            skill.activeDuration = 0.75f;
            skill.movementSpeedMultiplier = 2.40f;
            skill.cameraShakeStrength = 0.06f;
            skill.cameraShakeDuration = 0.40f;
        }
        else if (skill.skillType == EpicBossSkillType.HeavyRoar)
        {
            skill.activeDuration = 0.70f;
            skill.cameraShakeStrength = 0.08f;
            skill.cameraShakeDuration = 0.45f;
        }
        else if (skill.skillType == EpicBossSkillType.WaterShockwave)
        {
            skill.activeDuration = 0.65f;
            skill.cameraShakeStrength = 0.10f;
            skill.cameraShakeDuration = 0.55f;
        }
        else if (skill.skillType ==
                 EpicBossSkillType.DramaticCameraShake)
        {
            skill.activeDuration = 0.65f;
            skill.cameraShakeStrength = 0.16f;
            skill.cameraShakeDuration = 0.65f;
        }
        else
        {
            skill.activeDuration = 0.80f;
            skill.cameraShakeStrength = 0.12f;
            skill.cameraShakeDuration = 0.80f;
        }
    }

    private static void EnableSkill(
        EpicBossSkillEntry[] skills,
        EpicBossSkillType type,
        float weight
    )
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i].skillType == type)
            {
                skills[i].enabled = true;
                skills[i].weight = weight;
                return;
            }
        }
    }

    private static FishMovementProfile SelectMovementProfile(
        GeneratedProfiles profiles,
        int fishId
    )
    {
        if (fishId >= 39)
        {
            return profiles.mainBoss;
        }

        if (fishId >= 36)
        {
            return profiles.miniBoss;
        }

        switch (fishId)
        {
            case 1:
            case 4:
            case 8:
            case 10:
            case 18:
                return profiles.smallDarter;

            case 3:
            case 5:
            case 6:
            case 14:
            case 23:
            case 24:
            case 25:
            case 26:
                return profiles.rayGlider;

            case 15:
            case 29:
            case 30:
            case 31:
            case 34:
            case 35:
                return profiles.pulseCreature;

            case 7:
            case 13:
            case 19:
            case 22:
            case 27:
            case 28:
            case 32:
            case 33:
                return profiles.oceanHunter;

            default:
                return profiles.smallCruiser;
        }
    }

    private static FishDeathProfile SelectDeathProfile(
        GeneratedProfiles profiles,
        int fishId
    )
    {
        if (fishId <= 11) return profiles.smallDeath;
        if (fishId <= 30) return profiles.mediumDeath;
        if (fishId <= 35) return profiles.specialDeath;
        if (fishId <= 38) return profiles.miniBossDeath;
        if (fishId <= 42) return profiles.mainBossDeath;
        return profiles.epicBossDeath;
    }

    private static bool TryValidateSourceArray(
        SwapFishScript director,
        out string error
    )
    {
        if (director == null)
        {
            error = "The selected object has no SwapFishScript.";
            return false;
        }

        if (director.Fish == null ||
            director.Fish.Length != RequiredFishCount)
        {
            int actualCount = director.Fish == null
                ? 0
                : director.Fish.Length;

            error =
                "SwapFishScript.Fish must contain exactly 46 prefab " +
                "references before automatic setup. Current size: " +
                actualCount + ".";
            return false;
        }

        HashSet<string> usedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < RequiredFishCount; i++)
        {
            GameObject prefab = director.Fish[i];

            if (prefab == null)
            {
                error = "Fish[" + i + "] is empty.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Fish[" + i + "] (" + prefab.name +
                    ") is not a prefab asset. Drag the prefab from the " +
                    "Project window into Fish[].";
                return false;
            }

            if (!usedPaths.Add(path))
            {
                error =
                    "The same prefab is assigned more than once. Duplicate " +
                    "found at Fish[" + i + "]: " + path + ".";
                return false;
            }

            if (prefab.GetComponent<FishScript>() == null)
            {
                error =
                    "Fish[" + i + "] (" + prefab.name +
                    ") needs FishScript on the prefab root.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static void ValidateSetup(
        SwapFishScript director,
        bool showSuccessDialog
    )
    {
        string error;

        if (!TryValidateSourceArray(director, out error))
        {
            Debug.LogError(error, director);
            return;
        }

        List<string> warnings = new List<string>();

        for (int i = 0; i < RequiredFishCount; i++)
        {
            GameObject prefab = director.Fish[i];
            FishScript fish = prefab.GetComponent<FishScript>();
            SerializedObject serializedFish = new SerializedObject(fish);

            if (fish.id != i)
            {
                warnings.Add(
                    "Fish[" + i + "] has ID " + fish.id + "."
                );
            }

            SerializedProperty movement =
                serializedFish.FindProperty("movementProfile");
            SerializedProperty death =
                serializedFish.FindProperty("deathProfile");

            if (movement == null || movement.objectReferenceValue == null)
            {
                warnings.Add(
                    "Fish[" + i + "] has no Movement Profile."
                );
            }
            else
            {
                FishMovementProfile movementProfile =
                    movement.objectReferenceValue as FishMovementProfile;

                if (movementProfile == null ||
                    !movementProfile.enableOrganicSteering ||
                    !movementProfile.enableReactiveHitMotion)
                {
                    warnings.Add(
                        "Fish[" + i +
                        "] Movement Profile is missing professional " +
                        "organic steering or reactive hit motion."
                    );
                }
            }

            if (death == null || death.objectReferenceValue == null)
            {
                warnings.Add(
                    "Fish[" + i + "] has no Death Profile."
                );
            }
            else
            {
                FishDeathProfile deathProfile =
                    death.objectReferenceValue as FishDeathProfile;

                if (deathProfile == null ||
                    !deathProfile.enableCinematicDeathMotion)
                {
                    warnings.Add(
                        "Fish[" + i +
                        "] Death Profile is missing cinematic death motion."
                    );
                }
            }

            if (i >= 43)
            {
                EpicBossController epic =
                    prefab.GetComponent<EpicBossController>();

                if (epic == null || !epic.HasProfile)
                {
                    warnings.Add(
                        "Fish[" + i +
                        "] has no configured EpicBossController."
                    );
                }
                else
                {
                    SerializedObject serializedEpic =
                        new SerializedObject(epic);
                    SerializedProperty epicProfileProperty =
                        serializedEpic.FindProperty("profile");
                    EpicBossProfile epicProfile =
                        epicProfileProperty != null
                            ? epicProfileProperty.objectReferenceValue
                                as EpicBossProfile
                            : null;

                    if (epicProfile == null ||
                        !epicProfile.enableReactiveFireConvulsions ||
                        !epicProfile.enableCinematicOffscreenPasses ||
                        epicProfile.deathEffectPulseCount < 2)
                    {
                        warnings.Add(
                            "Fish[" + i +
                            "] Epic profile is missing professional " +
                            "movement, reactive convulsion, or death polish."
                        );
                    }
                }

                if (prefab.GetComponent<Rigidbody2D>() == null)
                {
                    warnings.Add(
                        "Fish[" + i +
                        "] needs a Rigidbody2D on its root."
                    );
                }

                if (prefab.GetComponentsInChildren<Collider2D>(true)
                        .Length == 0)
                {
                    warnings.Add(
                        "Fish[" + i + "] needs at least one Collider2D."
                    );
                }
            }
        }

        if (warnings.Count == 0)
        {
            const string success =
                "46-fish validation passed: IDs, prefab order, organic " +
                "movement, reactive hit motion, cinematic deaths, and " +
                "Epic controllers are configured.";
            Debug.Log(success, director);

            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog(
                    "46-Fish Validation Passed",
                    success,
                    "OK"
                );
            }

            return;
        }

        string report =
            "46-fish validation found " + warnings.Count +
            " item(s):\n- " + string.Join("\n- ", warnings.ToArray());
        Debug.LogWarning(report, director);
    }

    private static SwapFishScript FindSelectedOrSceneDirector()
    {
        if (Selection.activeGameObject != null)
        {
            SwapFishScript selected =
                Selection.activeGameObject.GetComponent<SwapFishScript>();

            if (selected != null)
            {
                return selected;
            }
        }

        return UnityEngine.Object.FindObjectOfType<SwapFishScript>();
    }

    private static WeightedSwimStyle W(
        FishScript.SwimStyle style,
        float weight
    )
    {
        WeightedSwimStyle result = new WeightedSwimStyle();
        result.style = style;
        result.weight = weight;
        return result;
    }

    private static T LoadOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    private static void SetObjectReference(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.targetObject.GetType().Name,
                propertyName
            );
        }

        property.objectReferenceValue = value;
    }

    private static void SetObjectReferenceIfEmpty(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        if (value == null)
        {
            return;
        }

        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void EnsureSortingLayer(string layerName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
            "ProjectSettings/TagManager.asset"
        );

        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning(
                "Could not inspect TagManager. Create a Sorting Layer named " +
                layerName + " manually."
            );
            return;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers =
            tagManager.FindProperty("m_SortingLayers");

        if (layers == null || !layers.isArray)
        {
            Debug.LogWarning(
                "Could not inspect Sorting Layers. Create a layer named " +
                layerName + " manually."
            );
            return;
        }

        HashSet<long> usedIds = new HashSet<long>();

        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            SerializedProperty name = layer.FindPropertyRelative("name");
            SerializedProperty uniqueId =
                layer.FindPropertyRelative("uniqueID");

            if (name != null && name.stringValue == layerName)
            {
                return;
            }

            if (uniqueId != null)
            {
                usedIds.Add(uniqueId.longValue);
            }
        }

        layers.arraySize++;
        SerializedProperty newLayer =
            layers.GetArrayElementAtIndex(layers.arraySize - 1);
        SerializedProperty newName =
            newLayer.FindPropertyRelative("name");
        SerializedProperty newId =
            newLayer.FindPropertyRelative("uniqueID");
        SerializedProperty locked =
            newLayer.FindPropertyRelative("locked");

        if (newName != null)
        {
            newName.stringValue = layerName;
        }

        if (newId != null)
        {
            long candidate;

            do
            {
                candidate = unchecked((uint)Guid.NewGuid().GetHashCode());
            }
            while (candidate == 0 || usedIds.Contains(candidate));

            newId.longValue = candidate;
        }

        if (locked != null)
        {
            locked.boolValue = false;
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets[0]);
    }
}
#endif
