using System.Collections.Generic;
using FishNet.Component.ColliderRollback;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 滞后补偿命中解析器。在服务端执行物理查询前回滚碰撞体到历史 tick，
    /// 查询完成后恢复，确保高延迟玩家的命中判定与服务端实际状态一致。
    /// 查询命中的目标统一通过 <see cref="BattleDamageDispatcher"/> 施加伤害。
    /// </summary>
    public sealed class LagCompensatedHitResolver : NetworkBehaviour
    {
        [SerializeField] private int _maxHits = 32;

        private Collider[] _hits;
        private readonly HashSet<CombatState> _damagedTargets = new();
        private readonly HashSet<BattleDestructibleObject> _damagedObjects = new();

        private void Awake()
        {
            _hits = new Collider[Mathf.Max(1, _maxHits)];
        }

        /// <summary>盒形范围伤害查询（滞后补偿）。</summary>
        /// <param name="sourceClipId">伤害来源的技能节点 ClipId。</param>
        /// <returns>实际命中并施加伤害的目标数。</returns>
        [Server]
        public int ResolveDamageBox(PreciseTick preciseTick, CombatState attackerState, NetworkConnection attackerConnection, Vector3 center, Quaternion rotation, Vector3 halfExtents, LayerMask layerMask, int damage, uint sourceClipId = 0u)
        {
            RollbackForQuery(preciseTick);
            try
            {
                int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hits, rotation, layerMask, QueryTriggerInteraction.Ignore);
                return ApplyDamageToHits(preciseTick, hitCount, attackerState, attackerConnection, damage, sourceClipId);
            }
            finally
            {
                ReturnRollback();
            }
        }

        /// <summary>球形范围伤害查询（滞后补偿）。</summary>
        [Server]
        public int ResolveDamageSphere(PreciseTick preciseTick, CombatState attackerState, NetworkConnection attackerConnection, Vector3 center, float radius, LayerMask layerMask, int damage, uint sourceClipId = 0u)
        {
            RollbackForQuery(preciseTick);
            try
            {
                int hitCount = Physics.OverlapSphereNonAlloc(center, radius, _hits, layerMask, QueryTriggerInteraction.Ignore);
                return ApplyDamageToHits(preciseTick, hitCount, attackerState, attackerConnection, damage, sourceClipId);
            }
            finally
            {
                ReturnRollback();
            }
        }

        /// <summary>射线伤害查询（滞后补偿），命中第一个有效目标。</summary>
        [Server]
        public bool ResolveDamageRay(PreciseTick preciseTick, CombatState attackerState, NetworkConnection attackerConnection, Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, int damage, uint sourceClipId = 0u)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            direction.Normalize();
            RollbackForQuery(preciseTick);
            try
            {
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, layerMask, QueryTriggerInteraction.Ignore))
                    return false;

                // --- 命中战斗体：排除自伤，分发伤害 ---
                CombatState target = hit.collider.GetComponentInParent<CombatState>();
                if (target != null)
                {
                    if (target == attackerState)
                        return false;
                    BattleDamageDispatcher.Apply(new BattleDamageInfo
                    {
                        Type = BattleDamageType.Skill,
                        Amount = damage,
                        Source = attackerState,
                        Target = target,
                        SourceConnection = attackerConnection,
                        SourceClipId = sourceClipId,
                        HitPoint = hit.point,
                        HitNormal = hit.normal,
                        Tick = preciseTick.Tick
                    });
                    return true;
                }

                // --- 命中可破坏物：分发伤害 ---
                BattleDestructibleObject destructible = hit.collider.GetComponentInParent<BattleDestructibleObject>();
                if (destructible == null)
                    return false;

                BattleDamageDispatcher.Apply(new BattleDamageInfo
                {
                    Type = BattleDamageType.Skill,
                    Amount = damage,
                    Source = attackerState,
                    Target = destructible,
                    SourceConnection = attackerConnection,
                    SourceClipId = sourceClipId,
                    HitPoint = hit.point,
                    HitNormal = hit.normal,
                    Tick = preciseTick.Tick
                });
                return true;
            }
            finally
            {
                ReturnRollback();
            }
        }

        /// <summary>遍历 Overlap 命中结果，去重后对每个目标分发伤害。</summary>
        private int ApplyDamageToHits(PreciseTick preciseTick, int hitCount, CombatState attackerState, NetworkConnection attackerConnection, int damage, uint sourceClipId)
        {
            int applied = 0;
            _damagedTargets.Clear();
            _damagedObjects.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                if (hit == null)
                    continue;

                // --- 战斗体：排除自伤、已死亡、重复命中 ---
                CombatState target = hit.GetComponentInParent<CombatState>();
                if (target != null)
                {
                    if (target == attackerState || target.IsDead || !_damagedTargets.Add(target))
                        continue;

                    BattleDamageDispatcher.Apply(new BattleDamageInfo
                    {
                        Type = BattleDamageType.Skill,
                        Amount = damage,
                        Source = attackerState,
                        Target = target,
                        SourceConnection = attackerConnection,
                        SourceClipId = sourceClipId,
                        HitPoint = hit.transform.position,
                        Tick = preciseTick.Tick
                    });
                    applied++;
                    continue;
                }

                // --- 可破坏物：排除已破坏、重复命中 ---
                BattleDestructibleObject destructible = hit.GetComponentInParent<BattleDestructibleObject>();
                if (destructible != null && !destructible.IsDestroyed && _damagedObjects.Add(destructible))
                {
                    BattleDamageDispatcher.Apply(new BattleDamageInfo
                    {
                        Type = BattleDamageType.Skill,
                        Amount = damage,
                        Source = attackerState,
                        Target = destructible,
                        SourceConnection = attackerConnection,
                        SourceClipId = sourceClipId,
                        HitPoint = hit.transform.position,
                        Tick = preciseTick.Tick
                    });
                    applied++;
                }
            }

            return applied;
        }

        /// <summary>回滚碰撞体到指定 tick 的历史状态。</summary>
        private void RollbackForQuery(PreciseTick preciseTick)
        {
            if (RollbackManager != null)
                RollbackManager.Rollback(preciseTick, RollbackPhysicsType.Physics, IsOwner);
        }

        /// <summary>恢复碰撞体到当前状态。</summary>
        private void ReturnRollback()
        {
            if (RollbackManager != null)
                RollbackManager.Return();
        }
    }
}
