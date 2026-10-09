using System;
using System.Collections.Generic;

namespace EndlessDescent.World
{
    // One thing a hall, a bench or a teacher can do for you. Choose does it and says what happened
    public struct ServiceOption
    {
        public string Text;
        public bool Available;
        public Func<string> Choose;
    }

    // The options are asked for again after every choice, because buying one changes what the rest
    // cost and whether they can still be had
    public interface IServiceScreen
    {
        void Open(string title, Func<IReadOnlyList<ServiceOption>> options);
    }

    // Set by the screen itself when the HUD installs it, so nothing in the world has to search for it
    public static class ServiceScreens
    {
        public static IServiceScreen Current { get; set; }
    }
}
