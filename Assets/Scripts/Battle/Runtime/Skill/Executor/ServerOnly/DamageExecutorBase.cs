using System;
using FishNet.Managing.Timing;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 伤害 Executor 共享基类。封装命中判定 + 伤害分发的通用逻辑，
    /// <see cref="SingleDamageClipExecutor"/> 和 <see cref="MultiDamageClipExecutor"/> 继承此类。
    /// </summary>
    public abstract class DamageExecutorBase<TData> : ServerOnlySkillExecutor<TData>
        where TData : struct
    {
        /// <summary>执行一次命中判定 + 伤害分发，带伤害组过滤。</summary>
        protected void DoHit(in SkillExecutionContext context, SkillHitShape shape, SkillSpace space, Vector3 offset, Vector3 halfExtents, float radius, float distance, LayerMask hitMask, int damage, byte damageGroupId)
        {
            LagCompensatedHitResolver resolver = context.Services != null ? context.Services.HitResolver : null;
            if (resolver == null)
                return;

            Vector3 center = context.Motor.Transform.position + SkillUtility.ResolveVector(space, offset, context.Motor.Transform, context.AimDirection, false);
            Quaternion rotation = SkillUtility.ResolveRotation(space, context.Motor.Transform, context.AimDirection);
            PreciseTick preciseTick = context.GetCurrentPreciseTick();
            int scaledDamage = context.ScaleOutgoingDamage(damage);

            // --- 伤害组过滤回调（提取 in 参数到局部，避免 lambda 捕获 ref）---
            Func<IBattleDamageTarget, bool> canHit = null;
            if (damageGroupId != 0)
            {
                SkillController controller = context.Controller;
                byte groupId = damageGroupId;
                canHit = target => controller.TryConsumeGroupHit(groupId, target);
            }

            switch (shape)
            {
                case SkillHitShape.Sphere:
                    resolver.ResolveDamageSphere(preciseTick, context.CombatState, context.Player.Owner, center, Mathf.Max(0.01f, radius), hitMask, scaledDamage, context.Node.ClipId, canHit);
                    break;
                case SkillHitShape.Ray:
                    resolver.ResolveDamageRay(preciseTick, context.CombatState, context.Player.Owner, center, rotation * Vector3.forward, Mathf.Max(0.01f, distance), hitMask, scaledDamage, context.Node.ClipId, canHit);
                    break;
                default:
                    resolver.ResolveDamageBox(preciseTick, context.CombatState, context.Player.Owner, center, rotation, Vector3.Max(halfExtents, Vector3.one * 0.01f), hitMask, scaledDamage, context.Node.ClipId, canHit);
                    break;
            }
        }
    }
}
