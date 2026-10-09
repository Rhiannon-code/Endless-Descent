using UnityEngine;

namespace EndlessDescent.Data
{
    public enum ArmourWeight { Light, Medium, Heavy }

    // Daggerfall charges nothing for wearing plate, which is why anyone who can afford it wears
    // nothing else. Here the protection comes out of the same stamina bar that swings, blocks and
    // runs, and off the footing and the quiet as well, so picking a set is a decision
    public static class ArmourWeights
    {
        // Head, chest, legs, hands, feet and the off hand, six places to put weight, all heavy
        public const int MaxLoad = 12;

        public static int Load(ArmourWeight weight) => (int)weight;

        public static float Fraction(int load) => Mathf.Clamp01(load / (float)MaxLoad);

        public static float StaminaRegenScale(int load) => Mathf.Lerp(1f, 0.55f, Fraction(load));

        public static float MoveScale(int load) => Mathf.Lerp(1f, 0.85f, Fraction(load));

        // What the set does to how far away you can be heard and seen moving
        public static float NoiseScale(int load) => Mathf.Lerp(1f, 1.9f, Fraction(load));

        public static string Describe(int load)
        {
            float fraction = Fraction(load);

            if (fraction <= 0.05f) return "Unencumbered";
            if (fraction < 0.3f) return "Light";
            if (fraction < 0.6f) return "Medium";
            if (fraction < 0.85f) return "Heavy";
            return "Ponderous";
        }
    }
}
