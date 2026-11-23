using Unity.Netcode;

namespace RustSurvival.Resources
{
    /// <summary>Server-write inventory replicated to all clients for HUD display.</summary>
    public sealed class PlayerInventory : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Wood = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Stone = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Food = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Water = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int GetAmount(ResourceType type)
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

        public bool TryAdd(ResourceType type, int amount)
        {
            if ((IsSpawned && !IsServer) || amount <= 0) return false;
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

        public bool TrySpend(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0 || GetAmount(type) < amount) return false;
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
    }
}
