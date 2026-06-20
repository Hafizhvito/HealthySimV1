using UnityEngine;

/// <summary>
/// Hides mesh renderers while keeping colliders active for proximity interaction.
/// Attach to food counters or other invisible interaction zones.
/// </summary>
[DisallowMultipleComponent]
public class InvisibleInteractableVisual : MonoBehaviour
{
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool hideOnAwake = true;

    void Awake()
    {
        if (hideOnAwake)
            Apply();
    }

    public void Apply()
    {
        if (includeChildren)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
            return;
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }
}
