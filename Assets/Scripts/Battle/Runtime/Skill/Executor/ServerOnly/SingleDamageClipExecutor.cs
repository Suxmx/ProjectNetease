using Hoshino;

namespace Battle
{
    /// <summary>
    /// 单次伤害判定 Executor（ServerOnly 域）。
    /// 仅在 OnStart 时执行一次伤害判定，无持续判定。
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.SingleDamageClip)]
    public sealed class SingleDamageClipExecutor : DamageExecutorBase<SingleDamageNodeData>
    {
        protected override void OnStart(in SkillExecutionContext context, in SingleDamageNodeData data)
        {
            DoHit(context, data.Shape, data.Space, data.Offset, data.HalfExtents, data.Radius, data.Distance, data.HitMask, data.Damage, data.DamageGroupId);
        }
    }
}
