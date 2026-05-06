using UnityEngine;
using DG.Tweening;

public class UISpinner : MonoBehaviour
{
    [SerializeField] private float rotationDuration = 1f;

    private void Start()
    {
        // muter 360 derajat
        transform.DORotate(new Vector3(0f, 0f, -360f), rotationDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
