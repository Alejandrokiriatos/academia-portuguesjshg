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
            if (node == null || !node.NetworkObject.IsSpawned) return;

            if (IsSpawned)
                RequestHarvestServerRpc(node.NetworkObjectId);
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
            if (playerObject == null || Vector3.Distance(playerObject.transform.position, target.transform.position) > reach + 0.75f)
                return;

            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            node.ApplyHarvestServer(inventory, harvestDamage);
        }
    }
}
