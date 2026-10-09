using System;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Combat
{
    [Serializable]
    public struct HealthState
    {
        public int Current;
        public int Max;
    }

    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable, ISaveable
    {
        [SerializeField, Min(1)] int maxHealth = 100;
        [SerializeField] DamageMitigation mitigation;
        [SerializeField] Poise poise;
        [SerializeField] string saveKey = "health.player";

        int current;

        public event Action<int, int> Changed;
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public int Max => maxHealth;
        public int Current => current;
        public float Normalized => maxHealth > 0 ? current / (float)maxHealth : 0f;
        public bool IsAlive => current > 0;
        public string SaveKey => saveKey;

        void Awake()
        {
            current = maxHealth;

            // Creatures are given their armour when they spawn, so the component is not on the prefab
            // to be wired in the inspector
            if (mitigation == null)
                mitigation = GetComponent<DamageMitigation>();
        }

        public void Configure(int newMax)
        {
            maxHealth = Mathf.Max(1, newMax);
            current = maxHealth;
            Changed?.Invoke(current, maxHealth);
        }

        // A new ceiling that keeps what is left under it. Configure starts full, which is right for a
        // creature spawning and wrong for a player whose Endurance just went up mid fight
        public void SetMax(int newMax)
        {
            maxHealth = Mathf.Max(1, newMax);
            current = Mathf.Min(current, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive)
                return;

            if (mitigation != null && !info.Unmitigated)
                info = mitigation.Modify(info);

            if (info.Amount > 0)
            {
                current = Mathf.Max(0, current - info.Amount);
                Changed?.Invoke(current, maxHealth);
            }

            Damaged?.Invoke(info);

            if (info.PoiseDamage > 0f && poise != null)
                poise.Apply(info.PoiseDamage);

            if (current <= 0)
                Died?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return;

            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }

        public string CaptureJson() => JsonUtility.ToJson(new HealthState { Current = current, Max = maxHealth });

        // The ceiling comes back too, the sheet that sets it may be restored after this
        public void RestoreJson(string json)
        {
            HealthState state = JsonUtility.FromJson<HealthState>(json);

            if (state.Max > 0)
                maxHealth = state.Max;

            current = Mathf.Clamp(state.Current, 0, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }
    }
}
