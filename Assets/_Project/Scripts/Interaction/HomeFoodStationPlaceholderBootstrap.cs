using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeFoodStationPlaceholderBootstrap
{
    private const string PlaceholderName = "HomeFoodStation_Placeholder";
    private const string SupportedScene = "SampleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlaceholderExists()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != SupportedScene)
            return;

        if (Object.FindFirstObjectByType<HomeFoodStationInteractable>() != null)
            return;

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = PlaceholderName;

        Transform player = FindPlayerTransform();
        if (player != null)
        {
            Vector3 offset = player.forward * 2.0f + player.right * 1.1f;
            Vector3 spawnPos = player.position + offset;
            spawnPos.y = Mathf.Max(0.6f, player.position.y + 0.6f);
            placeholder.transform.position = spawnPos;
        }
        else
        {
            placeholder.transform.position = new Vector3(-55f, 0.6f, 30f);
        }

        placeholder.transform.localScale = new Vector3(1.1f, 1.2f, 0.8f);

        Renderer renderer = placeholder.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = new Color(0.23f, 0.66f, 0.86f, 1f);

        if (placeholder.GetComponent<HomeFoodStationInteractable>() == null)
            placeholder.AddComponent<HomeFoodStationInteractable>();

        Debug.Log("[HomeFood] Placeholder station dibuat otomatis untuk SampleScene.");
    }

    private static Transform FindPlayerTransform()
    {
        GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
        if (playerByTag != null)
            return playerByTag.transform;

        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            return playerController.transform;

        return null;
    }
}
