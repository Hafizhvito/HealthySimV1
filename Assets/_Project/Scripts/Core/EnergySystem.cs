using UnityEngine;

public class EnergySystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;

    [Header("Faint Settings")]
    [SerializeField] private float faintRecoveryEnergy = 30f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Transform respawnPoint;

    private PlayerStats stats;
    private Rigidbody rb;
    private bool hasFainted = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
        if (playerController != null && rb == null)
            rb = playerController.GetComponent<Rigidbody>();

        if (respawnPoint == null)
        {
            GameObject sp = GameObject.Find("SpawnPoint");
            if (sp != null)
            {
                respawnPoint = sp.transform;
            }
            else
            {
                GameObject newSpawn = new GameObject("SpawnPoint");
                newSpawn.transform.position = new Vector3(0f, 0f, 2f);
                respawnPoint = newSpawn.transform;
            }
        }
    }

    void Start()
    {
        stats = PlayerStats.Instance;
        if (stats != null)
        {
            stats.OnEnergyStateChanged += HandleEnergyStateChange;
            stats.OnPlayerFainted += HandleFaint;
        }
    }

    void HandleEnergyStateChange(PlayerStats.EnergyState state)
    {
        // Speed multiplier is handled inside PlayerController
        // via PlayerStats.GetSpeedMultiplier()
        Debug.Log("Energy state changed to: " + state.ToString());
    }

    void HandleFaint()
    {
        if (hasFainted) return;
        hasFainted = true;
        Debug.Log("Player fainted!");

        // Disable player input temporarily
        if (playerController != null)
            playerController.LockMovement("EnergySystem_Fainted");

        // Respawn after delay
        Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        hasFainted = false;

        // Move to respawn point if set, otherwise stay
        if (respawnPoint != null)
        {
            if (playerController != null)
                playerController.transform.position = respawnPoint.position;
            else if (transform.parent != null)
                transform.parent.position = respawnPoint.position;
            else
                transform.position = respawnPoint.position;
        }

        // Restore some energy
        if (stats != null)
            stats.AddFood(faintRecoveryEnergy, 0f, -10f);

        // Re-enable player input
        if (playerController != null)
            playerController.UnlockMovement("EnergySystem_Fainted");

        Debug.Log("Player respawned with " + faintRecoveryEnergy + " energy.");
    }

    void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnEnergyStateChanged -= HandleEnergyStateChange;
            stats.OnPlayerFainted -= HandleFaint;
        }
    }
}
