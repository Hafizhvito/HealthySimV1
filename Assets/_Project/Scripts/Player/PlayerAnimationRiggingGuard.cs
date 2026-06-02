using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Stops Animation Rigging foot IK from running until explicitly enabled.
/// Prevents PropertyStreamHandle errors when IK bones are not bound to the active avatar.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class PlayerAnimationRiggingGuard : MonoBehaviour
{
    [SerializeField] private bool suppressFootIkRigging = true;

    void Awake()
    {
        ApplySuppression();
    }

    void OnEnable()
    {
        ApplySuppression();
    }

    public void ApplySuppression()
    {
        if (!suppressFootIkRigging)
            return;

        DisableFootIkRigging(gameObject);
    }

    public static void DisableFootIkRigging(GameObject player)
    {
        if (player == null)
            return;

        RigBuilder rigBuilder = player.GetComponent<RigBuilder>();
        if (rigBuilder != null)
            rigBuilder.enabled = false;

        Rig[] rigs = player.GetComponentsInChildren<Rig>(true);
        for (int i = 0; i < rigs.Length; i++)
            rigs[i].weight = 0f;

        TwoBoneIKConstraint[] ikConstraints = player.GetComponentsInChildren<TwoBoneIKConstraint>(true);
        for (int i = 0; i < ikConstraints.Length; i++)
            ikConstraints[i].weight = 0f;

        PlayerLocomotionRig locomotionRig = player.GetComponent<PlayerLocomotionRig>();
        if (locomotionRig != null)
            locomotionRig.enabled = false;
    }

    public static void EnsureOnPlayer(GameObject player)
    {
        if (player == null)
            return;

        PlayerAnimationRiggingGuard guard = player.GetComponent<PlayerAnimationRiggingGuard>();
        if (guard == null)
            guard = player.AddComponent<PlayerAnimationRiggingGuard>();

        guard.suppressFootIkRigging = true;
        guard.ApplySuppression();
    }
}
