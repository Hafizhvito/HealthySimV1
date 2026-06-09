using UnityEngine;

/// <summary>
/// Resets persistent gameplay session state when leaving SampleScene (e.g. pause → main menu).
/// Profile fields are reloaded from <see cref="PlayerData"/> / PlayerPrefs.
/// </summary>
public static class SessionResetService
{
    public static void ResetAllForMenuExit()
    {
        PlayerPersist.DestroyForNewSession();

        Time.timeScale = 1f;

        if (ModalStateManager.Instance != null)
            ModalStateManager.Instance.ForceResetAllModals();

        if (FadeManager.Instance != null)
            FadeManager.Instance.ReleaseInputBlock();

        PlayerData.Load();

        PlayerStats.Gender gender = string.Equals(PlayerData.JenisKelamin, "Perempuan", System.StringComparison.OrdinalIgnoreCase)
            ? PlayerStats.Gender.Female
            : PlayerStats.Gender.Male;

        string playerName = !string.IsNullOrWhiteSpace(PlayerData.PlayerName) ? PlayerData.PlayerName : "Pemain";
        float height = PlayerData.TinggiBadan > 0f ? PlayerData.TinggiBadan : 170f;
        float weight = PlayerData.BeratBadan > 0f ? PlayerData.BeratBadan : 65f;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.ResetForNewSession(playerName, height, weight, gender);

        if (TimeManager.Instance != null)
            TimeManager.Instance.StartGame();

        if (SessionFoodStash.Instance != null)
            SessionFoodStash.Instance.ClearStash();

        if (WorkSessionManager.Instance != null)
            WorkSessionManager.Instance.ResetForNewSession();

        if (GymProgressionSystem.Instance != null)
            GymProgressionSystem.Instance.ResetForNewSession();

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.ResetSession();

        SpawnPlayerManager.ClearSpawnTarget();
        ResetIntroAndBackstoryFlags();

        Debug.Log("[SessionReset] Gameplay session reset for menu exit.");
    }

    private static void ResetIntroAndBackstoryFlags()
    {
        PlayerPrefs.DeleteKey("StoryIntroPlayed");
        PlayerPrefs.DeleteKey("StoryIntroVersion");
        PlayerPrefs.DeleteKey("healthsim.backstory.shown");
        PlayerPrefs.Save();

        if (StoryIntroManager.Instance != null)
            StoryIntroManager.Instance.ResetIntroFlag();
    }
}
