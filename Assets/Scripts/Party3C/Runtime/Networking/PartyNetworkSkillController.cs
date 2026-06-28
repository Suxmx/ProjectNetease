using System;
using System.Collections.Generic;
using FishNet.Object;
using Hoshino;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Synchronizes locally-authoritative skill playback and resolves damage requests on the server.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyNetworkSkillController : NetworkBehaviour
    {
        private sealed class ServerSkillInstance
        {
            public SkillDefinition Skill;
            public int SkillId;
            public int Sequence;
            public uint StartTick;
            public Vector3 AimDirection;
            public readonly HashSet<int> ResolvedHitKeys = new();
        }

        public event Action<int, int, int, int, Vector3, Vector3> SkillHitVisualReceived;

        [SerializeField] private PartySkillLibrary _skillLibrary;
        [SerializeField] private PartySkillRuntime _skillRuntime;
        [SerializeField] private PartyAnimancerPresenter _animancerPresenter;
        [SerializeField, Min(0)] private int _hitTickTolerance = 8;
        [SerializeField, Min(0f)] private float _serverInstanceKeepSeconds = 2f;

        private readonly Dictionary<int, ServerSkillInstance> _serverInstances = new();
        private readonly List<int> _serverInstancesToRemove = new();
        private int _nextSequence;

        /// <summary>
        /// Starts animation set preloading when this network object becomes active on a client.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveReferences();
            PreloadAnimationSets();
        }

        /// <summary>
        /// Starts animation set preloading when this network object becomes active on the server.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            ResolveReferences();
            PreloadAnimationSets();
        }

        /// <summary>
        /// Starts a skill from local owner input using the current transform forward as aim direction.
        /// </summary>
        public bool TryStartSkill(int skillId)
        {
            return TryStartSkill(skillId, transform.forward);
        }

        /// <summary>
        /// Starts a skill from local owner input and notifies the server.
        /// </summary>
        public bool TryStartSkill(int skillId, Vector3 aimDirection)
        {
            ResolveReferences();
            if (!IsOwner || _skillLibrary == null || _skillRuntime == null)
                return false;

            if (!_skillLibrary.TryGetSkill(skillId, out SkillDefinition skill))
                return false;

            if (!_skillLibrary.AreAnimationSetsLoaded(skill))
            {
                PreloadAnimationSets();
                return false;
            }

            int sequence = ++_nextSequence;
            Vector3 normalizedAim = NormalizeAimDirection(aimDirection);
            _skillRuntime.BeginSkill(skill, sequence, normalizedAim, 0, requestServerHits: true);
            StartSkillServerRpc(skillId, sequence, normalizedAim);
            return true;
        }

        /// <summary>
        /// Called by the local skill runtime when a damage timeline node reaches a hit tick.
        /// </summary>
        public void RequestSkillHit(int sequence, int nodeId, int localSkillTick, Vector3 aimDirection)
        {
            if (!IsOwner)
                return;

            RequestSkillHitServerRpc(sequence, nodeId, localSkillTick, NormalizeAimDirection(aimDirection));
        }

        /// <summary>
        /// Records an owner-started skill on the server and forwards its presentation to remote observers.
        /// </summary>
        [ServerRpc]
        private void StartSkillServerRpc(int skillId, int sequence, Vector3 aimDirection)
        {
            ResolveReferences();
            if (_skillLibrary == null || !_skillLibrary.TryGetSkill(skillId, out SkillDefinition skill))
                return;

            Vector3 normalizedAim = NormalizeAimDirection(aimDirection);
            uint startTick = TimeManager.Tick;
            _serverInstances[sequence] = new ServerSkillInstance
            {
                Skill = skill,
                SkillId = skillId,
                Sequence = sequence,
                StartTick = startTick,
                AimDirection = normalizedAim
            };

            StartSkillObserversRpc(skillId, sequence, normalizedAim, startTick);
        }

        /// <summary>
        /// Starts remote presentation for a server-forwarded skill instance.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void StartSkillObserversRpc(int skillId, int sequence, Vector3 aimDirection, uint serverStartTick)
        {
            ResolveReferences();
            if (_skillLibrary == null || _skillRuntime == null)
                return;

            if (!_skillLibrary.TryGetSkill(skillId, out SkillDefinition skill))
                return;

            if (!_skillLibrary.AreAnimationSetsLoaded(skill))
            {
                PreloadAnimationSets();
                return;
            }

            int initialTick = GetElapsedSkillTicks(skill, serverStartTick);
            _skillRuntime.BeginSkill(skill, sequence, NormalizeAimDirection(aimDirection), initialTick, requestServerHits: false);
        }

        /// <summary>
        /// Validates a locally-timed hit request and resolves the hitbox on the server.
        /// </summary>
        [ServerRpc]
        private void RequestSkillHitServerRpc(int sequence, int nodeId, int localSkillTick, Vector3 aimDirection)
        {
            if (!_serverInstances.TryGetValue(sequence, out ServerSkillInstance instance) || instance.Skill == null)
                return;

            if (!TryFindNode(instance.Skill, nodeId, out SkillRuntimeNode node))
                return;

            if (node.ClipId != SkillGeneratedIds.SingleDamageClip && node.ClipId != SkillGeneratedIds.MultiDamageClip)
                return;

            int serverSkillTick = GetElapsedSkillTicks(instance.Skill, instance.StartTick);
            if (Mathf.Abs(serverSkillTick - localSkillTick) > _hitTickTolerance)
                return;

            Vector3 normalizedAim = NormalizeAimDirection(aimDirection);
            if (node.ClipId == SkillGeneratedIds.SingleDamageClip)
            {
                if (localSkillTick != node.StartTick)
                    return;

                int hitKey = CreateHitKey(nodeId, node.StartTick);
                if (!instance.ResolvedHitKeys.Add(hitKey))
                    return;

                if (SkillGeneratedSerializationServices.Runtime.TryRead(instance.Skill, node, out SingleDamageNodeData singleDamage))
                    ResolveDamageNode(instance, node, singleDamage.Shape, singleDamage.Space, singleDamage.Offset, singleDamage.HalfExtents, singleDamage.Radius, singleDamage.Distance, singleDamage.HitMask, singleDamage.Damage, normalizedAim);

                return;
            }

            if (!node.IsActiveAt(localSkillTick))
                return;

            if (!SkillGeneratedSerializationServices.Runtime.TryRead(instance.Skill, node, out MultiDamageNodeData multiDamage))
                return;

            int elapsedTick = Mathf.Max(0, localSkillTick - node.StartTick);
            int interval = Mathf.Max(1, multiDamage.HitIntervalTicks);
            if (elapsedTick % interval != 0)
                return;

            int multiHitKey = CreateHitKey(nodeId, localSkillTick);
            if (!instance.ResolvedHitKeys.Add(multiHitKey))
                return;

            ResolveDamageNode(instance, node, multiDamage.Shape, multiDamage.Space, multiDamage.Offset, multiDamage.HalfExtents, multiDamage.Radius, multiDamage.Distance, multiDamage.HitMask, multiDamage.Damage, normalizedAim);
        }

        /// <summary>
        /// Removes expired server skill instances after their timeline and grace period have elapsed.
        /// </summary>
        private void Update()
        {
            if (!IsServerStarted || _serverInstances.Count == 0)
                return;

            _serverInstancesToRemove.Clear();
            foreach (KeyValuePair<int, ServerSkillInstance> pair in _serverInstances)
            {
                ServerSkillInstance instance = pair.Value;
                float elapsedSeconds = (float)TimeManager.TimePassed(instance.StartTick);
                float skillSeconds = SkillTickUtility.TicksToSeconds(instance.Skill.LengthTicks, instance.Skill.SourceTickRate);
                if (elapsedSeconds > skillSeconds + _serverInstanceKeepSeconds)
                    _serverInstancesToRemove.Add(pair.Key);
            }

            for (int i = 0; i < _serverInstancesToRemove.Count; i++)
                _serverInstances.Remove(_serverInstancesToRemove[i]);
        }

        /// <summary>
        /// Computes and applies one server-authoritative damage node.
        /// </summary>
        private void ResolveDamageNode(ServerSkillInstance instance, SkillRuntimeNode node, SkillHitShape shape, SkillSpace space, Vector3 offset, Vector3 halfExtents, float radius, float distance, LayerMask hitMask, int damage, Vector3 aimDirection)
        {
            Vector3 center = transform.position + ResolveVector(space, offset, aimDirection, flattenValue: false);
            Quaternion rotation = ResolveRotation(space, aimDirection);

            Collider[] hits = shape switch
            {
                SkillHitShape.Sphere => Physics.OverlapSphere(center, Mathf.Max(0.01f, radius), hitMask, QueryTriggerInteraction.Ignore),
                SkillHitShape.Ray => ResolveRayHits(center, rotation * Vector3.forward, Mathf.Max(0f, distance), hitMask),
                _ => Physics.OverlapBox(center, Vector3.Max(halfExtents, Vector3.one * 0.01f), rotation, hitMask, QueryTriggerInteraction.Ignore)
            };

            HashSet<IPartySkillDamageReceiver> damagedReceivers = new();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                IPartySkillDamageReceiver receiver = FindDamageReceiver(hit);
                if (receiver == null || !damagedReceivers.Add(receiver))
                    continue;

                Vector3 hitPoint = hit.ClosestPoint(center);
                Vector3 hitDirection = hitPoint - transform.position;
                if (hitDirection.sqrMagnitude <= 0.0001f)
                    hitDirection = aimDirection;

                hitDirection.Normalize();
                PartySkillDamageContext context = new(this, instance.SkillId, instance.Sequence, node.NodeId, damage, hitPoint, hitDirection, hit);
                receiver.ReceiveSkillDamage(context);
                SkillHitObserversRpc(instance.SkillId, instance.Sequence, node.NodeId, damage, hitPoint, hitDirection);
            }
        }

        /// <summary>
        /// Emits one visual hit event to every observer.
        /// </summary>
        [ObserversRpc]
        private void SkillHitObserversRpc(int skillId, int sequence, int nodeId, int damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            SkillHitVisualReceived?.Invoke(skillId, sequence, nodeId, damage, hitPoint, hitDirection);
        }

        /// <summary>
        /// Returns colliders hit by a ray damage shape.
        /// </summary>
        private Collider[] ResolveRayHits(Vector3 origin, Vector3 direction, float distance, LayerMask hitMask)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
                return new[] { hit.collider };

            return Array.Empty<Collider>();
        }

        /// <summary>
        /// Creates a stable per-node per-tick key for server damage settlement deduplication.
        /// </summary>
        private static int CreateHitKey(int nodeId, int localSkillTick)
        {
            unchecked
            {
                return (nodeId * 397) ^ localSkillTick;
            }
        }

        /// <summary>
        /// Finds the first damage receiver on a collider or its parents.
        /// </summary>
        private IPartySkillDamageReceiver FindDamageReceiver(Collider hit)
        {
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPartySkillDamageReceiver receiver)
                    return receiver;
            }

            return null;
        }

        /// <summary>
        /// Finds a compiled skill node by runtime node id.
        /// </summary>
        private bool TryFindNode(SkillDefinition skill, int nodeId, out SkillRuntimeNode node)
        {
            SkillRuntimeNode[] nodes = skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeId != nodeId)
                    continue;

                node = nodes[i];
                return true;
            }

            node = default;
            return false;
        }

        /// <summary>
        /// Converts elapsed FishNet server ticks to the skill timeline tick rate.
        /// </summary>
        private int GetElapsedSkillTicks(SkillDefinition skill, uint serverStartTick)
        {
            double elapsedSeconds = TimeManager.TimePassed(serverStartTick);
            return Mathf.Clamp(Mathf.FloorToInt((float)elapsedSeconds * skill.SourceTickRate), 0, Mathf.Max(0, skill.LengthTicks));
        }

        /// <summary>
        /// Resolves a timeline vector into world space using the skill space mode.
        /// </summary>
        private Vector3 ResolveVector(SkillSpace space, Vector3 value, Vector3 aimDirection, bool flattenValue)
        {
            if (flattenValue)
                value.y = 0f;

            return space switch
            {
                SkillSpace.ActorForward => transform.TransformDirection(value),
                SkillSpace.AimDirection => ResolveRotation(space, aimDirection) * value,
                _ => value
            };
        }

        /// <summary>
        /// Resolves a timeline rotation into world space using the skill space mode.
        /// </summary>
        private Quaternion ResolveRotation(SkillSpace space, Vector3 aimDirection)
        {
            if (space == SkillSpace.ActorForward)
                return transform.rotation;

            if (space == SkillSpace.AimDirection)
                return Quaternion.LookRotation(NormalizeAimDirection(aimDirection), transform.up);

            return Quaternion.identity;
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_skillRuntime == null)
                _skillRuntime = GetComponent<PartySkillRuntime>();

            if (_animancerPresenter == null)
                _animancerPresenter = GetComponentInChildren<PartyAnimancerPresenter>();

            if (_skillRuntime != null)
                _skillRuntime.Configure(_animancerPresenter, this);
        }

        /// <summary>
        /// Requests preload for every Addressable animation set referenced by configured skills.
        /// </summary>
        private void PreloadAnimationSets()
        {
            if (_skillLibrary != null)
                _skillLibrary.PreloadAnimationSets();
        }

        /// <summary>
        /// Returns a stable planar aim direction for skill playback and hitboxes.
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
