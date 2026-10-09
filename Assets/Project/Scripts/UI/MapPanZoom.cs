using UnityEngine;
using UnityEngine.EventSystems;

namespace EndlessDescent.UI
{
    // Drag to pan, wheel to zoom. Kept apart from the page that draws the map so the same handling
    // serves the world map and the local map without either knowing about the other
    [DisallowMultipleComponent]
    public class MapPanZoom : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public const float MinZoom = 1f;
        public const float MaxZoom = 14f;

        public float Zoom { get; private set; } = 1f;
        public Vector2 Pan { get; private set; }

        // Panning only moves what is already drawn, zooming changes which labels fit and has to
        // redraw. Rebuilding seventy one markers on every drag frame destroyed them between the
        // press and the release, which is why a place could never be clicked
        public System.Action Moved;
        public System.Action Changed;

        public void Reset()
        {
            Zoom = 1f;
            Pan = Vector2.zero;
            Changed?.Invoke();
        }

        public void Step(float factor)
        {
            float was = Zoom;
            Zoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);

            // Zooming about the middle of the view, so the pan has to grow with it or the point you
            // were looking at slides away as you zoom in
            Pan *= Zoom / was;
            Changed?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            Pan += eventData.delta;
            Moved?.Invoke();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Abs(eventData.scrollDelta.y) > 0.01f)
                Step(eventData.scrollDelta.y > 0f ? 1.15f : 1f / 1.15f);
        }
    }
}
