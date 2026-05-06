using System;
using UnityEngine;

public class ModalUIStateManager : MonoBehaviour
{
    public static ModalUIStateManager Instance { get; private set; }

    private int modalDepth;

    public bool IsAnyModalOpen => modalDepth > 0;

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

    public void PushModal(string source)
    {
        if (ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.OpenModal(source);
            return;
        }

        bool wasOpen = IsAnyModalOpen;
        modalDepth = Mathf.Max(0, modalDepth + 1);
        if (wasOpen != IsAnyModalOpen)
            OnModalStateChanged?.Invoke(IsAnyModalOpen);

        Debug.Log($"[ModalUI] OPEN by {source}. Depth={modalDepth}");
    }

    public void PopModal(string source)
    {
        if (ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.CloseModal(source);
            return;
        }

        bool wasOpen = IsAnyModalOpen;
        modalDepth = Mathf.Max(0, modalDepth - 1);
        if (wasOpen != IsAnyModalOpen)
            OnModalStateChanged?.Invoke(IsAnyModalOpen);

        Debug.Log($"[ModalUI] CLOSE by {source}. Depth={modalDepth}");
    }

    public void ResetState(string source)
    {
        if (ModalStateManager.Instance != null)
        {
            ModalStateManager.Instance.ForceResetAllModals();
            return;
        }

        bool wasOpen = IsAnyModalOpen;
        modalDepth = 0;
        if (wasOpen)
            OnModalStateChanged?.Invoke(false);

        Debug.Log($"[ModalUI] RESET by {source}.");
    }
}
