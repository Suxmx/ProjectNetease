using Hoshino.Skill.Executor;

namespace Party3C
{
    /// <summary>
    /// Base class for skill executors that only drive local visual presentation.
    /// </summary>
    [SkillExecutorDomain((int)EPartySkillExecutionDomain.Presentation)]
    public abstract class PartyPresentationSkillExecutor<TData> : LifecycleSkillNodeExecutor<PartySkillExecutionContext, TData>
        where TData : struct
    {
    }
}
