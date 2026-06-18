using Hoshino;
using UnityEngine;

namespace Battle
{
    [BattleSkillExecutor(SkillGeneratedIds.AttributeModifierClip, SkillNodeExecutionDomain.ServerAuthority)]
    public sealed class AttributeModifierClipExecutor : BattleSkillNodeExecutor<AttributeModifierNodeData>
    {
        protected override void Execute(in BattleSkillExecutionContext context, in AttributeModifierNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(AttributeModifierNodeData)} @ elapsed={context.ElapsedTicks} clipId={context.Node.ClipId} domain=ServerAuthority: {JsonUtility.ToJson(data)}");
            // if (context.AttributeSet == null)
            //     return;
            //
            // context.AttributeSet.ApplyModifier(data.AttributeKey, data.AddValue, data.MultiplyValue, data.DurationSeconds);
        }
    }
}
