using System;
using UnityEngine;

namespace MemoFramework
{
    public class RepeatTimer : Timer
    {
        public Action OnCompletedOnce;
        public Action OnAllCompleted;
        public int RemainingRepeatTimes { get; private set; }

        public RepeatTimer(float interval, int repeatTimes, bool start = false) : base(interval, start)
        {
            RemainingRepeatTimes = repeatTimes < 0 ? int.MaxValue / 3 : repeatTimes;
            if (start) Restart();
        }

        public override void Tick()
        {
            if (IsRunning && CurrentTime > 0)
            {
                CurrentTime -= Time.deltaTime;
            }

            if (IsRunning && CurrentTime <= 0)
            {
                OnCompletedOnce?.Invoke();
                RemainingRepeatTimes--;
                if (RemainingRepeatTimes > 0)
                {
                    Restart();
                }
                else
                {
                    OnAllCompleted?.Invoke();
                    Stop();
                }
            }
        }

        public override bool IsFinished => CurrentTime <= 0 && RemainingRepeatTimes <= 0;
    }
}