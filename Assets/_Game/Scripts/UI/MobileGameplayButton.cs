using UnityEngine;
using UnityEngine.EventSystems;
using RustSurvival.Building;
using RustSurvival.Player;
using RustSurvival.Resources;
using RustSurvival.Survival;

namespace RustSurvival.UI
{
    /// <summary>
    /// Touch-only action shortcuts. Attach to a UI image/button and choose an action
    /// in the Inspector. Every request is processed by the local player component,
    /// which still relies on server-side validation.
    /// </summary>
    public sealed class MobileGameplayButton : MonoBehaviour, IPointerDownHandler
    {
        public enum ActionType
        {
            Harvest,
            Eat,
            Drink,
            ToggleBuild,
            PlaceBuilding,
            RotateBuilding,
            NextBuilding
        }

        [SerializeField] private ActionType action;

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayerMovement player = PlayerMovement.LocalPlayer;
            if (player == null) return;

            switch (action)
            {
                case ActionType.Harvest:
                    player.GetComponent<GatheringInteractor>()?.TryHarvest();
                    break;
                case ActionType.Eat:
                    player.GetComponent<SurvivalStats>()?.TryConsumeFood();
                    break;
                case ActionType.Drink:
                    player.GetComponent<SurvivalStats>()?.TryConsumeWater();
                    break;
                case ActionType.ToggleBuild:
                    BuildingPlacement building = player.GetComponent<BuildingPlacement>();
                    if (building != null) building.ToggleBuildMode();
                    break;
                case ActionType.PlaceBuilding:
                    player.GetComponent<BuildingPlacement>()?.PlaceCurrent();
                    break;
                case ActionType.RotateBuilding:
                    player.GetComponent<BuildingPlacement>()?.RotateFromMobileButton();
                    break;
                case ActionType.NextBuilding:
                    player.GetComponent<BuildingPlacement>()?.SelectNextStructure();
                    break;
            }
        }
    }
}
