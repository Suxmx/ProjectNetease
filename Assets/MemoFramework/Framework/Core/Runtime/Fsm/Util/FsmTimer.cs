using UnityEngine;

namespace MemoFramework
{
	/// <summary>
	/// Default timer that calculates the elapsed time based on Time.time.
	/// </summary>
	public class FsmTimer : IFsmTimer
	{
		public float startTime;
		public virtual float Elapsed => Time.time - startTime;

		public virtual void Reset()
		{
			startTime = Time.time;
		}

		public static bool operator >(FsmTimer timer, float duration)
			=> timer.Elapsed > duration;

		public static bool operator <(FsmTimer timer, float duration)
			=> timer.Elapsed < duration;

		public static bool operator >=(FsmTimer timer, float duration)
			=> timer.Elapsed >= duration;

		public static bool operator <=(FsmTimer timer, float duration)
			=> timer.Elapsed <= duration;
	}
}
