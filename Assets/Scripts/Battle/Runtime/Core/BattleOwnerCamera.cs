using FishNet.Object;
using Unity.Cinemachine;
using UnityEngine;

namespace Battle
{
    public sealed class BattleOwnerCamera : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private CinemachineCamera _virtualCamera;

        public Transform CameraTarget => _cameraTarget != null ? _cameraTarget : transform;

        private void Reset()
        {
            _cameraTarget = transform;
        }

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
                    Debug.LogWarning($"Battle owner camera is missing a CinemachineCamera reference on {name}.");
                return;
            }

            _virtualCamera.gameObject.SetActive(active);
            _virtualCamera.enabled = active;
        }
    }
}
