using UnityEngine;

[DisallowMultipleComponent]
public sealed class RotateZoomLooper : MonoBehaviour
{
    public enum IntroStyle
    {
        None,
        LargeToNormal,
        SmallToNormal
    }

    public enum IntroEase
    {
        Linear,
        Smooth,
        EaseIn,
        EaseOut,
        EaseInOut,
        BackOut
    }

    public enum LoopMode
    {
        None,
        RotateOnly,
        ZoomOnly,
        RotateAndZoom
    }

    public enum ZoomTemplate
    {
        GentleBreathing,
        StandardPulse,
        FastPulse,
        PunchyPop,
        SlowCinematic,
        Custom
    }

    public enum ZoomStartDirection
    {
        ZoomIn,
        ZoomOut
    }

    [Header("General")]
    [Tooltip("Automatically start or restart whenever the prefab is enabled.")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("Continue animating while Time.timeScale is zero.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Restore the normal prefab scale when disabled.")]
    [SerializeField] private bool resetScaleOnDisable = true;

    [Header("Step 1 - Intro")]
    [SerializeField] private IntroStyle introStyle = IntroStyle.LargeToNormal;

    [Tooltip("LargeToNormal example: 4 means scale 4 to scale 1.")]
    [Min(0.01f)]
    [SerializeField] private float introStartScale = 4f;

    [Min(0.01f)]
    [SerializeField] private float introDuration = 0.45f;

    [SerializeField] private IntroEase introEase = IntroEase.EaseOut;

    [Tooltip("Allow Step 2 rotation to run during Step 1.")]
    [SerializeField] private bool rotateDuringIntro;

    [Header("Step 2 - Unlimited Loop")]
    [SerializeField] private LoopMode loopMode = LoopMode.RotateOnly;

    [Tooltip("Negative rotates clockwise. Positive rotates counterclockwise.")]
    [SerializeField] private float rotationDegreesPerSecond = -90f;

    [SerializeField] private ZoomTemplate zoomTemplate = ZoomTemplate.GentleBreathing;

    [SerializeField] private ZoomStartDirection zoomStartDirection = ZoomStartDirection.ZoomIn;

    [Header("Custom Zoom Template")]
    [Min(0.01f)]
    [SerializeField] private float customMinimumScale = 0.9f;

    [Min(0.01f)]
    [SerializeField] private float customMaximumScale = 1.1f;

    [Tooltip("Duration of one complete zoom-in and zoom-out cycle.")]
    [Min(0.05f)]
    [SerializeField] private float customCycleDuration = 1.5f;

    [SerializeField] private IntroEase customZoomEase = IntroEase.Smooth;

    [Header("Runtime Information")]
    [SerializeField] private bool isPlaying;
    [SerializeField] private bool introFinished;

    private Vector3 baseScale;
    private float introTimer;
    private float loopTimer;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        isPlaying = false;

        if (resetScaleOnDisable)
        {
            transform.localScale = baseScale;
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        float deltaTime = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (!introFinished)
        {
            UpdateIntro(deltaTime);

            if (rotateDuringIntro && UsesRotation())
            {
                UpdateRotation(deltaTime);
            }

            return;
        }

        if (UsesRotation())
        {
            UpdateRotation(deltaTime);
        }

        if (UsesZoom())
        {
            UpdateZoomLoop(deltaTime);
        }
        else
        {
            transform.localScale = baseScale;
        }
    }

    private void UpdateIntro(float deltaTime)
    {
        if (introStyle == IntroStyle.None)
        {
            FinishIntro();
            return;
        }

        introTimer += deltaTime;

        float normalizedTime = Mathf.Clamp01(
            introTimer / Mathf.Max(0.01f, introDuration)
        );

        float easedTime = EvaluateEase(normalizedTime, introEase);

        float startingMultiplier;

        switch (introStyle)
        {
            case IntroStyle.LargeToNormal:
                startingMultiplier = Mathf.Max(0.01f, introStartScale);
                break;

            case IntroStyle.SmallToNormal:
                startingMultiplier = Mathf.Clamp(introStartScale, 0.01f, 1f);
                break;

            default:
                startingMultiplier = 1f;
                break;
        }

        float currentMultiplier = Mathf.LerpUnclamped(
            startingMultiplier,
            1f,
            easedTime
        );

        transform.localScale = baseScale * currentMultiplier;

        if (normalizedTime >= 1f)
        {
            FinishIntro();
        }
    }

    private void FinishIntro()
    {
        introFinished = true;
        introTimer = 0f;
        loopTimer = 0f;
        transform.localScale = baseScale;
    }

    private void UpdateRotation(float deltaTime)
    {
        transform.Rotate(
            0f,
            0f,
            rotationDegreesPerSecond * deltaTime,
            Space.Self
        );
    }

    private void UpdateZoomLoop(float deltaTime)
    {
        GetZoomSettings(
            out float minimumScale,
            out float maximumScale,
            out float cycleDuration,
            out IntroEase zoomEase
        );

        loopTimer += deltaTime;

        float cycle = Mathf.Repeat(
            loopTimer / Mathf.Max(0.05f, cycleDuration),
            1f
        );

        bool firstHalf = cycle < 0.5f;

        float halfProgress = firstHalf
            ? cycle * 2f
            : (cycle - 0.5f) * 2f;

        bool firstHalfIsZoomIn =
            zoomStartDirection == ZoomStartDirection.ZoomIn;

        bool zoomingIn = firstHalf
            ? firstHalfIsZoomIn
            : !firstHalfIsZoomIn;

        float fromScale = zoomingIn ? minimumScale : maximumScale;
        float toScale = zoomingIn ? maximumScale : minimumScale;

        float easedTime = EvaluateEase(halfProgress, zoomEase);

        float currentMultiplier = Mathf.LerpUnclamped(
            fromScale,
            toScale,
            easedTime
        );

        transform.localScale = baseScale * currentMultiplier;
    }

    private void GetZoomSettings(
        out float minimumScale,
        out float maximumScale,
        out float cycleDuration,
        out IntroEase zoomEase)
    {
        switch (zoomTemplate)
        {
            case ZoomTemplate.GentleBreathing:
                minimumScale = 0.96f;
                maximumScale = 1.04f;
                cycleDuration = 2.4f;
                zoomEase = IntroEase.Smooth;
                break;

            case ZoomTemplate.StandardPulse:
                minimumScale = 0.85f;
                maximumScale = 1.15f;
                cycleDuration = 1.25f;
                zoomEase = IntroEase.EaseInOut;
                break;

            case ZoomTemplate.FastPulse:
                minimumScale = 0.9f;
                maximumScale = 1.1f;
                cycleDuration = 0.65f;
                zoomEase = IntroEase.EaseInOut;
                break;

            case ZoomTemplate.PunchyPop:
                minimumScale = 0.8f;
                maximumScale = 1.18f;
                cycleDuration = 0.85f;
                zoomEase = IntroEase.BackOut;
                break;

            case ZoomTemplate.SlowCinematic:
                minimumScale = 0.75f;
                maximumScale = 1.25f;
                cycleDuration = 3.2f;
                zoomEase = IntroEase.EaseInOut;
                break;

            case ZoomTemplate.Custom:
            default:
                minimumScale = customMinimumScale;
                maximumScale = customMaximumScale;
                cycleDuration = customCycleDuration;
                zoomEase = customZoomEase;
                break;
        }

        minimumScale = Mathf.Max(0.01f, minimumScale);
        maximumScale = Mathf.Max(0.01f, maximumScale);

        if (minimumScale > maximumScale)
        {
            float temporaryValue = minimumScale;
            minimumScale = maximumScale;
            maximumScale = temporaryValue;
        }
    }

    private bool UsesRotation()
    {
        return loopMode == LoopMode.RotateOnly ||
               loopMode == LoopMode.RotateAndZoom;
    }

    private bool UsesZoom()
    {
        return loopMode == LoopMode.ZoomOnly ||
               loopMode == LoopMode.RotateAndZoom;
    }

    private static float EvaluateEase(float time, IntroEase ease)
    {
        time = Mathf.Clamp01(time);

        switch (ease)
        {
            case IntroEase.Linear:
                return time;

            case IntroEase.EaseIn:
                return time * time;

            case IntroEase.EaseOut:
                return 1f - Mathf.Pow(1f - time, 2f);

            case IntroEase.EaseInOut:
                return time < 0.5f
                    ? 2f * time * time
                    : 1f - Mathf.Pow(-2f * time + 2f, 2f) * 0.5f;

            case IntroEase.BackOut:
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float shiftedTime = time - 1f;

                return 1f +
                       c3 * shiftedTime * shiftedTime * shiftedTime +
                       c1 * shiftedTime * shiftedTime;
            }

            case IntroEase.Smooth:
            default:
                return time * time * (3f - 2f * time);
        }
    }

    [ContextMenu("Play / Restart")]
    public void Play()
    {
        isPlaying = true;
        introTimer = 0f;
        loopTimer = 0f;
        introFinished = introStyle == IntroStyle.None;

        ApplyIntroStartingScale();
    }

    public void PlayFromScale(float startingScale)
    {
        introStyle = startingScale >= 1f
            ? IntroStyle.LargeToNormal
            : IntroStyle.SmallToNormal;

        introStartScale = Mathf.Max(0.01f, startingScale);
        Play();
    }

    public void PlayIntro(
        float startingScale,
        float duration,
        IntroEase ease)
    {
        introStyle = startingScale >= 1f
            ? IntroStyle.LargeToNormal
            : IntroStyle.SmallToNormal;

        introStartScale = Mathf.Max(0.01f, startingScale);
        introDuration = Mathf.Max(0.01f, duration);
        introEase = ease;

        Play();
    }

    public void PlayDynamic(
        float startingScale,
        float duration,
        LoopMode newLoopMode,
        float rotationSpeed,
        ZoomTemplate newZoomTemplate)
    {
        introStyle = startingScale >= 1f
            ? IntroStyle.LargeToNormal
            : IntroStyle.SmallToNormal;

        introStartScale = Mathf.Max(0.01f, startingScale);
        introDuration = Mathf.Max(0.01f, duration);
        loopMode = newLoopMode;
        rotationDegreesPerSecond = rotationSpeed;
        zoomTemplate = newZoomTemplate;

        Play();
    }

    public void Stop(bool resetToNormalScale = true)
    {
        isPlaying = false;

        if (resetToNormalScale)
        {
            transform.localScale = baseScale;
        }
    }

    [ContextMenu("Capture Current Scale As Normal")]
    public void CaptureCurrentScaleAsNormal()
    {
        baseScale = transform.localScale;
    }

    public void RestartAnimation()
    {
        Play();
    }

    private void ApplyIntroStartingScale()
    {
        switch (introStyle)
        {
            case IntroStyle.LargeToNormal:
                transform.localScale =
                    baseScale * Mathf.Max(1f, introStartScale);
                break;

            case IntroStyle.SmallToNormal:
                transform.localScale =
                    baseScale * Mathf.Clamp(introStartScale, 0.01f, 1f);
                break;

            default:
                transform.localScale = baseScale;
                break;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        introStartScale = Mathf.Max(0.01f, introStartScale);
        introDuration = Mathf.Max(0.01f, introDuration);
        customMinimumScale = Mathf.Max(0.01f, customMinimumScale);
        customMaximumScale = Mathf.Max(0.01f, customMaximumScale);
        customCycleDuration = Mathf.Max(0.05f, customCycleDuration);
    }
#endif
}
