/* 
    Script ini bisa digunakan di semua objek yang membutuhkan pergantian Scene
    Sesuia dengan nama scriptnya, script ini hanya untuk meng-load scene yang dituju oleh suatu object
    NOTE: kalau mau merubah codenya kasih tanda ============= untuk code barunya dan code lamanya cukup dijadikan comment saja
    Contoh
    private void Awake() { <---- misal ini code lamanya (jadikan comment kalau mau ubah code)
        
    }
    ====================== <---- kasih tanda ini
    private void Start() { <---- taruh dibawah tanda untuk code barunya 
        
    }

    Kalau udah fix hapus code lama gpp
*/



using UnityEngine;

public class SceneLoaderInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string _targetSceneName; // nanti tinggal ubah disini saja lewat inspector
    [SerializeField] private string _targetSpawnID; // nanti ubah juga ini untuk posisi spawnnya
    [SerializeField] private string _promptText = "Masuk";

    private Collider _cachedCollider;

    private void Awake()
    {
        _cachedCollider = GetComponent<Collider>();
        if (_cachedCollider == null)
            Debug.LogWarning("Collider tidak ditemukan");
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, _cachedCollider, transform);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText() => _promptText;
    public string GetInteractPrompt() => _promptText;
    public bool CanInteract(GameObject interactor) => true;
    public void Interact(GameObject interactor) => OnInteract(interactor);

    public void OnInteract(GameObject player)
    {
        FadeManager fadeManager = FadeManager.Instance;

        if (fadeManager == null)
        {
            Debug.LogWarning("FadeManager tidak ditemukan!");
            return;
        }

        SpawnPlayerManager.TargetSpawnID = _targetSpawnID;
        fadeManager.FadeToBlackAndLoad(_targetSceneName, 0.5f);
    }
}
