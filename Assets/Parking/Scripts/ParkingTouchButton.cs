using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityParking
{
    // Each control owns its pointer IDs so two thumbs can steer and drive simultaneously.
    public class ParkingTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        readonly HashSet<int> pointers = new HashSet<int>();
        public bool Held => pointers.Count > 0;
        public Image surface;
        public Color normal = new Color(.13f, .20f, .24f, .94f);
        public Color pressed = new Color(.18f, .65f, .48f, 1);
        public void OnPointerDown(PointerEventData e) { pointers.Add(e.pointerId); Refresh(); }
        public void OnPointerUp(PointerEventData e) { pointers.Remove(e.pointerId); Refresh(); }
        public void OnPointerExit(PointerEventData e) { pointers.Remove(e.pointerId); Refresh(); }
        public void Clear() { pointers.Clear(); Refresh(); }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focus) { if (!focus) Clear(); }
        void OnApplicationPause(bool paused) { if (paused) Clear(); }
        void Refresh() { if (surface) surface.color = Held ? pressed : normal; }
    }
}
