namespace Battle
{
    /// <summary>Executor 统一接口，由 <see cref="SkillController"/> 调度。</summary>
    public interface IBattleSkillNodeExecutor
    {
        void Execute(in SkillExecutionContext context);
    }
}
