using Unity.Cinemachine;
using UnityEngine;

namespace MemoFramework.ThreeC.Cinemachine
{
    /// <summary>
    /// 把 3C 摄像机目标绑定到 CinemachineCamera，并把主相机 Transform 注入相机相对移动 Motor。
    /// </summary>
    public class MemoCinemachineCameraBinder3C : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private MemoCameraTarget3C _cameraTarget;
        [SerializeField] private MemoCameraRelativeMotor3C _cameraRelativeMotor;
        [SerializeField] private Camera _unityCamera;
        [SerializeField] private bool _bindOnStart = true;
        [SerializeField] private bool _setCameraTransformOnMotor = true;
        [SerializeField] private bool _disableCameraWhenInactive;
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority;

        /// <summary>
        /// 当前绑定的 CinemachineCamera。
        /// </summary>
        public CinemachineCamera CinemachineCamera => CacheCinemachineCamera();

        /// <summary>
        /// Unity 启动时执行默认绑定。
        /// </summary>
        protected virtual void Start()
        {
            if (_bindOnStart)
                Bind(_cameraTarget);
        }

        /// <summary>
        /// 绑定 3C 摄像机目标。
        /// </summary>
        /// <param name="cameraTarget">要跟随的 3C 摄像机目标。</param>
        public virtual void Bind(MemoCameraTarget3C cameraTarget)
        {
            _cameraTarget = cameraTarget;
            CinemachineCamera cinemachineCamera = CacheCinemachineCamera();
            if (cinemachineCamera == null || _cameraTarget == null)
                return;

            cinemachineCamera.Follow = _cameraTarget.FollowTarget;
            cinemachineCamera.LookAt = _cameraTarget.LookAtTarget;

            if (_setCameraTransformOnMotor)
                BindCameraRelativeMotor();
        }

        /// <summary>
        /// 设置 Cinemachine 优先级。
        /// </summary>
        /// <param name="priority">新的优先级。</param>
        public void SetPriority(int priority)
        {
            CinemachineCamera cinemachineCamera = CacheCinemachineCamera();
            if (cinemachineCamera != null)
                cinemachineCamera.Priority = priority;
        }

        /// <summary>
        /// 设置该摄像机是否作为当前活动摄像机候选。
        /// </summary>
        /// <param name="active">是否激活。</param>
        public virtual void SetCameraActive(bool active)
        {
            CinemachineCamera cinemachineCamera = CacheCinemachineCamera();
            if (cinemachineCamera == null)
                return;

            cinemachineCamera.Priority = active ? _activePriority : _inactivePriority;
            if (_disableCameraWhenInactive)
                cinemachineCamera.enabled = active;
        }

        /// <summary>
        /// 把 Unity 相机 Transform 注入相机相对移动 Motor。
        /// </summary>
        private void BindCameraRelativeMotor()
        {
            if (_cameraRelativeMotor == null && _cameraTarget != null)
                _cameraRelativeMotor = _cameraTarget.GetComponentInParent<MemoCameraRelativeMotor3C>();

            if (_cameraRelativeMotor == null)
                return;

            Camera unityCamera = ResolveUnityCamera();
            if (unityCamera != null)
                _cameraRelativeMotor.SetCameraTransform(unityCamera.transform);
        }

        /// <summary>
        /// 缓存同物体上的 CinemachineCamera。
        /// </summary>
        /// <returns>当前 CinemachineCamera。</returns>
        private CinemachineCamera CacheCinemachineCamera()
        {
            if (_cinemachineCamera == null)
                _cinemachineCamera = GetComponent<CinemachineCamera>();
            return _cinemachineCamera;
        }

        /// <summary>
        /// 获取配置相机，未配置时使用主相机。
        /// </summary>
        /// <returns>Unity 相机。</returns>
        private Camera ResolveUnityCamera()
        {
            if (_unityCamera != null)
                return _unityCamera;

            _unityCamera = Camera.main;
            return _unityCamera;
        }
    }
}
