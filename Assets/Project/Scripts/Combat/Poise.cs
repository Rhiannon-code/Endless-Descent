using System;
using UnityEngine;

namespace EndlessDescent.Combat
{
    [DisallowMultipleComponent]
    public class Poise : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxPoise = 30f;
        [SerializeField, Min(0f)] float regenPerSecond = 10f;
        [SerializeField, Min(0f)] float staggerDuration = 0.8f;

        float current;
        float staggerUntil;

        public event Action Staggered;

        public bool IsStaggered => Time.time < staggerUntil;

        void Awake() => current = maxPoise;

        void Update()
        {
            if (current < maxPoise && !IsStaggered)
                current = Mathf.Min(maxPoise, current + regenPerSecond * Time.deltaTime);
        }

        public void Apply(float poiseDamage)
        {
            if (IsStaggered)
                return;

            current -= poiseDamage;
            if (current > 0f)
                return;

            current = maxPoise;
            staggerUntil = Time.time + staggerDuration;
            Staggered?.Invoke();
        }

        public void ForceStagger() => Apply(maxPoise);
    }
}
