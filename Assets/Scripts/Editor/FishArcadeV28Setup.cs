#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates or directly updates one editable FishGameplayProfile per entry in
/// SwapFishScript.Fish, assigns it to the prefab, applies the clean current
/// absolute combat balance plus the existing species movement tuning, rewrites the
/// current boss-occupancy
/// and level-population presets, applies the open-scene cannon economy, and
/// authors species-specific movement such as Crystal Whale long-body steering.
/// This is the all-in-one Generate button: stale generated population/boss
/// pacing values are overwritten directly, so no separate migration is needed.
/// Existing movement/death/epic profiles and all prefab children are preserved.
/// </summary>
public static class FishArcadeV28Setup
{
    private const string CurrentRoot = "Assets/FishArcadeGenerated/Current";
    private const string ProfileRoot = CurrentRoot + "/GameplayProfiles";
    private const string DeathProfileRoot = CurrentRoot + "/DeathProfiles";
    private const string LevelProfileRoot = CurrentRoot + "/LevelProfiles";
    private const string SkillFrontGunFolder = "Assets/Prefabs/Effects/SkillFrontGUN";

    private static readonly string[] LegacyGeneratedFolders =
    {
        "Assets/FishProfiles/Professional",
        "Assets/FishProfiles/GeneratedDeathProfiles"
    };

    private static readonly string[] LegacyEditorGeneratorScripts =
    {
        "Assets/Scripts/Editor/ArcadeReferenceFeatureSetup.cs",
        "Assets/Scripts/Editor/ArmoredCrabProfileSetup.cs",
        "Assets/Scripts/Editor/CrystalWhaleCloneCinematicSetup.cs",
        "Assets/Scripts/Editor/FishArcade46Setup.cs",
        "Assets/Scripts/Editor/FishArcadeProfileSetup.cs",
        "Assets/Scripts/Editor/FishArcadeV21MixedParadeNpcSetup.cs",
        "Assets/Scripts/Editor/FishGameplayProfileGenerator.cs",
        "Assets/Scripts/Editor/FishProfileBalanceUpgradeV13.cs",
        "Assets/Scripts/Editor/FishProfileBalanceUpgradeV14.cs",
        "Assets/Scripts/Editor/FrontGunRewardV26CleanupSetup.cs",
        "Assets/Scripts/Editor/MermaidMainBossProfileSetup.cs",
        "Assets/Scripts/Editor/MainBossFrontGunSkillV22Setup.cs",
        "Assets/Scripts/Editor/MainBossFrontGunSkillV23Setup.cs",
        "Assets/Scripts/Editor/FrontGunRewardV24Setup.cs",
        "Assets/Scripts/Editor/FrontGunRewardV25Setup.cs",
        "Assets/Scripts/Editor/FishArcadeV27Setup.cs"
    };

    private static readonly Dictionary<string, FishDeathProfile>
        RebuildDeathSnapshots = new Dictionary<string, FishDeathProfile>();

    [MenuItem("Tools/Fish Arcade/V28/Rebuild Clean Current Setup")]
    public static void RebuildCleanCurrentSetup()
    {
        bool confirm = EditorUtility.DisplayDialog(
            "Fish Arcade V28 - Clean Rebuild",
            "This creates one clean generated root at:\n" +
            CurrentRoot +
            "\n\nIt removes the old Professional / GeneratedDeathProfiles folders, " +
            "preserves the currently assigned per-fish death presentation in memory, " +
            "regenerates current gameplay + level + death profiles, assigns Main Boss " +
            "front-gun skill indices Fish 39=0, 40=1, 41=2, 42=3, prepares embedded " +
            "reward text prefabs, and removes old Editor generator scripts.\n\n" +
            "Existing movement profiles, Crystal Whale runtime cinematic code, fish " +
            "prefabs, VFX, sound, pooling, and manually authored non-generated assets " +
            "are not deleted.",
            "Rebuild V28",
            "Cancel"
        );

        if (!confirm)
        {
            return;
        }

        CaptureAssignedDeathProfiles();

        try
        {
            DeleteGeneratedFolder(CurrentRoot);
            for (int i = 0; i < LegacyGeneratedFolders.Length; i++)
            {
                DeleteGeneratedFolder(LegacyGeneratedFolders[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GenerateAndAssignProfiles();
            int preparedFrontGunPrefabs = PrepareEmbeddedFrontGunPrefabs();
            int configuredManagers = ConfigureFrontGunManagers();
            int deletedLegacyScripts = RemoveLegacyGeneratorScripts();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Fish Arcade V28] Clean rebuild complete. Generated root=" +
                CurrentRoot +
                ", front-gun prefabs prepared=" + preparedFrontGunPrefabs +
                ", AnimatiorManager objects configured=" + configuredManagers +
                ", legacy generator scripts removed=" + deletedLegacyScripts +
                ". Main Boss mapping: Fish39->0, Fish40->1, Fish41->2, Fish42->3."
            );

            EditorUtility.DisplayDialog(
                "Fish Arcade V28 Complete",
                "Generated data is now organized under:\n" + CurrentRoot +
                "\n\nMain Boss front-gun mapping:\n" +
                "Fish 39 Golden Leviathan -> Skill 0\n" +
                "Fish 40 Jester Boss (Main Boss2) -> Skill 1\n" +
                "Fish 41 Crystal Whale -> Skill 2\n" +
                "Fish 42 Treasure Idol -> Skill 3\n\n" +
                "Old balance/death generated folders and old Editor generator scripts " +
                "were removed. Existing movement/profile assets outside the old balance " +
                "folders were intentionally kept so prefab movement references are not broken.",
                "OK"
            );
        }
        finally
        {
            ReleaseDeathProfileSnapshots();
        }
    }

    private static readonly string[] RecommendedNames =
    {
        "Amber Dart", "Golden Moon", "Violet Ribbon", "Silver Wing",
        "Blue Stripe", "Lantern Minnow", "Azure Comet", "Needle Runner",
        "Crimson Sprite", "Coral Flash", "Ember Fry", "Reef Zebra",
        "Bronze Mask", "Blue Sail", "Golden Ray", "Violet Puffer",
        "Azure Orb", "Yellow Puffer", "Captain Puffer", "Emerald Beetlefish",
        "Green Tang", "Spiked Puffer", "Electric Eel", "Coral Butterfly",
        "Ancient Turtle", "Violet Ray", "Star Ray", "Blue Runner",
        "Sea Dragon", "Golden Coin Fish", "Armored Crab",
        "Rose School", "Golden Carp", "Treasure Koi", "Golden Current",
        "Golden Jelly", "Royal Shark", "Flame Seahorse", "Sun Lionfish",
        "Golden Leviathan", "Jester Boss", "Crystal Whale", "Treasure Idol",
        "Storm Emperor", "Abyss Turtle", "Ancient Ocean Throne"
    };

    public static void GenerateAndAssignProfiles()
    {
        SwapFishScript[] directors = UnityEngine.Object.FindObjectsOfType<SwapFishScript>(true);
        if (directors == null || directors.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Fish Arcade Profile Setup",
                "No SwapFishScript was found in the open scenes.",
                "OK"
            );
            return;
        }

        EnsureFolder(CurrentRoot);
        EnsureFolder(ProfileRoot);
        EnsureFolder(DeathProfileRoot);
        EnsureFolder(LevelProfileRoot);
        HashSet<GameObject> processedPrefabs = new HashSet<GameObject>();
        int profileCount = 0;
        int deathProfileCount = 0;
        int prefabCount = 0;
        int skippedCount = 0;

        try
        {
            for (int directorIndex = 0; directorIndex < directors.Length; directorIndex++)
            {
                SwapFishScript director = directors[directorIndex];
                if (director == null)
                {
                    continue;
                }

                EnsureDirectorComponents(director);
                ApplyBossOccupancyAndVarietyPreset(director);
                GenerateAndAssignLevelProfiles(director);
                GameObject[] fishArray = director.Fish;
                if (fishArray == null)
                {
                    continue;
                }

                for (int fishIndex = 0; fishIndex < fishArray.Length; fishIndex++)
                {
                    GameObject prefab = fishArray[fishIndex];
                    if (prefab == null || processedPrefabs.Contains(prefab))
                    {
                        continue;
                    }

                    processedPrefabs.Add(prefab);
                    EditorUtility.DisplayProgressBar(
                        "Fish Arcade V28 Profiles",
                        "Processing " + prefab.name + " (array index " + fishIndex + ")",
                        fishArray.Length > 0 ? fishIndex / (float)fishArray.Length : 1f
                    );

                    FishGameplayProfile profile = GetOrCreateProfile(fishIndex, prefab.name);
                    ApplyRecommendedValues(profile, fishIndex, prefab.name);
                    profileCount++;

                    FishDeathProfile sourceDeathProfile =
                        GetCapturedDeathProfile(prefab);
                    if (sourceDeathProfile == null)
                    {
                        sourceDeathProfile = GetAssignedDeathProfile(prefab);
                    }
                    FishDeathProfile generatedDeathProfile =
                        GetOrCreateGeneratedDeathProfile(
                            fishIndex,
                            prefab.name,
                            sourceDeathProfile
                        );
                    deathProfileCount++;

                    if (AssignProfileToPrefab(
                            prefab,
                            fishIndex,
                            profile,
                            generatedDeathProfile
                        ))
                    {
                        prefabCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        int economyManagerCount = ApplyCleanEconomyToOpenScenes();
        int npcWeaponCount = ApplyNpcGunPresetToOpenScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Fish Arcade V28 Generate Complete",
            "Current gameplay profiles created/updated: " + profileCount +
            "\nDeath profiles created/updated: " + deathProfileCount +
            "\nPrefabs updated: " + prefabCount +
            "\nSkipped non-prefab entries: " + skippedCount +
            "\nOpen-scene GameManagers economy updated: " + economyManagerCount +
            "\nOpen-scene NPC weapons capped/tuned: " + npcWeaponCount +
            "\n\nClean absolute HP/reward/resistance values, gun economy, boss occupancy, anti-repeat spawning, target-boss arrays and level population are rewritten directly by Generate." +
            "\nThe first Main Boss is prioritized early, and later levels receive more target bosses / occasional multi-boss batches." +
            "\nNo separate balance or occupancy migration command is required." +
            "\n\nGameplay profiles: " + ProfileRoot +
            "\nLevel profiles: " + LevelProfileRoot +
            "\nDeath profiles: " + DeathProfileRoot,
            "OK"
        );
    }

    [MenuItem("Tools/Fish Arcade/V28/Validate Current Setup")]
    public static void ValidateProfessionalSetup()
    {
        SwapFishScript[] directors =
            UnityEngine.Object.FindObjectsOfType<SwapFishScript>(true);

        int missingFishScript = 0;
        int missingProfile = 0;
        int missingDeathProfile = 0;
        int missingAgent = 0;
        int invalidProfile = 0;
        int mainBossIndexMismatch = 0;
        int checkedFish = 0;

        for (int directorIndex = 0;
             directorIndex < directors.Length;
             directorIndex++)
        {
            GameObject[] fishArray = directors[directorIndex].Fish;
            if (fishArray == null)
            {
                continue;
            }

            for (int i = 0; i < fishArray.Length; i++)
            {
                GameObject prefab = fishArray[i];
                if (prefab == null)
                {
                    continue;
                }

                checkedFish++;

                FishScript script =
                    prefab.GetComponentInChildren<FishScript>(true);

                if (script == null)
                {
                    missingFishScript++;
                    Debug.LogError(
                        "Fish array index " + i +
                        " has no FishScript.",
                        prefab
                    );
                    continue;
                }

                FishGameplayProfile gameplayProfile =
                    script.GetGameplayProfile();

                if (gameplayProfile == null)
                {
                    missingProfile++;
                    Debug.LogWarning(
                        "Fish array index " + i +
                        " has no FishGameplayProfile.",
                        prefab
                    );
                }
                else if (
                    gameplayProfile.maximumSpeed <
                        gameplayProfile.minimumSpeed ||
                    gameplayProfile.health <= 0f ||
                    gameplayProfile.maximumSimultaneousCount <= 0)
                {
                    invalidProfile++;
                    Debug.LogWarning(
                        "Invalid profile values on " +
                        gameplayProfile.name + ".",
                        gameplayProfile
                    );
                }

                SerializedObject fishSo = new SerializedObject(script);
                SerializedProperty deathProperty =
                    fishSo.FindProperty("deathProfile");
                FishDeathProfile deathProfile =
                    deathProperty != null
                        ? deathProperty.objectReferenceValue as FishDeathProfile
                        : null;

                if (deathProfile == null)
                {
                    missingDeathProfile++;
                    Debug.LogWarning(
                        "Fish array index " + i +
                        " has no FishDeathProfile.",
                        prefab
                    );
                }
                else if (i >= 39 && i <= 42)
                {
                    int expectedIndex = i - 39;

                    if (!deathProfile.playMainBossSkillInFrontGun ||
                        deathProfile.mainBossFrontGunSkillIndex !=
                            expectedIndex)
                    {
                        mainBossIndexMismatch++;
                        Debug.LogWarning(
                            "Fish " + i +
                            " Main Boss front-gun mapping is wrong. " +
                            "Expected skill index " + expectedIndex +
                            ", actual " +
                            deathProfile.mainBossFrontGunSkillIndex +
                            ".",
                            deathProfile
                        );
                    }
                }

                if (script.GetComponent<FishMotionAgent>() == null)
                {
                    missingAgent++;
                    Debug.LogWarning(
                        "Fish array index " + i +
                        " has no FishMotionAgent.",
                        prefab
                    );
                }
            }
        }

        AnimatiorManager[] managers =
            UnityEngine.Object.FindObjectsOfType<AnimatiorManager>(true);

        int managersMissingFourSkillSlots = 0;
        for (int i = 0; i < managers.Length; i++)
        {
            GameObject[] skills =
                managers[i].mainBossFrontGunSkillPrefabs;

            if (skills == null ||
                skills.Length < 4 ||
                skills[0] == null ||
                skills[1] == null ||
                skills[2] == null ||
                skills[3] == null)
            {
                managersMissingFourSkillSlots++;
                Debug.LogWarning(
                    "AnimatiorManager needs non-null front-gun skill " +
                    "slots 0, 1, 2 and 3 for Fish 39-42.",
                    managers[i]
                );
            }
        }

        EditorUtility.DisplayDialog(
            "Fish Arcade V28 Validation",
            "Fish checked: " + checkedFish +
            "\nMissing FishScript: " + missingFishScript +
            "\nMissing gameplay profile: " + missingProfile +
            "\nMissing death profile: " + missingDeathProfile +
            "\nMissing motion agent: " + missingAgent +
            "\nInvalid gameplay profile: " + invalidProfile +
            "\nMain Boss index mismatches: " + mainBossIndexMismatch +
            "\nAnimatiorManager missing skill slots 0-3: " +
            managersMissingFourSkillSlots,
            "OK"
        );
    }

    private static void GenerateAndAssignLevelProfiles(SwapFishScript director)
    {
        FishLevelPopulationProfile[] profiles = new FishLevelPopulationProfile[6];
        for (int level = 0; level < profiles.Length; level++)
        {
            string assetPath = LevelProfileRoot + "/Level_" + (level + 1).ToString("00") +
                "_Population_Profile.asset";
            FishLevelPopulationProfile profile =
                AssetDatabase.LoadAssetAtPath<FishLevelPopulationProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FishLevelPopulationProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            ApplyLevelPopulationPreset(profile, level);
            profiles[level] = profile;
        }

        SerializedObject serializedDirector = new SerializedObject(director);
        SerializedProperty array = serializedDirector.FindProperty("levelPopulationProfiles");
        if (array != null)
        {
            array.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }
            serializedDirector.ApplyModifiedProperties();
        }
    }

    private static void ApplyLevelPopulationPreset(
        FishLevelPopulationProfile profile,
        int levelIndex
    )
    {
        // Occupancy-first mix: ambient fish still keep the screen alive, but
        // Small fish no longer dominate the selection while the Main Boss is
        // delayed behind repeated crowd waves.
        float[] small = { 0.48f, 0.45f, 0.42f, 0.40f, 0.38f, 0.35f };
        float[] large = { 0.12f, 0.14f, 0.16f, 0.18f, 0.20f, 0.22f };
        float[] special = { 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.10f };
        float[] common = { 0.62f, 0.58f, 0.54f, 0.50f, 0.46f, 0.42f };
        float[] uncommon = { 0.25f, 0.27f, 0.29f, 0.30f, 0.31f, 0.32f };
        float[] rare = { 0.11f, 0.13f, 0.15f, 0.17f, 0.19f, 0.21f };
        float[] epic = { 0.02f, 0.02f, 0.02f, 0.03f, 0.04f, 0.05f };
        int[] maximum = { 24, 25, 27, 28, 30, 32 };
        int[] minimum = { 14, 15, 16, 17, 18, 19 };
        int[] maximumLarge = { 3, 3, 4, 4, 5, 5 };
        int[] maximumGroups = { 3, 3, 4, 4, 5, 5 };
        float[] bossTiming = { 4.5f, 4.8f, 5.0f, 5.2f, 5.5f, 5.8f };
        string[] names =
        {
            "Ribbon Current", "Arrowhead School", "Living Halo",
            "Cardinal Cross", "Twin Current", "Royal Armada"
        };

        int i = Mathf.Clamp(levelIndex, 0, 5);
        profile.levelNumber = i + 1;
        profile.displayName = names[i];
        profile.smallFishPercentage = small[i];
        profile.mediumFishPercentage = 1f - small[i];
        profile.largeFishPercentage = large[i];
        profile.specialFishPercentage = special[i];
        profile.commonPercentage = common[i];
        profile.uncommonPercentage = uncommon[i];
        profile.rarePercentage = rare[i];
        profile.epicPercentage = epic[i];
        profile.maximumActiveFish = maximum[i];
        profile.minimumActiveFish = minimum[i];
        profile.maximumLargeFish = maximumLarge[i];
        profile.maximumGroups = maximumGroups[i];
        profile.bossSpawnTiming = bossTiming[i];
        // Clean current balance uses absolute fish HP/reward values. Level
        // difficulty comes from population mix and boss progression rather
        // than stacking another hidden HP/reward multiplier on the same fish.
        profile.fishHealthMultiplier = 1f;
        profile.fishSpeedMultiplier = 1f;
        profile.rewardMultiplier = 1f;
        profile.evasiveBehaviorChance = 0.04f + i * 0.035f;
        profile.complexRouteChance = 0.08f + i * 0.06f;
        EditorUtility.SetDirty(profile);
    }

    private static int ApplyNpcGunPresetToOpenScenes()
    {
        WeaponNPC[] npcWeapons =
            UnityEngine.Object.FindObjectsOfType<WeaponNPC>(true);
        int updated = 0;

        for (int i = 0; i < npcWeapons.Length; i++)
        {
            WeaponNPC npc = npcWeapons[i];
            if (npc == null)
            {
                continue;
            }

            npc.ApplyRecommendedNpcGunPreset();
            EditorUtility.SetDirty(npc);
            if (npc.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
            }
            updated++;
        }

        return updated;
    }

    private static int ApplyCleanEconomyToOpenScenes()
    {
        GameManager[] managers =
            UnityEngine.Object.FindObjectsOfType<GameManager>(true);
        int updated = 0;

        for (int i = 0; i < managers.Length; i++)
        {
            GameManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            manager.ApplyCleanBalancedEconomy();
            EditorUtility.SetDirty(manager);

            if (manager.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }

            updated++;
        }

        return updated;
    }

    private static void EnsureDirectorComponents(SwapFishScript director)
    {
        if (director.GetComponent<FishSpawnDirectorService>() == null)
        {
            Undo.AddComponent<FishSpawnDirectorService>(director.gameObject);
        }

        FishDebugVisualizer visualizer = director.GetComponent<FishDebugVisualizer>();
        if (visualizer == null)
        {
            visualizer = Undo.AddComponent<FishDebugVisualizer>(director.gameObject);
            visualizer.enabled = false;
        }

        EditorUtility.SetDirty(director.gameObject);
        if (director.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        }
    }

    private static void ApplyBossOccupancyAndVarietyPreset(SwapFishScript director)
    {
        if (director == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(director);

        // First real Main Boss should arrive quickly instead of letting
        // opening, mini-boss heralds, rush fish and crowd build-up stack for
        // many seconds before a target boss becomes visible.
        SetBool(so, "prioritizeMainBossPresence", true);
        SetFloat(so, "firstMainBossWarmupCap", 3.5f);
        SetInt(so, "firstMainBossParadeWaveCap", 1);
        SetFloat(so, "firstMainBossParadeGapCap", 0.55f);
        SetBool(so, "skipMiniBossBeforeFirstMainBoss", true);
        SetFloat(so, "firstMainBossWarningDelayCap", 0.65f);
        SetFloat(so, "firstMainBossArrivalEventDurationCap", 0.65f);
        SetBool(so, "skipFirstMainBossRushFish", true);
        SetBool(so, "skipFirstMainBossCrowdBuildUp", true);

        // Keep the presentation, but shorten the old small-fish-heavy waits.
        SetFloat(so, "level1Warmup", 4f);
        SetFloat(so, "warmupIncreasePerLevel", 0.35f);
        SetFloat(so, "bossWarningBaseDelay", 0.8f);
        SetFloat(so, "bossWarningIncreasePerLevel", 0.05f);
        SetFloat(so, "betweenTargetBossBaseDelay", 1.0f);
        SetFloat(so, "betweenTargetBossIncreasePerLevel", 0.10f);
        SetFloat(so, "postBossRecoveryBaseDelay", 0.45f);
        SetFloat(so, "postBossRecoveryIncreasePerLevel", 0.05f);

        SetInt(so, "earlyLevelParadeWaves", 1);
        SetInt(so, "advancedLevelParadeWaves", 1);
        SetFloat(so, "preBossParadeBaseGap", 0.75f);
        SetFloat(so, "preBossParadeGapPerLevel", 0.05f);
        SetFloat(so, "preBossFinalPauseBase", 0.20f);
        SetFloat(so, "preBossFinalPausePerLevel", 0.02f);
        SetFloat(so, "preBossMiniBossBaseChance", 0.08f);
        SetFloat(so, "preBossMiniBossChancePerLevel", 0.025f);
        SetFloat(so, "preBossMiniBossLeadBaseDelay", 1.15f);
        SetFloat(so, "preBossMiniBossLeadPerLevel", 0.10f);
        SetFloat(so, "preBossSpaceWaitTimeout", 1.5f);

        // Keep opening/standalone parades, but stop them from becoming the
        // only thing the player sees before every Main Boss.
        SetInt(so, "levelOpeningParadeWaveCount", 1);
        SetFloat(so, "levelOpeningParadeDelay", 0.10f);
        SetFloat(so, "levelOpeningParadeWaveGap", 0.40f);
        SetFloat(so, "standaloneFeatureParadeChance", 0.28f);
        SetFloat(so, "standaloneFeatureParadeDelay", 0.15f);
        SetInt(so, "standaloneFeatureParadeWaveCount", 1);

        // Later boss batches can still build crowds, just not with the old
        // large repeated small-fish flood.
        SetBool(so, "enableBossArrivalCrowdBuildUp", true);
        SetInt(so, "bossArrivalCrowdBaseWaves", 1);
        SetInt(so, "bossArrivalCrowdMaximumWaves", 2);
        SetInt(so, "bossArrivalCrowdBaseFishPerWave", 8);
        SetInt(so, "bossArrivalCrowdFishPerLevel", 2);
        SetFloat(so, "bossArrivalCrowdWaveGap", 0.45f);
        SetFloat(so, "bossArrivalSpecialChanceBase", 0.06f);
        SetFloat(so, "bossArrivalSpecialChancePerLevel", 0.02f);
        SetInt(so, "bossArrivalRushFishMultiplier", 1);

        // Increase target-boss presence: every level owns multiple real Main
        // Boss targets, with occasional simultaneous bosses from Level 3+.
        List<int> bosses = FindBossCandidateIndexes(director);
        if (bosses.Count > 0)
        {
            int[] counts = { 2, 2, 3, 3, 3, 4 };
            int[] starts = { 0, 1, 2, 3, 4, Mathf.Max(0, bosses.Count - 4) };
            string[] fieldNames =
            {
                "level1BossTargets", "level2BossTargets", "level3BossTargets",
                "level4BossTargets", "level5BossTargets", "level6BossTargets"
            };

            for (int level = 0; level < fieldNames.Length; level++)
            {
                SetIntArray(
                    so,
                    fieldNames[level],
                    BuildBossTargetSequence(bosses, starts[level], counts[level])
                );
            }

            SetInt(so, "bigBossStartIndex", bosses[0]);
            SetInt(so, "bigBossEndIndex", bosses[bosses.Count - 1]);
        }

        SetInt(so, "globalMaximumSimultaneousBosses", 2);
        SetIntArray(so, "maximumSimultaneousBossesPerLevel", new[] { 1, 1, 2, 2, 2, 2 });
        SetFloatArray(so, "doubleBossChancePerLevel", new[] { 0f, 0.04f, 0.12f, 0.22f, 0.30f, 0.42f });
        SetFloatArray(so, "tripleBossChancePerLevel", new[] { 0f, 0f, 0f, 0f, 0f, 0f });

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);
        if (director.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        }

        // Strengthen anti-repeat selection so the ambient filler does not
        // repeatedly choose the same Small fish while the boss is active.
        FishSpawnDirectorService spawnService = director.GetComponent<FishSpawnDirectorService>();
        if (spawnService != null)
        {
            SerializedObject spawnSo = new SerializedObject(spawnService);
            SetInt(spawnSo, "recentFishHistorySize", 12);
            SetFloat(spawnSo, "repeatedFishWeightMultiplier", 0.06f);
            SetFloat(spawnSo, "immediateRepeatWeightMultiplier", 0.01f);
            SetInt(spawnSo, "maximumConsecutiveSameSpecies", 1);
            SetFloat(spawnSo, "minimumLargeFishShare", 0.18f);
            SetFloat(spawnSo, "minimumSpecialFishShare", 0.05f);
            SetFloat(spawnSo, "minimumRareFishShare", 0.10f);
            spawnSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawnService);
        }

        // Apply this last because the historical occupancy preset above still
        // writes several parade fields for presentation. Clean current is the
        // authoritative parade/spawn configuration and must win.
        director.ApplyCleanParadePreset();
    }

    private static List<int> FindBossCandidateIndexes(SwapFishScript director)
    {
        List<int> result = new List<int>();
        GameObject[] fish = director != null ? director.Fish : null;
        if (fish == null || fish.Length == 0)
        {
            return result;
        }

        // The professional 46-fish layout reserves 39-45 for Main Bosses.
        int preferredStart = Mathf.Min(39, fish.Length - 1);
        int preferredEnd = Mathf.Min(45, fish.Length - 1);
        if (fish.Length > 39)
        {
            for (int i = preferredStart; i <= preferredEnd; i++)
            {
                if (fish[i] != null)
                {
                    result.Add(i);
                }
            }
        }

        // Fallback for projects whose Fish array was reordered.
        if (result.Count == 0)
        {
            for (int i = 0; i < fish.Length; i++)
            {
                GameObject prefab = fish[i];
                if (prefab == null)
                {
                    continue;
                }

                FishScript script = prefab.GetComponentInChildren<FishScript>(true);
                if (script != null && script.GetFishTier() == FishTier.MainBoss)
                {
                    result.Add(i);
                }
            }
        }

        return result;
    }

    private static int[] BuildBossTargetSequence(List<int> bosses, int start, int count)
    {
        if (bosses == null || bosses.Count == 0 || count <= 0)
        {
            return new int[0];
        }

        int actualCount = Mathf.Max(1, count);
        int[] values = new int[actualCount];
        int safeStart = Mathf.Max(0, start);
        for (int i = 0; i < actualCount; i++)
        {
            values[i] = bosses[(safeStart + i) % bosses.Count];
        }
        return values;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.stringValue = value ?? string.Empty;
    }

    private static void SetIntArray(SerializedObject so, string name, int[] values)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        p.arraySize = values != null ? values.Length : 0;
        for (int i = 0; values != null && i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }

    private static void SetFloatArray(SerializedObject so, string name, float[] values)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        p.arraySize = values != null ? values.Length : 0;
        for (int i = 0; values != null && i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }

    private static FishGameplayProfile GetOrCreateProfile(int fishIndex, string prefabName)
    {
        string safeName = MakeSafeFileName(GetRecommendedName(fishIndex, prefabName));
        string assetPath = ProfileRoot + "/Fish_" + fishIndex.ToString("00") +
            "_" + safeName + "_Profile.asset";
        FishGameplayProfile profile = AssetDatabase.LoadAssetAtPath<FishGameplayProfile>(assetPath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FishGameplayProfile>();
            AssetDatabase.CreateAsset(profile, assetPath);
        }

        return profile;
    }

    private static FishDeathProfile GetAssignedDeathProfile(
        GameObject prefab
    )
    {
        if (prefab == null)
        {
            return null;
        }

        FishScript fish = prefab.GetComponentInChildren<FishScript>(true);
        if (fish == null)
        {
            return null;
        }

        SerializedObject serializedFish = new SerializedObject(fish);
        SerializedProperty death = serializedFish.FindProperty("deathProfile");
        return death != null
            ? death.objectReferenceValue as FishDeathProfile
            : null;
    }

    private static FishDeathProfile GetOrCreateGeneratedDeathProfile(
        int fishIndex,
        string prefabName,
        FishDeathProfile sourceProfile
    )
    {
        string safeName = MakeSafeFileName(
            GetRecommendedName(fishIndex, prefabName)
        );
        string assetPath = DeathProfileRoot + "/Fish_" +
            fishIndex.ToString("00") + "_" + safeName + "_Death.asset";

        FishDeathProfile profile =
            AssetDatabase.LoadAssetAtPath<FishDeathProfile>(assetPath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FishDeathProfile>();

            // Preserve the user's existing authored death presentation on the
            // first generation. From this point onward the generated per-fish
            // asset can be tuned directly without being overwritten by a
            // shared category profile.
            if (sourceProfile != null)
            {
                EditorUtility.CopySerialized(sourceProfile, profile);
            }
            else
            {
                ApplySafeGeneratedDeathDefaults(profile, fishIndex);
            }

            AssetDatabase.CreateAsset(profile, assetPath);
        }

        ApplyGeneratedDeathBalance(profile, fishIndex);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ApplyGeneratedDeathBalance(
        FishDeathProfile profile,
        int fishIndex
    )
    {
        if (profile == null)
        {
            return;
        }

        FishArcadeBalanceModel.GetFishCombatPreset(
            fishIndex,
            out _,
            out _,
            out float reward,
            out FishTier tier,
            out _,
            out _
        );

        // Reward safety floor: the generated per-fish death profile carries the
        // same clean reward as its minimum. This prevents the old serialized
        // default 10 from winning while still allowing intentional runtime
        // reward multipliers to increase the payout. FishScript V27 also
        // resolves the authoritative base reward independently, so Rocket and
        // alternate death paths match.
        profile.rewardCalculationMode =
            FishRewardCalculationMode.FixedFishCoin;
        profile.rewardMultiplier = 1f;
        profile.maximumSmallShotBonusPercent = 0f;
        profile.minimumReward = reward;
        profile.maximumReward = 0f;

        // V28 deterministic Main Boss front-gun mapping.
        // These four IDs are the normal Main Boss set in the 46-fish array.
        // The skill array index is no longer random for them.
        if (fishIndex >= 39 && fishIndex <= 42)
        {
            profile.rewardTextStyle = RewardTextStyle.None;
            profile.playStandardCoinAnimation = false;

            profile.playMainBossSkillInFrontGun = true;
            profile.mainBossFrontGunSkillIndex = fishIndex - 39;
            profile.mainBossFrontGunSkillTiming =
                MainBossFrontGunSkillDeathTiming.ConvulsionStart;
            profile.mainBossFrontGunSkillDelay = 0f;
            profile.mainBossFrontGunCoinStartDelayOverride = -1f;
            profile.playMainBossFrontGunSkillSound = true;
            profile.mainBossFrontGunSkillSoundIndex = -1;
            profile.showMainBossFrontGunRewardText = true;
            profile.mainBossFrontGunRewardTextPrefix = "+";
            profile.mainBossFrontGunRewardTextDelay = 0.03f;
        }
        else if (tier != FishTier.MainBoss)
        {
            // Keep the generated assets explicit and uncluttered. Epic Bosses
            // have their own presentation and are not part of the 39-42 map.
            profile.playMainBossSkillInFrontGun = false;
        }
    }

    private static void ApplySafeGeneratedDeathDefaults(
        FishDeathProfile profile,
        int fishIndex
    )
    {
        FishArcadeBalanceModel.GetFishCombatPreset(
            fishIndex,
            out _,
            out _,
            out _,
            out FishTier tier,
            out _,
            out _
        );

        profile.rewardTextStyle = tier == FishTier.Small
            ? RewardTextStyle.Small
            : tier == FishTier.MainBoss
                ? RewardTextStyle.None
                : RewardTextStyle.Big;
        profile.playStandardCoinAnimation = tier != FishTier.MainBoss;
        profile.coinAnimationPlayCount = tier == FishTier.Small
            ? 1
            : tier == FishTier.Medium
                ? 1
                : tier == FishTier.Special
                    ? 2
                    : tier == FishTier.MiniBoss
                        ? 3
                        : 1;
        profile.coinAnimationInterval = 0.08f;
        profile.coinAnimationSpreadRadius = tier == FishTier.Small
            ? 0.10f
            : tier == FishTier.Medium
                ? 0.16f
                : 0.30f;
        profile.enableCinematicDeathMotion = true;
        profile.disableCollidersDuringDeath = true;
        profile.fadeSpriteRenderers = true;
        profile.deathDuration = tier == FishTier.Small
            ? 0.24f
            : tier == FishTier.Medium
                ? 0.38f
                : tier == FishTier.Special
                    ? 0.52f
                    : 0.62f;
    }

    private static bool AssignProfileToPrefab(
        GameObject prefab,
        int fishIndex,
        FishGameplayProfile profile,
        FishDeathProfile generatedDeathProfile
    )
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(prefabPath) ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                "Skipped " + prefab.name +
                " because it is not a prefab asset. Assign the generated profile manually.",
                prefab
            );
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            FishScript fishScript = prefabRoot.GetComponentInChildren<FishScript>(true);
            if (fishScript == null)
            {
                Debug.LogError("No FishScript found in prefab " + prefabPath, prefab);
                return false;
            }

            if (fishScript.GetComponent<FishMotionAgent>() == null)
            {
                fishScript.gameObject.AddComponent<FishMotionAgent>();
            }

            SerializedObject serializedFish = new SerializedObject(fishScript);
            SetObjectReference(serializedFish, "gameplayProfile", profile);
            SetObjectReference(
                serializedFish,
                "deathProfile",
                generatedDeathProfile
            );

            FishArcadeBalanceModel.GetFishCombatPreset(
                fishIndex,
                out float fallbackHealth,
                out _,
                out float fallbackReward,
                out _,
                out _,
                out _
            );
            SerializedProperty hpProperty = serializedFish.FindProperty("Hp");
            if (hpProperty != null)
            {
                hpProperty.floatValue = fallbackHealth;
            }
            SerializedProperty rewardProperty =
                serializedFish.FindProperty("CoinFish");
            if (rewardProperty != null)
            {
                rewardProperty.floatValue = fallbackReward;
            }

            SetBoolean(serializedFish, "useProfessionalMotionAgent", true);
            SetBoolean(serializedFish, "useArrayIndexTierFallback", false);
            SetInteger(serializedFish, "id", fishIndex);

            if (fishIndex == 41)
            {
                // Crystal Whale's authored 1.18-2.30 profile range is meant
                // to be felt directly in world space instead of being reduced
                // by the legacy 0.55 professional compatibility scale.
                SerializedProperty professionalSpeedScale =
                    serializedFish.FindProperty("professionalProfileSpeedScale");
                if (professionalSpeedScale != null)
                {
                    professionalSpeedScale.floatValue = 1f;
                }
            }
            SerializedProperty tierProperty = serializedFish.FindProperty("fishTier");
            if (tierProperty != null)
            {
                tierProperty.enumValueIndex = (int)profile.fishTier;
            }

            if (profile.fishTier == FishTier.Special)
            {
                SerializedProperty spawnMode =
                    serializedFish.FindProperty("naturalSpawnMode");
                if (spawnMode != null)
                {
                    spawnMode.enumValueIndex =
                        (int)FishScript.NaturalSpawnMode.Solo;
                }

                SerializedProperty ambientWeight =
                    serializedFish.FindProperty("ambientSpawnWeight");
                if (ambientWeight != null)
                {
                    ambientWeight.floatValue = Mathf.Max(
                        0.16f,
                        profile.spawnWeight
                    );
                }

                SerializedProperty simultaneous =
                    serializedFish.FindProperty("maximumSimultaneousCount");
                if (simultaneous != null)
                {
                    simultaneous.intValue = Mathf.Clamp(
                        profile.maximumSimultaneousCount,
                        1,
                        2
                    );
                }
            }

            serializedFish.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ApplyRecommendedValues(
        FishGameplayProfile profile,
        int fishIndex,
        string prefabName
    )
    {
        profile.fishId = fishIndex;
        profile.displayName = GetRecommendedName(fishIndex, prefabName);
        profile.minimumLevel = GetMinimumLevel(fishIndex);
        profile.preferredEntryDirection = FishPreferredDirection.Any;
        profile.preferredExitDirection = FishPreferredDirection.OppositeEntry;
        profile.screenEdgePadding = 0.10f;
        profile.shadowOpacity = 0.42f;
        profile.shadowSortingOrder = -1;
        profile.flipDirection = true;
        profile.routeLengthRange = new Vector2(0.8f, 1.8f);
        profile.pauseDurationRange = new Vector2(0.12f, 0.60f);
        profile.offScreenWaitingTimeRange = new Vector2(1.5f, 4.5f);
        profile.groupEntryDelayRange = new Vector2(0.06f, 0.22f);

        // Reset optional species-specific boss steering every Generate click.
        // Individual boss overrides below can then opt in without stale values
        // leaking into other fish profiles.
        profile.bossUseLongBodySteering = false;
        profile.bossSteeringDegreesPerSecond = 28f;
        profile.bossRetreatOutsideOnly = false;
        profile.bossSpeedChangeIntervalRange = new Vector2(1.4f, 3.6f);
        profile.bossPauseChance = 0.22f;
        profile.bossNormalWorldSpeedCap = 0f;
        profile.ignoreReactiveHitMotion = false;

        if (fishIndex <= 11)
        {
            ApplySmallPreset(profile, fishIndex);
        }
        else if (fishIndex <= 30)
        {
            ApplyMediumPreset(profile, fishIndex);
        }
        else if (fishIndex <= 35)
        {
            ApplySpecialPreset(profile, fishIndex);
        }
        else if (fishIndex <= 38)
        {
            ApplyMiniBossPreset(profile, fishIndex);
        }
        else if (fishIndex <= 42)
        {
            ApplyMainBossPreset(profile, fishIndex);
        }
        else
        {
            ApplyEpicBossPreset(profile, fishIndex);
        }

        ApplyVisualSpeciesOverrides(profile, fishIndex);
        ApplyAcrossScreenSizeTuning(profile);
        ApplyCleanCombatBalance(profile, fishIndex);
        profile.MarkMovementBalanceVersion(15);
        EditorUtility.SetDirty(profile);
    }

    private static void ApplySmallPreset(FishGameplayProfile p, int index)
    {
        p.fishCategory = "Small Fish";
        p.fishTier = FishTier.Small;
        p.rarity = index >= 8 ? FishRarity.Uncommon : FishRarity.Common;
        p.sizeClass = index <= 5 ? FishSizeClass.Tiny : FishSizeClass.Small;
        p.rewardValue = 8f + index * 2.5f;
        p.health = 8f + index * 2f;
        p.damageResistance = 0f;
        p.spawnWeight = index >= 8 ? 0.72f : 1.20f;
        p.maximumSimultaneousCount = 8;
        p.minimumSpeed = 2.75f + (index % 3) * 0.14f;
        p.maximumSpeed = 4.05f + (index % 4) * 0.18f;
        p.acceleration = 7.0f;
        p.deceleration = 8.0f;
        p.turnSpeed = 165f;
        p.turnFrequency = 0.9f;
        p.preferredSwimmingDepth = 0.50f;
        p.horizontalMovementPreference = 0.80f;
        p.verticalMovementPreference = 0.20f;
        p.centerCrossingChance = 0.56f;
        p.edgeRouteChance = 0.10f;
        p.curveStrength = 0.28f;
        p.wobbleAmount = 0.12f;
        p.wobbleFrequency = 1.25f;
        p.pauseChance = 0.025f;
        p.burstSpeedChance = 0.14f;
        p.burstSpeedMultiplier = 1.42f;
        p.returnChance = 0.12f;
        p.swimmingPattern = index % 4 == 0
            ? FishSwimmingPattern.FastStraightSwimmer
            : FishSwimmingPattern.SchoolingSwimmer;
        p.behavior = FishBehaviorFlags.Calm |
            FishBehaviorFlags.Social |
            FishBehaviorFlags.GroupFollowing |
            FishBehaviorFlags.RandomRoaming;
        p.canJoinSchool = true;
        p.minimumGroupSize = 2;
        p.maximumGroupSize = 7;
        p.leaderChance = 0.20f;
        p.formationType = FishFormationType.LooseSchool;
        p.followDistance = 0.62f;
        p.separationDistance = 0.42f;
        p.alignmentStrength = 0.65f;
        p.cohesionStrength = 0.55f;
        p.groupSpeedVariation = 0.08f;
        p.minimumSpawnDistance = 0.58f;
        p.minimumSwimmingDistance = 0.46f;
        p.overlapCheckInterval = 0.28f;
        p.routeAdjustmentStrength = 0.48f;
        p.maximumRouteCorrectionAttempts = 6;
        p.fishSizeSpacingMultiplier = 0.90f;
        p.schoolingSpacingMultiplier = 0.58f;
        p.animatorSpeedMultiplier = 1.28f;
        p.tailMovementSpeed = 1.30f;
        p.bodySwayAmount = 0.09f;
        p.routeWeights = BuildRouteWeights(1.10f, 0.85f, 1.35f, 0.80f, 0.15f, 0.70f, 0.10f);
        p.useBossMovement = false;
    }

    private static void ApplyMediumPreset(FishGameplayProfile p, int index)
    {
        int local = index - 12;
        p.fishCategory = "Medium Fish";
        p.fishTier = FishTier.Medium;
        p.rarity = local >= 13 ? FishRarity.Rare : local >= 7 ? FishRarity.Uncommon : FishRarity.Common;
        p.sizeClass = local >= 12 ? FishSizeClass.Large : FishSizeClass.Medium;
        p.rewardValue = 45f + local * 7f;
        p.health = 45f + local * 9f;
        p.damageResistance = local >= 12 ? 0.08f : 0.03f;
        p.spawnWeight = local >= 13 ? 0.42f : local >= 7 ? 0.72f : 1f;
        p.maximumSimultaneousCount = local >= 12 ? 2 : 4;
        p.minimumSpeed = 1.65f + (local % 4) * 0.09f;
        p.maximumSpeed = 2.65f + (local % 3) * 0.12f;
        p.acceleration = 3.8f;
        p.deceleration = 4.8f;
        p.turnSpeed = 96f;
        p.turnFrequency = 1.25f;
        p.preferredSwimmingDepth = 0.52f;
        p.horizontalMovementPreference = 0.72f;
        p.verticalMovementPreference = 0.28f;
        p.centerCrossingChance = 0.50f;
        p.edgeRouteChance = 0.14f;
        p.curveStrength = 0.40f;
        p.wobbleAmount = 0.09f;
        p.wobbleFrequency = 0.75f;
        p.pauseChance = 0.055f;
        p.burstSpeedChance = 0.09f;
        p.burstSpeedMultiplier = 1.32f;
        p.returnChance = 0.16f;
        p.swimmingPattern = FishSwimmingPattern.SmoothCurvedSwimmer;
        p.behavior = FishBehaviorFlags.Calm |
            FishBehaviorFlags.Curious |
            FishBehaviorFlags.RandomRoaming;
        p.canJoinSchool = local < 12;
        p.minimumGroupSize = 1;
        p.maximumGroupSize = local < 8 ? 4 : 2;
        p.leaderChance = 0.18f;
        p.formationType = FishFormationType.Arc;
        p.followDistance = 0.92f;
        p.separationDistance = 0.72f;
        p.alignmentStrength = 0.48f;
        p.cohesionStrength = 0.38f;
        p.groupSpeedVariation = 0.055f;
        p.minimumSpawnDistance = 0.92f;
        p.minimumSwimmingDistance = 0.76f;
        p.overlapCheckInterval = 0.32f;
        p.routeAdjustmentStrength = 0.38f;
        p.maximumRouteCorrectionAttempts = 6;
        p.fishSizeSpacingMultiplier = local >= 12 ? 1.25f : 1f;
        p.schoolingSpacingMultiplier = 0.62f;
        p.animatorSpeedMultiplier = 1f;
        p.tailMovementSpeed = 1f;
        p.bodySwayAmount = 0.08f;
        p.routeWeights = BuildRouteWeights(0.90f, 0.85f, 1.25f, 1.10f, 0.28f, 0.75f, 0.20f);
        p.useBossMovement = false;
    }

    private static void ApplySpecialPreset(FishGameplayProfile p, int index)
    {
        int local = index - 31;
        p.fishCategory = "Special Fish";
        p.fishTier = FishTier.Special;
        p.rarity = local >= 3 ? FishRarity.Epic : FishRarity.Rare;
        p.sizeClass = local == 0 ? FishSizeClass.Medium : FishSizeClass.Large;
        p.rewardValue = 220f + local * 85f;
        p.health = 180f + local * 70f;
        p.damageResistance = 0.08f + local * 0.015f;
        p.spawnWeight = 0.18f;
        p.maximumSimultaneousCount = local == 0 ? 2 : 1;
        p.minimumSpeed = 1.45f + local * 0.06f;
        p.maximumSpeed = 2.55f + local * 0.12f;
        p.acceleration = 4.2f;
        p.deceleration = 5.1f;
        p.turnSpeed = 112f;
        p.turnFrequency = 0.95f;
        p.centerCrossingChance = 0.68f;
        p.edgeRouteChance = 0.08f;
        p.curveStrength = 0.52f;
        p.wobbleAmount = 0.12f;
        p.wobbleFrequency = 0.82f;
        p.pauseChance = 0.08f;
        p.burstSpeedChance = 0.16f;
        p.burstSpeedMultiplier = 1.45f;
        p.returnChance = 0.24f;
        p.swimmingPattern = FishSwimmingPattern.UnpredictableRareFish;
        p.behavior = FishBehaviorFlags.Curious |
            FishBehaviorFlags.Evasive |
            FishBehaviorFlags.CenterSeeking |
            FishBehaviorFlags.RandomRoaming;
        p.canJoinSchool = local == 0;
        p.minimumGroupSize = local == 0 ? 3 : 1;
        p.maximumGroupSize = local == 0 ? 5 : 1;
        p.leaderChance = 0.35f;
        p.formationType = local == 0 ? FishFormationType.Diamond : FishFormationType.LeaderAndFollowers;
        p.followDistance = 1.1f;
        p.separationDistance = 0.92f;
        p.alignmentStrength = 0.58f;
        p.cohesionStrength = 0.48f;
        p.groupSpeedVariation = 0.04f;
        p.minimumSpawnDistance = 1.22f;
        p.minimumSwimmingDistance = 1.02f;
        p.overlapCheckInterval = 0.30f;
        p.routeAdjustmentStrength = 0.50f;
        p.maximumRouteCorrectionAttempts = 7;
        p.fishSizeSpacingMultiplier = 1.15f;
        p.schoolingSpacingMultiplier = 0.62f;
        p.animatorSpeedMultiplier = 1.10f;
        p.tailMovementSpeed = 1.08f;
        p.bodySwayAmount = 0.11f;
        p.routeWeights = BuildRouteWeights(0.35f, 0.90f, 1.65f, 1.25f, 0.08f, 1.15f, 0.55f);
        p.useBossMovement = false;
    }

    private static void ApplyMiniBossPreset(FishGameplayProfile p, int index)
    {
        int local = index - 36;
        p.fishCategory = "Mini Boss";
        p.fishTier = FishTier.MiniBoss;
        p.rarity = FishRarity.Epic;
        p.sizeClass = FishSizeClass.Huge;
        p.rewardValue = 750f + local * 250f;
        p.health = 850f + local * 300f;
        p.damageResistance = 0.14f + local * 0.02f;
        p.spawnWeight = 0.05f;
        p.maximumSimultaneousCount = 1;
        p.minimumSpeed = 1.12f + local * 0.07f;
        p.maximumSpeed = 1.82f + local * 0.08f;
        p.acceleration = 2.45f;
        p.deceleration = 3.0f;
        p.turnSpeed = 70f;
        p.turnFrequency = 1.7f;
        p.centerCrossingChance = 0.62f;
        p.edgeRouteChance = 0.10f;
        p.curveStrength = 0.58f;
        p.wobbleAmount = 0.07f;
        p.wobbleFrequency = 0.56f;
        p.pauseChance = 0.08f;
        p.burstSpeedChance = 0.18f;
        p.burstSpeedMultiplier = 1.50f;
        p.returnChance = 0.30f;
        p.swimmingPattern = FishSwimmingPattern.HunterChasingSwimmer;
        p.behavior = FishBehaviorFlags.Aggressive |
            FishBehaviorFlags.Territorial |
            FishBehaviorFlags.PlayerApproaching |
            FishBehaviorFlags.Evasive |
            FishBehaviorFlags.EscapeAtLowHealth;
        p.lowHealthEscapeThreshold = 0.18f;
        p.canJoinSchool = false;
        p.minimumGroupSize = 1;
        p.maximumGroupSize = 1;
        p.minimumSpawnDistance = 1.75f;
        p.minimumSwimmingDistance = 1.48f;
        p.overlapCheckInterval = 0.38f;
        p.routeAdjustmentStrength = 0.32f;
        p.maximumRouteCorrectionAttempts = 7;
        p.fishSizeSpacingMultiplier = 1.35f;
        p.animatorSpeedMultiplier = 0.95f;
        p.tailMovementSpeed = 0.92f;
        p.bodySwayAmount = 0.07f;
        p.routeWeights = BuildRouteWeights(0.25f, 0.75f, 1.60f, 1.35f, 0.10f, 0.85f, 0.90f);
        p.useBossMovement = false;
    }

    private static void ApplyMainBossPreset(FishGameplayProfile p, int index)
    {
        int local = index - 39;
        p.fishCategory = "Main Boss";
        p.fishTier = FishTier.MainBoss;
        p.rarity = FishRarity.Boss;
        p.sizeClass = FishSizeClass.Boss;
        p.rewardValue = 2600f + local * 850f;
        p.health = 3600f + local * 1400f;
        p.damageResistance = 0.18f + local * 0.02f;
        p.spawnWeight = 0.01f;
        p.maximumSimultaneousCount = 1;
        p.minimumSpeed = 0.88f;
        p.maximumSpeed = 1.55f;
        p.acceleration = 1.45f;
        p.deceleration = 1.85f;
        p.turnSpeed = 54f;
        p.turnFrequency = 2.4f;
        p.routeLengthRange = new Vector2(1.25f, 2.65f);
        p.centerCrossingChance = 0.64f;
        p.edgeRouteChance = 0.08f;
        p.curveStrength = 0.70f;
        p.wobbleAmount = 0.045f;
        p.wobbleFrequency = 0.42f;
        p.pauseChance = 0.12f;
        p.pauseDurationRange = new Vector2(0.25f, 1.15f);
        p.burstSpeedChance = 0.08f;
        p.burstSpeedMultiplier = 1.35f;
        p.offScreenWaitingTimeRange = new Vector2(1.5f, 4.0f);
        p.returnChance = 1f;
        p.swimmingPattern = FishSwimmingPattern.HeavyBossMovement;
        p.behavior = FishBehaviorFlags.Aggressive |
            FishBehaviorFlags.Territorial |
            FishBehaviorFlags.PlayerApproaching |
            FishBehaviorFlags.CenterSeeking |
            FishBehaviorFlags.EscapeAtLowHealth;
        p.canJoinSchool = false;
        p.minimumGroupSize = 1;
        p.maximumGroupSize = 1;
        p.minimumSpawnDistance = 2.45f;
        p.minimumSwimmingDistance = 2.10f;
        p.overlapCheckInterval = 0.45f;
        p.routeAdjustmentStrength = 0.26f;
        p.maximumRouteCorrectionAttempts = 8;
        p.fishSizeSpacingMultiplier = 1.55f;
        p.animatorSpeedMultiplier = 0.90f;
        p.tailMovementSpeed = 0.82f;
        p.bodySwayAmount = 0.055f;
        p.routeWeights = BuildRouteWeights(0.12f, 0.60f, 1.85f, 1.80f, 0.05f, 1.15f, 0.75f);
        p.useBossMovement = true;
        p.bossMinimumSpeed = 0.86f;
        p.bossMaximumSpeed = 1.58f;
        p.bossAcceleration = 1.45f;
        p.bossDeceleration = 1.85f;
        p.bossTurnSpeed = 54f;
        p.bossEnterNearPlayerChance = 0.28f;
        p.bossCrossCenterChance = 0.66f;
        p.bossWideRouteChance = 0.62f;
        p.bossRouteDistanceRange = new Vector2(1.25f, 2.75f);
        p.bossPauseDurationRange = new Vector2(0.20f, 1.20f);
        p.screenEdgePadding = 0.10f;
        p.bossReturnDelayRange = new Vector2(1.4f, 4.0f);
        p.bossEnterAndRetreatChance = 0.18f;
        p.bossLoopRouteChance = 0.20f;
    }

    private static void ApplyEpicBossPreset(FishGameplayProfile p, int index)
    {
        ApplyMainBossPreset(p, index);
        int local = Mathf.Max(0, index - 43);
        p.fishCategory = "Epic Boss";
        p.rarity = FishRarity.Boss;
        p.rewardValue = 8000f + local * 2500f;
        p.health = 12000f + local * 5000f;
        p.damageResistance = 0.25f + local * 0.025f;
        p.minimumSpeed = 0.68f;
        p.maximumSpeed = 1.30f;
        p.acceleration = 1.10f;
        p.deceleration = 1.45f;
        p.turnSpeed = 42f;
        p.minimumSpawnDistance = 3.10f;
        p.minimumSwimmingDistance = 2.65f;
        p.fishSizeSpacingMultiplier = 1.75f;
        p.bossMinimumSpeed = 0.68f;
        p.bossMaximumSpeed = 1.34f;
        p.bossAcceleration = 1.10f;
        p.bossDeceleration = 1.45f;
        p.bossTurnSpeed = 42f;
        p.bossCrossCenterChance = 0.70f;
        p.bossWideRouteChance = 0.72f;
        p.bossRouteDistanceRange = new Vector2(1.45f, 3.10f);
        p.bossReturnDelayRange = new Vector2(1.8f, 4.8f);
        p.bossLoopRouteChance = 0.28f;
        p.animatorSpeedMultiplier = 0.82f;
        p.tailMovementSpeed = 0.76f;
        p.bodySwayAmount = 0.045f;
    }

    private static void ApplyVisualSpeciesOverrides(FishGameplayProfile p, int index)
    {
        switch (index)
        {
            case 7: // needle-shaped fish
            case 27:
                p.swimmingPattern = FishSwimmingPattern.FastStraightSwimmer;
                p.maximumSpeed *= 1.18f;
                p.turnSpeed *= 0.86f;
                p.burstSpeedChance = 0.22f;
                p.routeWeights = BuildRouteWeights(1.70f, 1.05f, 1.25f, 0.35f, 0.08f, 0.40f, 0.15f);
                break;
            case 15:
            case 17:
            case 18:
            case 21: // puffer-like bodies
                p.swimmingPattern = FishSwimmingPattern.StopAndGoSwimmer;
                p.minimumSpeed *= 0.82f;
                p.maximumSpeed *= 0.88f;
                p.pauseChance = 0.18f;
                p.turnSpeed *= 0.82f;
                p.fishSizeSpacingMultiplier *= 1.15f;
                break;
            case 22: // eel
                p.swimmingPattern = FishSwimmingPattern.SShapedSwimmer;
                p.wobbleAmount = 0.20f;
                p.wobbleFrequency = 1.05f;
                p.routeWeights = BuildRouteWeights(0.25f, 0.75f, 1.30f, 0.70f, 0.10f, 1.95f, 0.20f);
                break;
            case 24: // turtle
                p.swimmingPattern = FishSwimmingPattern.SlowAndSteady;
                p.minimumSpeed = 0.92f;
                p.maximumSpeed = 1.48f;
                p.acceleration = 2.2f;
                p.turnSpeed = 62f;
                p.damageResistance = 0.18f;
                p.fishSizeSpacingMultiplier = 1.45f;
                break;
            case 25:
            case 26: // rays
                p.swimmingPattern = FishSwimmingPattern.WideRoamingSwimmer;
                p.curveStrength = 0.72f;
                p.turnSpeed = 76f;
                p.routeWeights = BuildRouteWeights(0.20f, 0.75f, 1.55f, 1.85f, 0.08f, 0.85f, 0.30f);
                break;
            case 28: // sea dragon
                p.swimmingPattern = FishSwimmingPattern.UnpredictableRareFish;
                p.rarity = FishRarity.Rare;
                p.spawnWeight = 0.28f;
                p.pauseChance = 0.12f;
                p.burstSpeedChance = 0.22f;
                break;
            case 29: // coin fish
                p.displayName = "Golden Coin Fish";
                p.rarity = FishRarity.Epic;
                p.rewardValue *= 2.4f;
                p.spawnWeight = 0.12f;
                p.maximumSimultaneousCount = 1;
                p.swimmingPattern = FishSwimmingPattern.ShortBurstSwimmer;
                p.pauseChance = 0.18f;
                p.burstSpeedChance = 0.32f;
                p.centerCrossingChance = 0.72f;
                break;
            case 30: // armored crab
                p.swimmingPattern = FishSwimmingPattern.BottomAreaSwimmer;
                p.preferredSwimmingDepth = 0.12f;
                p.verticalMovementPreference = 0.08f;
                p.horizontalMovementPreference = 0.92f;
                p.damageResistance = 0.25f;
                p.minimumSpeed = 0.78f;
                p.maximumSpeed = 1.28f;
                p.turnSpeed = 52f;
                p.sizeClass = FishSizeClass.Large;
                p.fishSizeSpacingMultiplier = 1.55f;
                break;
            case 31: // visible rose-colored school
                p.swimmingPattern = FishSwimmingPattern.SchoolingSwimmer;
                p.behavior |= FishBehaviorFlags.Social | FishBehaviorFlags.GroupFollowing;
                p.canJoinSchool = true;
                p.minimumGroupSize = 3;
                p.maximumGroupSize = 6;
                p.formationType = FishFormationType.Diamond;
                break;
            case 35: // jelly
                p.swimmingPattern = FishSwimmingPattern.UnpredictableRareFish;
                p.preferredEntryDirection = FishPreferredDirection.Vertical;
                p.horizontalMovementPreference = 0.35f;
                p.verticalMovementPreference = 0.65f;
                p.wobbleAmount = 0.16f;
                p.pauseChance = 0.16f;
                break;
            case 36: // royal shark
                p.swimmingPattern = FishSwimmingPattern.HunterChasingSwimmer;
                p.maximumSpeed *= 1.16f;
                p.burstSpeedChance = 0.26f;
                break;
            case 37: // seahorse
                p.swimmingPattern = FishSwimmingPattern.ZigzagSwimmer;
                p.turnSpeed *= 1.30f;
                p.wobbleAmount = 0.16f;
                break;
            case 38: // heavy lionfish
                p.swimmingPattern = FishSwimmingPattern.WideRoamingSwimmer;
                p.minimumSpeed *= 0.82f;
                p.maximumSpeed *= 0.88f;
                p.turnSpeed *= 0.78f;
                p.fishSizeSpacingMultiplier *= 1.18f;
                break;
            case 40: // mermaid-like main boss
                p.swimmingPattern = FishSwimmingPattern.UnpredictableRareFish;
                p.bossMinimumSpeed = 0.90f;
                p.bossMaximumSpeed = 1.68f;
                p.bossTurnSpeed = 68f;
                p.bossEnterNearPlayerChance = 0.38f;
                p.bossEnterAndRetreatChance = 0.28f;
                break;
            case 41: // Crystal Whale - very long body, smooth pass/retreat boss
                p.displayName = "Crystal Whale";
                p.swimmingPattern = FishSwimmingPattern.HeavyBossMovement;

                // Fast/slow breathing without a burst that can exceed the
                // authored 2.30 normal-swim maximum.
                p.minimumSpeed = 1.18f;
                p.maximumSpeed = 2.30f;
                p.acceleration = 1.05f;
                p.deceleration = 0.92f;
                p.turnSpeed = 18f;
                p.turnFrequency = 3.8f;
                p.burstSpeedChance = 0f;
                p.pauseChance = 0f;
                p.pauseDurationRange = new Vector2(0.10f, 0.28f);
                p.curveStrength = 0.28f;
                p.wobbleAmount = 0.012f;
                p.wobbleFrequency = 0.24f;
                p.animatorSpeedMultiplier = 1.08f;
                p.tailMovementSpeed = 1.12f;
                p.bodySwayAmount = 0.035f;

                p.useBossMovement = true;
                p.bossMinimumSpeed = 1.18f;
                p.bossMaximumSpeed = 2.30f;
                p.bossAcceleration = 1.05f;
                p.bossDeceleration = 0.92f;
                p.bossTurnSpeed = 18f;
                p.bossSteeringDegreesPerSecond = 18f;
                p.bossUseLongBodySteering = true;
                p.bossRetreatOutsideOnly = true;
                p.bossSpeedChangeIntervalRange = new Vector2(2.6f, 5.2f);
                p.bossPauseChance = 0f;
                p.bossNormalWorldSpeedCap = 2.30f;
                p.ignoreReactiveHitMotion = true;
                p.routeAdjustmentStrength = 0.06f;
                p.overlapCheckInterval = 0.34f;

                // Retreat is the signature behavior, but it is implemented as
                // a forward pass to an off-screen corridor. No visible 180 turn.
                p.bossEnterAndRetreatChance = 0.40f;
                p.bossCrossCenterChance = 0.96f;
                p.bossWideRouteChance = 0.20f;
                p.bossLoopRouteChance = 0f;
                p.bossEnterNearPlayerChance = 0.04f;
                p.bossRouteDistanceRange = new Vector2(1.85f, 3.35f);
                p.bossPauseDurationRange = new Vector2(0.08f, 0.16f);
                p.bossReturnDelayRange = new Vector2(0.35f, 0.85f);
                p.centerCrossingChance = 0.92f;
                p.edgeRouteChance = 0f;
                p.horizontalMovementPreference = 0.76f;
                p.verticalMovementPreference = 0.24f;
                p.routeLengthRange = new Vector2(1.80f, 3.20f);
                p.routeWeights = BuildRouteWeights(
                    2.10f,  // straight
                    1.30f,  // diagonal
                    2.30f,  // center
                    0.55f,  // wide curve
                    0.00f,  // edge
                    0.05f,  // S
                    0.00f   // loop
                );
                break;
            case 42: // treasure idol
                p.swimmingPattern = FishSwimmingPattern.StopAndGoSwimmer;
                p.bossPauseDurationRange = new Vector2(0.45f, 1.45f);
                p.bossTurnSpeed = 38f;
                p.bossWideRouteChance = 0.74f;
                break;
            case 44: // epic turtle
                p.damageResistance += 0.06f;
                p.bossMinimumSpeed *= 0.88f;
                p.bossMaximumSpeed *= 0.90f;
                p.bossTurnSpeed *= 0.80f;
                p.fishSizeSpacingMultiplier *= 1.15f;
                break;
            case 45: // throne/idol epic boss
                p.swimmingPattern = FishSwimmingPattern.StopAndGoSwimmer;
                p.bossPauseDurationRange = new Vector2(0.55f, 1.75f);
                p.bossWideRouteChance = 0.80f;
                p.bossLoopRouteChance = 0.34f;
                break;
        }

        p.maximumSpeed = Mathf.Max(p.minimumSpeed, p.maximumSpeed);
        p.bossMaximumSpeed = Mathf.Max(p.bossMinimumSpeed, p.bossMaximumSpeed);
        p.damageResistance = Mathf.Clamp(p.damageResistance, 0f, 0.75f);
    }

    private static void ApplyAcrossScreenSizeTuning(
        FishGameplayProfile p
    )
    {
        if (p == null)
        {
            return;
        }

        // Large fish should read clearly as deliberate screen-crossing targets
        // instead of circling too tightly. Small fish stay the fastest class.
        if (p.sizeClass == FishSizeClass.Large)
        {
            p.preferredEntryDirection = FishPreferredDirection.Any;
            p.preferredExitDirection = FishPreferredDirection.OppositeEntry;
            p.horizontalMovementPreference = Mathf.Max(
                p.horizontalMovementPreference,
                0.86f
            );
            p.verticalMovementPreference = Mathf.Min(
                p.verticalMovementPreference,
                0.14f
            );
            p.centerCrossingChance = Mathf.Max(
                p.centerCrossingChance,
                0.68f
            );
            p.edgeRouteChance = Mathf.Min(p.edgeRouteChance, 0.07f);
            p.pauseChance = Mathf.Min(p.pauseChance, 0.055f);
            p.returnChance = Mathf.Min(p.returnChance, 0.12f);
            p.routeLengthRange = new Vector2(
                Mathf.Max(1.05f, p.routeLengthRange.x),
                Mathf.Max(2.15f, p.routeLengthRange.y)
            );
            p.routeWeights = BuildRouteWeights(
                1.55f,
                1.25f,
                1.55f,
                0.95f,
                0.08f,
                0.55f,
                0.20f
            );
        }
        else if (p.sizeClass == FishSizeClass.Huge)
        {
            p.preferredEntryDirection = FishPreferredDirection.Any;
            p.preferredExitDirection = FishPreferredDirection.OppositeEntry;
            p.horizontalMovementPreference = Mathf.Max(
                p.horizontalMovementPreference,
                0.90f
            );
            p.verticalMovementPreference = Mathf.Min(
                p.verticalMovementPreference,
                0.10f
            );
            p.centerCrossingChance = Mathf.Max(
                p.centerCrossingChance,
                0.72f
            );
            p.edgeRouteChance = Mathf.Min(p.edgeRouteChance, 0.05f);
            p.pauseChance = Mathf.Min(p.pauseChance, 0.07f);
            p.returnChance = Mathf.Min(p.returnChance, 0.14f);
            p.routeLengthRange = new Vector2(
                Mathf.Max(1.25f, p.routeLengthRange.x),
                Mathf.Max(2.55f, p.routeLengthRange.y)
            );
            p.routeWeights = BuildRouteWeights(
                1.75f,
                1.35f,
                1.75f,
                1.10f,
                0.05f,
                0.45f,
                0.22f
            );
        }
        else if (p.sizeClass == FishSizeClass.Boss)
        {
            p.preferredExitDirection = FishPreferredDirection.OppositeEntry;

            if (p.bossUseLongBodySteering)
            {
                // Long bosses should make deliberate center-screen passes,
                // not inherit the generic boss preference for wide circling.
                p.bossCrossCenterChance = Mathf.Max(
                    p.bossCrossCenterChance,
                    0.94f
                );
                p.bossWideRouteChance = Mathf.Min(
                    p.bossWideRouteChance,
                    0.25f
                );
                p.bossLoopRouteChance = 0f;
                p.centerCrossingChance = Mathf.Max(
                    p.centerCrossingChance,
                    0.90f
                );
                p.edgeRouteChance = 0f;
            }
            else
            {
                p.bossCrossCenterChance = Mathf.Max(
                    p.bossCrossCenterChance,
                    0.74f
                );
                p.bossWideRouteChance = Mathf.Max(
                    p.bossWideRouteChance,
                    0.70f
                );
            }

            p.bossRouteDistanceRange = new Vector2(
                Mathf.Max(1.45f, p.bossRouteDistanceRange.x),
                Mathf.Max(3.0f, p.bossRouteDistanceRange.y)
            );
        }
    }

    private static void ApplyCleanCombatBalance(
        FishGameplayProfile profile,
        int fishIndex
    )
    {
        if (profile == null)
        {
            return;
        }

        FishArcadeBalanceModel.GetFishCombatPreset(
            fishIndex,
            out float health,
            out float resistance,
            out float reward,
            out FishTier tier,
            out FishRarity rarity,
            out FishSizeClass sizeClass
        );

        profile.fishTier = tier;
        profile.rarity = rarity;
        profile.sizeClass = sizeClass;
        profile.fishCategory = fishIndex <= 11
            ? "Small Fish"
            : fishIndex <= 23
                ? "Medium Fish"
                : fishIndex <= 26
                    ? "Large Fish"
                    : fishIndex <= 30
                        ? "Rare Large Fish"
                        : fishIndex <= 35
                            ? "Special Fish"
                            : fishIndex <= 38
                                ? "Mini Boss"
                                : fishIndex <= 42
                                    ? "Main Boss"
                                    : "Epic Boss";
        profile.SetCombatBalanceAbsolute(
            FishArcadeBalanceModel.BalanceVersion,
            health,
            resistance,
            reward
        );
    }


    private static void CaptureAssignedDeathProfiles()
    {
        ReleaseDeathProfileSnapshots();

        SwapFishScript[] directors =
            UnityEngine.Object.FindObjectsOfType<SwapFishScript>(true);

        for (int d = 0; d < directors.Length; d++)
        {
            SwapFishScript director = directors[d];
            if (director == null || director.Fish == null)
            {
                continue;
            }

            for (int i = 0; i < director.Fish.Length; i++)
            {
                GameObject prefab = director.Fish[i];
                if (prefab == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(path) ||
                    RebuildDeathSnapshots.ContainsKey(path))
                {
                    continue;
                }

                FishDeathProfile source = GetAssignedDeathProfile(prefab);
                if (source == null)
                {
                    continue;
                }

                FishDeathProfile snapshot =
                    ScriptableObject.CreateInstance<FishDeathProfile>();
                snapshot.hideFlags = HideFlags.HideAndDontSave;
                EditorUtility.CopySerialized(source, snapshot);
                RebuildDeathSnapshots[path] = snapshot;
            }
        }
    }

    private static FishDeathProfile GetCapturedDeathProfile(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        RebuildDeathSnapshots.TryGetValue(path, out FishDeathProfile snapshot);
        return snapshot;
    }

    private static void ReleaseDeathProfileSnapshots()
    {
        foreach (KeyValuePair<string, FishDeathProfile> pair
                 in RebuildDeathSnapshots)
        {
            if (pair.Value != null)
            {
                UnityEngine.Object.DestroyImmediate(pair.Value);
            }
        }

        RebuildDeathSnapshots.Clear();
    }

    private static void DeleteGeneratedFolder(string path)
    {
        if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static int RemoveLegacyGeneratorScripts()
    {
        int deleted = 0;

        for (int i = 0; i < LegacyEditorGeneratorScripts.Length; i++)
        {
            string path = LegacyEditorGeneratorScripts[i];
            if (string.Equals(
                    path,
                    "Assets/Scripts/Editor/FishArcadeV28Setup.cs",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) != null &&
                AssetDatabase.DeleteAsset(path))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static int PrepareEmbeddedFrontGunPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { SkillFrontGunFolder }
        );

        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefabAsset == null ||
                prefabAsset.name.IndexOf(
                    "PF_BossReward",
                    StringComparison.OrdinalIgnoreCase
                ) < 0)
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (root == null)
                {
                    continue;
                }

                FrontGunRewardEffectSpeed speed =
                    root.GetComponent<FrontGunRewardEffectSpeed>();
                if (speed == null)
                {
                    speed = root.AddComponent<FrontGunRewardEffectSpeed>();
                }

                SerializedObject speedSo = new SerializedObject(speed);
                SetFloat(speedSo, "animatorSpeed", 2f);
                SetBool(speedSo, "includeChildAnimators", true);
                SetBool(speedSo, "forceAlwaysAnimate", true);
                SetBool(speedSo, "restartAnimatorOnEnable", false);
                speedSo.ApplyModifiedPropertiesWithoutUndo();

                FrontGunRewardPresentation presentation =
                    root.GetComponent<FrontGunRewardPresentation>();
                if (presentation == null)
                {
                    presentation =
                        root.AddComponent<FrontGunRewardPresentation>();
                }

                FrontGunRewardCounterText counter =
                    root.GetComponentInChildren<FrontGunRewardCounterText>(true);

                if (counter == null)
                {
                    TMP_Text rewardText = FindRewardText(root);
                    if (rewardText == null)
                    {
                        rewardText = CreateStarterRewardText(root.transform);
                    }

                    counter =
                        rewardText.GetComponent<FrontGunRewardCounterText>();
                    if (counter == null)
                    {
                        counter = rewardText.gameObject.AddComponent<
                            FrontGunRewardCounterText>();
                    }
                }

                SerializedObject counterSo = new SerializedObject(counter);
                SetFloat(counterSo, "startDelay", 0.08f);
                SetFloat(counterSo, "countDuration", 0.60f);
                SetFloat(counterSo, "startPercent", 0f);
                SetString(counterSo, "defaultPrefix", "+");
                SetBool(counterSo, "useThousandsSeparator", true);
                SetBool(counterSo, "roundToWholeCoins", true);
                SetBool(counterSo, "animateScale", true);
                SetFloat(counterSo, "finalPunchScale", 1.12f);
                SetFloat(counterSo, "finalPunchDuration", 0.12f);
                counterSo.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject presentationSo =
                    new SerializedObject(presentation);
                SetObjectReference(
                    presentationSo,
                    "rewardCounter",
                    counter
                );
                SerializedProperty anchor =
                    presentationSo.FindProperty("rewardTextAnchor");
                if (anchor != null)
                {
                    anchor.objectReferenceValue =
                        counter.transform.parent != null
                            ? counter.transform.parent
                            : counter.transform;
                }
                SetBool(
                    presentationSo,
                    "autoFindCounterInChildren",
                    true
                );
                SetString(
                    presentationSo,
                    "preferredRewardTextObjectName",
                    "RewardText"
                );
                SetBool(
                    presentationSo,
                    "hideTextUntilRewardIsPlayed",
                    true
                );
                presentationSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                updated++;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        return updated;
    }

    private static TMP_Text FindRewardText(GameObject root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (string.Equals(
                    text.name,
                    "RewardText",
                    StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            if (fallback == null &&
                text.name.IndexOf(
                    "reward",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                fallback = text;
            }
        }

        return fallback;
    }

    private static TMP_Text CreateStarterRewardText(Transform root)
    {
        Transform anchor = root.Find("RewardTextAnchor");
        if (anchor == null)
        {
            GameObject anchorObject = new GameObject("RewardTextAnchor");
            anchor = anchorObject.transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, -0.15f, 0f);
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
        }

        GameObject textObject = new GameObject("RewardText");
        textObject.transform.SetParent(anchor, false);
        textObject.transform.localPosition = Vector3.zero;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
        tmp.text = "+0";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.84f, 0.08f, 1f);
        tmp.fontSize = 5.5f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 2f;
        tmp.fontSizeMax = 8f;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.rectTransform.sizeDelta = new Vector2(3.2f, 0.8f);

        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        Renderer renderer = tmp.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "SkillFrontGun";
            renderer.sortingOrder = 50;
        }

        return tmp;
    }

    private static int ConfigureFrontGunManagers()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { SkillFrontGunFolder }
        );

        List<string> paths = new List<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null &&
                prefab.name.IndexOf(
                    "PF_BossReward",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);

        List<GameObject> skills = new List<GameObject>();
        for (int i = 0; i < paths.Count; i++)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab != null)
            {
                skills.Add(prefab);
            }
        }

        if (skills.Count < 4)
        {
            Debug.LogWarning(
                "[Fish Arcade V28] Main Boss mapping expects 4 " +
                "PF_BossReward prefabs. Found " + skills.Count +
                " in " + SkillFrontGunFolder +
                ". Missing slots temporarily reuse the last available reward " +
                "prefab so Fish 42 index 3 still plays. Add PF_BossReward_04 " +
                "and rerun V27 for a unique fourth effect."
            );
        }

        int requiredSlots = Mathf.Max(4, skills.Count);
        GameObject[] mappedSkills = new GameObject[requiredSlots];

        for (int i = 0; i < mappedSkills.Length; i++)
        {
            if (i < skills.Count)
            {
                mappedSkills[i] = skills[i];
            }
            else if (skills.Count > 0)
            {
                mappedSkills[i] = skills[skills.Count - 1];
            }
        }

        AnimatiorManager[] managers =
            UnityEngine.Object.FindObjectsOfType<AnimatiorManager>(true);

        int updated = 0;

        for (int i = 0; i < managers.Length; i++)
        {
            AnimatiorManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            Undo.RecordObject(
                manager,
                "Configure V27 Main Boss Front-Gun Skills"
            );

            manager.mainBossFrontGunSkillPrefabs = mappedSkills;

            float[] previous =
                manager.mainBossFrontGunSkillVisibleDurations;
            float[] durations = new float[mappedSkills.Length];

            for (int j = 0; j < durations.Length; j++)
            {
                float oldValue =
                    previous != null && j < previous.Length
                        ? previous[j]
                        : 0f;
                durations[j] = oldValue > 0f ? oldValue : 2.5f;
            }

            manager.mainBossFrontGunSkillVisibleDurations = durations;
            manager.mainBossCoinTravelTimeout =
                Mathf.Max(1.25f, manager.mainBossCoinTravelTimeout);

            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    manager.gameObject.scene
                );
            }

            updated++;
        }

        return updated;
    }

    private static WeightedFishRoute[] BuildRouteWeights(
        float straight,
        float diagonal,
        float center,
        float wide,
        float edge,
        float sShape,
        float approach
    )
    {
        return new[]
        {
            new WeightedFishRoute { route = FishRoutePattern.StraightCrossing, weight = straight },
            new WeightedFishRoute { route = FishRoutePattern.DiagonalCrossing, weight = diagonal },
            new WeightedFishRoute { route = FishRoutePattern.CenterCrossing, weight = center },
            new WeightedFishRoute { route = FishRoutePattern.WideCurvedRoute, weight = wide },
            new WeightedFishRoute { route = FishRoutePattern.EdgeRoute, weight = edge },
            new WeightedFishRoute { route = FishRoutePattern.SShapedRoute, weight = sShape },
            new WeightedFishRoute { route = FishRoutePattern.PlayerApproachRoute, weight = approach }
        };
    }

    private static int GetMinimumLevel(int fishIndex)
    {
        if (fishIndex <= 11) return 1 + fishIndex / 6;
        if (fishIndex <= 30) return 1 + (fishIndex - 12) / 5;
        if (fishIndex <= 35) return 2 + (fishIndex - 31);
        if (fishIndex <= 38) return 2 + (fishIndex - 36) * 2;
        if (fishIndex <= 42) return Mathf.Clamp(1 + (fishIndex - 39), 1, 6);
        return 6;
    }

    private static string GetRecommendedName(int fishIndex, string fallback)
    {
        if (fishIndex >= 0 && fishIndex < RecommendedNames.Length)
        {
            return RecommendedNames[fishIndex];
        }

        return string.IsNullOrWhiteSpace(fallback)
            ? "Fish " + fishIndex
            : fallback;
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }
        return value.Replace(' ', '_');
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
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

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBoolean(
        SerializedObject serializedObject,
        string propertyName,
        bool value
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetInteger(
        SerializedObject serializedObject,
        string propertyName,
        int value
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }
}
#endif
