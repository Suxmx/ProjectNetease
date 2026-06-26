using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 基于相机 yaw 的 3C Motor 基类，供第一人称和第三人称移动复用。
    /// </summary>
    public abstract class MemoCameraRelativeMotor3C : MemoCharacterMotor3C
    {
        [SerializeField] private Transform _cameraTransform;

        /// <summary>
        /// 当前用于决定移动方向的相机。
        /// </summary>
        protected Transform CameraTransform => ResolveCameraTransform();

        /// <summary>
        /// 设置用于决定移动方向的相机。
        /// </summary>
        /// <param name="cameraTransform">相机 Transform。</param>
        public void SetCameraTransform(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        /// <summary>
        /// 将二维输入按相机 yaw 转换为世界空间方向。
        /// </summary>
        /// <param name="input">二维移动输入。</param>
        /// <returns>世界空间移动方向。</returns>
        protected override Vector3 GetWorldMoveDirection(Vector2 input)
        {
            Transform cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
                return new Vector3(input.x, 0f, input.y);

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right = right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;

            return right * input.x + forward * input.y;
        }

        /// <summary>
        /// 获取已配置相机，未配置时尝试使用主相机。
        /// </summary>
        /// <returns>相机 Transform。</returns>
        private Transform ResolveCameraTransform()
        {
            if (_cameraTransform != null)
                return _cameraTransform;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                _cameraTransform = mainCamera.transform;

            return _cameraTransform;
        }
    }
}
