using System.Collections.Generic;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
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

        [ContextMenu("Reload & Start")]
        public void ReloadAndStart()
        {
            _executedServerNodeIds.Clear();
            _elapsedSeconds = 0f;
            _running = false;

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

        private void Update()
        {
            if (!_running || _skill == null)
                return;

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

            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!BattleSkillExecutorDomains.TryGet(node.ClipId, out SkillNodeExecutionDomain domain))
                    continue;

                bool shouldDispatch;
                switch (domain)
                {
                    case SkillNodeExecutionDomain.Predicted:
                        shouldDispatch = node.IsActiveAt(elapsedTicks) && (_logPredictedEveryTick || elapsedTicks == node.StartTick);
                        break;
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

        private void DispatchNode(SkillRuntimeNode node, SkillNodeExecutionDomain domain, uint currentTick, int elapsedTicks, float delta)
        {
            if (!BattleSkillNodeExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
            {
                Debug.LogWarning($"[SkillPlaybackTester] No executor for clipId={node.ClipId}", this);
                return;
            }

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
