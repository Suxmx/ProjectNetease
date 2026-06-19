using UnityEngine;

namespace MemoFramework {
    /// <summary>
    /// Timer that counts up from zero to infinity.  Great for measuring durations.
    /// </summary>
    public class StopwatchTimer : Timer {
        public StopwatchTimer(bool start = false) : base(0, start)
        {
            if(start) Restart();
        }

        public override void Tick() {
            if (IsRunning) {
                CurrentTime += Time.deltaTime;
            }
        }

        public override bool IsFinished => false;
    }
}