using UnityEngine;

public class NpcLocomotionAnimator : MonoBehaviour
{
    private NpcWanderController wander;
    private Animator animator;
    private bool wasWalking;
    private bool initialized;
    private float retryTimer;
    private const float RetryInterval = 0.5f;

    void Start()
    {
        wander = GetComponent<NpcWanderController>();
        TryInit();
    }

    void Update()
    {
        if (!initialized)
        {
            retryTimer -= Time.deltaTime;
            if (retryTimer <= 0f)
            {
                TryInit();
                retryTimer = RetryInterval;
            }
            return;
        }

        if (wander == null) return;

        bool walking = wander.IsWalking;
        if (walking != wasWalking)
        {
            animator.ResetTrigger("walk");
            animator.ResetTrigger("idle");
            animator.SetTrigger(walking ? "walk" : "idle");
            wasWalking = walking;
        }
    }

    private void TryInit()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.ResetTrigger("walk");
        animator.ResetTrigger("idle");
        animator.SetTrigger("idle");
        wasWalking = false;
        initialized = true;
    }
}
