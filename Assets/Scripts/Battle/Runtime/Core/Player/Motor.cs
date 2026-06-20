using FishNet.Object.Prediction;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 玩家移动逻辑。普通 C# 类，由 <see cref="Player"/> 持有实例。
    /// 只负责移动物理（速度/位移/传送/旋转），不感知技能调度和网络生命周期。
    /// 技能 Executor 通过 <see cref="SkillExecutionContext.Motor"/> 调用
    /// <see cref="AddPredictedVelocity"/>/<see cref="AddPredictedDisplacement"/>/<see cref="TeleportPredicted"/> 施加位移。
    /// </summary>
    public sealed class Motor
    {
        private readonly Rigidbody _rigidbody;
        private readonly PredictionRigidbody _predictionRigidbody = new();
        private readonly CombatState _combatState;
        private readonly AttributeSet _attributeSet;
        private readonly float _moveSpeed;
        private readonly float _turnSpeed;

        private Vector3 _aimDirection = Vector3.forward;
        private Vector3 _predictedVelocity;
        private Vector3 _predictedDisplacement;
        private bool _hasPendingTeleport;
        private Vector3 _pendingTeleportPosition;

        /// <summary>当前朝向（水平面归一化）。</summary>
        public Vector3 AimDirection => _aimDirection;

        /// <summary>刚体位置，供 Executor 读取。</summary>
        public Vector3 Position => _rigidbody.position;

        /// <summary>刚体 transform，供 Executor 做空间换算。</summary>
        public Transform Transform => _rigidbody.transform;

        public Motor(Rigidbody rigidbody, CombatState combatState, AttributeSet attributeSet, float moveSpeed, float turnSpeed)
        {
            _rigidbody = rigidbody;
            _combatState = combatState;
            _attributeSet = attributeSet;
            _moveSpeed = moveSpeed;
            _turnSpeed = turnSpeed;

            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            _predictionRigidbody.Initialize(rigidbody);
        }

        /// <summary>
        /// tick 前半段：更新朝向 + 清零上一 tick 的预测修改器。
        /// 由 Player 在技能调度前调用，确保技能 Executor 累加的是本 tick 的位移。
        /// </summary>
        public void BeginTick(ReplicateData data)
        {
            // --- 更新朝向 ---
            Vector3 aim = data.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
                _aimDirection = aim.normalized;

            // --- 重置上一 tick 的预测修改器（技能 Executor 在此之后累加本 tick 的） ---
            ResetPredictedModifiers();
        }

        /// <summary>
        /// tick 后半段：计算最终速度（输入 + 技能预测速度）+ 传送/位移/旋转 + 物理模拟。
        /// 由 Player 在技能调度后调用，此时技能 Executor 已累加完本 tick 的预测位移。
        /// </summary>
        public void EndTick(ReplicateData data, float delta)
        {
            bool canAct = _combatState == null || _combatState.CanAct;

            // --- 计算移动速度（输入 + 属性加成 + 技能预测速度） ---
            Vector3 desiredVelocity = Vector3.zero;
            if (canAct)
            {
                Vector2 input = data.MoveInput.sqrMagnitude > 1f ? data.MoveInput.normalized : data.MoveInput;
                float moveMultiplier = _attributeSet != null ? _attributeSet.MoveSpeedMultiplier : 1f;
                desiredVelocity = new Vector3(input.x, 0f, input.y) * (_moveSpeed * moveMultiplier);
            }

            desiredVelocity += _predictedVelocity;

            // --- 应用传送或位移 ---
            if (_hasPendingTeleport)
                _predictionRigidbody.MovePosition(_pendingTeleportPosition);
            else if (_predictedDisplacement.sqrMagnitude > 0.000001f)
                _predictionRigidbody.MovePosition(_rigidbody.position + _predictedDisplacement);

            // --- 朝向旋转 ---
            if (_aimDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_aimDirection, Vector3.up);
                Quaternion nextRotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _turnSpeed * delta);
                _predictionRigidbody.MoveRotation(nextRotation);
            }

            // --- 写入速度并模拟物理 ---
            Vector3 velocity = GetVelocity(_rigidbody);
            velocity.x = desiredVelocity.x;
            velocity.z = desiredVelocity.z;
            if (!canAct)
                velocity.y = 0f;

            _predictionRigidbody.Velocity(velocity);
            _predictionRigidbody.Simulate();
        }
        public void AddPredictedVelocity(Vector3 value)
        {
            _predictedVelocity += value;
        }

        /// <summary>累加预测位移（由技能 Executor 调用）。</summary>
        public void AddPredictedDisplacement(Vector3 value)
        {
            _predictedDisplacement += value;
        }

        /// <summary>设置待执行的传送位置（由技能 Executor 调用）。</summary>
        public void TeleportPredicted(Vector3 position)
        {
            _hasPendingTeleport = true;
            _pendingTeleportPosition = position;
        }

        /// <summary>采集刚体和朝向状态用于 reconcile。</summary>
        public MotorReconcileState CaptureState()
        {
            return new MotorReconcileState
            {
                Rigidbody = _predictionRigidbody,
                AimDirection = _aimDirection
            };
        }

        /// <summary>从 reconcile 状态恢复刚体和朝向。</summary>
        public void ApplyState(MotorReconcileState state)
        {
            _predictionRigidbody.Reconcile(state.Rigidbody);
            _aimDirection = state.AimDirection.sqrMagnitude > 0.0001f ? state.AimDirection.normalized : _aimDirection;
        }

        /// <summary>清零本 tick 的预测速度/位移/传送缓存。</summary>
        private void ResetPredictedModifiers()
        {
            _predictedVelocity = Vector3.zero;
            _predictedDisplacement = Vector3.zero;
            _hasPendingTeleport = false;
            _pendingTeleportPosition = Vector3.zero;
        }

        /// <summary>兼容 Unity 6 的 linearVelocity 重命名。</summary>
        private static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
    }
}
