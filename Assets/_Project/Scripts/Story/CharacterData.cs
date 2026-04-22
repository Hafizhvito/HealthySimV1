using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCharacter", menuName = "HealthSim/Story/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Karakter Utama";
    public int startingAge = 18;

    [Header("Backstory")]
    [TextArea(3, 12)] public string backstoryText = "";
    [TextArea(2, 8)] public string backstorySummary = "Kamu datang ke kota ini dengan kebiasaan hidup yang belum teratur.";
    [TextArea(2, 8)] public string[] additionalBackstoryLines = new string[]
    {
        "Kamu sering menunda makan sehat karena ritme kerja yang padat.",
        "Hari ini kamu memutuskan untuk mulai membangun kebiasaan yang lebih baik."
    };

    public void AppendBackstory(StringBuilder sb)
    {
        if (sb == null)
            return;

        if (!string.IsNullOrWhiteSpace(backstoryText))
        {
            sb.Append(backstoryText.Trim());
            return;
        }

        if (!string.IsNullOrWhiteSpace(backstorySummary))
            sb.AppendLine(backstorySummary.Trim());

        if (additionalBackstoryLines == null)
            return;

        for (int i = 0; i < additionalBackstoryLines.Length; i++)
        {
            string line = additionalBackstoryLines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(line.Trim());
        }
    }
}
