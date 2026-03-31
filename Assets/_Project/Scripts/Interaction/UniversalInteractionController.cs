using UnityEngine;
using TMPro;
using UnityEngine.UI;
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
    [SerializeField] private float bubbleMaxDistance = 5.5f;
    [SerializeField] private float bubbleScale = 0.0035f;
    [SerializeField] private Color bubbleNormalColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color bubbleSelectedColor = new Color(1f, 0.78f, 0.2f, 0.92f);

    private float nextInteractAllowedTime;
    private IInteractable currentInteractable;
    private bool pendingInteract;

    private Canvas hintCanvas;
    private TextMeshProUGUI hintText;
    private ModalStateManager modalStateManager;
    private bool forceSuppressPrompt;
    private int visibleBubbleCount;

    private readonly Dictionary<Transform, BubbleVisual> bubbleByTransform = new Dictionary<Transform, BubbleVisual>();

    private sealed class BubbleVisual
    {
        public Canvas Canvas;
        public RectTransform RootRect;
        public Image Background;
        public TextMeshProUGUI Text;
        public Transform FollowTarget;
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        modalStateManager = FindFirstObjectByType<ModalStateManager>();

        EnsureHintUI();
        SetHintVisible(false, string.Empty);
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

        if (currentInteractable == null)
            return;

        if (Time.time < nextInteractAllowedTime)
            return;

        if (!currentInteractable.CanInteract(gameObject))
            return;

        nextInteractAllowedTime = Time.time + inputDebounceSeconds;

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(PlayerActionTracker.ActionType.GenericInteraction, currentInteractable.GetInteractionText());

        currentInteractable.Interact(gameObject);
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
            }
        }

        currentInteractable = best;
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

            Vector3 worldPos = center + Vector3.up * bubbleHeight;
            bubble.Canvas.transform.position = worldPos;
            bubble.Canvas.transform.rotation = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
            bubble.Canvas.gameObject.SetActive(true);
            visibleCount++;

            bool selected = currentInteractable != null && ReferenceEquals(currentInteractable, entry.Interactable);
            bubble.Background.color = selected ? bubbleSelectedColor : bubbleNormalColor;
            bubble.Text.color = selected ? new Color(0.14f, 0.1f, 0.02f, 1f) : new Color(1f, 1f, 1f, 0.92f);
            bubble.RootRect.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
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
        canvas.sortingOrder = 20;
        canvas.transform.localScale = Vector3.one * bubbleScale;
        canvasObj.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(160f, 160f);

        GameObject bgObj = new GameObject("BubbleBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(98f, 98f);

        Image bg = bgObj.AddComponent<Image>();
        bg.raycastTarget = false;
        bg.color = bubbleNormalColor;

        GameObject textObj = new GameObject("BubbleText");
        textObj.transform.SetParent(bgObj.transform, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(82f, 82f);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "E";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 68f;
        text.raycastTarget = false;
        text.color = Color.white;

        BubbleVisual bubble = new BubbleVisual
        {
            Canvas = canvas,
            RootRect = bgRect,
            Background = bg,
            Text = text,
            FollowTarget = target
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
}
