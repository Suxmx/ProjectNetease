using System;
using UnityEngine;

namespace MemoFramework {
    /// <summary>
    /// Timer that ticks at a specific frequency. (N times per second)
    /// </summary>
    public class FrequencyTimer : Timer {
        public int TicksPerSecond { get; private set; }
        
        public Action OnTick = delegate { };
        
        float timeThreshold;

        public FrequencyTimer(int ticksPerSecond,bool start=false) : base(0,start) {
            CalculateTimeThreshold(ticksPerSecond);
            if(start) Restart();
        }

        public override void Tick() {
            if (IsRunning && CurrentTime >= timeThreshold) {
                CurrentTime -= timeThreshold;
                OnTick.Invoke();
            }

            if (IsRunning && CurrentTime < timeThreshold) {
                CurrentTime += Time.deltaTime;
            }
        }

        public override bool IsFinished => !IsRunning;

        public override void Reset() {
            CurrentTime = 0;
        }
        
        public void Reset(int newTicksPerSecond) {
            CalculateTimeThreshold(newTicksPerSecond);
            Reset();
        }
        
        void CalculateTimeThreshold(int ticksPerSecond) {
            TicksPerSecond = ticksPerSecond;
            timeThreshold = 1f / TicksPerSecond;
        }
    }
}