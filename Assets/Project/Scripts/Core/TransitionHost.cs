using UnityEngine;

namespace EndlessDescent.Core
{
    // Runs transitions on an object that outlives whatever started them, a door in a town that is cleared
    // while the screen is dark would otherwise take its own transition down with it
    [DisallowMultipleComponent]
    public class TransitionHost : MonoBehaviour
    {
        static TransitionHost instance;

        public static TransitionHost Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                GameObject host = new GameObject("Transitions");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<TransitionHost>();
                return instance;
            }
        }
    }
}
