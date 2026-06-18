using UnityEngine;

namespace Battle
{
    public sealed class BattleSkillRuntimeServices : MonoBehaviour
    {
        [SerializeField] private BattleLagCompensatedHitResolver _hitResolver;

        public BattleLagCompensatedHitResolver HitResolver
        {
            get
            {
                if (_hitResolver == null)
                    _hitResolver = FindFirstObjectByType<BattleLagCompensatedHitResolver>();

                return _hitResolver;
            }
        }
    }
}
