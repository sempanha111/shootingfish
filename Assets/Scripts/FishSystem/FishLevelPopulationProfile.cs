using UnityEngine;

[CreateAssetMenu(
    menuName = "Fish Arcade/Level Population Profile",
    fileName = "FishLevelPopulationProfile"
)]
public sealed class FishLevelPopulationProfile : ScriptableObject
{
    [Header("Identity")]
    [Range(1, 100)] public int levelNumber = 1;
    public string displayName = "Ocean Level";

    [Header("Ambient Size Mix")]
    [Range(0f, 1f)] public float smallFishPercentage = 0.58f;
    [Range(0f, 1f)] public float mediumFishPercentage = 0.42f;
    [Range(0f, 1f)] public float largeFishPercentage = 0.08f;
    [Range(0f, 1f)] public float specialFishPercentage = 0.02f;

    [Header("Rarity Mix")]
    [Range(0f, 1f)] public float commonPercentage = 0.72f;
    [Range(0f, 1f)] public float uncommonPercentage = 0.22f;
    [Range(0f, 1f)] public float rarePercentage = 0.055f;
    [Range(0f, 1f)] public float epicPercentage = 0.005f;

    [Header("Population Limits")]
    [Range(1, 100)] public int maximumActiveFish = 26;
    [Range(0, 100)] public int minimumActiveFish = 18;
    [Range(0, 20)] public int maximumLargeFish = 3;
    [Range(0, 20)] public int maximumGroups = 4;

    [Header("Progression")]
    [Min(1f)] public float bossSpawnTiming = 10f;
    [Range(0.5f, 2f)] public float fishHealthMultiplier = 1f;
    [Range(0.5f, 2f)] public float fishSpeedMultiplier = 1f;
    [Range(0.5f, 3f)] public float rewardMultiplier = 1f;
    [Range(0f, 1f)] public float evasiveBehaviorChance = 0.05f;
    [Range(0f, 1f)] public float complexRouteChance = 0.10f;

    public float GetSmallFishSelectionWeight()
    {
        float total = Mathf.Max(0.0001f, smallFishPercentage + mediumFishPercentage);
        return Mathf.Clamp01(smallFishPercentage / total);
    }


    public float GetSizeWeight(FishSizeClass sizeClass)
    {
        if (sizeClass == FishSizeClass.Boss)
        {
            return 0f;
        }

        bool isLarge = sizeClass == FishSizeClass.Large ||
                       sizeClass == FishSizeClass.Huge;
        float desired = isLarge
            ? largeFishPercentage
            : 1f - largeFishPercentage;
        float baseline = isLarge ? 0.08f : 0.92f;
        return Mathf.Clamp(desired / Mathf.Max(0.0001f, baseline), 0.05f, 5f);
    }

    public float GetRarityWeight(FishRarity rarity)
    {
        float desired;
        float baseline;
        switch (rarity)
        {
            case FishRarity.Uncommon:
                desired = uncommonPercentage;
                baseline = 0.22f;
                break;
            case FishRarity.Rare:
                desired = rarePercentage;
                baseline = 0.055f;
                break;
            case FishRarity.Epic:
            case FishRarity.Legendary:
                desired = epicPercentage;
                baseline = 0.005f;
                break;
            case FishRarity.Boss:
                return 0f;
            default:
                desired = commonPercentage;
                baseline = 0.72f;
                break;
        }

        return Mathf.Clamp(desired / Mathf.Max(0.0001f, baseline), 0.05f, 5f);
    }

    private void OnValidate()
    {
        levelNumber = Mathf.Max(1, levelNumber);
        maximumActiveFish = Mathf.Max(1, maximumActiveFish);
        minimumActiveFish = Mathf.Clamp(minimumActiveFish, 0, maximumActiveFish);
        maximumLargeFish = Mathf.Clamp(maximumLargeFish, 0, maximumActiveFish);
        maximumGroups = Mathf.Clamp(maximumGroups, 0, maximumActiveFish);
        bossSpawnTiming = Mathf.Max(1f, bossSpawnTiming);
    }
}
