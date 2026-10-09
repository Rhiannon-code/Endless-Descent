using System;
using UnityEngine;

namespace EndlessDescent.Player
{
    [Serializable]
    public struct PlayerStaminaState
    {
        public float Current;
    }

    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] float max = 100f;
        [SerializeField] float regenPerSecond = 18f;
        [SerializeField] float regenDelay = 1.1f;
        [SerializeField] GearLoad gear;

        float current;
        float regenBlockedUntil;
        float capacityMultiplier = 1f;
        float regenMultiplier = 1f;

        public event Action<float, float> Changed;

        public float Max => max * capacityMultiplier;
        public float Current => current;
        public float Normalized => Max > 0f ? current / Max : 0f;
        public bool IsEmpty => current <= 0f;

        void Awake() => current = Max;

        // Endurance and Strength set the size of the pool, hunger and fatigue then scale it
        public void Configure(float newMax)
        {
            max = Mathf.Max(1f, newMax);
            current = Mathf.Min(current, Max);
            Changed?.Invoke(current, Max);
        }

        // Hunger and fatigue shrink the pool that movement and combat both spend from
        public void SetMultipliers(float capacity, float regen)
        {
            capacityMultiplier = Mathf.Max(0.05f, capacity);
            regenMultiplier = Mathf.Max(0f, regen);
            current = Mathf.Min(current, Max);
            Changed?.Invoke(current, Max);
        }

        void Update()
        {
            if (current >= Max || Time.time < regenBlockedUntil)
                return;

            float worn = gear != null ? gear.StaminaRegenScale : 1f;
            Set(Mathf.Min(Max, current + regenPerSecond * regenMultiplier * worn * Time.deltaTime));
        }

        public bool CanSpend(float amount) => current >= amount;

        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
                return true;

            if (current < amount)
                return false;

            Set(current - amount);
            regenBlockedUntil = Time.time + regenDelay;
            return true;
        }

        public PlayerStaminaState CaptureState() => new PlayerStaminaState { Current = current };

        public void RestoreState(PlayerStaminaState state) => Set(Mathf.Clamp(state.Current, 0f, Max));

        void Set(float value)
        {
            current = value;
            Changed?.Invoke(current, Max);
        }
    }
}
