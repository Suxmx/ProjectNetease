using System.Collections.Generic;
using Hoshino;
using Hoshino.Skill.Executor;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Runs compiled MemoFramework skill timelines for local presentation and owner hit requests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartySkillRuntime : MonoBehaviour
    {
        private sealed class SkillInstance
        {
            public SkillDefinition Skill;
            public int Sequence;
            public Vector3 AimDirection;
            public int LocalTick;
            public float TickAccumulator;
            public bool RequestServerHits;
            public readonly HashSet<int> ActiveNodeIds = new();
            public readonly HashSet<int> RequestedHitKeys = new();
        }

        [SerializeField] private PartyAnimancerPresenter _animancerPresenter;
        [SerializeField] private PartyNetworkSkillController _networkController;

        private readonly List<SkillInstance> _instances = new();

        /// <summary>
        /// Assigns runtime service references used by skill node execution.
        /// </summary>
        public void Configure(PartyAnimancerPresenter animancerPresenter, PartyNetworkSkillController networkController)
        {
            _animancerPresenter = animancerPresenter;
            _networkController = networkController;
        }

        /// <summary>
        /// Starts a local or remote skill timeline instance.
        /// </summary>
        public bool BeginSkill(SkillDefinition skill, int sequence, Vector3 aimDirection, int initialLocalTick, bool requestServerHits)
        {
            ResolveReferences();
            if (skill == null)
                return false;

            SkillInstance instance = new()
            {
                Skill = skill,
                Sequence = sequence,
                AimDirection = NormalizeAimDirection(aimDirection),
                LocalTick = Mathf.Clamp(initialLocalTick, 0, Mathf.Max(0, skill.LengthTicks)),
                RequestServerHits = requestServerHits
            };

            _instances.Add(instance);
            ProcessTick(instance);
            instance.LocalTick++;
            return true;
        }

        /// <summary>
        /// Advances all active skill instances at each skill's source tick rate.
        /// </summary>
        private void Update()
        {
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                SkillInstance instance = _instances[i];
                if (instance.Skill == null)
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                float tickDelta = 1f / Mathf.Max(1, instance.Skill.SourceTickRate);
                instance.TickAccumulator += Time.deltaTime;
                while (instance.TickAccumulator >= tickDelta)
                {
                    instance.TickAccumulator -= tickDelta;
                    if (instance.LocalTick > instance.Skill.LengthTicks)
                    {
                        StopInstance(instance);
                        _instances.RemoveAt(i);
                        break;
                    }

                    ProcessTick(instance);
                    instance.LocalTick++;
                }
            }
        }

        /// <summary>
        /// Dispatches one skill tick to active timeline nodes.
        /// </summary>
        private void ProcessTick(SkillInstance instance)
        {
            SkillRuntimeNode[] nodes = instance.Skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                bool isActive = node.IsActiveAt(instance.LocalTick);
                bool wasActive = instance.ActiveNodeIds.Contains(node.NodeId);

                if (isActive && !wasActive)
                {
                    instance.ActiveNodeIds.Add(node.NodeId);
                    ExecuteNode(instance, node, ESkillNodeLifecyclePhase.Start);
                    ExecuteNode(instance, node, ESkillNodeLifecyclePhase.Tick);
                }
                else if (isActive)
                {
                    ExecuteNode(instance, node, ESkillNodeLifecyclePhase.Tick);
                }
                else if (wasActive)
                {
                    ExecuteNode(instance, node, ESkillNodeLifecyclePhase.Tick);
                    instance.ActiveNodeIds.Remove(node.NodeId);
                    ExecuteNode(instance, node, ESkillNodeLifecyclePhase.End);
                }
            }
        }

        /// <summary>
        /// Executes generated presentation executors and emits owner hit requests for damage nodes.
        /// </summary>
        private void ExecuteNode(SkillInstance instance, SkillRuntimeNode node, ESkillNodeLifecyclePhase phase)
        {
            PartySkillExecutionContext context = new(
                instance.Skill,
                node,
                phase,
                transform,
                _animancerPresenter,
                _networkController,
                instance.LocalTick,
                instance.AimDirection);

            if (SkillGeneratedExecutorBindings.TryGetExecutor(node.ClipId, out ISkillNodeExecutor<PartySkillExecutionContext> executor))
                executor.Execute(context);

            if (instance.RequestServerHits)
                TryRequestServerHit(instance, node, phase);
        }

        /// <summary>
        /// Sends server hit requests when local timeline damage nodes reach their hit ticks.
        /// </summary>
        private void TryRequestServerHit(SkillInstance instance, SkillRuntimeNode node, ESkillNodeLifecyclePhase phase)
        {
            if (_networkController == null)
                return;

            if (node.ClipId == SkillGeneratedIds.SingleDamageClip)
            {
                if (phase != ESkillNodeLifecyclePhase.Start)
                    return;

                RequestHitOnce(instance, node, instance.LocalTick);
                return;
            }

            if (node.ClipId != SkillGeneratedIds.MultiDamageClip || phase != ESkillNodeLifecyclePhase.Tick)
                return;

            if (!node.IsActiveAt(instance.LocalTick))
                return;

            if (!SkillGeneratedSerializationServices.Runtime.TryRead(instance.Skill, node, out MultiDamageNodeData data))
                return;

            int elapsedTick = Mathf.Max(0, instance.LocalTick - node.StartTick);
            int interval = Mathf.Max(1, data.HitIntervalTicks);
            if (elapsedTick % interval != 0)
                return;

            RequestHitOnce(instance, node, instance.LocalTick);
        }

        /// <summary>
        /// Sends one deduplicated hit request for a timeline damage node and skill tick.
        /// </summary>
        private void RequestHitOnce(SkillInstance instance, SkillRuntimeNode node, int localSkillTick)
        {
            int hitKey = CreateHitKey(node.NodeId, localSkillTick);
            if (!instance.RequestedHitKeys.Add(hitKey))
                return;

            _networkController.RequestSkillHit(instance.Sequence, node.NodeId, localSkillTick, instance.AimDirection);
        }

        /// <summary>
        /// Creates a stable per-node per-tick key for local hit request deduplication.
        /// </summary>
        private static int CreateHitKey(int nodeId, int localSkillTick)
        {
            unchecked
            {
                return (nodeId * 397) ^ localSkillTick;
            }
        }

        /// <summary>
        /// Ends all active nodes for an interrupted or finished skill instance.
        /// </summary>
        private void StopInstance(SkillInstance instance)
        {
            SkillRuntimeNode[] nodes = instance.Skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (!instance.ActiveNodeIds.Contains(node.NodeId))
                    continue;

                ExecuteNode(instance, node, ESkillNodeLifecyclePhase.End);
            }

            instance.ActiveNodeIds.Clear();
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_animancerPresenter == null)
                _animancerPresenter = GetComponentInChildren<PartyAnimancerPresenter>();

            if (_networkController == null)
                _networkController = GetComponentInParent<PartyNetworkSkillController>();
        }

        /// <summary>
        /// Returns a stable aim direction for timeline spatial calculations.
        /// </summary>
        private Vector3 NormalizeAimDirection(Vector3 aimDirection)
        {
            Vector3 planar = Vector3.ProjectOnPlane(aimDirection, transform.up);
            if (planar.sqrMagnitude <= 0.0001f)
                planar = Vector3.ProjectOnPlane(transform.forward, transform.up);

            return planar.sqrMagnitude > 0.0001f ? planar.normalized : transform.forward;
        }
    }
}
