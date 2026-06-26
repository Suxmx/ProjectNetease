using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// TopDown 3C Motor，直接把输入映射到世界 XZ 平面。
    /// </summary>
    public class MemoTopDownMotor3C : MemoCharacterMotor3C
    {
        /// <summary>
        /// 将二维输入转换为世界空间 XZ 平面移动方向。
        /// </summary>
        /// <param name="input">二维移动输入。</param>
        /// <returns>世界空间移动方向。</returns>
        protected override Vector3 GetWorldMoveDirection(Vector2 input)
        {
            return new Vector3(input.x, 0f, input.y);
        }
    }
}
