using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 泛型 Executor 根基类。自动从 NodeDataBlob 反序列化节点数据并传给子类。
    /// 不直接继承此类——必须继承 <see cref="ClientPredictionSkillExecutor{TData}"/>/
    /// <see cref="ClientOnlySkillExecutor{TData}"/>/<see cref="ServerOnlySkillExecutor{TData}"/> 之一，
    /// 由 domain 基类 sealed override <see cref="OnExecute"/> 分发生命周期。
    /// </summary>
    /// <typeparam name="TData">节点数据结构（生成的 XxxNodeData）。</typeparam>
    public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
        where TData : struct
    {
        /// <summary>反序列化节点数据并分发给 domain 基类。</summary>
        public void Execute(in BattleSkillExecutionContext context)
        {
            if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
            {
                Debug.LogWarning($"[BattleSkill] Node data mismatch. Expected {typeof(TData).Name} for clip id {context.Node.ClipId}.");
                return;
            }

            OnExecute(context, in data);
        }

        /// <summary>domain 基类 sealed override 此方法，按 phase 分发到 OnStart/OnTick/OnEnd。</summary>
        protected abstract void OnExecute(in BattleSkillExecutionContext context, in TData data);
    }
}
