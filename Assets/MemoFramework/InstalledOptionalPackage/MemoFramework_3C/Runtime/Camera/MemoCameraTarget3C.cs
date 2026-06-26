using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 3C 角色暴露给摄像机系统使用的跟随与注视目标。
    /// </summary>
    public class MemoCameraTarget3C : MonoBehaviour
    {
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Transform _lookAtTarget;
        [SerializeField] private Transform _headTarget;

        /// <summary>
        /// 摄像机跟随目标。
        /// </summary>
        public Transform FollowTarget => _followTarget != null ? _followTarget : transform;

        /// <summary>
        /// 摄像机注视目标。
        /// </summary>
        public Transform LookAtTarget => _lookAtTarget != null ? _lookAtTarget : FollowTarget;

        /// <summary>
        /// 第一人称相机或头部挂点。
        /// </summary>
        public Transform HeadTarget => _headTarget != null ? _headTarget : LookAtTarget;

        /// <summary>
        /// 运行时设置摄像机目标。
        /// </summary>
        /// <param name="followTarget">跟随目标。</param>
        /// <param name="lookAtTarget">注视目标。</param>
        /// <param name="headTarget">头部目标。</param>
        public void SetTargets(Transform followTarget, Transform lookAtTarget, Transform headTarget)
        {
            _followTarget = followTarget;
            _lookAtTarget = lookAtTarget;
            _headTarget = headTarget;
        }
    }
}
