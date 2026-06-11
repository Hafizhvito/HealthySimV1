using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UniversalInteractionController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float tieBreakForwardWeight = 0.2f;
    [SerializeField] private float interactRadius = 0.3f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private float inputDebounceSeconds = 0.2f;
    [SerializeField] private float minForwardDot = -0.2f;
    [SerializeField] private float bubbleHeight = 1.6f;
    [SerializeField] private float bubbleMaxDistance = 6.5f;
    [SerializeField] private float bubbleScale = 0.0046f;
    [SerializeField] private float bubbleBgSize = 118f;
    [SerializeField] private float bubbleFontSize = 80f;
    [SerializeField] private Color bubbleNormalColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color bubbleSelectedColor = new Color(1f, 0.78f, 0.2f, 0.92f);

    [Header("Mobile Proximity Button")]
    [SerializeField] private bool preferProximityButtonOnMobile = true;
    [SerializeField] private bool hideWorldBubblesOnMobile = false;

    private const int PresentationVersion = 2;
    [SerializeField] private int presentationVersion;

    private float nextInteractAllowedTime;
    private IInteractable currentInteractable;
    private Transform currentInteractableTransform;
    private Collider currentInteractableCollider;
    private bool pendingInteract;

    private Canvas hintCanvas;
    private TextMeshProUGUI hintText;
    private ModalStateManager modalStateManager;
    private bool forceSuppressPrompt;
    private int visibleBubbleCount;

    private readonly Dictionary<Transform, BubbleVisual> bubbleByTransform = new Dictionary<Transform, BubbleVisual>();
    private static Sprite bubbleCircleSprite;

    private sealed class BubbleVisual
    {
        public Canvas Canvas;
        public RectTransform RootRect;
        public Image Background;
        public TextMeshProUGUI Text;
        public Transform FollowTarget;
        public IInteractable Interactable;
        public Collider TargetCollider;
        public BubbleTapHandler TapHandler;
    }

    private sealed class BubbleTapHandler : MonoBehaviour, IPointerClickHandler
    {
        private UniversalInteractionController owner;
        private IInteractable target;
        private Transform targetTransform;
        private Collider targetCollider;

        public void Initialize(UniversalInteractionController controller)
        {
            owner = controller;
        }

        public void SetTarget(IInteractable interactable, Transform transform, Collider collider)
        {
            target = interactable;
            targetTransform = transform;
            targetCollider = collider;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner == null)
                return;

            owner.TryInteract(target, targetTransform, targetCollider);
        }
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        modalStateManager = FindFirstObjectByType<ModalStateManager>();
        ApplyPresentationDefaults();

        EnsureHintUI();
        SetHintVisible(false, string.Empty);
    }

    private void ApplyPresentationDefaults()
    {
        if (presentationVersion >= PresentationVersion)
            return;

        hideWorldBubblesOnMobile = false;
        if (bubbleScale < 0.0042f)
            bubbleScale = 0.0046f;
        if (bubbleMaxDistance < 6f)
            bubbleMaxDistance = 6.5f;
        if (bubbleBgSize < 110f)
            bubbleBgSize = 118f;
        if (bubbleFontSize < 76f)
            bubbleFontSize = 80f;

        presentationVersion = PresentationVersion;
        RefreshBubbleWorldScale();
    }

    private void RefreshBubbleWorldScale()
    {
        foreach (var pair in bubbleByTransform)
        {
            if (pair.Value?.Canvas == null)
                continue;

            pair.Value.Canvas.transform.localScale = Vector3.one * bubbleScale;
        }
    }

    void Update()
    {
        if (IsModalBlocked())
            pendingInteract = false;

        UpdateHint();
        UpdateBubbles();

        if (PressedInteractionThisFrame())
            pendingInteract = true;
    }

    void FixedUpdate()
    {
        ResolveCurrentInteractable();

        if (!pendingInteract)
            return;

        pendingInteract = false;

        TryInteract(currentInteractable, currentInteractableTransform, currentInteractableCollider);
    }

    private void ResolveCurrentInteractable()
    {
        currentInteractable = null;

        Transform playerTransform = transform;
        if (playerTransform == null)
            return;

        var entries = InteractableRegistry.Entries;
        if (entries == null || entries.Count == 0)
            return;

        Vector3 playerPos = playerTransform.position;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : playerTransform.forward;

        float bestScore = float.NegativeInfinity;
        IInteractable best = null;
        Transform bestTransform = null;
        Collider bestCollider = null;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Collider == null || entry.Interactable == null || entry.Transform == null)
                continue;

            if (!entry.Collider.enabled || !entry.Transform.gameObject.activeInHierarchy)
                continue;

            Vector3 targetPoint = entry.Collider.bounds.center;
            Vector3 toTarget = targetPoint - playerPos;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > interactDistance)
                continue;

            Vector3 dir = toTarget / distance;
            float forwardDot = Vector3.Dot(forward, dir);
            if (forwardDot < minForwardDot)
                continue;

            Vector3 castOrigin = playerPos + Vector3.up * 1.1f;
            if (!HasLineOfSight(castOrigin, targetPoint, entry.Collider, distance + 0.8f))
                continue;

            // Area-first selection: nearest valid target wins; camera facing only breaks close ties.
            float distanceScore = 1f - Mathf.Clamp01(distance / interactDistance);
            float score = distanceScore + (forwardDot * tieBreakForwardWeight);
            if (score > bestScore)
            {
                bestScore = score;
                best = entry.Interactable;
                bestTransform = entry.Transform;
                bestCollider = entry.Collider;
            }
        }

        currentInteractable = best;
        currentInteractableTransform = bestTransform;
        currentInteractableCollider = bestCollider;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, Collider targetCollider, float distance)
    {
        Vector3 dir = (to - from).normalized;
        RaycastHit[] hits = Physics.SphereCastAll(
            from,
            interactRadius * 0.5f,
            dir,
            distance,
            interactMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            // Ignore the player body/camera colliders so interactables are not blocked by self-hit.
            if (hitCollider.transform.IsChildOf(transform))
                continue;

            return hitCollider == targetCollider || hitCollider.transform.IsChildOf(targetCollider.transform);
        }

        return false;
    }

    private void EnsureHintUI()
    {
        if (hintCanvas != null && hintText != null)
            return;

        var canvasObj = new GameObject("InteractionHintCanvas");
        hintCanvas = canvasObj.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasObj.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("InteractionHintText");
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.05f);
        rect.anchorMax = new Vector2(0.5f, 0.05f);
        rect.sizeDelta = new Vector2(520f, 56f);
        rect.anchoredPosition = Vector2.zero;

        hintText = textObj.AddComponent<TextMeshProUGUI>();
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.fontSize = 20f;
        hintText.color = new Color(1f, 0.96f, 0.82f, 1f);
        hintText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void UpdateHint()
    {
        if (IsModalBlocked())
        {
            SetHintVisible(false, string.Empty);
            return;
        }

        if (ShouldUseProximityButton())
        {
            SetHintVisible(false, string.Empty);
            return;
        }

        // Prevent duplicate cue when world bubbles are already visible.
        if (visibleBubbleCount > 0)
        {
            SetHintVisible(false, string.Empty);
            return;
        }

        if (currentInteractable != null)
            SetHintVisible(true, currentInteractable.GetInteractionText());
        else
            SetHintVisible(false, string.Empty);
    }

    private void UpdateBubbles()
    {
        if (playerCamera == null)
            return;

        if (IsModalBlocked())
        {
            visibleBubbleCount = 0;
            HideAllBubbles();
            return;
        }

        if (ShouldUseProximityButton() && hideWorldBubblesOnMobile)
        {
            visibleBubbleCount = 0;
            HideAllBubbles();
            return;
        }

        var entries = InteractableRegistry.Entries;
        HashSet<Transform> keep = new HashSet<Transform>();
        int visibleCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Transform == null || entry.Collider == null || !entry.Transform.gameObject.activeInHierarchy)
                continue;

            Vector3 center = entry.Collider.bounds.center;
            float distanceToPlayer = Vector3.Distance(transform.position, center);
            if (distanceToPlayer > bubbleMaxDistance)
                continue;

            keep.Add(entry.Transform);
            BubbleVisual bubble = GetOrCreateBubble(entry.Transform);
            bubble.Interactable = entry.Interactable;
            bubble.TargetCollider = entry.Collider;
            if (bubble.TapHandler != null)
                bubble.TapHandler.SetTarget(entry.Interactable, entry.Transform, entry.Collider);

            Vector3 worldPos = center + Vector3.up * bubbleHeight;
            bubble.Canvas.transform.position = worldPos;
            bubble.Canvas.transform.rotation = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
            bubble.Canvas.gameObject.SetActive(true);
            visibleCount++;

            bool selected = currentInteractable != null && ReferenceEquals(currentInteractable, entry.Interactable);
            bubble.Background.color = selected ? bubbleSelectedColor : bubbleNormalColor;
            bubble.Text.color = selected ? new Color(0.14f, 0.1f, 0.02f, 1f) : new Color(1f, 1f, 1f, 0.92f);

            float proximity = 1f - Mathf.Clamp01(distanceToPlayer / bubbleMaxDistance);
            float responsiveScale = Mathf.Lerp(1f, 1.22f, proximity);
            float selectedScale = selected ? 1.12f : 1f;
            bubble.RootRect.localScale = Vector3.one * (responsiveScale * selectedScale);
            bubble.Text.text = GetBubbleLabel();
        }

        var keys = new List<Transform>(bubbleByTransform.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Transform key = keys[i];
            if (!keep.Contains(key))
                SetBubbleVisible(key, false);
        }

        visibleBubbleCount = visibleCount;
    }

    private BubbleVisual GetOrCreateBubble(Transform target)
    {
        if (bubbleByTransform.TryGetValue(target, out BubbleVisual existing))
            return existing;

        GameObject canvasObj = new GameObject($"E_Bubble_{target.name}");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 250;
        canvas.worldCamera = playerCamera != null ? playerCamera : Camera.main;
        canvas.transform.localScale = Vector3.one * bubbleScale;
        canvasObj.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        float bgSize = Mathf.Max(96f, bubbleBgSize);
        float textSize = Mathf.Max(64f, bubbleFontSize);
        float canvasSize = bgSize + 42f;
        float textBoxSize = bgSize - 18f;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasSize, canvasSize);

        GameObject bgObj = new GameObject("BubbleBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(bgSize, bgSize);

        Image bg = bgObj.AddComponent<Image>();
        bg.raycastTarget = true;
        bg.sprite = GetOrCreateBubbleCircleSprite();
        bg.type = Image.Type.Simple;
        bg.color = bubbleNormalColor;

        Button button = bgObj.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;

        BubbleTapHandler tapHandler = bgObj.AddComponent<BubbleTapHandler>();
        tapHandler.Initialize(this);

        GameObject textObj = new GameObject("BubbleText");
        textObj.transform.SetParent(bgObj.transform, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(textBoxSize, textBoxSize);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = GetBubbleLabel();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = textSize;
        text.raycastTarget = false;
        text.color = Color.white;

        BubbleVisual bubble = new BubbleVisual
        {
            Canvas = canvas,
            RootRect = bgRect,
            Background = bg,
            Text = text,
            FollowTarget = target,
            TapHandler = tapHandler
        };

        bubbleByTransform[target] = bubble;
        return bubble;
    }

    private void SetBubbleVisible(Transform target, bool visible)
    {
        if (!bubbleByTransform.TryGetValue(target, out BubbleVisual bubble) || bubble.Canvas == null)
            return;

        bubble.Canvas.gameObject.SetActive(visible);
    }

    void OnDisable()
    {
        CleanupBubbles();
    }

    private void CleanupBubbles()
    {
        foreach (var pair in bubbleByTransform)
        {
            if (pair.Value != null && pair.Value.Canvas != null)
                Destroy(pair.Value.Canvas.gameObject);
        }

        bubbleByTransform.Clear();
    }

    private void SetHintVisible(bool visible, string text)
    {
        if (hintText == null)
            return;

        hintText.gameObject.SetActive(visible);
        if (visible)
            hintText.text = text;
    }

    public void SetGlobalPromptSuppressed(bool suppressed)
    {
        forceSuppressPrompt = suppressed;
        if (suppressed)
            SetHintVisible(false, string.Empty);
    }

    public void HideAllBubbles()
    {
        var keys = new List<Transform>(bubbleByTransform.Keys);
        for (int i = 0; i < keys.Count; i++)
            SetBubbleVisible(keys[i], false);
    }

    private bool IsModalBlocked()
    {
        bool modalOpen = modalStateManager != null && modalStateManager.IsAnyModalOpen;
        return forceSuppressPrompt || modalOpen;
    }

    private bool PressedInteractionThisFrame()
    {
        bool legacy = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        legacy = Input.GetKeyDown(KeyCode.E);
#endif

#if ENABLE_INPUT_SYSTEM
        bool inputSystem = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        return legacy || inputSystem;
#else
        return legacy;
#endif
    }

    public bool ShouldUseProximityButton()
    {
        // Active on Android builds and in Editor/desktop so proximity UX can be tested while developing.
        return preferProximityButtonOnMobile;
    }

    public bool TryGetProximityActionLabel(out string label)
    {
        label = string.Empty;

        if (IsModalBlocked() || !ShouldUseProximityButton())
            return false;

        if (!TryResolveNearestInteractableForUi(out IInteractable interactable, out _, out _, bubbleMaxDistance))
            return false;

        if (!interactable.CanInteract(gameObject))
            return false;

        label = FormatProximityActionLabel(interactable.GetInteractionText());
        return !string.IsNullOrWhiteSpace(label);
    }

    public void TriggerInteractFromMobile()
    {
        if (TryResolveNearestInteractableForUi(out IInteractable interactable, out Transform targetTransform, out Collider targetCollider, bubbleMaxDistance))
        {
            TryInteract(interactable, targetTransform, targetCollider, skipForwardCheck: true);
            return;
        }

        pendingInteract = true;
    }

    private void TryInteract(IInteractable interactable, Transform targetTransform, Collider targetCollider, bool skipForwardCheck = false)
    {
        if (IsModalBlocked())
            return;

        if (interactable == null || targetTransform == null || targetCollider == null)
            return;

        if (!targetCollider.enabled || !targetTransform.gameObject.activeInHierarchy)
            return;

        if (Time.time < nextInteractAllowedTime)
            return;

        Vector3 playerPos = transform.position;
        Vector3 targetPoint = targetCollider.bounds.center;
        float distance = Vector3.Distance(playerPos, targetPoint);
        if (distance <= 0.001f || distance > interactDistance)
            return;

        if (!skipForwardCheck)
        {
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            Vector3 dir = (targetPoint - playerPos).normalized;
            float forwardDot = Vector3.Dot(forward, dir);
            if (forwardDot < minForwardDot)
                return;
        }

        Vector3 castOrigin = playerPos + Vector3.up * 1.1f;
        if (!HasLineOfSight(castOrigin, targetPoint, targetCollider, distance + 0.8f))
            return;

        if (!interactable.CanInteract(gameObject))
            return;

        nextInteractAllowedTime = Time.time + inputDebounceSeconds;

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, interactable.GetInteractionText());

        interactable.Interact(gameObject);
    }

    private bool TryResolveNearestInteractableForUi(
        out IInteractable interactable,
        out Transform targetTransform,
        out Collider targetCollider,
        float maxDistance = -1f)
    {
        interactable = null;
        targetTransform = null;
        targetCollider = null;

        if (maxDistance <= 0f)
            maxDistance = interactDistance;

        var entries = InteractableRegistry.Entries;
        if (entries == null || entries.Count == 0)
            return false;

        Vector3 playerPos = transform.position;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Collider == null || entry.Interactable == null || entry.Transform == null)
                continue;

            if (!entry.Collider.enabled || !entry.Transform.gameObject.activeInHierarchy)
                continue;

            Vector3 targetPoint = entry.Collider.bounds.center;
            float distance = Vector3.Distance(playerPos, targetPoint);
            if (distance <= 0.001f || distance > maxDistance)
                continue;

            if (!entry.Interactable.CanInteract(gameObject))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                interactable = entry.Interactable;
                targetTransform = entry.Transform;
                targetCollider = entry.Collider;
            }
        }

        return interactable != null;
    }

    private static string FormatProximityActionLabel(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        string text = rawText.Trim();

        if (text.StartsWith("Tekan E untuk ", System.StringComparison.OrdinalIgnoreCase))
            text = text.Substring("Tekan E untuk ".Length);

        if (text.StartsWith("Tekan E ", System.StringComparison.OrdinalIgnoreCase))
            text = text.Substring("Tekan E ".Length);

        return text.Trim();
    }

    private string GetBubbleLabel()
    {
        if (Application.isMobilePlatform)
            return "?";

        MobileInputController mobile = MobileInputController.Instance;
        if (mobile != null && mobile.IsTouchUiEnabled)
            return "?";

        return "E";
    }

    private static Sprite GetOrCreateBubbleCircleSprite()
    {
        if (bubbleCircleSprite != null)
            return bubbleCircleSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "InteractionBubbleCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = (y * size) + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        bubbleCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        bubbleCircleSprite.name = "InteractionBubbleCircle";
        return bubbleCircleSprite;
    }
}
