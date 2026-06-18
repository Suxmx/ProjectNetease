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
        [SerializeField] private bool _logPredictedEveryTick = true;

        private SkillDefinition _skill;
        private float _elapsedSeconds;
        private bool _running;
        private readonly HashSet<int> _executedServerNodeIds = new();

        private void Start()
        {
            ReloadAndStart();
        }

        /// <summary>重新加载技能二进制并开始回放。可通过 ContextMenu 在运行时重跑。</summary>
        [ContextMenu("Reload & Start")]
        public void ReloadAndStart()
        {
            _executedServerNodeIds.Clear();
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
                    BattleSkillExecutorDomains.TryGet(n.ClipId, out SkillNodeExecutionDomain d);
                    Debug.Log($"[SkillPlaybackTester] node#{n.NodeId} track={n.SourceTrackName} clipId={n.ClipId} domain={d} start={n.StartTick} end={n.EndTick} dataLen={n.DataLength}");
                }
            }

            _running = true;
        }

        /// <summary>每帧累加时间，换算为 tick，按域规则派发节点。</summary>
        private void Update()
        {
            if (!_running || _skill == null)
                return;

            // --- 累计时间换算 tick，超出技能时长则结束 ---
            _elapsedSeconds += Time.deltaTime;
            int elapsedTicks = Mathf.FloorToInt(_elapsedSeconds * _skill.SourceTickRate);

            if (elapsedTicks > _skill.LengthTicks)
            {
                _running = false;
                Debug.Log($"[SkillPlaybackTester] done at elapsedTicks={elapsedTicks} (lengthTicks={_skill.LengthTicks})");
                return;
            }

            SkillRuntimeNode[] nodes = _skill.Nodes;
            if (nodes == null)
                return;

            float delta = Time.deltaTime;
            uint currentTick = (uint)elapsedTicks;

            // --- 遍历节点，按域决定是否派发 ---
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;

                bool shouldDispatch;
                switch (domain)
                {
                    // --- Predicted：active 区间内每 tick（或仅起始 tick） ---
                    case SkillNodeExecutionDomain.Predicted:
                        shouldDispatch = node.IsActiveAt(elapsedTicks) && (_logPredictedEveryTick || elapsedTicks == node.StartTick);
                        break;
                    // --- Server/Lag：仅起始 tick 执行一次，HashSet 去重 ---
                    case SkillNodeExecutionDomain.ServerAuthority:
                    case SkillNodeExecutionDomain.LagCompensatedQuery:
                        shouldDispatch = elapsedTicks == node.StartTick && _executedServerNodeIds.Add(node.NodeId);
                        break;
                    default:
                        shouldDispatch = false;
                        break;
                }

                if (!shouldDispatch)
                    continue;

                DispatchNode(node, domain, currentTick, elapsedTicks, delta);
            }
        }

        /// <summary>查找 Executor 并派发执行（context 依赖全 null，Executor 为 log-only）。</summary>
        private void DispatchNode(SkillRuntimeNode node, SkillNodeExecutionDomain domain, uint currentTick, int elapsedTicks, float delta)
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
                replicateState: default);

            Debug.Log($"[SkillPlaybackTester] -> dispatch node#{node.NodeId} clipId={node.ClipId} domain={domain} elapsed={elapsedTicks}");
            executor.Execute(context);
        }
    }
}
