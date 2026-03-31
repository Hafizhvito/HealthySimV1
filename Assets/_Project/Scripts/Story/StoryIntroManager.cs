using System.Collections;
using UnityEngine;

public class StoryIntroManager : MonoBehaviour
{
    public static StoryIntroManager Instance { get; private set; }

    public event System.Action OnIntroFlowCompleted;

    private StoryTemplate[] templates;

    public StoryTemplate ActiveTemplate { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildTemplates();
    }

    public IEnumerator StartIntroFlow()
    {
        if (templates == null || templates.Length == 0)
        {
            OnIntroFlowCompleted?.Invoke();
            yield break;
        }

        int index = SessionSeedManager.Instance != null
            ? SessionSeedManager.Instance.NextInt(0, templates.Length)
            : Random.Range(0, templates.Length);

        ActiveTemplate = templates[index];

        Debug.Log($"[StoryIntro] Template terpilih: {ActiveTemplate.title}");

        var cutscene = FindFirstObjectByType<IntroCutsceneController>();
        if (cutscene != null)
            yield return cutscene.PlayIntro(ActiveTemplate);

        OnIntroFlowCompleted?.Invoke();
    }

    private void BuildTemplates()
    {
        templates = new[]
        {
            new StoryTemplate
            {
                title = "Pagi Sibuk di Kota",
                recommendedAction = "Cari pilihan sarapan sehat terlebih dahulu.",
                lines = new[]
                {
                    "Pagi ini kamu memulai hari di tengah kota yang ramai.",
                    "Waktu hanya lima menit, setiap keputusan akan berdampak.",
                    "Pilih interaksi dengan bijak untuk menjaga energi dan mood."
                }
            },
            new StoryTemplate
            {
                title = "Target Gaya Hidup Sehat",
                recommendedAction = "Fokus ke aksi positif dan makanan sehat.",
                lines = new[]
                {
                    "Kamu menargetkan hidup lebih sehat mulai hari ini.",
                    "Ada banyak distraksi, tapi tubuhmu butuh keputusan yang tepat.",
                    "Amati lingkungan, lalu lakukan aksi terbaikmu."
                }
            },
            new StoryTemplate
            {
                title = "Hari Ujian Kebiasaan",
                recommendedAction = "Jangan abaikan percakapan yang memberi motivasi.",
                lines = new[]
                {
                    "Hari ini jadi ujian kebiasaan harianmu.",
                    "Pilihan kecil seperti makanan dan obrolan bisa mengubah hasil akhir.",
                    "Bangun ritme yang sehat sebelum waktu habis."
                }
            }
        };
    }
}
