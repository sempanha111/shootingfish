using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public WeaponsScripts weaponsScripts { get; private set; }
    public UIManager UIManager { get; private set; }
    public SoundManager SoundManager { get; private set; }
    public DisplayTextManagerScript DisplayTextManagerScript { get; private set; }
    public CoinManager coinManager { get; private set; }
    public AnimatiorManager animatiorManager { get; private set; }
    public Shoot shoot { get; private set;}
    public List<FishScript> fishInScreenList = new List<FishScript>();
    public Gun1 gun1;
    public Gun2 gun2;
    public Gun3 gun3;
    public Color shadowColor;

    public GameObject[] prefab_Bullet;
    public Animator[] Net; 
    public float Amount = 2000;
    public GameObject Shadow;

    // NEW: A pool to recycle Nets instead of Destroying them
    private List<Animator> netPool = new List<Animator>();

    void Awake()
    {
        Instance = this;
        weaponsScripts = GetComponent<WeaponsScripts>();
        UIManager = GetComponent<UIManager>();
        DisplayTextManagerScript = GetComponent<DisplayTextManagerScript>();
        coinManager = GetComponent<CoinManager>();
        shoot = GetComponent<Shoot>();
        animatiorManager = GetComponent<AnimatiorManager>();
        gun1 = GetComponent<Gun1>();
        gun2 = GetComponent<Gun2>();
        gun3 = GetComponent<Gun3>();      
        SoundManager = GetComponent<SoundManager>();      
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
    }

    // NEW: Function to handle Net Pooling
    public void SpawnNet(int activeGun, Vector3 position, float netTime)
    {
        StartCoroutine(NetRoutine(activeGun, position, netTime));
    }

    private IEnumerator NetRoutine(int activeGun, Vector3 position, float netTime)
    {
        // Find an inactive net in the pool, or create a new one
        Animator netInstance = netPool.FirstOrDefault(n => !n.gameObject.activeSelf && n.name == Net[activeGun - 1].name + "(Clone)");
        
        if (netInstance == null)
        {
            netInstance = Instantiate(Net[activeGun - 1], position, Quaternion.identity);
            netPool.Add(netInstance);
        }

        netInstance.transform.position = position;
        netInstance.gameObject.SetActive(true);

        yield return new WaitForSeconds(netTime);

        // Put the net to sleep instead of Destroying it!
        if (netInstance != null)
        {
            netInstance.gameObject.SetActive(false);
        }
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
        while (amount > 0.01f) // Changed from >= 0 to prevent infinite float precision loops
        {
            Amount += perIncrease;
            amount -= perIncrease;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public IEnumerator IEnumCalulateNPCCoinFish(float Coinfish, int BulletId)
    {
        float perIncrease = Coinfish / 5;
        yield return new WaitForSeconds(1.5f);

        while (Coinfish > 0.01f)
        {
            if (BulletId == 1) gun1.AmountCoin += perIncrease;
            else if (BulletId == 2) gun2.AmountCoin += perIncrease;
            else gun3.AmountCoin += perIncrease;
            
            Coinfish -= perIncrease;
            yield return new WaitForSeconds(0.2f);
        }
    }

    void Update()
    {
        UIManager.SetTextTotal(Amount.ToString("F0"));
        UIManager.SetTextBet(weaponsScripts.Totalbet.ToString());
    }
}