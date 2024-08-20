using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public WeaponsScripts weaponsScripts {get; private set;}
    public UIManager UIManager {get;private set;}
    public DisplayTextManagerScript DisplayTextManagerScript {get;private set;}
    public CoinManager coinManager {get; private set;}
    

    public float Amount = 2000;

    void Awake() {
        Instance = this;
        weaponsScripts = GetComponent<WeaponsScripts>();
        UIManager = GetComponent<UIManager>();
        DisplayTextManagerScript = GetComponent<DisplayTextManagerScript>();
        coinManager = GetComponent<CoinManager>();
    }

    public void CalculateTotalCoinWithBet(){
        Amount -= weaponsScripts.Totalbet;
    }
    

    void Update()
    {
        UIManager.SetTextTotal(Amount.ToString());
        UIManager.SetTextBet(weaponsScripts.Totalbet.ToString());   
    }
}
