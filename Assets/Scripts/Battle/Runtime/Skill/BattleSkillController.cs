using System;
using System.Collections.Generic;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    public sealed class BattleSkillController : MonoBehaviour
    {
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

        public void TickPredicted(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
        {
            if (command.Type == BattleSkillCommandType.Press)
                TryStartSkill(command, currentTick);
            else if (command.Type == BattleSkillCommandType.Release && _isActive)
                _phase = 2;
            else if (command.Type == BattleSkillCommandType.Cancel && _isActive)
                StopSkill();

            if (!_isActive || _activeSkill == null)
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            if (elapsedTicks > _activeSkill.LengthTicks)
            {
                StopSkill();
                return;
            }

            foreach (SkillRuntimeNode node in _activeSkill.Nodes)
            {
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;
                if (domain != SkillNodeExecutionDomain.Predicted || !node.IsActiveAt(elapsedTicks))
                    continue;

                ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, delta, state, node);
            }
        }

        public void TickServerAuthority(BattlePlayerMotor motor, BattleSkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state)
        {
            if (!_isActive || _activeSkill == null)
                return;
            if (!state.ContainsTicked() || state.ContainsReplayed())
                return;

            int elapsedTicks = currentTick >= _startTick ? (int)(currentTick - _startTick) : 0;
            foreach (SkillRuntimeNode node in _activeSkill.Nodes)
            {
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;
                if (domain != SkillNodeExecutionDomain.LagCompensatedQuery && domain != SkillNodeExecutionDomain.ServerAuthority)
                    continue;
                if (elapsedTicks != node.StartTick)
                    continue;

                int executionKey = BuildServerExecutionKey(_activeSequenceId, node.NodeId);
                if (!_executedServerNodeKeys.Add(executionKey))
                    continue;

                ExecuteNode(motor, command, aimDirection, currentTick, elapsedTicks, (float)motor.TimeManager.TickDelta, state, node);
            }
        }

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

        public SkillDefinition FindSkillBySlot(byte slot)
        {
            EnsureSkillCache();
            return _skillsBySlot.TryGetValue(slot, out SkillDefinition skill) ? skill : null;
        }

        public SkillDefinition FindSkillById(int skillId)
        {
            EnsureSkillCache();
            return _skillsById.TryGetValue(skillId, out SkillDefinition skill) ? skill : null;
        }

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

        private void StopSkill()
        {
            _activeSkill = null;
            _activeSequenceId = 0;
            _startTick = 0;
            _phase = 0;
            _isActive = false;
            _executedServerNodeKeys.Clear();
        }

        private BattleSkillRuntimeServices ResolveServices()
        {
            if (_services != null)
                return _services;

            _services = GetComponentInParent<BattleSkillRuntimeServices>();
            if (_services == null)
                _services = FindFirstObjectByType<BattleSkillRuntimeServices>();

            return _services;
        }

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

        private static int BuildServerExecutionKey(uint sequenceId, int nodeId)
        {
            unchecked
            {
                return ((int)sequenceId * 397) ^ nodeId;
            }
        }
    }
}
