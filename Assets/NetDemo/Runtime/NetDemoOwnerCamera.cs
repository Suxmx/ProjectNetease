using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace NetDemo
{
    public sealed class NetDemoOwnerCamera : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private CinemachineCamera _virtualCamera;

        public Transform CameraTarget => _cameraTarget;

        public override void OnStartClient()
        {
            SetCameraActive(IsOwner);
        }

        public override void OnStopClient()
        {
            SetCameraActive(false);
        }

        private void SetCameraActive(bool active)
        {
            if (_virtualCamera == null)
            {
                if (active)
                    Debug.LogWarning($"NetDemo owner camera is missing a CinemachineCamera reference on {name}.");
                return;
            }

            _virtualCamera.gameObject.SetActive(active);
            _virtualCamera.enabled = active;
        }
    }
}
