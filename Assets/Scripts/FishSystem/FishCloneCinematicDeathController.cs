using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Post-standard-death visual cinematic. It never creates FishScript,
/// Collider2D, Rigidbody2D, HP, reward, target-registration, or death logic.
/// Visual clones are rebuilt from Transform + SpriteRenderer + SortingGroup +
/// Animator data only, so one real fish remains responsible for gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class FishCloneCinematicDeathController : MonoBehaviour
{
    private sealed class AnimatorCopy
    {
        public Animator source;
        public Animator clone;
        public float playbackSpeed = 1f;
    }

    private sealed class CloneRuntime
    {
        public GameObject motionRoot;
        public Transform motionTransform;
        public GameObject visualRoot;
        public Transform visualTransform;
        public readonly List<AnimatorCopy> animators =
            new List<AnimatorCopy>(2);
        public SpriteRenderer[] visualRenderers;
        public Color[] baseRendererColors;
        public bool[] baseRendererEnabled;
        public Vector3 baseScale = Vector3.one;
        public float formationAngle;
        public float individualSpinAngle;
        public float originalWorldRotation;
        public Vector3 destination;
        public float moveDuration;
        public float finalMoveDuration;
        public float arrivalTime;
        public bool arrived;
    }

    [Header("Profile")]
    [SerializeField]
    private FishCloneCinematicDeathProfile profile;

    [Header("Optional Runtime Overrides")]
    [SerializeField] private FishScript owner;

    [Tooltip("Leave empty to safely copy the owner's complete visual hierarchy. Only visual/Animator components are recreated; gameplay components are never copied.")]
    [SerializeField] private Transform visualRootOverride;

    [SerializeField] private Camera targetCamera;

    [Tooltip("Optional world Transform. When assigned it overrides Formation Center Viewport.")]
    [SerializeField] private Transform formationCenterOverride;

    [Tooltip("Optional world Transform. When assigned it overrides Final Center Viewport/World Position.")]
    [SerializeField] private Transform finalCenterOverride;

    [Tooltip("Optional per-clone world targets. Non-null entries override that clone's profile destination.")]
    [SerializeField] private Transform[] cloneDestinationOverrides;

    [Header("Runtime Information")]
    [SerializeField] private bool sequenceRunning;
    [SerializeField] private int activeBulletId;
    [SerializeField] private int activeGunLevel;
    [SerializeField] private float ownerRotationAtDeathStart;

    private Coroutine sequenceRoutine;
    private GameManager gameManager;
    private Transform runtimeRoot;
    private Transform formationRoot;
    private Transform sourceVisualRoot;
    private Vector3 sourceVisualBaseScale = Vector3.one;
    private float sourceOriginalWorldRotation;
    private SpriteRenderer[] ownerRenderers;
    private bool[] ownerRendererEnabled;
    private readonly List<CloneRuntime> clonePool =
        new List<CloneRuntime>(8);
    private readonly Collider2D[] finalDamageOverlapBuffer =
        new Collider2D[64];
    private readonly HashSet<FishScript> finalDamagedFish =
        new HashSet<FishScript>();

    // Many legacy fish prefabs use an unnamed duplicate renderer as their
    // SpriteShadow target. Name-only checks therefore miss the real shadow and
    // clone it at body opacity. Cache the exact authored shadow renderers too.
    private readonly HashSet<SpriteRenderer> sourceShadowRenderers =
        new HashSet<SpriteRenderer>();

    // Optional long-running pooled background layer. Unlike the final Boom,
    // this can begin with the first clone frame and remain active/rotating
    // through every cinematic stage until normal cleanup.
    private GameObject backgroundEffectInstance;
    private float backgroundRotationElapsed;
    private float backgroundAccumulatedAbsoluteRotation;

    public FishCloneCinematicDeathProfile Profile
    {
        get { return profile; }
    }

    public bool IsRunning
    {
        get { return sequenceRunning; }
    }

    public bool WaitForExistingDeathPresentation
    {
        get
        {
            return profile != null &&
                   profile.waitForExistingDeathPresentation;
        }
    }

    public float AdditionalStartDelay
    {
        get
        {
            return profile != null
                ? Mathf.Max(0f, profile.additionalStartDelay)
                : 0f;
        }
    }

    public bool UsesUnscaledTime
    {
        get { return profile != null && profile.useUnscaledTime; }
    }

    private float DeltaTime
    {
        get
        {
            return profile != null && profile.useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }
    }

    private float CurrentTime
    {
        get
        {
            return profile != null && profile.useUnscaledTime
                ? Time.unscaledTime
                : Time.time;
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        if (owner == null)
        {
            owner = GetComponent<FishScript>();
        }
    }

    private void LateUpdate()
    {
        UpdateCinematicBackground();
    }

    private void OnDisable()
    {
        ResetForPool();
    }

    private void OnDestroy()
    {
        StopCinematicBackground();

        if (runtimeRoot != null)
        {
            Destroy(runtimeRoot.gameObject);
            runtimeRoot = null;
            formationRoot = null;
        }
    }

    public void SetProfile(FishCloneCinematicDeathProfile newProfile)
    {
        profile = newProfile;
    }

    public void SetVisualRoot(Transform newVisualRoot)
    {
        visualRootOverride = newVisualRoot;
        sourceVisualRoot = null;
    }

    public void SetFormationCenterOverride(Transform target)
    {
        formationCenterOverride = target;
    }

    public void SetFinalCenterOverride(Transform target)
    {
        finalCenterOverride = target;
    }

    public void SetCloneDestinationOverrides(Transform[] targets)
    {
        cloneDestinationOverrides = targets;
    }

    public bool CanRunForCurrentFish()
    {
        CacheReferences();

        if (profile == null ||
            !profile.enableCustomCloneDeathEffect ||
            owner == null ||
            !isActiveAndEnabled)
        {
            return false;
        }

        if (profile.requiredFishId >= 0 &&
            owner.id != profile.requiredFishId)
        {
            return false;
        }

        return ResolveCamera() != null;
    }

    /// <summary>
    /// Starts the visual-only post-death sequence. FishScript should call this
    /// only after its normal reward/death stage has already resolved.
    /// </summary>
    public bool TryBeginPostDeathCinematic(
        int bulletId,
        int gunLevel,
        float originalOwnerWorldRotation
    )
    {
        if (sequenceRunning || !CanRunForCurrentFish())
        {
            return false;
        }

        profile.EnsureCloneSettingsCount();
        CacheReferences();
        // Prefer FishScript's immutable lethal-hit snapshot. This prevents
        // chained final area-damage kills from being credited to an NPC when
        // the Crystal Whale was actually killed by the local player.
        activeBulletId = owner != null && owner.HasDeathCredit
            ? owner.DeathCreditBulletId
            : bulletId;
        activeGunLevel = owner != null && owner.HasDeathCredit
            ? owner.DeathCreditGunLevel
            : Mathf.Max(1, gunLevel);
        ownerRotationAtDeathStart = originalOwnerWorldRotation;

        sourceVisualRoot = visualRootOverride != null
            ? visualRootOverride
            : owner.transform;

        if (sourceVisualRoot == null)
        {
            return false;
        }

        CacheOwnerRendererState();
        CacheSourceShadowRenderers();
        CacheSourceVisualTransform();
        EnsureRuntimeRoots();
        EnsureCloneCount(profile.cloneCount);
        PrepareActiveClones();

        sequenceRunning = true;
        sequenceRoutine = StartCoroutine(RunSequence());
        return true;
    }

    public void ResetForPool()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        sequenceRunning = false;
        activeBulletId = 0;
        activeGunLevel = 0;
        ownerRotationAtDeathStart = 0f;
        StopCinematicBackground();
        HideAllClones();
        RestoreOwnerRendererState();

        if (formationRoot != null)
        {
            formationRoot.localPosition = Vector3.zero;
            formationRoot.localRotation = Quaternion.identity;
            formationRoot.localScale = Vector3.one;
        }
    }

    private void CacheReferences()
    {
        if (owner == null)
        {
            owner = GetComponent<FishScript>();
        }

        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        ResolveCamera();
    }

    private Camera ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }

    private void CacheSourceVisualTransform()
    {
        if (sourceVisualRoot == null)
        {
            sourceVisualBaseScale = Vector3.one;
            sourceOriginalWorldRotation = 0f;
            return;
        }

        if (owner != null &&
            (sourceVisualRoot == owner.transform ||
             sourceVisualRoot.IsChildOf(owner.transform)))
        {
            float visualRelativeRotation = Mathf.DeltaAngle(
                owner.transform.eulerAngles.z,
                sourceVisualRoot.eulerAngles.z
            );
            sourceOriginalWorldRotation =
                ownerRotationAtDeathStart + visualRelativeRotation;
        }
        else
        {
            sourceOriginalWorldRotation =
                sourceVisualRoot.eulerAngles.z;
        }

        if (owner != null &&
            (sourceVisualRoot == owner.transform ||
             sourceVisualRoot.IsChildOf(owner.transform)))
        {
            Vector3 parentScale = owner.transform.parent != null
                ? owner.transform.parent.lossyScale
                : Vector3.one;
            Vector3 defaultOwnerLossy = Vector3.Scale(
                owner.DefaultLocalScale,
                parentScale
            );
            Vector3 currentOwnerLossy = owner.transform.lossyScale;
            Vector3 visualRelativeScale = new Vector3(
                SafeDivideScale(
                    sourceVisualRoot.lossyScale.x,
                    currentOwnerLossy.x
                ),
                SafeDivideScale(
                    sourceVisualRoot.lossyScale.y,
                    currentOwnerLossy.y
                ),
                SafeDivideScale(
                    sourceVisualRoot.lossyScale.z,
                    currentOwnerLossy.z
                )
            );
            sourceVisualBaseScale = Vector3.Scale(
                defaultOwnerLossy,
                visualRelativeScale
            );
        }
        else
        {
            sourceVisualBaseScale = sourceVisualRoot.lossyScale;
        }

        sourceVisualBaseScale = new Vector3(
            SafeSignedScale(sourceVisualBaseScale.x),
            SafeSignedScale(sourceVisualBaseScale.y),
            SafeSignedScale(sourceVisualBaseScale.z)
        );
    }

    private void CacheSourceShadowRenderers()
    {
        sourceShadowRenderers.Clear();

        if (owner == null)
        {
            return;
        }

        SpriteShadow[] shadowComponents =
            owner.GetComponentsInChildren<SpriteShadow>(true);

        for (int i = 0; i < shadowComponents.Length; i++)
        {
            SpriteShadow shadow = shadowComponents[i];

            if (shadow == null)
            {
                continue;
            }

            SpriteRenderer shadowRenderer = shadow.ShadowSpriteRenderer;

            if (shadowRenderer != null)
            {
                sourceShadowRenderers.Add(shadowRenderer);
            }
        }
    }

    private void CacheOwnerRendererState()
    {
        if (owner == null)
        {
            return;
        }

        ownerRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        ownerRendererEnabled = new bool[ownerRenderers.Length];

        for (int i = 0; i < ownerRenderers.Length; i++)
        {
            ownerRendererEnabled[i] =
                ownerRenderers[i] != null && ownerRenderers[i].enabled;
        }
    }

    private void SetOwnerRenderersVisible(bool visible)
    {
        if (ownerRenderers == null)
        {
            return;
        }

        for (int i = 0; i < ownerRenderers.Length; i++)
        {
            SpriteRenderer renderer = ownerRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (visible)
            {
                bool enabled = ownerRendererEnabled != null &&
                               i < ownerRendererEnabled.Length &&
                               ownerRendererEnabled[i];
                renderer.enabled = enabled;
            }
            else
            {
                renderer.enabled = false;
            }
        }
    }

    private void RestoreOwnerRendererState()
    {
        SetOwnerRenderersVisible(true);
    }

    private void EnsureRuntimeRoots()
    {
        if (runtimeRoot == null)
        {
            GameObject root = new GameObject(
                owner != null
                    ? owner.name + "_CloneCinematicRuntime"
                    : "FishCloneCinematicRuntime"
            );
            root.hideFlags = HideFlags.DontSave;
            runtimeRoot = root.transform;
        }

        if (formationRoot == null)
        {
            GameObject root = new GameObject("FormationRoot");
            root.hideFlags = HideFlags.DontSave;
            formationRoot = root.transform;
            formationRoot.SetParent(runtimeRoot, false);
        }
    }

    private void EnsureCloneCount(int requiredCount)
    {
        requiredCount = Mathf.Clamp(requiredCount, 1, 16);

        while (clonePool.Count < requiredCount)
        {
            clonePool.Add(CreateVisualOnlyClone(clonePool.Count));
        }
    }

    private CloneRuntime CreateVisualOnlyClone(int index)
    {
        CloneRuntime clone = new CloneRuntime();

        GameObject motion = new GameObject("CloneMotion_" + (index + 1));
        motion.hideFlags = HideFlags.DontSave;
        clone.motionRoot = motion;
        clone.motionTransform = motion.transform;
        clone.motionTransform.SetParent(runtimeRoot, false);

        if (sourceVisualRoot == null)
        {
            clone.visualRoot = new GameObject("Visual");
            clone.visualTransform = clone.visualRoot.transform;
            clone.visualTransform.SetParent(clone.motionTransform, false);
            clone.motionRoot.SetActive(false);
            return clone;
        }

        GameObject visual = CopyVisualHierarchyRecursive(
            sourceVisualRoot,
            clone.motionTransform,
            clone,
            true,
            index
        );

        clone.visualRoot = visual;
        clone.visualTransform = visual != null
            ? visual.transform
            : null;
        CacheCloneRendererColors(clone);
        clone.motionRoot.SetActive(false);
        return clone;
    }

    private GameObject CopyVisualHierarchyRecursive(
        Transform source,
        Transform parent,
        CloneRuntime runtime,
        bool isRoot,
        int cloneIndex
    )
    {
        GameObject copy = new GameObject(source.name);
        copy.hideFlags = HideFlags.DontSave;
        Transform copyTransform = copy.transform;
        copyTransform.SetParent(parent, false);

        if (isRoot)
        {
            copyTransform.localPosition = Vector3.zero;
            copyTransform.localRotation = Quaternion.identity;
            copyTransform.localScale = Vector3.one;
        }
        else
        {
            copyTransform.localPosition = source.localPosition;
            copyTransform.localRotation = source.localRotation;
            copyTransform.localScale = source.localScale;
        }

        SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
        bool shadowObject = IsShadowObject(source, sourceRenderer);

        // Alpha 0 must mean truly invisible. Some custom sprite materials do
        // not respect vertex alpha exactly as Sprites/Default does, so when a
        // clone shadow is disabled/zero-opacity we do not create its renderer
        // at all. This also prevents four overlapping shadows from becoming a
        // dark duplicate silhouette.
        bool shadowShouldRender = profile == null ||
                                  (profile.includeShadowVisuals &&
                                   profile.cloneShadowOpacity > 0.001f);
        bool allowRenderer = !shadowObject || shadowShouldRender;

        if (sourceRenderer != null && allowRenderer)
        {
            SpriteRenderer targetRenderer =
                copy.AddComponent<SpriteRenderer>();
            CopySpriteRenderer(
                sourceRenderer,
                targetRenderer,
                cloneIndex,
                shadowObject
            );
        }

        SortingGroup sourceGroup = source.GetComponent<SortingGroup>();

        if (sourceGroup != null)
        {
            SortingGroup targetGroup = copy.AddComponent<SortingGroup>();
            targetGroup.sortingLayerID = sourceGroup.sortingLayerID;
            targetGroup.sortingOrder = ResolveCloneSortingOrder(
                sourceGroup.sortingOrder,
                cloneIndex
            );
        }

        Animator[] sourceAnimators = source.GetComponents<Animator>();

        for (int i = 0; i < sourceAnimators.Length; i++)
        {
            Animator targetAnimator = copy.AddComponent<Animator>();
            runtime.animators.Add(new AnimatorCopy
            {
                source = sourceAnimators[i],
                clone = targetAnimator
            });
        }

        for (int i = 0; i < source.childCount; i++)
        {
            CopyVisualHierarchyRecursive(
                source.GetChild(i),
                copyTransform,
                runtime,
                false,
                cloneIndex
            );
        }

        copy.SetActive(source.gameObject.activeSelf || isRoot);
        return copy;
    }

    private bool IsShadowObject(
        Transform source,
        SpriteRenderer sourceRenderer
    )
    {
        if (sourceRenderer != null &&
            sourceShadowRenderers.Contains(sourceRenderer))
        {
            return true;
        }

        // Keep the old convention as a fallback for prefabs that do use a
        // clearly named Shadow child but have no SpriteShadow component.
        string lowerName = source != null
            ? source.name.ToLowerInvariant()
            : string.Empty;
        return lowerName.Contains("shadow");
    }

    private void CopySpriteRenderer(
        SpriteRenderer source,
        SpriteRenderer target,
        int cloneIndex,
        bool isShadow
    )
    {
        target.sprite = source.sprite;
        target.enabled = source.enabled;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.tileMode = source.tileMode;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.maskInteraction = source.maskInteraction;
        target.spriteSortPoint = source.spriteSortPoint;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = ResolveCloneSortingOrder(
            source.sortingOrder,
            cloneIndex
        );

        // The real fish may already be faded to alpha 0 by the normal death
        // sequence, so normal clone visuals intentionally restore their body
        // opacity. Shadows are handled separately with a low absolute alpha;
        // this fixes the old behaviour where cloneOpacity made every shadow
        // fully opaque and four overlapping shadows became almost black.
        Color color = source.color;

        if (isShadow && profile != null)
        {
            color.a = Mathf.Clamp01(
                profile.cloneShadowOpacity * profile.cloneOpacity
            );
        }
        else
        {
            color.a = profile != null
                ? Mathf.Clamp01(profile.cloneOpacity)
                : Mathf.Clamp01(source.color.a);
        }

        target.color = color;

        // Guaranteed hide for zero-opacity shadows, including custom materials
        // whose shader ignores SpriteRenderer color alpha.
        if (isShadow && profile != null &&
            profile.cloneShadowOpacity <= 0.001f)
        {
            target.enabled = false;
        }
    }

    private int ResolveCloneSortingOrder(int sourceOrder, int cloneIndex)
    {
        if (profile == null || !profile.useFishSorting)
        {
            return sourceOrder;
        }

        return sourceOrder + profile.cloneSortingOrderOffset +
               profile.cloneSortingOrderStep * cloneIndex;
    }

    private void PrepareActiveClones()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        float step = 360f / Mathf.Max(1, count);

        for (int i = 0; i < clonePool.Count; i++)
        {
            CloneRuntime clone = clonePool[i];

            if (clone == null || clone.motionRoot == null)
            {
                continue;
            }

            if (i >= count)
            {
                clone.motionRoot.SetActive(false);
                continue;
            }

            FishCloneCinematicSlot slot = profile.GetCloneSettings(i);
            float autoAngle = profile.formationStartingAngle - step * i;
            bool useSlotAngle = slot != null &&
                (slot.overrideFormationAngle ||
                 !profile.autoDistributeClonesAroundCircle);
            clone.formationAngle = useSlotAngle
                ? slot.formationAngleDegrees
                : autoAngle;
            clone.individualSpinAngle = 0f;
            clone.originalWorldRotation = sourceOriginalWorldRotation;
            clone.arrived = false;
            clone.arrivalTime = 0f;

            float slotScale = slot != null
                ? Mathf.Max(0.01f, slot.scaleMultiplier)
                : 1f;
            clone.baseScale = sourceVisualBaseScale *
                              profile.baseCloneScaleMultiplier *
                              slotScale;

            clone.motionTransform.SetParent(formationRoot, false);
            clone.motionTransform.localPosition = Vector3.zero;
            clone.motionTransform.localRotation = Quaternion.identity;
            clone.motionTransform.localScale = clone.baseScale;
            clone.motionRoot.SetActive(true);

            RestoreCloneRendererColors(clone);
            ConfigureCloneAnimators(clone);
        }
    }

    private void CacheCloneRendererColors(CloneRuntime clone)
    {
        if (clone == null || clone.visualRoot == null)
        {
            return;
        }

        clone.visualRenderers =
            clone.visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        clone.baseRendererColors =
            new Color[clone.visualRenderers.Length];
        clone.baseRendererEnabled =
            new bool[clone.visualRenderers.Length];

        for (int i = 0; i < clone.visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = clone.visualRenderers[i];
            Color color = renderer != null ? renderer.color : Color.white;
            clone.baseRendererColors[i] = color;
            clone.baseRendererEnabled[i] =
                renderer != null && renderer.enabled;
        }
    }

    private void RestoreCloneRendererColors(CloneRuntime clone)
    {
        if (clone == null || clone.visualRenderers == null)
        {
            return;
        }

        for (int i = 0; i < clone.visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = clone.visualRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            Color color = clone.baseRendererColors != null &&
                          i < clone.baseRendererColors.Length
                ? clone.baseRendererColors[i]
                : renderer.color;
            renderer.color = color;
            renderer.enabled = clone.baseRendererEnabled != null &&
                               i < clone.baseRendererEnabled.Length
                ? clone.baseRendererEnabled[i]
                : true;
        }
    }

    private void ApplyFinalMergeTint(CloneRuntime clone, float normalized)
    {
        if (profile == null ||
            !profile.enableFinalMergeColorTint ||
            clone == null ||
            clone.visualRenderers == null)
        {
            return;
        }

        float curve = EvaluateCurve(
            profile.finalMergeTintCurve,
            normalized
        );
        float strength = Mathf.Clamp01(
            profile.finalMergeTintStrength * curve
        );

        for (int i = 0; i < clone.visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = clone.visualRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            Color baseColor = clone.baseRendererColors != null &&
                              i < clone.baseRendererColors.Length
                ? clone.baseRendererColors[i]
                : renderer.color;
            Color tint = profile.finalMergeTintColor;
            tint.a = baseColor.a;
            Color result = Color.Lerp(baseColor, tint, strength);
            result.a = baseColor.a;
            renderer.color = result;
        }
    }

    private void ConfigureCloneAnimators(CloneRuntime runtime)
    {
        for (int i = 0; i < runtime.animators.Count; i++)
        {
            AnimatorCopy pair = runtime.animators[i];

            if (pair == null || pair.clone == null)
            {
                continue;
            }

            Animator source = pair.source;
            Animator clone = pair.clone;

            clone.enabled = true;

            if (source == null || source.runtimeAnimatorController == null)
            {
                clone.runtimeAnimatorController = null;
                pair.playbackSpeed = Mathf.Max(
                    0.01f,
                    profile.animatorSpeedMultiplier
                );
                clone.speed = profile.keepAnimatorPlaying
                    ? pair.playbackSpeed
                    : 0f;
                continue;
            }

            clone.runtimeAnimatorController =
                source.runtimeAnimatorController;
            clone.avatar = source.avatar;
            clone.applyRootMotion = false;
            clone.updateMode = source.updateMode;
            clone.cullingMode = profile.forceAnimatorAlwaysAnimate
                ? AnimatorCullingMode.AlwaysAnimate
                : source.cullingMode;

            pair.playbackSpeed = Mathf.Max(
                0.01f,
                profile.animatorSpeedMultiplier
            );
            clone.speed = profile.keepAnimatorPlaying
                ? pair.playbackSpeed
                : 0f;

            clone.Rebind();
            clone.Update(0f);
            CopyAnimatorParameters(source, clone);

            switch (profile.animatorStartMode)
            {
                case FishCloneAnimatorStartMode.CopyCurrentState:
                    CopyAnimatorStates(source, clone);
                    break;

                case FishCloneAnimatorStartMode.PlaySpecificState:
                    if (!string.IsNullOrWhiteSpace(
                            profile.specificAnimatorStateName))
                    {
                        int layer = Mathf.Clamp(
                            profile.specificAnimatorLayer,
                            0,
                            Mathf.Max(0, clone.layerCount - 1)
                        );
                        clone.Play(
                            profile.specificAnimatorStateName,
                            layer,
                            Mathf.Clamp01(
                                profile.specificAnimatorNormalizedTime
                            )
                        );
                        clone.Update(0f);
                    }
                    break;

                case FishCloneAnimatorStartMode.RestartDefaultState:
                default:
                    break;
            }
        }
    }

    private static void CopyAnimatorParameters(
        Animator source,
        Animator target
    )
    {
        AnimatorControllerParameter[] parameters = source.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                    target.SetBool(
                        parameter.nameHash,
                        source.GetBool(parameter.nameHash)
                    );
                    break;

                case AnimatorControllerParameterType.Float:
                    target.SetFloat(
                        parameter.nameHash,
                        source.GetFloat(parameter.nameHash)
                    );
                    break;

                case AnimatorControllerParameterType.Int:
                    target.SetInteger(
                        parameter.nameHash,
                        source.GetInteger(parameter.nameHash)
                    );
                    break;
            }
        }
    }

    private static void CopyAnimatorStates(Animator source, Animator target)
    {
        int layerCount = Mathf.Min(source.layerCount, target.layerCount);

        for (int layer = 0; layer < layerCount; layer++)
        {
            target.SetLayerWeight(layer, source.GetLayerWeight(layer));
            AnimatorStateInfo info = source.GetCurrentAnimatorStateInfo(layer);

            if (info.fullPathHash == 0 ||
                !target.HasState(layer, info.fullPathHash))
            {
                continue;
            }

            target.Play(
                info.fullPathHash,
                layer,
                info.normalizedTime
            );
        }

        target.Update(0f);
    }

    private IEnumerator RunSequence()
    {
        StartCinematicBackground();

        if (profile.hideOriginalFishDuringCinematic)
        {
            SetOwnerRenderersVisible(false);
        }

        yield return RunFormationBuild();
        yield return RunSpinWait();
        yield return RunAggressiveOutAndBack();
        yield return RunSeparation();
        yield return RunFinalMerge();

        Vector3 finalCenter = ResolveFinalCenterWorld();
        PlayFinalEffects(finalCenter);
        ApplyFinalAreaDamage(finalCenter);

        if (profile.hideClonesAfterBoomDelay > 0f)
        {
            yield return Wait(profile.hideClonesAfterBoomDelay);
        }

        HideAllClones();

        if (profile.cleanupDelay > 0f)
        {
            yield return Wait(profile.cleanupDelay);
        }

        StopCinematicBackground();
        sequenceRoutine = null;
        sequenceRunning = false;
    }

    private IEnumerator RunFormationBuild()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        Vector3 center = ResolveFormationCenterWorld();
        formationRoot.position = center;
        formationRoot.rotation = Quaternion.Euler(
            0f,
            0f,
            profile.formationRotationStartingAngle
        );
        formationRoot.localScale = Vector3.one;

        float radius = ResolveFormationRadiusWorld(center);
        float duration = Mathf.Max(0f, profile.formationBuildDuration);
        float elapsed = 0f;

        SetAnimatorStagePlaying(profile.keepAnimatorPlayingDuringFormation);

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float normalized = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(
                profile.formationBuildCurve,
                normalized
            );

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                Vector3 targetLocal = DirectionFromDegrees(
                    clone.formationAngle
                ) * radius;
                clone.motionTransform.localPosition = Vector3.LerpUnclamped(
                    Vector3.zero,
                    targetLocal,
                    curved
                );
                UpdateFormationFacing(clone, Vector3.zero, false);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            clone.motionTransform.localPosition =
                DirectionFromDegrees(clone.formationAngle) * radius;
            UpdateFormationFacing(clone, Vector3.zero, false);
        }
    }

    private IEnumerator RunSpinWait()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        SetAnimatorStagePlaying(profile.keepAnimatorPlayingDuringSpin);

        float rotationWindow =
            profile.formationRotationEnabled &&
            !profile.continueFormationRotationUntilNextStage
                ? profile.formationRotationDuration
                : 0f;
        float stageDuration = Mathf.Max(
            profile.spinWaitDuration,
            rotationWindow
        );

        if (stageDuration <= 0f)
        {
            stageDuration = 0.001f;
        }

        float elapsed = 0f;
        float formationSignedRotation =
            profile.formationRotationStartingAngle;
        float accumulatedAbsoluteRotation = 0f;
        float formationDirection = profile.formationClockwise ? -1f : 1f;

        while (elapsed < stageDuration)
        {
            float dt = DeltaTime;
            elapsed += dt;

            bool timedRotationActive =
                profile.continueFormationRotationUntilNextStage ||
                (profile.formationRotationDuration > 0f &&
                 elapsed <= profile.formationRotationDuration);

            if (profile.formationRotationEnabled && timedRotationActive)
            {
                float delta = profile.formationRotationSpeed * dt;

                if (profile.limitFormationRotationByTurns)
                {
                    float maximumDegrees = Mathf.Max(
                        0f,
                        profile.maximumFormationTurns
                    ) * 360f;
                    delta = Mathf.Min(
                        delta,
                        Mathf.Max(
                            0f,
                            maximumDegrees - accumulatedAbsoluteRotation
                        )
                    );
                }

                accumulatedAbsoluteRotation += Mathf.Abs(delta);
                formationSignedRotation += delta * formationDirection;
                formationRoot.rotation = Quaternion.Euler(
                    0f,
                    0f,
                    formationSignedRotation
                );
            }

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                AdvanceIndividualSpin(
                    clone,
                    dt,
                    profile.individualFishRotationEnabled
                );
                UpdateFormationFacing(clone, Vector3.zero, true);
            }

            yield return null;
        }

        DetachActiveClonesFromFormation();
    }

    private IEnumerator RunAggressiveOutAndBack()
    {
        if (profile == null ||
            !profile.aggressiveOutAndBackEnabled ||
            profile.aggressiveOutAndBackRepeats <= 0)
        {
            yield break;
        }

        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        Vector3 center = ResolveFormationCenterWorld();
        Vector3[] homePositions = new Vector3[count];
        Vector3[] legStarts = new Vector3[count];
        Vector3[] legTargets = new Vector3[count];
        float[] legDurations = new float[count];

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            homePositions[i] = clone.motionTransform.position;
            clone.motionTransform.localScale = clone.baseScale;
        }

        SetAnimatorStagePlaying(
            profile.keepAnimatorPlayingDuringAggressiveDash,
            profile.aggressiveAnimatorSpeedMultiplier
        );

        int repeats = Mathf.Clamp(
            profile.aggressiveOutAndBackRepeats,
            1,
            8
        );

        for (int repeat = 0; repeat < repeats; repeat++)
        {
            float repeatSpeedMultiplier = Mathf.Pow(
                Mathf.Max(1f, profile.aggressiveRepeatSpeedMultiplier),
                repeat
            );

            // Small inward cocking motion before the launch. This gives the
            // whale a forceful "angry stride" instead of a soft UI tween.
            if (profile.aggressiveWindUpDuration > 0f &&
                profile.aggressiveWindUpFormationRadiusMultiplier > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    CloneRuntime clone = clonePool[i];
                    legStarts[i] = clone.motionTransform.position;
                    legTargets[i] = Vector3.Lerp(
                        homePositions[i],
                        center,
                        Mathf.Clamp01(
                            profile.aggressiveWindUpFormationRadiusMultiplier
                        )
                    );
                    legDurations[i] = profile.aggressiveWindUpDuration;
                }

                yield return RunAggressiveMovementLeg(
                    legStarts,
                    legTargets,
                    legDurations,
                    null,
                    false
                );
            }

            // Launch completely beyond the camera viewport.
            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                legStarts[i] = clone.motionTransform.position;
                legTargets[i] = ResolveAggressiveOffscreenTargetWorld(
                    center,
                    clone,
                    profile.aggressiveOutScreenViewportPadding
                );

                float distance = Vector3.Distance(
                    legStarts[i],
                    legTargets[i]
                );
                float speed = Mathf.Max(
                    0.01f,
                    profile.aggressiveOutboundSpeed *
                    repeatSpeedMultiplier
                );
                legDurations[i] = Mathf.Max(0.01f, distance / speed);
            }

            yield return RunAggressiveMovementLeg(
                legStarts,
                legTargets,
                legDurations,
                profile.aggressiveOutboundCurve,
                true
            );

            if (profile.aggressiveOutsideHoldDuration > 0f)
            {
                yield return Wait(profile.aggressiveOutsideHoldDuration);
            }

            // Attack back inward to the same spun formation position.
            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                legStarts[i] = clone.motionTransform.position;
                legTargets[i] = homePositions[i];

                float distance = Vector3.Distance(
                    legStarts[i],
                    legTargets[i]
                );
                float speed = Mathf.Max(
                    0.01f,
                    profile.aggressiveReturnSpeed *
                    repeatSpeedMultiplier
                );
                legDurations[i] = Mathf.Max(0.01f, distance / speed);
            }

            yield return RunAggressiveMovementLeg(
                legStarts,
                legTargets,
                legDurations,
                profile.aggressiveReturnCurve,
                true
            );

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                clone.motionTransform.position = homePositions[i];
                clone.motionTransform.localScale = clone.baseScale;
            }
        }
    }

    private IEnumerator RunAggressiveMovementLeg(
        Vector3[] starts,
        Vector3[] targets,
        float[] durations,
        AnimationCurve curve,
        bool useLungeScale
    )
    {
        int count = Mathf.Min(
            Mathf.Clamp(profile.cloneCount, 1, clonePool.Count),
            Mathf.Min(starts.Length, targets.Length)
        );
        float maximumDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            maximumDuration = Mathf.Max(
                maximumDuration,
                i < durations.Length ? durations[i] : 0f
            );
        }

        if (maximumDuration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < maximumDuration)
        {
            float dt = DeltaTime;
            elapsed += dt;

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                float duration = Mathf.Max(
                    0.001f,
                    i < durations.Length ? durations[i] : maximumDuration
                );
                float normalized = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null
                    ? EvaluateCurve(curve, normalized)
                    : Mathf.SmoothStep(0f, 1f, normalized);

                Vector3 previous = clone.motionTransform.position;
                Vector3 next = Vector3.LerpUnclamped(
                    starts[i],
                    targets[i],
                    curved
                );
                clone.motionTransform.position = next;

                if (profile.aggressiveContinueIndividualRotation)
                {
                    AdvanceIndividualSpin(clone, dt, true);
                }

                if (profile.aggressiveFaceMovementDirection)
                {
                    Vector3 movement = next - previous;

                    if (movement.sqrMagnitude > 0.000001f)
                    {
                        float angle = Mathf.Atan2(
                            movement.y,
                            movement.x
                        ) * Mathf.Rad2Deg;
                        ApplyWorldFacing(clone, angle, true);
                    }
                }

                if (useLungeScale &&
                    profile.aggressiveLungeScaleStrength > 0f)
                {
                    float punch = Mathf.Sin(normalized * Mathf.PI);
                    float strength =
                        profile.aggressiveLungeScaleStrength * punch;
                    Vector3 scale = clone.baseScale;
                    scale.x *= 1f + strength;
                    scale.y *= 1f - strength * 0.35f;
                    clone.motionTransform.localScale = scale;
                }
                else
                {
                    clone.motionTransform.localScale = clone.baseScale;
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            clone.motionTransform.position = targets[i];
            clone.motionTransform.localScale = clone.baseScale;
        }
    }

    private IEnumerator RunSeparation()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        SetAnimatorStagePlaying(profile.keepAnimatorPlayingDuringSeparation);

        Vector3[] starts = new Vector3[count];
        float[] startRotations = new float[count];
        float maximumMoveDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            FishCloneCinematicSlot slot = profile.GetCloneSettings(i);
            clone.destination = ResolveCloneDestinationWorld(i, slot);
            starts[i] = clone.motionTransform.position;
            startRotations[i] = clone.motionTransform.eulerAngles.z;
            clone.moveDuration = ResolveSeparationDuration(
                Vector3.Distance(starts[i], clone.destination),
                slot
            );
            clone.arrived = false;
            clone.arrivalTime = 0f;
            maximumMoveDuration = Mathf.Max(
                maximumMoveDuration,
                clone.moveDuration
            );
        }

        float elapsed = 0f;

        while (elapsed < maximumMoveDuration)
        {
            float dt = DeltaTime;
            elapsed += dt;

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];

                if (clone.arrived)
                {
                    if (profile.continueIndividualRotationAtDestination)
                    {
                        float baseAngle = clone.motionTransform.eulerAngles.z +
                                          profile.spriteForwardAngleDegrees -
                                          clone.individualSpinAngle;
                        AdvanceIndividualSpin(clone, dt, true);
                        ApplyWorldFacing(
                            clone,
                            baseAngle,
                            true
                        );
                    }

                    continue;
                }

                float duration = Mathf.Max(0.001f, clone.moveDuration);
                float normalized = Mathf.Clamp01(elapsed / duration);
                float curved = EvaluateCurve(
                    profile.separationMovementCurve,
                    normalized
                );

                Vector3 previous = clone.motionTransform.position;
                Vector3 next = Vector3.LerpUnclamped(
                    starts[i],
                    clone.destination,
                    curved
                );
                clone.motionTransform.position = next;

                if (profile.continueIndividualRotationDuringSeparation)
                {
                    AdvanceIndividualSpin(clone, dt, true);
                }

                if (profile.rotateWhileMoving)
                {
                    Vector3 movement = next - previous;
                    float angle = movement.sqrMagnitude > 0.000001f
                        ? Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg
                        : startRotations[i] +
                          profile.spriteForwardAngleDegrees;
                    ApplyWorldFacing(clone, angle, true);
                }
                else if (profile.continueIndividualRotationDuringSeparation)
                {
                    ApplyWorldFacing(
                        clone,
                        startRotations[i] +
                        profile.spriteForwardAngleDegrees,
                        true
                    );
                }

                if (normalized >= 1f)
                {
                    clone.motionTransform.position = clone.destination;
                    clone.arrived = true;
                    clone.arrivalTime = CurrentTime;
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            clone.motionTransform.position = clone.destination;

            if (!clone.arrived)
            {
                clone.arrived = true;
                clone.arrivalTime = CurrentTime;
            }
        }

        SetAnimatorStagePlaying(profile.keepAnimatorPlayingAtDestination);

        if (profile.waitForAllClones)
        {
            bool allReady = false;

            while (!allReady)
            {
                float now = CurrentTime;
                float dt = DeltaTime;
                allReady = true;

                for (int i = 0; i < count; i++)
                {
                    FishCloneCinematicSlot slot = profile.GetCloneSettings(i);
                    float hold = slot != null
                        ? Mathf.Max(0f, slot.destinationWaitDuration)
                        : 0f;

                    if (now < clonePool[i].arrivalTime + hold)
                    {
                        allReady = false;
                    }

                    if (profile.continueIndividualRotationAtDestination)
                    {
                        CloneRuntime clone = clonePool[i];
                        float baseAngle = clone.motionTransform.eulerAngles.z +
                                          profile.spriteForwardAngleDegrees -
                                          clone.individualSpinAngle;
                        AdvanceIndividualSpin(clone, dt, true);
                        ApplyWorldFacing(clone, baseAngle, true);
                    }
                }

                if (!allReady)
                {
                    yield return null;
                }
            }

            if (profile.delayAfterAllClonesReady > 0f)
            {
                yield return WaitWithDestinationRotation(
                    profile.delayAfterAllClonesReady
                );
            }
        }
        else if (profile.delayAfterLastCloneArrives > 0f)
        {
            yield return WaitWithDestinationRotation(
                profile.delayAfterLastCloneArrives
            );
        }
    }

    private IEnumerator RunFinalMerge()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);
        SetAnimatorStagePlaying(profile.keepAnimatorPlayingDuringFinalMerge);

        Vector3 center = ResolveFinalCenterWorld();
        Vector3[] starts = new Vector3[count];
        Vector3[] startScales = new Vector3[count];
        float[] baseRotations = new float[count];
        float maximumDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            FishCloneCinematicSlot slot = profile.GetCloneSettings(i);
            starts[i] = clone.motionTransform.position;
            startScales[i] = clone.motionTransform.localScale;
            baseRotations[i] = clone.motionTransform.eulerAngles.z +
                               profile.spriteForwardAngleDegrees -
                               clone.individualSpinAngle;
            clone.finalMoveDuration = ResolveFinalMoveDuration(
                Vector3.Distance(starts[i], center),
                slot
            );
            maximumDuration = Mathf.Max(
                maximumDuration,
                clone.finalMoveDuration
            );
        }

        float elapsed = 0f;

        while (elapsed < maximumDuration)
        {
            float dt = DeltaTime;
            elapsed += dt;

            for (int i = 0; i < count; i++)
            {
                CloneRuntime clone = clonePool[i];
                float duration = Mathf.Max(0.001f, clone.finalMoveDuration);
                float normalized = Mathf.Clamp01(elapsed / duration);
                float curved = EvaluateCurve(
                    profile.finalCenterMovementCurve,
                    normalized
                );

                clone.motionTransform.position = Vector3.LerpUnclamped(
                    starts[i],
                    center,
                    curved
                );

                float scaleCurve = EvaluateCurve(
                    profile.finalScaleCurve,
                    normalized
                );
                Vector3 targetScale = clone.baseScale *
                                      profile.finalScaleMultiplier;
                clone.motionTransform.localScale = Vector3.LerpUnclamped(
                    startScales[i],
                    targetScale,
                    scaleCurve
                );
                ApplyFinalMergeTint(clone, normalized);

                float baseAngle = baseRotations[i];

                if (profile.faceFinalCenterWhileMoving)
                {
                    Vector3 direction = center - clone.motionTransform.position;

                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        baseAngle = Mathf.Atan2(
                            direction.y,
                            direction.x
                        ) * Mathf.Rad2Deg;
                    }
                }

                if (profile.continueIndividualRotationDuringFinalMerge)
                {
                    AdvanceIndividualSpin(clone, dt, true);
                }

                if (profile.finalRotationEnabled)
                {
                    float sign = profile.finalRotationClockwise ? -1f : 1f;
                    clone.individualSpinAngle +=
                        profile.finalRotationSpeed * sign * dt;
                }

                ApplyWorldFacing(clone, baseAngle, true);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];
            clone.motionTransform.position = center;
            clone.motionTransform.localScale = clone.baseScale *
                                               profile.finalScaleMultiplier;
        }
    }

    private IEnumerator WaitWithDestinationRotation(float duration)
    {
        float elapsed = 0f;
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);

        while (elapsed < duration)
        {
            float dt = DeltaTime;
            elapsed += dt;

            if (profile.continueIndividualRotationAtDestination)
            {
                for (int i = 0; i < count; i++)
                {
                    CloneRuntime clone = clonePool[i];
                    float baseAngle = clone.motionTransform.eulerAngles.z +
                                      profile.spriteForwardAngleDegrees -
                                      clone.individualSpinAngle;
                    AdvanceIndividualSpin(clone, dt, true);
                    ApplyWorldFacing(clone, baseAngle, true);
                }
            }

            yield return null;
        }
    }

    private void SetAnimatorStagePlaying(
        bool stageAllowsPlayback,
        float speedMultiplier = 1f
    )
    {
        int count = Mathf.Clamp(
            profile != null ? profile.cloneCount : 0,
            0,
            clonePool.Count
        );
        bool shouldPlay = profile != null &&
                          profile.keepAnimatorPlaying &&
                          stageAllowsPlayback;

        for (int i = 0; i < count; i++)
        {
            CloneRuntime clone = clonePool[i];

            for (int j = 0; j < clone.animators.Count; j++)
            {
                AnimatorCopy pair = clone.animators[j];

                if (pair != null && pair.clone != null)
                {
                    pair.clone.speed = shouldPlay
                        ? pair.playbackSpeed * Mathf.Max(0f, speedMultiplier)
                        : 0f;
                }
            }
        }
    }

    private void AdvanceIndividualSpin(
        CloneRuntime clone,
        float dt,
        bool enabledForStage
    )
    {
        if (!profile.individualFishRotationEnabled || !enabledForStage)
        {
            return;
        }

        int index = clonePool.IndexOf(clone);
        FishCloneCinematicSlot slot = profile.GetCloneSettings(index);
        float speed = slot != null &&
                      slot.individualRotationSpeedOverride >= 0f
            ? slot.individualRotationSpeedOverride
            : profile.individualFishRotationSpeed;
        float sign = profile.individualFishRotationClockwise ? -1f : 1f;
        clone.individualSpinAngle += Mathf.Max(0f, speed) * sign * dt;
    }

    private void UpdateFormationFacing(
        CloneRuntime clone,
        Vector3 movementDirection,
        bool includeIndividualSpin
    )
    {
        Vector3 center = formationRoot != null
            ? formationRoot.position
            : ResolveFormationCenterWorld();
        Vector3 worldPosition = clone.motionTransform.position;
        Vector3 radial = worldPosition - center;
        float targetAngle = clone.originalWorldRotation;

        switch (profile.formationFacing)
        {
            case FishCloneFacingMode.FaceCenter:
                if (radial.sqrMagnitude > 0.000001f)
                {
                    targetAngle = Mathf.Atan2(-radial.y, -radial.x) *
                                  Mathf.Rad2Deg;
                }
                break;

            case FishCloneFacingMode.FaceAwayFromCenter:
                if (radial.sqrMagnitude > 0.000001f)
                {
                    targetAngle = Mathf.Atan2(radial.y, radial.x) *
                                  Mathf.Rad2Deg;
                }
                break;

            case FishCloneFacingMode.TangentClockwise:
                if (radial.sqrMagnitude > 0.000001f)
                {
                    targetAngle = Mathf.Atan2(radial.y, radial.x) *
                                  Mathf.Rad2Deg - 90f;
                }
                break;

            case FishCloneFacingMode.TangentCounterClockwise:
                if (radial.sqrMagnitude > 0.000001f)
                {
                    targetAngle = Mathf.Atan2(radial.y, radial.x) *
                                  Mathf.Rad2Deg + 90f;
                }
                break;

            case FishCloneFacingMode.FaceMovementDirection:
                if (movementDirection.sqrMagnitude > 0.000001f)
                {
                    targetAngle = Mathf.Atan2(
                        movementDirection.y,
                        movementDirection.x
                    ) * Mathf.Rad2Deg;
                }
                else if (radial.sqrMagnitude > 0.000001f)
                {
                    float tangent = profile.formationClockwise
                        ? -90f
                        : 90f;
                    targetAngle = Mathf.Atan2(radial.y, radial.x) *
                                  Mathf.Rad2Deg + tangent;
                }
                break;

            case FishCloneFacingMode.KeepOriginalRotation:
            default:
                targetAngle = clone.originalWorldRotation +
                              profile.spriteForwardAngleDegrees;
                break;
        }

        ApplyWorldFacing(clone, targetAngle, includeIndividualSpin);
    }

    private void ApplyWorldFacing(
        CloneRuntime clone,
        float desiredForwardWorldAngle,
        bool includeIndividualSpin
    )
    {
        float rotation = desiredForwardWorldAngle -
                         profile.spriteForwardAngleDegrees;

        if (includeIndividualSpin)
        {
            rotation += clone.individualSpinAngle;
        }

        clone.motionTransform.rotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private void DetachActiveClonesFromFormation()
    {
        int count = Mathf.Clamp(profile.cloneCount, 1, clonePool.Count);

        for (int i = 0; i < count; i++)
        {
            clonePool[i].motionTransform.SetParent(runtimeRoot, true);
        }
    }

    private float ResolveSeparationDuration(
        float distance,
        FishCloneCinematicSlot slot
    )
    {
        if (slot != null && slot.movementDurationOverride > 0f)
        {
            return Mathf.Max(0.01f, slot.movementDurationOverride);
        }

        if (profile.globalSeparationDurationOverride > 0f)
        {
            return Mathf.Max(
                0.01f,
                profile.globalSeparationDurationOverride
            );
        }

        float speed = slot != null && slot.movementSpeedOverride > 0f
            ? slot.movementSpeedOverride
            : profile.globalSeparationSpeed;

        return Mathf.Max(0.01f, distance / Mathf.Max(0.01f, speed));
    }

    private float ResolveFinalMoveDuration(
        float distance,
        FishCloneCinematicSlot slot
    )
    {
        if (profile.finalCenterDurationOverride > 0f)
        {
            return Mathf.Max(
                0.01f,
                profile.finalCenterDurationOverride
            );
        }

        float speed = slot != null && slot.finalMovementSpeedOverride > 0f
            ? slot.finalMovementSpeedOverride
            : profile.finalCenterMovementSpeed;

        return Mathf.Max(0.01f, distance / Mathf.Max(0.01f, speed));
    }

    private Vector3 ResolveFormationCenterWorld()
    {
        if (formationCenterOverride != null)
        {
            return formationCenterOverride.position;
        }

        return ViewportToWorld(
            profile.formationCenterViewport,
            owner != null ? owner.transform.position : Vector3.zero
        );
    }

    private float ResolveFormationRadiusWorld(Vector3 center)
    {
        if (profile.formationRadiusSpace ==
            FishCloneFormationRadiusSpace.WorldUnits)
        {
            return Mathf.Max(0f, profile.formationRadius);
        }

        Camera camera = ResolveCamera();

        if (camera == null)
        {
            return Mathf.Max(0f, profile.formationRadius);
        }

        Vector2 centerViewport = profile.formationCenterViewport;
        Vector2 upperViewport = centerViewport +
                                Vector2.up * profile.formationRadius;
        Vector3 upperWorld = ViewportToWorld(
            upperViewport,
            center
        );
        return Vector3.Distance(center, upperWorld);
    }

    private Vector3 ResolveCloneDestinationWorld(
        int index,
        FishCloneCinematicSlot slot
    )
    {
        if (cloneDestinationOverrides != null &&
            index >= 0 &&
            index < cloneDestinationOverrides.Length &&
            cloneDestinationOverrides[index] != null)
        {
            return cloneDestinationOverrides[index].position;
        }

        if (slot != null &&
            slot.destinationMode == FishCloneTargetMode.WorldPosition)
        {
            Vector3 world = slot.destinationWorldPosition;
            world.z = owner != null ? owner.transform.position.z : world.z;
            return world;
        }

        Vector2 viewport = slot != null
            ? slot.destinationViewport
            : new Vector2(0.5f, 0.5f);

        return ViewportToWorld(
            viewport,
            owner != null ? owner.transform.position : Vector3.zero
        );
    }

    private Vector3 ResolveAggressiveOffscreenTargetWorld(
        Vector3 centerWorld,
        CloneRuntime clone,
        float viewportPadding
    )
    {
        Vector3 radialWorldPosition = clone != null
            ? clone.motionTransform.position
            : centerWorld + Vector3.right;
        Camera camera = ResolveCamera();
        float padding = Mathf.Clamp(viewportPadding, 0f, 0.50f);

        if (camera == null)
        {
            Vector3 fallbackDirection = radialWorldPosition - centerWorld;

            if (fallbackDirection.sqrMagnitude <= 0.000001f)
            {
                fallbackDirection = Vector3.right;
            }

            return radialWorldPosition +
                   fallbackDirection.normalized *
                   Mathf.Max(1f, ResolveFormationRadiusWorld(centerWorld) * 5f);
        }

        if (profile != null && profile.aggressiveEnsureFullyOffscreen)
        {
            padding += ResolveCloneViewportVisualRadius(clone, camera);
        }

        Vector3 centerViewport3 = camera.WorldToViewportPoint(centerWorld);
        Vector3 radialViewport3 = camera.WorldToViewportPoint(
            radialWorldPosition
        );
        Vector2 centerViewport = new Vector2(
            centerViewport3.x,
            centerViewport3.y
        );
        Vector2 radialViewport = new Vector2(
            radialViewport3.x,
            radialViewport3.y
        );
        Vector2 direction = radialViewport - centerViewport;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();
        float tx = float.PositiveInfinity;
        float ty = float.PositiveInfinity;

        if (Mathf.Abs(direction.x) > 0.00001f)
        {
            float xBoundary = direction.x > 0f
                ? 1f + padding
                : -padding;
            float candidate =
                (xBoundary - centerViewport.x) / direction.x;

            if (candidate > 0f)
            {
                tx = candidate;
            }
        }

        if (Mathf.Abs(direction.y) > 0.00001f)
        {
            float yBoundary = direction.y > 0f
                ? 1f + padding
                : -padding;
            float candidate =
                (yBoundary - centerViewport.y) / direction.y;

            if (candidate > 0f)
            {
                ty = candidate;
            }
        }

        float travel = Mathf.Min(tx, ty);

        if (float.IsInfinity(travel) || float.IsNaN(travel))
        {
            travel = 1f + padding;
        }

        Vector2 targetViewport = centerViewport + direction * travel;
        return ViewportToWorld(targetViewport, radialWorldPosition);
    }

    private float ResolveCloneViewportVisualRadius(
        CloneRuntime clone,
        Camera camera
    )
    {
        if (clone == null ||
            camera == null ||
            clone.visualRenderers == null ||
            clone.visualRenderers.Length == 0)
        {
            return 0f;
        }

        bool hasBounds = false;
        Bounds combined = new Bounds();

        for (int i = 0; i < clone.visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = clone.visualRenderers[i];

            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return 0f;
        }

        Vector3 pivotViewport = camera.WorldToViewportPoint(
            clone.motionTransform.position
        );
        Vector3 min = combined.min;
        Vector3 max = combined.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, combined.center.z),
            new Vector3(min.x, max.y, combined.center.z),
            new Vector3(max.x, min.y, combined.center.z),
            new Vector3(max.x, max.y, combined.center.z)
        };
        float radius = 0f;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 cornerViewport = camera.WorldToViewportPoint(corners[i]);
            radius = Mathf.Max(
                radius,
                Mathf.Abs(cornerViewport.x - pivotViewport.x),
                Mathf.Abs(cornerViewport.y - pivotViewport.y)
            );
        }

        return Mathf.Clamp(radius, 0f, 0.60f);
    }

    private Vector3 ResolveFinalCenterWorld()
    {
        if (finalCenterOverride != null)
        {
            return finalCenterOverride.position;
        }

        if (profile.finalCenterMode == FishCloneTargetMode.WorldPosition)
        {
            Vector3 world = profile.finalCenterWorldPosition;
            world.z = owner != null ? owner.transform.position.z : world.z;
            return world;
        }

        return ViewportToWorld(
            profile.finalCenterViewport,
            owner != null ? owner.transform.position : Vector3.zero
        );
    }

    private Vector3 ViewportToWorld(
        Vector2 viewport,
        Vector3 referenceWorldPosition
    )
    {
        Camera camera = ResolveCamera();

        if (camera == null)
        {
            return referenceWorldPosition;
        }

        float depth = Vector3.Dot(
            referenceWorldPosition - camera.transform.position,
            camera.transform.forward
        );
        depth = Mathf.Max(0.01f, depth);

        Vector3 result = camera.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, depth)
        );
        result.z = referenceWorldPosition.z;
        return result;
    }

    private Vector3 ResolveBackgroundAnchorWorld()
    {
        if (profile == null)
        {
            return owner != null ? owner.transform.position : Vector3.zero;
        }

        switch (profile.backgroundAnchorMode)
        {
            case FishCloneBackgroundAnchorMode.FinalCenter:
                return ResolveFinalCenterWorld();

            case FishCloneBackgroundAnchorMode.NormalizedViewport:
                return ViewportToWorld(
                    profile.backgroundViewportPosition,
                    owner != null
                        ? owner.transform.position
                        : Vector3.zero
                );

            case FishCloneBackgroundAnchorMode.WorldPosition:
            {
                Vector3 world = profile.backgroundWorldPosition;
                if (owner != null)
                {
                    world.z = owner.transform.position.z;
                }
                return world;
            }

            case FishCloneBackgroundAnchorMode.FormationCenter:
            default:
                return ResolveFormationCenterWorld();
        }
    }

    private void StartCinematicBackground()
    {
        StopCinematicBackground();

        if (profile == null ||
            !profile.enableBackgroundEffect ||
            profile.backgroundPlayTiming !=
                FishCloneBackgroundPlayTiming.WholeCinematic ||
            profile.finalBackgroundEffectPrefab == null)
        {
            return;
        }

        CacheReferences();
        AnimatiorManager manager = gameManager != null
            ? gameManager.animatiorManager
            : null;

        if (manager == null)
        {
            return;
        }

        int sortingLayerId;
        int sortingOrder;
        GetOwnerSorting(out sortingLayerId, out sortingOrder);

        Vector3 position = ResolveBackgroundAnchorWorld() +
                           (Vector3)profile.finalBackgroundEffectOffset;

        backgroundEffectInstance =
            manager.PlayPersistentPooledEffectAdvanced(
                profile.finalBackgroundEffectPrefab,
                position,
                profile.finalBackgroundEffectRotationDegrees,
                ToScale(profile.finalBackgroundEffectScale),
                AddSortingOffset(
                    sortingOrder,
                    profile.finalBackgroundSortingOrderOffset
                ),
                sortingLayerId
            );

        backgroundRotationElapsed = 0f;
        backgroundAccumulatedAbsoluteRotation = 0f;
    }

    private void UpdateCinematicBackground()
    {
        if (!sequenceRunning ||
            profile == null ||
            backgroundEffectInstance == null ||
            !backgroundEffectInstance.activeInHierarchy)
        {
            return;
        }

        if (profile.backgroundFollowAnchor)
        {
            backgroundEffectInstance.transform.position =
                ResolveBackgroundAnchorWorld() +
                (Vector3)profile.finalBackgroundEffectOffset;
        }

        if (!profile.backgroundRotationEnabled)
        {
            return;
        }

        float dt = DeltaTime;
        backgroundRotationElapsed += dt;

        bool rotationActive =
            profile.continueBackgroundRotationUntilCinematicEnds ||
            (profile.backgroundRotationDuration > 0f &&
             backgroundRotationElapsed <=
                profile.backgroundRotationDuration);

        if (!rotationActive)
        {
            return;
        }

        float delta = Mathf.Max(0f, profile.backgroundRotationSpeed) * dt;

        if (profile.limitBackgroundRotationByTurns)
        {
            float maximumDegrees =
                Mathf.Max(0f, profile.maximumBackgroundTurns) * 360f;
            delta = Mathf.Min(
                delta,
                Mathf.Max(
                    0f,
                    maximumDegrees -
                    backgroundAccumulatedAbsoluteRotation
                )
            );
        }

        if (delta <= 0f)
        {
            return;
        }

        backgroundAccumulatedAbsoluteRotation += Mathf.Abs(delta);
        float direction = profile.backgroundRotationClockwise ? -1f : 1f;
        backgroundEffectInstance.transform.Rotate(
            0f,
            0f,
            delta * direction,
            Space.Self
        );
    }

    private void StopCinematicBackground()
    {
        if (backgroundEffectInstance != null)
        {
            CacheReferences();
            AnimatiorManager manager = gameManager != null
                ? gameManager.animatiorManager
                : null;

            if (manager != null)
            {
                manager.StopPersistentPooledEffect(
                    backgroundEffectInstance
                );
            }
            else
            {
                backgroundEffectInstance.SetActive(false);
            }
        }

        backgroundEffectInstance = null;
        backgroundRotationElapsed = 0f;
        backgroundAccumulatedAbsoluteRotation = 0f;
    }

    private void PlayFinalEffects(Vector3 center)
    {
        CacheReferences();
        AnimatiorManager manager = gameManager != null
            ? gameManager.animatiorManager
            : null;

        int sortingLayerId;
        int sortingOrder;
        GetOwnerSorting(out sortingLayerId, out sortingOrder);

        if (profile.enableBackgroundEffect &&
            profile.backgroundPlayTiming ==
                FishCloneBackgroundPlayTiming.FinalBoomOnly &&
            profile.finalBackgroundEffectPrefab != null &&
            manager != null)
        {
            manager.PlayPooledEffectAdvanced(
                profile.finalBackgroundEffectPrefab,
                center + (Vector3)profile.finalBackgroundEffectOffset,
                profile.finalBackgroundEffectRotationDegrees,
                ToScale(profile.finalBackgroundEffectScale),
                profile.finalBackgroundEffectVisibleDuration,
                profile.finalBackgroundEffectHideDelay,
                AddSortingOffset(
                    sortingOrder,
                    profile.finalBackgroundSortingOrderOffset
                ),
                sortingLayerId
            );
        }

        if (profile.playIndexedNetBoom && manager != null)
        {
            manager.PlayNetBoomAdvanced(
                center,
                activeBulletId,
                owner != null ? owner.id : 0,
                profile.finalNetBoomEffectIndex,
                Vector3.one * profile.finalNetBoomScaleMultiplier,
                sortingLayerId,
                AddSortingOffset(
                    sortingOrder,
                    profile.finalNetBoomSortingOrderOffset
                )
            );
        }

        if (profile.finalBoomPrefab != null && manager != null)
        {
            manager.PlayPooledEffectAdvanced(
                profile.finalBoomPrefab,
                center + (Vector3)profile.finalBoomSpawnOffset,
                profile.finalBoomRotationDegrees,
                ToScale(profile.finalBoomScale),
                profile.finalBoomVisibleDuration,
                profile.finalBoomHideDelay,
                AddSortingOffset(
                    sortingOrder,
                    profile.finalBoomSortingOrderOffset
                ),
                sortingLayerId
            );
        }

        if (profile.additionalFinalEffects != null && manager != null)
        {
            for (int i = 0; i < profile.additionalFinalEffects.Length; i++)
            {
                FishCloneCinematicFinalEffect effect =
                    profile.additionalFinalEffects[i];

                if (effect == null || effect.prefab == null)
                {
                    continue;
                }

                int effectLayer = effect.useFishSorting
                    ? sortingLayerId
                    : int.MinValue;
                int effectOrder = effect.useFishSorting
                    ? AddSortingOffset(
                        sortingOrder,
                        effect.sortingOrderOffset
                    )
                    : int.MinValue;

                manager.PlayPooledEffectAdvanced(
                    effect.prefab,
                    center + (Vector3)effect.positionOffset,
                    effect.rotationDegrees,
                    ToScale(effect.scale),
                    effect.visibleDuration,
                    effect.hideDelay,
                    effectOrder,
                    effectLayer
                );
            }
        }

        if (profile.playFinalNetBoomSound &&
            gameManager != null &&
            gameManager.SoundManager != null)
        {
            gameManager.SoundManager.PlayNetBoomSound(
                profile.finalNetBoomSoundIndex
            );
        }

        if (profile.playFinalCameraShake &&
            profile.finalCameraShakeStrength > 0f &&
            profile.finalCameraShakeDuration > 0f &&
            gameManager != null)
        {
            gameManager.PlayEarthquake(
                profile.finalCameraShakeDuration,
                profile.finalCameraShakeStrength
            );
        }
    }

    private void ApplyFinalAreaDamage(Vector3 center)
    {
        CinematicAreaDamageSettings settings = profile != null
            ? profile.finalAreaDamage
            : null;

        if (settings == null || !settings.enabled || settings.damage <= 0f)
        {
            return;
        }

        finalDamagedFish.Clear();

        int damageBulletId = owner != null && owner.HasDeathCredit
            ? owner.DeathCreditBulletId
            : activeBulletId;
        int damageGunLevel = owner != null && owner.HasDeathCredit
            ? owner.DeathCreditGunLevel
            : Mathf.Max(1, activeGunLevel);

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            center,
            Mathf.Max(0.1f, settings.radius),
            finalDamageOverlapBuffer,
            settings.fishLayers
        );

        int maximumTargets = Mathf.Clamp(
            settings.maximumTargets,
            1,
            finalDamageOverlapBuffer.Length
        );
        int affectedCount = 0;

        for (int i = 0;
             i < hitCount && affectedCount < maximumTargets;
             i++)
        {
            Collider2D hit = finalDamageOverlapBuffer[i];

            if (hit == null)
            {
                continue;
            }

            FishScript victim = hit.GetComponentInParent<FishScript>();

            if (victim == null ||
                victim == owner ||
                !victim.IsAliveTarget ||
                finalDamagedFish.Contains(victim))
            {
                continue;
            }

            FishTier tier = victim.GetFishTier();

            if ((settings.mainBossImmune && tier == FishTier.MainBoss) ||
                (settings.specialFishImmune && tier == FishTier.Special))
            {
                continue;
            }

            float tierMultiplier = GetFinalAreaDamageTierMultiplier(
                tier,
                settings
            );

            if (tierMultiplier <= 0f)
            {
                continue;
            }

            float distanceMultiplier = 1f;

            if (settings.useDistanceFalloff)
            {
                float distance = Vector2.Distance(
                    center,
                    victim.GetTargetCenterWorld()
                );
                float normalized = Mathf.Clamp01(
                    distance / Mathf.Max(0.1f, settings.radius)
                );
                distanceMultiplier = Mathf.Lerp(
                    1f,
                    settings.minimumFalloffMultiplier,
                    normalized
                );
            }

            float damage = settings.damage *
                           tierMultiplier *
                           distanceMultiplier;

            if (damage <= 0f)
            {
                continue;
            }

            finalDamagedFish.Add(victim);
            affectedCount++;

            float healthBefore = victim.CurrentHealth;
            SpriteRenderer targetRenderer =
                victim.GetComponent<SpriteRenderer>();

            if (targetRenderer == null)
            {
                targetRenderer =
                    victim.GetComponentInChildren<SpriteRenderer>();
            }

            victim.TakeDamage(
                targetRenderer,
                damage,
                damageBulletId,
                damageGunLevel
            );

            if (settings.requestReaction &&
                healthBefore > damage &&
                victim.IsAliveTarget)
            {
                victim.PlayExternalExplosionReaction(
                    settings.reactionDuration,
                    settings.reactionScaleStrength,
                    settings.reactionRotationDegrees,
                    settings.reactionRedStrength
                );
            }
        }
    }

    private static float GetFinalAreaDamageTierMultiplier(
        FishTier tier,
        CinematicAreaDamageSettings settings
    )
    {
        switch (tier)
        {
            case FishTier.Small:
                return settings.smallMultiplier;
            case FishTier.Medium:
                return settings.mediumMultiplier;
            case FishTier.Special:
                return settings.specialMultiplier;
            case FishTier.MiniBoss:
                return settings.miniBossMultiplier;
            case FishTier.MainBoss:
                return settings.mainBossMultiplier;
            default:
                return 1f;
        }
    }

    private void GetOwnerSorting(
        out int sortingLayerId,
        out int sortingOrder
    )
    {
        sortingLayerId = int.MinValue;
        sortingOrder = int.MinValue;

        if (owner == null || profile == null || !profile.useFishSorting)
        {
            return;
        }

        SortingGroup group = owner.GetComponent<SortingGroup>();

        if (group != null)
        {
            sortingLayerId = group.sortingLayerID;
            sortingOrder = group.sortingOrder;
            return;
        }

        SpriteRenderer renderer = owner.GetComponentInChildren<SpriteRenderer>(true);

        if (renderer != null)
        {
            sortingLayerId = renderer.sortingLayerID;
            sortingOrder = renderer.sortingOrder;
        }
    }

    private static int AddSortingOffset(int baseOrder, int offset)
    {
        return baseOrder == int.MinValue
            ? int.MinValue
            : baseOrder + offset;
    }

    private static float SafeDivideScale(float numerator, float denominator)
    {
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return numerator;
        }

        return numerator / denominator;
    }

    private static float SafeSignedScale(float value)
    {
        if (Mathf.Abs(value) >= 0.0001f)
        {
            return value;
        }

        return value < 0f ? -0.0001f : 0.0001f;
    }

    private static Vector3 ToScale(Vector2 scale)
    {
        return new Vector3(
            Mathf.Max(0.01f, scale.x),
            Mathf.Max(0.01f, scale.y),
            1f
        );
    }

    private static Vector3 DirectionFromDegrees(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Cos(radians),
            Mathf.Sin(radians),
            0f
        );
    }

    private static float EvaluateCurve(AnimationCurve curve, float value)
    {
        return curve != null && curve.length > 0
            ? curve.Evaluate(Mathf.Clamp01(value))
            : Mathf.Clamp01(value);
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private void HideAllClones()
    {
        for (int i = 0; i < clonePool.Count; i++)
        {
            CloneRuntime clone = clonePool[i];

            if (clone != null && clone.motionRoot != null)
            {
                clone.motionRoot.SetActive(false);
                clone.motionTransform.SetParent(runtimeRoot, false);
                clone.motionTransform.localPosition = Vector3.zero;
                clone.motionTransform.localRotation = Quaternion.identity;
                clone.motionTransform.localScale = Vector3.one;
            }
        }
    }
}
