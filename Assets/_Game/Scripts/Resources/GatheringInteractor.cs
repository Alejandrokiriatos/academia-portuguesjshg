using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RustSurvival.Resources
{
    /// <summary>Owner raycasts for interact intent; server validates node identity and range.</summary>
    public sealed class GatheringInteractor : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(1f)] private float reach = 4.25f;
        [SerializeField, Min(1f)] private float harvestDamage = 25f;
        [SerializeField] private LayerMask resourceLayers = ~0;

        private InputAction _harvestAction;

        private void Awake()
        {
            _harvestAction = new InputAction("Harvest", InputActionType.Button);
            _harvestAction.AddBinding("<Mouse>/leftButton");
            _harvestAction.AddBinding("<Keyboard>/e");
            _harvestAction.AddBinding("<Gamepad>/rightTrigger");
        }

        private void OnEnable() => _harvestAction.Enable();
        private void OnDisable() => _harvestAction.Disable();
        private void OnDestroy() => _harvestAction.Dispose();

        private void Update()
        {
            if (IsSpawned && !IsOwner) return;
            if (_harvestAction.WasPressedThisFrame())
                TryHarvest();
        }

        public void TryHarvest()
        {
            if (playerCamera == null) return;

            Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, resourceLayers, QueryTriggerInteraction.Ignore))
                return;

            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node == null) return;

            if (IsSpawned)
            {
                if (node.NetworkObject != null && node.NetworkObject.IsSpawned)
                    RequestHarvestServerRpc(node.NetworkObjectId);
            }
            else
            {
                PlayerInventory inventory = GetComponent<PlayerInventory>();
                node.ApplyHarvestOffline(inventory, harvestDamage);
            }
        }

        [ServerRpc]
        private void RequestHarvestServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject target))
                return;

            ResourceNode node = target.GetComponent<ResourceNode>();
            ulong sender = rpcParams.Receive.SenderClientId;
            if (node == null || !NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client))
                return;

            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null ||
                Vector3.Distance(playerObject.transform.position, target.transform.position) > reach + 0.75f)
                return;

            // Server checks line of sight as well as distance; clients cannot harvest
            // a node through walls or by sending arbitrary nearby object IDs.
            Vector3 eye = playerObject.transform.position + Vector3.up * 1.5f;
            Vector3 toTarget = target.transform.position + Vector3.up * 0.5f - eye;
            float targetDistance = toTarget.magnitude;
            if (targetDistance > reach + 0.75f || targetDistance <= 0.01f)
                return;
            if (!Physics.Raycast(eye, toTarget / targetDistance, out RaycastHit serverHit,
                    targetDistance + 0.25f, ~0, QueryTriggerInteraction.Ignore))
                return;
            if (serverHit.collider.GetComponentInParent<ResourceNode>() != node)
                return;

            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            node.ApplyHarvestServer(inventory, harvestDamage);
        }
    }
}
