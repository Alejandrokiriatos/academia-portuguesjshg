using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.World
{
    /// <summary>
    /// Spawns replicated resources when an NGO host/server starts.
    /// Offline play uses OfflineModeController and never starts this network spawner.
    /// </summary>
    public sealed class WorldResourceSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject treePrefab;
        [SerializeField] private NetworkObject rockPrefab;
        [SerializeField] private NetworkObject foodPrefab;
        [SerializeField] private NetworkObject waterPrefab;

        [SerializeField, Range(0, 600)] private int treeCount = 180;
        [SerializeField, Range(0, 300)] private int rockCount = 85;
        [SerializeField, Range(0, 120)] private int foodCacheCount = 35;
        [SerializeField, Range(0, 80)] private int waterCacheCount = 18;
        [SerializeField, Min(2f)] private float minimumSpacing = 7f;
        [SerializeField] private int spawnSeed = 8819;

        private NetworkManager _networkManager;
        private bool _hasSpawned;

        private void Start()
        {
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null) return;

            _networkManager.OnServerStarted += HandleServerStarted;
            if (_networkManager.IsServer)
                HandleServerStarted();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.OnServerStarted -= HandleServerStarted;
        }

        private void HandleServerStarted()
        {
            if (_hasSpawned) return;
            _hasSpawned = true;
            StartCoroutine(SpawnAfterTerrainGeneration());
        }

        private IEnumerator SpawnAfterTerrainGeneration()
        {
            Terrain terrain = null;
            float timeout = 30f;
            while (terrain == null && timeout > 0f)
            {
                terrain = FindObjectOfType<Terrain>();
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (terrain == null)
            {
                Debug.LogError("WorldResourceSpawner: no se encontró el Terrain.");
                yield break;
            }

            if (_networkManager == null || !_networkManager.IsServer)
                yield break;

            Random.State previous = Random.state;
            Random.InitState(spawnSeed);
            var occupied = new List<Vector3>(treeCount + rockCount + foodCacheCount + waterCacheCount);

            SpawnBatch(terrain, treePrefab, treeCount, occupied, 0.55f);
            SpawnBatch(terrain, rockPrefab, rockCount, occupied, 0.9f);
            SpawnBatch(terrain, foodPrefab, foodCacheCount, occupied, 0.55f);
            SpawnBatch(terrain, waterPrefab, waterCacheCount, occupied, 0.55f);

            Random.state = previous;
        }

        private void SpawnBatch(Terrain terrain, NetworkObject prefab, int count,
            List<Vector3> occupied, float minHeight)
        {
            if (prefab == null || count <= 0)
            {
                if (prefab == null)
                    Debug.LogWarning("WorldResourceSpawner: falta asignar un prefab de recursos.");
                return;
            }

            Vector3 size = terrain.terrainData.size;
            int created = 0;
            int attempts = Mathf.Max(100, count * 15);

            for (int i = 0; i < attempts && created < count; i++)
            {
                float x = Random.Range(8f, size.x - 8f);
                float z = Random.Range(8f, size.z - 8f);
                Vector3 position = terrain.transform.position + new Vector3(x, 0f, z);
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

                if (position.y < minHeight) continue;

                bool crowded = false;
                for (int j = 0; j < occupied.Count; j++)
                {
                    if ((occupied[j] - position).sqrMagnitude < minimumSpacing * minimumSpacing)
                    {
                        crowded = true;
                        break;
                    }
                }
                if (crowded) continue;

                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                NetworkObject instance = Instantiate(prefab, position, rotation);
                instance.Spawn(true);
                occupied.Add(position);
                created++;
            }

            if (created < count)
                Debug.LogWarning($"WorldResourceSpawner: se colocaron {created}/{count} nodos.");
        }
    }
}
