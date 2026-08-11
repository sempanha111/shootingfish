#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MermaidMainBossProfileSetup
{
    private const string ProfileFolder =
        "Assets/ShootingFish/Profiles";

    private const string ProfilePath =
        ProfileFolder + "/Mermaid_Showcase_MainBoss.asset";

    [MenuItem(
        "Tools/Shooting Fish/Apply Mermaid Showcase Profile To Selected Main Boss",
        false,
        121
    )]
    private static void ApplyProfileToSelectedBoss()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Mermaid Main Boss Profile",
                "Select the mermaid Main Boss prefab or scene object first.",
                "OK"
            );
            return;
        }

        FishScript fish = selected.GetComponent<FishScript>();

        if (fish == null)
        {
            fish = selected.GetComponentInChildren<FishScript>(true);
        }

        if (fish == null)
        {
            EditorUtility.DisplayDialog(
                "Mermaid Main Boss Profile",
                "The selected object does not contain FishScript.",
                "OK"
            );
            return;
        }

        FishMovementProfile profile = CreateOrUpdateProfile();

        Undo.RecordObject(fish, "Apply Mermaid Main Boss Profile");

        SerializedObject serializedFish = new SerializedObject(fish);
        serializedFish.Update();

        SetEnum(
            serializedFish,
            "fishTier",
            (int)FishTier.MainBoss
        );
        SetBool(serializedFish, "useArrayIndexTierFallback", false);
        SetObject(serializedFish, "movementProfile", profile);

        // This boss is an authored encounter only. It cannot enter a parade
        // or be selected as an ordinary ambient fish.
        SetEnum(
            serializedFish,
            "paradeParticipation",
            (int)FishScript.ParadeParticipationMode.AlwaysBlock
        );
        SetFloat(serializedFish, "paradeSelectionWeight", 0f);
        SetEnum(
            serializedFish,
            "naturalSpawnMode",
            (int)FishScript.NaturalSpawnMode.EventOnly
        );
        SetFloat(serializedFish, "ambientSpawnWeight", 0f);
        SetFloat(serializedFish, "ambientGroupChance", 0f);

        // Stay for the encounter, but roam through a wide safe arena instead
        // of sitting at viewport center.
        SetEnum(
            serializedFish,
            "mainBossPresenceMode",
            (int)FishScript.MainBossPresenceMode.StayUntilDefeated
        );
        SetFloat(serializedFish, "timedRetreatChance", 0f);
        SetVector2(
            serializedFish,
            "bossArenaStayDurationRange",
            new Vector2(60f, 90f)
        );
        SetFloat(serializedFish, "bossArenaWidthPercent", 0.84f);
        SetFloat(serializedFish, "bossArenaHeightPercent", 0.72f);
        SetFloat(
            serializedFish,
            "bossSmoothTurnDegreesPerSecond",
            42f
        );

        // Graceful health-phase changes: still visibly reactive, but never
        // reduced to a nearly stationary center target.
        SetFloat(serializedFish, "bossWoundedHealthPercent", 0.28f);
        SetFloat(serializedFish, "randomBossStyleChangeChance", 0.68f);
        SetFloat(serializedFish, "bossStyleChangeMinDelay", 7f);
        SetFloat(serializedFish, "bossStyleChangeMaxDelay", 11f);
        SetFloat(serializedFish, "woundedMoveSpeedMultiplier", 0.62f);
        SetFloat(serializedFish, "woundedTwitchStrength", 0.12f);
        SetFloat(serializedFish, "woundedTurnAngle", 5f);

        serializedFish.ApplyModifiedProperties();
        EditorUtility.SetDirty(fish);
        PrefabUtility.RecordPrefabInstancePropertyModifications(fish);
        AssetDatabase.SaveAssets();

        Selection.activeObject = fish.gameObject;

        EditorUtility.DisplayDialog(
            "Mermaid Main Boss Profile Applied",
            "Applied only to: " + fish.gameObject.name + "\n\n" +
            "The boss now uses wide figure-eight, patrol, orbit, and gentle " +
            "charge movement across an 84% x 72% arena. It stays until " +
            "defeated but does not lock to the screen center.\n\n" +
            "Recommended Move Speed: 1.2 to 1.5.",
            "OK"
        );
    }

    [MenuItem(
        "Tools/Shooting Fish/Apply Mermaid Showcase Profile To Selected Main Boss",
        true
    )]
    private static bool ValidateApplyProfile()
    {
        return Selection.activeGameObject != null;
    }

    private static FishMovementProfile CreateOrUpdateProfile()
    {
        EnsureFolder("Assets", "ShootingFish");
        EnsureFolder("Assets/ShootingFish", "Profiles");

        FishMovementProfile profile =
            AssetDatabase.LoadAssetAtPath<FishMovementProfile>(ProfilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FishMovementProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        Undo.RecordObject(profile, "Update Mermaid Main Boss Profile");

        profile.healthyStyles = new[]
        {
            Style(FishScript.SwimStyle.BossFigureEight, 58f),
            Style(FishScript.SwimStyle.BossPatrol, 34f),
            Style(FishScript.SwimStyle.BossOrbit, 8f)
        };

        profile.aggressiveStyles = new[]
        {
            Style(FishScript.SwimStyle.BossPatrol, 40f),
            Style(FishScript.SwimStyle.BossFigureEight, 30f),
            Style(FishScript.SwimStyle.BossCharge, 20f),
            Style(FishScript.SwimStyle.BossOrbit, 10f)
        };

        profile.criticalStyles = new[]
        {
            Style(FishScript.SwimStyle.BossPatrol, 38f),
            Style(FishScript.SwimStyle.BossFigureEight, 27f),
            Style(FishScript.SwimStyle.CriticalStagger, 25f),
            Style(FishScript.SwimStyle.BossCharge, 10f)
        };

        profile.enableOrganicSteering = true;
        profile.velocitySmoothTime = 0.28f;
        profile.healthyTurnDegreesPerSecond = 44f;
        profile.aggressiveTurnDegreesPerSecond = 60f;
        profile.criticalTurnDegreesPerSecond = 72f;
        profile.minimumWaypointViewportDistance = 0.50f;
        profile.waypointReachDistance = 0.58f;
        profile.waypointLifetimeRange = new Vector2(5.5f, 8.5f);
        profile.wanderAngleDegrees = 4f;
        profile.wanderFrequency = 0.20f;
        profile.speedPulseMultiplierRange = new Vector2(0.90f, 1.05f);
        profile.speedPulseDurationRange = new Vector2(4f, 7f);
        profile.styleHoldDurationRange = new Vector2(7f, 11f);
        profile.styleChangeChanceAfterHold = 0.68f;

        profile.spreadRoutesAcrossViewport = true;
        profile.centerAvoidanceHalfHeight = 0.12f;
        profile.trafficLaneJitter = 0.04f;
        profile.crossCenterLaneChangeChance = 0.12f;
        profile.orbitAnchorViewportInset = 0.30f;

        // Main Boss presence rules keep it in the arena, so natural
        // off-screen looping is deliberately disabled for this profile.
        profile.offscreenLoopChance = 0f;
        profile.maximumOffscreenLoops = 0;
        profile.offscreenViewportPadding = 0.10f;
        profile.offscreenReturnTimeout = 8f;
        profile.loopCooldown = 8f;
        profile.naturalLifetimeRange = new Vector2(60f, 90f);

        profile.stuckCheckInterval = 1f;
        profile.minimumStuckTravelDistance = 0.08f;
        profile.stuckRecoverySpeedMultiplier = 1f;

        // The large soft character should react subtly to repeated fire.
        profile.enableReactiveHitMotion = true;
        profile.microConvulsionChancePerHit = 0.14f;
        profile.repeatedMicroConvulsionChance = 0.34f;
        profile.sustainedFireHitCount = 3;
        profile.sustainedFireWindow = 0.65f;
        profile.fireReleaseGracePeriod = 0.36f;
        profile.microConvulsionDurationRange = new Vector2(0.05f, 0.09f);
        profile.microConvulsionRecoveryRange = new Vector2(0.10f, 0.18f);
        profile.microConvulsionScaleStrength = 0.018f;
        profile.reactionMovementSpeedMultiplier = 0.88f;

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static WeightedSwimStyle Style(
        FishScript.SwimStyle style,
        float weight
    )
    {
        return new WeightedSwimStyle
        {
            style = style,
            weight = weight
        };
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject target,
        string propertyName
    )
    {
        SerializedProperty property = target.FindProperty(propertyName);

        if (property == null)
        {
            throw new UnityException(
                "FishScript is missing serialized field: " + propertyName
            );
        }

        return property;
    }

    private static void SetBool(
        SerializedObject target,
        string propertyName,
        bool value
    )
    {
        RequireProperty(target, propertyName).boolValue = value;
    }

    private static void SetFloat(
        SerializedObject target,
        string propertyName,
        float value
    )
    {
        RequireProperty(target, propertyName).floatValue = value;
    }

    private static void SetEnum(
        SerializedObject target,
        string propertyName,
        int value
    )
    {
        RequireProperty(target, propertyName).enumValueIndex = value;
    }

    private static void SetVector2(
        SerializedObject target,
        string propertyName,
        Vector2 value
    )
    {
        RequireProperty(target, propertyName).vector2Value = value;
    }

    private static void SetObject(
        SerializedObject target,
        string propertyName,
        Object value
    )
    {
        RequireProperty(target, propertyName).objectReferenceValue = value;
    }
}
#endif
