using UnityEngine;
using TMPro;

public class UILoadingDots : MonoBehaviour
{
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private float interval = 0.4f;
    [SerializeField] private string baseText = "Loading";

    private float timer;
    private int dotCount;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            dotCount = (dotCount + 1) % 4;

            loadingText.text = baseText + new string('.', dotCount);
        }
    }
}
