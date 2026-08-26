using UnityEngine;

/// <summary>
/// Single source of truth for the clean fish-arcade economy.
/// Bullet cost, damage, fish HP, resistance and reward all derive from this
/// model so editor regeneration never stacks old balance multipliers.
/// </summary>
public static class FishArcadeBalanceModel
{
    public const int BalanceVersion = 23;
    public const int GunLevelCount = 17;
    public const float PlayerFireRate = 3.5f;
    public const float CostPerDamage = 0.65f;

    private static readonly float[] GunDamage =
    {
        10f, 20f, 30f, 50f, 80f, 120f, 180f, 250f, 350f,
        500f, 700f, 1000f, 1400f, 1900f, 2600f, 3500f, 5000f
    };

    private static readonly float[] GunCost =
    {
        7f, 13f, 20f, 33f, 50f, 80f, 115f, 165f, 230f,
        325f, 455f, 650f, 910f, 1235f, 1690f, 2275f, 3250f
    };

    private static readonly float[] SmallHealth =
    {
        20f, 24f, 28f, 32f, 36f, 42f,
        48f, 54f, 60f, 66f, 72f, 80f
    };

    private static readonly float[] SmallResistance =
    {
        0f, 0f, 0.01f, 0.01f, 0.02f, 0.02f,
        0.025f, 0.03f, 0.03f, 0.035f, 0.04f, 0.04f
    };

    private static readonly float[] MediumHealth =
    {
        120f, 145f, 170f, 200f, 235f, 275f,
        320f, 370f, 425f, 485f, 550f, 620f
    };

    private static readonly float[] MediumResistance =
    {
        0.05f, 0.05f, 0.055f, 0.06f, 0.06f, 0.065f,
        0.07f, 0.075f, 0.08f, 0.085f, 0.09f, 0.095f
    };

    private static readonly float[] LargeHealth =
    {
        700f, 850f, 1020f, 1220f, 1450f, 1700f, 2000f
    };

    private static readonly float[] LargeResistance =
    {
        0.10f, 0.11f, 0.12f, 0.13f, 0.14f, 0.15f, 0.16f
    };

    private static readonly float[] SpecialHealth =
    {
        2600f, 3400f, 4400f, 5700f, 7400f
    };

    private static readonly float[] SpecialResistance =
    {
        0.16f, 0.175f, 0.19f, 0.205f, 0.22f
    };

    private static readonly float[] MiniBossHealth =
    {
        12000f, 18000f, 28000f
    };

    private static readonly float[] MiniBossResistance =
    {
        0.20f, 0.24f, 0.28f
    };

    private static readonly float[] MainBossHealth =
    {
        40000f, 52000f, 68000f, 90000f
    };

    private static readonly float[] MainBossResistance =
    {
        0.28f, 0.30f, 0.32f, 0.34f
    };

    private static readonly float[] EpicBossHealth =
    {
        115000f, 150000f, 200000f
    };

    private static readonly float[] EpicBossResistance =
    {
        0.35f, 0.38f, 0.40f
    };

    public static float GetGunDamage(int gunLevel)
    {
        return GunDamage[Mathf.Clamp(gunLevel, 1, GunLevelCount) - 1];
    }

    public static float GetGunCost(int gunLevel)
    {
        return GunCost[Mathf.Clamp(gunLevel, 1, GunLevelCount) - 1];
    }

    public static float[] CopyGunDamageValues()
    {
        return (float[])GunDamage.Clone();
    }

    public static float[] CopyGunCostValues()
    {
        return (float[])GunCost.Clone();
    }

    public static float GetDamageMultiplier(int gunLevel)
    {
        return GetGunDamage(gunLevel) / GunDamage[0];
    }

    public static int GetExpectedShots(
        float health,
        float resistance,
        int gunLevel
    )
    {
        float effectiveDamage = GetGunDamage(gunLevel) *
            (1f - Mathf.Clamp(resistance, 0f, 0.95f));
        return Mathf.Max(1, Mathf.CeilToInt(
            Mathf.Max(1f, health) / Mathf.Max(0.01f, effectiveDamage)
        ));
    }

    public static float GetEffectiveHealth(float health, float resistance)
    {
        return Mathf.Max(1f, health) /
            Mathf.Max(0.05f, 1f - Mathf.Clamp(resistance, 0f, 0.95f));
    }

    public static void GetFishCombatPreset(
        int fishId,
        out float health,
        out float resistance,
        out float reward,
        out FishTier tier,
        out FishRarity rarity,
        out FishSizeClass sizeClass
    )
    {
        int id = Mathf.Max(0, fishId);
        float payoutMultiplier;

        if (id <= 11)
        {
            int local = Mathf.Clamp(id, 0, SmallHealth.Length - 1);
            health = SmallHealth[local];
            resistance = SmallResistance[local];
            tier = FishTier.Small;
            rarity = id >= 8 ? FishRarity.Uncommon : FishRarity.Common;
            sizeClass = id <= 5 ? FishSizeClass.Tiny : FishSizeClass.Small;
            payoutMultiplier = id >= 8 ? 1.10f : 1.05f;
        }
        else if (id <= 23)
        {
            int local = Mathf.Clamp(id - 12, 0, MediumHealth.Length - 1);
            health = MediumHealth[local];
            resistance = MediumResistance[local];
            tier = FishTier.Medium;
            rarity = local >= 7 ? FishRarity.Uncommon : FishRarity.Common;
            sizeClass = FishSizeClass.Medium;
            payoutMultiplier = local >= 7 ? 1.15f : 1.10f;
        }
        else if (id <= 30)
        {
            int local = Mathf.Clamp(id - 24, 0, LargeHealth.Length - 1);
            health = LargeHealth[local];
            resistance = LargeResistance[local];
            tier = FishTier.Medium;
            rarity = local >= 3 ? FishRarity.Rare : FishRarity.Uncommon;
            sizeClass = FishSizeClass.Large;
            payoutMultiplier = local >= 3 ? 1.25f : 1.18f;
        }
        else if (id <= 35)
        {
            int local = Mathf.Clamp(id - 31, 0, SpecialHealth.Length - 1);
            health = SpecialHealth[local];
            resistance = SpecialResistance[local];
            tier = FishTier.Special;
            rarity = local >= 3 ? FishRarity.Epic : FishRarity.Rare;
            sizeClass = local == 0 ? FishSizeClass.Medium : FishSizeClass.Large;
            payoutMultiplier = 1.30f;
        }
        else if (id <= 38)
        {
            int local = Mathf.Clamp(id - 36, 0, MiniBossHealth.Length - 1);
            health = MiniBossHealth[local];
            resistance = MiniBossResistance[local];
            tier = FishTier.MiniBoss;
            rarity = FishRarity.Epic;
            sizeClass = FishSizeClass.Huge;
            payoutMultiplier = 1.35f;
        }
        else if (id <= 42)
        {
            int local = Mathf.Clamp(id - 39, 0, MainBossHealth.Length - 1);
            health = MainBossHealth[local];
            resistance = MainBossResistance[local];
            tier = FishTier.MainBoss;
            rarity = FishRarity.Boss;
            sizeClass = FishSizeClass.Boss;
            payoutMultiplier = 1.40f;
        }
        else
        {
            int local = Mathf.Clamp(id - 43, 0, EpicBossHealth.Length - 1);
            health = EpicBossHealth[local];
            resistance = EpicBossResistance[local];
            tier = FishTier.MainBoss;
            rarity = FishRarity.Boss;
            sizeClass = FishSizeClass.Boss;
            payoutMultiplier = 1.45f;
        }

        float expectedSpend = GetEffectiveHealth(health, resistance) *
            CostPerDamage;
        reward = RoundReward(expectedSpend * payoutMultiplier);
    }

    private static float RoundReward(float value)
    {
        value = Mathf.Max(0f, value);

        if (value < 100f)
        {
            return Mathf.Round(value);
        }

        if (value < 1000f)
        {
            return Mathf.Round(value / 5f) * 5f;
        }

        if (value < 10000f)
        {
            return Mathf.Round(value / 10f) * 10f;
        }

        return Mathf.Round(value / 100f) * 100f;
    }
}
