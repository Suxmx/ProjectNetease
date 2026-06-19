
namespace MemoFramework
{
	public interface IFsmTimer
	{
		float Elapsed
		{
			get;
		}

		void Reset();
	}
}
