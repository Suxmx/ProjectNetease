using System;

namespace MemoFramework.Exceptions
{
	[Serializable]
	public class StateMachineException : Exception
	{
		public StateMachineException(string message) : base(message) { }
	}
}
