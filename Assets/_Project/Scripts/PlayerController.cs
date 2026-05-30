using System.Collections.Generic;
using Unity.Cinemachine;
using TMPro;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 9f;
    [SerializeField] private float inputDeadZone = 0.08f;
    [SerializeField] private float inputDirectionSmooth = 10f; // turunkan dari 14 ke 10
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 26f;
    [SerializeField] private float rotationInputThreshold = 0.12f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Air Feel")]
    [SerializeField] private float fallGravityMultiplier = 2.2f;
    [SerializeField] private float maxFallSpeed = 24f;
    [SerializeField] private float groundedStickyVelocity = -2f;
    [SerializeField] private float cancelSmallUpwardBounceBelow = 1.25f;

    [Header("Step Assist")]
    [SerializeField] private bool enableStepAssist = true;
    [SerializeField] private float stepHeight = 0.35f;
    [SerializeField] private float stepCheckDistance = 0.34f;
    [SerializeField] private float stepUpSpeed = 6f;
    [SerializeField] private float maxStepSurfaceAngle = 55f;
    [SerializeField] private float stepAssistCooldown = 0.08f;

    [Header("Wall Contact")]
    [SerializeField] private bool reduceWallStick = true;
    [SerializeField] private float steepWallNormalYThreshold = 0.18f;
    [SerializeField] private float wallContactMemory = 0.12f;
    [SerializeField] private float extraWallSlideGravity = 8f;
    [SerializeField] private bool autoAssignLowFrictionMaterial = true;
    [SerializeField] [Range(0f, 0.4f)] private float playerColliderFriction = 0f;

    [Header("FOV")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float runFOV = 68f;
    [SerializeField] private float fovSmoothSpeed = 5f;

    [Header("Fatigue Indicator")]
    [SerializeField] private bool showFatigueIndicator = true;
    [SerializeField] private float fatigueIndicatorHeight = 2.2f;
    [SerializeField] private float fatigueWarningPulseSpeed = 2.4f;
    [SerializeField] private float fatigueCriticalPulseSpeed = 4.5f;
    [SerializeField] private float fatigueCriticalBlinkSpeed = 10.5f;
    [SerializeField] [Range(0f, 1f)] private float fatigueCriticalMinAlpha = 0.2f;
    [SerializeField] private Color fatigueWarningColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color fatigueCriticalColor = new Color(1f, 0.28f, 0.22f, 1f);

    [Header("Health Warning Indicator")]
    [SerializeField] private bool showHealthIndicator = true;
    [SerializeField] private float healthIndicatorHeight = 2.55f;
    [SerializeField] private float healthWarningThreshold = 40f;
    [SerializeField] private float healthIndicatorPulseSpeed = 2.1f;
    [SerializeField] [Range(0.2f, 1f)] private float healthIndicatorMinAlpha = 0.5f;
    [SerializeField] private Color healthIndicatorColor = new Color(0.75f, 0.25f, 1f, 1f);

    private Rigidbody rb;
    private CapsuleCollider col;
    private Animator animator;
    private PlayerStats playerStats;
    private CameraSystem cameraSystem;
    private CinemachineBrain cachedBrain;
    private TextMeshPro fatigueIndicatorText;
    private Transform fatigueIndicatorTransform;
    private TextMeshPro healthIndicatorText;
    private Transform healthIndicatorTransform;
    private PhysicsMaterial runtimeLowFrictionMaterial;

    private Vector3 moveDir;
    private Vector3 desiredMoveDir;
    private Vector2 rawInput;       // raw input dibaca di Update
    private Vector2 keyboardInput;  // keyboard input sampled in Update
    private bool hasKeyboardInput;
    private Vector2 mobileInput;    // injected from MobileInputController
    private bool hasMobileInput;    // true when mobile joystick is actively pushed
    private bool mobileRunRequest;
    private bool isRunning;
    private bool isRunningRaw;      // raw running flag dari Update
    private bool isGrounded;
    private bool jumpRequest;
    private Vector3 cachedCameraForward;
    private Vector3 cachedCameraRight;
    private float lastStepAssistTime;
    private Vector3 steepWallNormal = Vector3.forward;
    private float lastSteepWallContactTime = -999f;
    private bool jumpedThisFixedFrame;

    private readonly Dictionary<string, int> inputLocks =
        new Dictionary<string, int>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        animator = GetComponent<Animator>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearDamping = 0f;      // pastikan tidak double-brake dengan Move()
        rb.angularDamping = 0.05f;

        EnsureLowFrictionColliderMaterial();

        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Fixed;
        animator.animatePhysics = true;

        if (groundLayer.value == 0)
            groundLayer = LayerMask.GetMask("Ground", "Default");
    }

    void Start()
    {
        playerStats = PlayerStats.Instance;
        cameraSystem = FindFirstObjectByType<CameraSystem>();
        if (Camera.main != null)
            cachedBrain = Camera.main.GetComponent<CinemachineBrain>();

        inputLocks.Clear();
        isGrounded = true;
        cachedCameraForward = transform.forward;
        cachedCameraForward.y = 0f;
        cachedCameraForward.Normalize();
        cachedCameraRight = Vector3.Cross(Vector3.up, cachedCameraForward).normalized;
        lastStepAssistTime = -999f;

        EnsureFatigueIndicatorBuilt();
        EnsureHealthIndicatorBuilt();
    }

    // ---------------------------------------------------------------
    // Update: hanya baca input mentah & hal frame-sensitive
    // ---------------------------------------------------------------
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.F8))
#endif
        {
            inputLocks.Clear();
            Debug.LogWarning("F8: Input locks cleared.");
        }

        UpdateFatigueIndicator();
        UpdateHealthIndicator();

        if (IsInputLocked)
        {
            keyboardInput = Vector2.zero;
            hasKeyboardInput = false;
            mobileInput = Vector2.zero;
            hasMobileInput = false;
            mobileRunRequest = false;
            rawInput = Vector2.zero;
            isRunningRaw = false;
            jumpRequest = false;
            UpdateAnimator();
            UpdateFOV();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        float h = 0f;
        float v = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                h -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                h += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                v += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                v -= 1f;
        }
#else
        // Baca keyboard axis
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
#endif
        if (Mathf.Abs(h) < inputDeadZone) h = 0f; 
        if (Mathf.Abs(v) < inputDeadZone) v = 0f;
        keyboardInput = new Vector2(h, v);
        hasKeyboardInput = keyboardInput.sqrMagnitude > 0.001f;

        // Merge keyboard and mobile; keyboard takes priority when both are active.
        if (hasMobileInput && mobileInput.sqrMagnitude > 0.001f)
            rawInput = hasKeyboardInput ? keyboardInput : mobileInput;
        else
            rawInput = keyboardInput;

        // Running flag (raw, difinalisasi di ProcessMoveDir)
    #if ENABLE_INPUT_SYSTEM
        bool shiftHeld = keyboard != null
            && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    #else
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
    #endif
        isRunningRaw = shiftHeld || (hasMobileInput && mobileRunRequest);

        // Cache camera basis di Update agar sinkron dengan update kamera.
        Transform cam = Camera.main != null
            ? Camera.main.transform
            : transform;

        Vector3 fwd = cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f)
            fwd = transform.forward;
        fwd.Normalize();

        cachedCameraForward = fwd;
        cachedCameraRight = Vector3.Cross(Vector3.up, cachedCameraForward).normalized;

        // Jump — harus di Update karena GetButtonDown frame-sensitive
#if ENABLE_INPUT_SYSTEM
        bool jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
        bool jumpPressed = Input.GetButtonDown("Jump")
            || Input.GetKeyDown(KeyCode.Space);
#endif
        if (jumpPressed)
            jumpRequest = true;

        if (playerStats != null)
            playerStats.DrainEnergy(
                moveDir.magnitude > 0.1f && !isRunning,
                isRunning,
                Time.deltaTime);

        UpdateAnimator();
        UpdateFOV();
    }

    // ---------------------------------------------------------------
    // FixedUpdate: semua physics processing
    // ---------------------------------------------------------------
    void FixedUpdate()
    {
        jumpedThisFixedFrame = false;
        GroundCheck();

        if (IsInputLocked)
        {
            moveDir = Vector3.zero;
            isRunning = false;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        ProcessMoveDir();   // kalkulasi moveDir pakai fixedDeltaTime
        Move();
        StepAssist();
        Rotate();
        ApplyJump();
        ApplyVerticalMotionTuning();
    }

    // ---------------------------------------------------------------
    // ProcessMoveDir — pindah dari ReadInput ke FixedUpdate
    // ---------------------------------------------------------------
    void ProcessMoveDir()
    {
        Vector3 targetMoveDir = cachedCameraForward * rawInput.y + cachedCameraRight * rawInput.x;
        if (targetMoveDir.magnitude > 1f)
            targetMoveDir.Normalize();

        desiredMoveDir = targetMoveDir.sqrMagnitude > 0.0001f
            ? targetMoveDir.normalized
            : Vector3.zero;

        // Gunakan fixedDeltaTime agar konsisten dengan physics step
        moveDir = Vector3.Lerp(moveDir, targetMoveDir,
            inputDirectionSmooth * Time.fixedDeltaTime);

        if (moveDir.sqrMagnitude < 0.0001f)
            moveDir = Vector3.zero;

        isRunning = isRunningRaw && moveDir.magnitude > 0.1f;
    }

    void Move()
    {
        float speedMult = playerStats != null
            ? playerStats.GetSpeedMultiplier()
            : 1f;

        float speed = moveDir.magnitude > 0.1f
            ? (isRunning ? runSpeed : walkSpeed) * speedMult
            : 0f;

        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetHorizontal = moveDir.sqrMagnitude > 0.0001f
            ? moveDir.normalized * speed
            : Vector3.zero;

        if (!isGrounded && IsRecentSteepWallContact())
        {
            Vector3 wallHorizontalNormal = new Vector3(steepWallNormal.x, 0f, steepWallNormal.z);
            if (wallHorizontalNormal.sqrMagnitude > 0.0001f)
            {
                wallHorizontalNormal.Normalize();
                targetHorizontal = Vector3.ProjectOnPlane(targetHorizontal, wallHorizontalNormal);
            }

            if (extraWallSlideGravity > 0f)
                rb.AddForce(Vector3.down * extraWallSlideGravity, ForceMode.Acceleration);
        }

        float accel = targetHorizontal.sqrMagnitude > currentHorizontal.sqrMagnitude
            ? acceleration
            : deceleration;

        Vector3 nextHorizontal = Vector3.MoveTowards(
            currentHorizontal,
            targetHorizontal,
            accel * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextHorizontal.x, rb.linearVelocity.y, nextHorizontal.z);
    }

    void Rotate()
    {
        if (cameraSystem != null && cameraSystem.IsFirstPerson)
        {
            Vector3 lookForward = cachedCameraForward;
            if (lookForward.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookForward);
            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime));
            return;
        }

        if (desiredMoveDir.sqrMagnitude < rotationInputThreshold * rotationInputThreshold)
            return;

        Quaternion target = Quaternion.LookRotation(desiredMoveDir);
        rb.MoveRotation(Quaternion.Slerp(
            rb.rotation,
            target,
            rotationSpeed * Time.fixedDeltaTime));
    }

    void ApplyJump()
    {
        if (!jumpRequest)
            return;

        jumpRequest = false;
        if (!isGrounded)
            return;

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z);

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpedThisFixedFrame = true;
        isGrounded = false;
        animator.SetBool("IsJumping", true);
    }

    private void ApplyVerticalMotionTuning()
    {
        Vector3 velocity = rb.linearVelocity;

        if (!isGrounded && velocity.y < -0.01f)
        {
            float extraGravityScale = Mathf.Max(1f, fallGravityMultiplier) - 1f;
            if (extraGravityScale > 0f)
                rb.AddForce(Physics.gravity * extraGravityScale, ForceMode.Acceleration);

            float maxDownward = -Mathf.Max(5f, maxFallSpeed);
            if (velocity.y < maxDownward)
                velocity.y = maxDownward;
        }

        if (isGrounded && !jumpedThisFixedFrame)
        {
            if (velocity.y > 0f && velocity.y <= Mathf.Max(0.1f, cancelSmallUpwardBounceBelow))
                velocity.y = 0f;
            else if (velocity.y < 0f)
                velocity.y = Mathf.Max(velocity.y, groundedStickyVelocity);
        }

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, velocity.y, rb.linearVelocity.z);
    }

    void StepAssist()
    {
        if (!enableStepAssist || !isGrounded) return;
        if (moveDir.sqrMagnitude < 0.01f) return;
        if (rb.linearVelocity.y > 0.5f) return;
        if (Time.time - lastStepAssistTime < stepAssistCooldown) return;
        if (IsRecentSteepWallContact()) return;

        Vector3 moveForward = moveDir.normalized;
        float probeRadius = Mathf.Max(0.05f, col.radius * 0.42f);

        float footWorldY = rb.position.y + col.center.y - (col.height * 0.5f);

        Vector3 lowerOrigin = new Vector3(
            rb.position.x + col.center.x,
            footWorldY + probeRadius + 0.01f,
            rb.position.z + col.center.z);

        bool lowerBlocked = Physics.SphereCast(
            lowerOrigin, probeRadius, moveForward,
            out RaycastHit lowerHit, stepCheckDistance,
            groundLayer, QueryTriggerInteraction.Ignore);

        if (!lowerBlocked) return;

        Vector3 upperOrigin = new Vector3(
            lowerOrigin.x, footWorldY + stepHeight + probeRadius, lowerOrigin.z);

        bool upperBlocked = Physics.SphereCast(
            upperOrigin, probeRadius, moveForward,
            out _, stepCheckDistance,
            groundLayer, QueryTriggerInteraction.Ignore);

        if (upperBlocked) return;

        Vector3 topProbeOrigin = lowerOrigin
            + moveForward * (lowerHit.distance + probeRadius * 2f)
            + Vector3.up * (stepHeight + 0.1f);

        bool topFound = Physics.Raycast(
            topProbeOrigin, Vector3.down,
            out RaycastHit topHit, stepHeight + 0.3f,
            groundLayer, QueryTriggerInteraction.Ignore);

        if (!topFound) return;
        if (Vector3.Angle(topHit.normal, Vector3.up) > maxStepSurfaceAngle) return;

        float stepDelta = topHit.point.y - footWorldY;
        if (stepDelta <= 0.01f || stepDelta > stepHeight + 0.05f) return;

        float clampedStepDelta = Mathf.Min(stepDelta, stepHeight * 0.65f);
        float targetLiftVelocity = Mathf.Min(
            clampedStepDelta / Time.fixedDeltaTime,
            stepUpSpeed);

        float liftVelocity = Mathf.Lerp(rb.linearVelocity.y, targetLiftVelocity, 0.45f);
        liftVelocity = Mathf.Max(0f, liftVelocity);

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            liftVelocity,
            rb.linearVelocity.z);
        lastStepAssistTime = Time.time;
    }

    void GroundCheck()
    {
        float radius = col.radius * 0.9f;
        Vector3 origin = transform.TransformPoint(col.center);
        float dist = col.height * 0.5f - col.radius + groundCheckDistance;

        bool hit = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit info,
            dist,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        isGrounded = hit && info.normal.y > 0.5f;

        animator.SetBool("IsGrounded", isGrounded);
        if (isGrounded && rb.linearVelocity.y <= 0.05f)
            animator.SetBool("IsJumping", false);
    }

    void UpdateAnimator()
    {
        float speed = isRunning
            ? 1f
            : (moveDir.magnitude > 0.1f ? 0.5f : 0f);

        animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

    void UpdateFOV()
    {
        if (cachedBrain == null)
            return;

        if (cameraSystem != null && cameraSystem.IsFirstPerson)
            return;

        float target = isRunning ? runFOV : normalFOV;
        CinemachineCamera cam = cachedBrain.ActiveVirtualCamera as CinemachineCamera;
        if (cam == null)
            return;

        LensSettings lens = cam.Lens;
        lens.FieldOfView = Mathf.Lerp(
            lens.FieldOfView,
            target,
            fovSmoothSpeed * Time.deltaTime);
        cam.Lens = lens;
    }

    private void EnsureFatigueIndicatorBuilt()
    {
        if (fatigueIndicatorText != null && fatigueIndicatorTransform != null)
            return;

        GameObject indicatorObj = new GameObject("FatigueIndicator", typeof(TextMeshPro));
        indicatorObj.transform.SetParent(transform, false);
        indicatorObj.transform.localPosition = new Vector3(0f, fatigueIndicatorHeight, 0f);
        indicatorObj.transform.localScale = Vector3.one * 0.22f;

        fatigueIndicatorTransform = indicatorObj.transform;
        fatigueIndicatorText = indicatorObj.GetComponent<TextMeshPro>();
        fatigueIndicatorText.text = "!";
        fatigueIndicatorText.fontSize = 14f;
        fatigueIndicatorText.alignment = TextAlignmentOptions.Center;
        fatigueIndicatorText.color = fatigueWarningColor;
        fatigueIndicatorText.outlineWidth = 0.12f;
        fatigueIndicatorText.outlineColor = new Color(0f, 0f, 0f, 0.9f);

        indicatorObj.SetActive(false);
    }

    private void UpdateFatigueIndicator()
    {
        if (!showFatigueIndicator)
        {
            SetFatigueIndicatorVisible(false);
            return;
        }

        if (playerStats == null)
            playerStats = PlayerStats.Instance;

        if (playerStats == null)
        {
            SetFatigueIndicatorVisible(false);
            return;
        }

        EnsureFatigueIndicatorBuilt();
        if (fatigueIndicatorText == null || fatigueIndicatorTransform == null)
            return;

        if (ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen)
        {
            SetFatigueIndicatorVisible(false);
            return;
        }

        PlayerStats.EnergyState state = playerStats.CurrentEnergyState;
        bool shouldShow = state == PlayerStats.EnergyState.Warning || state == PlayerStats.EnergyState.Critical;
        if (!shouldShow)
        {
            SetFatigueIndicatorVisible(false);
            return;
        }

        SetFatigueIndicatorVisible(true);

        fatigueIndicatorTransform.position = transform.position + Vector3.up * fatigueIndicatorHeight;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 lookDir = fatigueIndicatorTransform.position - cam.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
                fatigueIndicatorTransform.rotation = Quaternion.LookRotation(lookDir);
        }

        bool critical = state == PlayerStats.EnergyState.Critical;
        float pulseSpeed = critical ? fatigueCriticalPulseSpeed : fatigueWarningPulseSpeed;
        float pulseAmp = critical ? 0.22f : 0.14f;
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmp;
        float scale = 0.22f * Mathf.Max(0.62f, pulse);
        fatigueIndicatorTransform.localScale = new Vector3(scale, scale, scale);

        if (critical)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * fatigueCriticalBlinkSpeed));
            Color flicker = Color.Lerp(fatigueWarningColor, fatigueCriticalColor, blink);
            flicker.a = Mathf.Lerp(fatigueCriticalMinAlpha, 1f, blink);
            fatigueIndicatorText.color = flicker;
        }
        else
        {
            Color warn = fatigueWarningColor;
            warn.a = 0.95f;
            fatigueIndicatorText.color = warn;
        }
    }

    private void SetFatigueIndicatorVisible(bool visible)
    {
        if (fatigueIndicatorTransform == null)
            return;

        if (fatigueIndicatorTransform.gameObject.activeSelf != visible)
            fatigueIndicatorTransform.gameObject.SetActive(visible);
    }

    private void EnsureHealthIndicatorBuilt()
    {
        if (healthIndicatorText != null && healthIndicatorTransform != null)
            return;

        GameObject indicatorObj = new GameObject("HealthIndicator", typeof(TextMeshPro));
        indicatorObj.transform.SetParent(transform, false);
        indicatorObj.transform.localPosition = new Vector3(0f, healthIndicatorHeight, 0f);
        indicatorObj.transform.localScale = Vector3.one * 0.2f;

        healthIndicatorTransform = indicatorObj.transform;
        healthIndicatorText = indicatorObj.GetComponent<TextMeshPro>();
        healthIndicatorText.text = "!!!";
        healthIndicatorText.fontSize = 12f;
        healthIndicatorText.alignment = TextAlignmentOptions.Center;
        healthIndicatorText.color = healthIndicatorColor;
        healthIndicatorText.outlineWidth = 0.14f;
        healthIndicatorText.outlineColor = new Color(0f, 0f, 0f, 0.9f);

        indicatorObj.SetActive(false);
    }

    private void UpdateHealthIndicator()
    {
        if (!showHealthIndicator)
        {
            SetHealthIndicatorVisible(false);
            return;
        }

        if (playerStats == null)
            playerStats = PlayerStats.Instance;

        if (playerStats == null)
        {
            SetHealthIndicatorVisible(false);
            return;
        }

        EnsureHealthIndicatorBuilt();
        if (healthIndicatorText == null || healthIndicatorTransform == null)
            return;

        if (ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen)
        {
            SetHealthIndicatorVisible(false);
            return;
        }

        bool shouldShow = playerStats.HealthScoreThisPhase < healthWarningThreshold
            && !playerStats.VisitedHospitalToday;
        if (!shouldShow)
        {
            SetHealthIndicatorVisible(false);
            return;
        }

        SetHealthIndicatorVisible(true);

        healthIndicatorTransform.position = transform.position + Vector3.up * healthIndicatorHeight;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 lookDir = healthIndicatorTransform.position - cam.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
                healthIndicatorTransform.rotation = Quaternion.LookRotation(lookDir);
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * healthIndicatorPulseSpeed) * 0.16f;
        float scale = 0.2f * Mathf.Max(0.7f, pulse);
        healthIndicatorTransform.localScale = new Vector3(scale, scale, scale);

        float alphaPulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * healthIndicatorPulseSpeed));
        Color tint = healthIndicatorColor;
        tint.a = Mathf.Lerp(healthIndicatorMinAlpha, 1f, alphaPulse);
        healthIndicatorText.color = tint;
    }

    private void SetHealthIndicatorVisible(bool visible)
    {
        if (healthIndicatorTransform == null)
            return;

        if (healthIndicatorTransform.gameObject.activeSelf != visible)
            healthIndicatorTransform.gameObject.SetActive(visible);
    }

    private void EnsureLowFrictionColliderMaterial()
    {
        if (!autoAssignLowFrictionMaterial || col == null)
            return;

        if (runtimeLowFrictionMaterial == null)
        {
            runtimeLowFrictionMaterial = new PhysicsMaterial("PlayerLowFrictionRuntime")
            {
                dynamicFriction = Mathf.Clamp01(playerColliderFriction),
                staticFriction = Mathf.Clamp01(playerColliderFriction),
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        col.sharedMaterial = runtimeLowFrictionMaterial;
    }

    private bool IsRecentSteepWallContact()
    {
        if (!reduceWallStick)
            return false;

        float memory = Mathf.Max(0.02f, wallContactMemory);
        return Time.time - lastSteepWallContactTime <= memory;
    }

    private void RegisterSteepWallContact(Collision collision)
    {
        if (!reduceWallStick || collision == null || collision.contactCount == 0)
            return;

        Vector3 sum = Vector3.zero;
        int count = 0;
        float threshold = Mathf.Clamp(steepWallNormalYThreshold, -0.2f, 0.9f);

        ContactPoint[] contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            Vector3 normal = contacts[i].normal;
            if (normal.y >= threshold)
                continue;

            Vector3 horizontal = new Vector3(normal.x, 0f, normal.z);
            if (horizontal.sqrMagnitude < 0.0001f)
                continue;

            sum += horizontal.normalized;
            count++;
        }

        if (count <= 0)
            return;

        steepWallNormal = (sum / count).normalized;
        lastSteepWallContactTime = Time.time;
    }

    void OnCollisionEnter(Collision collision)
    {
        RegisterSteepWallContact(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        RegisterSteepWallContact(collision);
    }

    public bool IsGrounded() => isGrounded;
    public bool IsInputLocked => inputLocks.Count > 0;
    public bool IsMovementLocked => IsInputLocked;
    public bool IsInputBlocked() => IsInputLocked;

    public float JumpForce => jumpForce;
    public float CoyoteTime => 0f;
    public float JumpBufferTime => 0f;
    public float JumpCutMultiplier => 0.5f;
    public float GroundCheckRadius => col != null ? col.radius * 0.9f : 0.22f;
    public float GroundCheckDistance => groundCheckDistance;
    public float GroundedMinStableTime => 0f;

    public void LockInput(string source)
    {
        string key = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        if (!inputLocks.ContainsKey(key))
            inputLocks[key] = 0;
        inputLocks[key]++;
    }

    public void UnlockInput(string source)
    {
        string key = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        if (!inputLocks.TryGetValue(key, out int count))
            return;

        count = Mathf.Max(0, count - 1);
        if (count == 0)
            inputLocks.Remove(key);
        else
            inputLocks[key] = count;
    }

    public void LockMovement(string source) => LockInput(source);
    public void UnlockMovement(string source) => UnlockInput(source);
    public void ResetInputLocks(string source) => inputLocks.Clear();
    public void ForceResetLock() => inputLocks.Clear();

    public void SetInputBlocked(bool blocked, string reason)
    {
        if (blocked)
            LockInput(reason);
        else
            UnlockInput(reason);

        if (IsInputLocked)
        {
            moveDir = Vector3.zero;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void OnDisable()
    {
        jumpRequest = false;
        SetFatigueIndicatorVisible(false);
        SetHealthIndicatorVisible(false);
    }

    public void InjectMobileInput(Vector2 moveInput)
    {
        if (IsInputLocked)
            return;

        mobileInput = moveInput;
        hasMobileInput = moveInput.sqrMagnitude > 0.001f;

        mobileRunRequest = moveInput.y >= 0.9f
            && moveInput.magnitude >= 0.95f
            && Mathf.Abs(moveInput.x) <= 0.45f;

        // Apply immediately when keyboard is idle so mobile input never waits for next Update.
        if (!hasKeyboardInput)
            rawInput = hasMobileInput ? mobileInput : Vector2.zero;

        if (hasMobileInput && mobileRunRequest)
            isRunningRaw = true;
    }

    public void InjectMobileJump()
    {
        if (!IsInputLocked)
            jumpRequest = true;
    }
}