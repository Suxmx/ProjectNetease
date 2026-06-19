using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 玩家移动控制器。FishNet 预测系统的核心载体，
    /// 驱动移动、旋转、技能调度和 reconcile。
    /// 挂载于角色 prefab，聚合 Input/CombatState/AttributeSet/SkillController 等组件。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BattlePlayerMotor : TickNetworkBehaviour
    {
        /// <summary>
        /// Reconcile 快照数据。包含刚体状态、技能状态、朝向和死亡标记，
        /// 由服务端产生并回滚客户端预测。
        /// </summary>
        public struct BattleReconcileData : IReconcileData
        {
            public PredictionRigidbody Rigidbody;
            public BattleSkillReconcileState SkillState;
            public Vector3 AimDirection;
            public bool IsDead;

            private uint _tick;

            public BattleReconcileData(PredictionRigidbody rigidbody, BattleSkillReconcileState skillState, Vector3 aimDirection, bool isDead)
            {
                Rigidbody = rigidbody;
                SkillState = skillState;
                AimDirection = aimDirection;
                IsDead = isDead;
                _tick = 0;
            }

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
            public void Dispose() { }
        }

        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnSpeed = 720f;

        private readonly PredictionRigidbody _predictionRigidbody = new();
        private Rigidbody _rigidbody;
        private BattlePlayerInput _input;
        private BattleSkillController _skillController;
        private BattleCombatState _combatState;
        private BattleAttributeSet _attributeSet;
        private Vector3 _aimDirection = Vector3.forward;
        private Vector3 _predictedVelocity;
        private Vector3 _predictedDisplacement;
        private bool _hasPendingTeleport;
        private Vector3 _pendingTeleportPosition;

        public Vector3 AimDirection => _aimDirection;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _input = GetComponent<BattlePlayerInput>();
            _skillController = GetComponent<BattleSkillController>();
            _combatState = GetComponent<BattleCombatState>();
            _attributeSet = GetComponent<BattleAttributeSet>();

            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _predictionRigidbody.Initialize(_rigidbody);
        }

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildReplicateData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        /// <summary>采集技能 reconcile 快照并发送回滚。</summary>
        public override void CreateReconcile()
        {
            BattleSkillReconcileState skillState = _skillController != null
                ? _skillController.CaptureState(TimeManager.LocalTick)
                : default;

            BattleReconcileData data = new(_predictionRigidbody, skillState, _aimDirection, _combatState != null && _combatState.IsDead);
            PerformReconcile(data);
        }

        /// <summary>累加预测速度（由技能 Executor 调用）。</summary>
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

        /// <summary>从输入组件构建当前 tick 的 Replicate 数据。</summary>
        private BattleReplicateData BuildReplicateData()
        {
            if (!IsOwner || _input == null)
                return default;

            Vector2 move = _input.ReadMove();
            Vector3 aim = _input.ReadAimDirection(transform.position, _aimDirection);
            BattleSkillCommand command = _input.ConsumeSkillCommand(aim, TimeManager.LocalTick);

            // --- 按 slot 查找技能 ID ---
            if (command.Type != BattleSkillCommandType.None && _skillController != null && command.SkillId == 0)
            {
                Hoshino.SkillDefinition skill = _skillController.FindSkillBySlot(command.Slot);
                if (skill != null)
                    command.SkillId = skill.SkillId;
            }

            return new BattleReplicateData(move, aim, command);
        }

        /// <summary>
        /// FishNet Replicate 回调。每 tick 在客户端和服务端同步执行，
        /// 驱动技能调度、移动、旋转，并通过 PredictionRigidbody 模拟物理。
        /// </summary>
        [Replicate]
        private void PerformReplicate(BattleReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;
            bool canAct = _combatState == null || _combatState.CanAct;

            // --- 更新朝向 ---
            Vector3 aim = data.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
                _aimDirection = aim.normalized;

            // --- 重置上一 tick 的预测修改器 ---
            ResetPredictedModifiers();

            // --- 执行技能节点（ClientPrediction 所有身份 + ServerOnly 服务器 + ClientOnly 客户端） ---
            if (canAct)
                _skillController?.TickClientPrediction(this, data.SkillCommand, _aimDirection, data.GetTick(), state, delta);
            if (canAct && IsServerStarted)
                _skillController?.TickServerOnly(this, data.SkillCommand, _aimDirection, data.GetTick(), state);
            if (canAct && IsClientStarted)
                _skillController?.TickClientOnly(this, data.SkillCommand, _aimDirection, data.GetTick(), state, delta);

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

        /// <summary>FishNet Reconcile 回调。回滚刚体状态、朝向和技能状态。</summary>
        [Reconcile]
        private void PerformReconcile(BattleReconcileData data, Channel channel = Channel.Unreliable)
        {
            _predictionRigidbody.Reconcile(data.Rigidbody);
            _aimDirection = data.AimDirection.sqrMagnitude > 0.0001f ? data.AimDirection.normalized : _aimDirection;
            _skillController?.ApplyState(data.SkillState);
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
