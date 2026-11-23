using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.Networking
{
    /// <summary>Places newly approved player objects near the center of the generated map.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSpawnApproval : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Vector3 fallbackSpawn = new(250f, 8f, 250f);

        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("NetworkSpawnApproval necesita un NetworkManager.");
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            Vector3 spawn = fallbackSpawn;
            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                Vector3 center = terrain.transform.position +
                    new Vector3(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
                spawn = new Vector3(center.x,
                    terrain.SampleHeight(center) + terrain.transform.position.y + 2f,
                    center.z);
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = spawn;
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }
    }
}
