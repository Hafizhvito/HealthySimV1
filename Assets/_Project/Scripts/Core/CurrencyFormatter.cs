using System.Globalization;

public static class CurrencyFormatter
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static string Format(int amount)
    {
        int safe = UnityEngine.Mathf.Max(0, amount);
        return "Rp" + safe.ToString("N0", Indonesian);
    }
}
