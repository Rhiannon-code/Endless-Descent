using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // The options, in the one place both the front end and the in game menu can put them. It builds
    // its own rows, so neither of those has to author a widget per setting
    [DisallowMultipleComponent]
    public class OptionsScreen : MonoBehaviour
    {
        [SerializeField] RectTransform root;

        readonly List<Action> refreshers = new List<Action>();

        public RectTransform Root => root;
        public event Action Closed;

        public void Build(RectTransform into)
        {
            root = into;

            float y = -12f;

            MenuWidgets.Label(root, new Vector2(20f, y), 400f, "OPTIONS", 18).color = new Color(0.95f, 0.85f, 0.5f);
            y -= 40f;

            y = Slider(y, "Mouse sensitivity", Settings.MinSensitivity, Settings.MaxSensitivity,
                () => Settings.Sensitivity, v => Settings.Sensitivity = v, "F3");

            y = Slider(y, "Field of view", 55f, 110f,
                () => Settings.FieldOfView, v => Settings.FieldOfView = v, "F0");

            y = Slider(y, "Volume", 0f, 1f,
                () => Settings.Volume, v => Settings.Volume = v, "P0");

            y = Toggle(y, "Invert look", () => Settings.InvertY, v => Settings.InvertY = v);
            y = Toggle(y, "Quest markers", () => Settings.QuestMarkers, v => Settings.QuestMarkers = v);

            MenuWidgets.Button(root, new Vector2(20f, y - 12f), new Vector2(160f, 30f),
                () => Closed?.Invoke()).text = "Back";

            Refresh();
        }

        public void Refresh()
        {
            foreach (Action refresher in refreshers)
                refresher();
        }

        float Slider(float y, string caption, float min, float max, Func<float> get, Action<float> set, string format)
        {
            MenuWidgets.Label(root, new Vector2(20f, y), 220f, caption);

            Text readout = MenuWidgets.Label(root, new Vector2(250f, y), 90f, string.Empty);
            readout.color = new Color(0.85f, 0.82f, 0.7f);

            float step = (max - min) / 20f;

            MenuWidgets.Button(root, new Vector2(350f, y), new Vector2(30f, 24f),
                () => { set(get() - step); Refresh(); }).text = "-";

            MenuWidgets.Button(root, new Vector2(386f, y), new Vector2(30f, 24f),
                () => { set(get() + step); Refresh(); }).text = "+";

            refreshers.Add(() => readout.text = get().ToString(format));
            return y - 32f;
        }

        float Toggle(float y, string caption, Func<bool> get, Action<bool> set)
        {
            MenuWidgets.Label(root, new Vector2(20f, y), 220f, caption);

            Text state = MenuWidgets.Button(root, new Vector2(250f, y), new Vector2(166f, 24f),
                () => { set(!get()); Refresh(); });

            refreshers.Add(() => state.text = get() ? "On" : "Off");
            return y - 32f;
        }
    }
}
