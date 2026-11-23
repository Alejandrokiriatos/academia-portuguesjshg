using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.Survival
{
    /// <summary>Server-ticked survival values. Clients can request food/water use.</summary>
    public sealed class SurvivalStats : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(1f)] private float maximumHunger = 100f;
        [SerializeField, Min(1f)] private float maximumThirst = 100f;
        [SerializeField, Min(0f)] private float hungerDrainPerSecond = 0.10f;
        [SerializeField, Min(0f)] private float thirstDrainPerSecond = 0.16f;
        [SerializeField, Min(0f)] private float starvationDamagePerSecond = 2f;
        [SerializeField, Min(0f)] private float dehydrationDamagePerSecond = 3f;

        public readonly NetworkVariable<float> Health = new(100f);
        public readonly NetworkVariable<float> Hunger = new(100f);
        public readonly NetworkVariable<float> Thirst = new(100f);

        private float _tick;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Health.Value = maximumHealth;
                Hunger.Value = maximumHunger;
                Thirst.Value = maximumThirst;
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            _tick += Time.deltaTime;
            if (_tick < 1f) return;

            float elapsed = _tick;
            _tick = 0f;

            Hunger.Value = Mathf.Max(0f, Hunger.Value - hungerDrainPerSecond * elapsed);
            Thirst.Value = Mathf.Max(0f, Thirst.Value - thirstDrainPerSecond * elapsed);

            float damage = 0f;
            if (Hunger.Value <= 0f) damage += starvationDamagePerSecond;
            if (Thirst.Value <= 0f) damage += dehydrationDamagePerSecond;
            if (damage > 0f)
                Health.Value = Mathf.Max(0f, Health.Value - damage * elapsed);
        }

        [ServerRpc]
        public void ConsumeFoodServerRpc()
        {
            if (Hunger.Value >= maximumHunger) return;
            Resources.PlayerInventory inventory = GetComponent<Resources.PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(Resources.ResourceType.Food, 1)) return;
            Hunger.Value = Mathf.Min(maximumHunger, Hunger.Value + 35f);
        }

        [ServerRpc]
        public void ConsumeWaterServerRpc()
        {
            if (Thirst.Value >= maximumThirst) return;
            Resources.PlayerInventory inventory = GetComponent<Resources.PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(Resources.ResourceType.Water, 1)) return;
            Thirst.Value = Mathf.Min(maximumThirst, Thirst.Value + 40f);
        }

        [ServerRpc]
        public void HealServerRpc(float amount)
        {
            // Kept for future medkits. Do not expose arbitrary healing to clients:
            // this endpoint is intentionally disabled until item validation is added.
        }
    }
}
