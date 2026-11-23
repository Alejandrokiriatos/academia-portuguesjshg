using RustSurvival.Player;
using RustSurvival.Resources;
using RustSurvival.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace RustSurvival.UI
{
    /// <summary>HUD binder for the locally owned player's replicated survival/inventory values.</summary>
    public sealed class SurvivalHud : MonoBehaviour
    {
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider thirstBar;
        [SerializeField] private Text woodText;
        [SerializeField] private Text stoneText;
        [SerializeField] private Text foodText;
        [SerializeField] private Text waterText;

        private void Update()
        {
            PlayerMovement localPlayer = PlayerMovement.LocalPlayer;
            if (localPlayer != null)
            {
                if (stats == null) stats = localPlayer.GetComponent<SurvivalStats>();
                if (inventory == null) inventory = localPlayer.GetComponent<PlayerInventory>();
            }

            if (stats != null)
            {
                SetBar(healthBar, stats.Health.Value);
                SetBar(hungerBar, stats.Hunger.Value);
                SetBar(thirstBar, stats.Thirst.Value);
            }

            if (inventory != null)
            {
                if (woodText != null) woodText.text = "Madera: " + inventory.Wood.Value;
                if (stoneText != null) stoneText.text = "Piedra: " + inventory.Stone.Value;
                if (foodText != null) foodText.text = "Comida: " + inventory.Food.Value;
                if (waterText != null) waterText.text = "Agua: " + inventory.Water.Value;
            }
        }

        private static void SetBar(Slider slider, float value)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = value;
        }
    }
}
