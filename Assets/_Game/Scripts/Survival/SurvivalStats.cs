using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using RustSurvival.Resources;

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
        private InputAction _eatAction;
        private InputAction _drinkAction;

        private void Awake()
        {
            _eatAction = new InputAction("Eat", InputActionType.Button, "<Keyboard>/h");
            _eatAction.AddBinding("<Gamepad>/buttonWest");
            _drinkAction = new InputAction("Drink", InputActionType.Button, "<Keyboard>/j");
            _drinkAction.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            _eatAction?.Enable();
            _drinkAction?.Enable();
        }

        private void OnDisable()
        {
            _eatAction?.Disable();
            _drinkAction?.Disable();
        }

        private void OnDestroy()
        {
            _eatAction?.Dispose();
            _drinkAction?.Dispose();
        }

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
            if (!IsSpawned || IsOwner)
            {
                if (_eatAction.WasPressedThisFrame()) TryConsumeFood();
                if (_drinkAction.WasPressedThisFrame()) TryConsumeWater();
            }

            if (IsSpawned && !IsServer) return;

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

        public void TryConsumeFood()
        {
            if (IsSpawned && !IsServer)
            {
                ConsumeFoodServerRpc();
                return;
            }
            ConsumeFoodAuthoritative();
        }

        public void TryConsumeWater()
        {
            if (IsSpawned && !IsServer)
            {
                ConsumeWaterServerRpc();
                return;
            }
            ConsumeWaterAuthoritative();
        }

        [ServerRpc]
        private void ConsumeFoodServerRpc()
        {
            ConsumeFoodAuthoritative();
        }

        [ServerRpc]
        private void ConsumeWaterServerRpc()
        {
            ConsumeWaterAuthoritative();
        }

        private void ConsumeFoodAuthoritative()
        {
            if (Hunger.Value >= maximumHunger) return;
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(ResourceType.Food, 1)) return;
            Hunger.Value = Mathf.Min(maximumHunger, Hunger.Value + 35f);
        }

        private void ConsumeWaterAuthoritative()
        {
            if (Thirst.Value >= maximumThirst) return;
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(ResourceType.Water, 1)) return;
            Thirst.Value = Mathf.Min(maximumThirst, Thirst.Value + 40f);
        }
    }
}
