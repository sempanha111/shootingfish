#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// v14 one-time balance migration.
/// - Makes fish substantially more durable and more rewarding.
/// - Makes small fish clearly faster.
/// - Makes Large/Huge fish favor long across-screen routes.
/// - Keeps bosses deliberate while increasing cross-center travel.
/// Safe to run repeatedly because combat and movement upgrades are versioned.
/// </summary>
public static class FishProfileBalanceUpgradeV14
{
    private const int PreviousCombatVersion = 13;
    private const int TargetBalanceVersion = 14;

    [MenuItem("Tools/Fish Arcade/v14/Apply Durable Fish + Across-Screen Balance")]
    public static void ApplyUpgrade()
    {
        int gameplayProfiles = UpgradeGameplayProfiles();
        int levelProfiles = UpgradeLevelProfiles();
        int fallbackPrefabs = UpgradeFallbackPrefabs();
        int economyManagers = UpgradeOpenSceneEconomy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Fish Arcade v14 Balance Complete",
            "Gameplay profiles updated: " + gameplayProfiles +
            "\nLevel population profiles updated: " + levelProfiles +
            "\nFallback prefabs updated: " + fallbackPrefabs +
            "\nOpen-scene GameManagers economy updated: " + economyManagers +
            "\n\nSmall fish are faster, large fish cross the screen more, all tiers have higher HP/reward, and cannon costs are gentler.",
            "OK"
        );
    }

    private static int UpgradeOpenSceneEconomy()
    {
        GameManager[] managers =
            Object.FindObjectsOfType<GameManager>(true);
        int updated = 0;

        for (int i = 0; i < managers.Length; i++)
        {
            GameManager manager = managers[i];

            if (manager == null)
            {
                continue;
            }

            manager.ApplyV14DurableFishEconomy();
            EditorUtility.SetDirty(manager);
            updated++;
        }

        return updated;
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

            if (profile.CombatBalanceVersion < PreviousCombatVersion)
            {
                GetV13AbsoluteMultipliers(
                    profile.fishTier,
                    out float oldHealth,
                    out float oldReward
                );

                changed |= profile.ApplyCombatBalanceUpgrade(
                    PreviousCombatVersion,
                    oldHealth,
                    oldReward
                );
            }

            if (profile.CombatBalanceVersion < TargetBalanceVersion)
            {
                GetV13AbsoluteMultipliers(
                    profile.fishTier,
                    out float oldHealth,
                    out float oldReward
                );
                GetV14AbsoluteMultipliers(
                    profile.fishTier,
                    out float newHealth,
                    out float newReward
                );

                changed |= profile.ApplyCombatBalanceUpgrade(
                    TargetBalanceVersion,
                    Mathf.Max(1f, newHealth / Mathf.Max(0.01f, oldHealth)),
                    Mathf.Max(1f, newReward / Mathf.Max(0.01f, oldReward))
                );
            }

            if (profile.MovementBalanceVersion < TargetBalanceVersion)
            {
                GetMovementMultipliers(
                    profile.fishTier,
                    out float minimumSpeedMultiplier,
                    out float maximumSpeedMultiplier,
                    out float accelerationMultiplier,
                    out float turnSpeedMultiplier,
                    out float animatorMultiplier
                );

                changed |= profile.ApplyMovementBalanceUpgrade(
                    TargetBalanceVersion,
                    minimumSpeedMultiplier,
                    maximumSpeedMultiplier,
                    accelerationMultiplier,
                    turnSpeedMultiplier,
                    animatorMultiplier
                );

                changed |= ApplyAcrossScreenTuning(profile);
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

    private static bool ApplyAcrossScreenTuning(FishGameplayProfile p)
    {
        bool changed = false;

        if (p.sizeClass == FishSizeClass.Large)
        {
            changed |= SetPreferredExit(p, FishPreferredDirection.OppositeEntry);
            changed |= Raise(ref p.horizontalMovementPreference, 0.86f);
            changed |= Lower(ref p.verticalMovementPreference, 0.14f);
            changed |= Raise(ref p.centerCrossingChance, 0.68f);
            changed |= Lower(ref p.edgeRouteChance, 0.07f);
            changed |= Lower(ref p.pauseChance, 0.055f);
            changed |= Lower(ref p.returnChance, 0.12f);
            changed |= RaiseRouteLength(ref p.routeLengthRange, 1.05f, 2.15f);
            changed |= SetRouteWeights(
                p,
                BuildAcrossRouteWeights(1.55f, 1.25f, 1.55f, 0.95f, 0.08f, 0.55f, 0.20f)
            );
        }
        else if (p.sizeClass == FishSizeClass.Huge)
        {
            changed |= SetPreferredExit(p, FishPreferredDirection.OppositeEntry);
            changed |= Raise(ref p.horizontalMovementPreference, 0.90f);
            changed |= Lower(ref p.verticalMovementPreference, 0.10f);
            changed |= Raise(ref p.centerCrossingChance, 0.72f);
            changed |= Lower(ref p.edgeRouteChance, 0.05f);
            changed |= Lower(ref p.pauseChance, 0.07f);
            changed |= Lower(ref p.returnChance, 0.14f);
            changed |= RaiseRouteLength(ref p.routeLengthRange, 1.25f, 2.55f);
            changed |= SetRouteWeights(
                p,
                BuildAcrossRouteWeights(1.75f, 1.35f, 1.75f, 1.10f, 0.05f, 0.45f, 0.22f)
            );
        }
        else if (p.sizeClass == FishSizeClass.Boss)
        {
            changed |= SetPreferredExit(p, FishPreferredDirection.OppositeEntry);
            changed |= Raise(ref p.bossCrossCenterChance, 0.74f);
            changed |= Raise(ref p.bossWideRouteChance, 0.70f);
            changed |= RaiseRouteLength(
                ref p.bossRouteDistanceRange,
                1.45f,
                3.0f
            );
        }

        return changed;
    }

    private static int UpgradeLevelProfiles()
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

            int levelIndex = Mathf.Clamp(profile.levelNumber - 1, 0, 5);
            float targetHealth = 1f + levelIndex * 0.05f;
            float targetSpeed = 1f + levelIndex * 0.02f;
            float targetReward = 1f + levelIndex * 0.05f;
            bool changed = false;

            changed |= Raise(ref profile.fishHealthMultiplier, targetHealth);
            changed |= Raise(ref profile.fishSpeedMultiplier, targetSpeed);
            changed |= Raise(ref profile.rewardMultiplier, targetReward);

            if (!changed)
            {
                continue;
            }

            EditorUtility.SetDirty(profile);
            updated++;
        }

        return updated;
    }

    private static int UpgradeFallbackPrefabs()
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

                if (fish == null || fish.GetGameplayProfile() != null)
                {
                    continue;
                }

                FishTier tier = fish.GetFishTier();

                if (fish.FallbackCombatBalanceVersion < PreviousCombatVersion)
                {
                    GetV13AbsoluteMultipliers(
                        tier,
                        out float oldHealth,
                        out float oldReward
                    );
                    changed |= fish.ApplyFallbackCombatBalanceUpgrade(
                        PreviousCombatVersion,
                        oldHealth,
                        oldReward
                    );
                }

                if (fish.FallbackCombatBalanceVersion < TargetBalanceVersion)
                {
                    GetV13AbsoluteMultipliers(
                        tier,
                        out float oldHealth,
                        out float oldReward
                    );
                    GetV14AbsoluteMultipliers(
                        tier,
                        out float newHealth,
                        out float newReward
                    );
                    changed |= fish.ApplyFallbackCombatBalanceUpgrade(
                        TargetBalanceVersion,
                        Mathf.Max(1f, newHealth / Mathf.Max(0.01f, oldHealth)),
                        Mathf.Max(1f, newReward / Mathf.Max(0.01f, oldReward))
                    );
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

    private static void GetV13AbsoluteMultipliers(
        FishTier tier,
        out float health,
        out float reward
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                health = 2.10f;
                reward = 1.12f;
                break;
            case FishTier.Medium:
                health = 2.20f;
                reward = 1.14f;
                break;
            case FishTier.Special:
                health = 2.30f;
                reward = 1.18f;
                break;
            case FishTier.MiniBoss:
                health = 2.00f;
                reward = 1.18f;
                break;
            default:
                health = 1.85f;
                reward = 1.20f;
                break;
        }
    }

    private static void GetV14AbsoluteMultipliers(
        FishTier tier,
        out float health,
        out float reward
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                health = 3.40f;
                reward = 2.10f;
                break;
            case FishTier.Medium:
                health = 3.40f;
                reward = 2.30f;
                break;
            case FishTier.Special:
                health = 3.60f;
                reward = 2.50f;
                break;
            case FishTier.MiniBoss:
                health = 2.85f;
                reward = 2.70f;
                break;
            default:
                health = 2.65f;
                reward = 2.60f;
                break;
        }
    }

    private static void GetMovementMultipliers(
        FishTier tier,
        out float minSpeed,
        out float maxSpeed,
        out float acceleration,
        out float turnSpeed,
        out float animator
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                minSpeed = 1.22f;
                maxSpeed = 1.22f;
                acceleration = 1.12f;
                turnSpeed = 1.06f;
                animator = 1.10f;
                break;
            case FishTier.Medium:
                minSpeed = 1.15f;
                maxSpeed = 1.16f;
                acceleration = 1.08f;
                turnSpeed = 1.02f;
                animator = 1.05f;
                break;
            case FishTier.Special:
                minSpeed = 1.12f;
                maxSpeed = 1.14f;
                acceleration = 1.06f;
                turnSpeed = 1.00f;
                animator = 1.03f;
                break;
            case FishTier.MiniBoss:
                minSpeed = 1.06f;
                maxSpeed = 1.06f;
                acceleration = 1.03f;
                turnSpeed = 0.98f;
                animator = 1.00f;
                break;
            default:
                minSpeed = 1.06f;
                maxSpeed = 1.07f;
                acceleration = 1.02f;
                turnSpeed = 0.98f;
                animator = 1.00f;
                break;
        }
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

    private static bool Lower(ref float value, float maximum)
    {
        if (value <= maximum)
        {
            return false;
        }

        value = maximum;
        return true;
    }

    private static bool RaiseRouteLength(
        ref Vector2 value,
        float minimumX,
        float minimumY
    )
    {
        Vector2 next = new Vector2(
            Mathf.Max(value.x, minimumX),
            Mathf.Max(value.y, minimumY)
        );

        if (next == value)
        {
            return false;
        }

        value = next;
        return true;
    }

    private static WeightedFishRoute[] BuildAcrossRouteWeights(
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

    private static bool SetRouteWeights(
        FishGameplayProfile profile,
        WeightedFishRoute[] target
    )
    {
        if (profile == null || target == null)
        {
            return false;
        }

        profile.routeWeights = target;
        return true;
    }

    private static bool SetPreferredExit(
        FishGameplayProfile profile,
        FishPreferredDirection direction
    )
    {
        if (profile.preferredExitDirection == direction)
        {
            return false;
        }

        profile.preferredExitDirection = direction;
        return true;
    }
}
#endif
