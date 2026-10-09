using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Player
{
    // Field of view belongs to whoever is playing, not to the scene it was authored in
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class ViewSettings : MonoBehaviour
    {
        Camera view;

        void Awake() => view = GetComponent<Camera>();

        void OnEnable()
        {
            Settings.Changed += Apply;
            Apply();
        }

        void OnDisable() => Settings.Changed -= Apply;

        void Apply()
        {
            if (view != null)
                view.fieldOfView = Settings.FieldOfView;
        }
    }
}
