using System;
using UnityEngine;

namespace EndlessDescent.Core
{
    // What the game tells the player: a fine paid, a disease caught, a rest cut short. All of it used
    // to go to Debug.Log, which nobody playing ever sees. The HUD shows these (UI/NoticeLine)
    public static class Notice
    {
        public static event Action<string> Posted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Posted = null;

        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (Posted != null)
                Posted(text);
            else
                Debug.Log(text);
        }
    }
}
