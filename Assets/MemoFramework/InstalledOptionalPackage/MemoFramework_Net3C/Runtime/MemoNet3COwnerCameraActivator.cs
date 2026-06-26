using FishNet.Connection;
using FishNet.Object;
using MemoFramework.ThreeC.Cinemachine;
using UnityEngine;

namespace MemoFramework.Net3C
{
    /// <summary>
    /// 按 FishNet ownership 激活本地玩家的 3C Cinemachine 摄像机。
    /// </summary>
    public class MemoNet3COwnerCameraActivator : NetworkBehaviour
    {
        [SerializeField] private MemoCinemachineCameraBinder3C[] _cameraBinders;
        [SerializeField] private bool _disableOnNonOwner = true;

        /// <summary>
        /// 客户端启动后按 ownership 设置摄像机。
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyCameraState(IsOwner);
        }

        /// <summary>
        /// ownership 变化时刷新摄像机状态。
        /// </summary>
        /// <param name="prevOwner">变化前的拥有者连接。</param>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyCameraState(IsOwner);
        }

        /// <summary>
        /// 客户端停止时关闭该对象关联摄像机。
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();
            ApplyCameraState(false);
        }

        /// <summary>
        /// 按本地 ownership 设置所有绑定摄像机。
        /// </summary>
        /// <param name="hasAuthority">当前客户端是否拥有该玩家。</param>
        protected virtual void ApplyCameraState(bool hasAuthority)
        {
            MemoCinemachineCameraBinder3C[] cameraBinders = ResolveCameraBinders();
            for (int i = 0; i < cameraBinders.Length; i++)
            {
                if (cameraBinders[i] == null)
                    continue;

                bool active = hasAuthority || !_disableOnNonOwner;
                cameraBinders[i].SetCameraActive(active);
            }
        }

        /// <summary>
        /// 获取配置或子物体中的摄像机绑定器。
        /// </summary>
        /// <returns>摄像机绑定器集合。</returns>
        private MemoCinemachineCameraBinder3C[] ResolveCameraBinders()
        {
            if (_cameraBinders == null || _cameraBinders.Length == 0)
                _cameraBinders = GetComponentsInChildren<MemoCinemachineCameraBinder3C>(true);
            return _cameraBinders;
        }
    }
}
