using UnityEngine;

/// <summary>
/// Single source of truth for facility open hours shown in HUD and door interactables.
/// </summary>
public static class FacilityHours
{
    public const float WorkOpenHour = 7f;
    public const float WorkCloseHour = 15f;
    public const float GymOpenHour = 6f;
    public const float GymCloseHour = 22f;

    public const string WorkHoursLabel = "07.00 - 15.00";
    public const string GymHoursLabel = "06.00 - 22.00";

    public static bool IsWorkOpen(float hour)
    {
        return hour >= WorkOpenHour && hour < WorkCloseHour;
    }

    public static bool IsGymOpen(float hour)
    {
        return hour >= GymOpenHour && hour < GymCloseHour;
    }

    public static bool IsWorkOpen(TimeManager timeManager)
    {
        return timeManager != null && IsWorkOpen(timeManager.CurrentHour);
    }

    public static bool IsGymOpen(TimeManager timeManager)
    {
        return timeManager != null && IsGymOpen(timeManager.CurrentHour);
    }
}
