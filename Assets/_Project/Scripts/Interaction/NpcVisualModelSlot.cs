using UnityEngine;

/// <summary>
/// Ganti tampilan capsule NPC dengan model karakter.
/// Drag prefab ke field Character Prefab, atau drag prefab sebagai child di bawah NpcVisual.
/// </summary>
[DisallowMultipleComponent]
public class NpcVisualModelSlot : MonoBehaviour
{
    public const string VisualRootName = "NpcVisual";

    [Header("Character Model")]
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform visualRoot;

    [Header("Placement")]
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEuler = Vector3.zero;
    [SerializeField] private float modelScale = 1f;
    [SerializeField] private bool faceNegativeZ = true;

    [Header("Placeholder")]
    [SerializeField] private bool hidePlaceholderMesh = true;

    private GameObject spawnedVisual;

    public GameObject CharacterPrefab
    {
        get => characterPrefab;
        set
        {
            characterPrefab = value;
            ApplyVisual();
        }
    }

    private void Awake()
    {
        ApplyVisual();
    }

    public void ApplyVisual()
    {
        EnsureVisualRoot();
        HidePlaceholderIfNeeded();

        if (TryUseExistingChildVisual())
            return;

        if (characterPrefab == null)
            return;

        ClearSpawnedVisual();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            spawnedVisual = UnityEditor.PrefabUtility.InstantiatePrefab(characterPrefab, visualRoot) as GameObject;
        else
#endif
            spawnedVisual = Instantiate(characterPrefab, visualRoot);

        if (spawnedVisual == null)
            return;

        spawnedVisual.name = characterPrefab.name;
        ConfigureVisualTransform(spawnedVisual.transform);
    }

    public void RebuildFromPrefab()
    {
        EnsureVisualRoot();
        if (visualRoot == null)
            return;

        ClearSpawnedVisual();
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);
            if (child == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        ApplyVisual();
    }

    public void ClearSpawnedVisual()
    {
        if (spawnedVisual == null)
            return;

        if (Application.isPlaying)
            Destroy(spawnedVisual);
        else
            DestroyImmediate(spawnedVisual);

        spawnedVisual = null;
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        Transform existing = transform.Find(VisualRootName);
        if (existing != null)
        {
            visualRoot = existing;
            return;
        }

        GameObject root = new GameObject(VisualRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        visualRoot = root.transform;
    }

    private bool TryUseExistingChildVisual()
    {
        if (visualRoot == null)
            return false;

        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (child == null || child.gameObject == spawnedVisual)
                continue;

            ConfigureVisualTransform(child);
            return true;
        }

        return false;
    }

    private void ConfigureVisualTransform(Transform modelTransform)
    {
        if (modelTransform == null)
            return;

        modelTransform.localPosition = modelLocalPosition;
        Quaternion rotation = Quaternion.Euler(modelLocalEuler);
        if (faceNegativeZ)
            rotation *= Quaternion.Euler(0f, 180f, 0f);
        modelTransform.localRotation = rotation;
        modelTransform.localScale = Vector3.one * Mathf.Max(0.01f, modelScale);
    }

    private void HidePlaceholderIfNeeded()
    {
        if (!hidePlaceholderMesh)
            return;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }
}
