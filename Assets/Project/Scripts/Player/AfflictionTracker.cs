using System;
using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Player
{
    [Serializable]
    public struct AfflictionState
    {
        public int[] Ids;
        public float[] HoursRemaining;
        public double LastTickMinutes;
    }

    // Illness runs on the world clock, not on frames, so a disease you sleep through gets worse and
    // resting is not a way to dodge it
    [DisallowMultipleComponent]
    public class AfflictionTracker : MonoBehaviour, ISaveable, IAfflictable
    {
        [SerializeField] Health health;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] string saveKey = "afflictions.player";

        readonly Dictionary<AfflictionId, float> active = new Dictionary<AfflictionId, float>();

        double lastTickMinutes;

        public event Action Changed;

        public string SaveKey => saveKey;
        public IReadOnlyDictionary<AfflictionId, float> Active => active;
        public bool Has(AfflictionId affliction) => active.ContainsKey(affliction);

        public bool Contract(AfflictionId affliction)
        {
            if (affliction == AfflictionId.None || active.ContainsKey(affliction))
                return false;

            // Immunity earned from a class or a birthsign actually stops it
            if (affliction == AfflictionId.Poison && sheet != null && sheet.Has(CharacterSpecial.ImmunityToPoison))
                return false;

            active[affliction] = Afflictions.IsDisease(affliction) ? float.PositiveInfinity
                : Afflictions.HoursToBurnOut(affliction);

            Changed?.Invoke();
            Notice.Show($"Contracted {Afflictions.Describe(affliction)}.");
            return true;
        }

        public bool Cure(AfflictionId affliction)
        {
            if (!active.Remove(affliction))
                return false;

            Changed?.Invoke();
            return true;
        }

        public void CureAll()
        {
            if (active.Count == 0)
                return;

            active.Clear();
            Changed?.Invoke();
        }

        void Update()
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null)
                return;

            double minutes = clock.TotalMinutes;

            // Kept current while healthy, or the first hour of anything caught is charged for every
            // hour since the game began
            if (active.Count == 0)
            {
                lastTickMinutes = minutes;
                return;
            }

            float hours = (float)(minutes - lastTickMinutes) / WorldClock.MinutesPerHour;

            if (hours < 1f)
                return;

            lastTickMinutes = minutes;
            Tick(hours);
        }

        void Tick(float hours)
        {
            bool ended = false;

            foreach (AfflictionId affliction in new List<AfflictionId>(active.Keys))
            {
                health?.TakeDamage(new DamageInfo
                {
                    Amount = Mathf.RoundToInt(Afflictions.DamagePerHour(affliction) * hours),
                    Source = gameObject,
                    Parryable = false,
                    Unmitigated = true
                });

                if (Afflictions.IsDisease(affliction) && sheet != null)
                {
                    CharacterAttribute drained = Afflictions.Drains(affliction);
                    sheet.Set(drained, sheet.Get(drained) - 1);
                }

                float left = active[affliction] - hours;

                if (left > 0f)
                {
                    active[affliction] = left;
                    continue;
                }

                active.Remove(affliction);
                ended = true;
            }

            if (ended)
                Changed?.Invoke();
        }

        public string CaptureJson()
        {
            int[] ids = new int[active.Count];
            float[] left = new float[active.Count];

            int i = 0;
            foreach (KeyValuePair<AfflictionId, float> pair in active)
            {
                ids[i] = (int)pair.Key;
                left[i] = float.IsInfinity(pair.Value) ? -1f : pair.Value;
                i++;
            }

            return JsonUtility.ToJson(new AfflictionState
            {
                Ids = ids, HoursRemaining = left, LastTickMinutes = lastTickMinutes
            });
        }

        public void RestoreJson(string json)
        {
            active.Clear();

            AfflictionState state = JsonUtility.FromJson<AfflictionState>(json);

            // A save from before the tick time was kept would charge every hour since day 0
            lastTickMinutes = state.LastTickMinutes > 0d || WorldClock.Instance == null
                ? state.LastTickMinutes
                : WorldClock.Instance.TotalMinutes;
            if (state.Ids != null && state.HoursRemaining != null)
            {
                int count = Mathf.Min(state.Ids.Length, state.HoursRemaining.Length);
                for (int i = 0; i < count; i++)
                {
                    active[(AfflictionId)state.Ids[i]] =
                        state.HoursRemaining[i] < 0f ? float.PositiveInfinity : state.HoursRemaining[i];
                }
            }

            Changed?.Invoke();
        }
    }
}
