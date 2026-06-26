using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 3C 可选包共享输入数据，避免把相机 Look 输入写入 MemoFramework 核心输入层。
    /// </summary>
    public static class MemoThreeCInputData
    {
        /// <summary>
        /// 当前帧相机观察输入，通常由鼠标增量或手柄右摇杆写入。
        /// </summary>
        public static Vector2 LookInput { get; private set; }

        /// <summary>
        /// 写入当前帧相机观察输入。
        /// </summary>
        /// <param name="lookInput">二维观察输入。</param>
        public static void SetLookInput(Vector2 lookInput)
        {
            LookInput = lookInput;
        }

        /// <summary>
        /// 清空当前帧相机观察输入。
        /// </summary>
        public static void ClearLookInput()
        {
            LookInput = Vector2.zero;
        }
    }
}
