using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponNPC : MonoBehaviour
{
    private const int CurrentNpcRhythmVersion = 13;
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

    public int ShooterId
    {
        get { return id; }
    }

    public Transform ActiveGunTransform
    {
        get { return activeGun != null ? activeGun.transform : transform; }
    }

    // --- SMART & CHALLENGING AI SETTINGS ---
    [Header("Smart AI Settings")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float screenPadding = 0.15f; // Fish must be 15% inside screen before targeting

    [Tooltip("Legacy minimum reaction time retained for existing scenes.")]
    [SerializeField] private float reactionDelay = 0.85f;

    [Header("Human-Like Targeting Rhythm")]
    [Tooltip("Random delay after acquiring a new visible fish.")]
    [SerializeField] private Vector2 newTargetReactionDelayRange =
        new Vector2(1.15f, 2.15f);

    [Tooltip("Pause before searching again after a target dies or leaves.")]
    [SerializeField] private Vector2 targetLostRestRange =
        new Vector2(0.90f, 1.80f);

    [Tooltip("NPCs periodically pause after a short firing burst.")]
    [SerializeField] private Vector2Int shotsBeforeBreathingRange =
        new Vector2Int(3, 6);

    [SerializeField, Range(0f, 1f)]
    private float breathingPauseChance = 0.82f;

    [SerializeField] private Vector2 breathingPauseRange =
        new Vector2(0.80f, 1.60f);

    [SerializeField, Min(0.05f)]
    private float targetSearchInterval = 0.35f;

    [SerializeField] private Vector2 initialWakeDelayRange =
        new Vector2(0.75f, 1.65f);

    [Tooltip("Occasional longer rest that makes each NPC cannon feel less robotic.")]
    [SerializeField, Range(0f, 1f)]
    private float longSleepChance = 0.18f;

    [SerializeField] private Vector2 longSleepRange =
        new Vector2(1.80f, 3.20f);

    [SerializeField, HideInInspector]
    private int npcRhythmVersion;

    private float targetAcquiredTimer = 0f;
    private float nextTargetSearchTime;
    private float restUntil;
    private int shotsUntilBreath;
    private Camera mainCam;

    [Header("NPC Gun Level Limit")]
    [Tooltip("Hard maximum gun level NPC cannons may use. The player can still use all 17 gun levels.")]
    [SerializeField, Range(1, 17)] private int maximumNpcGunLevel = 8;

    [Tooltip("Lowest gun level NPCs normally use against bosses. This is clamped by Maximum NPC Gun Level.")]
    [SerializeField, Range(1, 17)] private int minimumBossNpcGunLevel = 7;

    [Header("Random Weapon Settings")]
    [Tooltip("Small +/- variation around the recommended NPC gun level. Keep this low so NPCs do not jump to oversized guns.")]
    [SerializeField, Range(0, 3)] private int randomVariance = 1;

    private float timeToShoot = 0;
    private float fireRate = 2.8f; 
    private FishScript targetFish;

    void Start()
    {
        ApplyV13NpcRhythmIfNeeded();
        GM = GameManager.Instance;
        mainCam = Camera.main;

        gun1 = GM.gun1;
        gun2 = GM.gun2;
        gun3 = GM.gun3;

        ChooseWeapon(id);
        ActivateGun(activeGunLevel);

        ResetBreathingBurst();
        restUntil = Time.time + RandomRangeSafe(
            initialWakeDelayRange,
            0.65f
        );
        nextTargetSearchTime = restUntil;
    }

    private void OnValidate()
    {
        ApplyV13NpcRhythmIfNeeded();
        maximumNpcGunLevel = Mathf.Clamp(maximumNpcGunLevel, 1, 17);
        minimumBossNpcGunLevel = Mathf.Clamp(
            minimumBossNpcGunLevel,
            1,
            maximumNpcGunLevel
        );
        randomVariance = Mathf.Clamp(randomVariance, 0, 3);
    }

    [ContextMenu("Apply Recommended NPC Gun Preset - Max Level 8")]
    public void ApplyRecommendedNpcGunPreset()
    {
        maximumNpcGunLevel = 8;
        minimumBossNpcGunLevel = 7;
        randomVariance = 1;
        activeGunLevel = Mathf.Clamp(activeGunLevel, 1, maximumNpcGunLevel);

        if (Gunlevel != null && Gunlevel.Length > 0)
        {
            ActivateGun(activeGunLevel);
        }
    }

    private void ApplyV13NpcRhythmIfNeeded()
    {
        if (npcRhythmVersion >= CurrentNpcRhythmVersion)
        {
            return;
        }

        reactionDelay = 0.85f;
        newTargetReactionDelayRange = new Vector2(1.15f, 2.15f);
        targetLostRestRange = new Vector2(0.90f, 1.80f);
        shotsBeforeBreathingRange = new Vector2Int(3, 6);
        breathingPauseChance = 0.82f;
        breathingPauseRange = new Vector2(0.80f, 1.60f);
        targetSearchInterval = 0.35f;
        initialWakeDelayRange = new Vector2(0.75f, 1.65f);
        longSleepChance = 0.18f;
        longSleepRange = new Vector2(1.80f, 3.20f);
        npcRhythmVersion = CurrentNpcRhythmVersion;
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
        if (Gunlevel == null || Gunlevel.Length == 0 ||
            Gun == null || Gun.Length == 0)
        {
            return;
        }

        // NPCs intentionally use only the lower eight gun levels by default.
        // Player gun progression remains independent and can still reach 17.
        int availableGunCount = Mathf.Min(Gunlevel.Length, Gun.Length);
        int npcMaximum = Mathf.Clamp(
            maximumNpcGunLevel,
            1,
            availableGunCount
        );
        gunLevelSet = Mathf.Clamp(gunLevelSet, 1, npcMaximum);

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
        if (Bet == null || Bet.Length == 0)
        {
            Totalbet = 0f;
            return;
        }

        int index = Mathf.Clamp(activeGunLevel - 1, 0, Bet.Length - 1);
        Totalbet = Bet[index];
    }

    void Update()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        // Update UI Text
        GM.UIManager.SetTextBetNPC(id, Totalbet.ToString());

        if (id == 1) GM.UIManager.SetTextTotalNPC(id, gun1.AmountCoin.ToString());
        else if (id == 2) GM.UIManager.SetTextTotalNPC(id, gun2.AmountCoin.ToString());
        else GM.UIManager.SetTextTotalNPC(id, gun3.AmountCoin.ToString());

        if (Time.time < restUntil)
        {
            return;
        }

        if (!IsCurrentTargetValid())
        {
            if (targetFish != null)
            {
                targetFish = null;
                ScheduleTargetLostRest();
                return;
            }

            if (Time.time < nextTargetSearchTime)
            {
                return;
            }

            nextTargetSearchTime =
                Time.time + Mathf.Max(0.05f, targetSearchInterval);
            targetFish = FindSmartTarget();

            if (targetFish == null)
            {
                return;
            }

            targetAcquiredTimer = Time.time + GetNewTargetReactionDelay();
            ResetBreathingBurst();
            return;
        }

        if (Time.time < targetAcquiredTimer)
        {
            RotateActiveGun();
            return;
        }

        RotateActiveGun();

        if (timeToShoot > Time.time)
        {
            return;
        }

        bool fired = TryShootingAIClick();
        timeToShoot = Time.time + 1f / Mathf.Max(0.1f, fireRate);

        if (!fired)
        {
            return;
        }

        shotsUntilBreath--;
        if (shotsUntilBreath <= 0)
        {
            ResetBreathingBurst();

            if (Random.value <= breathingPauseChance)
            {
                Vector2 selectedRest = Random.value <= longSleepChance
                    ? longSleepRange
                    : breathingPauseRange;
                restUntil = Time.time + RandomRangeSafe(
                    selectedRest,
                    1.05f
                );
                timeToShoot = restUntil;
            }
        }
    }

    private bool IsCurrentTargetValid()
    {
        return targetFish != null &&
               targetFish.IsAliveTarget &&
               targetFish.IsTargetVisibleTo(mainCam) &&
               IsFishDeepInScreen(targetFish.GetTargetCenterWorld());
    }

    private void ScheduleTargetLostRest()
    {
        float pause = RandomRangeSafe(targetLostRestRange, 0.75f);

        if (Random.value <= breathingPauseChance)
        {
            pause += RandomRangeSafe(breathingPauseRange, 1.0f) * 0.45f;
        }

        if (Random.value <= longSleepChance)
        {
            pause += RandomRangeSafe(longSleepRange, 2.2f);
        }

        restUntil = Time.time + pause;
        nextTargetSearchTime = restUntil;
        timeToShoot = restUntil;
    }

    private float GetNewTargetReactionDelay()
    {
        float randomDelay = RandomRangeSafe(
            newTargetReactionDelayRange,
            0.95f
        );
        return Mathf.Max(Mathf.Max(0f, reactionDelay), randomDelay);
    }

    private void ResetBreathingBurst()
    {
        int minimum = Mathf.Max(1, shotsBeforeBreathingRange.x);
        int maximum = Mathf.Max(minimum, shotsBeforeBreathingRange.y);
        shotsUntilBreath = Random.Range(minimum, maximum + 1);
    }

    private static float RandomRangeSafe(Vector2 range, float fallback)
    {
        float minimum = Mathf.Max(0f, Mathf.Min(range.x, range.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));

        if (maximum <= 0f)
        {
            return Mathf.Max(0f, fallback);
        }

        return Random.Range(minimum, maximum);
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
        List<FishScript> fishes = GM.fishInScreenList;
        FishScript bestTarget = null;
        float minDistance = Mathf.Infinity;

        foreach (FishScript fish in fishes)
        {
            if (fish == null ||
                !fish.IsAliveTarget ||
                !fish.IsTargetVisibleTo(mainCam))
            {
                continue;
            }

            // 1. IGNORE fish that just entered edge of screen
            if (!IsFishDeepInScreen(fish.GetTargetCenterWorld()))
            {
                continue;
            }

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

    // NPC gun choice follows the clean balance curve but never exceeds the
    // configured NPC cap. This keeps NPC cannons useful without letting them
    // constantly jump to player end-game guns 9-17.
    void ChangeGun(FishScript fishscript)
    {
        if (fishscript == null || Gunlevel == null || Gunlevel.Length == 0)
        {
            return;
        }

        int totalGuns = Mathf.Clamp(
            maximumNpcGunLevel,
            1,
            Gunlevel.Length
        );
        int selectedGunLevel;

        if (fishscript.IsBoss())
        {
            int minimumBossLevel = Mathf.Clamp(
                minimumBossNpcGunLevel,
                1,
                totalGuns
            );
            selectedGunLevel = Random.Range(
                minimumBossLevel,
                totalGuns + 1
            );
        }
        else
        {
            int baseLevel = GetRecommendedNpcGunLevel(fishscript);
            int offset = randomVariance > 0
                ? Random.Range(-randomVariance, randomVariance + 1)
                : 0;
            selectedGunLevel = Mathf.Clamp(
                baseLevel + offset,
                1,
                totalGuns
            );
        }

        ActivateGun(selectedGunLevel);
    }

    private int GetRecommendedNpcGunLevel(FishScript fishscript)
    {
        if (fishscript == null)
        {
            return 1;
        }

        // HP breakpoints are intentionally broad. They align the clean fish
        // balance with NPC guns 1-8 without making every target use the
        // strongest allowed cannon.
        float hp = Mathf.Max(1f, fishscript.Hp);

        if (hp <= 80f) return 1;
        if (hp <= 220f) return 2;
        if (hp <= 550f) return 3;
        if (hp <= 1000f) return 4;
        if (hp <= 1800f) return 5;
        if (hp <= 3500f) return 6;
        if (hp <= 7500f) return 7;
        return 8;
    }

    public void ShootingAIClick()
    {
        TryShootingAIClick();
    }

    private bool TryShootingAIClick()
    {
        if (activeGun == null)
        {
            return false;
        }

        int affordableLevel = GM.GetHighestAffordableGunLevel(
            id,
            activeGunLevel
        );
        if (affordableLevel <= 0)
        {
            timeToShoot = Time.time + 1.25f;
            return false;
        }

        if (affordableLevel != activeGunLevel)
        {
            ActivateGun(affordableLevel);
        }

        if (targetFish == null || !targetFish.IsAliveTarget)
        {
            return false;
        }

        // NPC cannons use ordinary first-hit collision. They aim toward one
        // fish, but any fish crossing the projectile path can be hit.
        if (!GM.shoot.TryShootOnce(
                activeGun.transform,
                activeGunLevel,
                id,
                out float shotCost
            ))
        {
            timeToShoot = Time.time + 1.25f;
            return false;
        }

        StartCoroutine(
            SwitchAndAnimateGun(
                Anima_Gun[activeGunLevel - 1],
                "Shoot" + activeGunLevel,
                "Idle" + activeGunLevel
            )
        );
        return true;
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