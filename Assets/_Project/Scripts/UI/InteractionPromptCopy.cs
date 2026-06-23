using System;
using UnityEngine;

/// <summary>
/// Player-facing copy for interaction prompts.
/// Editor and mobile builds use touch/proximity wording; desktop builds keep keyboard copy.
/// </summary>
public static class InteractionPromptCopy
{
    public static bool UseMobileWording =>
        Application.isEditor || Application.isMobilePlatform;

    public static string FormatInteractionPrompt(string desktopPrompt)
    {
        if (string.IsNullOrWhiteSpace(desktopPrompt))
            return string.Empty;

        if (!UseMobileWording)
            return desktopPrompt.Trim();

        return ReplaceKeyboardWording(desktopPrompt.Trim());
    }

    public static string FormatProximityButtonLabel(string rawPrompt)
    {
        if (string.IsNullOrWhiteSpace(rawPrompt))
            return string.Empty;

        string text = UseMobileWording
            ? ReplaceKeyboardWording(rawPrompt.Trim())
            : StripKeyboardPrefix(rawPrompt.Trim());

        text = text.Trim();
        if (text.Length == 0)
            return string.Empty;

        if (ContainsIgnoreCase(text, "sekali lagi untuk tidur"))
            return ContainsIgnoreCase(text, "risiko") ? "Konfirmasi tidur" : "Tidur sekarang";

        const int maxLength = 42;
        if (text.Length > maxLength)
            text = text.Substring(0, maxLength - 1).TrimEnd() + "…";

        return text;
    }

    public static string FormatBannerMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return string.Empty;

        return UseMobileWording
            ? ReplaceKeyboardWording(rawMessage.Trim())
            : rawMessage.Trim();
    }

    private static string ReplaceKeyboardWording(string text)
    {
        text = ReplaceIgnoreCase(text, "Tekan E sekali lagi", "Tap tombol biru sekali lagi");
        text = ReplaceIgnoreCase(text, "Tekan E untuk ", "Tap tombol biru untuk ");
        text = ReplaceIgnoreCase(text, "Tekan E ", "Tap tombol biru ");
        text = ReplaceIgnoreCase(text, "tekan E ", "tap tombol biru ");
        return text;
    }

    private static string StripKeyboardPrefix(string text)
    {
        if (text.StartsWith("Tekan E untuk ", StringComparison.OrdinalIgnoreCase))
            text = text.Substring("Tekan E untuk ".Length);

        if (text.StartsWith("Tekan E ", StringComparison.OrdinalIgnoreCase))
            text = text.Substring("Tekan E ".Length);

        return text.Trim();
    }

    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        int index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return source;

        return source.Remove(index, oldValue.Length).Insert(index, newValue);
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
