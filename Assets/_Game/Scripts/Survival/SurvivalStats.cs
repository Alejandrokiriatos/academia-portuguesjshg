using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using RustSurvival.Resources;

namespace RustSurvival.Survival
{
    /// <summary>
    /// Server-authoritative replicated stats in multiplayer and local-only fields
    /// in offline mode. This avoids writing NGO NetworkVariables before spawn.
    /// </summary>
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

        private float _offlineHealth;
        private float _offlineHunger;
        private float _offlineThirst;
        private float _tick;
        private InputAction _eatAction;
        private InputAction _drinkAction;

        private void Awake()
        {
            _offlineHealth = maximumHealth;
            _offlineHunger = maximumHunger;
            _offlineThirst = maximumThirst;

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

        public float GetHealth() => IsSpawned ? Health.Value : _offlineHealth;
        public float GetHunger() => IsSpawned ? Hunger.Value : _offlineHunger;
        public float GetThirst() => IsSpawned ? Thirst.Value : _offlineThirst;

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

            float hunger = Mathf.Max(0f, GetHunger() - hungerDrainPerSecond * elapsed);
            float thirst = Mathf.Max(0f, GetThirst() - thirstDrainPerSecond * elapsed);
            float damage = 0f;

            if (hunger <= 0f) damage += starvationDamagePerSecond;
            if (thirst <= 0f) damage += dehydrationDamagePerSecond;

            float health = Mathf.Max(0f, GetHealth() - damage * elapsed);

            if (IsSpawned)
            {
                Hunger.Value = hunger;
                Thirst.Value = thirst;
                Health.Value = health;
            }
            else
            {
                _offlineHunger = hunger;
                _offlineThirst = thirst;
                _offlineHealth = health;
            }
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
            if (GetHunger() >= maximumHunger) return;

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(ResourceType.Food, 1)) return;

            float newHunger = Mathf.Min(maximumHunger, GetHunger() + 35f);
            if (IsSpawned) Hunger.Value = newHunger;
            else _offlineHunger = newHunger;
        }

        private void ConsumeWaterAuthoritative()
        {
            if (GetThirst() >= maximumThirst) return;

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TrySpend(ResourceType.Water, 1)) return;

            float newThirst = Mathf.Min(maximumThirst, GetThirst() + 40f);
            if (IsSpawned) Thirst.Value = newThirst;
            else _offlineThirst = newThirst;
        }
    }
}
