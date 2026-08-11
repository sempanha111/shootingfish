using UnityEngine;

/// <summary>
/// Persistent wallet for the human player only. NPC balances deliberately do
/// not use this class and continue to reset with the gameplay scene.
/// </summary>
public static class PlayerCoinWallet
{
    private const string BalanceKey = "ShootingFish.PlayerCoin.v1";
    private const string DefaultBalanceKey =
        "ShootingFish.DefaultPlayerCoin.v1";
    private const float FallbackDefaultBalance = 100000f;

    public static void ConfigureDefault(float defaultBalance)
    {
        float safeDefault = Mathf.Max(0f, defaultBalance);
        PlayerPrefs.SetFloat(DefaultBalanceKey, safeDefault);

        if (!PlayerPrefs.HasKey(BalanceKey))
        {
            PlayerPrefs.SetFloat(BalanceKey, safeDefault);
            PlayerPrefs.Save();
        }
    }

    public static float Load(float fallback = FallbackDefaultBalance)
    {
        float defaultBalance = PlayerPrefs.GetFloat(
            DefaultBalanceKey,
            Mathf.Max(0f, fallback)
        );

        return Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(BalanceKey, defaultBalance)
        );
    }

    public static void Save(float balance, bool flushToDevice)
    {
        PlayerPrefs.SetFloat(BalanceKey, Mathf.Max(0f, balance));

        if (flushToDevice)
        {
            PlayerPrefs.Save();
        }
    }

    public static float ResetToDefault()
    {
        float defaultBalance = Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(
                DefaultBalanceKey,
                FallbackDefaultBalance
            )
        );

        Save(defaultBalance, true);
        return defaultBalance;
    }
}
