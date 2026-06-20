using Hoshino;

namespace Battle
{
    /// <summary>
    /// ClientOnly 域 Executor 基类。
    /// 只在客户端执行（含 Host 客户端身份），跳过 replay tick。
    /// 用于特效、音效、动画等纯表现节点。
    /// </summary>
    public abstract class ClientOnlySkillExecutor<TData> : BattleSkillNodeExecutor<TData>
        where TData : struct
    {
        protected sealed override void OnExecute(in SkillExecutionContext context, in TData data)
        {
            switch (context.LifecyclePhase)
            {
                case SkillNodeLifecyclePhase.Start:
                    OnStart(context, data);
                    OnTick(context, data);
                    break;
                case SkillNodeLifecyclePhase.Tick:
                    OnTick(context, data);
                    break;
                case SkillNodeLifecyclePhase.End:
                    OnTick(context, data);
                    OnEnd(context, data);
                    break;
            }
        }

        /// <summary>节点进入 active 区间时调用一次。</summary>
        protected virtual void OnStart(in SkillExecutionContext context, in TData data) { }

        /// <summary>active 区间内每 tick 调用（含 StartTick）。</summary>
        protected virtual void OnTick(in SkillExecutionContext context, in TData data) { }

        /// <summary>节点离开 active 区间时调用一次。</summary>
        protected virtual void OnEnd(in SkillExecutionContext context, in TData data) { }
    }
}
