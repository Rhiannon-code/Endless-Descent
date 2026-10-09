using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform cameraPivot;
        [Tooltip("Overridden by the options screen unless useSettings is off.")]
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] bool useSettings = true;
        [SerializeField] float minPitch = -89f;
        [SerializeField] float maxPitch = 89f;
        [SerializeField] bool lockCursor = true;

        float pitch;

        // Awake runs on a disabled component, so grabbing the cursor there takes it from the
        // character creator as well, a locked cursor puts every UI raycast off screen at (-1, -1)
        void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            // Mouse deltas are already per frame, so this must not be scaled by deltaTime
            Vector2 delta = input.Look * (useSettings ? Settings.Sensitivity : sensitivity);

            transform.Rotate(0f, delta.x, 0f, Space.Self);

            float vertical = useSettings && Settings.InvertY ? -delta.y : delta.y;
            pitch = Mathf.Clamp(pitch - vertical, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
