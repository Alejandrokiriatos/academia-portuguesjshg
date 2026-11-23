using Unity.Netcode;
using RustSurvival.Resources;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RustSurvival.Building
{
    /// <summary>
    /// Local green/red preview. The server rechecks distance, collision and resource
    /// costs before spawning a NetworkObject structure.
    /// </summary>
    public sealed class BuildingPlacement : NetworkBehaviour
    {
        [Header("Network prefabs (each must contain NetworkObject and colliders)")]
        [SerializeField] private NetworkObject[] structurePrefabs = new NetworkObject[3];
        [SerializeField] private int[] woodCosts = { 100, 50, 70 };
        [SerializeField] private int[] stoneCosts = { 0, 0, 0 };

        [Header("Placement")]
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(1f)] private float placementReach = 8f;
        [SerializeField] private float gridSize = 2f;
        [SerializeField] private LayerMask placementLayers = ~0;
        [SerializeField] private Material validGhostMaterial;
        [SerializeField] private Material invalidGhostMaterial;

        private InputAction _toggleAction;
        private InputAction _placeAction;
        private InputAction _rotateAction;
        private InputAction _cancelAction;
        private GameObject _ghost;
        private Renderer[] _ghostRenderers;
        private int _selectedIndex;
        private float _yaw;
        private bool _buildMode;
        private bool _localCanPlace;
        private bool _hasGhostState;
        private bool _lastGhostValid;

        private void Awake()
        {
            _toggleAction = new InputAction("BuildMode", InputActionType.Button, "<Keyboard>/v");
            _placeAction = new InputAction("Place", InputActionType.Button, "<Keyboard>/enter");
            _placeAction.AddBinding("<Mouse>/leftButton");
            _rotateAction = new InputAction("Rotate", InputActionType.Button, "<Keyboard>/r");
            _cancelAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
            _placeAction.Enable();
            _rotateAction.Enable();
            _cancelAction.Enable();
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
            _placeAction.Disable();
            _rotateAction.Disable();
            _cancelAction.Disable();
            DestroyGhost();
        }

        private void OnDestroy()
        {
            _toggleAction.Dispose();
            _placeAction.Dispose();
            _rotateAction.Dispose();
            _cancelAction.Dispose();
        }

        private void Update()
        {
            if (IsSpawned && !IsOwner) return;

            if (_toggleAction.WasPressedThisFrame())
                SetBuildMode(!_buildMode);
            if (!_buildMode) return;

            if (_cancelAction.WasPressedThisFrame())
            {
                SetBuildMode(false);
                return;
            }
            if (_rotateAction.WasPressedThisFrame())
                _yaw = (_yaw + 90f) % 360f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectStructure(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectStructure(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectStructure(2);
            }

            UpdateGhost();
            if (_placeAction.WasPressedThisFrame())
                PlaceCurrent();
        }

        public void ToggleBuildMode()
        {
            SetBuildMode(!_buildMode);
        }

        public void SetBuildMode(bool enabled)
        {
            _buildMode = enabled;
            if (_buildMode) CreateGhost();
            else DestroyGhost();
        }

        public void SelectStructure(int index)
        {
            if (structurePrefabs == null || index < 0 || index >= structurePrefabs.Length)
                return;
            _selectedIndex = index;
            if (_buildMode) CreateGhost();
        }

        public void RotateFromMobileButton()
        {
            _yaw = (_yaw + 90f) % 360f;
        }

        public void PlaceCurrent()
        {
            if (!_buildMode || !_localCanPlace || structurePrefabs == null ||
                _selectedIndex >= structurePrefabs.Length || structurePrefabs[_selectedIndex] == null)
                return;

            Vector3 position = _ghost.transform.position;
            if (IsSpawned)
                RequestPlaceServerRpc(_selectedIndex, position, _yaw);
        }

        private void CreateGhost()
        {
            DestroyGhost();
            if (structurePrefabs == null || _selectedIndex >= structurePrefabs.Length ||
                structurePrefabs[_selectedIndex] == null)
                return;

            _ghost = Instantiate(structurePrefabs[_selectedIndex].gameObject);
            _ghost.name = "LOCAL_BUILD_PREVIEW";
            foreach (Collider col in _ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (NetworkObject netObj in _ghost.GetComponentsInChildren<NetworkObject>())
                netObj.enabled = false;

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
            UpdateGhostMaterial(false);
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderers = null;
            _localCanPlace = false;
            _hasGhostState = false;
        }

        private void UpdateGhost()
        {
            if (_ghost == null || playerCamera == null) return;

            Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, placementReach, placementLayers, QueryTriggerInteraction.Ignore))
            {
                _localCanPlace = false;
                UpdateGhostMaterial(false);
                return;
            }

            Vector3 position = hit.point;
            if (gridSize > 0f)
            {
                position.x = Mathf.Round(position.x / gridSize) * gridSize;
                position.z = Mathf.Round(position.z / gridSize) * gridSize;
            }

            _ghost.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _yaw, 0f));
            _localCanPlace = CanPlaceAt(position, _yaw, transform);
            UpdateGhostMaterial(_localCanPlace);
        }

        private bool CanPlaceAt(Vector3 position, float yaw, Transform ignoredRoot)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Collider[] overlaps = Physics.OverlapBox(position + Vector3.up * 0.8f,
                new Vector3(0.92f, 0.78f, 0.92f), rotation, placementLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider overlap in overlaps)
            {
                if (overlap == null || overlap is TerrainCollider) continue;
                if (ignoredRoot != null && overlap.transform.root == ignoredRoot.root) continue;
                if (_ghost != null && overlap.transform.IsChildOf(_ghost.transform)) continue;
                return false;
            }
            return true;
        }

        private void UpdateGhostMaterial(bool valid)
        {
            if (_ghostRenderers == null) return;
            if (_hasGhostState && _lastGhostValid == valid) return;

            Material material = valid ? validGhostMaterial : invalidGhostMaterial;
            if (material == null) return;
            foreach (Renderer renderer in _ghostRenderers)
            {
                if (renderer != null)
                {
                    Material[] materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
            }

            _lastGhostValid = valid;
            _hasGhostState = true;
        }

        [ServerRpc]
        private void RequestPlaceServerRpc(int prefabIndex, Vector3 position, float yaw)
        {
            if (prefabIndex < 0 || structurePrefabs == null || prefabIndex >= structurePrefabs.Length)
                return;
            NetworkObject prefab = structurePrefabs[prefabIndex];
            if (prefab == null || Vector3.Distance(transform.position, position) > placementReach + 1.25f)
                return;
            if (!CanPlaceAt(position, yaw, transform))
                return;

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null) return;

            int wood = prefabIndex < woodCosts.Length ? woodCosts[prefabIndex] : 0;
            int stone = prefabIndex < stoneCosts.Length ? stoneCosts[prefabIndex] : 0;

            if (inventory.GetAmount(ResourceType.Wood) < wood ||
                inventory.GetAmount(ResourceType.Stone) < stone)
                return;

            if (wood > 0 && !inventory.TrySpend(ResourceType.Wood, wood)) return;
            if (stone > 0 && !inventory.TrySpend(ResourceType.Stone, stone))
            {
                if (wood > 0) inventory.TryAdd(ResourceType.Wood, wood);
                return;
            }

            NetworkObject placed = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f));
            placed.Spawn(true);
        }
    }
}
