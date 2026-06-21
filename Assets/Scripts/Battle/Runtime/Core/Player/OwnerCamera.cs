using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 本地玩家相机控制。仅在本机拥有者上激活 Cinemachine 虚拟相机，
    /// 其他客户端的相机组件保持关闭，避免多玩家场景相机冲突。
    /// 激活时将虚拟相机脱离 Player 层级并固定俯视朝向，
    /// 避免 Player 转向时通过父子关系拖动相机产生水平 yaw 旋转。
    /// </summary>
    public sealed class OwnerCamera : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private CinemachineCamera _virtualCamera;
        [SerializeField] private Vector3 _fixedEulerAngles = new(55f, 0f, 0f);

        public Transform CameraTarget => _cameraTarget != null ? _cameraTarget : transform;

        protected override void Reset()
        {
            base.Reset();
            _cameraTarget = transform;
        }

        /// <summary>客户端启动时，仅拥有者激活相机。</summary>
        public override void OnStartClient()
        {
            SetCameraActive(IsOwner);
        }

        /// <summary>客户端停止时关闭相机。</summary>
        public override void OnStopClient()
        {
            SetCameraActive(false);
        }

        private void OnDestroy()
        {
            // 虚拟相机 detach 后不再随 Player 销毁，需手动清理避免场景残留
            if (_virtualCamera != null && Application.isPlaying)
            {
                Transform camTransform = _virtualCamera.transform;
                if (camTransform != null && camTransform.parent == null)
                    Destroy(_virtualCamera.gameObject);
            }
        }

        /// <summary>激活或关闭虚拟相机 GameObject 和组件。激活时 detach 到场景根并固定俯视朝向。</summary>
        private void SetCameraActive(bool active)
        {
            if (_virtualCamera == null)
            {
                if (active)
                    Debug.LogWarning($"Battle owner camera is missing a CinemachineCamera reference on {name}.");
                return;
            }

            if (active)
            {
                // Player 的 Rigidbody 仅冻结 X/Z 旋转，Y 轴随 Motor.MoveRotation 转向，
                // 相机作为子物体会被带动产生水平 yaw 旋转。detach 后 rotation 不再受父级影响，
                // CinemachineFollow(WorldSpace) 仍以 CameraTarget 世界位置带阻尼跟随。
                Transform camTransform = _virtualCamera.transform;
                if (camTransform.parent != null)
                {
                    camTransform.SetParent(null, true);
                    camTransform.rotation = Quaternion.Euler(_fixedEulerAngles);
                }
                _virtualCamera.gameObject.SetActive(true);
                _virtualCamera.enabled = true;
            }
            else
            {
                _virtualCamera.enabled = false;
                _virtualCamera.gameObject.SetActive(false);
            }
        }
    }
}
