using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.Resources
{
    /// <summary>Networked harvestable tree/rock. Only the server changes health or grants loot.</summary>
    public sealed class ResourceNode : NetworkBehaviour
    {
        [SerializeField] private ResourceType resource = ResourceType.Wood;
        [SerializeField, Min(1)] private int rewardAmount = 25;
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField] private GameObject visualRoot;

        public readonly NetworkVariable<float> Health =
            new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public ResourceType Resource => resource;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                Health.Value = maximumHealth;

            Health.OnValueChanged += OnHealthChanged;
            ApplyVisualState(Health.Value);
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= OnHealthChanged;
        }

        /// <summary>Server-only entry point called after the interactor validates range.</summary>
        public void ApplyHarvestServer(PlayerInventory inventory, float damage)
        {
            if (!IsServer || inventory == null || !NetworkObject.IsSpawned || damage <= 0f)
                return;

            Health.Value = Mathf.Max(0f, Health.Value - damage);
            if (Health.Value <= 0f)
            {
                inventory.TryAdd(resource, rewardAmount);
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>Local-only harvest path for browser/desktop/mobile solo testing.</summary>
        public void ApplyHarvestOffline(PlayerInventory inventory, float damage)
        {
            if (IsSpawned || inventory == null || damage <= 0f)
                return;

            Health.Value = Mathf.Max(0f, Health.Value - damage);
            if (Health.Value <= 0f)
            {
                inventory.TryAdd(resource, rewardAmount);
                Destroy(gameObject);
            }
        }

        private void OnHealthChanged(float previous, float current)
        {
            ApplyVisualState(current);
        }

        private void ApplyVisualState(float currentHealth)
        {
            if (visualRoot != null)
                visualRoot.SetActive(currentHealth > 0f);
        }
    }
}
