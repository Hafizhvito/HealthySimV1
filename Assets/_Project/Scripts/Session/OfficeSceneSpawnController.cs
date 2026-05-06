using UnityEngine;
using UnityEngine.SceneManagement;

public class OfficeSceneSpawnController : MonoBehaviour
{
    private const string OfficeSceneName = "OfficeScene";
    private const string SpawnName = "PlayerSpawnPoint";
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOfficeSpawn()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name != OfficeSceneName)
            return;

        SpawnOrMovePlayerToSpawnPoint();
    }

    private static void SpawnOrMovePlayerToSpawnPoint()
    {
        GameObject spawn = GameObject.Find(SpawnName);
        if (spawn == null)
        {
            Debug.LogWarning($"{FallbackLogPrefix} Missing {SpawnName} in OfficeScene. Player spawn contract is incomplete.");
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            Debug.LogWarning($"{FallbackLogPrefix} Created placeholder Player in OfficeScene.");

            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (player.GetComponent<UniversalInteractionController>() == null)
            {
                player.AddComponent<UniversalInteractionController>();
                Debug.LogWarning($"{FallbackLogPrefix} Added UniversalInteractionController to placeholder Player in OfficeScene.");
            }
        }

        player.transform.position = spawn.transform.position;
        player.transform.rotation = spawn.transform.rotation;
    }
}
