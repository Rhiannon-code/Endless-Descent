using System.Collections;

namespace EndlessDescent.Core
{
    // Work that goes up a step at a time. A finished step yields null, a step waiting on
    // something the main thread cannot hurry, such as a worker, yields WaitFrame
    public static class Steps
    {
        public static readonly object WaitFrame = new object();

        // As many steps a frame as fit in the budget. One step longer than the budget still takes as
        // long as it takes, so how big a step is matters as much as how many there are
        // A step that throws ends the work and is logged, rather than escaping into the coroutine that
        // runs it, which stopped there silently and left whoever started it believing it still ran
        public static IEnumerator Budgeted(IEnumerator work, float budgetMs)
        {
            System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

            while (true)
            {
                clock.Restart();
                bool more;

                do more = Step(work);
                while (more && work.Current != WaitFrame && clock.Elapsed.TotalMilliseconds < budgetMs);

                if (!more)
                    yield break;

                yield return null;
            }
        }

        static bool Step(IEnumerator work)
        {
            try
            {
                return work.MoveNext();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return false;
            }
        }

        public static void RunToEnd(IEnumerator work)
        {
            while (work.MoveNext()) { }
        }
    }
}
