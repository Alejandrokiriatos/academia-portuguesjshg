using UnityEngine;
using UnityEngine.EventSystems;

namespace RustSurvival.Player
{
    /// <summary>Place over the right half of the mobile HUD. Disable Raycast Target on child art.</summary>
    public sealed class MobileLookArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private Vector2 _delta;
        private bool _tracking;

        public void OnPointerDown(PointerEventData eventData)
        {
            _tracking = true;
            _delta = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_tracking)
                _delta += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _tracking = false;
        }

        public Vector2 ConsumeDelta()
        {
            Vector2 result = _delta;
            _delta = Vector2.zero;
            return result;
        }
    }
}
