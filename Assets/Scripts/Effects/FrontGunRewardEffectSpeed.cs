using UnityEngine;

/// <summary>
/// Runtime-only speed override for the Main Boss front-gun reward prefab.
/// Attach this to PF_BossReward_01 / 02 / 03 (root is recommended).
/// It changes only Animator playback speed; it does not affect fish gameplay,
/// rewards, pooling, or Time.timeScale.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrontGunRewardEffectSpeed : MonoBehaviour
{
    [Header("Animator Speed")]
    [SerializeField, Min(0.01f)] private float animatorSpeed = 2.0f;
    [SerializeField] private bool includeChildAnimators = true;
    [SerializeField] private bool forceAlwaysAnimate = true;

    [Header("Optional Restart")]
    [Tooltip("Normally leave this OFF because AnimatiorManager already restarts pooled Animators. Turn it on only when this prefab is played outside AnimatiorManager.")]
    [SerializeField] private bool restartAnimatorOnEnable = false;

    private Animator[] cachedAnimators;

    public float AnimatorSpeed
    {
        get => animatorSpeed;
        set
        {
            animatorSpeed = Mathf.Max(0.01f, value);
            ApplySpeed();
        }
    }

    private void Awake()
    {
        CacheAnimators();
    }

    private void OnEnable()
    {
        if (cachedAnimators == null || cachedAnimators.Length == 0)
        {
            CacheAnimators();
        }

        ApplySpeed();

        if (restartAnimatorOnEnable)
        {
            RestartAnimators();
        }
    }

    private void OnValidate()
    {
        animatorSpeed = Mathf.Max(0.01f, animatorSpeed);

        if (Application.isPlaying)
        {
            CacheAnimators();
            ApplySpeed();
        }
    }

    private void CacheAnimators()
    {
        cachedAnimators = includeChildAnimators
            ? GetComponentsInChildren<Animator>(true)
            : GetComponents<Animator>();
    }

    private void ApplySpeed()
    {
        if (cachedAnimators == null)
        {
            return;
        }

        for (int i = 0; i < cachedAnimators.Length; i++)
        {
            Animator animator = cachedAnimators[i];
            if (animator == null)
            {
                continue;
            }

            animator.speed = animatorSpeed;

            if (forceAlwaysAnimate)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
    }

    private void RestartAnimators()
    {
        if (cachedAnimators == null)
        {
            return;
        }

        for (int i = 0; i < cachedAnimators.Length; i++)
        {
            Animator animator = cachedAnimators[i];

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.speed = animatorSpeed;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }
    }
}
