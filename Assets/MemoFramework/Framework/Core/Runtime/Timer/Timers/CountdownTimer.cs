using System;
using UnityEngine;

namespace MemoFramework
{
    /// <summary>
    /// Timer that counts down from a specific value to zero.
    /// </summary>
    public class CountdownTimer : Timer
    {
        public Action OnCompleted;

        public CountdownTimer(float value, bool start = false) : base(value, start)
        {
            if (start)
                Restart();
        }

        public override void Tick()
        {
            if (IsRunning && CurrentTime > 0)
            {
                CurrentTime -= Time.deltaTime;
            }

            if (IsRunning && CurrentTime <= 0)
            {
                Stop();
                OnCompleted?.Invoke();
            }
        }

        public override bool IsFinished => CurrentTime <= 0;
    }
}