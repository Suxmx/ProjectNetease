namespace Battle
{
    /// <summary>
    /// 节点生命周期阶段。由 Controller 传入 context，domain 基类据此分发到 OnStart/OnTick/OnEnd。
    /// </summary>
    public enum SkillNodeLifecyclePhase : byte
    {
        Start = 0,
        Tick = 1,
        End = 2
    }
}
