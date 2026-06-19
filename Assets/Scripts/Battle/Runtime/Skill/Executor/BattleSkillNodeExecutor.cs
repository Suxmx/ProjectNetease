using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 泛型 Executor 基类。自动从 NodeDataBlob 反序列化节点数据并传给子类。
    /// 新增技能节点时继承此类并实现抽象 <see cref="Execute(in BattleSkillExecutionContext, in TData)"/>。
    /// </summary>
    /// <typeparam name="TData">节点数据结构（生成的 XxxNodeData）。</typeparam>
    public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
        where TData : struct
    {
        /// <summary>反序列化节点数据并分发给子类。</summary>
        public void Execute(in BattleSkillExecutionContext context)
        {
            if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
            {
                Debug.LogWarning($"[BattleSkill] Node data mismatch. Expected {typeof(TData).Name} for clip id {context.Node.ClipId}.");
                return;
            }

            Execute(context, in data);
        }

        /// <summary>子类实现具体节点逻辑。</summary>
        protected abstract void Execute(in BattleSkillExecutionContext context, in TData data);
    }
}
