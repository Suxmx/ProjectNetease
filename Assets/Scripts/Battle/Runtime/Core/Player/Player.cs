using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 玩家主类。FishNet 预测系统的唯一入口，
    /// 组合 <see cref="Motor"/>（移动）和 <see cref="SkillController"/>（技能）两个子组件。
    /// 负责 [Replicate]/[Reconcile] 生命周期分发，自身不处理移动或技能逻辑。
    /// 挂载于角色 prefab，Inspector 暴露移动参数供 Motor 构造使用。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Player : TickNetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnSpeed = 720f;

        private Motor _motor;
        private SkillController _skillController;
        private BattlePlayerInput _input;
        private CombatState _combatState;

        /// <summary>Motor 实例，供 SkillController/Executor 通过 context 访问。</summary>
        public Motor Motor => _motor;

        private void Awake()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            _combatState = GetComponent<CombatState>();
            _input = GetComponent<BattlePlayerInput>();
            _skillController = GetComponent<SkillController>();

            _motor = new Motor(rigidbody, _combatState, GetComponent<AttributeSet>(), _moveSpeed, _turnSpeed);
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

        /// <summary>采集 Motor + SkillController 的 reconcile 快照并发送回滚。</summary>
        public override void CreateReconcile()
        {
            MotorReconcileState motorState = _motor.CaptureState();
            SkillReconcileState skillState = _skillController != null
                ? _skillController.CaptureState(TimeManager.LocalTick)
                : default;

            ReconcileData data = new(motorState, skillState, _combatState != null && _combatState.IsDead);
            PerformReconcile(data);
        }

        /// <summary>从输入组件构建当前 tick 的 Replicate 数据。</summary>
        private ReplicateData BuildReplicateData()
        {
            if (!IsOwner || _input == null)
                return default;

            Vector2 move = _input.ReadMove();
            Vector3 aim = _input.ReadAimDirection(transform.position, _motor.AimDirection);
            SkillCommand command = _input.ConsumeSkillCommand(aim, TimeManager.LocalTick);

            // --- 按 slot 查找技能 ID ---
            if (command.Type != SkillCommandType.None && _skillController != null && command.SkillId == 0)
            {
                Hoshino.SkillDefinition skill = _skillController.FindSkillBySlot(command.Slot);
                if (skill != null)
                    command.SkillId = skill.SkillId;
            }

            return new ReplicateData(move, aim, command);
        }

        /// <summary>
        /// FishNet Replicate 回调。分发到 Motor（移动）和 SkillController（技能）。
        /// </summary>
        [Replicate]
        private void PerformReplicate(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;

            // --- Motor 移动 ---
            _motor.TickReplicate(data, state, delta);

            // --- 技能调度 ---
            _skillController?.TickReplicate(data.SkillCommand, _motor.AimDirection, data.GetTick(), state, delta);
        }

        /// <summary>FishNet Reconcile 回调。分发到 Motor 和 SkillController。</summary>
        [Reconcile]
        private void PerformReconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            _motor.ApplyState(data.MotorState);
            _skillController?.ApplyState(data.SkillState);
        }
    }
}
