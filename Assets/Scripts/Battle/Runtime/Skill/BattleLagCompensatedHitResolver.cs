using System.Collections.Generic;
using FishNet.Component.ColliderRollback;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace Battle
{
    public sealed class BattleLagCompensatedHitResolver : NetworkBehaviour
    {
        [SerializeField] private int _maxHits = 32;

        private Collider[] _hits;
        private readonly HashSet<BattleCombatState> _damagedTargets = new();
        private readonly HashSet<BattleDestructibleObject> _damagedObjects = new();

        private void Awake()
        {
            _hits = new Collider[Mathf.Max(1, _maxHits)];
        }

        [Server]
        public int ResolveDamageBox(PreciseTick preciseTick, BattleCombatState attackerState, NetworkConnection attackerConnection, Vector3 center, Quaternion rotation, Vector3 halfExtents, LayerMask layerMask, int damage)
        {
            RollbackForQuery(preciseTick);
            try
            {
                int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hits, rotation, layerMask, QueryTriggerInteraction.Ignore);
                return ApplyDamageToHits(hitCount, attackerState, attackerConnection, damage);
            }
            finally
            {
                ReturnRollback();
            }
        }

        [Server]
        public int ResolveDamageSphere(PreciseTick preciseTick, BattleCombatState attackerState, NetworkConnection attackerConnection, Vector3 center, float radius, LayerMask layerMask, int damage)
        {
            RollbackForQuery(preciseTick);
            try
            {
                int hitCount = Physics.OverlapSphereNonAlloc(center, radius, _hits, layerMask, QueryTriggerInteraction.Ignore);
                return ApplyDamageToHits(hitCount, attackerState, attackerConnection, damage);
            }
            finally
            {
                ReturnRollback();
            }
        }

        [Server]
        public bool ResolveDamageRay(PreciseTick preciseTick, BattleCombatState attackerState, NetworkConnection attackerConnection, Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, int damage)
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

                BattleCombatState target = hit.collider.GetComponentInParent<BattleCombatState>();
                if (target != null)
                    return target != attackerState && target.TryTakeDamage(damage, attackerConnection);

                BattleDestructibleObject destructible = hit.collider.GetComponentInParent<BattleDestructibleObject>();
                return destructible != null && destructible.TryTakeDamage(damage, attackerConnection);
            }
            finally
            {
                ReturnRollback();
            }
        }

        private int ApplyDamageToHits(int hitCount, BattleCombatState attackerState, NetworkConnection attackerConnection, int damage)
        {
            int applied = 0;
            _damagedTargets.Clear();
            _damagedObjects.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                if (hit == null)
                    continue;

                BattleCombatState target = hit.GetComponentInParent<BattleCombatState>();
                if (target != null)
                {
                    if (target == attackerState || target.IsDead || !_damagedTargets.Add(target))
                        continue;

                    if (target.TryTakeDamage(damage, attackerConnection))
                        applied++;
                    continue;
                }

                BattleDestructibleObject destructible = hit.GetComponentInParent<BattleDestructibleObject>();
                if (destructible != null && !destructible.IsDestroyed && _damagedObjects.Add(destructible) && destructible.TryTakeDamage(damage, attackerConnection))
                    applied++;
            }

            return applied;
        }

        private void RollbackForQuery(PreciseTick preciseTick)
        {
            if (RollbackManager != null)
                RollbackManager.Rollback(preciseTick, RollbackPhysicsType.Physics, IsOwner);
        }

        private void ReturnRollback()
        {
            if (RollbackManager != null)
                RollbackManager.Return();
        }
    }
}
