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

    /// <summary>
    /// Destroys the persisted player so the next SampleScene load uses a fresh scene instance
    /// (fixes stale body-model refs and duplicate cameras/listeners after menu restart).
    /// </summary>
    public static void DestroyForNewSession()
    {
        if (_Instance == null)
            return;

        GameObject player = _Instance.gameObject;
        _Instance = null;
        Destroy(player);

        GameObject pool = GameObject.Find("CharacterModelPool");
        if (pool != null)
            Destroy(pool);
    }
}
