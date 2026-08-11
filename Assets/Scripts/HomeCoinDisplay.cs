using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the saved human-player balance in the Home scene and exposes the
/// Reset_btn action. Attach this component to HomeScreenController.
/// </summary>
public class HomeCoinDisplay : MonoBehaviour
{
    [Tooltip("Assign Canvas/Coin/TotalCoin. If empty, this finds a child Text named TotalCoin.")]
    [SerializeField] private Text totalCoinText;

    private void OnEnable()
    {
        ResolveTotalCoinText();
        RefreshTotalCoin();
    }

    public void RefreshTotalCoin()
    {
        if (totalCoinText != null)
        {
            totalCoinText.text = PlayerCoinWallet.Load().ToString("F0");
        }
    }

    public void ResetPlayerCoin()
    {
        float resetBalance = PlayerCoinWallet.ResetToDefault();

        if (totalCoinText != null)
        {
            totalCoinText.text = resetBalance.ToString("F0");
        }
    }

    public void ResetCoin()
    {
        ResetPlayerCoin();
    }

    private void ResolveTotalCoinText()
    {
        if (totalCoinText != null)
        {
            return;
        }

        Text[] childTexts = GetComponentsInChildren<Text>(true);

        for (int i = 0; i < childTexts.Length; i++)
        {
            if (childTexts[i] != null &&
                childTexts[i].gameObject.name == "TotalCoin")
            {
                totalCoinText = childTexts[i];
                return;
            }
        }
    }
}
