using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Battle
{
    public sealed class BattlePlayerInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _aimPlaneMask = ~0;
        [SerializeField] private float _fallbackAimDistance = 30f;

        private uint _nextSequenceId = 1;
        private BattleSkillCommand _pendingCommand;
        private readonly bool[] _heldSlots = new bool[8];
        private readonly ushort[] _chargeTicks = new ushort[8];

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

        public Vector3 ReadAimDirection(Vector3 origin, Vector3 fallback)
        {
            Camera camera = ResolveCamera();
            Mouse mouse = Mouse.current;
            if (camera == null || mouse == null)
                return FlattenOrFallback(fallback);

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, _fallbackAimDistance * 4f, _aimPlaneMask, QueryTriggerInteraction.Ignore))
                return FlattenOrFallback(hit.point - origin);

            Plane ground = new(Vector3.up, origin);
            if (ground.Raycast(ray, out float distance))
                return FlattenOrFallback(ray.GetPoint(distance) - origin);

            return FlattenOrFallback(ray.direction);
        }

        public BattleSkillCommand ConsumeSkillCommand(Vector3 aimDirection, uint inputTick)
        {
            UpdateKeyboardSkillCommands(aimDirection, inputTick);

            BattleSkillCommand result = _pendingCommand;
            _pendingCommand = BattleSkillCommand.None;
            return result;
        }

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

        private void UpdateSlot(byte slot, KeyControl key, Vector3 aimDirection, uint inputTick)
        {
            if (key == null)
                return;

            if (key.wasPressedThisFrame)
            {
                _heldSlots[slot] = true;
                _chargeTicks[slot] = 0;
                QueueCommand(BattleSkillCommandType.Press, slot, aimDirection, inputTick);
            }
            else if (key.wasReleasedThisFrame)
            {
                QueueCommand(BattleSkillCommandType.Release, slot, aimDirection, inputTick);
                _heldSlots[slot] = false;
                _chargeTicks[slot] = 0;
            }
            else if (_heldSlots[slot] && _pendingCommand.Type == BattleSkillCommandType.None)
            {
                _chargeTicks[slot] = (ushort)Mathf.Min(ushort.MaxValue, _chargeTicks[slot] + 1);
                QueueCommand(BattleSkillCommandType.Hold, slot, aimDirection, inputTick);
            }
        }

        private void QueueCommand(BattleSkillCommandType type, byte slot, Vector3 aimDirection, uint inputTick)
        {
            _pendingCommand = new BattleSkillCommand
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

        private Camera ResolveCamera()
        {
            if (_camera != null)
                return _camera;

            _camera = Camera.main;
            return _camera;
        }

        private static Vector3 FlattenOrFallback(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
        }
    }
}
