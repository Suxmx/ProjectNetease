using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.SpawnProjectileClip, SkillNodeExecutionDomain.ServerAuthority)]
    public sealed class SpawnProjectileClipExecutor : BattleSkillNodeExecutor<SpawnProjectileNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in SpawnProjectileNodeData data)
        {
            BattleProjectile prefab = context.Services != null ? context.Services.FindProjectilePrefab(data.ProjectileId) : null;
            if (prefab == null)
                return;

            Vector3 direction = BattleSkillNodeUtility.ResolveVector(data.DirectionSpace, Vector3.forward, context.Motor.transform, context.AimDirection);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = context.Motor.AimDirection;
            direction.y = 0f;
            direction.Normalize();

            Vector3 position = context.Motor.transform.position + BattleSkillNodeUtility.ResolveVector(data.DirectionSpace, data.SpawnOffset, context.Motor.transform, context.AimDirection, false);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            BattleProjectile projectile = Object.Instantiate(prefab, position, rotation);
            projectile.InitializeServer(context.CombatState, context.Motor.Owner, direction, data.Speed, context.ScaleOutgoingDamage(data.Damage), data.LifetimeSeconds);
            context.Motor.Spawn(projectile.gameObject);
        }
    }
}
