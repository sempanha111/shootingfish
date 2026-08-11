#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ArmoredCrabProfileSetup
{
    private const string Root =
        "Assets/FishArcadeGenerated/ArmoredCrab";
    private const string CinematicPath =
        Root + "/Fish_30_Armored_Crab_Profile.asset";
    private const string MovementPath =
        Root + "/Fish_30_Armored_Crab_Movement.asset";
    private const string RewardDeathPath =
        Root + "/Fish_30_Armored_Crab_RewardDeath.asset";

    [MenuItem(
        "Tools/Fish Arcade/Armored Crab/Create and Configure Selected Medium Fish19"
    )]
    private static void CreateAndConfigureSelected()
    {
        GameObject prefab = ResolveSelectedPrefab();

        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "Armored Crab Setup",
                "Select the Medium Fish19 prefab in the Project window, " +
                "then run this command again.",
                "OK"
            );
            return;
        }

        ConfigurePrefab(prefab);
    }

    [MenuItem(
        "Tools/Fish Arcade/Armored Crab/Validate Selected Medium Fish19"
    )]
    private static void ValidateSelected()
    {
        GameObject prefab = ResolveSelectedPrefab();

        if (prefab == null)
        {
            Debug.LogError(
                "Select the Medium Fish19 prefab in the Project window."
            );
            return;
        }

        ValidatePrefab(prefab);
    }

    private static GameObject ResolveSelectedPrefab()
    {
        GameObject selected = Selection.activeObject as GameObject;

        if (selected != null &&
            PrefabUtility.GetPrefabAssetType(selected) !=
                PrefabAssetType.NotAPrefab)
        {
            return selected;
        }

        string[] guids = AssetDatabase.FindAssets(
            "Medium Fish19 t:Prefab"
        );

        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets(
                "MediumFish19 t:Prefab"
            );
        }

        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void ConfigurePrefab(GameObject prefab)
    {
        EnsureFolder("Assets/FishArcadeGenerated");
        EnsureFolder(Root);

        SpecialFishCinematicDeathProfile cinematic =
            LoadOrCreate<SpecialFishCinematicDeathProfile>(CinematicPath);
        FishMovementProfile movement =
            LoadOrCreate<FishMovementProfile>(MovementPath);
        FishDeathProfile rewardDeath =
            LoadOrCreate<FishDeathProfile>(RewardDeathPath);

        ConfigureCinematicProfile(cinematic);
        ConfigureMovementProfile(movement);
        ConfigureRewardDeathProfile(rewardDeath);

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            FishScript fish = root.GetComponent<FishScript>();

            if (fish == null)
            {
                throw new InvalidOperationException(
                    prefabPath +
                    " has no FishScript on its root GameObject."
                );
            }

            SpecialFishCinematicDeathController controller =
                root.GetComponent<SpecialFishCinematicDeathController>();

            if (controller == null)
            {
                controller = root.AddComponent<
                    SpecialFishCinematicDeathController
                >();
            }

            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            Animator animator = root.GetComponent<Animator>();
            AudioSource audio = root.GetComponent<AudioSource>();

            if (audio == null)
            {
                audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.loop = false;
                audio.spatialBlend = 0f;
            }

            SerializedObject serializedFish = new SerializedObject(fish);
            SetObject(serializedFish, "movementProfile", movement);
            SetObject(serializedFish, "deathProfile", rewardDeath);
            SetObject(
                serializedFish,
                "cinematicDeathController",
                controller
            );
            SetFloat(serializedFish, "Hp", 30800f);
            SetFloat(serializedFish, "CoinFish", 21090f);
            SetFloat(serializedFish, "MoveSpeed", 0.62f);
            SetInteger(serializedFish, "id", 30);
            SetEnum(serializedFish, "fishTier", (int)FishTier.Medium);
            SetBool(serializedFish, "useArrayIndexTierFallback", false);
            SetEnum(
                serializedFish,
                "paradeParticipation",
                (int)FishScript.ParadeParticipationMode.AlwaysBlock
            );
            SetFloat(serializedFish, "paradeSelectionWeight", 0f);
            SetEnum(
                serializedFish,
                "naturalSpawnMode",
                (int)FishScript.NaturalSpawnMode.Solo
            );
            SetFloat(serializedFish, "ambientSpawnWeight", 0.20f);
            SetInteger(serializedFish, "maximumSimultaneousCount", 1);
            SetFloat(serializedFish, "ambientGroupChance", 0f);
            SetFloat(
                serializedFish,
                "formationHorizontalMultiplier",
                1.35f
            );
            SetFloat(
                serializedFish,
                "formationVerticalMultiplier",
                1.35f
            );
            SetFloat(serializedFish, "formationPadding", 0.24f);
            SetEnum(
                serializedFish,
                "gimmickType",
                (int)FishScript.GimmickType.BombCrab
            );
            SetFloat(serializedFish, "gimmickRadius", 2.5f);
            SetFloat(serializedFish, "gimmickDamage", 850f);
            serializedFish.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedController =
                new SerializedObject(controller);
            SetObject(serializedController, "profile", cinematic);
            SetObject(serializedController, "owner", fish);
            SetObject(serializedController, "body", body);
            SetObject(serializedController, "bodyAnimator", animator);
            SetObject(serializedController, "audioSource", audio);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            if (body != null)
            {
                body.gravityScale = 0f;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            EditorUtility.SetDirty(fish);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(audio);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;

        Debug.Log(
            "Armored Crab setup completed. Medium Fish19 now uses " +
            "Fish_30_Armored_Crab_Profile, a custom heavy movement profile, " +
            "a reward-only death profile, one-at-a-time ambient spawning, " +
            "and the reusable cinematic death controller.",
            prefab
        );

        EditorUtility.DisplayDialog(
            "Armored Crab Setup Complete",
            "Assign your Bomb Explosion prefab, audio clips, optional " +
            "certificate, and boss skill index on:\n\n" + CinematicPath,
            "OK"
        );
    }

    private static void ConfigureCinematicProfile(
        SpecialFishCinematicDeathProfile profile
    )
    {
        profile.displayName = "Armored Crab";
        profile.damageResistance = 0.28f;
        profile.useUnscaledTime = true;
        profile.disableCollidersDuringSequence = true;
        profile.allowNormalNetBackgroundEffect = false;
        profile.rewardTiming = CinematicRewardTiming.FinalExplosion;

        profile.enableConvulsion = true;
        profile.convulsionDuration = 0.62f;
        profile.convulsionPositionStrength = 0.085f;
        profile.convulsionRotationDegrees = 12f;
        profile.convulsionScaleStrength = 0.085f;
        profile.convulsionRedStrength = 0.48f;
        profile.convulsionFrequency = 18f;

        profile.enableMainCharge = true;
        profile.mainChargeDuration = 1.25f;
        profile.mainChargeScale = 1.48f;
        profile.mainChargePulseStrength = 0.075f;
        profile.mainChargePulseFrequency = 8f;
        profile.mainChargeRotationSpeed = 95f;
        profile.mainChargeRedStrength = 0.72f;
        profile.chargePresentationOrder =
            CinematicChargePresentationOrder.NetBoomThenChargeEffects;
        profile.playNetBoomAtCharge = true;
        profile.chargeNetBoomCount = 1;
        profile.chargeNetBoomReadDuration = 0.65f;
        profile.chargeStage2BMinimumDuration = 0.65f;
        profile.chargeEffectDelayAfterNetBoom = 0.10f;
        profile.chargeEffectScatterRadius = 0.18f;
        profile.chargeEffectOffset = Vector2.zero;
        profile.chargeEffectScale = 1f;
        profile.startChargeEarthquakeWithEffects = true;
        profile.chargeCameraShakeStrength = 0.075f;
        profile.chargeCameraShakeDuration = 0.45f;

        profile.enableFullScreenMovement = true;
        profile.routeMode = CinematicRouteMode.Mixed;
        profile.automaticRoutePointCount = 5;
        profile.viewportEdgePadding = 0.12f;
        profile.movementSpeed = 3.6f;
        profile.movementScale = 1.95f;
        profile.movementStartRotationSpeed = 145f;
        profile.movementEndRotationSpeed = 320f;
        profile.movementRedStrength = 0.82f;
        profile.movementNetBoomCount = 1;
        profile.movementExplosionInterval = 0.07f;
        profile.movementExplosionScatterRadius = 0.28f;
        profile.movementEffectsMatchOwnerSorting = true;
        profile.movementNetBoomOffset = Vector2.zero;
        profile.movementNetBoomScale = 1f;
        profile.movementNetBoomSortingOrderOffset = 3;
        profile.movementArrivalEffectOffset = Vector2.zero;
        profile.movementArrivalEffectScale = 1f;
        profile.movementArrivalEffectSortingOrderOffset = 4;
        profile.movementArrivalShakeStrength = 0.08f;
        profile.movementArrivalShakeDuration = 0.18f;

        profile.movementAreaDamage.damage = 850f;
        profile.movementAreaDamage.radius = 2.4f;
        profile.movementAreaDamage.maximumTargets = 10;
        profile.movementAreaDamage.smallMultiplier = 1.20f;
        profile.movementAreaDamage.mediumMultiplier = 0.55f;
        profile.movementAreaDamage.specialMultiplier = 0.35f;
        profile.movementAreaDamage.miniBossMultiplier = 0.15f;
        profile.movementAreaDamage.mainBossMultiplier = 0f;
        profile.movementAreaDamage.mainBossImmune = true;

        profile.certificateTiming =
            CinematicCertificateTiming.RouteStart;
        profile.useExistingCertificateSystem = true;
        profile.certificatePrefabIndex = -1;
        profile.certificateSpawnDelay = 0.10f;
        profile.certificatePosition =
            CinematicTargetType.ScreenCenter;

        profile.approachFinalAttacker = true;
        profile.attackerApproachSpeed = 3.8f;
        profile.attackerPauseDuration = 0.18f;

        profile.finalTarget = CinematicTargetType.DensestFishArea;
        profile.finalMoveSpeed = 3.2f;
        profile.finalChargeDuration = 0.82f;
        profile.finalChargeScale = 2.35f;
        profile.finalChargeRotationSpeed = 430f;
        profile.finalChargeRedStrength = 0.96f;
        profile.finalChargeShakeStrength = 0.17f;
        profile.finalChargeShakeDuration = 0.70f;

        profile.backgroundNetBoomCount = 8;
        profile.backgroundExplosionRadius = 2.7f;
        profile.backgroundExplosionInterval = 0.055f;
        profile.backgroundExplosionScaleRange =
            new Vector2(0.72f, 1.28f);
        profile.finalExplosionShakeStrength = 0.28f;
        profile.finalExplosionShakeDuration = 0.90f;
        profile.finalAreaDamage.damage = 1550f;
        profile.finalAreaDamage.radius = 3.4f;
        profile.finalAreaDamage.maximumTargets = 16;
        profile.finalAreaDamage.smallMultiplier = 1.25f;
        profile.finalAreaDamage.mediumMultiplier = 0.70f;
        profile.finalAreaDamage.specialMultiplier = 0.42f;
        profile.finalAreaDamage.miniBossMultiplier = 0.18f;
        profile.finalAreaDamage.mainBossMultiplier = 0f;
        profile.finalAreaDamage.mainBossImmune = true;

        profile.enableSlowMotion = true;
        profile.slowMotionTimeScale = 0.35f;
        profile.slowMotionDuration = 0.24f;
        profile.enableBossSkill = true;
        profile.bossSkillIndex = -1;
        profile.bossSkillDelay = 0.12f;
        profile.bossSkillVisualOnly = true;
        profile.hideBodyAfterExplosionDelay = 0.10f;
        profile.finalCleanupDelay = 0.65f;

        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureMovementProfile(
        FishMovementProfile profile
    )
    {
        profile.healthyStyles = new[]
        {
            W(FishScript.SwimStyle.LaneGlide, 55f),
            W(FishScript.SwimStyle.ArcSweep, 30f),
            W(FishScript.SwimStyle.SpiralCross, 15f)
        };
        profile.aggressiveStyles = new[]
        {
            W(FishScript.SwimStyle.ArcSweep, 42f),
            W(FishScript.SwimStyle.VerticalDive, 28f),
            W(FishScript.SwimStyle.LaneGlide, 20f),
            W(FishScript.SwimStyle.ZigZagBurst, 10f)
        };
        profile.criticalStyles = new[]
        {
            W(FishScript.SwimStyle.CriticalStagger, 50f),
            W(FishScript.SwimStyle.ArcSweep, 28f),
            W(FishScript.SwimStyle.VerticalDive, 22f)
        };

        profile.enableOrganicSteering = true;
        profile.velocitySmoothTime = 0.28f;
        profile.healthyTurnDegreesPerSecond = 48f;
        profile.aggressiveTurnDegreesPerSecond = 62f;
        profile.criticalTurnDegreesPerSecond = 78f;
        profile.minimumWaypointViewportDistance = 0.55f;
        profile.waypointReachDistance = 0.62f;
        profile.waypointLifetimeRange = new Vector2(5.2f, 8.2f);
        profile.wanderAngleDegrees = 4.5f;
        profile.wanderFrequency = 0.18f;
        profile.speedPulseMultiplierRange = new Vector2(0.92f, 1.05f);
        profile.speedPulseDurationRange = new Vector2(3.8f, 6.2f);
        profile.styleHoldDurationRange = new Vector2(6f, 10f);
        profile.styleChangeChanceAfterHold = 0.32f;
        profile.spreadRoutesAcrossViewport = true;
        profile.crossCenterLaneChangeChance = 0.18f;
        profile.offscreenLoopChance = 0.16f;
        profile.maximumOffscreenLoops = 1;
        profile.enableReactiveHitMotion = true;
        profile.microConvulsionChancePerHit = 0.18f;
        profile.repeatedMicroConvulsionChance = 0.48f;
        profile.microConvulsionScaleStrength = 0.028f;
        profile.reactionMovementSpeedMultiplier = 0.64f;

        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureRewardDeathProfile(
        FishDeathProfile profile
    )
    {
        profile.rewardCalculationMode =
            FishRewardCalculationMode.FixedFishCoin;
        profile.rewardMultiplier = 1f;
        profile.rewardTextStyle = RewardTextStyle.Big;
        profile.playStandardCoinAnimation = true;
        profile.coinAnimationPlayCount = 3;
        profile.coinAnimationInterval = 0.09f;
        profile.coinAnimationSpreadRadius = 0.42f;
        profile.playMainBossSkillInFrontGun = false;
        profile.playBossCoinBurst = false;
        profile.playNetBoom = false;
        profile.customDeathEffect = null;
        profile.enableCinematicDeathMotion = false;
        profile.fadeSpriteRenderers = false;
        profile.disableCollidersDuringDeath = true;
        profile.deathCameraShakeStrength = 0f;
        profile.deathCameraShakeDuration = 0f;
        profile.playBigWinSound = true;
        profile.bigWinSoundIndex = -1;
        profile.certificateChance = 0f;
        profile.minimumCertificateWin = 0f;

        // Optional existing center-screen prefab support remains available.
        // The setup leaves it disabled until the user assigns a prefab.
        profile.playCenterScreenDeathPrefab = false;
        profile.centerScreenDeathPrefabChance = 1f;
        profile.centerScreenDeathPrefabSpace =
            CenterScreenDeathPrefabSpace.Auto;
        profile.centerScreenDeathViewportPosition =
            new Vector2(0.5f, 0.5f);
        profile.centerScreenDeathUseUnscaledTime = true;

        EditorUtility.SetDirty(profile);
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

        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)
            .Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static void SetObject(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(
        SerializedObject target,
        string propertyName,
        float value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInteger(
        SerializedObject target,
        string propertyName,
        int value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetEnum(
        SerializedObject target,
        string propertyName,
        int value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private static void SetBool(
        SerializedObject target,
        string propertyName,
        bool value
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void ValidatePrefab(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            FishScript fish = root.GetComponent<FishScript>();
            SpecialFishCinematicDeathController controller =
                root.GetComponent<SpecialFishCinematicDeathController>();
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();

            bool valid = fish != null && controller != null &&
                controller.HasProfile && body != null && renderer != null;

            if (valid)
            {
                Debug.Log(
                    "Armored Crab validation passed for " + path + ".",
                    prefab
                );
            }
            else
            {
                Debug.LogError(
                    "Armored Crab validation failed. Required root " +
                    "components: FishScript, SpecialFishCinematicDeathController " +
                    "with profile, Rigidbody2D, and SpriteRenderer.",
                    prefab
                );
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
