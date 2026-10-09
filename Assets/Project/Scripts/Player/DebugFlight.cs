using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EndlessDescent.Player
{
    // Development builds only. F1 takes off and lands, WASD flies where you look, Space and Ctrl rise
    // and sink, Shift is fast and the scroll wheel sets the base speed
    [DisallowMultipleComponent]
    public class DebugFlight : MonoBehaviour
    {
        const float MinSpeed = 2f;
        const float MaxSpeed = 4000f;
        const float BoostFactor = 6f;
        const float ScrollStep = 1.25f;
        const float BelowMetres = 500f;
        const float AboveMetres = 6000f;

        PlayerInputReader input;
        PlayerLook look;
        CharacterController controller;
        Behaviour[] grounded;
        bool[] wasEnabled;
        bool restoreWhenFree;
        float speed = 25f;
        string status;
        float statusUntil;

        public bool Flying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Debug.isDebugBuild)
                return;

            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (PlayerLocomotion player in FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (player.GetComponent<DebugFlight>() == null)
                    player.gameObject.AddComponent<DebugFlight>();
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            look = GetComponent<PlayerLook>();
            controller = GetComponent<CharacterController>();

            // The three that drive the character controller themselves: walking, mantling, dodging
            grounded = new Behaviour[] { GetComponent<PlayerLocomotion>(), GetComponent<PlayerMantle>(), GetComponent<PlayerCombat>() };
            wasEnabled = new bool[grounded.Length];
        }

        void Update()
        {
            bool free = Cursor.lockState == CursorLockMode.Locked && look != null && look.enabled;

            if (restoreWhenFree && free)
            {
                Restore();
                restoreWhenFree = false;
            }

            if (Flying)
                HoldFlight();

            Keyboard keys = Keyboard.current;
            if (keys == null || !free)
                return;

            if (keys.f1Key.wasPressedThisFrame)
            {
                if (Flying) Land();
                else TakeOff();
            }

            if (Flying)
                Fly(keys);
        }

        // A journey, fast travel or a respawn re-enables the controller when it places the player, and
        // that ends flight. Closing a menu only re-enables walking, and flight simply switches it off again
        void HoldFlight()
        {
            if (controller != null && controller.enabled)
            {
                Flying = false;
                restoreWhenFree = true;
                return;
            }

            foreach (Behaviour behaviour in grounded)
                if (behaviour != null && behaviour.enabled)
                    behaviour.enabled = false;
        }

        void TakeOff()
        {
            for (int i = 0; i < grounded.Length; i++)
            {
                wasEnabled[i] = grounded[i] != null && grounded[i].enabled;
                if (grounded[i] != null) grounded[i].enabled = false;
            }

            if (controller != null)
                controller.enabled = false;

            Flying = true;
        }

        void Land()
        {
            if (!FindGround(out Vector3 ground))
            {
                Say("No ground under here yet. Keep flying, or wait for the terrain to arrive.");
                return;
            }

            transform.position = ground;

            if (controller != null)
                controller.enabled = true;

            Flying = false;
            Restore();
        }

        void Restore()
        {
            for (int i = 0; i < grounded.Length; i++)
                if (grounded[i] != null) grounded[i].enabled = wasEnabled[i];
        }

        // Below first, so landing in a dungeon keeps you in it; from far above only when there is
        // nothing close below, which is what flying under the terrain looks like
        bool FindGround(out Vector3 ground) =>
            Cast(transform.position + Vector3.up * 0.5f, BelowMetres, out ground)
            || Cast(transform.position + Vector3.up * AboveMetres, AboveMetres * 2f, out ground);

        bool Cast(Vector3 from, float distance, out Vector3 point)
        {
            point = default;
            float nearest = float.MaxValue;

            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(transform))
                    continue;

                nearest = hit.distance;
                point = hit.point + Vector3.up * 0.05f;
            }

            return nearest < float.MaxValue;
        }

        void Fly(Keyboard keys)
        {
            float scroll = Mouse.current != null ? Mouse.current.scroll.y.ReadValue() : 0f;

            if (scroll != 0f)
                speed = Mathf.Clamp(scroll > 0f ? speed * ScrollStep : speed / ScrollStep, MinSpeed, MaxSpeed);

            Transform view = Camera.main != null ? Camera.main.transform : transform;
            Vector2 move = input != null ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero;

            Vector3 direction = view.forward * move.y + view.right * move.x;
            if (keys.spaceKey.isPressed) direction += Vector3.up;
            if (keys.leftCtrlKey.isPressed) direction += Vector3.down;

            float rate = keys.leftShiftKey.isPressed ? speed * BoostFactor : speed;
            transform.position += Vector3.ClampMagnitude(direction, 1f) * (rate * Time.unscaledDeltaTime);
        }

        void Say(string message)
        {
            status = message;
            statusUntil = Time.unscaledTime + 3f;
        }

        void OnGUI()
        {
            if (Flying)
                GUI.Label(new Rect(Screen.width * 0.5f - 170f, 12f, 340f, 22f),
                    $"FLYING  {speed:0} m/s  at {transform.position.y:0} m   F1 to land");

            if (status != null && Time.unscaledTime < statusUntil)
                GUI.Label(new Rect(Screen.width * 0.5f - 250f, 36f, 500f, 22f), status);
        }
    }
}
