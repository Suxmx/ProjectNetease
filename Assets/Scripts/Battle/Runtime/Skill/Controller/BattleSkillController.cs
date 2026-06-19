using System;
using System.Collections.Generic;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能调度控制器。管理技能槽、技能启动/停止、节点 tick 调度。
    /// 三个 domain 分别调度：ClientPrediction（所有身份+replay）、ClientOnly（客户端跳过replay）、ServerOnly（服务器跳过replay）。
    /// 节点生命周期 OnStart/OnTick/OnEnd 通过状态跟踪 HashSet 实现。
    /// 非 NetworkBehaviour，由 <see cref="BattlePlayerMotor"/> 在 Replicate 回调中驱动。
    /// </summary>
    public sealed class BattleSkillController : MonoBehaviour
    {
        /// <summary>技能槽配置：槽位编号 + 编译后的 .bytes 二进制。</summary>
        [Serializable]
        public struct SkillSlot
        {
            public byte Slot;
            public TextAsset SkillBinary;
        }

        [SerializeField] private SkillSlot[] _skillSlots = Array.Empty<SkillSlot>();
        [SerializeField] private BattleSkillRuntimeServices _services;

        private SkillDefinition _activeSkill;
        private BattleCombatState _combatState;
        private BattleAttributeSet _attributeSet;
        private uint _activeSequenceId;
        private uint _startTick;
        private byte _phase;
        private bool _isActive;
        private readonly HashSet<int> _activeClientPredictionNodeIds = new();
        private readonly HashSet<int> _activeClientOnlyNodeIds = new();
        private readonly HashSet<int> _activeServerOnlyNodeIds = new();
        private readonly HashSet<uint> _missingExecutorKeys = new();
        private readonly Dictionary<byte, SkillDefinition> _skillsBySlot = new();
        private readonly Dictionary<int, SkillDefinition> _skillsById = new();
        private bool _skillCacheBuilt;

        public bool IsActive => _isActive;
        public int ActiveSkillId => _activeSkill != null ? _activeSkill.SkillId : 0;
        public BattleCombatState CombatState => _combatState;
        public BattleAttributeSet AttributeSet => _attributeSet;
        public BattleSkillRuntimeServices Services => ResolveServices();

        private void Awake()
        {
            _combatState = GetComponent<BattleCombatState>();
            _attributeSet = GetComponent<BattleAttributeSet>();
            ResolveServices();
            BattleSkillNodeExecutorRegistry.Init();
        }

        private void OnValidate()
        {
            _skillCacheBuilt = false;
        }

        /// <summary>
        /// ClientPrediction 域 tick 调度。处理输入、推进技能时间。
        /// 在客户端和服务端同步运行，含 replay tick，参与预测回滚。
        /// </summary>
        public void TickClientPrediction(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            // --- 处理技能输入 ---
            if (command.Type == BattleSkillCommandType.Press)
                TryStartSkill(command, currentTick);
            else if (command.Type == BattleSkillCommandType.Release && _isActive)
                _phase = 2;
            else if (command.Type == BattleSkillCommandType.Cancel && _isActive)
                StopSkill();

            if (!_isActive || _activeSkill == null)
                return;

            // --- 计算已过 tick 数，超时则停止 ---
            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            if (elapsedTicks > _activeSkill.LengthTicks)
            {
                StopSkill();
                return;
            }

            TickDomain(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, SkillNodeExecutionDomain.ClientPrediction, _activeClientPredictionNodeIds);
        }

        /// <summary>
        /// ClientOnly 域 tick 调度。只在客户端执行（含 Host 客户端身份），跳过 replay tick。
        /// 用于纯表现节点（特效/音效/动画）。
        /// </summary>
        public void TickClientOnly(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            if (!_isActive || _activeSkill == null)
                return;

            // --- 跳过 replay tick，纯表现不需要回放 ---
            if (state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            TickDomain(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, SkillNodeExecutionDomain.ClientOnly, _activeClientOnlyNodeIds);
        }

        /// <summary>
        /// ServerOnly 域 tick 调度。只在服务器真实 tick 执行，跳过 replay tick。
        /// 用于服务器权威节点（伤害/属性修改）。
        /// </summary>
        public void TickServerOnly(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state)
        {
            if (!_isActive || _activeSkill == null)
                return;

            // --- 跳过回放 tick，只在真实 tick 执行 ---
            if (!state.ContainsTicked() || state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            TickDomain(motor, command, aimDirection, currentTick, elapsedTicks, (float)motor.TimeManager.TickDelta, state, SkillNodeExecutionDomain.ServerOnly, _activeServerOnlyNodeIds);
        }

        /// <summary>
        /// 单 domain 的状态跟踪调度。根据 active 状态变化触发 OnStart/OnTick/OnEnd。
        /// </summary>
        private void TickDomain(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, SkillNodeExecutionDomain domain, HashSet<int> activeNodeIds)
        {
            SkillRuntimeNode[] nodes = _activeSkill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!SkillGeneratedExecutorMetas.TryGetDomain(node.ClipId, out SkillNodeExecutionDomain nodeDomain))
                    continue;
                if (nodeDomain != domain)
                    continue;

                int nodeId = node.NodeId;
                bool isActive = node.IsActiveAt(elapsedTicks);
                bool wasActive = activeNodeIds.Contains(nodeId);

                if (isActive && !wasActive)
                {
                    // --- 进入区间：OnStart ---
                    activeNodeIds.Add(nodeId);
                    ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node, BattleSkillNodeLifecyclePhase.Start);
                }
                else if (isActive && wasActive)
                {
                    // --- 区间内：OnTick ---
                    ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node, BattleSkillNodeLifecyclePhase.Tick);
                }
                else if (!isActive && wasActive)
                {
                    // --- 离开区间：OnEnd ---
                    activeNodeIds.Remove(nodeId);
                    ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node, BattleSkillNodeLifecyclePhase.End);
                }
            }
        }

        /// <summary>采集技能运行时状态用于 reconcile。</summary>
        public BattleSkillReconcileState CaptureState(uint currentTick)
        {
            return new BattleSkillReconcileState
            {
                ActiveSkillId = ActiveSkillId,
                ActiveSequenceId = _activeSequenceId,
                StartTick = _startTick,
                ElapsedTicks = _isActive && currentTick >= _startTick ? (ushort)Mathf.Min(ushort.MaxValue, currentTick - _startTick) : (ushort)0,
                Phase = _phase,
                IsActive = _isActive
            };
        }

        /// <summary>从 reconcile 状态恢复技能运行时状态。</summary>
        public void ApplyState(BattleSkillReconcileState state)
        {
            _activeSkill = state.IsActive ? FindSkillById(state.ActiveSkillId) : null;
            _activeSequenceId = state.ActiveSequenceId;
            _startTick = state.StartTick;
            _phase = state.Phase;
            _isActive = state.IsActive && _activeSkill != null;
            if (!_isActive)
                ClearActiveNodeIds();
        }

        /// <summary>按槽位查找技能定义。</summary>
        public SkillDefinition FindSkillBySlot(byte slot)
        {
            EnsureSkillCache();
            return _skillsBySlot.TryGetValue(slot, out SkillDefinition skill) ? skill : null;
        }

        /// <summary>按技能 ID 查找技能定义。</summary>
        public SkillDefinition FindSkillById(int skillId)
        {
            EnsureSkillCache();
            return _skillsById.TryGetValue(skillId, out SkillDefinition skill) ? skill : null;
        }

        /// <summary>查找 Executor 并派发节点执行。</summary>
        private void ExecuteNode(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, SkillRuntimeNode node, BattleSkillNodeLifecyclePhase phase)
        {
            if (!BattleSkillNodeExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
            {
                if (_missingExecutorKeys.Add(node.ClipId))
                    Debug.LogError($"[BattleSkill] Missing executor for clip id {node.ClipId}.");
                return;
            }

            BattleSkillExecutionContext context = new(motor, this, _combatState, _attributeSet, ResolveServices(), _activeSkill, command, node, aimDirection, currentTick, elapsedTicks, delta, state, phase);
            executor.Execute(context);
        }

        /// <summary>按指令启动技能，记录序列号和起始 tick。</summary>
        private void TryStartSkill(BattleSkillCommand command, uint currentTick)
        {
            SkillDefinition skill = command.SkillId != 0 ? FindSkillById(command.SkillId) : FindSkillBySlot(command.Slot);
            if (skill == null)
                return;

            _activeSkill = skill;
            _activeSequenceId = command.SequenceId;
            _startTick = currentTick;
            _phase = 1;
            _isActive = true;
            ClearActiveNodeIds();
        }

        /// <summary>停止当前技能，对所有 active 节点触发 OnEnd，然后清空状态。</summary>
        private void StopSkill()
        {
            // --- 对所有仍在 active 的节点触发 OnEnd ---
            if (_activeSkill != null && _combatState != null)
            {
                BattlePlayerMotor motor = GetComponent<BattlePlayerMotor>();
                StopAllActiveNodes(motor);
            }

            _activeSkill = null;
            _activeSequenceId = 0;
            _startTick = 0;
            _phase = 0;
            _isActive = false;
            ClearActiveNodeIds();
        }

        /// <summary>对三个 domain 的所有 active 节点触发 OnEnd（技能提前停止时）。</summary>
        private void StopAllActiveNodes(BattlePlayerMotor motor)
        {
            SkillRuntimeNode[] nodes = _activeSkill.Nodes;
            int elapsedTicks = 0;
            uint currentTick = 0;
            float delta = motor != null ? (float)motor.TimeManager.TickDelta : 0f;
            ReplicateState state = ReplicateState.Invalid;
            BattleSkillCommand command = BattleSkillCommand.None;
            Vector3 aim = motor != null ? motor.AimDirection : Vector3.forward;

            StopDomainNodes(motor, command, aim, currentTick, elapsedTicks, delta, state, _activeClientPredictionNodeIds);
            StopDomainNodes(motor, command, aim, currentTick, elapsedTicks, delta, state, _activeClientOnlyNodeIds);
            StopDomainNodes(motor, command, aim, currentTick, elapsedTicks, delta, state, _activeServerOnlyNodeIds);
        }

        /// <summary>对单个 domain 的 active 节点集合触发 OnEnd 并清空。</summary>
        private void StopDomainNodes(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, HashSet<int> activeNodeIds)
        {
            if (activeNodeIds.Count == 0 || _activeSkill == null)
                return;

            SkillRuntimeNode[] nodes = _activeSkill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!activeNodeIds.Contains(node.NodeId))
                    continue;

                ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node, BattleSkillNodeLifecyclePhase.End);
            }
            activeNodeIds.Clear();
        }

        /// <summary>清空三个 domain 的 active 节点集合。</summary>
        private void ClearActiveNodeIds()
        {
            _activeClientPredictionNodeIds.Clear();
            _activeClientOnlyNodeIds.Clear();
            _activeServerOnlyNodeIds.Clear();
        }

        /// <summary>解析服务依赖（Inspector 指定 → 父级查找 → 场景查找）。</summary>
        private BattleSkillRuntimeServices ResolveServices()
        {
            if (_services != null)
                return _services;

            _services = GetComponentInParent<BattleSkillRuntimeServices>();
            if (_services == null)
                _services = FindFirstObjectByType<BattleSkillRuntimeServices>();

            return _services;
        }

        /// <summary>首次访问时从 .bytes 反序列化所有技能并缓存。</summary>
        private void EnsureSkillCache()
        {
            if (_skillCacheBuilt)
                return;

            _skillsBySlot.Clear();
            _skillsById.Clear();

            foreach (SkillSlot item in _skillSlots)
            {
                if (item.SkillBinary == null)
                    continue;

                try
                {
                    SkillDefinition skill = SkillDefinition.FromBytes(item.SkillBinary.bytes);
                    if (skill == null)
                        continue;

                    _skillsBySlot[item.Slot] = skill;
                    _skillsById[skill.SkillId] = skill;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BattleSkill] Failed to load compiled skill binary '{item.SkillBinary.name}'.", this);
                    Debug.LogException(ex, this);
                }
            }

            _skillCacheBuilt = true;
        }
    }
}
