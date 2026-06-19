namespace Battle
{
    /// <summary>Executor 统一接口，由 <see cref="BattleSkillController"/> 调度。</summary>
    public interface IBattleSkillNodeExecutor
    {
        void Execute(in SkillExecutionContext context);
    }
}
