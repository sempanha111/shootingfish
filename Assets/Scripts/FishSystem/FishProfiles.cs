using System;
using UnityEngine;

public enum FishTier
{
    Small,
    Medium,
    Special,
    MiniBoss,
    MainBoss
}

public enum RewardTextStyle
{
    None,
    Small,
    Big,
    Custom
}

public enum FishHealthPhase
{
    Healthy,
    Aggressive,
    Critical
}

public enum FishRewardCalculationMode
{
    LegacyMultiplyByShotCost,
    FixedFishCoin,
    FishCoinWithSmallShotBonus
}

[Serializable]
public struct WeightedSwimStyle
{
    public FishScript.SwimStyle style;

    [Min(0f)]
    public float weight;
}
