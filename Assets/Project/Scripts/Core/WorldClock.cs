using System;
using UnityEngine;

namespace EndlessDescent.Core
{
    [Serializable]
    public struct WorldClockState
    {
        public double TotalMinutes;
    }

    [DisallowMultipleComponent]
    public class WorldClock : MonoBehaviour
    {
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        public static WorldClock Instance { get; private set; }

        [SerializeField] float realSecondsPerGameMinute = 1f;
        [SerializeField] int startDay = 1;
        [SerializeField, Range(0, 23)] int startHour = 8;

        double totalMinutes;
        int lastHour;
        int lastDay;

        public event Action<int> HourChanged;
        public event Action<int> DayChanged;

        public double TotalMinutes => totalMinutes;
        public int Day => (int)(totalMinutes / MinutesPerDay);
        public int Hour => (int)(totalMinutes / MinutesPerHour) % HoursPerDay;
        public int Minute => (int)(totalMinutes % MinutesPerHour);
        public float DayFraction => (float)(totalMinutes % MinutesPerDay / MinutesPerDay);
        public bool Paused { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"A second {nameof(WorldClock)} exists on '{name}'. There must be exactly one.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            totalMinutes = (double)startDay * MinutesPerDay + (double)startHour * MinutesPerHour;
            lastHour = Hour;
            lastDay = Day;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (Paused || realSecondsPerGameMinute <= 0f)
                return;

            Advance(Time.deltaTime / realSecondsPerGameMinute);
        }

        public void Skip(double minutes)
        {
            if (minutes > 0d)
                Advance(minutes);
        }

        public WorldClockState CaptureState() => new WorldClockState { TotalMinutes = totalMinutes };

        public void RestoreState(WorldClockState state)
        {
            totalMinutes = state.TotalMinutes;
            lastHour = Hour;
            lastDay = Day;
        }

        // Tells listeners the hour and day as if the clock had just reached them. Called once a whole
        // save is back, not when the clock alone is, or deadlines would be judged against the quests
        // of the game being left. Without it schedules kept the old hour until the next one came
        public void Announce()
        {
            HourChanged?.Invoke(lastHour);
            DayChanged?.Invoke(lastDay);
        }

        void Advance(double minutes)
        {
            totalMinutes += minutes;

            int hour = Hour;
            int day = Day;

            // Sleeping skips hours at once, so listeners re-read the clock instead of receiving
            // one event per boundary crossed
            if (hour != lastHour)
            {
                lastHour = hour;
                HourChanged?.Invoke(hour);
            }

            if (day != lastDay)
            {
                lastDay = day;
                DayChanged?.Invoke(day);
            }
        }
    }
}
