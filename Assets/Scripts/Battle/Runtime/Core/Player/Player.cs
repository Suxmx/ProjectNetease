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
        [Tooltip("勾选后该实例输出技能诊断日志到文件（见 SkillDiagLogger），用于排查突进斩网络预测问题")]
        [SerializeField] private bool _debugLog;

        private Motor _motor;
        private SkillController _skillController;
        private BattlePlayerInput _input;
        private CombatState _combatState;

        /// <summary>Motor 实例，供 SkillController/Executor 通过 context 访问。</summary>
        public Motor Motor => _motor;

        /// <summary>是否输出技能诊断日志，供 SkillController 共享开关。</summary>
        public bool DebugLog => _debugLog;

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
            Vector3 aim = _input.GetCachedAim();
            if (aim.sqrMagnitude < 0.0001f)
                aim = _motor.AimDirection;

            SkillCommand command = _input.ConsumeSkillCommand(TimeManager.LocalTick);

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

            // --- 诊断 D3：记录本 tick 前 rigidbody 位置，供旁观者跳变检测 ---
            Vector3 preRb = _debugLog ? _motor.Position : default;

            // --- Motor 前半段：朝向 + 清零上 tick 预测修改器 ---
            _motor.BeginTick(data);

            // --- 技能调度：Executor 在此期间累加本 tick 的预测速度/位移 ---
            _skillController?.TickReplicate(data.SkillCommand, _motor.AimDirection, data.GetTick(), state, delta);

            // --- Motor 后半段：算最终速度 + Simulate ---
            _motor.EndTick(data, delta);

            // --- 诊断 D3：旁观者技能活跃时记录位置与单 tick 跳变 ---
            if (_debugLog && !IsOwner && IsClientStarted)
            {
                bool skillActive = _skillController != null && _skillController.IsActive;
                if (skillActive)
                {
                    Vector3 rb = _motor.Position;
                    Vector3 vis = FindVisualPos();
                    Vector3 moved = rb - preRb;
                    SkillDiagLogger.Log($"[SPEC] role={SkillDiagLogger.RoleOf(this)} tick={data.GetTick()} rb={rb:F2} vis={vis:F2} moved={moved.magnitude:F3}");
                    if (moved.magnitude > 0.5f)
                        SkillDiagLogger.Log($"[SPEC JUMP] tick={data.GetTick()} jump={moved.magnitude:F3} from {preRb:F2} to {rb:F2}");
                }
            }

            // --- 诊断 D4：Owner 技能活跃时记录 rb 位置变化和速度 ---
            if (_debugLog && IsOwner)
            {
                bool skillActive = _skillController != null && _skillController.IsActive;
                if (skillActive)
                {
                    Vector3 rb = _motor.Position;
                    Vector3 moved = rb - preRb;
                    SkillDiagLogger.Log($"[OWNER] role={SkillDiagLogger.RoleOf(this)} tick={data.GetTick()} rb={rb:F2} moved={moved.magnitude:F3} state={state}");
                }
            }
        }

        /// <summary>FishNet Reconcile 回调。分发到 Motor 和 SkillController。</summary>
        [Reconcile]
        private void PerformReconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            // --- 诊断 D2：量化 reconcile 修正量，确认 Owner 结束拖拽来源 ---
            if (_debugLog)
            {
                Vector3 before = _motor.Position;
                SkillDiagLogger.Log($"[RECONCILE] role={SkillDiagLogger.RoleOf(this)} tick={TimeManager.LocalTick} before={before:F2}");
                _motor.ApplyState(data.MotorState);
                Vector3 after = _motor.Position;
                float delta = Vector3.Distance(before, after);
                SkillDiagLogger.Log($"[RECONCILE] role={SkillDiagLogger.RoleOf(this)} tick={TimeManager.LocalTick} after={after:F2} delta={delta:F3}");
            }
            else
            {
                _motor.ApplyState(data.MotorState);
            }
            _skillController?.ApplyState(data.SkillState, data.GetTick());
        }

        /// <summary>查找 Visual 子级位置用于诊断（detach 后为 root 直接子级，detach 前在 Presentation 下）。</summary>
        private Vector3 FindVisualPos()
        {
            Transform v = transform.Find("Visual");
            if (v == null)
            {
                Transform p = transform.Find("Presentation");
                if (p != null) v = p.Find("Visual");
            }
            return v != null ? v.position : transform.position;
        }
    }
}
