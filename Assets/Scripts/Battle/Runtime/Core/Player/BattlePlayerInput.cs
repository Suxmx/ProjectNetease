using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Battle
{
    /// <summary>
    /// 玩家输入采集。读取键盘移动、鼠标瞄准和技能按键，
    /// 产生 <see cref="SkillCommand"/> 供 Motor 的 Replicate 数据使用。
    /// 非网络组件，仅在本地客户端运行。
    /// </summary>
    public sealed class BattlePlayerInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _aimPlaneMask = ~0;
        [SerializeField] private float _fallbackAimDistance = 30f;

        private uint _nextSequenceId = 1;
        private SkillCommand _pendingCommand;
        private readonly bool[] _heldSlots = new bool[8];
        private readonly ushort[] _chargeTicks = new ushort[8];

        /// <summary>读取 WASD 移动输入并归一化。</summary>
        public Vector2 ReadMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed)
                move.y += 1f;
            if (keyboard.sKey.isPressed)
                move.y -= 1f;
            if (keyboard.dKey.isPressed)
                move.x += 1f;
            if (keyboard.aKey.isPressed)
                move.x -= 1f;

            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        /// <summary>通过鼠标射线投射到地面平面，计算瞄准方向。</summary>
        public Vector3 ReadAimDirection(Vector3 origin, Vector3 fallback)
        {
            Camera camera = ResolveCamera();
            Mouse mouse = Mouse.current;
            if (camera == null || mouse == null)
                return FlattenOrFallback(fallback);

            // --- 先尝试射线命中物理表面 ---
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, _fallbackAimDistance * 4f, _aimPlaneMask, QueryTriggerInteraction.Ignore))
                return FlattenOrFallback(hit.point - origin);

            // --- 命中失败则投射到角色所在的水平面 ---
            Plane ground = new(Vector3.up, origin);
            if (ground.Raycast(ray, out float distance))
                return FlattenOrFallback(ray.GetPoint(distance) - origin);

            return FlattenOrFallback(ray.direction);
        }

        /// <summary>采集并清空当前待发的技能指令。</summary>
        public SkillCommand ConsumeSkillCommand(Vector3 aimDirection, uint inputTick)
        {
            UpdateKeyboardSkillCommands(aimDirection, inputTick);

            SkillCommand result = _pendingCommand;
            _pendingCommand = SkillCommand.None;
            return result;
        }

        /// <summary>轮询各技能槽按键状态。</summary>
        private void UpdateKeyboardSkillCommands(Vector3 aimDirection, uint inputTick)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            UpdateSlot(0, keyboard.digit1Key, aimDirection, inputTick);
            UpdateSlot(1, keyboard.digit2Key, aimDirection, inputTick);
            UpdateSlot(2, keyboard.digit3Key, aimDirection, inputTick);
            UpdateSlot(3, keyboard.digit4Key, aimDirection, inputTick);
            UpdateSlot(4, keyboard.qKey, aimDirection, inputTick);
            UpdateSlot(5, keyboard.eKey, aimDirection, inputTick);
        }

        /// <summary>处理单个技能槽的 Press/Hold/Release 状态转换。</summary>
        private void UpdateSlot(byte slot, KeyControl key, Vector3 aimDirection, uint inputTick)
        {
            if (key == null)
                return;

            if (key.wasPressedThisFrame && !_heldSlots[slot])
            {
                _heldSlots[slot] = true;
                _chargeTicks[slot] = 0;
                QueueCommand(SkillCommandType.Press, slot, aimDirection, inputTick);
            }
            else if (key.wasReleasedThisFrame)
            {
                QueueCommand(SkillCommandType.Release, slot, aimDirection, inputTick);
                _heldSlots[slot] = false;
                _chargeTicks[slot] = 0;
            }
            else if (_heldSlots[slot] && _pendingCommand.Type == SkillCommandType.None)
            {
                _chargeTicks[slot] = (ushort)Mathf.Min(ushort.MaxValue, _chargeTicks[slot] + 1);
                QueueCommand(SkillCommandType.Hold, slot, aimDirection, inputTick);
            }
        }

        /// <summary>构造技能指令并暂存，等待 ConsumeSkillCommand 取走。</summary>
        private void QueueCommand(SkillCommandType type, byte slot, Vector3 aimDirection, uint inputTick)
        {
            _pendingCommand = new SkillCommand
            {
                Type = type,
                Slot = slot,
                SequenceId = _nextSequenceId++,
                InputTick = inputTick,
                AimDirection = aimDirection,
                TargetPoint = transform.position + aimDirection * _fallbackAimDistance,
                ChargeTicks = _chargeTicks[slot]
            };
        }

        /// <summary>解析瞄准用的相机（优先 Inspector 指定，回退 Camera.main）。</summary>
        private Camera ResolveCamera()
        {
            if (_camera != null)
                return _camera;

            _camera = Camera.main;
            return _camera;
        }

        /// <summary>将向量拍平到水平面并归一化，无效时回退 forward。</summary>
        private static Vector3 FlattenOrFallback(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
        }
    }
}
