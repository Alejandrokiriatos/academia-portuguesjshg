using Unity.Netcode;

namespace RustSurvival.Resources
{
    /// <summary>
    /// Server-owned replicated inventory in network sessions, local fields in offline mode.
    /// Never writes a NetworkVariable before its NetworkObject is spawned.
    /// </summary>
    public sealed class PlayerInventory : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Wood = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Stone = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Food = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Water = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private int _offlineWood;
        private int _offlineStone;
        private int _offlineFood;
        private int _offlineWater;

        public int GetAmount(ResourceType type)
        {
            if (IsSpawned)
            {
                return type switch
                {
                    ResourceType.Wood => Wood.Value,
                    ResourceType.Stone => Stone.Value,
                    ResourceType.Food => Food.Value,
                    ResourceType.Water => Water.Value,
                    _ => 0
                };
            }

            return type switch
            {
                ResourceType.Wood => _offlineWood,
                ResourceType.Stone => _offlineStone,
                ResourceType.Food => _offlineFood,
                ResourceType.Water => _offlineWater,
                _ => 0
            };
        }

        public bool TryAdd(ResourceType type, int amount)
        {
            if (amount <= 0) return false;

            if (IsSpawned)
            {
                if (!IsServer) return false;
                switch (type)
                {
                    case ResourceType.Wood: Wood.Value += amount; break;
                    case ResourceType.Stone: Stone.Value += amount; break;
                    case ResourceType.Food: Food.Value += amount; break;
                    case ResourceType.Water: Water.Value += amount; break;
                    default: return false;
                }
                return true;
            }

            switch (type)
            {
                case ResourceType.Wood: _offlineWood += amount; break;
                case ResourceType.Stone: _offlineStone += amount; break;
                case ResourceType.Food: _offlineFood += amount; break;
                case ResourceType.Water: _offlineWater += amount; break;
                default: return false;
            }
            return true;
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (amount <= 0 || GetAmount(type) < amount) return false;

            if (IsSpawned)
            {
                if (!IsServer) return false;
                switch (type)
                {
                    case ResourceType.Wood: Wood.Value -= amount; break;
                    case ResourceType.Stone: Stone.Value -= amount; break;
                    case ResourceType.Food: Food.Value -= amount; break;
                    case ResourceType.Water: Water.Value -= amount; break;
                    default: return false;
                }
                return true;
            }

            switch (type)
            {
                case ResourceType.Wood: _offlineWood -= amount; break;
                case ResourceType.Stone: _offlineStone -= amount; break;
                case ResourceType.Food: _offlineFood -= amount; break;
                case ResourceType.Water: _offlineWater -= amount; break;
                default: return false;
            }
            return true;
        }
    }
}
