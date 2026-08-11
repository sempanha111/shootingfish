using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional compact HUD inspired by polished four-gun arcade layouts. It reads
/// the existing six-level director without controlling spawning or rewards.
/// Every field is optional, so the component is safe while the UI is built.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArcadeHudController : MonoBehaviour
{
    [Header("Director")]
    [SerializeField] private SwapFishScript director;

    [Header("Level and Phase")]
    [SerializeField] private Text levelText;
    [SerializeField] private Text phaseText;
    [SerializeField] private Text targetProgressText;
    [SerializeField] private Text levelTimerText;
    [SerializeField] private Slider levelProgressSlider;
    [SerializeField] private Image phaseAccentImage;

    [Header("Boss HUD")]
    [SerializeField] private GameObject bossHealthRoot;
    [SerializeField] private Text bossNameText;
    [SerializeField] private Text bossHealthText;
    [SerializeField] private Slider bossHealthSlider;

    [Header("Phase Objects")]
    [SerializeField] private GameObject bossWarningRoot;
    [SerializeField] private GameObject bossBattleRoot;
    [SerializeField] private GameObject tideChangeRoot;

    [Header("Polish")]
    [SerializeField] private RectTransform phasePulseRoot;
    [SerializeField, Range(1f, 1.3f)] private float phasePulseScale = 1.10f;
    [SerializeField, Min(0.05f)] private float phasePulseDuration = 0.28f;
    [SerializeField, Min(0.1f)] private float progressSmoothSpeed = 2.8f;
    [SerializeField, Min(0.05f)] private float textRefreshInterval = 0.12f;

    [Header("Phase Accent Colors")]
    [SerializeField] private Color openingColor = new Color(0.20f, 0.85f, 1f, 1f);
    [SerializeField] private Color formationColor = new Color(0.45f, 1f, 0.62f, 1f);
    [SerializeField] private Color paradeColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.30f, 0.18f, 1f);
    [SerializeField] private Color battleColor = new Color(1f, 0.52f, 0.12f, 1f);
    [SerializeField] private Color recoveryColor = new Color(0.66f, 0.48f, 1f, 1f);
    [SerializeField] private Color tideColor = new Color(0.22f, 0.55f, 1f, 1f);

    private SwapFishScript.GameLevel lastLevel;
    private SwapFishScript.LevelPhase lastPhase;
    private bool hasState;
    private float displayedLevelProgress;
    private float displayedBossHealth = 1f;
    private float nextTextRefreshTime;
    private Coroutine pulseRoutine;

    private void Start()
    {
        ResolveDirector();
        RefreshAllImmediate();
    }

    private void OnDisable()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (phasePulseRoot != null)
        {
            phasePulseRoot.localScale = Vector3.one;
        }
    }

    private void Update()
    {
        ResolveDirector();

        if (director == null)
        {
            return;
        }

        DetectStateChange();
        UpdateProgressBars();
        UpdateBossPresentation();

        if (Time.unscaledTime >= nextTextRefreshTime)
        {
            nextTextRefreshTime = Time.unscaledTime +
                Mathf.Max(0.05f, textRefreshInterval);
            RefreshText();
        }
    }

    private void ResolveDirector()
    {
        if (director == null)
        {
            director = FindObjectOfType<SwapFishScript>();
        }
    }

    private void RefreshAllImmediate()
    {
        if (director == null)
        {
            return;
        }

        lastLevel = director.currentLevelState;
        lastPhase = director.currentPhase;
        hasState = true;
        displayedLevelProgress = director.CurrentBossProgressNormalized;

        FishScript boss = director.CurrentPrimaryTargetBoss;
        displayedBossHealth = boss != null
            ? boss.CurrentHealthNormalized
            : 1f;

        ApplyPhaseObjects();
        ApplyPhaseAccent();
        RefreshText();
        SetSlider(levelProgressSlider, displayedLevelProgress);
        UpdateBossPresentation(true);
    }

    private void DetectStateChange()
    {
        if (!hasState)
        {
            RefreshAllImmediate();
            return;
        }

        bool levelChanged = lastLevel != director.currentLevelState;
        bool phaseChanged = lastPhase != director.currentPhase;

        if (!levelChanged && !phaseChanged)
        {
            return;
        }

        lastLevel = director.currentLevelState;
        lastPhase = director.currentPhase;
        ApplyPhaseObjects();
        ApplyPhaseAccent();
        StartPhasePulse();
        RefreshText();
    }

    private void UpdateProgressBars()
    {
        float step = Mathf.Max(0.1f, progressSmoothSpeed) *
                     Time.unscaledDeltaTime;
        displayedLevelProgress = Mathf.MoveTowards(
            displayedLevelProgress,
            director.CurrentBossProgressNormalized,
            step
        );
        SetSlider(levelProgressSlider, displayedLevelProgress);
    }

    private void UpdateBossPresentation(bool immediate = false)
    {
        FishScript boss = director.CurrentPrimaryTargetBoss;
        bool showBoss = boss != null &&
                        director.currentPhase ==
                        SwapFishScript.LevelPhase.BossBattle;

        SetActiveIfDifferent(bossHealthRoot, showBoss);

        if (!showBoss)
        {
            displayedBossHealth = 1f;
            SetSlider(bossHealthSlider, 1f);
            return;
        }

        float targetHealth = boss.CurrentHealthNormalized;
        displayedBossHealth = immediate
            ? targetHealth
            : Mathf.MoveTowards(
                displayedBossHealth,
                targetHealth,
                Mathf.Max(0.1f, progressSmoothSpeed * 1.8f) *
                Time.unscaledDeltaTime
            );
        SetSlider(bossHealthSlider, displayedBossHealth);

        if (bossNameText != null)
        {
            bossNameText.text = CleanCloneName(boss.gameObject.name);
        }

        if (bossHealthText != null)
        {
            bossHealthText.text = Mathf.CeilToInt(
                displayedBossHealth * 100f
            ) + "%";
        }
    }

    private void RefreshText()
    {
        if (director == null)
        {
            return;
        }

        if (levelText != null)
        {
            levelText.text = "LEVEL " + director.CurrentLevelNumber +
                "  " + director.CurrentLevelDisplayName;
        }

        if (phaseText != null)
        {
            phaseText.text = director.CurrentPhaseDisplayName;
        }

        if (targetProgressText != null)
        {
            int total = director.CurrentBossTargetTotal;
            int completed = total > 0
                ? Mathf.Clamp(
                    Mathf.FloorToInt(
                        director.CurrentBossProgressNormalized * total +
                        0.001f
                    ),
                    0,
                    total
                )
                : 0;
            targetProgressText.text = total > 0
                ? "TARGET  " + completed + "/" + total
                : "TARGET  --";
        }

        if (levelTimerText != null)
        {
            int totalSeconds = Mathf.FloorToInt(
                director.CurrentLevelElapsedSeconds
            );
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            levelTimerText.text = minutes.ToString("00") + ":" +
                                  seconds.ToString("00");
        }
    }

    private void ApplyPhaseObjects()
    {
        SwapFishScript.LevelPhase phase = director.currentPhase;
        SetActiveIfDifferent(
            bossWarningRoot,
            phase == SwapFishScript.LevelPhase.BossWarning
        );
        SetActiveIfDifferent(
            bossBattleRoot,
            phase == SwapFishScript.LevelPhase.BossBattle
        );
        SetActiveIfDifferent(
            tideChangeRoot,
            phase == SwapFishScript.LevelPhase.TideChange
        );
    }

    private void ApplyPhaseAccent()
    {
        if (phaseAccentImage == null || director == null)
        {
            return;
        }

        switch (director.currentPhase)
        {
            case SwapFishScript.LevelPhase.Opening:
                phaseAccentImage.color = openingColor;
                break;
            case SwapFishScript.LevelPhase.FeatureBuildUp:
                phaseAccentImage.color = formationColor;
                break;
            case SwapFishScript.LevelPhase.PreBossParade:
                phaseAccentImage.color = paradeColor;
                break;
            case SwapFishScript.LevelPhase.BossWarning:
                phaseAccentImage.color = warningColor;
                break;
            case SwapFishScript.LevelPhase.BossBattle:
                phaseAccentImage.color = battleColor;
                break;
            case SwapFishScript.LevelPhase.Recovery:
                phaseAccentImage.color = recoveryColor;
                break;
            case SwapFishScript.LevelPhase.TideChange:
                phaseAccentImage.color = tideColor;
                break;
        }
    }

    private void StartPhasePulse()
    {
        if (phasePulseRoot == null || !isActiveAndEnabled)
        {
            return;
        }

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        pulseRoutine = StartCoroutine(PhasePulseRoutine());
    }

    private IEnumerator PhasePulseRoutine()
    {
        float duration = Mathf.Max(0.05f, phasePulseDuration);
        Vector3 startScale = Vector3.one * Mathf.Max(1f, phasePulseScale);
        phasePulseRoot.localScale = startScale;
        float elapsed = 0f;

        while (phasePulseRoot != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            phasePulseRoot.localScale = Vector3.LerpUnclamped(
                startScale,
                Vector3.one,
                smooth
            );
            yield return null;
        }

        if (phasePulseRoot != null)
        {
            phasePulseRoot.localScale = Vector3.one;
        }

        pulseRoutine = null;
    }

    private static void SetSlider(Slider slider, float normalized)
    {
        if (slider != null)
        {
            slider.normalizedValue = Mathf.Clamp01(normalized);
        }
    }

    private static void SetActiveIfDifferent(
        GameObject target,
        bool active
    )
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static string CleanCloneName(string rawName)
    {
        return string.IsNullOrEmpty(rawName)
            ? "BOSS"
            : rawName.Replace("(Clone)", string.Empty).Trim();
    }
}
