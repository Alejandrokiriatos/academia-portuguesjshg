using UnityEngine;
using UnityEngine.EventSystems;

namespace RustSurvival.Player
{
    /// <summary>Attach to UI buttons for Jump (tap), Sprint (hold) or Crouch (hold).</summary>
    public sealed class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum ActionType { Jump, Sprint, Crouch }

        [SerializeField] private PlayerMovement target;
        [SerializeField] private ActionType action = ActionType.Jump;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target == null) target = PlayerMovement.LocalPlayer;
            if (target != null) target.SetMobileButton(action, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (target == null) target = FindObjectOfType<PlayerMovement>();
            if (target != null) target.SetMobileButton(action, false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (target == null) target = FindObjectOfType<PlayerMovement>();
            if (target != null && action != ActionType.Jump)
                target.SetMobileButton(action, false);
        }
    }
}
