using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public WeaponsScripts weaponsScripts { get; private set; }
    public ArcadePowerSkillController arcadePowerSkills { get; private set; }
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
    [Tooltip("Fictional arcade points only; no real-money value or cash-out.")]
    public float Amount = 100000f;
    public GameObject Shadow;

    [Header("Shared Earthquake Effect")]
    [Tooltip("Assign Main Camera or a camera parent. If empty, Camera.main is used automatically.")]
    [SerializeField] private Transform earthquakeTarget;

    private Coroutine earthquakeRoutine;
    private Transform activeEarthquakeTarget;
    private Vector3 earthquakeBaseLocalPosition;

    // NEW: A pool to recycle Nets instead of Destroying them
    private List<Animator> netPool = new List<Animator>();

    void Awake()
    {
        Instance = this;
        PlayerCoinWallet.ConfigureDefault(Amount);
        Amount = PlayerCoinWallet.Load(Amount);
        weaponsScripts = GetComponent<WeaponsScripts>();
        arcadePowerSkills = GetComponent<ArcadePowerSkillController>();
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

    private void OnDestroy()
    {
        SavePlayerCoinNow();
        Application.lowMemory -= OnLowMemory;

        if (earthquakeRoutine != null)
        {
            StopCoroutine(earthquakeRoutine);
        }

        RestoreEarthquakeTarget();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnLowMemory()
    {
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            SavePlayerCoinNow();
        }
    }

    private void OnApplicationQuit()
    {
        SavePlayerCoinNow();
    }

    public void SavePlayerCoinNow()
    {
        PlayerCoinWallet.Save(Amount, true);
    }

    private void SetPlayerCoin(float amount)
    {
        Amount = Mathf.Max(0f, amount);
        PlayerCoinWallet.Save(Amount, false);
    }

    public void PlayEarthquake(float duration, float strength)
    {
        duration = Mathf.Max(0.05f, duration);
        strength = Mathf.Max(0f, strength);

        if (strength <= 0f)
        {
            return;
        }

        if (earthquakeRoutine != null)
        {
            StopCoroutine(earthquakeRoutine);
            RestoreEarthquakeTarget();
        }

        earthquakeRoutine = StartCoroutine(
            EarthquakeRoutine(duration, strength)
        );
    }

    public void StopEarthquake()
    {
        if (earthquakeRoutine != null)
        {
            StopCoroutine(earthquakeRoutine);
            earthquakeRoutine = null;
        }

        RestoreEarthquakeTarget();
    }

    private IEnumerator EarthquakeRoutine(float duration, float strength)
    {
        activeEarthquakeTarget = earthquakeTarget;

        if (activeEarthquakeTarget == null && Camera.main != null)
        {
            activeEarthquakeTarget = Camera.main.transform;
        }

        if (activeEarthquakeTarget == null)
        {
            earthquakeRoutine = null;
            yield break;
        }

        earthquakeBaseLocalPosition =
            activeEarthquakeTarget.localPosition;
        float elapsed = 0f;

        while (elapsed < duration && activeEarthquakeTarget != null)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = UnityEngine.Random.insideUnitCircle *
                             strength *
                             fade;

            activeEarthquakeTarget.localPosition =
                earthquakeBaseLocalPosition +
                new Vector3(offset.x, offset.y, 0f);

            yield return null;
        }

        RestoreEarthquakeTarget();
        earthquakeRoutine = null;
    }

    private void RestoreEarthquakeTarget()
    {
        if (activeEarthquakeTarget != null)
        {
            activeEarthquakeTarget.localPosition =
                earthquakeBaseLocalPosition;
        }

        activeEarthquakeTarget = null;
    }

    [ContextMenu("Apply Clean Balanced 17-Gun Economy")]
    public void ApplyCleanBalancedEconomy()
    {
        float[] shotCosts = FishArcadeBalanceModel.CopyGunCostValues();
        float[] damageValues = FishArcadeBalanceModel.CopyGunDamageValues();

        Amount = Mathf.Max(Amount, 100000f);
        PlayerCoinWallet.ConfigureDefault(100000f);

        if (weaponsScripts == null)
        {
            weaponsScripts = GetComponent<WeaponsScripts>();
        }

        if (gun1 == null) gun1 = GetComponent<Gun1>();
        if (gun2 == null) gun2 = GetComponent<Gun2>();
        if (gun3 == null) gun3 = GetComponent<Gun3>();

        if (weaponsScripts != null)
        {
            weaponsScripts.Bet = (float[])shotCosts.Clone();
            weaponsScripts.ApplyCleanBalancePreset(
                FishArcadeBalanceModel.PlayerFireRate
            );
        }

        ApplyNpcEconomy(gun1, shotCosts);
        ApplyNpcEconomy(gun2, shotCosts);
        ApplyNpcEconomy(gun3, shotCosts);

        if (prefab_Bullet != null)
        {
            int bulletCount = Mathf.Min(
                prefab_Bullet.Length,
                damageValues.Length
            );

            for (int i = 0; i < bulletCount; i++)
            {
                if (prefab_Bullet[i] == null ||
                    !prefab_Bullet[i].TryGetComponent<BulletScript>(
                        out BulletScript bullet
                    ))
                {
                    continue;
                }

                bullet.ApplyArcadePreset(damageValues[i]);

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(bullet);
#endif
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (weaponsScripts != null) UnityEditor.EditorUtility.SetDirty(weaponsScripts);
        if (gun1 != null) UnityEditor.EditorUtility.SetDirty(gun1);
        if (gun2 != null) UnityEditor.EditorUtility.SetDirty(gun2);
        if (gun3 != null) UnityEditor.EditorUtility.SetDirty(gun3);
#endif

        Debug.Log(
            "Applied clean v20 arcade economy: 17 gun levels, shared " +
            "damage/cost model and 3.5 shots/sec player fire rate.",
            this
        );
    }

    // Backward-compatible button. Old scenes or editor utilities that still
    // call the v14 method now receive the clean absolute economy instead of
    // reapplying a legacy curve.
    [ContextMenu("Apply v14 Durable Fish Economy (Compatibility)")]
    public void ApplyV14DurableFishEconomy()
    {
        ApplyCleanBalancedEconomy();
    }

    [ContextMenu("Apply 17-Level Arcade Point Economy (Clean v20)")]
    private void Apply17LevelArcadePointEconomy()
    {
        ApplyCleanBalancedEconomy();
    }

    private static void ApplyNpcEconomy(Gun1 gun, float[] values)
    {
        if (gun == null) return;
        gun.Bet = (float[])values.Clone();
        gun.AmountCoin = 100000f;
    }

    private static void ApplyNpcEconomy(Gun2 gun, float[] values)
    {
        if (gun == null) return;
        gun.Bet = (float[])values.Clone();
        gun.AmountCoin = 100000f;
    }

    private static void ApplyNpcEconomy(Gun3 gun, float[] values)
    {
        if (gun == null) return;
        gun.Bet = (float[])values.Clone();
        gun.AmountCoin = 100000f;
    }

    // Pooled bullet-net effect. A version token prevents an older
    // coroutine from disabling an object that has already been reused.
    public void SpawnNet(
        int activeGun,
        Vector3 position,
        float netTime
    )
    {
        if (Net == null ||
            activeGun <= 0 ||
            activeGun > Net.Length ||
            Net[activeGun - 1] == null)
        {
            return;
        }

        StartCoroutine(
            NetRoutine(activeGun, position, netTime)
        );
    }

    private IEnumerator NetRoutine(
        int activeGun,
        Vector3 position,
        float netTime
    )
    {
        Animator source = Net[activeGun - 1];
        string expectedName = source.name + "(Clone)";
        Animator netInstance = null;

        for (int i = 0; i < netPool.Count; i++)
        {
            Animator candidate = netPool[i];

            if (candidate != null &&
                !candidate.gameObject.activeSelf &&
                candidate.name == expectedName)
            {
                netInstance = candidate;
                break;
            }
        }

        if (netInstance == null)
        {
            netInstance = Instantiate(
                source,
                position,
                Quaternion.identity
            );

            netPool.Add(netInstance);
        }

        PooledEffectToken token =
            netInstance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = netInstance.gameObject
                .AddComponent<PooledEffectToken>();
        }

        int version = ++token.playVersion;

        netInstance.transform.position = position;
        netInstance.gameObject.SetActive(true);
        netInstance.enabled = true;
        netInstance.Rebind();
        netInstance.Update(0f);
        netInstance.Play(0, 0, 0f);

        yield return new WaitForSeconds(
            Mathf.Max(0.02f, netTime)
        );

        if (netInstance == null)
        {
            yield break;
        }

        PooledEffectToken currentToken =
            netInstance.GetComponent<PooledEffectToken>();

        if (currentToken != null &&
            currentToken.playVersion == version)
        {
            netInstance.gameObject.SetActive(false);
        }
    }


    public bool TryGetShotCost(int shooterId, int gunLevel, out float shotCost)
    {
        shotCost = 0f;
        float[] bets = null;
        if (shooterId == 0 && weaponsScripts != null) bets = weaponsScripts.Bet;
        else if (shooterId == 1 && gun1 != null) bets = gun1.Bet;
        else if (shooterId == 2 && gun2 != null) bets = gun2.Bet;
        else if (shooterId == 3 && gun3 != null) bets = gun3.Bet;
        if (bets == null || bets.Length == 0) return false;
        int index = Mathf.Clamp(gunLevel - 1, 0, bets.Length - 1);
        shotCost = Mathf.Max(0f, bets[index]);
        return true;
    }

    public float GetShooterBalance(int shooterId)
    {
        if (shooterId == 0) return Mathf.Max(0f, Amount);
        if (shooterId == 1 && gun1 != null) return Mathf.Max(0f, gun1.AmountCoin);
        if (shooterId == 2 && gun2 != null) return Mathf.Max(0f, gun2.AmountCoin);
        if (shooterId == 3 && gun3 != null) return Mathf.Max(0f, gun3.AmountCoin);
        return 0f;
    }

    public bool TryPayForShot(int shooterId, int gunLevel, out float shotCost)
    {
        if (!TryGetShotCost(shooterId, gunLevel, out shotCost)) return false;
        float balance = GetShooterBalance(shooterId);
        if (shotCost > balance + 0.0001f) return false;
        float remaining = Mathf.Max(0f, balance - shotCost);
        if (shooterId == 0) SetPlayerCoin(remaining);
        else if (shooterId == 1 && gun1 != null) gun1.AmountCoin = remaining;
        else if (shooterId == 2 && gun2 != null) gun2.AmountCoin = remaining;
        else if (shooterId == 3 && gun3 != null) gun3.AmountCoin = remaining;
        else return false;
        return true;
    }

    /// <summary>
    /// Pays an Inspector-configured fictional arcade-coin cost without reading
    /// any normal-gun Bet array. Big Rocket uses this so its cost can be tuned
    /// independently while the original normal-gun economy remains unchanged.
    /// </summary>
    public bool TryPayFixedShotCost(
        int shooterId,
        float configuredCost,
        out float shotCost
    )
    {
        shotCost = Mathf.Max(0f, configuredCost);
        float balance = GetShooterBalance(shooterId);

        if (shotCost > balance + 0.0001f)
        {
            return false;
        }

        float remaining = Mathf.Max(0f, balance - shotCost);

        if (shooterId == 0)
        {
            SetPlayerCoin(remaining);
        }
        else if (shooterId == 1 && gun1 != null)
        {
            gun1.AmountCoin = remaining;
        }
        else if (shooterId == 2 && gun2 != null)
        {
            gun2.AmountCoin = remaining;
        }
        else if (shooterId == 3 && gun3 != null)
        {
            gun3.AmountCoin = remaining;
        }
        else
        {
            return false;
        }

        return true;
    }

    public int GetHighestAffordableGunLevel(int shooterId, int preferredLevel)
    {
        int level = Mathf.Max(1, preferredLevel);
        for (; level >= 1; level--)
        {
            float cost;
            if (TryGetShotCost(shooterId, level, out cost) && GetShooterBalance(shooterId) + 0.0001f >= cost) return level;
        }
        return 0;
    }
    public void CalculateTotalCoinWithBet()
    {
        SetPlayerCoin(Amount - weaponsScripts.Totalbet);
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
        float totalReward = Mathf.Max(0f, amount);

        if (totalReward <= 0f)
        {
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        const int steps = 5;
        float paidReward = 0f;

        for (int step = 0; step < steps; step++)
        {
            float portion = step == steps - 1
                ? totalReward - paidReward
                : totalReward / steps;

            SetPlayerCoin(Amount + portion);
            paidReward += portion;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public IEnumerator IEnumCalulateNPCCoinFish(
        float coinReward,
        int bulletId
    )
    {
        float totalReward = Mathf.Max(0f, coinReward);

        if (totalReward <= 0f ||
            bulletId < 1 ||
            bulletId > 3)
        {
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        const int steps = 5;
        float paidReward = 0f;

        for (int step = 0; step < steps; step++)
        {
            float portion = step == steps - 1
                ? totalReward - paidReward
                : totalReward / steps;

            if (bulletId == 1 && gun1 != null)
            {
                gun1.AmountCoin = Mathf.Max(
                    0f,
                    gun1.AmountCoin + portion
                );
            }
            else if (bulletId == 2 && gun2 != null)
            {
                gun2.AmountCoin = Mathf.Max(
                    0f,
                    gun2.AmountCoin + portion
                );
            }
            else if (bulletId == 3 && gun3 != null)
            {
                gun3.AmountCoin = Mathf.Max(
                    0f,
                    gun3.AmountCoin + portion
                );
            }

            paidReward += portion;
            yield return new WaitForSeconds(0.2f);
        }
    }

    void Update()
    {
        UIManager.SetTextTotal(Amount.ToString("F0"));
        UIManager.SetTextBet(weaponsScripts.Totalbet.ToString());
    }
}
