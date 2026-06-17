using System;
using UnityEngine;

namespace Battle
{
    public sealed class BattleSkillRuntimeServices : MonoBehaviour
    {
        [Serializable]
        public struct ProjectileSlot
        {
            public int ProjectileId;
            public BattleProjectile Prefab;
        }

        [SerializeField] private BattleLagCompensatedHitResolver _hitResolver;
        [SerializeField] private ProjectileSlot[] _projectileSlots = Array.Empty<ProjectileSlot>();

        public BattleLagCompensatedHitResolver HitResolver
        {
            get
            {
                if (_hitResolver == null)
                    _hitResolver = FindFirstObjectByType<BattleLagCompensatedHitResolver>();

                return _hitResolver;
            }
        }

        public BattleProjectile FindProjectilePrefab(int projectileId)
        {
            foreach (ProjectileSlot item in _projectileSlots)
            {
                if (item.ProjectileId == projectileId)
                    return item.Prefab;
            }

            return null;
        }
    }
}
