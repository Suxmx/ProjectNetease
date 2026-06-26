using UnityEngine;
using UnityEngine.InputSystem;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 从 Unity Input System 读取鼠标和手柄观察输入，并写入 3C 共享输入数据。
    /// </summary>
    public class MemoThreeCLookInputProvider : MonoBehaviour
    {
        [SerializeField] private bool _readMouse = true;
        [SerializeField] private bool _readGamepad = true;
        [SerializeField] private bool _invertY = true;
        [SerializeField] private bool _lockCursorOnEnable;
        [SerializeField] private Vector2 _mouseSensitivity = new Vector2(0.08f, 0.08f);
        [SerializeField] private Vector2 _gamepadSensitivity = new Vector2(180f, 120f);

        /// <summary>
        /// 组件启用时按配置锁定光标。
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!_lockCursorOnEnable)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// 组件禁用时清空相机观察输入。
        /// </summary>
        protected virtual void OnDisable()
        {
            MemoThreeCInputData.ClearLookInput();
        }

        /// <summary>
        /// 每帧读取鼠标和手柄输入。
        /// </summary>
        protected virtual void Update()
        {
            Vector2 lookInput = Vector2.zero;

            // 读取鼠标增量，适合桌面第一/第三人称相机。
            if (_readMouse && Mouse.current != null)
                lookInput += Vector2.Scale(Mouse.current.delta.ReadValue(), _mouseSensitivity);

            // 读取手柄右摇杆，按时间缩放为每帧角度增量。
            if (_readGamepad && Gamepad.current != null)
                lookInput += Vector2.Scale(Gamepad.current.rightStick.ReadValue(), _gamepadSensitivity) * Time.unscaledDeltaTime;

            // 统一处理 Y 轴方向，默认鼠标上移抬头。
            if (_invertY)
                lookInput.y = -lookInput.y;

            MemoThreeCInputData.SetLookInput(lookInput);
        }
    }
}
