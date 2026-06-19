using System;

namespace MemoFramework
{
    public abstract class MFEventArgs : EventArgs, IReference
    {
        protected MFEventArgs()
        {
        }

        /// <summary>
        /// 清理引用。
        /// </summary>
        public abstract void Clear();
    }
}