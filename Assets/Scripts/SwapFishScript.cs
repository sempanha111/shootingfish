using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    [Header("Setup References")]
    public GameObject[] Fish;
    public Transform FishSpawnPosition;
    public Transform LeftPos;
    public Transform RightPos;

    [Header("Dynamic Level Systems")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float regularSpawnIntervalMin = 2.8f;
    [SerializeField] private float regularSpawnIntervalMax = 5.0f;
    [SerializeField] private float bossSpawnInterval = 15f; 
    [SerializeField] private float delayBeforeNextLevel = 3.0f;

    [Header("Failsafe Settings")]
    [SerializeField] private float fieldClearTimeout = 10f; // Maximum seconds allowed to clear field naturally

    private int orderLayer;
    private bool bossSpawnFromLeft = true;
    private int bossSpawnCount = 0;
    private bool isTransitioningLevel = false;

    // Optimized Object Pool
    private Dictionary<int, List<GameObject>> fishPool = new Dictionary<int, List<GameObject>>();
    
    // Tracking active bosses currently on screen
    private List<GameObject> activeBosses = new List<GameObject>();

    void Start()
    {
        // Initialize the dictionary categories for each fish prefab type
        for (int i = 0; i < Fish.Length; i++)
        {
            fishPool[i] = new List<GameObject>();
        }

        // Setup baseline configuration for Level 1
        ConfigureLevelSettings(1);

        // Start core spawn loops
        StartCoroutine(IEnumFish_SpawnLeftToRight());
        StartCoroutine(IEnumFish_SpawnRightToLeft());
        StartCoroutine(IEnumBoss_Spawner());
    }

    // --- Dynamic Level Progression Scaling ---
    private void ConfigureLevelSettings(int level)
    {
        switch (level)
        {
            case 1:
                regularSpawnIntervalMin = 2.8f;
                regularSpawnIntervalMax = 5.0f;
                bossSpawnInterval = 15f; 
                break;
            case 2:
                regularSpawnIntervalMin = 2.2f;
                regularSpawnIntervalMax = 4.2f;
                bossSpawnInterval = 13f;
                break;
            case 3:
                regularSpawnIntervalMin = 1.8f;
                regularSpawnIntervalMax = 3.5f;
                bossSpawnInterval = 12f;
                break;
            case 4:
                regularSpawnIntervalMin = 1.4f;
                regularSpawnIntervalMax = 2.8f;
                bossSpawnInterval = 11f;
                break;
            case 5:
                regularSpawnIntervalMin = 1.0f;
                regularSpawnIntervalMax = 2.2f;
                bossSpawnInterval = 10f;
                break;
            case 6:
            default:
                regularSpawnIntervalMin = 0.6f;
                regularSpawnIntervalMax = 1.5f;
                bossSpawnInterval = 8f;
                break;
        }
        Debug.Log($"[SYSTEM] Level {level} Configured! Boss Goal: {GetBossLimitForLevel(level)} | Spawn Rate: {regularSpawnIntervalMin}s - {regularSpawnIntervalMax}s");
    }

    private int GetBossLimitForLevel(int level)
    {
        return 3 + level; 
    }

    // --- Boss Spawning Logic ---
    private IEnumerator IEnumBoss_Spawner()
    {
        yield return new WaitForSeconds(bossSpawnInterval);

        while (true)
        {
            int currentLevelBossLimit = GetBossLimitForLevel(currentLevel);

            if (!isTransitioningLevel && bossSpawnCount < currentLevelBossLimit)
            {
                if (Fish.Length > 0 && Fish[0] != null)
                {
                    Transform startPosition = bossSpawnFromLeft ? LeftPos : RightPos;
                    Transform endPosition = bossSpawnFromLeft ? RightPos : LeftPos;

                    SpawnBoss(startPosition, endPosition);

                    bossSpawnFromLeft = !bossSpawnFromLeft;
                    bossSpawnCount++;

                    if (bossSpawnCount >= currentLevelBossLimit)
                    {
                        StartCoroutine(IEnum_TransitionToNextLevel());
                    }
                }
            }

            yield return new WaitForSeconds(bossSpawnInterval);
        }
    }

    private void SpawnBoss(Transform startPos, Transform endPos)
    {
        Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
        GameObject spawnedBoss = Instantiate(Fish[0], startPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);

        RotateForFish(spawnedBoss.transform, endPos, Random.Range(-5, 5));
        spawnedBoss.SetActive(true);

        if (spawnedBoss.TryGetComponent<FishScript>(out FishScript fishScript))
        {
            fishScript.MoveHandle();
        }

        if (spawnedBoss.TryGetComponent<SpriteRenderer>(out SpriteRenderer fishSprite))
        {
            fishSprite.sortingOrder = orderLayer + 100;
        }

        activeBosses.Add(spawnedBoss);
    }

    // --- Field Clearing & Level Transitioning ---
    private IEnumerator IEnum_TransitionToNextLevel()
    {
        isTransitioningLevel = true;
        Debug.Log($"Level {currentLevel} complete! Waiting for screen to clear with a maximum safety limit of {fieldClearTimeout} seconds...");

        float timeSpentWaiting = 0f;

        // Loop checks every 0.5 seconds if fish or bosses are still active on screen
        while (AreBossesActive() || AreRegularFishActive())
        {
            yield return new WaitForSeconds(0.5f);
            timeSpentWaiting += 0.5f;

            // Failsafe Trigger: If it has taken too long, force set all remaining fish to false!
            if (timeSpentWaiting >= fieldClearTimeout)
            {
                Debug.LogWarning("[FAILSAFE] Field clearance timed out! Forcing cleanup of stuck fish.");
                ForceClearAllFish();
                break; 
            }
        }

        Debug.Log("Field clean and empty! Ready for background change window...");
        
        yield return new WaitForSeconds(delayBeforeNextLevel);
        
        SwitchBackgroundVisuals(currentLevel + 1);

        if (currentLevel < 6)
        {
            currentLevel++;
        }
        else
        {
            Debug.Log("Max level reached! Restarting difficulty rotation cycle.");
            currentLevel = 1; 
        }

        bossSpawnCount = 0; 
        ConfigureLevelSettings(currentLevel);
        
        isTransitioningLevel = false;
    }

    // Forces any lingering fish or bosses to hide immediately
    private void ForceClearAllFish()
    {
        // 1. Deactivate regular pooled fish
        foreach (var keyValuePair in fishPool)
        {
            List<GameObject> list = keyValuePair.Value;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].activeSelf)
                {
                    list[i].SetActive(false);
                }
            }
        }

        // 2. Deactivate boss instances
        for (int i = 0; i < activeBosses.Count; i++)
        {
            if (activeBosses[i] != null && activeBosses[i].activeSelf)
            {
                activeBosses[i].SetActive(false);
            }
        }
        activeBosses.Clear();
    }

    private void SwitchBackgroundVisuals(int targetLevel)
    {
        Debug.Log($"[VISUALS] Swapping game background to match Level {targetLevel}");
    }

    // Monitors your active bosses list and removes them dynamically as they go off-screen
    private bool AreBossesActive()
    {
        for (int i = activeBosses.Count - 1; i >= 0; i--)
        {
            if (activeBosses[i] == null)
            {
                activeBosses.RemoveAt(i);
                continue;
            }

            // If a boss has gone offscreen and been set to active = false by your movement script:
            if (!activeBosses[i].activeSelf)
            {
                activeBosses.RemoveAt(i); // Cleanly remove it from this tracking list
                continue;
            }

            return true; // There is still a boss active on screen
        }
        return false;
    }

    private bool AreRegularFishActive()
    {
        foreach (var keyValuePair in fishPool)
        {
            List<GameObject> list = keyValuePair.Value;
            for (int i = 0; i < list.Count; i++)
            {
                // If your fish movement script turned it off off-screen, it naturally bypasses this check
                if (list[i] != null && list[i].activeSelf) return true;
            }
        }
        return false;
    }

    // --- Dynamic Spawn Rarity System ---
    private int GetFishGenerate() 
    {
        int percent = Random.Range(1, 101);
        int fishLength = Fish.Length;
        if (fishLength == 0) return 0;

        float highTierChance = 90f - (currentLevel * 4f); 
        float midTierChance = 70f - (currentLevel * 5f);  

        if (percent >= highTierChance) 
        {
            int maxRange = Mathf.Min(12, fishLength);
            int minRange = Mathf.Min(9, fishLength - 1);
            return Random.Range(Mathf.Max(0, minRange), maxRange);
        }
        if (percent >= midTierChance) 
        {
            int minRange = Mathf.Min(6, fishLength - 1);
            return Random.Range(Mathf.Max(0, minRange), fishLength);
        }
        else 
        {
            int maxRange = Mathf.Min(5, fishLength);
            return Random.Range(1, maxRange);
        }
    }

    // --- Helper Utilities & Pooling Setup ---
    private float GetAngle(Transform spawnedFish, Transform endPosition)
    {
        Vector3 worldPosition = endPosition.position;
        worldPosition.z = 0;
        Vector3 direction = worldPosition - spawnedFish.position;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void RotateForFish(Transform spawnedFish, Transform endPosition, int angleRandom)
    {
        float angle = GetAngle(spawnedFish, endPosition);
        spawnedFish.rotation = Quaternion.Euler(0, 0, angle + angleRandom);
    }

    private GameObject GetFishFromPool(int fishId)
    {
        if (!fishPool.ContainsKey(fishId)) fishPool[fishId] = new List<GameObject>();

        List<GameObject> poolList = fishPool[fishId];
        for (int i = 0; i < poolList.Count; i++)
        {
            if (poolList[i] != null && !poolList[i].activeSelf) return poolList[i];
        }

        GameObject fishObject = Instantiate(Fish[fishId], FishSpawnPosition);
        poolList.Add(fishObject);
        return fishObject;
    }

    private Vector3[] GetFishFormation(int count, float spacing = 1.2f)
    {
        switch (count)
        {
            case 2: return new Vector3[] { new Vector3(0, spacing, 0), new Vector3(0, -spacing, 0) };
            case 3: return new Vector3[] { Vector3.zero, new Vector3(-spacing, spacing, 0), new Vector3(-spacing, -spacing, 0) };
            case 4: return new Vector3[] { new Vector3(0, spacing, 0), new Vector3(0, -spacing, 0), new Vector3(-spacing, spacing, 0), new Vector3(-spacing, -spacing, 0) };
            case 5: return new Vector3[] { Vector3.zero, new Vector3(-spacing, spacing, 0), new Vector3(-spacing, -spacing, 0), new Vector3(-spacing * 2, spacing, 0), new Vector3(-spacing * 2, -spacing, 0) };
            case 6: return new Vector3[] { Vector3.zero, new Vector3(-spacing, spacing, 0), new Vector3(-spacing, -spacing, 0), new Vector3(-spacing * 2, spacing, 0), new Vector3(-spacing * 2, -spacing, 0), new Vector3(-spacing * 3, 0, 0) };
        }
        return new Vector3[] { Vector3.zero };
    }

    private void SpawnFish(int fishId, Vector3 position, Vector3 endPosition)
    {
        GameObject fishObject = GetFishFromPool(fishId);
        fishObject.transform.position = position;

        float angle = Mathf.Atan2(endPosition.y - position.y, endPosition.x - position.x) * Mathf.Rad2Deg;
        fishObject.transform.rotation = Quaternion.Euler(0, 0, angle);
        fishObject.SetActive(true);

        if (fishObject.TryGetComponent<FishScript>(out FishScript fishScript)) fishScript.MoveHandle();
        if (fishObject.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr)) sr.sortingOrder = orderLayer;

        orderLayer += 2;
        if (orderLayer > 30000) orderLayer = 0;
    }

    private IEnumerator IEnumFish_SpawnLeftToRight()
    {
        while (true)
        {
            if (isTransitioningLevel)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            int fishId = GetFishGenerate();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-6f, 6f), 0f);
            Vector3 spawnPos = LeftPos.position + randomOffset;

            if (fishId == 1 || fishId == 2 || fishId == 3)
            {
                int groupCount = Random.Range(2, 7);
                Vector3[] formation = GetFishFormation(groupCount);
                Vector3 endCenter = RightPos.position + new Vector3(0f, Random.Range(-5f, 5f), 0f);

                foreach (Vector3 offset in formation)
                {
                    Vector3 endOffset = offset * Random.Range(0.9f, 1.1f);
                    SpawnFish(fishId, spawnPos + offset, endCenter + endOffset);
                }
            }
            else
            {
                SpawnFish(fishId, spawnPos, RightPos.position + new Vector3(0f, Random.Range(-5f, 5f), 0f));
            }

            yield return new WaitForSeconds(Random.Range(regularSpawnIntervalMin, regularSpawnIntervalMax));
        }
    }

    private IEnumerator IEnumFish_SpawnRightToLeft()
    {
        while (true)
        {
            if (isTransitioningLevel)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            int fishId = GetFishGenerate();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-6f, 6f), 0f);
            Vector3 spawnPos = RightPos.position + randomOffset;

            if (fishId == 1 || fishId == 2 || fishId == 3)
            {
                int groupCount = Random.Range(2, 7);
                Vector3[] formation = GetFishFormation(groupCount);
                Vector3 endCenter = LeftPos.position + new Vector3(0f, Random.Range(-5f, 5f), 0f);

                foreach (Vector3 offset in formation)
                {
                    Vector3 endOffset = offset * Random.Range(0.9f, 1.1f);
                    SpawnFish(fishId, spawnPos + offset, endCenter + endOffset);
                }
            }
            else
            {
                SpawnFish(fishId, spawnPos, LeftPos.position + new Vector3(0f, Random.Range(-5f, 5f), 0f));
            }

            yield return new WaitForSeconds(Random.Range(regularSpawnIntervalMin, regularSpawnIntervalMax));
        }
    }
}