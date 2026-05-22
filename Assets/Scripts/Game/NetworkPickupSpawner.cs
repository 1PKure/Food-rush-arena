using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NetworkPickupSpawner : NetworkBehaviour
{
    [Header("Pickup Prefabs")]
    [SerializeField] private NetworkObject[] pickupPrefabs;

    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointsRoot;
    [SerializeField] private float randomOffsetRadius = 0f;

    [Header("Spawn Settings")]
    [SerializeField] private int maxActivePickups = 4;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private bool spawnOnlyWhileRaceIsActive = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool drawSpawnGizmos = true;

    [Networked] private TickTimer SpawnTimer { get; set; }

    private readonly List<Vector3> spawnPositions = new();
    private readonly List<int> availableSpawnIndexes = new();
    private readonly Dictionary<ScorePickup, int> occupiedIndexesByPickup = new();

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        CacheSpawnPositions();
        RebuildAvailableIndexes();

        if (showDebugLogs)
        {
            Debug.Log($"[NetworkPickupSpawner] Cached spawn positions: {spawnPositions.Count}");

            for (int i = 0; i < spawnPositions.Count; i++)
            {
                Debug.Log($"[NetworkPickupSpawner] Spawn index {i}: {spawnPositions[i]}");
            }
        }

        SpawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        CleanInvalidPickups();

        if (!CanSpawnPickups())
        {
            return;
        }

        if (!SpawnTimer.Expired(Runner))
        {
            return;
        }

        TrySpawnPickup();

        SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
    }

    public void NotifyPickupDespawned(ScorePickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        if (occupiedIndexesByPickup.TryGetValue(pickup, out int releasedIndex))
        {
            occupiedIndexesByPickup.Remove(pickup);

            if (!availableSpawnIndexes.Contains(releasedIndex))
            {
                availableSpawnIndexes.Add(releasedIndex);
            }

            if (showDebugLogs)
            {
                Debug.Log($"[NetworkPickupSpawner] Released spawn index: {releasedIndex}");
            }
        }
    }

    private void CacheSpawnPositions()
    {
        spawnPositions.Clear();

        if (spawnPointsRoot == null)
        {
            Debug.LogError("[NetworkPickupSpawner] SpawnPointsRoot is null. Assign PickupSpawnPoints in the Inspector.");
            return;
        }

        for (int i = 0; i < spawnPointsRoot.childCount; i++)
        {
            Transform child = spawnPointsRoot.GetChild(i);

            if (child == null)
            {
                continue;
            }

            spawnPositions.Add(child.position);
        }
    }

    private void RebuildAvailableIndexes()
    {
        availableSpawnIndexes.Clear();

        for (int i = 0; i < spawnPositions.Count; i++)
        {
            availableSpawnIndexes.Add(i);
        }

        ShuffleAvailableIndexes();
    }

    private bool CanSpawnPickups()
    {
        if (pickupPrefabs == null || pickupPrefabs.Length == 0)
        {
            return false;
        }

        if (spawnPositions.Count == 0)
        {
            return false;
        }

        if (occupiedIndexesByPickup.Count >= maxActivePickups)
        {
            return false;
        }

        if (availableSpawnIndexes.Count == 0)
        {
            return false;
        }

        if (!spawnOnlyWhileRaceIsActive)
        {
            return true;
        }

        if (NetworkRaceManager.Instance == null)
        {
            return true;
        }

        return NetworkRaceManager.Instance.CanPlayersMove;
    }

    private void TrySpawnPickup()
    {
        NetworkObject selectedPrefab = GetRandomPickupPrefab();

        if (selectedPrefab == null)
        {
            Debug.LogWarning("[NetworkPickupSpawner] No valid pickup prefab found.");
            return;
        }

        int spawnIndex = GetNextAvailableSpawnIndex();

        if (spawnIndex < 0 || spawnIndex >= spawnPositions.Count)
        {
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(spawnIndex);
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[NetworkPickupSpawner] Spawning {selectedPrefab.name} | " +
                $"Index: {spawnIndex} | " +
                $"Position: {spawnPosition}"
            );
        }

        NetworkObject spawnedObject = Runner.Spawn(
            selectedPrefab,
            spawnPosition,
            spawnRotation
        );

        if (spawnedObject == null)
        {
            ReleaseIndex(spawnIndex);
            return;
        }

        spawnedObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        ScorePickup pickup = spawnedObject.GetComponent<ScorePickup>();

        if (pickup == null)
        {
            Debug.LogWarning($"[NetworkPickupSpawner] Spawned object {spawnedObject.name} has no ScorePickup.");
            Runner.Despawn(spawnedObject);
            ReleaseIndex(spawnIndex);
            return;
        }

        pickup.Initialize(this);
        occupiedIndexesByPickup[pickup] = spawnIndex;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[NetworkPickupSpawner] Spawned object final transform: {spawnedObject.transform.position}"
            );
        }
    }

    private NetworkObject GetRandomPickupPrefab()
    {
        List<NetworkObject> validPrefabs = new();

        foreach (NetworkObject prefab in pickupPrefabs)
        {
            if (prefab != null)
            {
                validPrefabs.Add(prefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    private int GetNextAvailableSpawnIndex()
    {
        if (availableSpawnIndexes.Count == 0)
        {
            return -1;
        }

        int selectedIndex = availableSpawnIndexes[0];
        availableSpawnIndexes.RemoveAt(0);

        return selectedIndex;
    }

    private Vector3 GetSpawnPosition(int spawnIndex)
    {
        Vector3 position = spawnPositions[spawnIndex];

        if (randomOffsetRadius <= 0f)
        {
            return position;
        }

        Vector2 randomCircle = Random.insideUnitCircle * randomOffsetRadius;
        position.x += randomCircle.x;
        position.z += randomCircle.y;

        return position;
    }

    private void ReleaseIndex(int index)
    {
        if (!availableSpawnIndexes.Contains(index))
        {
            availableSpawnIndexes.Add(index);
            ShuffleAvailableIndexes();
        }
    }

    private void ShuffleAvailableIndexes()
    {
        for (int i = 0; i < availableSpawnIndexes.Count; i++)
        {
            int randomIndex = Random.Range(i, availableSpawnIndexes.Count);
            (availableSpawnIndexes[i], availableSpawnIndexes[randomIndex]) =
                (availableSpawnIndexes[randomIndex], availableSpawnIndexes[i]);
        }
    }

    private void CleanInvalidPickups()
    {
        List<ScorePickup> invalidPickups = null;

        foreach (KeyValuePair<ScorePickup, int> pair in occupiedIndexesByPickup)
        {
            if (pair.Key == null)
            {
                invalidPickups ??= new List<ScorePickup>();
                invalidPickups.Add(pair.Key);
            }
        }

        if (invalidPickups == null)
        {
            return;
        }

        foreach (ScorePickup pickup in invalidPickups)
        {
            occupiedIndexesByPickup.Remove(pickup);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawSpawnGizmos || spawnPointsRoot == null)
        {
            return;
        }

        for (int i = 0; i < spawnPointsRoot.childCount; i++)
        {
            Transform child = spawnPointsRoot.GetChild(i);

            if (child == null)
            {
                continue;
            }

            Gizmos.DrawSphere(child.position, 0.25f);

            if (randomOffsetRadius > 0f)
            {
                Gizmos.DrawWireSphere(child.position, randomOffsetRadius);
            }
        }
    }
}