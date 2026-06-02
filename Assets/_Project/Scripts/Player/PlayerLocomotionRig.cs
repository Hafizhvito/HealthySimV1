using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Foot IK via Animation Rigging — rebinds to the active humanoid after CharacterModelSwapper swaps body models.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerLocomotionRig : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private Rig locomotionRig;
    [SerializeField] private TwoBoneIKConstraint leftLegIk;
    [SerializeField] private TwoBoneIKConstraint rightLegIk;

    [Header("IK Targets")]
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;
    [SerializeField] private Transform leftKneeHint;
    [SerializeField] private Transform rightKneeHint;

    [Header("Foot Grounding")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float footRayHeight = 0.35f;
    [SerializeField] private float footRayDistance = 1.2f;
    [SerializeField] private float targetFollowSmooth = 18f;

    [Header("IK Weight")]
    [SerializeField] private bool footIkEnabled;
    [SerializeField] [Range(0f, 1f)] private float movingIkWeight = 0.55f;
    [SerializeField] private float speedThreshold = 0.05f;

    Animator animator;
    RigBuilder rigBuilder;
    int speedParamHash = Animator.StringToHash("Speed");
    bool ikReady;

    public bool IsIkActive => ikReady;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rigBuilder = GetComponent<RigBuilder>();

        if (groundLayers.value == ~0)
            groundLayers = LayerMask.GetMask("Ground", "Default");

        if (!footIkEnabled)
        {
            DisableIkSystem();
            enabled = false;
            return;
        }

        DisableIkSystem();
    }

    void OnDisable()
    {
        DisableIkSystem();
    }

    public void RefreshAfterModelSwap()
    {
        if (!footIkEnabled || !enabled)
            return;

        DisableIkSystem();

        if (animator == null || rigBuilder == null)
            return;

        if (!animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            return;

        bool leftBound = BindLegIk(leftLegIk,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            leftFootTarget,
            leftKneeHint);

        bool rightBound = BindLegIk(rightLegIk,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            rightFootTarget,
            rightKneeHint);

        if (!leftBound || !rightBound)
            return;

        SnapTargetsToFeet();
        rigBuilder.Build();
        ikReady = true;
        rigBuilder.enabled = true;
    }

    void LateUpdate()
    {
        if (!ikReady || animator == null || rigBuilder == null || !rigBuilder.enabled)
            return;

        if (leftLegIk == null || rightLegIk == null)
            return;

        if (!animator.isHuman)
            return;

        float speed = animator.GetFloat(speedParamHash);
        float ikWeight = speed > speedThreshold ? movingIkWeight : 0f;
        SetIkWeight(ikWeight);

        if (ikWeight <= 0.001f)
            return;

        UpdateFootTarget(leftFootTarget, HumanBodyBones.LeftFoot);
        UpdateFootTarget(rightFootTarget, HumanBodyBones.RightFoot);
        UpdateKneeHints();
    }

    bool BindLegIk(
        TwoBoneIKConstraint ik,
        HumanBodyBones upperLeg,
        HumanBodyBones lowerLeg,
        HumanBodyBones foot,
        Transform target,
        Transform hint)
    {
        if (ik == null)
            return false;

        Transform root = animator.GetBoneTransform(upperLeg);
        Transform mid = animator.GetBoneTransform(lowerLeg);
        Transform tip = animator.GetBoneTransform(foot);

        if (root == null || mid == null || tip == null)
        {
            ik.weight = 0f;
            return false;
        }

        ik.data.root = root;
        ik.data.mid = mid;
        ik.data.tip = tip;
        ik.data.target = target;
        ik.data.hint = hint;

        ik.data.targetPositionWeight = 1f;
        ik.data.targetRotationWeight = 0.35f;
        ik.data.hintWeight = 0.45f;
        ik.data.maintainTargetPositionOffset = false;
        ik.data.maintainTargetRotationOffset = false;
        return true;
    }

    void DisableIkSystem()
    {
        ikReady = false;
        SetIkWeight(0f);

        if (rigBuilder != null)
            rigBuilder.enabled = false;
    }

    void SnapTargetsToFeet()
    {
        SnapTargetToBone(leftFootTarget, HumanBodyBones.LeftFoot);
        SnapTargetToBone(rightFootTarget, HumanBodyBones.RightFoot);
        UpdateKneeHints();
    }

    void SnapTargetToBone(Transform target, HumanBodyBones footBone)
    {
        if (target == null)
            return;

        Transform foot = animator.GetBoneTransform(footBone);
        if (foot == null)
            return;

        target.position = foot.position;
        target.rotation = foot.rotation;
    }

    void UpdateFootTarget(Transform target, HumanBodyBones footBone)
    {
        if (target == null)
            return;

        Transform foot = animator.GetBoneTransform(footBone);
        if (foot == null)
            return;

        Vector3 rayOrigin = foot.position + Vector3.up * footRayHeight;
        Vector3 desired = foot.position;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, footRayDistance, groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            desired = hit.point;
        }

        float t = 1f - Mathf.Exp(-targetFollowSmooth * Time.deltaTime);
        target.position = Vector3.Lerp(target.position, desired, t);
        target.rotation = Quaternion.Slerp(target.rotation, foot.rotation, t);
    }

    void UpdateKneeHints()
    {
        PlaceKneeHint(leftKneeHint, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        PlaceKneeHint(rightKneeHint, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
    }

    void PlaceKneeHint(Transform hint, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot)
    {
        if (hint == null)
            return;

        Transform upperT = animator.GetBoneTransform(upper);
        Transform lowerT = animator.GetBoneTransform(lower);
        Transform footT = animator.GetBoneTransform(foot);
        if (upperT == null || lowerT == null || footT == null)
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        hint.position = lowerT.position + forward * 0.18f;
        hint.rotation = lowerT.rotation;
    }

    void SetIkWeight(float weight)
    {
        if (locomotionRig != null)
            locomotionRig.weight = weight;

        if (leftLegIk != null)
            leftLegIk.weight = weight;

        if (rightLegIk != null)
            rightLegIk.weight = weight;
    }
}
