using System;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // One copy of the four widgets every screen needs. Each screen used to carry its own, which is
    // how one of them ended up putting an Image on an object that already had a Text
    public static class MenuWidgets
    {
        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, position, size);
            return rect;
        }

        public static Text Label(Transform parent, Vector2 position, float width, string text, int size = 13)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Text label = go.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.color = Color.white;
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // Text is a raycast target by default and a label is nobody's button. On the world map
            // seventy one place names, 160 px wide apiece, blanketed the markers and swallowed every
            // click meant for one. Nothing here is ever meant to be clicked through its caption
            label.raycastTarget = false;

            Anchor(label.rectTransform, position, new Vector2(width, size + 7f));
            return label;
        }

        // The caption is always a child of the button, never a second Graphic on it, a GameObject
        // carries one Graphic and the second AddComponent silently returns null
        public static Text Button(Transform parent, Vector2 position, Vector2 size, Action onClick, Color fill)
        {
            GameObject go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.color = fill;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick());

            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, position, size);

            Text caption = Label(rect, new Vector2(8f, 0f), size.x - 12f, string.Empty);
            caption.raycastTarget = false;
            return caption;
        }

        public static Text Button(Transform parent, Vector2 position, Vector2 size, Action onClick) =>
            Button(parent, position, size, onClick, new Color(0.15f, 0.15f, 0.19f, 0.9f));

        public static Image Block(Transform parent, Vector2 position, Vector2 size, Color colour,
            float rotation = 0f)
        {
            GameObject go = new GameObject("Block", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;

            Anchor(image.rectTransform, position, size);

            if (!Mathf.Approximately(rotation, 0f))
            {
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = position + new Vector2(size.x, -size.y) * 0.5f;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }

            return image;
        }

        // A list that scrolls. What comes back is the content to put rows into, laid out downwards from
        // its top as every panel here already is; give it its full height with Fit once the rows are in.
        // The viewport takes no clicks, so a list hidden behind another panel never blocks it, and the
        // wheel still reaches the list through the rows. Fixed panels lost every row past the bottom
        public static RectTransform ScrollPanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform viewport = Panel(parent, name, position, size);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Panel(viewport, $"{name} Rows", Vector2.zero, new Vector2(size.x, size.y));

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        public static void Fit(RectTransform content, float height)
        {
            RectTransform viewport = content.parent as RectTransform;
            float least = viewport != null ? viewport.rect.height : 0f;

            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(least, height));
        }

        public static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    public interface IMenuPage
    {
        string Title { get; }
        RectTransform Root { get; }
        void Refresh();
    }
}
