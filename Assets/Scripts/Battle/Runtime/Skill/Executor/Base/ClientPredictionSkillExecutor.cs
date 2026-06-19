using Hoshino;

namespace Battle
{
    /// <summary>
    /// ClientPrediction 域 Executor 基类。
    /// 在客户端和服务器同步执行，参与预测回滚。
    /// 按 Controller 传入的 phase 分发到 OnStart/OnTick/OnEnd。
    /// 用于位移、传送、锁定等需要预测的节点。
    /// </summary>
    public abstract class ClientPredictionSkillExecutor<TData> : BattleSkillNodeExecutor<TData>
        where TData : struct
    {
        protected sealed override void OnExecute(in SkillExecutionContext context, in TData data)
        {
            switch (context.LifecyclePhase)
            {
                case SkillNodeLifecyclePhase.Start:
                    OnStart(context, data);
                    break;
                case SkillNodeLifecyclePhase.Tick:
                    OnTick(context, data);
                    break;
                case SkillNodeLifecyclePhase.End:
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
