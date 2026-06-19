using Hoshino;

namespace Battle
{
    /// <summary>
    /// ServerOnly 域 Executor 基类。
    /// 只在服务器真实 tick 执行，跳过 replay tick。
    /// 用于伤害、属性修改、生成等服务器权威节点。
    /// </summary>
    public abstract class ServerOnlySkillExecutor<TData> : BattleSkillNodeExecutor<TData>
        where TData : struct
    {
        protected sealed override void OnExecute(in BattleSkillExecutionContext context, in TData data)
        {
            switch (context.LifecyclePhase)
            {
                case BattleSkillNodeLifecyclePhase.Start:
                    OnStart(context, data);
                    break;
                case BattleSkillNodeLifecyclePhase.Tick:
                    OnTick(context, data);
                    break;
                case BattleSkillNodeLifecyclePhase.End:
                    OnEnd(context, data);
                    break;
            }
        }

        /// <summary>节点进入 active 区间时调用一次。</summary>
        protected virtual void OnStart(in BattleSkillExecutionContext context, in TData data) { }

        /// <summary>active 区间内每 tick 调用（含 StartTick）。</summary>
        protected virtual void OnTick(in BattleSkillExecutionContext context, in TData data) { }

        /// <summary>节点离开 active 区间时调用一次。</summary>
        protected virtual void OnEnd(in BattleSkillExecutionContext context, in TData data) { }
    }
}
