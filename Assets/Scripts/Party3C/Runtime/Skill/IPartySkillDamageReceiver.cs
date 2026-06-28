using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Receives server-authoritative damage results from party skill hit checks.
    /// </summary>
    public interface IPartySkillDamageReceiver
    {
        /// <summary>
        /// Applies one resolved skill damage event on the server.
        /// </summary>
        void ReceiveSkillDamage(in PartySkillDamageContext context);
    }

    /// <summary>
    /// Describes one server-resolved skill damage event.
    /// </summary>
    public readonly struct PartySkillDamageContext
    {
        public readonly PartyNetworkSkillController Attacker;
        public readonly int SkillId;
        public readonly int Sequence;
        public readonly int NodeId;
        public readonly int Damage;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitDirection;
        public readonly Collider HitCollider;

        /// <summary>
        /// Creates a damage context for a resolved hit.
        /// </summary>
        public PartySkillDamageContext(PartyNetworkSkillController attacker, int skillId, int sequence, int nodeId, int damage, Vector3 hitPoint, Vector3 hitDirection, Collider hitCollider)
        {
            Attacker = attacker;
            SkillId = skillId;
            Sequence = sequence;
            NodeId = nodeId;
            Damage = damage;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            HitCollider = hitCollider;
        }
    }
}
