using System.Collections.Generic;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能回放测试组件。加载编译后的 .bytes 技能二进制，
    /// 按 SourceTickRate 模拟 tick 推进，镜像 <see cref="BattleSkillController"/> 的调度逻辑
    /// 派发节点到 Executor（当前 Executor 为 log-only 测试态）。
    /// 用于验证编译→加载→反序列化→调度的完整数据链路。
    /// 挂载到场景 GameObject，Inspector 拖入 .bytes，进 PlayMode 自动运行。
    /// </summary>
    public sealed class SkillPlaybackTester : MonoBehaviour
    {
        [SerializeField] private TextAsset _skillBinary;

        private SkillDefinition _skill;
        private float _elapsedSeconds;
        private bool _running;
        private readonly HashSet<int> _activeClientPredictionNodeIds = new();
        private readonly HashSet<int> _activeClientOnlyNodeIds = new();
        private readonly HashSet<int> _activeServerOnlyNodeIds = new();

        private void Start()
        {
            ReloadAndStart();
        }

        /// <summary>重新加载技能二进制并开始回放。可通过 ContextMenu 在运行时重跑。</summary>
        [ContextMenu("Reload & Start")]
        public void ReloadAndStart()
        {
            _activeClientPredictionNodeIds.Clear();
            _activeClientOnlyNodeIds.Clear();
            _activeServerOnlyNodeIds.Clear();
            _elapsedSeconds = 0f;
            _running = false;

            // --- 校验并加载技能二进制 ---
            if (_skillBinary == null || _skillBinary.bytes == null || _skillBinary.bytes.Length == 0)
            {
                Debug.LogError("[SkillPlaybackTester] No SkillBinary assigned.", this);
                return;
            }

            try
            {
                _skill = SkillDefinition.FromBytes(_skillBinary.bytes);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SkillPlaybackTester] FromBytes failed: {ex.Message}", this);
                return;
            }

            if (_skill == null)
            {
                Debug.LogError("[SkillPlaybackTester] FromBytes returned null.", this);
                return;
            }

            // --- 输出技能概要和节点元信息 ---
            Debug.Log($"[SkillPlaybackTester] Loaded skill key={_skill.SkillKey} id={_skill.SkillId} lengthTicks={_skill.LengthTicks} tickRate={_skill.SourceTickRate} nodes={(_skill.Nodes?.Length ?? 0)}");
            if (_skill.Nodes != null)
            {
                for (int i = 0; i < _skill.Nodes.Length; i++)
                {
                    SkillRuntimeNode n = _skill.Nodes[i];
                    SkillGeneratedExecutorMetas.TryGetDomain(n.ClipId, out SkillNodeExecutionDomain d);
                    Debug.Log($"[SkillPlaybackTester] node#{n.NodeId} track={n.SourceTrackName} clipId={n.ClipId} domain={d} start={n.StartTick} end={n.EndTick} dataLen={n.DataLength}");
                }
            }

            _running = true;
        }

        /// <summary>每帧累加时间，换算为 tick，按域状态跟踪派发节点。</summary>
        private void Update()
        {
            if (!_running || _skill == null)
                return;

            // --- 累计时间换算 tick，超出技能时长则结束 ---
            _elapsedSeconds += Time.deltaTime;
            int elapsedTicks = Mathf.FloorToInt(_elapsedSeconds * _skill.SourceTickRate);

            if (elapsedTicks > _skill.LengthTicks)
            {
                // --- 技能结束：对所有 active 节点触发 OnEnd ---
                StopAllActiveNodes(elapsedTicks);
                _running = false;
                Debug.Log($"[SkillPlaybackTester] done at elapsedTicks={elapsedTicks} (lengthTicks={_skill.LengthTicks})");
                return;
            }

            SkillRuntimeNode[] nodes = _skill.Nodes;
            if (nodes == null)
                return;

            float delta = Time.deltaTime;
            uint currentTick = (uint)elapsedTicks;

            // --- 三个 domain 分别状态跟踪调度 ---
            TickDomain(nodes, elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ClientPrediction, _activeClientPredictionNodeIds);
            TickDomain(nodes, elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ClientOnly, _activeClientOnlyNodeIds);
            TickDomain(nodes, elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ServerOnly, _activeServerOnlyNodeIds);
        }

        /// <summary>单 domain 状态跟踪调度，触发 OnStart/OnTick/OnEnd。</summary>
        private void TickDomain(SkillRuntimeNode[] nodes, int elapsedTicks, float delta, uint currentTick, SkillNodeExecutionDomain domain, HashSet<int> activeNodeIds)
        {
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
                    activeNodeIds.Add(nodeId);
                    DispatchNode(node, domain, currentTick, elapsedTicks, delta, BattleSkillNodeLifecyclePhase.Start);
                }
                else if (isActive && wasActive)
                {
                    DispatchNode(node, domain, currentTick, elapsedTicks, delta, BattleSkillNodeLifecyclePhase.Tick);
                }
                else if (!isActive && wasActive)
                {
                    activeNodeIds.Remove(nodeId);
                    DispatchNode(node, domain, currentTick, elapsedTicks, delta, BattleSkillNodeLifecyclePhase.End);
                }
            }
        }

        /// <summary>技能结束时对所有 active 节点触发 OnEnd。</summary>
        private void StopAllActiveNodes(int elapsedTicks)
        {
            float delta = Time.deltaTime;
            uint currentTick = (uint)elapsedTicks;
            StopDomainNodes(elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ClientPrediction, _activeClientPredictionNodeIds);
            StopDomainNodes(elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ClientOnly, _activeClientOnlyNodeIds);
            StopDomainNodes(elapsedTicks, delta, currentTick, SkillNodeExecutionDomain.ServerOnly, _activeServerOnlyNodeIds);
        }

        /// <summary>对单个 domain 的 active 节点触发 OnEnd 并清空。</summary>
        private void StopDomainNodes(int elapsedTicks, float delta, uint currentTick, SkillNodeExecutionDomain domain, HashSet<int> activeNodeIds)
        {
            if (activeNodeIds.Count == 0 || _skill?.Nodes == null)
                return;

            SkillRuntimeNode[] nodes = _skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!activeNodeIds.Contains(node.NodeId))
                    continue;

                DispatchNode(node, domain, currentTick, elapsedTicks, delta, BattleSkillNodeLifecyclePhase.End);
            }
            activeNodeIds.Clear();
        }

        /// <summary>查找 Executor 并派发执行（context 依赖全 null，Executor 为 log-only）。</summary>
        private void DispatchNode(SkillRuntimeNode node, SkillNodeExecutionDomain domain, uint currentTick, int elapsedTicks, float delta, BattleSkillNodeLifecyclePhase phase)
        {
            if (!BattleSkillNodeExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
            {
                Debug.LogWarning($"[SkillPlaybackTester] No executor for clipId={node.ClipId}", this);
                return;
            }

            // --- 构造桩 context：Motor/CombatState/Services 全 null，Executor 当前不依赖它们 ---
            BattleSkillExecutionContext context = new(
                motor: null,
                controller: null,
                combatState: null,
                attributeSet: null,
                services: null,
                skill: _skill,
                command: default,
                node: node,
                aimDirection: Vector3.forward,
                currentTick: currentTick,
                elapsedTicks: elapsedTicks,
                delta: delta,
                replicateState: default,
                lifecyclePhase: phase);

            Debug.Log($"[SkillPlaybackTester] -> dispatch node#{node.NodeId} clipId={node.ClipId} domain={domain} phase={phase} elapsed={elapsedTicks}");
            executor.Execute(context);
        }
    }
}
