using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using RustSurvival.Player;
using RustSurvival.Resources;
using RustSurvival.World;

namespace RustSurvival.Core
{
    /// <summary>
    /// Boots an entirely local survival session without Unity Services or internet.
    /// This is the ready-to-play entry point for first WebGL/desktop/mobile testing.
    /// </summary>
    public sealed class OfflineModeController : MonoBehaviour
    {
        [Header("Generated prototype prefabs")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;
        [SerializeField] private GameObject foodPrefab;
        [SerializeField] private GameObject waterPrefab;

        [Header("World population")]
        [SerializeField, Range(0, 600)] private int treeCount = 180;
        [SerializeField, Range(0, 300)] private int rockCount = 85;
        [SerializeField, Range(0, 120)] private int foodCount = 35;
        [SerializeField, Range(0, 80)] private int waterCount = 18;
        [SerializeField, Min(2f)] private float minimumResourceSpacing = 7f;
        [SerializeField] private int worldSeed = 8819;

        private bool _hasStarted;
        private readonly List<GameObject> _spawnedResources = new();

        public bool HasStarted => _hasStarted;

        public void StartOfflineGame()
        {
            if (_hasStarted) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                Debug.LogWarning("Cierra la sesión multijugador antes de iniciar el modo local.");
                return;
            }

            WorldGenerator generator = FindObjectOfType<WorldGenerator>();
            if (generator == null)
            {
                Debug.LogError("No hay WorldGenerator en la escena.");
                return;
            }

            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                generator.Generate();
                terrain = FindObjectOfType<Terrain>();
            }

            if (terrain == null)
            {
                Debug.LogError("No se pudo generar el terreno.");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("No está asignado el prefab Player.");
                return;
            }

            Vector3 center = terrain.transform.position +
                new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
            center.y = terrain.SampleHeight(center) + terrain.transform.position.y + 2f;

            GameObject player = Instantiate(playerPrefab, center, Quaternion.identity);
            player.name = "LocalPlayer";
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.InitializeOfflineOwner();

            SpawnWorldResources(terrain, center);
            _hasStarted = true;
            Debug.Log("[Rust Survival] Modo local iniciado. No requiere conexión a internet.");
        }

        private void SpawnWorldResources(Terrain terrain, Vector3 playerPosition)
        {
            Random.State previous = Random.state;
            Random.InitState(worldSeed);

            var occupied = new List<Vector3>(treeCount + rockCount + foodCount + waterCount)
            {
                playerPosition
            };

            SpawnBatch(terrain, treePrefab, treeCount, occupied, 5f, playerPosition);
            SpawnBatch(terrain, rockPrefab, rockCount, occupied, 5f, playerPosition);
            SpawnBatch(terrain, foodPrefab, foodCount, occupied, 1f, playerPosition);
            SpawnBatch(terrain, waterPrefab, waterCount, occupied, 1f, playerPosition);

            Random.state = previous;
        }

        private void SpawnBatch(Terrain terrain, GameObject prefab, int count,
            List<Vector3> occupied, float minHeight, Vector3 playerPosition)
        {
            if (prefab == null || count <= 0) return;

            Vector3 size = terrain.terrainData.size;
            int created = 0;
            int attempts = Mathf.Max(150, count * 20);

            for (int i = 0; i < attempts && created < count; i++)
            {
                float x = Random.Range(8f, size.x - 8f);
                float z = Random.Range(8f, size.z - 8f);
                Vector3 position = terrain.transform.position + new Vector3(x, 0f, z);
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

                if (position.y < minHeight) continue;
                if ((position - playerPosition).sqrMagnitude < 18f * 18f) continue;

                bool crowded = false;
                foreach (Vector3 existing in occupied)
                {
                    if ((existing - position).sqrMagnitude <
                        minimumResourceSpacing * minimumResourceSpacing)
                    {
                        crowded = true;
                        break;
                    }
                }
                if (crowded) continue;

                GameObject resource = Instantiate(prefab, position,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                resource.name = prefab.name + "_Local";
                _spawnedResources.Add(resource);
                occupied.Add(position);
                created++;
            }
        }
    }
}
