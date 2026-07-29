using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public enum GameLevel
    {
        Level1_Beginner,
        Level2_School,
        Level3_Circle,
        Level4_Cross,
        Level5_Festival,
        Level6_BossOcean
    }

    public enum LevelPhase
    {
        Opening,
        FeatureBuildUp,
        PreBossParade,
        BossWarning,
        BossBattle,
        Recovery,
        TideChange
    }

    [Header("Background Settings")]
    public SpriteRenderer backgroundRenderer;
    public Sprite[] levelBackgrounds;

    [Header("Setup References")]
    public GameObject[] Fish;
    public Transform FishSpawnPosition;
    public Transform LeftPos;
    public Transform RightPos;
    public Transform TopPos;
    public Transform BottomPos;

    [Header("Gimmick Prefabs")]
    public GameObject BombCrabPrefab;
    public GameObject LightningChainPrefab;

    [Header("Array Index Rules - Match Your Inspector")]
    [SerializeField] private int bigBossStartIndex = 0;
    [SerializeField] private int bigBossEndIndex = 8;

    [SerializeField] private int smallFishStartIndex = 9;
    [SerializeField] private int smallFishEndIndex = 20;

    [SerializeField] private int mediumFishStartIndex = 21;
    [SerializeField] private int mediumFishEndIndex = 40;

    [SerializeField] private int miniBossStartIndex = 41;
    [SerializeField] private int miniBossEndIndex = 43;

    [SerializeField] private int extraNormalFishStartIndex = 44;

    [Header("Boss Targets - Exactly 1 or 2 Per Level")]
    [SerializeField] private int[] level1BossTargets = { 0 };
    [SerializeField] private int[] level2BossTargets = { 1, 2 };
    [SerializeField] private int[] level3BossTargets = { 3 };
    [SerializeField] private int[] level4BossTargets = { 4, 5 };
    [SerializeField] private int[] level5BossTargets = { 6 };
    [SerializeField] private int[] level6BossTargets = { 7, 8 };

    [Header("Professional Level Flow")]
    [Tooltip("Opening + feature time before the first target boss.")]
    [SerializeField] private float level1Warmup = 18f;
    [SerializeField] private float warmupIncreasePerLevel = 7f;

    [Tooltip("Boss warning gets a little longer every level.")]
    [SerializeField] private float bossWarningBaseDelay = 3f;
    [SerializeField] private float bossWarningIncreasePerLevel = 0.6f;

    [Tooltip("Used only on levels with two target bosses.")]
    [SerializeField] private float betweenTargetBossBaseDelay = 6f;
    [SerializeField] private float betweenTargetBossIncreasePerLevel = 1f;

    [SerializeField] private float postBossRecoveryBaseDelay = 4f;
    [SerializeField] private float postBossRecoveryIncreasePerLevel = 0.8f;

    [Tooltip("The screen-clean and background-swap delay increases every level.")]
    [SerializeField] private float tideChangeBaseDuration = 4f;
    [SerializeField] private float tideChangeIncreasePerLevel = 0.7f;

    [Header("Spawn Pacing")]
    [SerializeField] private float ambientDelayMin = 1.8f;
    [SerializeField] private float ambientDelayMax = 3.2f;
    [SerializeField] private float paradeDelayMin = 6f;
    [SerializeField] private float paradeDelayMax = 10f;
    [SerializeField] private int maxActiveFishBase = 28;
    [SerializeField] private int maxActiveFishPerLevel = 3;

    [Header("Pre-Boss Royal Parade")]
    [SerializeField, Range(2, 3)]
    private int earlyLevelParadeWaves = 2;

    [SerializeField, Range(2, 3)]
    private int advancedLevelParadeWaves = 3;

    [SerializeField]
    private float preBossParadeBaseGap = 3.2f;

    [SerializeField]
    private float preBossParadeGapPerLevel = 0.55f;

    [SerializeField]
    private float preBossFinalPauseBase = 1.5f;

    [SerializeField]
    private float preBossFinalPausePerLevel = 0.30f;

    [SerializeField, Range(0f, 1f)]
    private float preBossMiniBossBaseChance = 0.12f;

    [SerializeField, Range(0f, 1f)]
    private float preBossMiniBossChancePerLevel = 0.05f;

    [SerializeField]
    private float preBossMiniBossLeadBaseDelay = 4.5f;

    [SerializeField]
    private float preBossMiniBossLeadPerLevel = 0.55f;

    [SerializeField]
    private float preBossSpaceWaitTimeout = 6f;

    [Header("Concurrent Parade Groups")]
    [SerializeField] private int preBossExtraFishCapacity = 15;

    [SerializeField] private int paradeFishPerGroupBase = 6;
    [SerializeField] private int paradeFishPerGroupPerLevel = 1;
    [SerializeField] private int paradeFishPerGroupMaximum = 10;

    [SerializeField] private float paradeLaneSpacing = 2.6f;
    [SerializeField] private float paradeFishHorizontalGap = 1.05f;

    [SerializeField] private float paradeSpeedMultiplierBase = 1.55f;
    [SerializeField] private float paradeSpeedMultiplierPerLevel = 0.07f;

    [Header("Boss Arrival Events")]
    [SerializeField] private bool enableBossArrivalEvents = true;

    [Tooltip("Assign a camera parent or Main Camera. When empty, Camera.main is used.")]
    [SerializeField] private Transform cameraShakeTarget;

    [SerializeField] private float bossEventBaseDuration = 1.3f;
    [SerializeField] private float bossEventDurationPerLevel = 0.25f;

    [SerializeField] private float bossEventBaseShakeStrength = 0.07f;
    [SerializeField] private float bossEventShakeStrengthPerLevel = 0.035f;

    [SerializeField, Range(0f, 1f)]
    private float bossEventBackgroundTintStrength = 0.28f;

    [Tooltip("Optional event effects. Array index 0-5 matches Level 1-6.")]
    [SerializeField] private GameObject[] bossArrivalEventEffects;

    [SerializeField] private Transform bossEventEffectSpawnPoint;

    [Header("Longer Boss Arrival Timing")]
    [SerializeField]
    private float bossArrivalExtraBaseDelay = 3f;

    [SerializeField]
    private float bossArrivalExtraPerLevel = 0.8f;

    [Header("Gentle Difficulty Scaling")]
    [SerializeField] private float normalHpPerLevel = 0.03f;
    [SerializeField] private float normalRewardPerLevel = 0.025f;
    [SerializeField] private float normalSpeedPerLevel = 0.015f;

    [SerializeField] private float miniBossBaseHpMultiplier = 1.15f;
    [SerializeField] private float miniBossHpPerLevel = 0.10f;
    [SerializeField] private float miniBossRewardPerLevel = 0.08f;
    [SerializeField] private float miniBossSpeedPerLevel = 0.02f;

    [SerializeField] private float bossBaseHpMultiplier = 1.25f;
    [SerializeField] private float bossHpPerLevel = 0.15f;
    [SerializeField] private float bossRewardPerLevel = 0.12f;
    [SerializeField] private float bossSpeedPerLevel = 0.02f;

    [SerializeField] private float endlessHpPerLoop = 0.10f;
    [SerializeField] private float endlessRewardPerLoop = 0.08f;
    [SerializeField] private float endlessSpeedPerLoop = 0.02f;
    [SerializeField] private int maxScalingLoops = 10;

    [Header("Runtime Debug")]
    public GameLevel currentLevelState = GameLevel.Level1_Beginner;
    public LevelPhase currentPhase = LevelPhase.Opening;
    public bool isEndlessMode;
    public int loopMultiplier;

    [SerializeField] private float levelTimer;
    [SerializeField] private int targetBossNumber;
    [SerializeField] private int targetBossTotal;
    [SerializeField] private int defeatedBossCount;

    private float ambientSpawnTimer;
    private float paradeCooldownTimer;
    private bool isTideChanging;
    private bool currentTargetBossDefeated;
    private int orderLayer;

    private GameObject currentTargetBoss;
    private Coroutine gameLoopCoroutine;

    private readonly Dictionary<int, List<GameObject>> fishPool =
        new Dictionary<int, List<GameObject>>();

    private readonly List<GameObject> spawnedGimmicks =
        new List<GameObject>();

    private void Start()
    {
        ValidateAndRepairConfiguration();

        if (!enabled)
        {
            return;
        }

        FitSpriteToScreen(backgroundRenderer);
        BuildPoolDictionary();

        ambientSpawnTimer = 0.5f;
        paradeCooldownTimer = 0f;
        gameLoopCoroutine = StartCoroutine(GameLoopRoutine());
    }

    private void Update()
    {
        if (isTideChanging)
        {
            return;
        }

        levelTimer += Time.deltaTime;
        ambientSpawnTimer -= Time.deltaTime;
        paradeCooldownTimer -= Time.deltaTime;

        // These are clean presentation phases. Do not add more fish.
        if (currentPhase == LevelPhase.PreBossParade ||
    currentPhase == LevelPhase.BossWarning ||
    currentPhase == LevelPhase.Recovery)
        {
            return;
        }

        if (ambientSpawnTimer <= 0f)
        {
            SpawnAmbientNormalFish();
            ambientSpawnTimer = GetNextAmbientDelay();
        }

        if (paradeCooldownTimer <= 0f)
        {
            if (currentPhase == LevelPhase.BossBattle)
            {
                SpawnBossSupportWave();
                paradeCooldownTimer = GetBossSupportDelay();
            }
            else
            {
                TriggerLevelSpecificSpawn();
                paradeCooldownTimer = GetNextParadeDelay();
            }
        }
    }

    private IEnumerator GameLoopRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(RunLevelRoutine(currentLevelState));

            GameLevel nextLevel = GetNextLevel(currentLevelState);
            yield return StartCoroutine(TideChangeRoutine(nextLevel));

            currentLevelState = nextLevel;
        }
    }

    private IEnumerator RunLevelRoutine(GameLevel level)
    {
        levelTimer = 0f;
        defeatedBossCount = 0;
        targetBossNumber = 0;
        currentTargetBoss = null;
        currentTargetBossDefeated = false;

        int[] bossTargets = GetBossTargetsForLevel(level);
        targetBossTotal = bossTargets.Length;

        currentPhase = LevelPhase.Opening;
        paradeCooldownTimer = 0f;
        ambientSpawnTimer = 0.5f;

        float warmupDuration = GetWarmupDuration(level);
        float openingDuration = warmupDuration * 0.42f;
        float featureDuration = warmupDuration - openingDuration;

        Debug.Log(
            "<color=#4FC3F7>[LEVEL]</color> " +
            level + " opening started."
        );

        yield return new WaitForSeconds(openingDuration);

        currentPhase = LevelPhase.FeatureBuildUp;
        paradeCooldownTimer = 0f;

        Debug.Log(
            "<color=#FFD54F>[FEATURE]</color> " +
            "Mini-bosses and special formations enabled."
        );

        yield return new WaitForSeconds(featureDuration);

        // Bosses are sequential targets. A level cannot swap until all are defeated.
        for (int i = 0; i < bossTargets.Length; i++)
        {
            targetBossNumber = i + 1;
            currentTargetBossDefeated = false;

            currentPhase = LevelPhase.PreBossParade;

            yield return StartCoroutine(
                RunPreBossRoyalParadeRoutine(level, i)
            );

            currentPhase = LevelPhase.BossWarning;

            Debug.Log(
                "<color=#FF8A65>[BOSS WARNING]</color> Target " +
                targetBossNumber + "/" + targetBossTotal +
                " approaching. Fish array index: " + bossTargets[i]
            );

            yield return new WaitForSeconds(
                GetBossWarningDelay(level)
            );

            yield return StartCoroutine(
                RunBossArrivalEvent(level)
            );

            SpawnTargetBoss(bossTargets[i]);
            currentPhase = LevelPhase.BossBattle;
            paradeCooldownTimer = GetBossSupportDelay();
            ambientSpawnTimer = GetNextAmbientDelay() * 1.5f;

            while (!currentTargetBossDefeated)
            {
                // Safety: if another system disables the boss without defeating it,
                // restore the same target instead of leaving the level stuck forever.
                if (currentTargetBoss == null || !currentTargetBoss.activeSelf)
                {
                    yield return new WaitForSeconds(1.5f);

                    if (!currentTargetBossDefeated)
                    {
                        SpawnTargetBoss(bossTargets[i]);
                    }
                }

                yield return null;
            }

            defeatedBossCount++;
            currentPhase = LevelPhase.Recovery;

            if (i < bossTargets.Length - 1)
            {
                yield return new WaitForSeconds(GetBetweenBossDelay(level));
            }
        }

        currentPhase = LevelPhase.Recovery;

        Debug.Log(
            "<color=#81C784>[LEVEL CLEAR]</color> " +
            "All target bosses defeated."
        );

        yield return new WaitForSeconds(GetPostBossRecoveryDelay(level));
    }

    // Called only from FishScript after a real target-boss defeat.
    public void NotifyLevelBossDefeated(FishScript defeatedBoss)
    {
        if (defeatedBoss == null || currentTargetBoss == null)
        {
            return;
        }

        if (defeatedBoss.gameObject != currentTargetBoss ||
            currentTargetBossDefeated)
        {
            return;
        }

        currentTargetBossDefeated = true;
        currentTargetBoss = null;

        Debug.Log(
            "<color=#BA68C8>[TARGET DEFEATED]</color> " +
            targetBossNumber + "/" + targetBossTotal
        );
    }

    private IEnumerator TideChangeRoutine(GameLevel newLevel)
    {
        isTideChanging = true;
        currentPhase = LevelPhase.TideChange;

        int oldLevelIndex = (int)currentLevelState;
        float tideDuration =
            tideChangeBaseDuration +
            oldLevelIndex * tideChangeIncreasePerLevel;

        Debug.Log(
            "<color=#26C6DA>[TIDE CHANGE]</color> " +
            currentLevelState + " -> " + newLevel +
            " | delay " + tideDuration.ToString("0.0") + "s"
        );

        if (backgroundRenderer != null && levelBackgrounds != null)
        {
            int backgroundIndex = (int)newLevel;

            if (backgroundIndex >= 0 &&
                backgroundIndex < levelBackgrounds.Length)
            {
                StartCoroutine(
                    TransitionBackground(levelBackgrounds[backgroundIndex])
                );
            }
        }

        FishScript[] activeFish = FindObjectsOfType<FishScript>();

        foreach (FishScript fish in activeFish)
        {
            if (fish != null && fish.gameObject.activeSelf)
            {
                fish.SetMovementStyle(FishScript.SwimStyle.FastTideExit);
            }
        }

        yield return new WaitForSeconds(tideDuration);

        ForceDisableAllFish();
        ClearSpawnedGimmicks();

        if (currentLevelState == GameLevel.Level6_BossOcean &&
            newLevel == GameLevel.Level1_Beginner)
        {
            loopMultiplier++;
            isEndlessMode = true;

            Debug.Log(
                "<color=#EC407A>[ENDLESS CYCLE]</color> Cycle " +
                (loopMultiplier + 1) + " started."
            );
        }

        isTideChanging = false;
        currentPhase = LevelPhase.Opening;
        paradeCooldownTimer = 0f;
        ambientSpawnTimer = 0.5f;
    }

    private IEnumerator TransitionBackground(Sprite newSprite)
    {
        if (backgroundRenderer == null || newSprite == null)
        {
            yield break;
        }

        GameObject temporaryBackground = new GameObject("TempBG_Fade");
        temporaryBackground.transform.position = backgroundRenderer.transform.position;
        temporaryBackground.transform.rotation = backgroundRenderer.transform.rotation;
        temporaryBackground.transform.localScale = backgroundRenderer.transform.localScale;

        SpriteRenderer temporaryRenderer =
            temporaryBackground.AddComponent<SpriteRenderer>();

        temporaryRenderer.sprite = backgroundRenderer.sprite;
        temporaryRenderer.sortingLayerID = backgroundRenderer.sortingLayerID;
        temporaryRenderer.sortingOrder = backgroundRenderer.sortingOrder + 1;
        temporaryRenderer.color = backgroundRenderer.color;

        backgroundRenderer.sprite = newSprite;
        FitSpriteToScreen(backgroundRenderer);

        float fadeDuration = 1.5f;
        float timer = 0f;
        Color startColor = temporaryRenderer.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

            temporaryRenderer.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                alpha
            );

            yield return null;
        }

        Destroy(temporaryBackground);
    }

    private void TriggerLevelSpecificSpawn()
    {
        if (CountActiveFish() >= GetMaxActiveFish())
        {
            return;
        }

        int smallFishIndex = GetSmallFishIndex();

        switch (currentLevelState)
        {
            case GameLevel.Level1_Beginner:
                if (Random.value < 0.55f)
                {
                    SpawnStraightLineParade(
                        smallFishIndex,
                        FishScript.SwimStyle.Straight
                    );
                }
                else
                {
                    SpawnStraightLineParade(
                        smallFishIndex,
                        FishScript.SwimStyle.Wave
                    );
                }
                break;

            case GameLevel.Level2_School:
                SpawnVFormationParade(smallFishIndex);
                TrySpawnFeatureMiniBoss(0.12f);
                break;

            case GameLevel.Level3_Circle:
                SpawnCircleParade(smallFishIndex);
                TrySpawnGimmick(0.18f);
                TrySpawnFeatureMiniBoss(0.16f);
                break;

            case GameLevel.Level4_Cross:
                SpawnCrossAttackParade();
                TrySpawnGimmick(0.20f);
                TrySpawnFeatureMiniBoss(0.18f);
                break;

            case GameLevel.Level5_Festival:
                float festivalRoll = Random.value;

                if (festivalRoll < 0.34f)
                {
                    SpawnVFormationParade(smallFishIndex);
                }
                else if (festivalRoll < 0.67f)
                {
                    SpawnCircleParade(smallFishIndex);
                }
                else
                {
                    SpawnStraightLineParade(
                        smallFishIndex,
                        FishScript.SwimStyle.Curious
                    );
                }

                TrySpawnGimmick(0.35f);
                TrySpawnFeatureMiniBoss(0.22f);
                break;

            case GameLevel.Level6_BossOcean:
                SpawnBossPreviewEscort();
                TrySpawnGimmick(0.30f);
                TrySpawnFeatureMiniBoss(0.25f);
                break;
        }
    }

    private IEnumerator RunPreBossRoyalParadeRoutine(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        int waveCount = GetPreBossParadeWaveCount(
            level,
            bossSequenceIndex
        );

        Debug.Log(
            "<color=#CE93D8>[ROYAL PARADE]</color> " +
            waveCount + " parade waves before boss target " +
            (bossSequenceIndex + 1) + "."
        );

        for (int waveIndex = 0;
             waveIndex < waveCount;
             waveIndex++)
        {
            yield return StartCoroutine(
                WaitForPreBossParadeSpace()
            );

            SpawnPreBossParadeWave(
                level,
                waveIndex
            );

            yield return new WaitForSeconds(
                GetPreBossParadeGap(
                    level,
                    waveIndex
                )
            );
        }

        if (ShouldSpawnPreBossMiniBoss(
            level,
            bossSequenceIndex
        ))
        {
            Debug.Log(
                "<color=#BA68C8>[MINI BOSS HERALD]</color> " +
                "A mini boss enters before the main boss."
            );

            SpawnMiniBoss(
                GetMiniBossIndexForLevel(level)
            );

            yield return new WaitForSeconds(
                GetPreBossMiniBossLeadDelay(level)
            );
        }

        yield return new WaitForSeconds(
            GetPreBossFinalPause(level)
        );
    }

    private IEnumerator WaitForPreBossParadeSpace()
    {
        float elapsed = 0f;

        float maximumWait = Mathf.Min(
            preBossSpaceWaitTimeout,
            1.5f
        );

        int paradeActiveLimit =
            GetMaxActiveFish() +
            preBossExtraFishCapacity;

        while (CountActiveFish() >= paradeActiveLimit &&
               elapsed < maximumWait)
        {
            elapsed += 0.25f;

            yield return new WaitForSeconds(0.25f);
        }
    }


    private IEnumerator RunBossArrivalEvent(
        GameLevel level
    )
    {
        if (!enableBossArrivalEvents)
        {
            yield break;
        }

        Transform shakeTransform = cameraShakeTarget;

        if (shakeTransform == null &&
            Camera.main != null)
        {
            shakeTransform = Camera.main.transform;
        }

        Vector3 originalCameraLocalPosition =
            shakeTransform != null
                ? shakeTransform.localPosition
                : Vector3.zero;

        Color originalBackgroundColor =
            backgroundRenderer != null
                ? backgroundRenderer.color
                : Color.white;

        Color eventColor =
            GetBossArrivalEventColor(level);

        eventColor.a =
            originalBackgroundColor.a;

        float duration =
            bossEventBaseDuration +
            (int)level *
            bossEventDurationPerLevel;

        float shakeStrength =
            bossEventBaseShakeStrength +
            (int)level *
            bossEventShakeStrengthPerLevel;

        Debug.Log(
            "<color=#FF5252>[BOSS EVENT]</color> " +
            GetBossArrivalEventName(level)
        );

        SpawnBossEventRushFish(level);
        SpawnOptionalBossEventEffect(level, duration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsed / duration
            );

            float remainingStrength =
                1f - normalizedTime;

            float pulse =
                (Mathf.Sin(elapsed * 18f) + 1f) * 0.5f;

            if (shakeTransform != null)
            {
                Vector2 randomShake =
                    Random.insideUnitCircle *
                    shakeStrength *
                    remainingStrength;

                shakeTransform.localPosition =
                    originalCameraLocalPosition +
                    new Vector3(
                        randomShake.x,
                        randomShake.y,
                        0f
                    );
            }

            if (backgroundRenderer != null)
            {
                float tintAmount =
                    pulse *
                    bossEventBackgroundTintStrength *
                    remainingStrength;

                backgroundRenderer.color = Color.Lerp(
                    originalBackgroundColor,
                    eventColor,
                    tintAmount
                );
            }

            yield return null;
        }

        if (shakeTransform != null)
        {
            shakeTransform.localPosition =
                originalCameraLocalPosition;
        }

        if (backgroundRenderer != null)
        {
            backgroundRenderer.color =
                originalBackgroundColor;
        }
    }

    private string GetBossArrivalEventName(
        GameLevel level
    )
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return "Water Rumble";

            case GameLevel.Level2_School:
                return "Tidal Surge";

            case GameLevel.Level3_Circle:
                return "Earthquake";

            case GameLevel.Level4_Cross:
                return "Thunder Pulse";

            case GameLevel.Level5_Festival:
                return "Ocean Storm";

            case GameLevel.Level6_BossOcean:
                return "Abyss Quake";

            default:
                return "Boss Warning";
        }
    }
    private Color GetBossArrivalEventColor(
        GameLevel level
    )
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return new Color(
                    0.35f,
                    0.85f,
                    1f,
                    1f
                );

            case GameLevel.Level2_School:
                return new Color(
                    0.1f,
                    0.55f,
                    1f,
                    1f
                );

            case GameLevel.Level3_Circle:
                return new Color(
                    0.75f,
                    0.7f,
                    0.55f,
                    1f
                );

            case GameLevel.Level4_Cross:
                return new Color(
                    0.75f,
                    0.45f,
                    1f,
                    1f
                );

            case GameLevel.Level5_Festival:
                return new Color(
                    0.2f,
                    0.25f,
                    0.65f,
                    1f
                );

            case GameLevel.Level6_BossOcean:
                return new Color(
                    0.8f,
                    0.12f,
                    0.15f,
                    1f
                );

            default:
                return Color.white;
        }
    }

    private void SpawnBossEventRushFish(
        GameLevel level
    )
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int levelIndex = (int)level;

        int rushGroupCount = Mathf.Clamp(
            1 + levelIndex / 2,
            1,
            3
        );

        int fishPerGroup = Mathf.Clamp(
            4 + levelIndex,
            4,
            8
        );

        for (int group = 0;
             group < rushGroupCount;
             group++)
        {
            bool fromLeft = group % 2 == 0;

            float laneY =
                GetParadeGroupLaneY(
                    group,
                    rushGroupCount
                );

            SpawnFastLineParadeGroup(
                GetSmallFishIndex(),
                laneY,
                fromLeft,
                fishPerGroup,
                FishScript.SwimStyle.FastTideExit
            );
        }
    }

    private void SpawnOptionalBossEventEffect(
        GameLevel level,
        float eventDuration
    )
    {
        int levelIndex = (int)level;

        if (bossArrivalEventEffects == null ||
            levelIndex < 0 ||
            levelIndex >= bossArrivalEventEffects.Length ||
            bossArrivalEventEffects[levelIndex] == null)
        {
            return;
        }

        Vector3 spawnPosition =
            bossEventEffectSpawnPoint != null
                ? bossEventEffectSpawnPoint.position
                : Vector3.zero;

        GameObject eventEffect = Instantiate(
            bossArrivalEventEffects[levelIndex],
            spawnPosition,
            Quaternion.identity
        );

        Destroy(
            eventEffect,
            eventDuration + 2f
        );
    }




    private void SpawnPreBossParadeWave(
    GameLevel level,
    int waveIndex
)
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int groupCount = GetConcurrentParadeGroupCount(
            level,
            waveIndex
        );

        int paradeActiveLimit =
            GetMaxActiveFish() +
            preBossExtraFishCapacity;

        int availableSlots = Mathf.Max(
            0,
            paradeActiveLimit - CountActiveFish()
        );

        if (availableSlots <= 0)
        {
            return;
        }

        // Every group should have at least three fish.
        groupCount = Mathf.Min(
            groupCount,
            Mathf.Max(1, availableSlots / 3)
        );

        int targetFishPerGroup =
            GetParadeFishPerGroup(level);

        int fishPerGroup = Mathf.Clamp(
            availableSlots / groupCount,
            3,
            targetFishPerGroup
        );

        Debug.Log(
            "<color=#80DEEA>[PARADE BATCH]</color> " +
            groupCount + " groups spawning together, " +
            fishPerGroup + " fish per group."
        );

        for (int groupIndex = 0;
             groupIndex < groupCount;
             groupIndex++)
        {
            float laneY = GetParadeGroupLaneY(
                groupIndex,
                groupCount
            );

            bool fromLeft =
                (waveIndex + groupIndex) % 2 == 0;

            int fishIndex = GetSmallFishIndex();

            int pattern =
                ((int)level + waveIndex + groupIndex) % 3;

            switch (pattern)
            {
                case 0:
                    SpawnFastLineParadeGroup(
                        fishIndex,
                        laneY,
                        fromLeft,
                        fishPerGroup,
                        FishScript.SwimStyle.Wave
                    );
                    break;

                case 1:
                    SpawnFastVParadeGroup(
                        fishIndex,
                        laneY,
                        fromLeft,
                        fishPerGroup
                    );
                    break;

                default:
                    SpawnFastSchoolParadeGroup(
                        fishIndex,
                        laneY,
                        fromLeft,
                        fishPerGroup
                    );
                    break;
            }
        }
    }


    private int GetConcurrentParadeGroupCount(
    GameLevel level,
    int waveIndex
)
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return 1;

            case GameLevel.Level2_School:
                return Random.Range(1, 3);

            case GameLevel.Level3_Circle:
                return 2;

            case GameLevel.Level4_Cross:
                return Random.Range(2, 4);

            case GameLevel.Level5_Festival:
            case GameLevel.Level6_BossOcean:
                return 3;

            default:
                return 1;
        }
    }

    private float GetParadeGroupLaneY(
        int groupIndex,
        int groupCount
    )
    {
        float centeredIndex =
            groupIndex -
            (groupCount - 1) * 0.5f;

        return centeredIndex * paradeLaneSpacing +
               Random.Range(-0.25f, 0.25f);
    }

    private void SpawnFastLineParadeGroup(
    int fishIndex,
    float laneY,
    bool fromLeft,
    int fishCount,
    FishScript.SwimStyle style
)
    {
        Vector3 startPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 trailingDirection =
            fromLeft
                ? Vector3.left
                : Vector3.right;

        for (int i = 0; i < fishCount; i++)
        {
            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (i * paradeFishHorizontalGap);

            SpawnFastParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                style
            );
        }
    }

    private void SpawnFastVParadeGroup(
        int fishIndex,
        float laneY,
        bool fromLeft,
        int fishCount
    )
    {
        Vector3 leaderPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            leader,
            out FishScript leaderScript
        ))
        {
            return;
        }

        ApplyParadeScaling(leaderScript);

        leaderScript.SetMovementStyle(
            FishScript.SwimStyle.Wave
        );

        int remainingFish = fishCount - 1;
        int pairCount = remainingFish / 2;

        for (int pair = 1; pair <= pairCount; pair++)
        {
            Vector3 upperLocalOffset = new Vector3(
                -1.05f * pair,
                0.65f * pair,
                0f
            );

            Vector3 lowerLocalOffset = new Vector3(
                -1.05f * pair,
                -0.65f * pair,
                0f
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                upperLocalOffset,
                targetPosition
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                lowerLocalOffset,
                targetPosition
            );
        }

        // Add one center follower when fishCount is even.
        if (remainingFish % 2 != 0)
        {
            Vector3 centerOffset = new Vector3(
                -1.05f * (pairCount + 1),
                0f,
                0f
            );

            SpawnFastParadeFollower(
                fishIndex,
                leader,
                centerOffset,
                targetPosition
            );
        }
    }

    private void SpawnFastParadeFollower(
        int fishIndex,
        GameObject leader,
        Vector3 localOffset,
        Vector3 targetPosition
    )
    {
        if (leader == null)
        {
            return;
        }

        Vector3 worldOffset =
            leader.transform.TransformDirection(
                localOffset
            );

        GameObject follower = GetFishFromPool(
            fishIndex,
            leader.transform.position + worldOffset,
            targetPosition
        );

        if (!TryGetFishScript(
            follower,
            out FishScript followerScript
        ))
        {
            return;
        }

        ApplyParadeScaling(followerScript);

        followerScript.SetMovementStyle(
            FishScript.SwimStyle.FollowLeader,
            leader.transform,
            localOffset
        );
    }


    private void SpawnFastSchoolParadeGroup(
        int fishIndex,
        float laneY,
        bool fromLeft,
        int fishCount
    )
    {
        Vector3 startPosition =
            (fromLeft ? LeftPos.position : RightPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 targetPosition =
            (fromLeft ? RightPos.position : LeftPos.position) +
            new Vector3(0f, laneY, 0f);

        Vector3 trailingDirection =
            fromLeft
                ? Vector3.left
                : Vector3.right;

        for (int i = 0; i < fishCount; i++)
        {
            int column = i / 2;
            int row = i % 2;

            float rowY =
                row == 0 ? -0.55f : 0.55f;

            Vector3 spawnPosition =
                startPosition +
                trailingDirection *
                (column * paradeFishHorizontalGap) +
                new Vector3(0f, rowY, 0f);

            SpawnFastParadeFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                FishScript.SwimStyle.Straight
            );
        }
    }

    private void SpawnFastParadeFish(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        FishScript.SwimStyle style
    )
    {
        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(
            fish,
            out FishScript script
        ))
        {
            return;
        }

        ApplyParadeScaling(script);
        script.SetMovementStyle(style);
    }

    private void ApplyParadeScaling(
        FishScript script
    )
    {
        int levelIndex =
            (int)currentLevelState;

        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        float hpMultiplier =
            1f +
            levelIndex * normalHpPerLevel +
            effectiveLoop * endlessHpPerLoop;

        float rewardMultiplier =
            1f +
            levelIndex * normalRewardPerLevel +
            effectiveLoop * endlessRewardPerLoop;

        float paradeSpeedBonus =
            paradeSpeedMultiplierBase +
            levelIndex *
            paradeSpeedMultiplierPerLevel;

        float speedMultiplier =
            (
                1f +
                levelIndex * normalSpeedPerLevel +
                effectiveLoop * endlessSpeedPerLoop
            ) * paradeSpeedBonus;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private void SpawnAmbientNormalFish()
    {
        if (!ReferencesAreReady() ||
            CountActiveFish() >= GetMaxActiveFish())
        {
            return;
        }

        int fishIndex = Random.value < 0.72f
            ? GetSmallFishIndex()
            : GetMediumFishIndex();

        bool spawnFromLeft = Random.value > 0.5f;
        Vector3 spawnPosition = spawnFromLeft
            ? LeftPos.position
            : RightPos.position;

        Vector3 targetPosition = spawnFromLeft
            ? RightPos.position
            : LeftPos.position;

        spawnPosition += new Vector3(
            0f,
            Random.Range(-4.5f, 4.5f),
            0f
        );

        targetPosition += new Vector3(
            0f,
            Random.Range(-4.5f, 4.5f),
            0f
        );

        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(fish, out FishScript script))
        {
            return;
        }

        ApplyNormalScaling(script);

        FishScript.SwimStyle style = Random.value < 0.55f
            ? FishScript.SwimStyle.Wave
            : FishScript.SwimStyle.Curious;

        script.SetMovementStyle(style);
    }

    private void SpawnStraightLineParade(
        int fishIndex,
        FishScript.SwimStyle style
    )
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        Vector3 startPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-4f, 4f),
            0f
        );

        Vector3 targetPosition = RightPos.position + new Vector3(
            0f,
            startPosition.y - LeftPos.position.y,
            0f
        );

        int count = Mathf.Clamp(5 + GetFormationGrowth(), 5, 12);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition =
                startPosition - new Vector3(i * 1.4f, 0f, 0f);

            SpawnScaledNormalFish(
                fishIndex,
                spawnPosition,
                targetPosition,
                style
            );
        }
    }

    private void SpawnVFormationParade(int fishIndex)
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        Vector3 leaderPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-2f, 2f),
            0f
        );

        Vector3 targetPosition = RightPos.position + new Vector3(
            0f,
            leaderPosition.y - LeftPos.position.y,
            0f
        );

        GameObject leader = GetFishFromPool(
            fishIndex,
            leaderPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            return;
        }

        ApplyNormalScaling(leaderScript);
        leaderScript.SetMovementStyle(FishScript.SwimStyle.Straight);

        int pairs = Mathf.Clamp(
            2 + GetFormationGrowth() / 2,
            2,
            5
        );

        for (int pair = 1; pair <= pairs; pair++)
        {
            Vector3 topOffset = new Vector3(
                -1.2f * pair,
                0.9f * pair,
                0f
            );

            Vector3 bottomOffset = new Vector3(
                -1.2f * pair,
                -0.9f * pair,
                0f
            );

            GameObject topFish = GetFishFromPool(
                fishIndex,
                leaderPosition + topOffset,
                targetPosition
            );

            if (TryGetFishScript(topFish, out FishScript topScript))
            {
                ApplyNormalScaling(topScript);
                topScript.SetMovementStyle(
                    FishScript.SwimStyle.FollowLeader,
                    leader.transform,
                    topOffset
                );
            }

            GameObject bottomFish = GetFishFromPool(
                fishIndex,
                leaderPosition + bottomOffset,
                targetPosition
            );

            if (TryGetFishScript(bottomFish, out FishScript bottomScript))
            {
                ApplyNormalScaling(bottomScript);
                bottomScript.SetMovementStyle(
                    FishScript.SwimStyle.FollowLeader,
                    leader.transform,
                    bottomOffset
                );
            }
        }
    }

    private void SpawnCircleParade(int fishIndex)
    {
        float radius = 3.5f;
        int count = Mathf.Clamp(7 + GetFormationGrowth(), 7, 14);

        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            ) * radius;

            GameObject fish = GetFishFromPool(
                fishIndex,
                offset,
                Vector3.zero
            );

            if (TryGetFishScript(fish, out FishScript script))
            {
                ApplyNormalScaling(script);
                script.SetMovementStyle(FishScript.SwimStyle.OrbitCenter);
            }
        }
    }

    private void SpawnCrossAttackParade()
    {
        if (!ReferencesAreReady() || TopPos == null || BottomPos == null)
        {
            return;
        }

        int horizontalFishIndex = GetSmallFishIndex();
        int verticalFishIndex = GetSmallFishIndex();
        int lineCount = Mathf.Clamp(
            3 + GetFormationGrowth() / 2,
            3,
            7
        );

        for (int i = 0; i < lineCount; i++)
        {
            SpawnScaledNormalFish(
                horizontalFishIndex,
                LeftPos.position + new Vector3(-i * 1.4f, 2f, 0f),
                RightPos.position,
                FishScript.SwimStyle.Straight
            );

            SpawnScaledNormalFish(
                horizontalFishIndex,
                RightPos.position + new Vector3(i * 1.4f, -2f, 0f),
                LeftPos.position,
                FishScript.SwimStyle.Straight
            );

            SpawnScaledNormalFish(
                verticalFishIndex,
                TopPos.position + new Vector3(-2f, i * 1.4f, 0f),
                BottomPos.position,
                FishScript.SwimStyle.Straight
            );

            SpawnScaledNormalFish(
                verticalFishIndex,
                BottomPos.position + new Vector3(2f, -i * 1.4f, 0f),
                TopPos.position,
                FishScript.SwimStyle.Straight
            );
        }
    }

    private void SpawnTwinLaneParade(int fishIndex)
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int fishPerLane = Mathf.Clamp(
            3 + GetFormationGrowth() / 2,
            3,
            6
        );

        float topLaneY = 2.2f;
        float bottomLaneY = -2.2f;

        for (int i = 0; i < fishPerLane; i++)
        {
            SpawnScaledNormalFish(
                fishIndex,
                LeftPos.position + new Vector3(
                    -i * 1.25f,
                    topLaneY,
                    0f
                ),
                RightPos.position + new Vector3(
                    0f,
                    topLaneY,
                    0f
                ),
                FishScript.SwimStyle.Wave
            );

            SpawnScaledNormalFish(
                fishIndex,
                RightPos.position + new Vector3(
                    i * 1.25f,
                    bottomLaneY,
                    0f
                ),
                LeftPos.position + new Vector3(
                    0f,
                    bottomLaneY,
                    0f
                ),
                FishScript.SwimStyle.Wave
            );
        }
    }


    private int GetPreBossParadeWaveCount(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        int count = (int)level < 2
            ? earlyLevelParadeWaves
            : advancedLevelParadeWaves;

        // The second target boss still receives a parade,
        // but the sequence is slightly shorter.
        if (bossSequenceIndex > 0)
        {
            count--;
        }

        return Mathf.Clamp(count, 2, 3);
    }

    private float GetPreBossParadeGap(
        GameLevel level,
        int waveIndex
    )
    {
        return preBossParadeBaseGap +
               (int)level * preBossParadeGapPerLevel +
               waveIndex * 0.35f;
    }

    private bool ShouldSpawnPreBossMiniBoss(
        GameLevel level,
        int bossSequenceIndex
    )
    {
        float chance =
            preBossMiniBossBaseChance +
            (int)level *
            preBossMiniBossChancePerLevel;

        // Reduce the chance before a second target boss.
        if (bossSequenceIndex > 0)
        {
            chance *= 0.65f;
        }

        return Random.value <= Mathf.Clamp(
            chance,
            0f,
            0.45f
        );
    }

    private float GetPreBossMiniBossLeadDelay(
        GameLevel level
    )
    {
        return preBossMiniBossLeadBaseDelay +
               (int)level *
               preBossMiniBossLeadPerLevel;
    }

    private float GetPreBossFinalPause(
        GameLevel level
    )
    {
        return preBossFinalPauseBase +
               (int)level *
               preBossFinalPausePerLevel;
    }

    private int GetParadeFishPerGroup(GameLevel level)
    {
        int levelIndex = (int)level;

        return Mathf.Clamp(
            paradeFishPerGroupBase +
            levelIndex * paradeFishPerGroupPerLevel,
            3,
            paradeFishPerGroupMaximum
        );
    }
    private void SpawnBossPreviewEscort()
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        int previewFishIndex = GetMediumFishIndex();

        Vector3 startPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-2f, 2f),
            0f
        );

        Vector3 targetPosition = RightPos.position;

        GameObject leader = GetFishFromPool(
            previewFishIndex,
            startPosition,
            targetPosition
        );

        if (!TryGetFishScript(leader, out FishScript leaderScript))
        {
            return;
        }

        ApplyNormalScaling(leaderScript);
        leaderScript.SetMovementStyle(FishScript.SwimStyle.Wave);

        int guardCount = 4 + Mathf.Clamp(loopMultiplier, 0, 3);

        for (int i = 0; i < guardCount; i++)
        {
            float angle = i * 360f / guardCount * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            ) * 2.2f;

            GameObject guard = GetFishFromPool(
                GetSmallFishIndex(),
                startPosition + offset,
                targetPosition
            );

            if (TryGetFishScript(guard, out FishScript guardScript))
            {
                ApplyNormalScaling(guardScript);
                guardScript.SetMovementStyle(
                    FishScript.SwimStyle.FollowLeader,
                    leader.transform,
                    offset
                );
            }
        }
    }

    private void SpawnTargetBoss(int bossIndex)
    {
        if (!ReferencesAreReady())
        {
            currentTargetBossDefeated = true;
            return;
        }

        if (!IsValidBigBossIndex(bossIndex))
        {
            Debug.LogError(
                "Boss index " + bossIndex +
                " is invalid. Big bosses must use array index 0 to 8."
            );

            currentTargetBossDefeated = true;
            return;
        }

        Vector3 spawnPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-1.5f, 1.5f),
            0f
        );

        GameObject boss = GetFishFromPool(
            bossIndex,
            spawnPosition,
            Vector3.zero
        );

        if (!TryGetFishScript(boss, out FishScript bossScript))
        {
            currentTargetBossDefeated = true;
            return;
        }

        ApplyBossScaling(bossScript);
        bossScript.ConfigureAsLevelBoss(this);
        bossScript.SetMovementStyle(FishScript.SwimStyle.BossArena);

        currentTargetBoss = boss;
        SpawnBossEntranceGuards(boss.transform);
    }

    private void SpawnBossEntranceGuards(Transform bossTransform)
    {
        if (bossTransform == null)
        {
            return;
        }

        int guardCount = Mathf.Clamp(
            4 + (int)currentLevelState,
            4,
            8
        );

        float radius = 2.4f;

        for (int i = 0; i < guardCount; i++)
        {
            float angle = i * 360f / guardCount * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            ) * radius;

            GameObject guard = GetFishFromPool(
                GetSmallFishIndex(),
                bossTransform.position + offset,
                bossTransform.position
            );

            if (TryGetFishScript(guard, out FishScript guardScript))
            {
                ApplyNormalScaling(guardScript);
                guardScript.SetMovementStyle(
                    FishScript.SwimStyle.FollowLeader,
                    bossTransform,
                    offset
                );
            }
        }
    }

    private void SpawnBossSupportWave()
    {
        if (!ReferencesAreReady() ||
            currentTargetBoss == null ||
            !currentTargetBoss.activeSelf ||
            CountActiveFish() >= GetMaxActiveFish())
        {
            return;
        }

        int supportCount = Mathf.Clamp(
            2 + (int)currentLevelState / 2,
            2,
            5
        );

        bool spawnFromLeft = Random.value > 0.5f;
        Vector3 start = spawnFromLeft
            ? LeftPos.position
            : RightPos.position;

        Vector3 end = spawnFromLeft
            ? RightPos.position
            : LeftPos.position;

        for (int i = 0; i < supportCount; i++)
        {
            Vector3 spawnPosition = start + new Vector3(
                0f,
                Random.Range(-3f, 3f),
                0f
            );

            spawnPosition -= new Vector3(i * 1.1f, 0f, 0f);

            SpawnScaledNormalFish(
                GetSmallFishIndex(),
                spawnPosition,
                end,
                FishScript.SwimStyle.Wave
            );
        }
    }

    private void TrySpawnFeatureMiniBoss(float chance)
    {
        if (currentPhase != LevelPhase.FeatureBuildUp ||
            Random.value > chance)
        {
            return;
        }

        SpawnMiniBoss(GetMiniBossIndexForLevel(currentLevelState));
    }

    private void SpawnMiniBoss(int miniBossIndex)
    {
        if (!ReferencesAreReady() ||
            !IsValidMiniBossIndex(miniBossIndex))
        {
            return;
        }

        bool spawnFromLeft = Random.value > 0.5f;
        Vector3 spawnPosition = spawnFromLeft
            ? LeftPos.position
            : RightPos.position;

        Vector3 targetPosition = spawnFromLeft
            ? RightPos.position
            : LeftPos.position;

        spawnPosition += new Vector3(
            0f,
            Random.Range(-3f, 3f),
            0f
        );

        GameObject miniBoss = GetFishFromPool(
            miniBossIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(miniBoss, out FishScript script))
        {
            return;
        }

        ApplyMiniBossScaling(script);
        script.ConfigureAsMiniBoss();

        script.SetMovementStyle(
            Random.value > 0.5f
                ? FishScript.SwimStyle.Wave
                : FishScript.SwimStyle.Curious
        );
    }

    private void TrySpawnGimmick(float chance)
    {
        if (Random.value <= chance)
        {
            SpawnGimmickFish();
        }
    }

    private void SpawnGimmickFish()
    {
        if (!ReferencesAreReady())
        {
            return;
        }

        GameObject selectedPrefab = Random.value < 0.5f
            ? BombCrabPrefab
            : LightningChainPrefab;

        if (selectedPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = LeftPos.position + new Vector3(
            0f,
            Random.Range(-4f, 4f),
            0f
        );

        Vector3 targetPosition = RightPos.position + new Vector3(
            0f,
            spawnPosition.y - LeftPos.position.y,
            0f
        );

        GameObject gimmick = Instantiate(
            selectedPrefab,
            spawnPosition,
            Quaternion.identity,
            FishSpawnPosition
        );

        spawnedGimmicks.Add(gimmick);

        float angle = Mathf.Atan2(
            targetPosition.y - spawnPosition.y,
            targetPosition.x - spawnPosition.x
        ) * Mathf.Rad2Deg;

        gimmick.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        gimmick.SetActive(true);

        if (gimmick.TryGetComponent<FishScript>(out FishScript script))
        {
            ApplyNormalScaling(script);
            script.SetMovementStyle(FishScript.SwimStyle.Wave);
        }
    }

    private void SpawnScaledNormalFish(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        FishScript.SwimStyle style
    )
    {
        GameObject fish = GetFishFromPool(
            fishIndex,
            spawnPosition,
            targetPosition
        );

        if (!TryGetFishScript(fish, out FishScript script))
        {
            return;
        }

        ApplyNormalScaling(script);
        script.SetMovementStyle(style);
    }

    private void ApplyNormalScaling(FishScript script)
    {
        int levelIndex = (int)currentLevelState;
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        float hpMultiplier =
            1f +
            levelIndex * normalHpPerLevel +
            effectiveLoop * endlessHpPerLoop;

        float rewardMultiplier =
            1f +
            levelIndex * normalRewardPerLevel +
            effectiveLoop * endlessRewardPerLoop;

        float speedMultiplier =
            1f +
            levelIndex * normalSpeedPerLevel +
            effectiveLoop * endlessSpeedPerLoop;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private void ApplyMiniBossScaling(FishScript script)
    {
        int levelIndex = (int)currentLevelState;
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        float hpMultiplier =
            miniBossBaseHpMultiplier +
            levelIndex * miniBossHpPerLevel +
            effectiveLoop * endlessHpPerLoop;

        float rewardMultiplier =
            1.10f +
            levelIndex * miniBossRewardPerLevel +
            effectiveLoop * endlessRewardPerLoop;

        float speedMultiplier =
            0.95f +
            levelIndex * miniBossSpeedPerLevel +
            effectiveLoop * endlessSpeedPerLoop;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private void ApplyBossScaling(FishScript script)
    {
        int levelIndex = (int)currentLevelState;
        int effectiveLoop = Mathf.Clamp(
            loopMultiplier,
            0,
            maxScalingLoops
        );

        float hpMultiplier =
            bossBaseHpMultiplier +
            levelIndex * bossHpPerLevel +
            effectiveLoop * endlessHpPerLoop;

        float rewardMultiplier =
            1.20f +
            levelIndex * bossRewardPerLevel +
            effectiveLoop * endlessRewardPerLoop;

        float speedMultiplier =
            0.90f +
            levelIndex * bossSpeedPerLevel +
            effectiveLoop * endlessSpeedPerLoop;

        script.ApplyRuntimeScaling(
            hpMultiplier,
            rewardMultiplier,
            speedMultiplier
        );
    }

    private GameObject GetFishFromPool(
        int fishIndex,
        Vector3 spawnPosition,
        Vector3 targetPosition
    )
    {
        if (Fish == null || Fish.Length == 0)
        {
            return null;
        }

        fishIndex = Mathf.Clamp(fishIndex, 0, Fish.Length - 1);

        if (Fish[fishIndex] == null)
        {
            Debug.LogError(
                "Fish array element " + fishIndex + " is empty."
            );
            return null;
        }

        if (!fishPool.ContainsKey(fishIndex))
        {
            fishPool[fishIndex] = new List<GameObject>();
        }

        GameObject fishInstance = null;
        List<GameObject> pool = fishPool[fishIndex];

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeSelf)
            {
                fishInstance = pool[i];
                break;
            }
        }

        if (fishInstance == null)
        {
            fishInstance = Instantiate(
                Fish[fishIndex],
                FishSpawnPosition
            );

            pool.Add(fishInstance);
        }

        fishInstance.transform.position = spawnPosition;

        float angle = Mathf.Atan2(
            targetPosition.y - spawnPosition.y,
            targetPosition.x - spawnPosition.x
        ) * Mathf.Rad2Deg;

        fishInstance.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        fishInstance.SetActive(true);

        if (fishInstance.TryGetComponent<FishScript>(out FishScript script))
        {
            script.ResetRuntimeSpawnState();
        }

        if (fishInstance.TryGetComponent<SpriteRenderer>(out SpriteRenderer renderer))
        {
            renderer.sortingOrder = orderLayer;
        }

        orderLayer += 2;

        if (orderLayer > 30000)
        {
            orderLayer = 0;
        }

        return fishInstance;
    }

    private int[] GetBossTargetsForLevel(GameLevel level)
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
                return level1BossTargets;

            case GameLevel.Level2_School:
                return level2BossTargets;

            case GameLevel.Level3_Circle:
                return level3BossTargets;

            case GameLevel.Level4_Cross:
                return level4BossTargets;

            case GameLevel.Level5_Festival:
                return level5BossTargets;

            case GameLevel.Level6_BossOcean:
                return level6BossTargets;

            default:
                return level1BossTargets;
        }
    }

    private int GetMiniBossIndexForLevel(GameLevel level)
    {
        switch (level)
        {
            case GameLevel.Level1_Beginner:
            case GameLevel.Level2_School:
                return miniBossStartIndex;

            case GameLevel.Level3_Circle:
                return Random.Range(
                    miniBossStartIndex,
                    Mathf.Min(miniBossStartIndex + 2, miniBossEndIndex + 1)
                );

            case GameLevel.Level4_Cross:
                return Mathf.Clamp(
                    miniBossStartIndex + 1,
                    miniBossStartIndex,
                    miniBossEndIndex
                );

            case GameLevel.Level5_Festival:
                return Random.Range(
                    Mathf.Clamp(
                        miniBossStartIndex + 1,
                        miniBossStartIndex,
                        miniBossEndIndex
                    ),
                    miniBossEndIndex + 1
                );

            case GameLevel.Level6_BossOcean:
                return miniBossEndIndex;

            default:
                return miniBossStartIndex;
        }
    }

    private int GetSmallFishIndex()
    {
        return Random.Range(
            smallFishStartIndex,
            smallFishEndIndex + 1
        );
    }

    private int GetMediumFishIndex()
    {
        bool hasExtraNormalFish =
            extraNormalFishStartIndex >= 0 &&
            extraNormalFishStartIndex < Fish.Length;

        if (hasExtraNormalFish && Random.value < 0.12f)
        {
            return Random.Range(
                extraNormalFishStartIndex,
                Fish.Length
            );
        }

        return Random.Range(
            mediumFishStartIndex,
            mediumFishEndIndex + 1
        );
    }

    private bool IsValidBigBossIndex(int index)
    {
        return index >= bigBossStartIndex &&
               index <= bigBossEndIndex &&
               index < Fish.Length;
    }

    private bool IsValidMiniBossIndex(int index)
    {
        return index >= miniBossStartIndex &&
               index <= miniBossEndIndex &&
               index < Fish.Length;
    }

    private GameLevel GetNextLevel(GameLevel level)
    {
        if (level == GameLevel.Level6_BossOcean)
        {
            return GameLevel.Level1_Beginner;
        }

        return (GameLevel)((int)level + 1);
    }

    private float GetWarmupDuration(GameLevel level)
    {
        return level1Warmup +
               (int)level * warmupIncreasePerLevel;
    }

    private float GetBossWarningDelay(GameLevel level)
    {
        return bossWarningBaseDelay +
               bossArrivalExtraBaseDelay +
               (int)level * (
                   bossWarningIncreasePerLevel +
                   bossArrivalExtraPerLevel
               );
    }

    private float GetBetweenBossDelay(GameLevel level)
    {
        return betweenTargetBossBaseDelay +
               (int)level * betweenTargetBossIncreasePerLevel;
    }

    private float GetPostBossRecoveryDelay(GameLevel level)
    {
        return postBossRecoveryBaseDelay +
               (int)level * postBossRecoveryIncreasePerLevel;
    }

    private float GetNextAmbientDelay()
    {
        float levelDelay = (int)currentLevelState * 0.22f;
        float phaseMultiplier =
            currentPhase == LevelPhase.BossBattle ? 1.8f : 1f;

        return Random.Range(
            ambientDelayMin,
            ambientDelayMax
        ) * phaseMultiplier + levelDelay;
    }

    private float GetNextParadeDelay()
    {
        float levelDelay = (int)currentLevelState * 1.1f;

        return Random.Range(
            paradeDelayMin,
            paradeDelayMax
        ) + levelDelay;
    }

    private float GetBossSupportDelay()
    {
        return Random.Range(10f, 14f) +
               (int)currentLevelState * 0.8f;
    }

    private int GetFormationGrowth()
    {
        int levelGrowth = (int)currentLevelState;
        int loopGrowth = Mathf.Clamp(loopMultiplier, 0, 4);
        return levelGrowth + loopGrowth;
    }

    private int GetMaxActiveFish()
    {
        return maxActiveFishBase +
               (int)currentLevelState * maxActiveFishPerLevel;
    }

    private int CountActiveFish()
    {
        int count = 0;

        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].activeSelf)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void BuildPoolDictionary()
    {
        fishPool.Clear();

        for (int i = 0; i < Fish.Length; i++)
        {
            fishPool[i] = new List<GameObject>();
        }
    }

    private void ForceDisableAllFish()
    {
        foreach (KeyValuePair<int, List<GameObject>> entry in fishPool)
        {
            List<GameObject> pool = entry.Value;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].activeSelf)
                {
                    pool[i].SetActive(false);
                }
            }
        }

        currentTargetBoss = null;
        currentTargetBossDefeated = false;
    }

    private void ClearSpawnedGimmicks()
    {
        for (int i = spawnedGimmicks.Count - 1; i >= 0; i--)
        {
            if (spawnedGimmicks[i] != null)
            {
                Destroy(spawnedGimmicks[i]);
            }
        }

        spawnedGimmicks.Clear();
    }

    private bool TryGetFishScript(
        GameObject fish,
        out FishScript script
    )
    {
        script = null;

        if (fish == null)
        {
            return false;
        }

        if (!fish.TryGetComponent<FishScript>(out script))
        {
            Debug.LogError(
                fish.name + " does not contain FishScript."
            );
            fish.SetActive(false);
            return false;
        }

        return true;
    }

    private bool ReferencesAreReady()
    {
        return FishSpawnPosition != null &&
               LeftPos != null &&
               RightPos != null;
    }

    private void FitSpriteToScreen(SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null ||
            targetRenderer.sprite == null ||
            Camera.main == null)
        {
            return;
        }

        float screenHeight = Camera.main.orthographicSize * 2f;
        float screenWidth = screenHeight * Screen.width / Screen.height;
        float spriteWidth = targetRenderer.sprite.bounds.size.x;
        float spriteHeight = targetRenderer.sprite.bounds.size.y;

        if (spriteWidth <= 0f || spriteHeight <= 0f)
        {
            return;
        }

        float scaleX = screenWidth / spriteWidth;
        float scaleY = screenHeight / spriteHeight;

        targetRenderer.transform.localScale = new Vector3(
            scaleX,
            scaleY,
            1f
        );
    }

    private void ValidateAndRepairConfiguration()
    {
        if (Fish == null || Fish.Length == 0)
        {
            Debug.LogError("SwapFishScript: Fish array is empty.");
            enabled = false;
            return;
        }

        bigBossStartIndex = Mathf.Clamp(
            bigBossStartIndex,
            0,
            Fish.Length - 1
        );

        bigBossEndIndex = Mathf.Clamp(
            bigBossEndIndex,
            bigBossStartIndex,
            Fish.Length - 1
        );

        smallFishStartIndex = Mathf.Clamp(
            smallFishStartIndex,
            0,
            Fish.Length - 1
        );

        smallFishEndIndex = Mathf.Clamp(
            smallFishEndIndex,
            smallFishStartIndex,
            Fish.Length - 1
        );

        mediumFishStartIndex = Mathf.Clamp(
            mediumFishStartIndex,
            0,
            Fish.Length - 1
        );

        mediumFishEndIndex = Mathf.Clamp(
            mediumFishEndIndex,
            mediumFishStartIndex,
            Fish.Length - 1
        );

        miniBossStartIndex = Mathf.Clamp(
            miniBossStartIndex,
            0,
            Fish.Length - 1
        );

        miniBossEndIndex = Mathf.Clamp(
            miniBossEndIndex,
            miniBossStartIndex,
            Fish.Length - 1
        );

        extraNormalFishStartIndex = Mathf.Clamp(
            extraNormalFishStartIndex,
            0,
            Fish.Length
        );

        level1BossTargets = RepairBossTargetArray(
            level1BossTargets,
            new int[] { 0 }
        );

        level2BossTargets = RepairBossTargetArray(
            level2BossTargets,
            new int[] { 1, 2 }
        );

        level3BossTargets = RepairBossTargetArray(
            level3BossTargets,
            new int[] { 3 }
        );

        level4BossTargets = RepairBossTargetArray(
            level4BossTargets,
            new int[] { 4, 5 }
        );

        level5BossTargets = RepairBossTargetArray(
            level5BossTargets,
            new int[] { 6 }
        );

        level6BossTargets = RepairBossTargetArray(
            level6BossTargets,
            new int[] { 7, 8 }
        );
    }

    private int[] RepairBossTargetArray(
        int[] source,
        int[] fallback
    )
    {
        if (source == null || source.Length < 1 || source.Length > 2)
        {
            source = fallback;
        }

        int safeLength = Mathf.Clamp(source.Length, 1, 2);
        int[] repaired = new int[safeLength];

        for (int i = 0; i < safeLength; i++)
        {
            repaired[i] = Mathf.Clamp(
                source[i],
                bigBossStartIndex,
                bigBossEndIndex
            );
        }

        return repaired;
    }
}
