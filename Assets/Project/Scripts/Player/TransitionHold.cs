using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndlessDescent.Player
{
    // Holds the player still while a transition has the screen dark. The ground under them can be taken
    // away and rebuilt somewhere else in that time, and falling through it is how you arrive under the
    // world. Installs itself on the player, so no scene needs rebuilding
    [DisallowMultipleComponent]
    public class TransitionHold : MonoBehaviour
    {
        Behaviour[] held;
        bool[] wasEnabled;
        CharacterController controller;
        bool controllerWasEnabled;
        bool holding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (PlayerLocomotion player in FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (player.GetComponent<TransitionHold>() == null)
                    player.gameObject.AddComponent<TransitionHold>();
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            held = new Behaviour[]
            {
                GetComponent<PlayerLocomotion>(), GetComponent<PlayerMantle>(),
                GetComponent<PlayerCombat>(), GetComponent<PlayerInteractor>()
            };
            wasEnabled = new bool[held.Length];
        }

        void OnEnable()
        {
            Transition.Began += Hold;
            Transition.Ended += Release;

            if (Transition.Busy)
                Hold();
        }

        void OnDisable()
        {
            Transition.Began -= Hold;
            Transition.Ended -= Release;
            Release();
        }

        void Hold()
        {
            if (holding)
                return;

            holding = true;

            for (int i = 0; i < held.Length; i++)
            {
                wasEnabled[i] = held[i] != null && held[i].enabled;
                if (held[i] != null) held[i].enabled = false;
            }

            controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
        }

        void Release()
        {
            if (!holding)
                return;

            holding = false;

            for (int i = 0; i < held.Length; i++)
                if (held[i] != null) held[i].enabled = wasEnabled[i];

            if (controller != null) controller.enabled = controllerWasEnabled;
        }
    }
}
