using System.Collections;
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
    private bool hasFainted;
    private Coroutine respawnRoutine;

    public const string FaintLockKey = "EnergySystem_Fainted";

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
        if (hasFainted)
            return;

        hasFainted = true;
        Debug.Log("Player fainted!");

        if (playerController != null)
            playerController.LockMovement(FaintLockKey);

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(respawnDelay);
        respawnRoutine = null;
        Respawn();
    }

    /// <summary>
    /// Cancels pending auto-respawn and releases the faint movement lock.
    /// Called when the faint panel is dismissed or faint state is reset.
    /// </summary>
    public void CompleteFaintRecovery()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        hasFainted = false;
        ReleaseMovementLock();
    }

    void Respawn()
    {
        hasFainted = false;

        if (respawnPoint != null)
        {
            if (playerController != null)
                playerController.transform.position = respawnPoint.position;
            else if (transform.parent != null)
                transform.parent.position = respawnPoint.position;
            else
                transform.position = respawnPoint.position;
        }

        if (stats != null)
            stats.AddFood(faintRecoveryEnergy, 0f, -10f, 0f, 0f);

        ReleaseMovementLock();
        Debug.Log("Player respawned with " + faintRecoveryEnergy + " energy.");
    }

    private void ReleaseMovementLock()
    {
        if (playerController != null)
            playerController.ForceUnlockInput(FaintLockKey);
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
