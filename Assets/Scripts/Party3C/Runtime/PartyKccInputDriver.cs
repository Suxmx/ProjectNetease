using MemoFramework.Extension;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// 将 MemoFramework 输入数据转换为派对 KCC 角色使用的世界空间移动意图。
    /// </summary>
    public sealed class PartyKccInputDriver : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private PartyKccCharacterController _character;
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _autoFindCamera = true;
        [SerializeField] private bool _inputEnabled = true;
        [SerializeField] private bool _driveCameraLook = true;

        #endregion

        #region Runtime State

        private Vector2 _lastMouseScreenPosition;
        private bool _hasLastMouseScreenPosition;

        #endregion

        #region Setup

        /// <summary>
        /// 设置由编辑器工具或自定义生成器创建的角色引用。
        /// </summary>
        public void ConfigureReferences(PartyKccCharacterController character)
        {
            _character = character;
        }

        /// <summary>
        /// 在没有显式引用时查找同物体上的角色控制器。
        /// </summary>
        private void Awake()
        {
            if (_character == null)
                _character = GetComponent<PartyKccCharacterController>();
        }

        #endregion

        #region Input

        /// <summary>
        /// 启用或禁用本地输入采集，后续可由网络所有权控制。
        /// </summary>
        public void SetInputEnabled(bool inputEnabled)
        {
            if (_inputEnabled == inputEnabled)
                return;

            _inputEnabled = inputEnabled;
            _hasLastMouseScreenPosition = false;

            if (!_inputEnabled && _character != null)
                _character.ClearInputs();
        }

        /// <summary>
        /// 每帧读取 MF 输入，并向 KCC 控制器提交一份移动意图快照。
        /// </summary>
        private void Update()
        {
            if (!_inputEnabled || _character == null)
                return;

            if (_autoFindCamera && _camera == null)
                _camera = Camera.main;

            if (_driveCameraLook)
                _character.AddCameraLookInput(ReadMouseDelta());

            PartyKccCharacterInputs inputs = new()
            {
                MoveWorld = BuildCameraRelativeMove(InputData.MoveInput),
                RunHeld = InputData.HasEvent(InputEvent.Run),
                JumpPressed = InputData.HasEventStart(InputEvent.Jump),
                DashPressed = InputData.HasEventStart(InputEvent.Dash)
            };
            inputs.LookWorld = ResolveLookWorld(inputs.MoveWorld);

            _character.SetInputs(inputs);
        }

        /// <summary>
        /// 从 MF 缓存的鼠标数据计算当前帧鼠标移动量。
        /// </summary>
        private Vector2 ReadMouseDelta()
        {
            if (InputData.MouseDelta.sqrMagnitude > 0f)
            {
                _lastMouseScreenPosition = InputData.MouseScreenPosition;
                _hasLastMouseScreenPosition = true;
                return InputData.MouseDelta;
            }

            Vector2 currentMousePosition = InputData.MouseScreenPosition;
            if (!_hasLastMouseScreenPosition)
            {
                _lastMouseScreenPosition = currentMousePosition;
                _hasLastMouseScreenPosition = true;
                return Vector2.zero;
            }

            Vector2 delta = currentMousePosition - _lastMouseScreenPosition;
            _lastMouseScreenPosition = currentMousePosition;
            return delta;
        }

        #endregion

        #region Camera Space

        /// <summary>
        /// 从移动方向选择角色朝向；没有移动输入时交给控制器保持当前朝向。
        /// </summary>
        private Vector3 ResolveLookWorld(Vector3 moveWorld)
        {
            if (moveWorld.sqrMagnitude > 0.0001f)
                return moveWorld;

            return Vector3.zero;
        }

        /// <summary>
        /// 将二维移动输入转换为角色平面上的世界空间向量。
        /// </summary>
        private Vector3 BuildCameraRelativeMove(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <= 0f)
                return Vector3.zero;

            Vector3 up = _character.CharacterUp;
            Vector3 forward = BuildCameraForward();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 move = (forward * moveInput.y) + (right * moveInput.x);
            return Vector3.ClampMagnitude(Vector3.ProjectOnPlane(move, up), 1f);
        }

        /// <summary>
        /// 获取可用的平面摄像机前向，并兼容俯视相机的退化方向。
        /// </summary>
        private Vector3 BuildCameraForward()
        {
            Vector3 up = _character != null ? _character.CharacterUp : Vector3.up;
            Transform cameraTransform = _camera != null ? _camera.transform : null;
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, up) : Vector3.zero;

            if (forward.sqrMagnitude <= 0.0001f && cameraTransform != null)
                forward = Vector3.ProjectOnPlane(cameraTransform.up, up);

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.ProjectOnPlane(transform.forward, up);

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        #endregion
    }
}
