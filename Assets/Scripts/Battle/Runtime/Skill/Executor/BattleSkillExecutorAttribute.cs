using System;
using Hoshino;

namespace Battle
{
    /// <summary>
    /// 标记一个类为技能节点 Executor，绑定 ClipId 和执行域。
    /// 代码生成器扫描此特性生成 <see cref="SkillGeneratedExecutorMetas"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BattleSkillExecutorAttribute : Attribute
    {
        public BattleSkillExecutorAttribute(uint clipId, SkillNodeExecutionDomain domain)
        {
            ClipId = clipId;
            Domain = domain;
        }

        public uint ClipId { get; }
        public SkillNodeExecutionDomain Domain { get; }
    }
}
