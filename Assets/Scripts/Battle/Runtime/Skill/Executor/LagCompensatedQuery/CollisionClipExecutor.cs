using FishNet.Managing.Timing;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 碰撞伤害节点 Executor（LagCompensatedQuery 域）。
    /// 在节点起始 tick 于服务端执行滞后补偿物理查询，
    /// 按 Shape（Sphere/Ray/Box）检测命中并分发伤害。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
    [BattleSkillExecutor(SkillGeneratedIds.CollisionClip, SkillNodeExecutionDomain.LagCompensatedQuery)]
    public sealed class CollisionClipExecutor : BattleSkillNodeExecutor<CollisionNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in CollisionNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(CollisionNodeData)} @ elapsed={context.ElapsedTicks} clipId={context.Node.ClipId} domain=LagCompensatedQuery: {JsonUtility.ToJson(data)}");
            // BattleLagCompensatedHitResolver resolver = context.Services != null ? context.Services.HitResolver : null;
            // if (resolver == null)
            //     return;
            //
            // Vector3 center = context.Motor.transform.position + BattleSkillNodeUtility.ResolveVector(data.Space, data.Offset, context.Motor.transform, context.AimDirection, false);
            // Quaternion rotation = BattleSkillNodeUtility.ResolveRotation(data.Space, context.Motor.transform, context.AimDirection);
            // PreciseTick preciseTick = context.GetCurrentPreciseTick();
            // int damage = context.ScaleOutgoingDamage(data.Damage);
            //
            // switch (data.Shape)
            // {
            //     case SkillHitShape.Sphere:
            //         resolver.ResolveDamageSphere(preciseTick, context.CombatState, context.Motor.Owner, center, Mathf.Max(0.01f, data.Radius), data.HitMask, damage, context.Node.ClipId);
            //         break;
            //     case SkillHitShape.Ray:
            //         resolver.ResolveDamageRay(preciseTick, context.CombatState, context.Motor.Owner, center, rotation * Vector3.forward, Mathf.Max(0.01f, data.Distance), data.HitMask, damage, context.Node.ClipId);
            //         break;
            //     default:
            //         resolver.ResolveDamageBox(preciseTick, context.CombatState, context.Motor.Owner, center, rotation, Vector3.Max(data.HalfExtents, Vector3.one * 0.01f), data.HitMask, damage, context.Node.ClipId);
            //         break;
            // }
        }
    }
}
