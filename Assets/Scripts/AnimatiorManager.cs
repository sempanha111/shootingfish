using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

public class AnimatiorManager : MonoBehaviour
{
    [Header("Main Boss Death Skill In Front Gun")]
    public Transform animatorParent;

    [FormerlySerializedAs("Anima_object")]
    public GameObject[] mainBossFrontGunSkillPrefabs;

    [Tooltip("Seconds each skill remains visible. Use 0 to use the first Animator clip length. This is important for looping Animator states.")]
    public float[] mainBossFrontGunSkillVisibleDurations;

    [Min(0f)]
    public float mainBossFrontGunSkillHideDelay = 0.25f;

    [Header("Boss Coin Burst Pool")]
    [Tooltip("Multiple boss coin-burst prefabs. Each array index owns a separate object pool.")]
    public GameObject[] bossCoinBurstPrefabs;

    [Tooltip("Optional visible time per prefab index. Use 0 to detect the Animator or ParticleSystem duration automatically. Set a value for looping effects.")]
    public float[] bossCoinBurstVisibleDurations;

    [Min(0f)]
    [Tooltip("Extra time to keep the selected boss coin burst visible after its Animator or ParticleSystem finishes.")]
    public float bossCoinBurstHideDelay = 0.15f;

    [FormerlySerializedAs("partical")]
    [FormerlySerializedAs("bossCoinBurstPrefab")]
    [SerializeField, HideInInspector]
    private GameObject legacyBossCoinBurstPrefab;

    public Transform particleParent;
    public Transform[] GunPos;

    [FormerlySerializedAs("Coin")]
    [Tooltip("Coin object that moves from the dead main boss to the shooter's gun.")]
    public GameObject mainBossRewardCoinPrefab;

    [Header("Main Boss Coin-To-Gun Timing")]
    [Min(0f)] public float mainBossCoinStartDelay = 0.8f;
    [Min(0.01f)] public float mainBossCoinMoveSpeed = 50f;
    [Min(0f)] public float mainBossCoinArrivalDistance = 1f;
    [Min(0.1f)] public float mainBossCoinTravelTimeout = 1.5f;

    [Header("NetBoom Pool")]
    public GameObject[] netBoomPrefabs;
    [Min(0f)] public float netBoomHideDelay = 0.15f;

    // Hidden migration-only fields. They preserve old serialized data without
    // exposing obsolete Inspector lists.
    [FormerlySerializedAs("NetBoom")]
    [SerializeField, HideInInspector]
    private GameObject[] legacyNetBoomAssignments;

    [FormerlySerializedAs("CerificateText")]
    [SerializeField, HideInInspector]
    private GameObject[] legacyCertificateTextAssignments;

    [Header("Certificate Popup")]
    public CertificateTextManager certificateTextManager;

    [Header("Center-Screen Death Prefab Placement")]
    [Tooltip("Optional override. When empty, the active Main Camera is used for world-space celebration prefabs.")]
    public Camera centerScreenDeathCamera;

    [Tooltip("Optional Canvas for UI/RectTransform celebration prefabs. When empty, an active screen-space Canvas is found automatically.")]
    public Canvas centerScreenDeathCanvas;

    [Tooltip("Optional UI parent under Center Screen Death Canvas. Leave empty to use the Canvas root.")]
    public Transform centerScreenDeathCanvasParent;

    [Tooltip("Optional parent and Z plane for SpriteRenderer/ParticleSystem celebration prefabs.")]
    public Transform centerScreenDeathWorldParent;

    private sealed class GenericEffectPoolEntry
    {
        public GameObject prefab;
        public readonly List<GameObject> instances = new List<GameObject>();
    }

    private readonly List<GameObject> animationPool = new List<GameObject>();
    private readonly List<GameObject> coinPool = new List<GameObject>();
    private readonly List<List<GameObject>> bossCoinBurstPools =
        new List<List<GameObject>>();
    private readonly List<List<GameObject>> netBoomPools = new List<List<GameObject>>();
    private readonly List<GenericEffectPoolEntry> genericEffectPools =
        new List<GenericEffectPoolEntry>();
    private readonly List<GameObject> activeCenterScreenDeathEffects =
        new List<GameObject>();
    private readonly Dictionary<FishDeathProfile, int>
        lastCenterScreenDeathPrefabIndexes =
            new Dictionary<FishDeathProfile, int>();

    private int lastRandomBossCoinBurstIndex = -1;

    private void OnValidate()
    {
        MigrateLegacyBossCoinBurst();
    }

    private void Awake()
    {
        MigrateLegacyBossCoinBurst();

        if ((netBoomPrefabs == null || netBoomPrefabs.Length == 0) &&
            legacyNetBoomAssignments != null &&
            legacyNetBoomAssignments.Length > 0)
        {
            netBoomPrefabs = legacyNetBoomAssignments;
        }

        EnsureBossCoinBurstPools();
        EnsureNetBoomPools();
    }

    private void MigrateLegacyBossCoinBurst()
    {
        if ((bossCoinBurstPrefabs == null ||
             bossCoinBurstPrefabs.Length == 0) &&
            legacyBossCoinBurstPrefab != null)
        {
            bossCoinBurstPrefabs = new[]
            {
                legacyBossCoinBurstPrefab
            };
        }
    }

    private void EnsureBossCoinBurstPools()
    {
        MigrateLegacyBossCoinBurst();

        int count = bossCoinBurstPrefabs == null
            ? 0
            : bossCoinBurstPrefabs.Length;

        while (bossCoinBurstPools.Count < count)
        {
            bossCoinBurstPools.Add(new List<GameObject>());
        }
    }

    private bool IsValidBossCoinBurstIndex(int index)
    {
        return bossCoinBurstPrefabs != null &&
               index >= 0 &&
               index < bossCoinBurstPrefabs.Length &&
               bossCoinBurstPrefabs[index] != null;
    }

    private int ResolveBossCoinBurstIndex(int requestedIndex)
    {
        EnsureBossCoinBurstPools();

        if (requestedIndex >= 0 &&
            IsValidBossCoinBurstIndex(requestedIndex))
        {
            return requestedIndex;
        }

        if (bossCoinBurstPrefabs == null ||
            bossCoinBurstPrefabs.Length == 0)
        {
            return -1;
        }

        int validCount = 0;

        for (int i = 0; i < bossCoinBurstPrefabs.Length; i++)
        {
            if (bossCoinBurstPrefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return -1;
        }

        if (requestedIndex == -1)
        {
            int target = Random.Range(0, validCount);
            int selected = -1;

            for (int i = 0; i < bossCoinBurstPrefabs.Length; i++)
            {
                if (bossCoinBurstPrefabs[i] == null)
                {
                    continue;
                }

                if (target == 0)
                {
                    selected = i;
                    break;
                }

                target--;
            }

            if (validCount > 1 &&
                selected == lastRandomBossCoinBurstIndex)
            {
                for (int i = 1; i <= bossCoinBurstPrefabs.Length; i++)
                {
                    int candidate =
                        (selected + i) % bossCoinBurstPrefabs.Length;

                    if (bossCoinBurstPrefabs[candidate] != null)
                    {
                        selected = candidate;
                        break;
                    }
                }
            }

            lastRandomBossCoinBurstIndex = selected;
            return selected;
        }

        for (int i = 0; i < bossCoinBurstPrefabs.Length; i++)
        {
            if (bossCoinBurstPrefabs[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private float GetBossCoinBurstVisibleDuration(int effectIndex)
    {
        if (bossCoinBurstVisibleDurations == null ||
            effectIndex < 0 ||
            effectIndex >= bossCoinBurstVisibleDurations.Length)
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            bossCoinBurstVisibleDurations[effectIndex]
        );
    }

    private GameObject GetBossCoinBurstEffect(int effectIndex)
    {
        if (!IsValidBossCoinBurstIndex(effectIndex))
        {
            return null;
        }

        List<GameObject> pool = bossCoinBurstPools[effectIndex];
        GameObject inactive = GetInactive(pool);

        if (inactive != null)
        {
            return inactive;
        }

        GameObject prefab = bossCoinBurstPrefabs[effectIndex];
        Transform parent = particleParent != null
            ? particleParent
            : animatorParent != null
                ? animatorParent
                : transform;

        GameObject instance = Instantiate(prefab, parent);
        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.poolIndex = effectIndex;
        token.CaptureDefaultTransform(instance.transform);
        instance.SetActive(false);
        pool.Add(instance);
        return instance;
    }

    /// <summary>
    /// Compatibility overload. Uses boss coin-burst array index 0.
    /// </summary>
    public void PlayMainBossCoinBurst(Vector3 position)
    {
        PlayMainBossCoinBurst(position, 0);
    }

    /// <summary>
    /// Plays a pooled boss coin burst.
    /// effectIndex -1 selects a random valid prefab.
    /// Invalid positive indexes fall back to the first valid prefab.
    /// </summary>
    public void PlayMainBossCoinBurst(
        Vector3 position,
        int effectIndex
    )
    {
        int resolvedIndex =
            ResolveBossCoinBurstIndex(effectIndex);

        if (resolvedIndex < 0)
        {
            return;
        }

        GameObject effect =
            GetBossCoinBurstEffect(resolvedIndex);

        if (effect == null)
        {
            return;
        }

        PooledEffectToken token =
            effect.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = effect.AddComponent<PooledEffectToken>();
        }

        token.poolIndex = resolvedIndex;
        token.CaptureDefaultTransform(effect.transform);
        int version = ++token.playVersion;

        Transform parent = particleParent != null
            ? particleParent
            : animatorParent != null
                ? animatorParent
                : transform;

        effect.transform.SetParent(parent, false);
        token.RestoreDefaultTransform(effect.transform);
        effect.transform.position = position;
        ResetVisuals(effect);
        effect.SetActive(true);

        float animatorDuration = RestartAnimators(effect);
        float particleDuration = RestartParticleSystems(effect);
        float detectedDuration = Mathf.Max(
            0.1f,
            animatorDuration,
            particleDuration
        );

        float configuredDuration =
            GetBossCoinBurstVisibleDuration(resolvedIndex);

        float finalDuration = configuredDuration > 0f
            ? configuredDuration
            : detectedDuration;

        StartCoroutine(
            ReturnVersioned(
                effect,
                version,
                Mathf.Max(0.02f, finalDuration) +
                Mathf.Max(0f, bossCoinBurstHideDelay)
            )
        );
    }

    /// <summary>
    /// Sends a reward coin to the correct gun and plays the configured
    /// main-boss death skill in front of that gun. Missing reward-coin or
    /// GunPos references no longer silently cancel the skill: the method
    /// resolves a safe gun/death-position fallback and still plays the VFX.
    /// skillIndex -1 selects a random valid prefab.
    /// </summary>
    public void PlayMainBossDeathSkillInFrontGun(
        Vector3 deathPosition,
        int shooterId,
        int skillIndex = -1,
        float coinStartDelayOverride = -1f,
        float rewardAmount = 0f,
        bool playSkillSound = false,
        int skillSoundIndex = -1,
        bool showRewardText = false,
        string rewardTextPrefix = "+",
        float rewardTextDelay = 0f
    )
    {
        Vector3 fallbackSkillPosition = ResolveMainBossFrontGunTargetPosition(
            shooterId,
            deathPosition
        );


        GameObject coin = GetInactive(coinPool);

        if (coin == null && mainBossRewardCoinPrefab != null)
        {
            coin = Instantiate(
                mainBossRewardCoinPrefab,
                animatorParent
            );

            PooledEffectToken createdToken =
                coin.GetComponent<PooledEffectToken>();

            if (createdToken == null)
            {
                createdToken = coin.AddComponent<PooledEffectToken>();
            }

            createdToken.CaptureDefaultTransform(coin.transform);
            coinPool.Add(coin);
        }

        // The skill must never depend on the optional reward-coin prefab.
        if (coin == null)
        {
            StartCoroutine(
                PlayMainBossDeathSkill(
                    fallbackSkillPosition,
                    skillIndex,
                    rewardAmount,
                    playSkillSound,
                    skillSoundIndex,
                    showRewardText,
                    rewardTextPrefix,
                    rewardTextDelay
                )
            );
            return;
        }

        PooledEffectToken token = coin.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = coin.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(coin.transform);
        int version = ++token.playVersion;

        token.RestoreDefaultTransform(coin.transform);
        coin.transform.position = deathPosition;
        ResetVisuals(coin);
        coin.SetActive(true);

        StartCoroutine(
            MoveMainBossCoinToGun(
                coin,
                version,
                shooterId,
                skillIndex,
                coinStartDelayOverride,
                rewardAmount,
                playSkillSound,
                skillSoundIndex,
                showRewardText,
                rewardTextPrefix,
                rewardTextDelay
            )
        );
    }

    public void PlayNetBoom(Vector3 pos, int bulletId, int fishId)
    {
        PlayNetBoom(pos, bulletId, fishId, 0);
    }

    public void PlayNetBoom(
        Vector3 pos,
        int bulletId,
        int fishId,
        int effectIndex
    )
    {
        PlayNetBoomAndGetDuration(
            pos,
            bulletId,
            fishId,
            effectIndex
        );
    }

    /// <summary>
    /// Plays a pooled Net Boom and returns its detected visible lifetime.
    /// The cinematic death controller uses this to begin its next stage
    /// only after the selected Net Boom animation has completed.
    /// </summary>
    public float PlayNetBoomAndGetDuration(
        Vector3 pos,
        int bulletId,
        int fishId,
        int effectIndex
    )
    {
        return PlayNetBoomAdvancedAndGetDuration(
            pos,
            bulletId,
            fishId,
            effectIndex,
            Vector3.one,
            int.MinValue,
            int.MinValue
        );
    }

    /// <summary>
    /// Plays a pooled Net Boom with temporary scale and sorting overrides.
    /// The defaults are restored automatically before the pooled object is
    /// reused, so one Armored Crab play cannot affect another fish.
    /// </summary>
    public float PlayNetBoomAdvancedAndGetDuration(
        Vector3 pos,
        int bulletId,
        int fishId,
        int effectIndex,
        Vector3 scaleMultiplier,
        int sortingLayerId = int.MinValue,
        int sortingOrder = int.MinValue
    )
    {
        EnsureNetBoomPools();

        if (!IsValidNetBoomIndex(effectIndex))
        {
            effectIndex = GetFirstValidNetBoomIndex();

            if (effectIndex < 0)
            {
                return 0f;
            }
        }

        GameObject effect = GetNetBoom(effectIndex);

        if (effect == null)
        {
            return 0f;
        }

        PooledEffectToken token = effect.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = effect.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(effect.transform);
        token.CaptureDefaultSorting(effect);
        token.poolIndex = effectIndex;
        int version = ++token.playVersion;

        effect.transform.SetParent(animatorParent, false);
        token.RestoreDefaultTransform(effect.transform);
        token.RestoreDefaultSorting(effect);
        effect.transform.position = pos;
        effect.transform.localScale = Vector3.Scale(
            effect.transform.localScale,
            new Vector3(
                Mathf.Max(0.01f, scaleMultiplier.x),
                Mathf.Max(0.01f, scaleMultiplier.y),
                Mathf.Max(0.01f, scaleMultiplier.z)
            )
        );
        ApplyTemporarySorting(
            effect,
            sortingLayerId,
            sortingOrder
        );
        ResetVisuals(effect);
        effect.SetActive(true);

        float duration = RestartAnimators(effect);
        float particleDuration = RestartParticleSystems(effect);
        float visibleDuration = Mathf.Max(
            0.01f,
            Mathf.Max(duration, particleDuration) + netBoomHideDelay
        );

        StartCoroutine(
            ReturnVersioned(
                effect,
                version,
                visibleDuration
            )
        );

        return visibleDuration;
    }

    public void PlayNetBoomAdvanced(
        Vector3 pos,
        int bulletId,
        int fishId,
        int effectIndex,
        Vector3 scaleMultiplier,
        int sortingLayerId = int.MinValue,
        int sortingOrder = int.MinValue
    )
    {
        PlayNetBoomAdvancedAndGetDuration(
            pos,
            bulletId,
            fishId,
            effectIndex,
            scaleMultiplier,
            sortingLayerId,
            sortingOrder
        );
    }

    public void PlayCerificateText(Vector3 pos, int bulletId, int fishId)
    {
        PlayCertificateText(pos);
    }

    public void PlayCertificateText(Vector3 pos)
    {
        PlayCertificateText(pos, -1);
    }

    /// <summary>
    /// Plays an exact pooled certificate when prefabIndex is valid.
    /// Negative values keep the existing random-certificate behavior.
    /// </summary>
    public void PlayCertificateText(Vector3 pos, int prefabIndex)
    {
        if (certificateTextManager == null)
        {
            return;
        }

        if (prefabIndex >= 0)
        {
            certificateTextManager.Play(prefabIndex, pos);
        }
        else
        {
            certificateTextManager.PlayRandom(pos);
        }
    }

    private GameObject GetNetBoom(int index)
    {
        List<GameObject> pool = netBoomPools[index];
        GameObject inactive = GetInactive(pool);

        if (inactive != null)
        {
            return inactive;
        }

        GameObject prefab = netBoomPrefabs[index];

        if (prefab == null)
        {
            return null;
        }

        GameObject effect = Instantiate(prefab, animatorParent);
        PooledEffectToken token = effect.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = effect.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(effect.transform);
        effect.SetActive(false);
        pool.Add(effect);
        return effect;
    }

    private IEnumerator ReturnVersioned(
        GameObject effect,
        int version,
        float duration
    )
    {
        yield return new WaitForSeconds(Mathf.Max(0.02f, duration));

        if (effect == null)
        {
            yield break;
        }

        PooledEffectToken token = effect.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            effect.SetActive(false);
        }
    }

    private IEnumerator MoveMainBossCoinToGun(
        GameObject coin,
        int coinVersion,
        int shooterId,
        int skillIndex,
        float coinStartDelayOverride,
        float rewardAmount,
        bool playSkillSound,
        int skillSoundIndex,
        bool showRewardText,
        string rewardTextPrefix,
        float rewardTextDelay
    )
    {
        float startDelay = coinStartDelayOverride >= 0f
            ? coinStartDelayOverride
            : mainBossCoinStartDelay;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (coin == null || !IsCurrentPlay(coin, coinVersion))
        {
            yield break;
        }

        Vector3 targetPosition = ResolveMainBossFrontGunTargetPosition(
            shooterId,
            coin.transform.position
        );

        float initialDistance = Mathf.Max(
            0.01f,
            Vector2.Distance(coin.transform.position, targetPosition)
        );

        float distance = initialDistance;
        float travelElapsed = 0f;

        while (coin.activeSelf &&
               IsCurrentPlay(coin, coinVersion) &&
               distance > mainBossCoinArrivalDistance &&
               travelElapsed < Mathf.Max(0.1f, mainBossCoinTravelTimeout))
        {
            travelElapsed += Time.deltaTime;
            float distanceFactor = distance / initialDistance;

            coin.transform.position = Vector2.MoveTowards(
                coin.transform.position,
                targetPosition,
                Time.deltaTime *
                mainBossCoinMoveSpeed *
                Mathf.Max(0.15f, distanceFactor)
            );

            distance = Vector2.Distance(
                coin.transform.position,
                targetPosition
            );

            yield return null;
        }

        if (!IsCurrentPlay(coin, coinVersion))
        {
            yield break;
        }

        Vector3 skillPosition = targetPosition;
        coin.SetActive(false);
        StartCoroutine(
            PlayMainBossDeathSkill(
                skillPosition,
                skillIndex,
                rewardAmount,
                playSkillSound,
                skillSoundIndex,
                showRewardText,
                rewardTextPrefix,
                rewardTextDelay
            )
        );
    }

    /// <summary>
    /// Plays one pooled main-boss skill. A configured visible duration
    /// overrides clip length, allowing looping Animator states to be hidden.
    /// </summary>
    public IEnumerator PlayMainBossDeathSkill(
        Vector3 position,
        int skillIndex = -1,
        float rewardAmount = 0f,
        bool playSkillSound = false,
        int skillSoundIndex = -1,
        bool showRewardText = false,
        string rewardTextPrefix = "+",
        float rewardTextDelay = 0f
    )
    {
        int resolvedIndex = ResolveMainBossSkillIndex(skillIndex);

        if (resolvedIndex < 0)
        {
            Debug.LogWarning(
                "[Main Boss Front Gun] No valid skill prefab is assigned in " +
                "AnimatiorManager.mainBossFrontGunSkillPrefabs.",
                this
            );
            yield break;
        }

        string poolName = "MainBossFrontGunSkill_" + resolvedIndex;
        GameObject clone = null;

        for (int i = 0; i < animationPool.Count; i++)
        {
            GameObject candidate = animationPool[i];

            if (candidate != null &&
                !candidate.activeSelf &&
                candidate.name == poolName)
            {
                clone = candidate;
                break;
            }
        }

        if (clone == null)
        {
            clone = Instantiate(
                mainBossFrontGunSkillPrefabs[resolvedIndex],
                animatorParent
            );

            clone.name = poolName;

            PooledEffectToken newToken =
                clone.GetComponent<PooledEffectToken>();

            if (newToken == null)
            {
                newToken = clone.AddComponent<PooledEffectToken>();
            }

            newToken.CaptureDefaultTransform(clone.transform);
            animationPool.Add(clone);
        }

        PooledEffectToken token = clone.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = clone.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(clone.transform);
        int version = ++token.playVersion;

        token.RestoreDefaultTransform(clone.transform);
        clone.transform.position = position;
        ResetVisuals(clone);
        clone.SetActive(true);


        float clipDuration = RestartAnimators(clone);

        GameManager manager = GameManager.Instance;

        if (playSkillSound && manager != null && manager.SoundManager != null)
        {
            manager.SoundManager.PlayMainBossFrontGunSkillSound(
                skillSoundIndex
            );
        }

        if (showRewardText && rewardAmount > 0f)
        {
            TryPlayEmbeddedFrontGunRewardText(
                clone,
                rewardAmount,
                rewardTextPrefix,
                rewardTextDelay
            );
        }

        float configuredDuration = GetMainBossSkillVisibleDuration(resolvedIndex);
        float visibleDuration = configuredDuration > 0f
            ? configuredDuration
            : clipDuration;

        yield return new WaitForSeconds(
            Mathf.Max(0.02f, visibleDuration) +
            mainBossFrontGunSkillHideDelay
        );

        if (clone != null && token.playVersion == version)
        {
            clone.SetActive(false);
        }
    }




    private bool TryPlayEmbeddedFrontGunRewardText(
        GameObject skillClone,
        float rewardAmount,
        string prefix,
        float delay
    )
    {
        if (skillClone == null || rewardAmount <= 0f)
        {
            return false;
        }

        FrontGunRewardPresentation presentation =
            skillClone.GetComponent<FrontGunRewardPresentation>();

        if (presentation == null)
        {
            presentation =
                skillClone.GetComponentInChildren<FrontGunRewardPresentation>(true);
        }

        if (presentation != null &&
            presentation.PlayReward(
                rewardAmount,
                prefix ?? string.Empty,
                Mathf.Max(0f, delay)
            ))
        {
            return true;
        }

        // Compatibility: if the prefab already contains the counter component
        // but not the presentation wrapper, use it directly.
        FrontGunRewardCounterText counter =
            skillClone.GetComponentInChildren<FrontGunRewardCounterText>(true);

        if (counter != null)
        {
            counter.SetVisible(true);
            counter.Play(
                rewardAmount,
                prefix ?? string.Empty,
                Mathf.Max(0f, delay)
            );

            return true;
        }

        Debug.LogWarning(
            "[Main Boss Front Gun] No embedded RewardText found in " +
            skillClone.name + ". Add FrontGunRewardPresentation or " +
            "FrontGunRewardCounterText inside the SkillFrontGun prefab.",
            skillClone
        );

        return false;
    }


    private Vector3 ResolveMainBossFrontGunTargetPosition(
        int shooterId,
        Vector3 fallbackPosition
    )
    {
        Transform target = null;

        if (GunPos != null &&
            shooterId >= 0 &&
            shooterId < GunPos.Length)
        {
            target = GunPos[shooterId];
        }

        GameManager manager = GameManager.Instance;

        if (target == null && manager != null)
        {
            if (shooterId == 0 &&
                manager.weaponsScripts != null &&
                manager.weaponsScripts.activeGun != null)
            {
                target = manager.weaponsScripts.activeGun.transform;
            }
            else if (shooterId > 0)
            {
                WeaponNPC[] npcWeapons = FindObjectsOfType<WeaponNPC>();

                for (int i = 0; i < npcWeapons.Length; i++)
                {
                    WeaponNPC npc = npcWeapons[i];

                    if (npc != null && npc.ShooterId == shooterId)
                    {
                        target = npc.ActiveGunTransform;
                        break;
                    }
                }
            }
        }

        if (target == null)
        {
            Debug.LogWarning(
                "[Main Boss Front Gun] GunPos for shooter " + shooterId +
                " is not assigned. Playing the skill at the safe fallback " +
                "position instead of cancelling it.",
                this
            );
            return fallbackPosition;
        }

        Vector3 gunOffset =
            shooterId == 0 || shooterId == 1
                ? new Vector3(0f, 2f, 0f)
                : new Vector3(0f, -2f, 0f);

        return target.position + gunOffset;
    }


    [ContextMenu("Validate Main Boss Front-Gun Skill Setup")]
    private void ValidateMainBossFrontGunSkillSetup()
    {
        int validSkills = 0;

        if (mainBossFrontGunSkillPrefabs != null)
        {
            for (int i = 0; i < mainBossFrontGunSkillPrefabs.Length; i++)
            {
                if (mainBossFrontGunSkillPrefabs[i] != null)
                {
                    validSkills++;
                }
            }
        }

        Debug.Log(
            "[Main Boss Front Gun] Valid skill prefabs=" + validSkills +
            ", reward coin=" +
            (mainBossRewardCoinPrefab != null ? "assigned" : "missing (direct-play fallback enabled)") +
            ", GunPos slots=" + (GunPos != null ? GunPos.Length : 0) +
            ".",
            this
        );
    }

    private int ResolveMainBossSkillIndex(int requestedIndex)
    {
        if (mainBossFrontGunSkillPrefabs == null ||
            mainBossFrontGunSkillPrefabs.Length == 0)
        {
            return -1;
        }

        if (requestedIndex >= 0 &&
            requestedIndex < mainBossFrontGunSkillPrefabs.Length &&
            mainBossFrontGunSkillPrefabs[requestedIndex] != null)
        {
            return requestedIndex;
        }

        int validCount = 0;

        for (int i = 0; i < mainBossFrontGunSkillPrefabs.Length; i++)
        {
            if (mainBossFrontGunSkillPrefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return -1;
        }

        int selectedValid = Random.Range(0, validCount);

        for (int i = 0; i < mainBossFrontGunSkillPrefabs.Length; i++)
        {
            if (mainBossFrontGunSkillPrefabs[i] == null)
            {
                continue;
            }

            if (selectedValid == 0)
            {
                return i;
            }

            selectedValid--;
        }

        return -1;
    }

    private float GetMainBossSkillVisibleDuration(int skillIndex)
    {
        if (mainBossFrontGunSkillVisibleDurations == null ||
            skillIndex < 0 ||
            skillIndex >= mainBossFrontGunSkillVisibleDurations.Length)
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            mainBossFrontGunSkillVisibleDurations[skillIndex]
        );
    }

    private static bool IsCurrentPlay(GameObject target, int version)
    {
        if (target == null)
        {
            return false;
        }

        PooledEffectToken token = target.GetComponent<PooledEffectToken>();
        return token != null && token.playVersion == version;
    }

    private static void DisableIfCurrent(GameObject target, int version)
    {
        if (IsCurrentPlay(target, version))
        {
            target.SetActive(false);
        }
    }

    private static GameObject GetInactive(List<GameObject> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeSelf)
            {
                return pool[i];
            }
        }

        return null;
    }

    private void EnsureNetBoomPools()
    {
        int count = netBoomPrefabs == null ? 0 : netBoomPrefabs.Length;

        while (netBoomPools.Count < count)
        {
            netBoomPools.Add(new List<GameObject>());
        }
    }

    private bool IsValidNetBoomIndex(int index)
    {
        return netBoomPrefabs != null &&
               index >= 0 &&
               index < netBoomPrefabs.Length &&
               netBoomPrefabs[index] != null;
    }

    private int GetFirstValidNetBoomIndex()
    {
        if (netBoomPrefabs == null)
        {
            return -1;
        }

        for (int i = 0; i < netBoomPrefabs.Length; i++)
        {
            if (netBoomPrefabs[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private static float RestartAnimators(GameObject root)
    {
        float longestDuration = 0.1f;
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, 0f);
            animator.Update(0f);

            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);

            if (clips.Length > 0 && clips[0].clip != null)
            {
                longestDuration = Mathf.Max(
                    longestDuration,
                    clips[0].clip.length /
                    Mathf.Max(0.01f, animator.speed)
                );
            }
        }

        return longestDuration;
    }

    private static void ResetVisuals(GameObject root)
    {
        CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);

        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].alpha = 1f;
        }

        SpriteRenderer[] sprites =
            root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < sprites.Length; i++)
        {
            Color color = sprites[i].color;
            color.a = 1f;
            sprites[i].color = color;
        }
    }

    private static float RestartParticleSystems(GameObject root)
    {
        float longestDuration = 0f;
        ParticleSystem[] particles =
            root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];

            if (particle == null)
            {
                continue;
            }

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particle.Play(true);

            ParticleSystem.MainModule main = particle.main;
            float duration = main.loop
                ? main.startLifetime.constantMax
                : main.duration + main.startLifetime.constantMax;

            longestDuration = Mathf.Max(
                longestDuration,
                duration
            );
        }

        return longestDuration;
    }

    /// <summary>
    /// Estimates how long a pooled effect will remain visibly active without
    /// spawning it. Death profiles use this to delay late zoom/fade phases
    /// until a convulsion/reward prefab has finished. A configured duration
    /// always wins; otherwise Animator clips, legacy Animation clips, and
    /// non-looping ParticleSystems are inspected on the prefab asset.
    /// </summary>
    public float EstimatePooledEffectVisibleDuration(
        GameObject prefab,
        float configuredDuration = 0f
    )
    {
        if (configuredDuration > 0f)
        {
            return configuredDuration;
        }

        if (prefab == null)
        {
            return 0f;
        }

        float animatorDuration = EstimateAnimatorDuration(prefab);
        float animationDuration = EstimateLegacyAnimationDuration(prefab);
        float particleDuration = EstimateParticleDuration(prefab);

        return Mathf.Max(
            0.1f,
            animatorDuration,
            animationDuration,
            particleDuration
        );
    }

    private static float EstimateAnimatorDuration(GameObject root)
    {
        if (root == null)
        {
            return 0f;
        }

        float longest = 0f;
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                continue;
            }

            AnimationClip[] clips =
                animator.runtimeAnimatorController.animationClips;

            if (clips == null)
            {
                continue;
            }

            for (int c = 0; c < clips.Length; c++)
            {
                AnimationClip clip = clips[c];

                if (clip != null)
                {
                    longest = Mathf.Max(longest, clip.length);
                }
            }
        }

        return longest;
    }

    private static float EstimateLegacyAnimationDuration(GameObject root)
    {
        if (root == null)
        {
            return 0f;
        }

        float longest = 0f;
        Animation[] animations =
            root.GetComponentsInChildren<Animation>(true);

        for (int i = 0; i < animations.Length; i++)
        {
            Animation animation = animations[i];

            if (animation == null)
            {
                continue;
            }

            foreach (AnimationState state in animation)
            {
                if (state != null && state.clip != null)
                {
                    longest = Mathf.Max(longest, state.clip.length);
                }
            }
        }

        return longest;
    }

    private static float EstimateParticleDuration(GameObject root)
    {
        if (root == null)
        {
            return 0f;
        }

        float longest = 0f;
        ParticleSystem[] particles =
            root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];

            if (particle == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particle.main;
            float startDelay = main.startDelay.constantMax;
            float lifetime = main.startLifetime.constantMax;

            // Looping systems cannot be inferred safely from the prefab.
            // The authored configured duration should be used for those.
            float duration = main.loop
                ? Mathf.Max(0.1f, lifetime)
                : startDelay + main.duration + lifetime;

            longest = Mathf.Max(longest, duration);
        }

        return longest;
    }

    /// <summary>
    /// Plays any optional boss or death VFX through a prefab-specific pool.
    /// Animator and ParticleSystem children are restarted from time zero.
    /// A play-version token prevents an older coroutine from hiding a reused
    /// instance. visibleDuration 0 uses the longest detected clip/particle.
    /// </summary>
    public void PlayPooledEffect(
        GameObject prefab,
        Vector3 position,
        float visibleDuration = 0f,
        float hideDelay = 0.15f
    )
    {
        PlayPooledEffectAdvanced(
            prefab,
            position,
            0f,
            Vector3.one,
            visibleDuration,
            hideDelay,
            int.MinValue
        );
    }

    /// <summary>
    /// Reuses the existing generic effect pool while allowing a cinematic
    /// caller to override rotation, scale, and sorting order for this play.
    /// Existing callers keep using PlayPooledEffect unchanged.
    /// </summary>
    public GameObject PlayPooledEffectAdvanced(
        GameObject prefab,
        Vector3 position,
        float zRotation,
        Vector3 scaleMultiplier,
        float visibleDuration = 0f,
        float hideDelay = 0.15f,
        int sortingOrder = int.MinValue,
        int sortingLayerId = int.MinValue
    )
    {
        if (prefab == null)
        {
            return null;
        }

        Transform effectParent = animatorParent != null
            ? animatorParent
            : transform;
        GameObject instance = GetGenericPooledEffect(
            prefab,
            effectParent
        );

        if (instance == null)
        {
            return null;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        token.CaptureDefaultSorting(instance);
        int version = ++token.playVersion;

        instance.transform.SetParent(
            effectParent,
            false
        );
        token.RestoreDefaultTransform(instance.transform);
        token.RestoreDefaultSorting(instance);
        instance.transform.position = position;
        instance.transform.rotation =
            instance.transform.rotation *
            Quaternion.Euler(0f, 0f, zRotation);
        instance.transform.localScale = Vector3.Scale(
            instance.transform.localScale,
            scaleMultiplier
        );

        ApplyTemporarySorting(instance, sortingLayerId, sortingOrder);
        ResetVisuals(instance);
        instance.SetActive(true);

        float animatorDuration = RestartAnimators(instance);
        float particleDuration = RestartParticleSystems(instance);
        float detectedDuration = Mathf.Max(
            0.1f,
            animatorDuration,
            particleDuration
        );
        float finalDuration = visibleDuration > 0f
            ? visibleDuration
            : detectedDuration;

        StartCoroutine(
            ReturnVersioned(
                instance,
                version,
                Mathf.Max(0.02f, finalDuration) +
                Mathf.Max(0f, hideDelay)
            )
        );

        return instance;
    }

    /// <summary>
    /// Starts a pooled effect without scheduling an automatic return.
    /// The caller owns its lifetime and must call StopPersistentPooledEffect.
    /// This is used by long-running cinematic background layers that need to
    /// remain active while several fish-animation stages are playing.
    /// </summary>
    public GameObject PlayPersistentPooledEffectAdvanced(
        GameObject prefab,
        Vector3 position,
        float zRotation,
        Vector3 scaleMultiplier,
        int sortingOrder = int.MinValue,
        int sortingLayerId = int.MinValue
    )
    {
        if (prefab == null)
        {
            return null;
        }

        Transform effectParent = animatorParent != null
            ? animatorParent
            : transform;
        GameObject instance = GetGenericPooledEffect(
            prefab,
            effectParent
        );

        if (instance == null)
        {
            return null;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        token.CaptureDefaultSorting(instance);

        // Incrementing the version invalidates any stale return coroutine from
        // an older use of this pooled instance.
        ++token.playVersion;

        instance.transform.SetParent(
            effectParent,
            false
        );
        token.RestoreDefaultTransform(instance.transform);
        token.RestoreDefaultSorting(instance);
        instance.transform.position = position;
        instance.transform.rotation =
            instance.transform.rotation *
            Quaternion.Euler(0f, 0f, zRotation);
        instance.transform.localScale = Vector3.Scale(
            instance.transform.localScale,
            scaleMultiplier
        );

        ApplyTemporarySorting(instance, sortingLayerId, sortingOrder);
        ResetVisuals(instance);
        instance.SetActive(true);
        RestartAnimators(instance);
        RestartParticleSystems(instance);
        return instance;
    }

    /// <summary>
    /// Returns an explicitly persistent pooled effect to the normal inactive
    /// state. Incrementing the play token prevents any stale coroutine from a
    /// previous play from affecting the next reuse.
    /// </summary>
    public void StopPersistentPooledEffect(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token != null)
        {
            ++token.playVersion;
        }

        instance.SetActive(false);
    }

    private static void ApplyTemporarySorting(
        GameObject instance,
        int sortingLayerId,
        int sortingOrder
    )
    {
        if (instance == null ||
            (sortingLayerId == int.MinValue &&
             sortingOrder == int.MinValue))
        {
            return;
        }

        SortingGroup[] groups =
            instance.GetComponentsInChildren<SortingGroup>(true);

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
            {
                continue;
            }

            if (sortingLayerId != int.MinValue)
            {
                groups[i].sortingLayerID = sortingLayerId;
            }

            if (sortingOrder != int.MinValue)
            {
                groups[i].sortingOrder = sortingOrder + i;
            }
        }

        Renderer[] renderers =
            instance.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (sortingLayerId != int.MinValue)
            {
                renderers[i].sortingLayerID = sortingLayerId;
            }

            if (sortingOrder != int.MinValue)
            {
                renderers[i].sortingOrder = sortingOrder + i;
            }
        }

        Canvas[] canvases =
            instance.GetComponentsInChildren<Canvas>(true);

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
            {
                continue;
            }

            canvases[i].overrideSorting = true;

            if (sortingLayerId != int.MinValue)
            {
                canvases[i].sortingLayerID = sortingLayerId;
            }

            if (sortingOrder != int.MinValue)
            {
                canvases[i].sortingOrder = sortingOrder + i;
            }
        }
    }

    private GenericEffectPoolEntry GetGenericEffectPool(GameObject prefab)
    {
        for (int i = 0; i < genericEffectPools.Count; i++)
        {
            GenericEffectPoolEntry existing = genericEffectPools[i];

            if (existing != null && existing.prefab == prefab)
            {
                return existing;
            }
        }

        GenericEffectPoolEntry created = new GenericEffectPoolEntry();
        created.prefab = prefab;
        genericEffectPools.Add(created);
        return created;
    }

    private GameObject GetGenericPooledEffect(
        GameObject prefab,
        Transform parent
    )
    {
        if (prefab == null)
        {
            return null;
        }

        GenericEffectPoolEntry entry = GetGenericEffectPool(prefab);
        GameObject instance = GetInactive(entry.instances);

        if (instance != null)
        {
            return instance;
        }

        instance = Instantiate(
            prefab,
            parent != null ? parent : transform
        );

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        instance.SetActive(false);
        entry.instances.Add(instance);
        return instance;
    }

    /// <summary>
    /// Plays one profile-selected death celebration at a stable viewport
    /// position. UI and world prefabs share the normal prefab-specific pool.
    /// Returns false when the profile, chance, reward gate, selection, or
    /// overlap policy prevents a play.
    /// </summary>
    public bool PlayCenterScreenDeathPrefab(
        FishDeathProfile profile,
        float rewardAmount
    )
    {
        if (profile == null ||
            !profile.playCenterScreenDeathPrefab ||
            rewardAmount <
                Mathf.Max(
                    0f,
                    profile.minimumCenterScreenDeathPrefabReward
                ))
        {
            return false;
        }

        float chance = Mathf.Clamp01(
            profile.centerScreenDeathPrefabChance
        );

        if (chance <= 0f ||
            (chance < 1f && Random.value > chance))
        {
            return false;
        }

        CleanActiveCenterScreenDeathEffects();

        if (profile.centerScreenDeathPrefabOverlap ==
                CenterScreenDeathPrefabOverlapMode.IgnoreWhileVisible &&
            activeCenterScreenDeathEffects.Count > 0)
        {
            return false;
        }

        if (profile.centerScreenDeathPrefabOverlap ==
            CenterScreenDeathPrefabOverlapMode.ReplaceCurrent)
        {
            StopActiveCenterScreenDeathEffects();
        }

        int prefabIndex = ResolveCenterScreenDeathPrefabIndex(profile);

        if (prefabIndex < 0)
        {
            return false;
        }

        GameObject prefab =
            profile.centerScreenDeathPrefabs[prefabIndex];
        bool useCanvas = ShouldUseCanvasForCenterScreenDeath(
            profile,
            prefab
        );
        Canvas targetCanvas = useCanvas
            ? ResolveCenterScreenDeathCanvas()
            : null;
        bool prefabHasOwnCanvas =
            prefab.GetComponentInChildren<Canvas>(true) != null;

        if (useCanvas && targetCanvas == null && !prefabHasOwnCanvas)
        {
            useCanvas = false;
        }

        Transform parent = useCanvas
            ? ResolveCenterScreenDeathCanvasParent(targetCanvas)
            : ResolveCenterScreenDeathWorldParent();
        GameObject instance = GetGenericPooledEffect(prefab, parent);

        if (instance == null)
        {
            return false;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        token.CaptureDefaultTransform(instance.transform);
        int version = ++token.playVersion;

        instance.transform.SetParent(parent, false);
        token.RestoreDefaultTransform(instance.transform);
        ResetVisuals(instance);
        instance.SetActive(true);
        DisableCenterScreenDeathInteraction(instance, useCanvas);

        float animatorDuration = RestartAnimators(instance);
        float particleDuration = RestartParticleSystems(instance);

        ApplyCenterScreenDeathPlacement(
            instance,
            token,
            profile,
            useCanvas,
            targetCanvas
        );

        if (!activeCenterScreenDeathEffects.Contains(instance))
        {
            activeCenterScreenDeathEffects.Add(instance);
        }

        float detectedDuration = Mathf.Max(
            0.1f,
            animatorDuration,
            particleDuration
        );
        float configuredDuration = Mathf.Max(
            0f,
            profile.centerScreenDeathVisibleDuration
        );
        float visibleDuration = configuredDuration > 0f
            ? configuredDuration
            : detectedDuration;

        StartCoroutine(
            ReturnCenterScreenDeathVersioned(
                instance,
                version,
                Mathf.Max(0.02f, visibleDuration) +
                    Mathf.Max(
                        0f,
                        profile.centerScreenDeathHideDelay
                    ),
                profile.centerScreenDeathUseUnscaledTime
            )
        );

        return true;
    }

    private int ResolveCenterScreenDeathPrefabIndex(
        FishDeathProfile profile
    )
    {
        GameObject[] prefabs = profile.centerScreenDeathPrefabs;

        if (prefabs == null || prefabs.Length == 0)
        {
            return -1;
        }

        if (profile.centerScreenDeathPrefabSelection ==
            CenterScreenDeathPrefabSelectionMode.ExactIndex)
        {
            int exactIndex = profile.centerScreenDeathPrefabIndex;
            return exactIndex >= 0 &&
                   exactIndex < prefabs.Length &&
                   prefabs[exactIndex] != null
                ? exactIndex
                : -1;
        }

        int validCount = 0;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return -1;
        }

        int selectedValidIndex = Random.Range(0, validCount);
        int selectedIndex = -1;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
            {
                continue;
            }

            if (selectedValidIndex == 0)
            {
                selectedIndex = i;
                break;
            }

            selectedValidIndex--;
        }

        int lastIndex;
        bool avoidRepeat =
            profile.centerScreenDeathPrefabSelection ==
                CenterScreenDeathPrefabSelectionMode.RandomNoRepeat &&
            validCount > 1 &&
            lastCenterScreenDeathPrefabIndexes.TryGetValue(
                profile,
                out lastIndex
            ) &&
            selectedIndex == lastIndex;

        if (avoidRepeat)
        {
            for (int offset = 1; offset <= prefabs.Length; offset++)
            {
                int candidate =
                    (selectedIndex + offset) % prefabs.Length;

                if (prefabs[candidate] != null)
                {
                    selectedIndex = candidate;
                    break;
                }
            }
        }

        if (selectedIndex >= 0)
        {
            lastCenterScreenDeathPrefabIndexes[profile] = selectedIndex;
        }

        return selectedIndex;
    }

    private static bool ShouldUseCanvasForCenterScreenDeath(
        FishDeathProfile profile,
        GameObject prefab
    )
    {
        if (profile.centerScreenDeathPrefabSpace ==
            CenterScreenDeathPrefabSpace.Canvas)
        {
            return true;
        }

        if (profile.centerScreenDeathPrefabSpace ==
            CenterScreenDeathPrefabSpace.World)
        {
            return false;
        }

        return prefab != null &&
               (prefab.GetComponentInChildren<Canvas>(true) != null ||
                prefab.GetComponentInChildren<RectTransform>(true) != null);
    }

    private Canvas ResolveCenterScreenDeathCanvas()
    {
        if (centerScreenDeathCanvas != null)
        {
            return centerScreenDeathCanvas;
        }

        if (centerScreenDeathCanvasParent != null)
        {
            centerScreenDeathCanvas =
                centerScreenDeathCanvasParent.GetComponentInParent<Canvas>();

            if (centerScreenDeathCanvas != null)
            {
                return centerScreenDeathCanvas;
            }
        }

        if (animatorParent != null)
        {
            centerScreenDeathCanvas =
                animatorParent.GetComponentInParent<Canvas>();

            if (centerScreenDeathCanvas != null)
            {
                return centerScreenDeathCanvas;
            }
        }

        Canvas fallback = null;
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];

            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (candidate.renderMode ==
                RenderMode.ScreenSpaceOverlay)
            {
                centerScreenDeathCanvas = candidate;
                return centerScreenDeathCanvas;
            }

            if (fallback == null &&
                candidate.renderMode ==
                    RenderMode.ScreenSpaceCamera)
            {
                fallback = candidate;
            }
        }

        centerScreenDeathCanvas = fallback;
        return centerScreenDeathCanvas;
    }

    private Transform ResolveCenterScreenDeathCanvasParent(Canvas canvas)
    {
        if (centerScreenDeathCanvasParent != null)
        {
            return centerScreenDeathCanvasParent;
        }

        return canvas != null ? canvas.transform : transform;
    }

    private Transform ResolveCenterScreenDeathWorldParent()
    {
        if (centerScreenDeathWorldParent != null)
        {
            return centerScreenDeathWorldParent;
        }

        return animatorParent != null ? animatorParent : transform;
    }

    private void ApplyCenterScreenDeathPlacement(
        GameObject instance,
        PooledEffectToken token,
        FishDeathProfile profile,
        bool useCanvas,
        Canvas targetCanvas
    )
    {
        Transform target = instance.transform;
        token.RestoreDefaultTransform(target);

        float scaleMultiplier = Mathf.Max(
            0.01f,
            profile.centerScreenDeathScaleMultiplier
        );
        target.localScale = token.defaultLocalScale * scaleMultiplier;

        Vector2 viewportPosition =
            profile.centerScreenDeathViewportPosition;
        viewportPosition.x = Mathf.Clamp01(viewportPosition.x);
        viewportPosition.y = Mathf.Clamp01(viewportPosition.y);

        if (useCanvas)
        {
            RectTransform targetRect = target as RectTransform;
            RectTransform parentRect = target.parent as RectTransform;

            if (targetRect != null && parentRect != null)
            {
                targetRect.anchorMin = viewportPosition;
                targetRect.anchorMax = viewportPosition;
                targetRect.anchoredPosition =
                    profile.centerScreenDeathCanvasOffset;

                Vector3 localPosition = targetRect.localPosition;
                localPosition.z = 0f;
                targetRect.localPosition = localPosition;
            }
            else if (targetCanvas != null)
            {
                Camera canvasCamera =
                    targetCanvas.renderMode ==
                        RenderMode.ScreenSpaceOverlay
                        ? null
                        : targetCanvas.worldCamera;
                Vector2 screenPoint = new Vector2(
                    targetCanvas.pixelRect.xMin +
                        targetCanvas.pixelRect.width * viewportPosition.x,
                    targetCanvas.pixelRect.yMin +
                        targetCanvas.pixelRect.height * viewportPosition.y
                ) + profile.centerScreenDeathCanvasOffset;
                RectTransform canvasRect =
                    targetCanvas.transform as RectTransform;
                Vector2 localPoint;

                if (canvasRect != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screenPoint,
                        canvasCamera,
                        out localPoint
                    ))
                {
                    target.localPosition = new Vector3(
                        localPoint.x,
                        localPoint.y,
                        0f
                    );
                }
            }

            target.SetAsLastSibling();
            return;
        }

        Camera targetCamera = centerScreenDeathCamera != null
            ? centerScreenDeathCamera
            : Camera.main;
        float worldPlaneZ = centerScreenDeathWorldParent != null
            ? centerScreenDeathWorldParent.position.z
            : 0f;
        Vector3 worldPosition;

        if (targetCamera != null)
        {
            float cameraDistance = Mathf.Abs(
                worldPlaneZ - targetCamera.transform.position.z
            );
            cameraDistance = Mathf.Max(0.01f, cameraDistance);
            worldPosition = targetCamera.ViewportToWorldPoint(
                new Vector3(
                    viewportPosition.x,
                    viewportPosition.y,
                    cameraDistance
                )
            );
            worldPosition.z = worldPlaneZ;
        }
        else
        {
            worldPosition = new Vector3(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f,
                worldPlaneZ
            );
        }

        worldPosition += (Vector3)profile.centerScreenDeathWorldOffset;
        target.position = worldPosition;
    }

    private static void DisableCenterScreenDeathInteraction(
        GameObject instance,
        bool useCanvas
    )
    {
        Collider2D[] colliders2D =
            instance.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders2D.Length; i++)
        {
            colliders2D[i].enabled = false;
        }

        Collider[] colliders3D =
            instance.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders3D.Length; i++)
        {
            colliders3D[i].enabled = false;
        }

        if (!useCanvas)
        {
            return;
        }

        CanvasGroup interactionGroup =
            instance.GetComponent<CanvasGroup>();

        if (interactionGroup == null)
        {
            interactionGroup = instance.AddComponent<CanvasGroup>();
        }

        interactionGroup.blocksRaycasts = false;
        interactionGroup.interactable = false;
    }

    private void CleanActiveCenterScreenDeathEffects()
    {
        for (int i = activeCenterScreenDeathEffects.Count - 1;
             i >= 0;
             i--)
        {
            GameObject instance = activeCenterScreenDeathEffects[i];

            if (instance == null || !instance.activeSelf)
            {
                activeCenterScreenDeathEffects.RemoveAt(i);
            }
        }
    }

    private void StopActiveCenterScreenDeathEffects()
    {
        for (int i = 0;
             i < activeCenterScreenDeathEffects.Count;
             i++)
        {
            GameObject instance = activeCenterScreenDeathEffects[i];

            if (instance == null)
            {
                continue;
            }

            PooledEffectToken token =
                instance.GetComponent<PooledEffectToken>();

            if (token != null)
            {
                token.playVersion++;
            }

            instance.SetActive(false);
        }

        activeCenterScreenDeathEffects.Clear();
    }

    private IEnumerator ReturnCenterScreenDeathVersioned(
        GameObject instance,
        int version,
        float duration,
        bool useUnscaledTime
    )
    {
        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.02f, duration)
            );
        }
        else
        {
            yield return new WaitForSeconds(
                Mathf.Max(0.02f, duration)
            );
        }

        if (instance == null)
        {
            yield break;
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            instance.SetActive(false);
            activeCenterScreenDeathEffects.Remove(instance);
        }
    }

    // -----------------------------------------------------------------
    // Compatibility wrappers. Existing scripts continue to compile.
    // -----------------------------------------------------------------

    [System.Obsolete("Use mainBossFrontGunSkillPrefabs instead.")]
    public GameObject[] Anima_object
    {
        get { return mainBossFrontGunSkillPrefabs; }
        set { mainBossFrontGunSkillPrefabs = value; }
    }

    [System.Obsolete("Use bossCoinBurstPrefabs instead.")]
    public GameObject partical
    {
        get
        {
            if (bossCoinBurstPrefabs != null &&
                bossCoinBurstPrefabs.Length > 0)
            {
                return bossCoinBurstPrefabs[0];
            }

            return legacyBossCoinBurstPrefab;
        }
        set
        {
            legacyBossCoinBurstPrefab = value;

            if (value == null)
            {
                bossCoinBurstPrefabs = null;
            }
            else
            {
                bossCoinBurstPrefabs = new[]
                {
                    value
                };
            }
        }
    }

    [System.Obsolete("Use bossCoinBurstPrefabs instead.")]
    public GameObject bossCoinBurstPrefab
    {
        get { return partical; }
        set { partical = value; }
    }

    [System.Obsolete("Use mainBossRewardCoinPrefab instead.")]
    public GameObject Coin
    {
        get { return mainBossRewardCoinPrefab; }
        set { mainBossRewardCoinPrefab = value; }
    }

    [System.Obsolete("Use PlayMainBossCoinBurst instead.")]
    public void PlayBoomSupriceCoins(Vector3 pos)
    {
        PlayMainBossCoinBurst(pos);
    }

    [System.Obsolete("Use PlayMainBossDeathSkillInFrontGun instead.")]
    public void PlayCoinAndMoveSkillFrontGun(
        Vector3 pos,
        int bulletId,
        int fishId
    )
    {
        PlayMainBossDeathSkillInFrontGun(
            pos,
            bulletId,
            ConvertLegacyFishIdToSkillIndex(fishId)
        );
    }

    [System.Obsolete("Use PlayMainBossDeathSkill instead.")]
    public IEnumerator PlayAnima(Vector3 pos, int fishId)
    {
        yield return PlayMainBossDeathSkill(
            pos,
            ConvertLegacyFishIdToSkillIndex(fishId)
        );
    }

    private static int ConvertLegacyFishIdToSkillIndex(int fishId)
    {
        if (fishId == 37)
        {
            return 0;
        }

        if (fishId == 36)
        {
            return 2;
        }

        return 1;
    }
}
