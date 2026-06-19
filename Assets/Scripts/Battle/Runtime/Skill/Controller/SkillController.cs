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
    /// MonoBehaviour，由 <see cref="Player"/> 在 Replicate 回调中驱动。
    /// </summary>
    public sealed class SkillController : MonoBehaviour
    {
        /// <summary>技能槽配置：槽位编号 + 编译后的 .bytes 二进制。</summary>
        [Serializable]
        public struct SkillSlot
        {
            public byte Slot;
            public TextAsset SkillBinary;
        }

        [SerializeField] private SkillSlot[] _skillSlots = Array.Empty<SkillSlot>();
        [SerializeField] private SkillRuntimeServices _services;

        private SkillDefinition _activeSkill;
        private CombatState _combatState;
        private AttributeSet _attributeSet;
        private Player _player;
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

        // --- 伤害组运行时状态 ---
        private readonly Dictionary<byte, byte> _groupMaxHits = new();
        private readonly Dictionary<byte, Dictionary<IBattleDamageTarget, int>> _groupHitCounts = new();

        public bool IsActive => _isActive;
        public int ActiveSkillId => _activeSkill != null ? _activeSkill.SkillId : 0;
        public CombatState CombatState => _combatState;
        public AttributeSet AttributeSet => _attributeSet;
        public SkillRuntimeServices Services => ResolveServices();

        /// <summary>当前技能 CD 进度（0=刚开始，1=结束）。无技能时返回 0。</summary>
        public float GetCooldownProgress(uint currentTick)
        {
            if (!_isActive || _activeSkill == null || _activeSkill.LengthTicks <= 0)
                return 0f;

            int elapsed = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            return Mathf.Clamp01((float)elapsed / _activeSkill.LengthTicks);
        }

        /// <summary>当前技能的已过 tick 和总 tick。无技能时返回 (0, 0)。</summary>
        public (int current, int total) GetCooldownTicks(uint currentTick)
        {
            if (!_isActive || _activeSkill == null)
                return (0, 0);

            int elapsed = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            return (elapsed, _activeSkill.LengthTicks);
        }

        private void Awake()
        {
            _combatState = GetComponent<CombatState>();
            _attributeSet = GetComponent<AttributeSet>();
            _player = GetComponent<Player>();
            ResolveServices();
            SkillExecutorRegistry.Init();
        }

        private void OnValidate()
        {
            _skillCacheBuilt = false;
        }

        /// <summary>
        /// 技能 tick 统一入口。由 Player 在 Replicate 回调中调用一次，
        /// 内部按 canAct/IsServer/IsClient 分发到三个域。
        /// </summary>
        public void TickReplicate(SkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            bool canAct = _combatState == null || _combatState.CanAct;
            if (!canAct)
                return;

            TickClientPrediction(command, aimDirection, currentTick, state, delta);
            if (_player.IsServerStarted)
                TickServerOnly(command, aimDirection, currentTick, state);
            if (_player.IsClientStarted)
                TickClientOnly(command, aimDirection, currentTick, state, delta);
        }

        /// <summary>
        /// ClientPrediction 域 tick 调度。处理输入、推进技能时间。
        /// 在客户端和服务端同步运行，含 replay tick，参与预测回滚。
        /// </summary>
        public void TickClientPrediction(SkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            // --- 处理技能输入 ---
            if (command.Type == SkillCommandType.Press)
                TryStartSkill(command, currentTick);
            else if (command.Type == SkillCommandType.Release && _isActive)
                _phase = 2;
            else if (command.Type == SkillCommandType.Cancel && _isActive)
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

            TickDomain(command, aimDirection, currentTick, elapsedTicks, delta, state, SkillNodeExecutionDomain.ClientPrediction, _activeClientPredictionNodeIds);
        }

        /// <summary>
        /// ClientOnly 域 tick 调度。只在客户端执行（含 Host 客户端身份），跳过 replay tick。
        /// 用于纯表现节点（特效/音效/动画）。
        /// </summary>
        public void TickClientOnly(SkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            if (!_isActive || _activeSkill == null)
                return;

            // --- 跳过 replay tick，纯表现不需要回放 ---
            if (state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            TickDomain(command, aimDirection, currentTick, elapsedTicks, delta, state, SkillNodeExecutionDomain.ClientOnly, _activeClientOnlyNodeIds);
        }

        /// <summary>
        /// ServerOnly 域 tick 调度。只在服务器真实 tick 执行，跳过 replay tick。
        /// 用于服务器权威节点（伤害/属性修改）。
        /// </summary>
        public void TickServerOnly(SkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state)
        {
            if (!_isActive || _activeSkill == null)
                return;

            // --- 跳过回放 tick，只在真实 tick 执行 ---
            if (!state.ContainsTicked() || state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            TickDomain(command, aimDirection, currentTick, elapsedTicks, (float)_player.TimeManager.TickDelta, state, SkillNodeExecutionDomain.ServerOnly, _activeServerOnlyNodeIds);
        }

        /// <summary>
        /// 单 domain 的状态跟踪调度。根据 active 状态变化触发 OnStart/OnTick/OnEnd。
        /// </summary>
        private void TickDomain(SkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, SkillNodeExecutionDomain domain, HashSet<int> activeNodeIds)
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
                    ExecuteNode(command, aimDirection, currentTick, elapsedTicks, delta, state, node, SkillNodeLifecyclePhase.Start);
                }
                else if (isActive && wasActive)
                {
                    // --- 区间内：OnTick ---
                    ExecuteNode(command, aimDirection, currentTick, elapsedTicks, delta, state, node, SkillNodeLifecyclePhase.Tick);
                }
                else if (!isActive && wasActive)
                {
                    // --- 离开区间：OnEnd ---
                    activeNodeIds.Remove(nodeId);
                    ExecuteNode(command, aimDirection, currentTick, elapsedTicks, delta, state, node, SkillNodeLifecyclePhase.End);
                }
            }
        }

        /// <summary>采集技能运行时状态用于 reconcile。</summary>
        public SkillReconcileState CaptureState(uint currentTick)
        {
            return new SkillReconcileState
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
        public void ApplyState(SkillReconcileState state)
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
        private void ExecuteNode(SkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, SkillRuntimeNode node, SkillNodeLifecyclePhase phase)
        {
            if (!SkillExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
            {
                if (_missingExecutorKeys.Add(node.ClipId))
                    Debug.LogError($"[BattleSkill] Missing executor for clip id {node.ClipId}.");
                return;
            }

            SkillExecutionContext context = new(_player, _player.Motor, this, _combatState, _attributeSet, ResolveServices(), _activeSkill, command, node, aimDirection, currentTick, elapsedTicks, delta, state, phase);
            executor.Execute(context);
        }

        /// <summary>按指令启动技能，记录序列号和起始 tick。</summary>
        private void TryStartSkill(SkillCommand command, uint currentTick)
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
            BuildDamageGroups();
        }

        /// <summary>停止当前技能，对所有 active 节点触发 OnEnd，然后清空状态。</summary>
        private void StopSkill()
        {
            // --- 对所有仍在 active 的节点触发 OnEnd ---
            if (_activeSkill != null && _player != null)
                StopAllActiveNodes();

            _activeSkill = null;
            _activeSequenceId = 0;
            _startTick = 0;
            _phase = 0;
            _isActive = false;
            ClearActiveNodeIds();
            ClearDamageGroups();
        }

        /// <summary>对三个 domain 的所有 active 节点触发 OnEnd（技能提前停止时）。</summary>
        private void StopAllActiveNodes()
        {
            int elapsedTicks = 0;
            uint currentTick = 0;
            float delta = _player != null ? (float)_player.TimeManager.TickDelta : 0f;
            ReplicateState state = ReplicateState.Invalid;
            SkillCommand command = SkillCommand.None;
            Vector3 aim = _player != null && _player.Motor != null ? _player.Motor.AimDirection : Vector3.forward;

            StopDomainNodes(command, aim, currentTick, elapsedTicks, delta, state, _activeClientPredictionNodeIds);
            StopDomainNodes(command, aim, currentTick, elapsedTicks, delta, state, _activeClientOnlyNodeIds);
            StopDomainNodes(command, aim, currentTick, elapsedTicks, delta, state, _activeServerOnlyNodeIds);
        }

        /// <summary>对单个 domain 的 active 节点集合触发 OnEnd 并清空。</summary>
        private void StopDomainNodes(SkillCommand command, Vector3 aimDirection, uint currentTick, int elapsedTicks, float delta, ReplicateState state, HashSet<int> activeNodeIds)
        {
            if (activeNodeIds.Count == 0 || _activeSkill == null)
                return;

            SkillRuntimeNode[] nodes = _activeSkill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!activeNodeIds.Contains(node.NodeId))
                    continue;

                ExecuteNode(command, aimDirection, currentTick, elapsedTicks, delta, state, node, SkillNodeLifecyclePhase.End);
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

        /// <summary>
        /// 扫描当前技能的 SpecialDatas，构建伤害组配置（groupId → maxHits）。
        /// 在技能启动时调用一次。
        /// </summary>
        private void BuildDamageGroups()
        {
            _groupMaxHits.Clear();
            _groupHitCounts.Clear();

            if (_activeSkill?.SpecialDatas == null)
                return;

            foreach (SkillRuntimeSpecialData entry in _activeSkill.SpecialDatas)
            {
                if (entry.SpecialDataTypeId != SkillGeneratedIds.DamageGroupData)
                    continue;

                if (SkillGeneratedSerializationServices.Runtime.TryReadSpecialData<RuntimeDamageGroupData>(_activeSkill, entry, out RuntimeDamageGroupData data))
                    _groupMaxHits[data.GroupId] = data.MaxHitsPerTarget;
            }
        }

        /// <summary>清空伤害组运行时状态。</summary>
        private void ClearDamageGroups()
        {
            _groupMaxHits.Clear();
            _groupHitCounts.Clear();
        }

        /// <summary>
        /// 检查并累加伤害组命中次数。超限返回 false（跳过该目标的本次伤害）。
        /// groupId=0 表示无组，直接放行。
        /// </summary>
        public bool TryConsumeGroupHit(byte groupId, IBattleDamageTarget target)
        {
            if (groupId == 0)
                return true;

            if (!_groupMaxHits.TryGetValue(groupId, out byte maxHits))
                return true;

            if (!_groupHitCounts.TryGetValue(groupId, out Dictionary<IBattleDamageTarget, int> counts))
            {
                counts = new Dictionary<IBattleDamageTarget, int>();
                _groupHitCounts[groupId] = counts;
            }

            counts.TryGetValue(target, out int current);
            if (current >= maxHits)
                return false;

            counts[target] = current + 1;
            return true;
        }

        /// <summary>解析服务依赖（Inspector 指定 → 父级查找 → 场景查找）。</summary>
        private SkillRuntimeServices ResolveServices()
        {
            if (_services != null)
                return _services;

            _services = GetComponentInParent<SkillRuntimeServices>();
            if (_services == null)
                _services = FindFirstObjectByType<SkillRuntimeServices>();

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
