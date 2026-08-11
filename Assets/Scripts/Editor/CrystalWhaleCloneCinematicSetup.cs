#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CrystalWhaleCloneCinematicSetup
{
    private const string GeneratedRoot = "Assets/FishArcadeGenerated";
    private const string DeathProfilesFolder =
        GeneratedRoot + "/DeathProfiles";
    private const string ProfilePath =
        DeathProfilesFolder + "/CrystalWhaleCloneCinematic.asset";

    [MenuItem(
        "Fish Arcade/Crystal Whale Clone Cinematic/1 - Create Default Profile"
    )]
    public static void CreateDefaultProfile()
    {
        FishCloneCinematicDeathProfile profile = LoadOrCreateProfile();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
        Debug.Log(
            "Crystal Whale clone cinematic profile is ready at: " +
            ProfilePath
        );
    }

    [MenuItem(
        "Fish Arcade/Crystal Whale Clone Cinematic/2 - Apply To Selected Fish"
    )]
    public static void ApplyToSelectedFish()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Crystal Whale Clone Cinematic",
                "Select the Crystal Whale GameObject or prefab first.",
                "OK"
            );
            return;
        }

        FishCloneCinematicDeathProfile profile = LoadOrCreateProfile();

        if (PrefabUtility.IsPartOfPrefabAsset(selected))
        {
            string path = AssetDatabase.GetAssetPath(selected);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                FishScript fish = root.GetComponent<FishScript>();

                if (fish == null)
                {
                    fish = root.GetComponentInChildren<FishScript>(true);
                }

                if (fish == null)
                {
                    EditorUtility.DisplayDialog(
                        "Crystal Whale Clone Cinematic",
                        "The selected prefab does not contain FishScript.",
                        "OK"
                    );
                    return;
                }

                ApplyToFish(fish, profile, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        else
        {
            FishScript fish = selected.GetComponent<FishScript>();

            if (fish == null)
            {
                fish = selected.GetComponentInParent<FishScript>();
            }

            if (fish == null)
            {
                fish = selected.GetComponentInChildren<FishScript>(true);
            }

            if (fish == null)
            {
                EditorUtility.DisplayDialog(
                    "Crystal Whale Clone Cinematic",
                    "The selected object does not belong to a FishScript.",
                    "OK"
                );
                return;
            }

            ApplyToFish(fish, profile, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            "Applied Crystal Whale visual-only post-death clone cinematic. " +
            "Open the generated profile to tune formation, targets, merge, " +
            "Boom, sound, shake, and cleanup."
        );
    }

    [MenuItem(
        "Fish Arcade/Crystal Whale Clone Cinematic/3 - Apply Aggressive V2 Preset"
    )]
    public static void ApplyAggressiveV2Preset()
    {
        FishCloneCinematicDeathProfile profile = LoadOrCreateProfile();
        Undo.RecordObject(profile, "Crystal Whale Aggressive V2 Preset");
        ApplyRecommendedAggressiveV2Settings(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);

        Debug.Log(
            "Crystal Whale Aggressive V2 preset applied: preserved shadow " +
            "alpha, 2 off-screen attack loops, faster separation/merge, " +
            "and final center area damage. Existing Boom prefab assignments " +
            "were not replaced."
        );
    }

    private static void ApplyToFish(
        FishScript fish,
        FishCloneCinematicDeathProfile profile,
        bool useUndo
    )
    {
        FishCloneCinematicDeathController controller =
            fish.GetComponent<FishCloneCinematicDeathController>();

        if (controller == null)
        {
            controller = useUndo
                ? Undo.AddComponent<FishCloneCinematicDeathController>(
                    fish.gameObject
                )
                : fish.gameObject.AddComponent<
                    FishCloneCinematicDeathController
                >();
        }

        if (useUndo)
        {
            Undo.RecordObject(controller, "Configure Clone Cinematic");
            Undo.RecordObject(fish, "Connect Clone Cinematic");
        }

        controller.SetProfile(profile);

        SerializedObject fishSerialized = new SerializedObject(fish);
        SerializedProperty controllerProperty = fishSerialized.FindProperty(
            "postDeathCloneCinematicController"
        );

        if (controllerProperty != null)
        {
            controllerProperty.objectReferenceValue = controller;
            fishSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(fish);

        if (fish.id != 41)
        {
            Debug.LogWarning(
                "The selected FishScript currently shows ID " + fish.id +
                ". The generated cinematic profile requires Fish ID 41. " +
                "If GameplayProfile assigns ID 41 at runtime, this warning " +
                "can be ignored."
            );
        }
    }

    private static FishCloneCinematicDeathProfile LoadOrCreateProfile()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(DeathProfilesFolder);

        FishCloneCinematicDeathProfile profile =
            AssetDatabase.LoadAssetAtPath<
                FishCloneCinematicDeathProfile
            >(ProfilePath);

        if (profile != null)
        {
            return profile;
        }

        profile = ScriptableObject.CreateInstance<
            FishCloneCinematicDeathProfile
        >();
        profile.name = "CrystalWhaleCloneCinematic";
        profile.requiredFishId = 41;
        profile.cloneCount = 4;
        profile.formationStartingAngle = 90f;
        profile.formationFacing = FishCloneFacingMode.FaceAwayFromCenter;
        profile.formationRotationEnabled = true;
        profile.formationRotationSpeed = 135f;
        profile.formationClockwise = false;
        profile.spinWaitDuration = 1.4f;
        profile.continueFormationRotationUntilNextStage = true;
        profile.individualFishRotationEnabled = false;
        ApplyRecommendedAggressiveV2Settings(profile);
        profile.EnsureCloneSettingsCount();

        AssetDatabase.CreateAsset(profile, ProfilePath);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void ApplyRecommendedAggressiveV2Settings(
        FishCloneCinematicDeathProfile profile
    )
    {
        if (profile == null)
        {
            return;
        }

        profile.requiredFishId = 41;
        profile.cloneCount = 4;
        profile.cloneOpacity = 1f;
        profile.cloneShadowOpacity = 0.18f;

        profile.aggressiveOutAndBackEnabled = true;
        profile.aggressiveOutAndBackRepeats = 2;
        profile.aggressiveOutScreenViewportPadding = 0.10f;
        profile.aggressiveEnsureFullyOffscreen = true;
        profile.aggressiveWindUpFormationRadiusMultiplier = 0.16f;
        profile.aggressiveWindUpDuration = 0.065f;
        profile.aggressiveOutboundSpeed = 18f;
        profile.aggressiveReturnSpeed = 22f;
        profile.aggressiveOutsideHoldDuration = 0.035f;
        profile.aggressiveRepeatSpeedMultiplier = 1.12f;
        profile.aggressiveLungeScaleStrength = 0.14f;
        profile.aggressiveFaceMovementDirection = true;
        profile.keepAnimatorPlayingDuringAggressiveDash = true;
        profile.aggressiveAnimatorSpeedMultiplier = 1.25f;

        // Existing custom destinations remain supported, but the default
        // global movement is intentionally much faster than V1.
        profile.globalSeparationSpeed = 12f;
        profile.globalSeparationDurationOverride = 0f;
        profile.finalCenterMovementSpeed = 18f;
        profile.finalCenterDurationOverride = 0f;
        profile.finalScaleMultiplier = 0.88f;

        profile.playFinalCameraShake = true;
        profile.finalCameraShakeStrength = 0.38f;
        profile.finalCameraShakeDuration = 0.38f;

        if (profile.finalAreaDamage == null)
        {
            profile.finalAreaDamage = new CinematicAreaDamageSettings();
        }

        profile.finalAreaDamage.enabled = true;
        profile.finalAreaDamage.damage = 1200f;
        profile.finalAreaDamage.radius = 3.2f;
        profile.finalAreaDamage.useDistanceFalloff = true;
        profile.finalAreaDamage.minimumFalloffMultiplier = 0.35f;
        profile.finalAreaDamage.maximumTargets = 16;
        profile.finalAreaDamage.smallMultiplier = 1.20f;
        profile.finalAreaDamage.mediumMultiplier = 0.70f;
        profile.finalAreaDamage.specialMultiplier = 0.42f;
        profile.finalAreaDamage.miniBossMultiplier = 0.18f;
        profile.finalAreaDamage.mainBossMultiplier = 0f;
        profile.finalAreaDamage.mainBossImmune = true;
        profile.finalAreaDamage.specialFishImmune = false;
        profile.finalAreaDamage.requestReaction = true;

        profile.EnsureCloneSettingsCount();
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
}
#endif
