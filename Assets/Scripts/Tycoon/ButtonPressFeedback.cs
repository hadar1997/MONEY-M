using UnityEngine;
using UnityEngine.EventSystems;

namespace Tycoon
{
    /// <summary>Tiny scale-down-on-press feedback for UI buttons - Unity's default
    /// Button only gives a color tint on click, which reads flat as a mobile touch
    /// target. Added to every button UIFactory builds.</summary>
    public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        RectTransform rt;

        void Awake() => rt = (RectTransform)transform;

        public void OnPointerDown(PointerEventData eventData) => rt.localScale = Vector3.one * 0.95f;
        public void OnPointerUp(PointerEventData eventData) => rt.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData eventData) => rt.localScale = Vector3.one;
    }
}
