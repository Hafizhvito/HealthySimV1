using UnityEngine;

public class PlayerPersist : MonoBehaviour
{
    private static PlayerPersist _Instance;

    private void Awake()
    {
        if (_Instance != null && _Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
