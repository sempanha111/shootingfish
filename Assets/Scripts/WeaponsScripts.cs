using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponsScripts : MonoBehaviour
{
    private enum PlayerFireMode
    {
        ManualNormal,
        AutoShotNormal,
        RocketSingleShot,
        RocketAutoShot
    }

    [Header("Existing Normal Guns - Keep Existing Values")]
    public GameObject[] Gunlevel;
    public GameObject[] Gun;
    public Animator[] Anima_Gun;
    [HideInInspector, SerializeField] public float[] Bet;

    private readonly List<GameObject> Listpoinclick =
        new List<GameObject>();

    public GameObject poinClick;

    [SerializeField] public int activeGunLevel = 1;
    [SerializeField] private float AnimaShootWait = 0.11f;

    public float Totalbet;

    private GameManager GM;
    public GameObject activeGun;

    private bool holding;
    private float timeToShoot;
    private float fireRate = 3.5f;

    [Header("Auto Shot - Normal Gun GPS")]
    [Tooltip("World-space crosshair prefab. The same marker is reused for Auto Shot and Rocket targeting.")]
    [SerializeField] private GameObject pointGpsPrefab;

    [Tooltip("Layers containing fish colliders that can be selected.")]
    [SerializeField] private LayerMask fishTargetLayers = ~0;

    [Tooltip("Optional parent for the GPS marker and Rocket Net effects.")]
    [SerializeField] private Transform gpsEffectParent;

    [SerializeField] private int gpsSortingOrder = 5000;

    [Header("Automatic Target Search")]
    [Tooltip("When Auto Shot is active, automatically select another visible living fish when no valid target exists.")]
    [SerializeField] private bool autoFindVisibleFish = true;

    [Tooltip("Minimum delay between target searches. The existing GameManager fish list is reused, so no scene search is performed.")]
    [SerializeField, Min(0.02f)] private float autoTargetSearchInterval = 0.08f;

    [Tooltip("Keeps auto-selected fish slightly inside the viewport. Zero allows fish touching the exact screen edge.")]
    [SerializeField, Range(0f, 0.20f)] private float autoTargetViewportPadding = 0.015f;

    [Tooltip("Adds a small selection preference for bosses without preventing ordinary fish from being targeted.")]
    [SerializeField] private bool autoTargetPreferBosses = true;

    [Header("Manual Fish Target Lock")]
    [Tooltip("When enabled, clicking a fish locks normal-gun shots or Big Rocket shots to that exact fish. The lock never auto-selects a replacement when the fish dies or leaves the screen.")]
    [SerializeField] private bool targetLockEnabled;

    [Tooltip("Automatically keeps firing the selected normal gun or Big Rocket at the manually locked fish until it dies, is pooled, or leaves the visible screen. No replacement target is selected.")]
    [SerializeField] private bool targetLockAutoFire = true;

    [Tooltip("Optional Lock Target button root RectTransform. It grows while manual target lock is controlling the gun.")]
    [SerializeField] private RectTransform targetLockSkillUi;

    [Header("Skill Button Selection UI")]
    [Tooltip("Assign the Auto Shot button root RectTransform. It grows while Auto Shot is active.")]
    [SerializeField] private RectTransform autoShotSkillUi;

    [Tooltip("Assign the BigRoket button root RectTransform. It grows while Rocket mode is active.")]
    [SerializeField] private RectTransform bigRocketSkillUi;

    [SerializeField, Range(1f, 1.5f)]
    private float activeSkillUiScale = 1.15f;

    [SerializeField, Min(0f)]
    private float skillUiScaleDuration = 0.12f;

    [Header("Big Rocket Gun")]
    [Tooltip("The Rocket gun model shown while the skill is active.")]
    [SerializeField] private GameObject rocketGunVisual;

    [Tooltip("Transform that rotates to aim the Rocket gun. Uses Rocket Gun Visual when empty.")]
    [SerializeField] private Transform rocketAimTransform;

    [Tooltip("Rocket projectile prefab. It can be copied from a normal bullet; normal BulletScript is disabled at runtime.")]
    [SerializeField] private GameObject rocketBulletPrefab;

    [Tooltip("Optional parent for pooled Rocket projectiles.")]
    [SerializeField] private Transform rocketPoolParent;

    [Tooltip("Drag the Rocket Net effect prefab here. It is played only when the locked fish is hit.")]
    public GameObject rocketNetEffectPrefab;

    [Tooltip("Manual cost of each Big Rocket in fictional arcade coins. This does not change the normal Gun Bet array.")]
    [SerializeField, Min(0f)] private float bigRocketShotCost = 25f;

    [SerializeField, Min(0.1f)] private float rocketSpeed = 5f;
    [SerializeField, Min(0.05f)] private float rocketMinimumSecondsBetweenShots = 0.65f;
    [SerializeField, Min(1f)] private float rocketHomingTurnDegreesPerSecond = 300f;
    [SerializeField, Min(0.5f)] private float rocketLifetime = 12f;
    [SerializeField, Min(0.05f)] private float rocketImpactDistance = 0.22f;
    [SerializeField, Min(0.05f)] private float rocketDamage = 1f;
    [SerializeField, Min(0.02f)] private float rocketNetVisibleTime = 0.35f;
    [SerializeField, Min(0.05f)] private float earthquakeDuration = 0.30f;
    [SerializeField, Min(0f)] private float earthquakeStrength = 0.16f;

    [Header("Big Rocket Radial Area Damage")]
    [Tooltip("Fish layers damaged by the large Rocket Net blast.")]
    [SerializeField] private LayerMask rocketAreaDamageLayers = ~0;

    [Tooltip("World-space radius around the impact. The blast is circular and reaches equally in every direction.")]
    [SerializeField, Min(0.05f)] private float rocketAreaRadius = 1.6f;

    [Tooltip("Nearby-fish damage relative to the direct Rocket damage. 1 means full damage.")]
    [SerializeField, Min(0f)] private float rocketAreaDamageMultiplier = 1f;

    [Tooltip("Safety limit for fish damaged by one Rocket blast.")]
    [SerializeField, Range(1, 64)] private int rocketMaximumAreaTargets = 32;

    [SerializeField] private Animator rocketGunAnimator;

    [Tooltip("Optional Rocket fire Trigger or Animator state name. Leave empty to fire without a gun animation.")]
    [SerializeField] private string rocketShootTrigger = "Shoot";

    private readonly List<GameObject> rocketPool =
        new List<GameObject>();
    private readonly List<GameObject> rocketNetPool =
        new List<GameObject>();
    private readonly HashSet<FishScript> rocketAreaDamagedFish =
        new HashSet<FishScript>();
    private readonly Collider2D[] rocketAreaHitBuffer =
        new Collider2D[64];

    private PlayerFireMode fireMode = PlayerFireMode.ManualNormal;
    private FishScript trackedFish;
    private int trackedFishLifeVersion;
    private GameObject gpsMarkerInstance;
    private RotateZoomLooper gpsMarkerAnimation;
    private int normalGunLevelBeforeRocket = 1;
    private float nextRocketShotTime;
    private float nextAutoTargetSearchTime;
    private int lastTargetInputFrame = -1;
    private float lastTargetInputTime = float.NegativeInfinity;
    private Coroutine autoShotUiScaleRoutine;
    private Coroutine bigRocketUiScaleRoutine;
    private Coroutine targetLockUiScaleRoutine;

    /// <summary>
    /// Read-only skill state for custom buttons and companion ability controllers.
    /// Target lifetime validation prevents pooled fish from being returned.
    /// </summary>
    public FishScript CurrentTrackedFish
    {
        get { return IsTrackedFishValid() ? trackedFish : null; }
    }

    public bool AutoShotActive
    {
        get { return IsAutoShotEnabled; }
    }

    public bool RocketModeActive
    {
        get { return IsRocketMode; }
    }

    /// <summary>
    /// True when the player has enabled manual target-lock preference.
    /// Auto Shot can temporarily override this preference without turning it off.
    /// </summary>
    public bool TargetLockEnabled
    {
        get { return targetLockEnabled; }
    }

    /// <summary>
    /// True while manual Target Lock controls either the normal gun or
    /// Big Rocket single-target mode. Auto Shot always takes priority.
    /// </summary>
    public bool ManualTargetLockActive
    {
        get { return IsManualTargetLockActive; }
    }

    private bool IsRocketMode
    {
        get
        {
            return fireMode == PlayerFireMode.RocketSingleShot ||
                   fireMode == PlayerFireMode.RocketAutoShot;
        }
    }

    private bool IsAutoShotEnabled
    {
        get
        {
            return fireMode == PlayerFireMode.AutoShotNormal ||
                   fireMode == PlayerFireMode.RocketAutoShot;
        }
    }

    private bool IsManualNormalTargetLockActive
    {
        get
        {
            return targetLockEnabled &&
                   fireMode == PlayerFireMode.ManualNormal &&
                   !IsAutoShotEnabled;
        }
    }

    private bool IsManualRocketTargetLockActive
    {
        get
        {
            return targetLockEnabled &&
                   fireMode == PlayerFireMode.RocketSingleShot &&
                   !IsAutoShotEnabled;
        }
    }

    private bool IsManualTargetLockActive
    {
        get
        {
            return IsManualNormalTargetLockActive ||
                   IsManualRocketTargetLockActive;
        }
    }

    public void ApplyCleanBalancePreset(float shotsPerSecond)
    {
        fireRate = Mathf.Max(0.1f, shotsPerSecond);
        AnimaShootWait = Mathf.Min(
            Mathf.Max(0.04f, 0.38f / fireRate),
            0.14f
        );

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void Start()
    {
        GM = GameManager.Instance;
        ActivateGun(activeGunLevel);

        if (rocketGunVisual != null)
        {
            rocketGunVisual.SetActive(false);
        }

        ApplySkillUiScaleImmediate();
    }

    private void OnDisable()
    {
        holding = false;
        targetLockEnabled = false;
        ClearTrackedFish();
        fireMode = PlayerFireMode.ManualNormal;
        ApplySkillUiScaleImmediate();
    }

    public void ActivateGun(int gununLevelSet)
    {
        if (Gunlevel == null ||
            Gun == null ||
            Gunlevel.Length == 0 ||
            Gun.Length == 0)
        {
            return;
        }

        int safeLevel = Mathf.Clamp(
            gununLevelSet,
            1,
            Mathf.Min(Gunlevel.Length, Gun.Length)
        );

        fireMode = PlayerFireMode.ManualNormal;
        holding = false;
        ClearTrackedFish();
        SetRocketVisual(false);
        RefreshSkillUiScale();

        for (int i = 0; i < Gunlevel.Length; i++)
        {
            if (Gunlevel[i] != null)
            {
                Gunlevel[i].SetActive(i == safeLevel - 1);
            }
        }

        AlwayGetBet(safeLevel);
        activeGun = Gun[safeLevel - 1];
        activeGunLevel = safeLevel;
        normalGunLevelBeforeRocket = safeLevel;
    }

    /// <summary>
    /// Toggles manual fish target lock. After the player clicks a fish, manual
    /// lock can continuously fire at that exact fish. It never searches for a
    /// replacement target. When the fish dies, is pooled, or leaves the visible
    /// screen, firing stops, the marker is cleared, and the player must click
    /// another fish. Auto Shot always has priority and may retarget normally.
    /// </summary>
    public void ToggleTargetLock()
    {
        SetTargetLockEnabled(!targetLockEnabled);
    }

    public void LockFishTarget()
    {
        SetTargetLockEnabled(true);
    }

    public void UnlockFishTarget()
    {
        SetTargetLockEnabled(false);
    }

    // Compatibility aliases for Unity Button OnClick dropdowns.
    public void LockTarget()
    {
        ToggleTargetLock();
    }

    public void FishTargetLock()
    {
        ToggleTargetLock();
    }

    public void SetTargetLockEnabled(bool enabled)
    {
        if (targetLockEnabled == enabled)
        {
            RefreshSkillUiScale();
            return;
        }

        targetLockEnabled = enabled;
        holding = false;

        // Auto Shot keeps control while active. Otherwise changing manual lock
        // starts with a clean target and requires an intentional fish click.
        if (!IsAutoShotEnabled)
        {
            ClearTrackedFish();
        }

        RefreshSkillUiScale();
    }

    /// <summary>
    /// Enables GPS tracking with the currently selected normal gun. Auto Shot
    /// immediately searches for a visible living fish and keeps
    /// reacquiring when that fish dies or leaves the screen. A manual fish
    /// click can still override the selected target. Auto Shot bullets ignore
    /// non-target fish and continue toward the selected fish. Auto Shot takes
    /// priority over manual Target Lock and keeps automatic replacement enabled.
    /// </summary>
    public void AutoShot()
    {
        if (IsAutoShotEnabled)
        {
            StopAutoShot();
            return;
        }

        if (fireMode == PlayerFireMode.RocketSingleShot)
        {
            fireMode = PlayerFireMode.RocketAutoShot;
            holding = false;
            nextAutoTargetSearchTime = 0f;
            nextRocketShotTime = Time.time;
            RefreshSkillUiScale();
            return;
        }

        // Keep a valid manually locked fish as Auto Shot's first target.
        // From this point onward Auto Shot owns the target and may replace it
        // automatically when it dies or leaves the visible screen.
        bool keepCurrentTarget = IsTrackedFishValid();

        fireMode = PlayerFireMode.AutoShotNormal;
        holding = false;

        if (!keepCurrentTarget)
        {
            ClearTrackedFish();
        }

        nextAutoTargetSearchTime = 0f;
        timeToShoot = Time.time;
        RefreshSkillUiScale();
    }

    public void StopAutoShot()
    {
        if (fireMode == PlayerFireMode.RocketAutoShot)
        {
            fireMode = PlayerFireMode.RocketSingleShot;
            ClearTrackedFish();
            RefreshSkillUiScale();
        }
        else if (fireMode == PlayerFireMode.AutoShotNormal)
        {
            fireMode = PlayerFireMode.ManualNormal;
            ClearTrackedFish();
            RefreshSkillUiScale();
        }
    }

    /// <summary>
    /// Changes the visual to Big Rocket. Auto Shot can be enabled at the same
    /// time; otherwise each fish click fires one Rocket.
    /// </summary>
    public void ActivateRocketSkill()
    {
        if (IsRocketMode)
        {
            bool keepAutoShot = fireMode == PlayerFireMode.RocketAutoShot;
            bool keepTargetWhenLeavingRocket = IsTrackedFishValid() &&
                (keepAutoShot || targetLockEnabled);

            RestoreNormalGunVisual();
            fireMode = keepAutoShot
                ? PlayerFireMode.AutoShotNormal
                : PlayerFireMode.ManualNormal;

            if (!keepTargetWhenLeavingRocket)
            {
                ClearTrackedFish();
            }

            AlwayGetBet(activeGunLevel);
            RefreshSkillUiScale();
            return;
        }

        normalGunLevelBeforeRocket = activeGunLevel;
        bool enableRocketAuto = fireMode == PlayerFireMode.AutoShotNormal;
        bool keepTargetWhenEnteringRocket = IsTrackedFishValid() &&
            (enableRocketAuto || targetLockEnabled);

        fireMode = enableRocketAuto
            ? PlayerFireMode.RocketAutoShot
            : PlayerFireMode.RocketSingleShot;
        holding = false;

        if (!keepTargetWhenEnteringRocket)
        {
            ClearTrackedFish();
        }

        nextAutoTargetSearchTime = 0f;
        SetNormalGunLevelsActive(false);
        SetRocketVisual(true);
        Totalbet = Mathf.Max(0f, bigRocketShotCost);
        nextRocketShotTime = Time.time;
        RefreshSkillUiScale();
    }

    // Compatibility aliases make the UI event easy to find in Unity.
    public void ActivateBigRocketSkill()
    {
        ActivateRocketSkill();
    }

    public void ActivateRocketGun()
    {
        ActivateRocketSkill();
    }

    public void ReturnToNormalGun()
    {
        ActivateGun(normalGunLevelBeforeRocket);
    }

    private void RefreshSkillUiScale()
    {
        StartSkillUiScale(
            autoShotSkillUi,
            IsAutoShotEnabled,
            ref autoShotUiScaleRoutine
        );

        StartSkillUiScale(
            bigRocketSkillUi,
            IsRocketMode,
            ref bigRocketUiScaleRoutine
        );

        StartSkillUiScale(
            targetLockSkillUi,
            IsManualTargetLockActive,
            ref targetLockUiScaleRoutine
        );
    }

    private void StartSkillUiScale(
        RectTransform target,
        bool selected,
        ref Coroutine runningRoutine
    )
    {
        if (target == null)
        {
            return;
        }

        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        Vector3 destination = selected
            ? Vector3.one * Mathf.Max(1f, activeSkillUiScale)
            : Vector3.one;

        if (skillUiScaleDuration <= 0f || !isActiveAndEnabled)
        {
            target.localScale = destination;
            runningRoutine = null;
            return;
        }

        runningRoutine = StartCoroutine(
            AnimateSkillUiScale(target, destination)
        );
    }

    private IEnumerator AnimateSkillUiScale(
        RectTransform target,
        Vector3 destination
    )
    {
        Vector3 startScale = target.localScale;
        float duration = Mathf.Max(0.01f, skillUiScaleDuration);
        float elapsed = 0f;

        while (target != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            target.localScale = Vector3.LerpUnclamped(
                startScale,
                destination,
                t
            );
            yield return null;
        }

        if (target != null)
        {
            target.localScale = destination;
        }
    }

    private void ApplySkillUiScaleImmediate()
    {
        if (autoShotSkillUi != null)
        {
            autoShotSkillUi.localScale = IsAutoShotEnabled
                ? Vector3.one * Mathf.Max(1f, activeSkillUiScale)
                : Vector3.one;
        }

        if (bigRocketSkillUi != null)
        {
            bigRocketSkillUi.localScale = IsRocketMode
                ? Vector3.one * Mathf.Max(1f, activeSkillUiScale)
                : Vector3.one;
        }

        if (targetLockSkillUi != null)
        {
            targetLockSkillUi.localScale = IsManualTargetLockActive
                ? Vector3.one * Mathf.Max(1f, activeSkillUiScale)
                : Vector3.one;
        }
    }

    private void AlwayGetBet(int gunLevel)
    {
        if (Bet == null || Bet.Length == 0)
        {
            Totalbet = 0f;
            return;
        }

        int index = Mathf.Clamp(gunLevel - 1, 0, Bet.Length - 1);
        Totalbet = Mathf.Max(0f, Bet[index]);
    }

    private void SetNormalGunLevelsActive(bool isActive)
    {
        if (Gunlevel == null)
        {
            return;
        }

        for (int i = 0; i < Gunlevel.Length; i++)
        {
            if (Gunlevel[i] != null)
            {
                Gunlevel[i].SetActive(
                    isActive && i == activeGunLevel - 1
                );
            }
        }
    }

    private void SetRocketVisual(bool isActive)
    {
        if (rocketGunVisual == null)
        {
            return;
        }

        if (isActive)
        {
            // Support the user's hierarchy where RocketGun is nested inside
            // one GunLevel object. Do not activate unrelated scene parents.
            GameObject owningLevel = FindRocketOwningGunLevel();
            Transform parent = rocketGunVisual.transform.parent;

            while (parent != null &&
                   owningLevel != null &&
                   parent != owningLevel.transform)
            {
                parent.gameObject.SetActive(true);
                parent = parent.parent;
            }

            if (owningLevel != null)
            {
                owningLevel.SetActive(true);
            }
        }

        rocketGunVisual.SetActive(isActive);
    }

    private GameObject FindRocketOwningGunLevel()
    {
        if (Gunlevel == null || rocketGunVisual == null)
        {
            return null;
        }

        for (int i = 0; i < Gunlevel.Length; i++)
        {
            GameObject level = Gunlevel[i];

            if (level != null &&
                (rocketGunVisual == level ||
                 rocketGunVisual.transform.IsChildOf(level.transform)))
            {
                return level;
            }
        }

        return null;
    }

    private void RestoreNormalGunVisual()
    {
        SetRocketVisual(false);
        activeGunLevel = Mathf.Max(1, normalGunLevelBeforeRocket);
        SetNormalGunLevelsActive(true);
    }

    private void RotateActiveGunAt(Vector3 targetPosition)
    {
        if (activeGun != null)
        {
            RotateTransformAt(activeGun.transform, targetPosition);
        }
    }

    private static void RotateTransformAt(
        Transform aimTransform,
        Vector3 targetPosition
    )
    {
        if (aimTransform == null)
        {
            return;
        }

        Vector3 direction = targetPosition - aimTransform.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) *
                      Mathf.Rad2Deg - 90f;

        aimTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void RotateActiveGun()
    {
        if (activeGun == null || Camera.main == null)
        {
            return;
        }

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(
            Input.mousePosition
        );
        worldPosition.z = 0f;
        RotateActiveGunAt(worldPosition);
    }

    private IEnumerator SwitchAndAnimateGun(
        Animator animator,
        string animationName,
        string idleAnimation
    )
    {
        if (animator == null ||
            !animator.gameObject.activeInHierarchy)
        {
            yield break;
        }

        animator.Play(animationName);
        yield return new WaitForSeconds(AnimaShootWait);

        if (animator == null ||
            !animator.gameObject.activeInHierarchy)
        {
            yield break;
        }

        animator.Play(idleAnimation);
    }

    private void PoinMouseClick()
    {
        if (poinClick == null || Camera.main == null)
        {
            return;
        }

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(
            Input.mousePosition
        );

        mousePosition.z = 0f;
        GameObject pointClone = null;

        for (int i = 0; i < Listpoinclick.Count; i++)
        {
            GameObject candidate = Listpoinclick[i];

            if (candidate != null && !candidate.activeSelf)
            {
                pointClone = candidate;
                break;
            }
        }

        if (pointClone == null)
        {
            pointClone = Instantiate(poinClick);
            Listpoinclick.Add(pointClone);
        }

        PooledEffectToken token =
            pointClone.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = pointClone.AddComponent<PooledEffectToken>();
        }

        int version = ++token.playVersion;

        pointClone.transform.position = mousePosition;
        pointClone.transform.rotation = Quaternion.identity;
        pointClone.SetActive(true);

        StartCoroutine(ResetPointRoutine(pointClone, version));
    }

    private IEnumerator ResetPointRoutine(
        GameObject pointClone,
        int version
    )
    {
        yield return new WaitForSeconds(0.2f);

        if (pointClone == null)
        {
            yield break;
        }

        PooledEffectToken token =
            pointClone.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            pointClone.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateTrackedFishAndGps();

        if (fireMode == PlayerFireMode.AutoShotNormal)
        {
            UpdateAutoShot();
            return;
        }

        if (fireMode == PlayerFireMode.RocketAutoShot)
        {
            UpdateAutoRocketShot();
            return;
        }

        if (fireMode == PlayerFireMode.RocketSingleShot)
        {
            // Target Lock can own Big Rocket exactly like the normal gun.
            // It repeatedly fires at the manually selected fish and stops
            // without selecting a replacement when that fish becomes invalid.
            if (IsManualRocketTargetLockActive && targetLockAutoFire)
            {
                TryFireLockedRocketNow();
            }

            return;
        }

        if (IsManualNormalTargetLockActive)
        {
            // Target Lock owns this exact fish only. With Auto Fire enabled,
            // one click starts continuous target-exclusive fire until the fish
            // dies, is pooled, or leaves the visible camera area. It never
            // acquires a replacement; Auto Shot is the mode that may retarget.
            if (targetLockAutoFire || holding)
            {
                TryShootLockedTargetNow();
            }

            return;
        }

        if (!holding || timeToShoot > Time.time)
        {
            return;
        }

        if (TryShootPlayerWeapon())
        {
            PoinMouseClick();
        }

        ScheduleNextNormalShot();
    }

    private void UpdateAutoShot()
    {
        if (!IsTrackedFishValid() || timeToShoot > Time.time)
        {
            return;
        }

        Vector3 targetPosition = trackedFish.GetTargetCenterWorld();

        if (TryShootPlayerWeaponAt(targetPosition, trackedFish))
        {
            ScheduleNextNormalShot();
        }
        else
        {
            // Avoid retrying every frame when the current balance is too low.
            timeToShoot = Time.time + 0.15f;
        }
    }

    private void UpdateAutoRocketShot()
    {
        if (!IsTrackedFishValid() || Time.time < nextRocketShotTime)
        {
            return;
        }

        if (!TryFireSingleRocket(trackedFish))
        {
            // Avoid retrying every frame if the balance or prefab setup is not
            // ready. A successful shot schedules the normal Rocket interval.
            nextRocketShotTime = Time.time + 0.15f;
        }
    }

    private bool TryFireLockedRocketNow()
    {
        if (!IsManualRocketTargetLockActive ||
            !IsTrackedFishValid() ||
            Time.time < nextRocketShotTime)
        {
            return false;
        }

        if (TryFireSingleRocket(trackedFish))
        {
            return true;
        }

        // Avoid retrying every frame if the balance or Rocket prefab setup is
        // not ready. The manual lock remains on the same target.
        nextRocketShotTime = Time.time + 0.15f;
        return false;
    }

    private bool TryShootLockedTargetNow()
    {
        if (!IsManualNormalTargetLockActive ||
            !IsTrackedFishValid() ||
            timeToShoot > Time.time)
        {
            return false;
        }

        Vector3 targetPosition = trackedFish.GetTargetCenterWorld();

        if (TryShootPlayerWeaponAt(targetPosition, trackedFish))
        {
            ScheduleNextNormalShot();
            return true;
        }

        // Prevent a failed balance or prefab check from retrying every frame.
        timeToShoot = Time.time + 0.15f;
        return false;
    }

    private void ScheduleNextNormalShot()
    {
        timeToShoot = Time.time +
            1f / Mathf.Max(0.1f, fireRate);
    }

    public void ShootingClick()
    {
        if (fireMode != PlayerFireMode.ManualNormal)
        {
            ProcessTargetInputOncePerFrame();
            return;
        }

        if (IsManualNormalTargetLockActive)
        {
            ProcessTargetInputOncePerFrame();
            TryShootLockedTargetNow();
            return;
        }

        TryShootPlayerWeapon();
    }

    private bool TryShootPlayerWeapon()
    {
        RotateActiveGun();
        return TryShootPlayerWeaponCore(null);
    }

    private bool TryShootPlayerWeaponAt(
        Vector3 targetPosition,
        FishScript exclusiveTarget
    )
    {
        RotateActiveGunAt(targetPosition);
        return TryShootPlayerWeaponCore(exclusiveTarget);
    }

    private bool TryShootPlayerWeaponCore(FishScript exclusiveTarget)
    {
        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        if (GM == null ||
            GM.shoot == null ||
            activeGun == null)
        {
            return false;
        }

        if (exclusiveTarget != null &&
            (!exclusiveTarget.IsAliveTarget ||
             !exclusiveTarget.IsTargetVisibleTo(Camera.main)))
        {
            return false;
        }

        float shotCost;
        bool targetExclusive = exclusiveTarget != null;
        int targetLifeVersion = targetExclusive
            ? exclusiveTarget.TargetLifeVersion
            : 0;

        if (!GM.shoot.TryShootOnce(
                activeGun.transform,
                activeGunLevel,
                0,
                out shotCost,
                exclusiveTarget,
                targetLifeVersion,
                targetExclusive
            ))
        {
            return false;
        }

        PlayNormalGunAnimation();
        return true;
    }

    private void PlayNormalGunAnimation()
    {
        int animatorIndex = activeGunLevel - 1;

        if (Anima_Gun != null &&
            animatorIndex >= 0 &&
            animatorIndex < Anima_Gun.Length)
        {
            StartCoroutine(
                SwitchAndAnimateGun(
                    Anima_Gun[animatorIndex],
                    "Shoot" + activeGunLevel,
                    "Idle" + activeGunLevel
                )
            );
        }
    }

    public void OnClickDown()
    {
        if (fireMode != PlayerFireMode.ManualNormal)
        {
            holding = false;
            ProcessTargetInputOncePerFrame();
            return;
        }

        holding = true;

        if (IsManualNormalTargetLockActive)
        {
            ProcessTargetInputOncePerFrame();
        }
    }

    public void OnClickUp()
    {
        holding = false;
    }

    private void ProcessTargetInputOncePerFrame()
    {
        // Unity UI may invoke PointerDown and PointerClick for one physical
        // click on different frames. This guard keeps one target selection and
        // one manual Rocket launch per physical click.
        if (lastTargetInputFrame == Time.frameCount ||
            Time.unscaledTime - lastTargetInputTime < 0.12f)
        {
            return;
        }

        lastTargetInputFrame = Time.frameCount;
        lastTargetInputTime = Time.unscaledTime;
        FishScript selectedFish = GetFishUnderPointer();

        if (selectedFish == null)
        {
            return;
        }

        SetTrackedFish(selectedFish);

        if (fireMode == PlayerFireMode.RocketSingleShot)
        {
            TryFireSingleRocket(selectedFish);
        }
    }

    private FishScript GetFishUnderPointer()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return null;
        }

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(
            Input.mousePosition
        );
        Vector2 point = new Vector2(worldPoint.x, worldPoint.y);
        Collider2D[] hits = Physics2D.OverlapPointAll(
            point,
            fishTargetLayers
        );

        FishScript bestFish = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            FishScript fish = hit != null
                ? hit.GetComponentInParent<FishScript>()
                : null;

            if (fish == null ||
                !fish.IsAliveTarget ||
                !fish.IsTargetVisibleTo(mainCamera))
            {
                continue;
            }

            float distance = ((Vector2)hit.bounds.center - point).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestFish = fish;
            }
        }

        return bestFish;
    }

    private void SetTrackedFish(FishScript fish)
    {
        if (trackedFish != null && trackedFish != fish &&
            trackedFish.TargetLifeVersion == trackedFishLifeVersion)
        {
            trackedFish.NotifyIncomingFireStopped();
        }

        trackedFish = fish;
        trackedFishLifeVersion = fish != null
            ? fish.TargetLifeVersion
            : 0;

        if (fish == null)
        {
            HideGpsMarker();
            return;
        }

        EnsureGpsMarker();
        UpdateGpsMarkerPosition();
    }

    private void ClearTrackedFish()
    {
        if (trackedFish != null &&
            trackedFish.TargetLifeVersion == trackedFishLifeVersion)
        {
            trackedFish.NotifyIncomingFireStopped();
        }

        trackedFish = null;
        trackedFishLifeVersion = 0;
        HideGpsMarker();
    }

    private bool IsTrackedFishValid()
    {
        Camera mainCamera = Camera.main;

        return trackedFish != null &&
               trackedFish.TargetLifeVersion == trackedFishLifeVersion &&
               trackedFish.IsAliveTarget &&
               trackedFish.IsTargetVisibleTo(mainCamera);
    }

    private void UpdateTrackedFishAndGps()
    {
        if (trackedFish != null && !IsTrackedFishValid())
        {
            ClearTrackedFish();
        }

        if (trackedFish == null &&
            IsAutoShotEnabled &&
            autoFindVisibleFish)
        {
            TryAcquireAutomaticTarget(null, false);
        }

        if (trackedFish == null)
        {
            return;
        }

        UpdateGpsMarkerPosition();

        Transform aim = IsRocketMode
            ? GetRocketAimTransform()
            : activeGun != null ? activeGun.transform : null;

        RotateTransformAt(aim, trackedFish.GetTargetCenterWorld());
    }

    private bool TryAcquireAutomaticTarget(
        FishScript excludedFish,
        bool forceSearch
    )
    {
        if (!autoFindVisibleFish)
        {
            return false;
        }

        if (!forceSearch && Time.unscaledTime < nextAutoTargetSearchTime)
        {
            return false;
        }

        nextAutoTargetSearchTime = Time.unscaledTime +
            Mathf.Max(0.02f, autoTargetSearchInterval);

        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        Camera mainCamera = Camera.main;
        if (GM == null || mainCamera == null ||
            GM.fishInScreenList == null)
        {
            return false;
        }

        Transform aim = IsRocketMode
            ? GetRocketAimTransform()
            : activeGun != null ? activeGun.transform : transform;

        Vector3 origin = aim != null ? aim.position : transform.position;
        FishScript bestFish = null;
        float bestScore = float.PositiveInfinity;
        float padding = Mathf.Clamp(autoTargetViewportPadding, 0f, 0.20f);

        List<FishScript> fishes = GM.fishInScreenList;
        for (int i = 0; i < fishes.Count; i++)
        {
            FishScript fish = fishes[i];

            if (fish == null || fish == excludedFish ||
                !fish.IsAliveTarget ||
                !fish.IsTargetVisibleTo(mainCamera))
            {
                continue;
            }

            Vector3 center = fish.GetTargetCenterWorld();
            Vector3 viewport = mainCamera.WorldToViewportPoint(center);

            if (viewport.z <= 0f ||
                viewport.x < padding || viewport.x > 1f - padding ||
                viewport.y < padding || viewport.y > 1f - padding)
            {
                continue;
            }

            float distanceScore =
                ((Vector2)(center - origin)).sqrMagnitude;
            float centerScore =
                ((Vector2)viewport - new Vector2(0.5f, 0.5f)).sqrMagnitude * 2f;
            float score = distanceScore + centerScore;

            if (autoTargetPreferBosses && fish.IsBoss())
            {
                score *= 0.55f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestFish = fish;
            }
        }

        if (bestFish == null)
        {
            return false;
        }

        SetTrackedFish(bestFish);
        return true;
    }

    /// <summary>
    /// Lets a Rocket already in flight reacquire a new visible fish when its
    /// original target dies or leaves the screen. Retargeting is allowed only
    /// while Rocket Auto Shot remains active.
    /// </summary>
    public bool TryGetAutomaticRocketRetarget(
        FishScript previousTarget,
        out FishScript replacementTarget,
        out int replacementLifeVersion
    )
    {
        replacementTarget = null;
        replacementLifeVersion = 0;

        if (fireMode != PlayerFireMode.RocketAutoShot)
        {
            return false;
        }

        if (!IsTrackedFishValid() || trackedFish == previousTarget)
        {
            if (trackedFish != null && !IsTrackedFishValid())
            {
                ClearTrackedFish();
            }

            TryAcquireAutomaticTarget(previousTarget, true);
        }

        if (!IsTrackedFishValid() || trackedFish == previousTarget)
        {
            return false;
        }

        replacementTarget = trackedFish;
        replacementLifeVersion = trackedFishLifeVersion;
        return true;
    }

    private void EnsureGpsMarker()
    {
        if (pointGpsPrefab == null)
        {
            return;
        }

        if (gpsMarkerInstance == null)
        {
            gpsMarkerInstance = Instantiate(
                pointGpsPrefab,
                gpsEffectParent
            );

            SpriteRenderer[] markerRenderers =
                gpsMarkerInstance.GetComponentsInChildren<SpriteRenderer>(
                    true
                );

            for (int i = 0; i < markerRenderers.Length; i++)
            {
                markerRenderers[i].sortingOrder = gpsSortingOrder + i;
            }

            gpsMarkerAnimation =
                gpsMarkerInstance.GetComponentInChildren<RotateZoomLooper>(
                    true
                );
        }

        gpsMarkerInstance.SetActive(true);

        if (gpsMarkerAnimation != null)
        {
            gpsMarkerAnimation.Play();
            return;
        }

        // Compatibility fallback for older GPS prefabs that still use an
        // Animator instead of RotateZoomLooper.
        Animator markerAnimator =
            gpsMarkerInstance.GetComponentInChildren<Animator>(true);

        if (markerAnimator != null)
        {
            markerAnimator.enabled = true;
            markerAnimator.Rebind();
            markerAnimator.Update(0f);
            markerAnimator.Play(0, 0, 0f);
        }
    }

    private void UpdateGpsMarkerPosition()
    {
        if (gpsMarkerInstance == null || trackedFish == null)
        {
            return;
        }

        Vector3 center = trackedFish.GetTargetCenterWorld();
        center.z = gpsMarkerInstance.transform.position.z;
        gpsMarkerInstance.transform.position = center;
    }

    private void HideGpsMarker()
    {
        if (gpsMarkerInstance != null)
        {
            gpsMarkerInstance.SetActive(false);
        }
    }

    private Transform GetRocketAimTransform()
    {
        if (rocketAimTransform != null)
        {
            return rocketAimTransform;
        }

        return rocketGunVisual != null
            ? rocketGunVisual.transform
            : null;
    }

    private bool TryFireSingleRocket(FishScript target)
    {
        if (target == null ||
            !target.IsAliveTarget ||
            !target.IsTargetVisibleTo(Camera.main) ||
            Time.time < nextRocketShotTime)
        {
            return false;
        }

        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        if (GM == null || activeGun == null || rocketBulletPrefab == null)
        {
            return false;
        }

        GameObject rocket = GetRocketFromPool();

        if (rocket == null)
        {
            return false;
        }

        float shotCost;

        // Big Rocket has its own Inspector-configured arcade-coin cost. The
        // normal per-gun Bet array remains untouched.
        if (!GM.TryPayFixedShotCost(
                0,
                bigRocketShotCost,
                out shotCost
            ))
        {
            return false;
        }

        Vector3 targetPosition = target.GetTargetCenterWorld();
        Transform rocketAim = GetRocketAimTransform();
        RotateTransformAt(rocketAim, targetPosition);

        // Exact same origin as Shoot.TryShootOnce for the normal gun.
        Vector3 spawnPosition = activeGun.transform.position;
        Vector3 direction = (targetPosition - spawnPosition).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) *
                      Mathf.Rad2Deg - 90f;

        rocket.transform.position = spawnPosition;
        rocket.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        BigRocketBullet rocketScript =
            rocket.GetComponent<BigRocketBullet>();

        rocketScript.Configure(
            this,
            target,
            target.TargetLifeVersion,
            rocketDamage,
            activeGunLevel,
            rocketSpeed,
            rocketHomingTurnDegreesPerSecond,
            rocketLifetime,
            rocketImpactDistance
        );

        rocket.SetActive(true);
        nextRocketShotTime = Time.time +
            Mathf.Max(0.05f, rocketMinimumSecondsBetweenShots);

        TryPlayRocketShootAnimation();

        return true;
    }

    private void TryPlayRocketShootAnimation()
    {
        if (rocketGunAnimator == null ||
            !rocketGunAnimator.isActiveAndEnabled ||
            string.IsNullOrWhiteSpace(rocketShootTrigger))
        {
            return;
        }

        string animationName = rocketShootTrigger.Trim();
        int animationHash = Animator.StringToHash(animationName);
        AnimatorControllerParameter[] parameters =
            rocketGunAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == animationHash &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                rocketGunAnimator.ResetTrigger(animationHash);
                rocketGunAnimator.SetTrigger(animationHash);
                return;
            }
        }

        // Some gun controllers expose a direct animation state instead of a
        // Trigger parameter. Support both layouts without producing Unity's
        // "Parameter does not exist" console error.
        for (int layer = 0; layer < rocketGunAnimator.layerCount; layer++)
        {
            string fullStateName =
                rocketGunAnimator.GetLayerName(layer) + "." + animationName;
            int fullStateHash = Animator.StringToHash(fullStateName);

            if (rocketGunAnimator.HasState(layer, fullStateHash))
            {
                rocketGunAnimator.Play(fullStateHash, layer, 0f);
                return;
            }

            if (rocketGunAnimator.HasState(layer, animationHash))
            {
                rocketGunAnimator.Play(animationHash, layer, 0f);
                return;
            }
        }
    }

    private GameObject GetRocketFromPool()
    {
        string expectedName = rocketBulletPrefab.name + "(Clone)";

        for (int i = 0; i < rocketPool.Count; i++)
        {
            GameObject candidate = rocketPool[i];

            if (candidate != null &&
                !candidate.activeSelf &&
                candidate.name == expectedName)
            {
                PrepareRocketObject(candidate);
                return candidate;
            }
        }

        GameObject created = Instantiate(
            rocketBulletPrefab,
            rocketPoolParent
        );
        created.SetActive(false);
        PrepareRocketObject(created);
        rocketPool.Add(created);
        return created;
    }

    private static void PrepareRocketObject(GameObject rocket)
    {
        // A Rocket prefab is often copied from a normal bullet. Disable every
        // inherited BulletScript, including scripts placed on child objects,
        // so only BigRocketBullet owns its trigger collision.
        BulletScript[] inheritedNormalBullets =
            rocket.GetComponentsInChildren<BulletScript>(true);

        for (int i = 0; i < inheritedNormalBullets.Length; i++)
        {
            inheritedNormalBullets[i].enabled = false;
        }

        Collider2D[] colliders =
            rocket.GetComponentsInChildren<Collider2D>(true);

        if (colliders.Length == 0)
        {
            CircleCollider2D createdCollider =
                rocket.AddComponent<CircleCollider2D>();
            createdCollider.radius = 0.16f;
            createdCollider.isTrigger = true;
            createdCollider.enabled = true;
        }
        else
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
                colliders[i].enabled = true;
            }
        }

        Rigidbody2D body = rocket.GetComponent<Rigidbody2D>();

        if (body == null)
        {
            body = rocket.AddComponent<Rigidbody2D>();
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (rocket.GetComponent<BigRocketBullet>() == null)
        {
            rocket.AddComponent<BigRocketBullet>();
        }
    }

    public void PlayRocketImpactEffects(
        Vector3 impactPosition,
        int gunLevel
    )
    {
        PlayRocketImpactEffectsInternal(impactPosition, gunLevel);
    }

    public void ResolveRocketImpact(
        FishScript directTarget,
        int directTargetLifeVersion,
        Vector3 impactPosition,
        float directDamage,
        int gunLevel
    )
    {
        GameObject netEffect = PlayRocketImpactEffectsInternal(
            impactPosition,
            gunLevel
        );

        ApplyRocketAreaDamage(
            netEffect,
            directTarget,
            directTargetLifeVersion,
            impactPosition,
            directDamage,
            gunLevel
        );
    }

    private GameObject PlayRocketImpactEffectsInternal(
        Vector3 impactPosition,
        int gunLevel
    )
    {
        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        GameObject netEffect = null;

        if (rocketNetEffectPrefab != null)
        {
            netEffect = PlayRocketNetEffect(impactPosition);
        }
        else if (GM != null)
        {
            // Safe fallback for old scenes. Assign Rocket Net Effect Prefab to
            // make the Rocket independent from the normal per-gun Net array.
            GM.SpawnNet(gunLevel, impactPosition, rocketNetVisibleTime);
        }

        if (GM != null)
        {
            if (GM.SoundManager != null)
            {
                GM.SoundManager.PlayNetBoomSound();
            }

            GM.PlayEarthquake(
                earthquakeDuration,
                earthquakeStrength
            );
        }

        return netEffect;
    }

    private GameObject PlayRocketNetEffect(Vector3 position)
    {
        string expectedName = rocketNetEffectPrefab.name + "(Clone)";
        GameObject instance = null;

        for (int i = 0; i < rocketNetPool.Count; i++)
        {
            GameObject candidate = rocketNetPool[i];

            if (candidate != null &&
                !candidate.activeSelf &&
                candidate.name == expectedName)
            {
                instance = candidate;
                break;
            }
        }

        if (instance == null)
        {
            instance = Instantiate(
                rocketNetEffectPrefab,
                gpsEffectParent
            );
            instance.SetActive(false);
            rocketNetPool.Add(instance);
        }

        PooledEffectToken token =
            instance.GetComponent<PooledEffectToken>();

        if (token == null)
        {
            token = instance.AddComponent<PooledEffectToken>();
        }

        int version = ++token.playVersion;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
        instance.SetActive(true);

        Animator effectAnimator = instance.GetComponent<Animator>();

        if (effectAnimator != null)
        {
            effectAnimator.enabled = true;
            effectAnimator.Rebind();
            effectAnimator.Update(0f);
            effectAnimator.Play(0, 0, 0f);
        }

        ParticleSystem[] particles =
            instance.GetComponentsInChildren<ParticleSystem>();

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        StartCoroutine(
            HideRocketNetRoutine(instance, version)
        );

        return instance;
    }

    private void ApplyRocketAreaDamage(
        GameObject netEffect,
        FishScript directTarget,
        int directTargetLifeVersion,
        Vector3 impactPosition,
        float directDamage,
        int gunLevel
    )
    {
        rocketAreaDamagedFish.Clear();
        int targetLimit = Mathf.Clamp(rocketMaximumAreaTargets, 1, 64);

        // Always apply the direct hit exactly once, even when the target's
        // collider was disabled by an old prefab setting.
        TryDamageRocketFish(
            directTarget,
            directTargetLifeVersion,
            Mathf.Max(0.05f, directDamage),
            gunLevel,
            targetLimit
        );

        Vector2 areaCenter = impactPosition;
        float areaRadius = Mathf.Max(0.05f, rocketAreaRadius);

        ConfigureRuntimeRocketDamageCircle(
            netEffect,
            areaRadius
        );

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            areaCenter,
            areaRadius,
            rocketAreaHitBuffer,
            rocketAreaDamageLayers
        );

        float areaDamage = Mathf.Max(
            0f,
            directDamage * rocketAreaDamageMultiplier
        );

        for (int i = 0;
             i < hitCount && rocketAreaDamagedFish.Count < targetLimit;
             i++)
        {
            Collider2D hit = rocketAreaHitBuffer[i];
            FishScript fish = hit != null
                ? hit.GetComponentInParent<FishScript>()
                : null;

            if (fish == null || fish == directTarget)
            {
                continue;
            }

            TryDamageRocketFish(
                fish,
                fish.TargetLifeVersion,
                areaDamage,
                gunLevel,
                targetLimit
            );
        }
    }

    private void TryDamageRocketFish(
        FishScript fish,
        int requiredLifeVersion,
        float damage,
        int gunLevel,
        int targetLimit
    )
    {
        if (fish == null ||
            rocketAreaDamagedFish.Count >= targetLimit ||
            rocketAreaDamagedFish.Contains(fish) ||
            !fish.IsAliveTarget ||
            fish.TargetLifeVersion != requiredLifeVersion ||
            damage <= 0f)
        {
            return;
        }

        rocketAreaDamagedFish.Add(fish);
        SpriteRenderer targetSprite = fish.GetComponent<SpriteRenderer>();

        if (targetSprite == null)
        {
            targetSprite = fish.GetComponentInChildren<SpriteRenderer>();
        }

        // Every fish receives one independent HP calculation. Fish killed by
        // the blast runs its normal reward path, so each valid kill pays out.
        fish.TakeRocketDamage(targetSprite, damage, 0, gunLevel);
    }

    private static void ConfigureRuntimeRocketDamageCircle(
        GameObject netEffect,
        float radius
    )
    {
        if (netEffect == null)
        {
            return;
        }

        const string childName = "RocketDamageCircle";
        Transform child = netEffect.transform.Find(childName);

        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(netEffect.transform, false);
        }

        CircleCollider2D damageCircle =
            child.GetComponent<CircleCollider2D>();

        if (damageCircle == null)
        {
            damageCircle = child.gameObject.AddComponent<CircleCollider2D>();
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        damageCircle.isTrigger = true;
        damageCircle.offset = Vector2.zero;
        damageCircle.radius = Mathf.Max(0.05f, radius);

        // OverlapCircleNonAlloc performs the one-frame HP scan. Keeping this
        // visual collider disabled prevents duplicate trigger callbacks.
        damageCircle.enabled = false;
    }

    private IEnumerator HideRocketNetRoutine(
        GameObject effect,
        int version
    )
    {
        yield return new WaitForSeconds(
            Mathf.Max(0.02f, rocketNetVisibleTime)
        );

        if (effect == null)
        {
            yield break;
        }

        PooledEffectToken token =
            effect.GetComponent<PooledEffectToken>();

        if (token != null && token.playVersion == version)
        {
            effect.SetActive(false);
        }
    }
}
