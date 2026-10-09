using UnityEngine;

namespace EndlessDescent.Combat
{
    public struct DamageInfo
    {
        public int Amount;
        public float PoiseDamage;
        public GameObject Source;
        public Vector3 Direction;
        public bool Parryable;

        // The share of the target's armour this blow goes through, and damage that armour never
        // sees at all. An axe gets the first, a burning edge gets the second
        public float Penetration;
        public int Unstoppable;

        // The Lamplighters ask one question and they check. Somebody has to know the answer
        public bool FromSpell;

        // Harm from inside, a disease or a poison already in the blood. Armour, a shield and a
        // well timed dodge do nothing about it
        public bool Unmitigated;

        public static DamageInfo Melee(int amount, float poiseDamage, GameObject source, Vector3 direction)
        {
            return new DamageInfo
            {
                Amount = amount,
                PoiseDamage = poiseDamage,
                Source = source,
                Direction = direction,
                Parryable = true
            };
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
