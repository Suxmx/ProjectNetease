using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 属性修改节点 Executor（ServerAuthority 域）。
    /// 在节点起始 tick 于服务端对 AttributeSet 施加临时属性修改。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
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
