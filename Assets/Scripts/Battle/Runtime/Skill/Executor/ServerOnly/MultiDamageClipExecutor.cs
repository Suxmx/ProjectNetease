using Hoshino;

namespace Battle
{
    /// <summary>
    /// 多次伤害判定 Executor（ServerOnly 域）。
    /// OnStart 判定一次，OnTick 每隔 HitIntervalTicks 判定一次。
    /// DamageGroupId>0 时通过伤害组做命中次数限制。
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.MultiDamageClip)]
    public sealed class MultiDamageClipExecutor : DamageExecutorBase<MultiDamageNodeData>
    {
        protected override void OnStart(in SkillExecutionContext context, in MultiDamageNodeData data)
        {
            DoHit(context, data.Shape, data.Space, data.Offset, data.HalfExtents, data.Radius, data.Distance, data.HitMask, data.Damage, data.DamageGroupId);
        }

        protected override void OnTick(in SkillExecutionContext context, in MultiDamageNodeData data)
        {
            if (data.HitIntervalTicks <= 0)
                return;

            if (context.ElapsedTicks % data.HitIntervalTicks != 0)
                return;

            DoHit(context, data.Shape, data.Space, data.Offset, data.HalfExtents, data.Radius, data.Distance, data.HitMask, data.Damage, data.DamageGroupId);
        }
    }
}
