using RustSurvival.Resources;
using RustSurvival.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace RustSurvival.UI
{
    /// <summary>Optional HUD binder. Assign sliders and labels in the player HUD prefab.</summary>
    public sealed class SurvivalHud : MonoBehaviour
    {
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider hungerBar;
        [SerializeField] private Slider thirstBar;
        [SerializeField] private Text woodText;
        [SerializeField] private Text stoneText;

        private void Update()
        {
            if (stats != null)
            {
                SetBar(healthBar, stats.Health.Value);
                SetBar(hungerBar, stats.Hunger.Value);
                SetBar(thirstBar, stats.Thirst.Value);
            }

            if (inventory != null)
            {
                if (woodText != null) woodText.text = inventory.Wood.Value.ToString();
                if (stoneText != null) stoneText.text = inventory.Stone.Value.ToString();
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
