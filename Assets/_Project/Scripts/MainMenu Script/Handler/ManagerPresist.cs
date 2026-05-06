using UnityEngine;

public class ManagerPresist : MonoBehaviour
{
    private void Awake()
    {
        if (transform.parent != null)
        {
            Debug.LogWarning("Manager harus jadi root GameObject!");
            return;
        }

        DontDestroyOnLoad(gameObject);
    }
}
