using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Battle
{
    /// <summary>
    /// 玩家输入采集。在 Update 里采集按键事件和瞄准方向，
    /// 在网络 tick 里通过 ConsumeSkillCommand 消费。
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

        // --- Update 里缓存，tick 里读取 ---
        private Vector3 _cachedAim;
        private Vector3 _lastAimFallback = Vector3.forward;

        /// <summary>读取 WASD 移动输入并归一化（连续状态，tick 里直接读 isPressed）。</summary>
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

        /// <summary>获取 Update 里缓存的瞄准方向（tick 里调用）。</summary>
        public Vector3 GetCachedAim()
        {
            return _cachedAim;
        }

        /// <summary>采集并清空当前待发的技能指令（tick 回调调用）。</summary>
        public SkillCommand ConsumeSkillCommand(uint inputTick)
        {
            SkillCommand result = _pendingCommand;
            _pendingCommand = SkillCommand.None;

            // --- 没有事件时，检查是否有 hold 状态 ---
            if (result.Type == SkillCommandType.None)
            {
                for (byte slot = 0; slot < 6; slot++)
                {
                    if (_heldSlots[slot])
                    {
                        result = BuildCommand(SkillCommandType.Hold, slot);
                        break;
                    }
                }
            }

            if (result.Type != SkillCommandType.None)
                result.InputTick = inputTick;

            return result;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // --- 缓存瞄准方向 ---
            _cachedAim = ReadAimDirection(transform.position, _lastAimFallback);
            _lastAimFallback = _cachedAim;

            // --- 采集技能按键事件 ---
            UpdateSlot(0, keyboard.digit1Key);
            UpdateSlot(1, keyboard.digit2Key);
            UpdateSlot(2, keyboard.digit3Key);
            UpdateSlot(3, keyboard.digit4Key);
            UpdateSlot(4, keyboard.qKey);
            UpdateSlot(5, keyboard.eKey);
        }

        /// <summary>处理单个技能槽的 Press/Release 事件（在 Update 里调用）。</summary>
        private void UpdateSlot(byte slot, KeyControl key)
        {
            if (key == null)
                return;

            if (key.wasPressedThisFrame && !_heldSlots[slot])
            {
                _heldSlots[slot] = true;
                _pendingCommand = BuildCommand(SkillCommandType.Press, slot);
            }
            else if (key.wasReleasedThisFrame)
            {
                _heldSlots[slot] = false;
                _pendingCommand = BuildCommand(SkillCommandType.Release, slot);
            }
        }

        /// <summary>构造技能指令（用缓存的 aim 方向）。</summary>
        private SkillCommand BuildCommand(SkillCommandType type, byte slot)
        {
            return new SkillCommand
            {
                Type = type,
                Slot = slot,
                SequenceId = _nextSequenceId++,
                AimDirection = _cachedAim,
                TargetPoint = transform.position + _cachedAim * _fallbackAimDistance,
            };
        }

        /// <summary>通过鼠标射线投射到地面平面，计算瞄准方向。</summary>
        private Vector3 ReadAimDirection(Vector3 origin, Vector3 fallback)
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
