using Hoshino;
using UnityEngine;

namespace Battle
{
    public static class BattleSkillNodeUtility
    {
        public static Quaternion ResolveRotation(SkillSpace space, Transform actor, Vector3 aimDirection)
        {
            switch (space)
            {
                case SkillSpace.ActorForward:
                    return actor.rotation;
                case SkillSpace.AimDirection:
                    aimDirection.y = 0f;
                    if (aimDirection.sqrMagnitude <= 0.0001f)
                        aimDirection = actor.forward;
                    return Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                default:
                    return Quaternion.identity;
            }
        }

        public static Vector3 ResolveVector(SkillSpace space, Vector3 value, Transform actor, Vector3 aimDirection, bool flattenValue = true)
        {
            if (flattenValue)
                value.y = 0f;

            switch (space)
            {
                case SkillSpace.ActorForward:
                    return actor.TransformDirection(value);
                case SkillSpace.AimDirection:
                    aimDirection.y = 0f;
                    if (aimDirection.sqrMagnitude <= 0.0001f)
                        aimDirection = actor.forward;
                    Quaternion aimRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                    return aimRotation * value;
                default:
                    return value;
            }
        }
    }
}
