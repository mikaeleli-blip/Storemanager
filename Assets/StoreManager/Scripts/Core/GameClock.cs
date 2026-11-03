using System;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Game time. One scaled second = 30 game seconds. Drives Time.timeScale so every
    /// agent simply uses Time.deltaTime.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public const float GameMinutesPerSecond = 0.5f;
        public const int OpenHour = 8;
        public const int CloseHour = 22;
        public const float ClosedBoostFactor = 4f;
        public static readonly int[] Speeds = { 0, 1, 2, 4, 8 };
        static readonly string[] WeekdayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public int Day = 1;
        public float Minute = 6 * 60 + 50;
        public int SpeedIndex = 1;
        public bool ClosedBoost = true;
        int lastSpeed = 1;

        public event Action<int> HourChanged;
        public event Action NewDay;

        public int Hour { get { return Mathf.Clamp((int)(Minute / 60f), 0, 23); } }
        public bool IsOpen { get { return Hour >= OpenHour && Hour < CloseHour; } }
        public float AbsMinutes { get { return (Day - 1) * 1440f + Minute; } }
        public string Weekday { get { return WeekdayNames[(Day - 1) % 7]; } }
        public string TimeText { get { return Hour.ToString("00") + ":" + ((int)(Minute % 60f)).ToString("00"); } }
        public bool Paused { get { return SpeedIndex == 0; } }

        public float CurrentScale
        {
            get
            {
                float s = Speeds[SpeedIndex];
                if (ClosedBoost && !IsOpen) s *= ClosedBoostFactor;
                return Mathf.Min(s, 100f);
            }
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (SpeedIndex > 0) lastSpeed = SpeedIndex;
        }

        public void TogglePause()
        {
            SetSpeed(SpeedIndex == 0 ? Mathf.Max(1, lastSpeed) : 0);
        }

        void Update()
        {
            if (GameInput.Down(GameKey.Space)) TogglePause();
            if (GameInput.Down(GameKey.Alpha1)) SetSpeed(1);
            if (GameInput.Down(GameKey.Alpha2)) SetSpeed(2);
            if (GameInput.Down(GameKey.Alpha3)) SetSpeed(3);
            if (GameInput.Down(GameKey.Alpha4)) SetSpeed(4);

            Time.timeScale = CurrentScale;
            Advance(Time.deltaTime * GameMinutesPerSecond);
        }

        /// <summary>Moves the clock forward, firing every hour (and day) crossed.</summary>
        public void Advance(float minutes)
        {
            while (minutes > 0f)
            {
                float nextHour = (Hour + 1) * 60f;
                float toNext = nextHour - Minute;
                if (minutes < toNext)
                {
                    Minute += minutes;
                    return;
                }
                minutes -= toNext;
                Minute = nextHour;
                if (Minute >= 1440f)
                {
                    Minute = 0f;
                    Day++;
                    if (NewDay != null) NewDay();
                }
                if (HourChanged != null) HourChanged(Hour);
            }
        }
    }
}
