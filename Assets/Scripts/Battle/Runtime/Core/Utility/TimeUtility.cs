using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Battle 时间换算工具。提供 tick 与秒之间的通用换算方法，
    /// 供运行时各系统（buff 持续时间、技能冷却等）复用。
    /// </summary>
    public static class TimeUtility
    {
        /// <summary>将秒数换算为 tick 数（向上取整）。</summary>
        /// <param name="tickDelta">单个 tick 的时长（秒）。</param>
        /// <param name="seconds">要换算的秒数。</param>
        public static uint SecondsToTicks(float tickDelta, float seconds)
        {
            if (tickDelta <= 0f)
                return 1u;

            return (uint)Mathf.CeilToInt(seconds / Mathf.Max(0.0001f, tickDelta));
        }
    }
}
