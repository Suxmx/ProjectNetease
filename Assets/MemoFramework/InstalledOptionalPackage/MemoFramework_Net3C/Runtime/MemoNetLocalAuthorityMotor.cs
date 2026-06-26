using FishNet.Connection;
using FishNet.Component.Transforming;
using FishNet.Object;
using MemoFramework.ThreeC;
using UnityEngine;

namespace MemoFramework.Net3C
{
    /// <summary>
    /// 客户端权威移动适配层，负责按 FishNet ownership 开关本地 3C Motor。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class MemoNetLocalAuthorityMotor : NetworkBehaviour
    {
        [SerializeField] private MemoCharacterMotor3C _motor;
        [SerializeField] private bool _disableMotorOnNonOwner = true;
        [SerializeField] private bool _disableCharacterControllerOnNonOwner = true;
        private bool _missingMotorWarningLogged;

        /// <summary>
        /// 当前由网络适配层控制的非联网 3C Motor。
        /// </summary>
        public MemoCharacterMotor3C Motor => CacheMotor();

        /// <summary>
        /// 当前客户端是否拥有该网络对象的本地控制权。
        /// </summary>
        public bool HasLocalControl => IsOwner;

        /// <summary>
        /// 客户端初始化后按 ownership 配置本地 Motor。
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyAuthorityState(IsOwner);
        }

        /// <summary>
        /// 客户端停止时恢复 Motor，避免对象池或重新生成保留远端禁用状态。
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            RestoreMotorState();
        }

        /// <summary>
        /// ownership 变化时重新配置本地 Motor。
        /// </summary>
        /// <param name="prevOwner">变化前的拥有者连接。</param>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyAuthorityState(IsOwner);
        }

        /// <summary>
        /// 按 ownership 开关本地 Motor 和 CharacterController。
        /// </summary>
        /// <param name="hasAuthority">当前客户端是否拥有控制权。</param>
        protected virtual void ApplyAuthorityState(bool hasAuthority)
        {
            MemoCharacterMotor3C motor = CacheMotor();
            if (motor == null)
            {
                LogMissingMotorWarning();
                return;
            }

            bool motorEnabled = hasAuthority || !_disableMotorOnNonOwner;
            bool controllerEnabled = hasAuthority || !_disableCharacterControllerOnNonOwner;
            motor.SetControlEnabled(motorEnabled);
            motor.SetCharacterControllerEnabled(controllerEnabled);
        }

        /// <summary>
        /// 恢复 Motor 状态。
        /// </summary>
        protected virtual void RestoreMotorState()
        {
            MemoCharacterMotor3C motor = CacheMotor();
            if (motor == null)
                return;

            motor.SetControlEnabled(true);
            motor.SetCharacterControllerEnabled(true);
        }

        /// <summary>
        /// 缓存同物体上的 3C Motor。
        /// </summary>
        /// <returns>当前 3C Motor。</returns>
        private MemoCharacterMotor3C CacheMotor()
        {
            if (_motor == null)
                _motor = GetComponent<MemoCharacterMotor3C>();
            return _motor;
        }

        /// <summary>
        /// 只输出一次缺少基础 3C Motor 的装配警告。
        /// </summary>
        private void LogMissingMotorWarning()
        {
            if (_missingMotorWarningLogged)
                return;

            Debug.LogWarning($"{nameof(MemoNetLocalAuthorityMotor)} requires a {nameof(MemoCharacterMotor3C)} on the same GameObject.", this);
            _missingMotorWarningLogged = true;
        }
    }
}
