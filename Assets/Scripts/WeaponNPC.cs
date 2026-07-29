using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponNPC : MonoBehaviour
{
    [SerializeField] private int id;
    private GameManager GM;
    private Gun1 gun1;
    private Gun2 gun2;
    private Gun3 gun3;

    private GameObject[] Gunlevel;
    private GameObject[] Gun;
    private Animator[] Anima_Gun;
    private float[] Bet;
    public int activeGunLevel = 1;
    public float Totalbet;
    [SerializeField] private float AnimaShootWait = 0.15f;
    private GameObject activeGun;

    // --- SMART & CHALLENGING AI SETTINGS ---
    [Header("Smart AI Settings")]
    [Range(0.05f, 0.3f)] 
    [SerializeField] private float screenPadding = 0.15f; // Fish must be 15% inside screen before targeting
    [SerializeField] private float reactionDelay = 0.4f;   // Delay before shooting new target
    private float targetAcquiredTimer = 0f;
    private Camera mainCam;

    [Header("Random Weapon Settings")]
    [Tooltip("How many gun levels higher or lower the NPC can randomly choose.")]
    [SerializeField] private int randomVariance = 2; 

    private float timeToShoot = 0;
    private float fireRate = 3.5f; 
    private FishScript targetFish;

    void Start()
    {
        GM = GameManager.Instance;
        mainCam = Camera.main;

        gun1 = GM.gun1;
        gun2 = GM.gun2;
        gun3 = GM.gun3;

        ChooseWeapon(id);
        ActivateGun(activeGunLevel);
    }

    private void ChooseWeapon(int id)
    {
        switch (id)
        {
            case 1:
                Gunlevel = gun1.Gunlevel;
                Gun = gun1.Gun;
                Anima_Gun = gun1.Anima_Gun;
                Bet = gun1.Bet;
                break;
            case 2:
                Gunlevel = gun2.Gunlevel;
                Gun = gun2.Gun;
                Anima_Gun = gun2.Anima_Gun;
                Bet = gun2.Bet;
                break;
            default:
                Gunlevel = gun3.Gunlevel;
                Gun = gun3.Gun;
                Anima_Gun = gun3.Anima_Gun;
                Bet = gun3.Bet;
                break;
        }
    }

    public void ActivateGun(int gunLevelSet)
    {
        // Clamp to valid gun level array bounds
        gunLevelSet = Mathf.Clamp(gunLevelSet, 1, Gunlevel.Length);

        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == gunLevelSet - 1);
        }

        GetEachBetGun(gunLevelSet);
        activeGun = Gun[gunLevelSet - 1];
        activeGunLevel = gunLevelSet;
    }

    void GetEachBetGun(int activeGunLevel)
    {
        Totalbet = Bet[activeGunLevel - 1];
    }

    void Update()
    {
        // Update UI Text
        GM.UIManager.SetTextBetNPC(id, Totalbet.ToString());

        if (id == 1) GM.UIManager.SetTextTotalNPC(id, gun1.AmountCoin.ToString());
        else if (id == 2) GM.UIManager.SetTextTotalNPC(id, gun2.AmountCoin.ToString());
        else GM.UIManager.SetTextTotalNPC(id, gun3.AmountCoin.ToString());

        // Target Validation: Check if target lost, dead, or walked off-screen
        if (targetFish == null || !targetFish.gameObject.activeSelf || !IsFishDeepInScreen(targetFish.transform.position))
        {
            targetFish = FindSmartTarget();
            targetAcquiredTimer = Time.time + reactionDelay; // Reset human reaction delay timer

            if (targetFish == null) return;
        }
        else
        {
            // Reaction delay before firing at new targets
            if (Time.time < targetAcquiredTimer) return;

            if (timeToShoot <= Time.time)
            {
                RotateActiveGun();
                ShootingAIClick();
                timeToShoot = Time.time + 1f / fireRate;
            }
        }
    }

    // Checks if the fish is safely inside the playable viewport (not near edges)
    private bool IsFishDeepInScreen(Vector3 fishPosition)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return true;

        Vector3 vp = mainCam.WorldToViewportPoint(fishPosition);
        
        // Ensure fish is within inner padded area of screen
        return vp.x >= screenPadding && vp.x <= (1f - screenPadding) &&
               vp.y >= screenPadding && vp.y <= (1f - screenPadding);
    }

    private FishScript FindSmartTarget()
    {
        FishScript[] fishes = GM.fishInScreenList.ToArray();
        FishScript bestTarget = null;
        float minDistance = Mathf.Infinity;

        foreach (FishScript fish in fishes)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;

            // 1. IGNORE fish that just entered edge of screen
            if (!IsFishDeepInScreen(fish.transform.position)) continue;

            // Give preference to higher HP fish (Bosses)
            if (fish.IsBoss())
            {
                ChangeGun(fish);
                return fish;
            }

            float distance = Vector3.Distance(transform.position, fish.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                bestTarget = fish;
            }
        }

        if (bestTarget != null)
        {
            ChangeGun(bestTarget);
        }

        return bestTarget;
    }

    // RANDOMIZED GUN SELECTION FOR CHALLENGING / UNPREDICTABLE GAMEPLAY
    void ChangeGun(FishScript fishscript)
    {
        if (fishscript == null || Gunlevel == null || Gunlevel.Length == 0) return;

        int totalGuns = Gunlevel.Length;
        int selectedGunLevel = 1;

        if (fishscript.IsBoss())
        {
            // For Bosses, randomly select among the top 4 highest gun levels
            int minBossGun = Mathf.Max(1, totalGuns - 3);
            selectedGunLevel = Random.Range(minBossGun, totalGuns + 1);
        }
        else
        {
            // Calculate base ideal gun level according to fish HP
            int baseLevel = Mathf.CeilToInt(fishscript.Hp / 100f);

            // Add random variance (+/- randomVariance) to make choices unpredictable
            int offset = Random.Range(-randomVariance, randomVariance + 1);
            selectedGunLevel = Mathf.Clamp(baseLevel + offset, 1, totalGuns);
        }

        ActivateGun(selectedGunLevel);
    }

    public void ShootingAIClick()
    {
        if (activeGun == null) return;

        StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + activeGunLevel, "Idle" + activeGunLevel));
        GM.shoot.ShootOnce(activeGun.transform, activeGunLevel, id);

        switch (id)
        {
            case 1:
                gun1.AmountCoin -= Totalbet;
                break;
            case 2:
                gun2.AmountCoin -= Totalbet;
                break;
            default:
                gun3.AmountCoin -= Totalbet;
                break;
        }
    }

    private void RotateActiveGun()
    {
        if (targetFish == null || activeGun == null) return;
        float angle = GetAngle(activeGun.transform, targetFish.transform.position);
        activeGun.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private float GetAngle(Transform gunTransform, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - gunTransform.position;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }

    private IEnumerator SwitchAndAnimateGun(Animator animator, string animationName, string idleAnimation)
    {
        if (animator == null || !animator.gameObject.activeInHierarchy) yield break;

        animator.Play(animationName);
        yield return new WaitForSeconds(AnimaShootWait);

        if (animator == null || !animator.gameObject.activeInHierarchy) yield break;

        animator.Play(idleAnimation);
    }
}