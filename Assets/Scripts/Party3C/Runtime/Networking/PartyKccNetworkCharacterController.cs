using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using KinematicCharacterController;
using Unity.Cinemachine;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// 基于 FishNet 所有权控制 Party3C 角色的本地输入与 KCC 仿真。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyKccNetworkCharacterController : NetworkBehaviour
    {
        #region Serialized Fields

        [SerializeField] private PartyKccCharacterController _character;
        [SerializeField] private PartyKccInputDriver _inputDriver;
        [SerializeField] private KinematicCharacterMotor _motor;
        [SerializeField] private bool _disableRemoteMotorSimulation = true;
        [SerializeField] private bool _disableInputBeforeOwnershipReady = true;

        [Header("Local Camera")]
        [SerializeField] private bool _bindLocalCinemachineCameras = true;
        [SerializeField] private bool _autoFindLocalCinemachineCameras = true;
        [SerializeField] private bool _bindCameraLookAtTarget = true;
        [SerializeField] private List<CinemachineCamera> _localCinemachineCameras = new();

        #endregion

        #region Properties

        public bool HasLocalAuthority => IsOwner;

        #endregion

        #region Setup

        /// <summary>
        /// 初始化引用，并在所有权确认前按配置关闭本地输入。
        /// </summary>
        private void Awake()
        {
            ResolveReferences();

            if (_disableInputBeforeOwnershipReady)
                ApplyLocalAuthority(false);
        }

        /// <summary>
        /// 设置由编辑器工具或自定义生成器创建的联网角色引用。
        /// </summary>
        public void ConfigureReferences(PartyKccCharacterController character, PartyKccInputDriver inputDriver, KinematicCharacterMotor motor)
        {
            _character = character;
            _inputDriver = inputDriver;
            _motor = motor;
        }

        /// <summary>
        /// 在没有显式引用时查找同物体上的角色移动组件。
        /// </summary>
        private void ResolveReferences()
        {
            if (_character == null)
                _character = GetComponent<PartyKccCharacterController>();

            if (_inputDriver == null)
                _inputDriver = GetComponent<PartyKccInputDriver>();

            if (_motor == null)
                _motor = GetComponent<KinematicCharacterMotor>();
        }

        #endregion

        #region FishNet

        /// <summary>
        /// 客户端启动时按当前所有权刷新本地控制权限。
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyLocalAuthority(IsOwner);
        }

        /// <summary>
        /// 客户端所有权变化时刷新输入和 KCC 本地仿真状态。
        /// </summary>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyLocalAuthority(IsOwner);
        }

        /// <summary>
        /// 客户端停止时关闭本地输入与本地 KCC 仿真。
        /// </summary>
        public override void OnStopClient()
        {
            ApplyLocalAuthority(false);
            base.OnStopClient();
        }

        #endregion

        #region Authority

        /// <summary>
        /// 手动按当前 FishNet 所有权刷新本地控制权限。
        /// </summary>
        public void RefreshAuthority()
        {
            ApplyLocalAuthority(IsOwner);
        }

        /// <summary>
        /// 应用本地权威开关：拥有者采集输入并运行 KCC，远端代理只接收同步结果。
        /// </summary>
        private void ApplyLocalAuthority(bool hasAuthority)
        {
            if (_inputDriver != null)
                _inputDriver.SetInputEnabled(hasAuthority);

            if (!hasAuthority && _character != null)
                _character.ClearInputs();

            if (hasAuthority)
                BindLocalCameraTargets();

            if (_disableRemoteMotorSimulation && _motor != null)
                _motor.enabled = hasAuthority;
        }

        #endregion

        #region Camera

        /// <summary>
        /// 将本地启用的 Cinemachine 相机目标绑定到当前拥有者角色的跟随点。
        /// </summary>
        public void BindLocalCameraTargets()
        {
            if (!_bindLocalCinemachineCameras || _character == null || _character.CameraFollowPoint == null)
                return;

            Transform followPoint = _character.CameraFollowPoint;

            if (_autoFindLocalCinemachineCameras)
            {
                CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                    BindCameraTarget(cameras[i], followPoint);
            }

            for (int i = 0; i < _localCinemachineCameras.Count; i++)
                BindCameraTarget(_localCinemachineCameras[i], followPoint);
        }

        /// <summary>
        /// 给单个 Cinemachine 相机设置跟随目标，并按配置设置注视目标。
        /// </summary>
        private void BindCameraTarget(CinemachineCamera camera, Transform followPoint)
        {
            if (camera == null || !camera.isActiveAndEnabled)
                return;

            camera.Target.TrackingTarget = followPoint;

            if (_bindCameraLookAtTarget)
            {
                camera.Target.LookAtTarget = followPoint;
                camera.Target.CustomLookAtTarget = true;
            }
            else
            {
                camera.Target.LookAtTarget = null;
                camera.Target.CustomLookAtTarget = false;
            }
        }

        #endregion
    }
}
