using System;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct SurvivalNeedsState
    {
        public float Hunger;
        public float Fatigue;
        public double LastTickMinutes;
    }

    [DisallowMultipleComponent]
    public class SurvivalNeeds : MonoBehaviour, ISaveable
    {
        [SerializeField, Min(0f)] float hungerPerHour = 3.2f;
        [SerializeField, Min(0f)] float fatiguePerHour = 2.6f;
        [SerializeField, Min(0f)] float fatiguePerSleepHour = 14f;
        [SerializeField, Range(0f, 1f)] float worstCapacityMultiplier = 0.55f;
        [SerializeField, Range(0f, 1f)] float worstRegenMultiplier = 0.4f;
        [SerializeField] string saveKey = "needs.player";

        float hunger;
        float fatigue;
        double lastTickMinutes;

        public event Action<float, float> Changed;

        public float Hunger => hunger;
        public float Fatigue => fatigue;
        public bool IsStarving => hunger >= 100f;
        public bool IsExhausted => fatigue >= 100f;
        public string SaveKey => saveKey;

        // Needs exist to feed the stamina economy, not to nag, their only mechanical
        // effect is shrinking the pool everything else spends from
        public float StaminaCapacityMultiplier => Mathf.Lerp(1f, worstCapacityMultiplier, Worst);
        public float StaminaRegenMultiplier => Mathf.Lerp(1f, worstRegenMultiplier, Worst);

        float Worst => Mathf.Clamp01(Mathf.Max(hunger, fatigue) / 100f);

        void Start()
        {
            if (WorldClock.Instance != null)
                lastTickMinutes = WorldClock.Instance.TotalMinutes;
        }

        void Update()
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
                return;

            // A game minute at a time. Every frame was a change, and a change re-applies the stamina
            // multipliers and redraws two HUD bars
            double elapsed = clock.TotalMinutes - lastTickMinutes;
            if (elapsed < 1d)
                return;

            lastTickMinutes = clock.TotalMinutes;
            AdvanceHours((float)(elapsed / WorldClock.MinutesPerHour));
        }

        public void AdvanceHours(float hours)
        {
            if (hours <= 0f)
                return;

            hunger = Mathf.Clamp(hunger + hungerPerHour * hours, 0f, 100f);
            fatigue = Mathf.Clamp(fatigue + fatiguePerHour * hours, 0f, 100f);
            Changed?.Invoke(hunger, fatigue);
        }

        // Called once the clock has already been skipped through the night. Those hours are settled
        // here, so the next Update must not charge them again as if they had been spent awake; it did,
        // so a night's sleep cost twice the hunger and gave back less rest than it should
        public void Sleep(float hours)
        {
            if (hours <= 0f)
                return;

            fatigue = Mathf.Clamp(fatigue - fatiguePerSleepHour * hours, 0f, 100f);
            hunger = Mathf.Clamp(hunger + hungerPerHour * hours, 0f, 100f);

            if (WorldClock.Instance != null)
                lastTickMinutes = WorldClock.Instance.TotalMinutes;

            Changed?.Invoke(hunger, fatigue);
        }

        public void Consume(ConsumableDefinition consumable)
        {
            if (consumable == null)
                return;

            hunger = Mathf.Clamp(hunger - consumable.ReduceHunger, 0f, 100f);
            fatigue = Mathf.Clamp(fatigue - consumable.ReduceFatigue, 0f, 100f);
            Changed?.Invoke(hunger, fatigue);
        }

        public string CaptureJson()
        {
            return JsonUtility.ToJson(new SurvivalNeedsState
            {
                Hunger = hunger,
                Fatigue = fatigue,
                LastTickMinutes = lastTickMinutes
            });
        }

        public void RestoreJson(string json)
        {
            SurvivalNeedsState state = JsonUtility.FromJson<SurvivalNeedsState>(json);
            hunger = state.Hunger;
            fatigue = state.Fatigue;
            lastTickMinutes = state.LastTickMinutes;
            Changed?.Invoke(hunger, fatigue);
        }
    }
}
