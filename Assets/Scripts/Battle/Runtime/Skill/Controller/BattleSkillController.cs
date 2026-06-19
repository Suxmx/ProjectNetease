using System;
using System.Collections.Generic;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能调度控制器。管理技能槽、技能启动/停止、节点 tick 调度。
    /// 预测节点每 tick 在 active 区间内执行；服务端权威节点仅在 StartTick 执行一次。
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
        private readonly HashSet<int> _executedServerNodeKeys = new();
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
        }

        private void OnValidate()
        {
            _skillCacheBuilt = false;
        }

        /// <summary>
        /// 预测阶段 tick 调度。处理输入、推进技能时间、执行 Predicted 域节点。
        /// 在客户端和服务端同步运行，支持预测回滚。
        /// </summary>
        public void TickPredicted(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
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

            // --- 遍历节点，执行 Predicted 域节点（active 区间内每 tick） ---
            foreach (SkillRuntimeNode node in _activeSkill.Nodes)
            {
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;
                if (domain != SkillNodeExecutionDomain.Predicted || !node.IsActiveAt(elapsedTicks))
                    continue;

                ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node);
            }
        }

        /// <summary>
        /// 服务端权威阶段 tick 调度。仅在真实 tick（非 replay）执行
        /// ServerAuthority/LagCompensatedQuery 域节点，每个节点在 StartTick 执行一次。
        /// </summary>
        public void TickServerAuthority(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state)
        {
            if (!_isActive || _activeSkill == null)
                return;

            // --- 跳过回放 tick，只在真实 tick 执行 ---
            if (!state.ContainsTicked() || state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;

            // --- 遍历节点，执行 Server/Lag 域节点（仅 StartTick，去重） ---
            foreach (SkillRuntimeNode node in _activeSkill.Nodes)
            {
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;
                if (domain != SkillNodeExecutionDomain.LagCompensatedQuery && domain != SkillNodeExecutionDomain.ServerAuthority)
                    continue;
                if (elapsedTicks != node.StartTick)
                    continue;

                // --- 用 sequenceId+nodeId 组合 key 防重复执行 ---
                int executionKey = BuildServerExecutionKey(_activeSequenceId, node.NodeId);
                if (!_executedServerNodeKeys.Add(executionKey))
                    continue;

                ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, (float)motor.TimeManager.TickDelta, state, node);
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
                _executedServerNodeKeys.Clear();
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
        private void ExecuteNode(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, SkillRuntimeNode node)
        {
            if (!BattleSkillNodeExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
            {
                if (_missingExecutorKeys.Add(node.ClipId))
                    Debug.LogError($"[BattleSkill] Missing executor for clip id {node.ClipId}.");
                return;
            }

            BattleSkillExecutionContext context = new(motor, this, _combatState, _attributeSet, ResolveServices(), _activeSkill, command, node, aimDirection, currentTick, elapsedTicks, delta, state);
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
            _executedServerNodeKeys.Clear();
        }

        /// <summary>停止当前技能并清空状态。</summary>
        private void StopSkill()
        {
            _activeSkill = null;
            _activeSequenceId = 0;
            _startTick = 0;
            _phase = 0;
            _isActive = false;
            _executedServerNodeKeys.Clear();
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

        /// <summary>构建服务端节点去重 key（sequenceId × 397 ^ nodeId）。</summary>
        private static int BuildServerExecutionKey(uint sequenceId, int nodeId)
        {
            unchecked
            {
                return ((int)sequenceId * 397) ^ nodeId;
            }
        }
    }
}
