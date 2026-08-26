using TMPro;
using UnityEngine;

/// <summary>
/// Lives on PF_BossReward_01 / 02 / 03 and owns the reward-number
/// presentation inside that prefab. The RewardText child keeps its authored
/// local position, so each skill prefab can place the number exactly where
/// its artwork needs it.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrontGunRewardPresentation : MonoBehaviour
{
    [Header("Reward Text Inside This Skill Prefab")]
    [SerializeField] private Transform rewardTextAnchor;
    [SerializeField] private FrontGunRewardCounterText rewardCounter;

    [Header("Fallback Search")]
    [SerializeField] private bool autoFindCounterInChildren = true;
    [SerializeField] private string preferredRewardTextObjectName = "RewardText";
    [SerializeField] private bool hideTextUntilRewardIsPlayed = true;

    public bool HasRewardCounter
    {
        get
        {
            ResolveReferences();
            return rewardCounter != null;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (hideTextUntilRewardIsPlayed && rewardCounter != null)
        {
            rewardCounter.SetVisible(false);
        }
    }

    public bool PlayReward(float rewardAmount, string prefix = "+", float delay = 0f)
    {
        ResolveReferences();

        if (rewardCounter == null)
        {
            return false;
        }

        rewardCounter.SetVisible(true);
        rewardCounter.Play(
            Mathf.Max(0f, rewardAmount),
            prefix ?? string.Empty,
            Mathf.Max(0f, delay)
        );
        return true;
    }

    private void ResolveReferences()
    {
        if (rewardCounter == null && autoFindCounterInChildren)
        {
            rewardCounter = GetComponentInChildren<FrontGunRewardCounterText>(true);
        }

        if (rewardCounter == null && autoFindCounterInChildren)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            TMP_Text best = null;

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == preferredRewardTextObjectName)
                {
                    best = candidate;
                    break;
                }

                if (best == null &&
                    candidate.name.ToLowerInvariant().Contains("reward"))
                {
                    best = candidate;
                }
            }

            if (best != null)
            {
                rewardCounter = best.GetComponent<FrontGunRewardCounterText>();
                if (rewardCounter == null)
                {
                    rewardCounter = best.gameObject.AddComponent<FrontGunRewardCounterText>();
                }
            }
        }

        if (rewardTextAnchor == null && rewardCounter != null)
        {
            Transform parent = rewardCounter.transform.parent;
            rewardTextAnchor = parent != null ? parent : rewardCounter.transform;
        }
    }
}
