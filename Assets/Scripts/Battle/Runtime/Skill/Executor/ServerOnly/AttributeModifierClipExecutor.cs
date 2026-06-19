using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 属性修改节点 Executor（ServerOnly 域）。
    /// 在节点 OnStart 时于服务端对 AttributeSet 施加临时属性修改。
    /// 当前为测试态：仅 log 节点数据，实际功能代码已注释。
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.AttributeModifierClip)]
    public sealed class AttributeModifierClipExecutor : ServerOnlySkillExecutor<AttributeModifierNodeData>
    {
        protected override void OnStart(in SkillExecutionContext context, in AttributeModifierNodeData data)
        {
            Debug.Log($"[SkillTest] {nameof(AttributeModifierNodeData)} @ elapsed={context.ElapsedTicks} phase={context.LifecyclePhase}: {JsonUtility.ToJson(data)}");
            // if (context.AttributeSet == null)
            //     return;
            //
            // context.AttributeSet.ApplyModifier(data.AttributeKey, data.AddValue, data.MultiplyValue, data.DurationSeconds);
        }
    }
}
