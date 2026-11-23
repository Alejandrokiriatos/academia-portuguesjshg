using UnityEngine;
using UnityEngine.EventSystems;

namespace RustSurvival.Player
{
    /// <summary>Attach to a UI joystick background. Assign its knob RectTransform.</summary>
    public sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform knob;
        [SerializeField, Range(0.1f, 1f)] private float deadZone = 0.12f;
        [SerializeField, Range(0.2f, 1f)] private float handleRange = 0.65f;

        public Vector2 Value { get; private set; }
        public bool IsDragging { get; private set; }

        private void Awake()
        {
            if (background == null) background = transform as RectTransform;
            ResetKnob();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsDragging = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Vector2 half = background.rect.size * 0.5f;
            Vector2 normalized = new Vector2(
                half.x > 0f ? local.x / half.x : 0f,
                half.y > 0f ? local.y / half.y : 0f);
            Value = Vector2.ClampMagnitude(normalized, 1f);

            if (Value.magnitude < deadZone)
                Value = Vector2.zero;

            if (knob != null)
                knob.anchoredPosition = Vector2.Scale(Value, background.rect.size * handleRange);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsDragging = false;
            Value = Vector2.zero;
            ResetKnob();
        }

        private void ResetKnob()
        {
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
    }
}
