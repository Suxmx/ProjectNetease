using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 第一人称 3C Motor，移动方向和角色朝向都参考相机 yaw。
    /// </summary>
    public class MemoFirstPersonMotor3C : MemoCameraRelativeMotor3C
    {
        /// <summary>
        /// 第一人称角色优先朝向相机 yaw，而不是仅在移动时转向。
        /// </summary>
        /// <param name="moveDirection">当前移动方向。</param>
        /// <param name="facingDirection">输出朝向。</param>
        /// <returns>是否有有效朝向。</returns>
        protected override bool TryGetFacingDirection(Vector3 moveDirection, out Vector3 facingDirection)
        {
            Transform cameraTransform = CameraTransform;
            if (cameraTransform == null)
                return base.TryGetFacingDirection(moveDirection, out facingDirection);

            facingDirection = cameraTransform.forward;
            facingDirection.y = 0f;
            return facingDirection.sqrMagnitude > 0.0001f;
        }
    }
}
