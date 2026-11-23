using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.World
{
    /// <summary>Spawns replicated harvestable tree and rock nodes on the server only.</summary>
    public sealed class WorldResourceSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject treePrefab;
        [SerializeField] private NetworkObject rockPrefab;
        [SerializeField, Range(0, 600)] private int treeCount = 180;
        [SerializeField, Range(0, 300)] private int rockCount = 85;
        [SerializeField, Min(2f)] private float minimumSpacing = 7f;
        [SerializeField] private int spawnSeed = 8819;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                StartCoroutine(SpawnAfterTerrainGeneration());
        }

        private IEnumerator SpawnAfterTerrainGeneration()
        {
            Terrain terrain = null;
            float timeout = 10f;
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

            Random.State previous = Random.state;
            Random.InitState(spawnSeed);
            var occupied = new System.Collections.Generic.List<Vector3>(treeCount + rockCount);

            SpawnBatch(terrain, treePrefab, treeCount, occupied, 0.55f);
            SpawnBatch(terrain, rockPrefab, rockCount, occupied, 0.9f);

            Random.state = previous;
        }

        private void SpawnBatch(Terrain terrain, NetworkObject prefab, int count,
            System.Collections.Generic.List<Vector3> occupied, float minHeight)
        {
            if (prefab == null)
            {
                Debug.LogWarning("WorldResourceSpawner: falta asignar uno de los prefabs de recursos.");
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
