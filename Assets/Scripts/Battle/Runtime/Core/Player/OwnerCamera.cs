using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 本地玩家相机控制。仅在本机拥有者上激活 Cinemachine 虚拟相机，
    /// 其他客户端的相机组件保持关闭，避免多玩家场景相机冲突。
    /// </summary>
    public sealed class OwnerCamera : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private CinemachineCamera _virtualCamera;

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

        /// <summary>激活或关闭虚拟相机 GameObject 和组件。</summary>
        private void SetCameraActive(bool active)
        {
            if (_virtualCamera == null)
            {
                if (active)
                    Debug.LogWarning($"Battle owner camera is missing a CinemachineCamera reference on {name}.");
                return;
            }

            _virtualCamera.gameObject.SetActive(active);
            _virtualCamera.enabled = active;
        }
    }
}
