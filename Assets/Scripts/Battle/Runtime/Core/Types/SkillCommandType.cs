namespace Battle
{
    /// <summary>技能指令类型，描述玩家对技能槽的操作阶段。</summary>
    public enum SkillCommandType : byte
    {
        None = 0,
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4
    }
}
