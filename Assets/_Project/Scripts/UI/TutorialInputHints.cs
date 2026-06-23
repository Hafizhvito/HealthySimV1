using UnityEngine;

/// <summary>
/// Tutorial copy for movement/interaction hints.
/// Editor and mobile builds use proximity/joystick wording; desktop builds keep keyboard copy.
/// </summary>
public static class TutorialInputHints
{
    public static bool UseProximityCopy => InteractionPromptCopy.UseMobileWording;

    public static string MovementTitle => "Cara Bergerak";

    public static string MovementBody => UseProximityCopy
        ? "Gunakan joystick di kiri bawah layar untuk berjalan."
        : "Gunakan W A S D untuk berjalan.\nTahan Shift untuk berlari.";

    public static string InteractionTitle => "Cara Berinteraksi";

    public static string InteractionBody => UseProximityCopy
        ? "Dekati objek atau NPC.\nTombol biru di kanan bawah akan muncul — tap untuk berinteraksi."
        : "Dekati objek atau NPC,\nlalu tekan E untuk berinteraksi.";

    public static string NpcToast => UseProximityCopy
        ? "Coba dekati NPC — tombol biru akan muncul untuk ngobrol"
        : "Coba dekati NPC dan tekan E untuk ngobrol";

    public static string FoodToast => UseProximityCopy
        ? "Dekati makanan — tap tombol biru untuk makan atau simpan ke stash"
        : "Tekan E di dekat makanan untuk makan atau simpan ke stash";
}
