using UnityEngine;
using UnityEngine.SceneManagement;

public class OfficeSceneSpawnController : MonoBehaviour
{
    private const string OfficeSceneName = "OfficeScene";
    private const string SpawnName = "PlayerSpawnPoint";

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
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";

            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (player.GetComponent<UniversalInteractionController>() == null)
                player.AddComponent<UniversalInteractionController>();
        }

        player.transform.position = spawn.transform.position;
        player.transform.rotation = spawn.transform.rotation;
    }
}
