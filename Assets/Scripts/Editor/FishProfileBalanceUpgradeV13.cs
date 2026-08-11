#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time asset migration for the v13 multi-gun profile balance and spawn mix.
/// Health and rewards are written into the existing FishGameplayProfile
/// assets, so pooled runtime fish read one authoritative profile with no
/// per-frame or per-cannon balance layer.
/// </summary>
public static class FishProfileBalanceUpgradeV13
{
    private const int TargetBalanceVersion = 13;

    [MenuItem("Tools/Fish Arcade/v13/Apply Profile, Variety and Padding Upgrade")]
    public static void ApplyUpgrade()
    {
        int updatedProfiles = UpgradeGameplayProfiles();
        int updatedLevelProfiles = UpgradeLevelPopulationProfiles();
        int updatedPrefabs = UpgradeFishPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Fish Arcade v13 Upgrade Complete",
            "Gameplay profiles upgraded: " + updatedProfiles +
            "\nLevel population profiles updated: " + updatedLevelProfiles +
            "\nFish prefabs updated: " + updatedPrefabs +
            "\n\nThe upgrade is versioned and safe to run again.",
            "OK"
        );
    }

    private static int UpgradeGameplayProfiles()
    {
        string[] guids = AssetDatabase.FindAssets("t:FishGameplayProfile");
        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FishGameplayProfile profile =
                AssetDatabase.LoadAssetAtPath<FishGameplayProfile>(path);

            if (profile == null)
            {
                continue;
            }

            bool changed = false;
            GetCombatMultipliers(
                profile.fishTier,
                out float healthMultiplier,
                out float rewardMultiplier
            );

            if (profile.ApplyCombatBalanceUpgrade(
                    TargetBalanceVersion,
                    healthMultiplier,
                    rewardMultiplier
                ))
            {
                changed = true;
            }

            if (profile.screenEdgePadding <
                FishScreenBounds.MinimumViewportPadding)
            {
                profile.screenEdgePadding =
                    FishScreenBounds.MinimumViewportPadding;
                changed = true;
            }

            // Give large/rare/special fish a usable authored weight while the
            // central spawn director still enforces population limits.
            if (profile.fishTier == FishTier.Special &&
                profile.spawnWeight < 0.16f)
            {
                profile.spawnWeight = 0.16f;
                profile.maximumSimultaneousCount = Mathf.Clamp(
                    profile.maximumSimultaneousCount,
                    1,
                    2
                );
                changed = true;
            }
            else if ((profile.sizeClass == FishSizeClass.Large ||
                      profile.sizeClass == FishSizeClass.Huge) &&
                     profile.spawnWeight < 0.22f)
            {
                profile.spawnWeight = 0.22f;
                changed = true;
            }
            else if ((profile.rarity == FishRarity.Rare ||
                      profile.rarity == FishRarity.Epic ||
                      profile.rarity == FishRarity.Legendary) &&
                     profile.spawnWeight < 0.18f)
            {
                profile.spawnWeight = 0.18f;
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            EditorUtility.SetDirty(profile);
            updated++;
        }

        return updated;
    }

    private static int UpgradeLevelPopulationProfiles()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:FishLevelPopulationProfile"
        );
        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FishLevelPopulationProfile profile =
                AssetDatabase.LoadAssetAtPath<FishLevelPopulationProfile>(path);

            if (profile == null)
            {
                continue;
            }

            int level = Mathf.Clamp(profile.levelNumber, 1, 6);
            float minimumLarge = 0.06f + (level - 1) * 0.02f;
            float minimumSpecial = 0.03f + (level - 1) * 0.018f;
            float minimumRare = 0.05f + (level - 1) * 0.024f;

            bool changed = false;
            changed |= Raise(ref profile.largeFishPercentage, minimumLarge);
            changed |= Raise(
                ref profile.specialFishPercentage,
                Mathf.Min(0.12f, minimumSpecial)
            );
            changed |= Raise(
                ref profile.rarePercentage,
                Mathf.Min(0.17f, minimumRare)
            );

            int desiredLargeLimit = Mathf.Max(
                2,
                Mathf.CeilToInt(
                    profile.maximumActiveFish *
                    profile.largeFishPercentage * 1.25f
                )
            );
            if (profile.maximumLargeFish < desiredLargeLimit)
            {
                profile.maximumLargeFish = Mathf.Min(
                    desiredLargeLimit,
                    profile.maximumActiveFish
                );
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            EditorUtility.SetDirty(profile);
            updated++;
        }

        return updated;
    }

    private static int UpgradeFishPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/Prefabs/FishGroup" }
        );
        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            try
            {
                FishScript fish = root.GetComponentInChildren<FishScript>(true);
                if (fish == null)
                {
                    continue;
                }

                FishGameplayProfile profile = fish.GetGameplayProfile();
                FishTier tier = fish.GetFishTier();

                if (profile == null)
                {
                    GetCombatMultipliers(
                        tier,
                        out float healthMultiplier,
                        out float rewardMultiplier
                    );
                    changed |= fish.ApplyFallbackCombatBalanceUpgrade(
                        TargetBalanceVersion,
                        healthMultiplier,
                        rewardMultiplier
                    );
                }

                if (tier == FishTier.Special)
                {
                    SerializedObject serialized = new SerializedObject(fish);
                    SerializedProperty mode =
                        serialized.FindProperty("naturalSpawnMode");
                    SerializedProperty weight =
                        serialized.FindProperty("ambientSpawnWeight");
                    SerializedProperty maximum =
                        serialized.FindProperty("maximumSimultaneousCount");

                    if (mode != null &&
                        mode.enumValueIndex ==
                        (int)FishScript.NaturalSpawnMode.EventOnly)
                    {
                        mode.enumValueIndex =
                            (int)FishScript.NaturalSpawnMode.Solo;
                        changed = true;
                    }

                    float desiredWeight = profile != null
                        ? Mathf.Max(0.16f, profile.spawnWeight)
                        : 0.16f;
                    if (weight != null && weight.floatValue < desiredWeight)
                    {
                        weight.floatValue = desiredWeight;
                        changed = true;
                    }

                    if (maximum != null &&
                        (maximum.intValue < 1 || maximum.intValue > 2))
                    {
                        maximum.intValue = Mathf.Clamp(
                            maximum.intValue,
                            1,
                            2
                        );
                        changed = true;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    updated++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return updated;
    }

    private static bool Raise(ref float value, float minimum)
    {
        if (value >= minimum)
        {
            return false;
        }

        value = minimum;
        return true;
    }

    private static void GetCombatMultipliers(
        FishTier tier,
        out float healthMultiplier,
        out float rewardMultiplier
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                healthMultiplier = 2.10f;
                rewardMultiplier = 1.12f;
                break;
            case FishTier.Medium:
                healthMultiplier = 2.20f;
                rewardMultiplier = 1.14f;
                break;
            case FishTier.Special:
                healthMultiplier = 2.30f;
                rewardMultiplier = 1.18f;
                break;
            case FishTier.MiniBoss:
                healthMultiplier = 2.00f;
                rewardMultiplier = 1.18f;
                break;
            default:
                healthMultiplier = 1.85f;
                rewardMultiplier = 1.20f;
                break;
        }
    }
}
#endif
