using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Core
{
    // Every move that could show the world being put together goes through here, the screen
    // fades, the destination is prepared over as many frames as it needs while the game keeps drawing,
    // and the move happens in the dark. Nothing waits inside one frame, so nothing looks frozen
    public static class Transition
    {
        public const float Quick = 0.15f;
        public const float Long = 0.35f;

        public static bool Busy { get; private set; }
        public static float Blackout { get; private set; }
        public static string Caption { get; private set; }

        public static event Action Began;
        public static event Action Ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Busy = false;
            Blackout = 0f;
            Caption = null;
            Began = null;
            Ended = null;
        }

        public static bool Start(string caption, float fade, IEnumerator prepare, Action arrive = null) =>
            Begin(caption, fade, false, prepare, arrive);

        // For the start of the game, which has nothing on screen worth fading out of
        public static bool StartInDark(string caption, IEnumerator prepare, Action arrive = null) =>
            Begin(caption, Long, true, prepare, arrive);

        public static void Report(string caption) => Caption = caption;

        static bool Begin(string caption, float fade, bool dark, IEnumerator prepare, Action arrive)
        {
            if (Busy)
                return false;

            Busy = true;
            Caption = caption;

            if (dark)
                Blackout = 1f;

            TransitionHost.Instance.StartCoroutine(Run(fade, dark, prepare, arrive));
            return true;
        }

        static IEnumerator Run(float fade, bool dark, IEnumerator prepare, Action arrive)
        {
            Raise(Began);

            for (float t = dark ? fade : 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                Blackout = t / fade;
                yield return null;
            }

            Blackout = 1f;

            // Driven by hand rather than handed to Unity, so an exception part way through is caught here
            // and the screen still comes back, instead of the coroutine dying with the screen black
            Stack<IEnumerator> work = new Stack<IEnumerator>();
            if (prepare != null)
                work.Push(prepare);

            bool failed = false;

            while (work.Count > 0)
            {
                IEnumerator top = work.Peek();
                bool more;

                try
                {
                    more = top.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    failed = true;
                    break;
                }

                if (!more)
                {
                    work.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    work.Push(nested);
                    continue;
                }

                yield return null;
            }

            if (!failed && arrive != null)
            {
                try
                {
                    arrive();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // A frame for the moved player to settle onto the ground that was prepared for them
            yield return null;

            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                Blackout = 1f - t / fade;
                yield return null;
            }

            Blackout = 0f;
            Caption = null;
            Busy = false;
            Raise(Ended);
        }

        static void Raise(Action handlers)
        {
            try
            {
                handlers?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
