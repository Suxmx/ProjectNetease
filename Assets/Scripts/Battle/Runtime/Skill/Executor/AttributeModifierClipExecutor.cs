using Hoshino;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.AttributeModifierClip, SkillNodeExecutionDomain.ServerAuthority)]
    public sealed class AttributeModifierClipExecutor : BattleSkillNodeExecutor<AttributeModifierNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in AttributeModifierNodeData data)
        {
            if (context.AttributeSet == null)
                return;

            context.AttributeSet.ApplyModifier(data.AttributeKey, data.AddValue, data.MultiplyValue, data.DurationSeconds);
        }
    }
}
