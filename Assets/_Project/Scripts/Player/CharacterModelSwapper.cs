using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterModelSwapper : MonoBehaviour
{
    public enum BodyBuild
    {
        Kurus,
        Ideal,
        Overweight
    }

    [Header("Body Models")]
    [SerializeField] private GameObject femaleKurus;
    [SerializeField] private GameObject femaleIdeal;
    [SerializeField] private GameObject femaleOverweight;
    [SerializeField] private GameObject maleKurus;
    [SerializeField] private GameObject maleIdeal;
    [SerializeField] private GameObject maleOverweight;

    [Header("Animation")]
    [SerializeField] private RuntimeAnimatorController maleOverrideController;
    [SerializeField] private RuntimeAnimatorController femaleOverrideController;

    [Header("Legacy Placeholder")]
    [SerializeField] private string legacyModelChildName = "model";

    const string ModelPoolName = "CharacterModelPool";
    static readonly Vector3 LegacyBodyScale = Vector3.one;
    static readonly Vector3 NewBodyScale = new Vector3(2f, 2f, 2f);

    private Animator animator;
    private PlayerLocomotionRig locomotionRig;
    private Transform modelPool;
    private GameObject activeModel;
    private BodyBuild activeBuild = (BodyBuild)(-1);
    private PlayerStats.Gender activeGender = (PlayerStats.Gender)(-1);
    private int bindingGeneration;
    private readonly Dictionary<GameObject, Vector3> cachedLocalScales = new Dictionary<GameObject, Vector3>();
    private MixamoAvatarReference mixamoAvatarReference;

    void Awake()
    {
        PlayerAnimationRiggingGuard.EnsureOnPlayer(gameObject);

        animator = GetComponent<Animator>();
        locomotionRig = GetComponent<PlayerLocomotionRig>();
        if (animator == null)
            Debug.LogWarning("[CharacterModelSwapper] Animator tidak ditemukan di Player root.");

        EnsureDefaultControllers();
        mixamoAvatarReference = Resources.Load<MixamoAvatarReference>("MixamoAvatarReference");
        CacheBodyModelScales();
        EnsureModelPool();
    }

    void Start()
    {
        CacheBodyModelScales();

        if (PlayerStats.Instance != null)
            InitializeModel();
    }

    void OnDisable()
    {
        bindingGeneration++;
    }

    void EnsureDefaultControllers()
    {
        if (maleOverrideController == null)
            maleOverrideController = Resources.Load<RuntimeAnimatorController>("PlayerAnimator_Male");

        if (femaleOverrideController == null)
            femaleOverrideController = Resources.Load<RuntimeAnimatorController>("PlayerAnimator_Female");
    }

    public void InitializeModel()
    {
        EvaluateAndSwap(force: true);
    }

    public void EvaluateAndSwap()
    {
        EvaluateAndSwap(force: false);
    }

    void EvaluateAndSwap(bool force)
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
        {
            Debug.LogWarning("[CharacterModelSwapper] PlayerStats.Instance null — swap dibatalkan.");
            return;
        }

        BodyBuild build = ResolveBodyBuild(stats.PlayerBMI);
        PlayerStats.Gender gender = stats.PlayerGender;

        if (!force && activeModel != null && build == activeBuild && gender == activeGender)
            return;

        GameObject target = ResolveModel(gender, build);
        if (target == null)
        {
            Debug.LogWarning($"[CharacterModelSwapper] Tidak ada model untuk gender={gender} build={build}.");
            return;
        }

        SetActiveModel(target, gender);
        activeBuild = build;
        activeGender = gender;
    }

    public static BodyBuild ResolveBodyBuild(float bmi)
    {
        if (bmi < 18.5f)
            return BodyBuild.Kurus;

        if (bmi < 25f)
            return BodyBuild.Ideal;

        return BodyBuild.Overweight;
    }

    GameObject ResolveModel(PlayerStats.Gender gender, BodyBuild build)
    {
        if (gender == PlayerStats.Gender.Female)
        {
            return build switch
            {
                BodyBuild.Kurus      => femaleKurus,
                BodyBuild.Ideal      => femaleIdeal,
                BodyBuild.Overweight => femaleOverweight,
                _                    => femaleIdeal
            };
        }

        return build switch
        {
            BodyBuild.Kurus      => maleKurus,
            BodyBuild.Ideal      => maleIdeal,
            BodyBuild.Overweight => maleOverweight,
            _                    => maleIdeal
        };
    }

    void SetActiveModel(GameObject target, PlayerStats.Gender gender)
    {
        EnsureModelPool();
        DeactivateAllModels();
        AttachModelToPlayer(target);

        activeModel = target;

        bindingGeneration++;
        StartCoroutine(ApplyAnimatorBindingRoutine(target, gender, bindingGeneration));
    }

    void DeactivateAllModels()
    {
        MoveModelToPool(femaleKurus);
        MoveModelToPool(femaleIdeal);
        MoveModelToPool(femaleOverweight);
        MoveModelToPool(maleKurus);
        MoveModelToPool(maleIdeal);
        MoveModelToPool(maleOverweight);
        DeactivateLegacyPlaceholder();
    }

    void EnsureModelPool()
    {
        if (modelPool != null)
            return;

        GameObject existing = GameObject.Find(ModelPoolName);
        if (existing != null)
        {
            modelPool = existing.transform;
            if (modelPool.parent != transform.parent)
                modelPool.SetParent(transform.parent, false);
        }
        else
        {
            GameObject poolObject = new GameObject(ModelPoolName);
            poolObject.transform.SetParent(transform.parent, false);
            modelPool = poolObject.transform;
        }

        modelPool.gameObject.SetActive(false);
    }

    void CacheBodyModelScales()
    {
        CacheModelScale(femaleKurus);
        CacheModelScale(femaleIdeal);
        CacheModelScale(femaleOverweight);
        CacheModelScale(maleKurus);
        CacheModelScale(maleIdeal);
        CacheModelScale(maleOverweight);
    }

    void CacheModelScale(GameObject model)
    {
        if (model == null)
            return;

        cachedLocalScales[model] = ResolveBodyScale(model);
    }

    static bool UsesNewBodyNaming(string modelName)
    {
        return modelName.StartsWith("Female_", System.StringComparison.Ordinal) ||
               modelName.StartsWith("Male_", System.StringComparison.Ordinal);
    }

    static Vector3 ResolveBodyScale(GameObject model)
    {
        if (model == null)
            return LegacyBodyScale;

        if (UsesNewBodyNaming(model.name))
            return NewBodyScale;

        Vector3 sceneScale = model.transform.localScale;
        if (sceneScale.sqrMagnitude > 0.0001f)
            return sceneScale;

        return LegacyBodyScale;
    }

    void MoveModelToPool(GameObject model)
    {
        if (model == null || modelPool == null)
            return;

        CacheModelScale(model);
        model.SetActive(false);
        model.transform.SetParent(modelPool, false);
    }

    void AttachModelToPlayer(GameObject model)
    {
        if (model == null)
            return;

        CacheModelScale(model);

        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        Vector3 scale = ResolveBodyScale(model);
        cachedLocalScales[model] = scale;
        model.transform.localScale = scale;
        model.SetActive(true);
    }

    void DeactivateLegacyPlaceholder()
    {
        if (string.IsNullOrWhiteSpace(legacyModelChildName))
            return;

        Transform legacy = transform.Find(legacyModelChildName);
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    IEnumerator ApplyAnimatorBindingRoutine(GameObject model, PlayerStats.Gender gender, int generation)
    {
        // Let the active body model settle before touching the root Animator so humanoid
        // bind targets its rig, not CC_Base_* bones on inactive sibling models.
        yield return null;

        if (generation != bindingGeneration)
            yield break;

        if (animator == null)
            yield break;

        RuntimeAnimatorController controller = gender == PlayerStats.Gender.Female
            ? femaleOverrideController
            : maleOverrideController;

        Avatar avatar = ResolveAvatar(model);
        if (avatar == null)
        {
            Debug.LogWarning($"[CharacterModelSwapper] Avatar tidak ditemukan pada model '{model.name}'.");
            yield break;
        }

        if (!HasSkinnedMesh(model))
        {
            Debug.LogWarning(
                $"[CharacterModelSwapper] Model '{model.name}' tidak punya SkinnedMeshRenderer — mesh tidak akan ikut animasi. " +
                "Rig di Mixamo (Auto-Rig) lalu replace FBX di folder Character With No Rigging.");
        }

        animator.enabled = false;
        animator.avatar = null;

        if (controller != null)
            animator.runtimeAnimatorController = controller;

        animator.avatar = avatar;
        animator.Rebind();
        animator.enabled = true;

        LogAnimatorBindingDebug(model);

        if (locomotionRig != null && locomotionRig.enabled)
            locomotionRig.RefreshAfterModelSwap();
    }

    void LogAnimatorBindingDebug(GameObject model)
    {
        if (animator == null || model == null)
            return;

        string avatarName = animator.avatar != null ? animator.avatar.name : "(null)";
        string controllerName = animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : "(null)";

        Debug.Log(
            $"[CharacterModelSwapper] Binding debug model={model.name} " +
            $"avatar={avatarName} isHuman={animator.isHuman} controller={controllerName}");
    }

    Avatar ResolveAvatar(GameObject modelRoot)
    {
        if (modelRoot == null)
            return null;

        Animator[] animators = modelRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].avatar != null && animators[i].avatar.isValid)
                return animators[i].avatar;
        }

        if (mixamoAvatarReference != null && mixamoAvatarReference.avatar != null &&
            mixamoAvatarReference.avatar.isValid)
            return mixamoAvatarReference.avatar;

        return null;
    }

    static bool HasSkinnedMesh(GameObject modelRoot)
    {
        if (modelRoot == null)
            return false;

        return modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
    }
}
