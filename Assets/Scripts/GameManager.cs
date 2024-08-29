using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }

    public WeaponsScripts weaponsScripts { get; private set; }
    public UIManager UIManager { get; private set; }
    public DisplayTextManagerScript DisplayTextManagerScript { get; private set; }
    public CoinManager coinManager { get; private set; }
    public Shoot shoot { get; private set;}
    public List<FishScript> fishInScreenList = new List<FishScript>();
    public Gun1 gun1;
    public Gun2 gun2;
    public Gun3 gun3;
    public Color shadowColor;


    public GameObject[] prefab_Bullet;
    public Animator[] Net;
    public float Amount = 2000;

    void Awake()
    {
        Instance = this;
        weaponsScripts = GetComponent<WeaponsScripts>();
        UIManager = GetComponent<UIManager>();
        DisplayTextManagerScript = GetComponent<DisplayTextManagerScript>();
        coinManager = GetComponent<CoinManager>();
        shoot = GetComponent<Shoot>();
        gun1 = GetComponent<Gun1>();
        gun2 = GetComponent<Gun2>();
        gun3 = GetComponent<Gun3>();      
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        Application.lowMemory += OnLowMemory;
    }

    private void OnLowMemory()
    {
        Resources.UnloadUnusedAssets();
        GC.Collect();

        //More features, Do Delete Pooling

    }

    public void CalculateTotalCoinWithBet()
    {
        Amount -= weaponsScripts.Totalbet;
    }

    public void CalulateTotalCoinWithCoinFish(float amount)
    {
        StartCoroutine(IEnumCalulateCoinFish(amount));
    }
    public void CalulateNPCCoinFish(float amount, int BulletId)
    {
        StartCoroutine(IEnumCalulateNPCCoinFish(amount, BulletId));
    }

    public IEnumerator IEnumCalulateCoinFish(float amount)
    {
        float perIncrease = amount / 5;

        yield return new WaitForSeconds(1.5f);
        while (amount >= 0)
        {
            Amount += perIncrease;
            amount -= perIncrease;
            yield return new WaitForSeconds(0.2f);
        }
    }




    public IEnumerator IEnumCalulateNPCCoinFish(float Coinfish, int BulletId)
    {

        float perIncrease = Coinfish / 5;
        yield return new WaitForSeconds(1.5f);

        switch (BulletId)
        {
            case 1:
                while (Coinfish >= 0)
                {
                    gun1.AmountCoin += perIncrease;
                    Coinfish -= perIncrease;

                    yield return new WaitForSeconds(0.2f);
                }
                break;
            case 2:
                while (Coinfish >= 0)
                {
                    gun2.AmountCoin += perIncrease;
                    Coinfish -= perIncrease;
                    yield return new WaitForSeconds(0.2f);
                }
                break;
            default :
                while (Coinfish >= 0)
                {
                    gun3.AmountCoin += perIncrease;
                    Coinfish -= perIncrease;
                    yield return new WaitForSeconds(0.2f);
                }
                break;
        }
    }




    void Update()
    {
        UIManager.SetTextTotal(Amount.ToString());
        UIManager.SetTextBet(weaponsScripts.Totalbet.ToString());

        //NPC 

    }
}
