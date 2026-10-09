using System;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Player
{
    [Serializable]
    public struct ManaState
    {
        public float Current;
        public int Max;
    }

    [DisallowMultipleComponent]
    public class Mana : MonoBehaviour, ISaveable
    {
        [SerializeField, Min(1)] int max = 100;
        [SerializeField, Min(0f)] float regenPerSecond = 4f;
        [SerializeField] string saveKey = "mana.player";

        public string SaveKey => saveKey;

        float current;

        public event Action<float, int> Changed;

        public int Max => max;
        public float Current => current;
        public float Normalized => max > 0 ? current / max : 0f;

        void Awake() => current = max;

        void Update()
        {
            if (current >= max)
                return;

            current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
            Changed?.Invoke(current, max);
        }

        public void Configure(int newMax)
        {
            max = Mathf.Max(1, newMax);
            current = Mathf.Min(current, max);
            Changed?.Invoke(current, max);
        }

        public bool Spend(int amount)
        {
            if (amount > current)
                return false;

            current -= amount;
            Changed?.Invoke(current, max);
            return true;
        }

        public string CaptureJson() => JsonUtility.ToJson(new ManaState { Current = current, Max = max });

        public void RestoreJson(string json)
        {
            ManaState state = JsonUtility.FromJson<ManaState>(json);

            if (state.Max > 0)
                max = state.Max;

            current = Mathf.Clamp(state.Current, 0f, max);
            Changed?.Invoke(current, max);
        }
    }
}
