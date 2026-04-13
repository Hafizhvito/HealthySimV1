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
                recommendedAction = "Tubuhmu ingat semua yang kamu abaikan.",
                lines = new[]
                {
                    "Jakarta. Pagi yang belum sepenuhnya bangun.",
                    "Kamu bergegas, tapi tubuhmu sudah tertinggal sejak semalam.",
                    "Hari ini bukan soal sempurna. Tapi soal memilih lebih baik dari kemarin."
                }
            },
            new StoryTemplate
            {
                title = "Target Gaya Hidup Sehat",
                recommendedAction = "Kamu sudah tahu apa yang harus dilakukan. Pertanyaannya.. mulai kapan?",
                lines = new[]
                {
                    "Ada hari-hari ketika kamu memutuskan untuk berubah.",
                    "Bukan besok. Bukan minggu depan. Hari ini.",
                    "Tubuhmu sudah lama menunggu keputusan itu."
                }
            },
            new StoryTemplate
            {
                title = "Hari Ujian Kebiasaan",
                recommendedAction = "Kebiasaan buruk tidak terasa salah — sampai tubuhmu yang berbicara.",
                lines = new[]
                {
                    "Tidak ada yang tiba-tiba sakit. Tidak ada yang tiba-tiba sehat.",
                    "Semuanya dibangun diam-diam dari pilihan yang kamu anggap kecil.",
                    "Hari ini, pilihan itu ada di tanganmu."
                }
            },
            new StoryTemplate
            {
                title = "Harga Sebuah Pilihan",
                recommendedAction = "Kesehatan bukan hadiah. Itu hasil dari keputusan yang kamu buat setiap hari.",
                lines = new[]
                {
                    "Kamu tidak bisa membeli waktu yang sudah terbuang.",
                    "Tapi kamu masih bisa memilih apa yang kamu lakukan dengan waktu yang tersisa.",
                    "Mulai dari sini. Mulai dari sekarang."
                }
            }
        };
    }
}
