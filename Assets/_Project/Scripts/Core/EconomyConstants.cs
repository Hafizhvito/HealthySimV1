using UnityEngine;

/// <summary>
/// Central economy tuning for a 12-day run. Asset prices stay small (e.g. 32);
/// <see cref="PriceScale"/> converts them to realistic Rupiah at runtime.
/// </summary>
public static class EconomyConstants
{
    public const int PriceScale = 1000;

    // ~2–3 hari makan campuran tanpa kerja, lalu player perlu shift.
    public const int StartingMoney = 600_000;

    // Serialized prefabs/scenes may still hold pre-scale cash (e.g. 500).
    public const int LegacyStartingMoneyMax = 1_000;

    // Satu shift pagi ≈ 5–7× makan rumah; sore/siang ≈ 4–5×.
    public const int BasePayMorning = 280_000;
    public const int BasePayAfternoon = 210_000;
    public const int BasePayEvening = 210_000;

    public const float HomePriceMultiplier = 0.65f;
    public const float RestaurantPriceMultiplier = 1.25f;
    public const float BazaarDiscountMultiplier = 0.22f;

    public static int ScalePrice(int assetPrice)
    {
        return Mathf.Max(1, assetPrice * PriceScale);
    }
}
