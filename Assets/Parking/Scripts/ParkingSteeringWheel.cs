using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CityParking
{
    // Track one finger around a fixed centre; rotate only the visual, not the touch surface.
    public class ParkingSteeringWheel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform visual;
        public Func<bool> canDrive;
        public Action released;
        public bool Held { get; private set; }
        public float Steering => Held ? -angle / 135f : 0;
        int pointer;
        float angle, previousAngle;
        bool hasAngle;

        public void OnPointerDown(PointerEventData e)
        {
            if (Held || (canDrive != null && !canDrive())) return;
            pointer = e.pointerId;
            Held = true;
            hasAngle = TryAngle(e, out previousAngle);
        }
        public void OnDrag(PointerEventData e)
        {
            if (!Held || pointer != e.pointerId) return;
            if (!TryAngle(e, out float current)) { hasAngle = false; return; }
            if (hasAngle) angle = Mathf.Clamp(angle + Mathf.DeltaAngle(previousAngle, current), -135, 135);
            previousAngle = current; hasAngle = true;
            if (visual) visual.localRotation = Quaternion.Euler(0, 0, angle);
        }
        bool TryAngle(PointerEventData e, out float value)
        {
            value = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var point)
                || point.sqrMagnitude < 225) return false;
            value = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            return true;
        }
        public void OnPointerUp(PointerEventData e) { if (Held && pointer == e.pointerId) Clear(); }
        public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
        public void Clear()
        {
            bool wasHeld = Held;
            Held = hasAngle = false; angle = 0;
            if (visual) visual.localRotation = Quaternion.identity;
            if (wasHeld) released?.Invoke();
        }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focus) { if (!focus) Clear(); }
        void OnApplicationPause(bool paused) { if (paused) Clear(); }
    }
}
