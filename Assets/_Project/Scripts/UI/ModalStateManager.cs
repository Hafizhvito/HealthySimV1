using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModalStateManager : MonoBehaviour
{
    public static ModalStateManager Instance { get; private set; }

    private readonly Dictionary<string, int> modalCountsBySource = new Dictionary<string, int>();
    private PlayerController playerController;

    public bool IsAnyModalOpen => GetActiveModalCount() > 0;
    public event Action<bool> OnModalStateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        ForceResetAllModals();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playerController = FindFirstObjectByType<PlayerController>();
        ForceResetAllModals();
    }

    public void OpenModal(string modalName)
    {
        EnsurePlayerController();

        bool wasOpen = IsAnyModalOpen;
        string key = string.IsNullOrWhiteSpace(modalName) ? "UnknownModal" : modalName;

        if (!modalCountsBySource.ContainsKey(key))
            modalCountsBySource[key] = 0;

        modalCountsBySource[key]++;

        if (playerController != null)
            playerController.LockInput(key);

        if (!wasOpen)
            OnModalStateChanged?.Invoke(true);

        Debug.Log($"[ModalState] Open {key} (active={GetActiveModalCount()})");
    }

    public void CloseModal(string modalName)
    {
        EnsurePlayerController();

        bool wasOpen = IsAnyModalOpen;
        string key = string.IsNullOrWhiteSpace(modalName) ? "UnknownModal" : modalName;

        if (!modalCountsBySource.TryGetValue(key, out int count))
        {
            Debug.LogWarning($"[ModalState] Close diabaikan: {key} tidak tercatat sebagai modal aktif.");
            return;
        }

        count = Mathf.Max(0, count - 1);
        if (count == 0)
            modalCountsBySource.Remove(key);
        else
            modalCountsBySource[key] = count;

        if (playerController != null)
            playerController.UnlockInput(key);

        if (wasOpen && !IsAnyModalOpen)
            OnModalStateChanged?.Invoke(false);

        Debug.Log($"[ModalState] Close {key} (active={GetActiveModalCount()})");
    }

    public void ForceResetAllModals()
    {
        EnsurePlayerController();

        modalCountsBySource.Clear();
        if (playerController != null)
            playerController.ResetInputLocks("ForceReset");

        OnModalStateChanged?.Invoke(false);
        Debug.Log("[ModalState] Force reset all modals.");
    }

    private int GetActiveModalCount()
    {
        int total = 0;
        foreach (var pair in modalCountsBySource)
            total += pair.Value;

        return total;
    }

    private void EnsurePlayerController()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
    }
}