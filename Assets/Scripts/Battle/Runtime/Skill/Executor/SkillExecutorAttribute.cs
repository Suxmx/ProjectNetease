using System;

namespace Battle
{
    /// <summary>
    /// 标记一个类为技能节点 Executor，绑定 ClipId。
    /// domain 由继承的基类（ClientPredictionSkillExecutor/ClientOnlySkillExecutor/ServerOnlySkillExecutor）自动推断，
    /// 代码生成器扫描此特性 + 继承链生成 <see cref="SkillGeneratedExecutorMetas"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SkillExecutorAttribute : Attribute
    {
        public SkillExecutorAttribute(uint clipId)
        {
            ClipId = clipId;
        }

        public uint ClipId { get; }
    }
}
