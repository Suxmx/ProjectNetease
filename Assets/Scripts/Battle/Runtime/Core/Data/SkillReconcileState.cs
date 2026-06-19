namespace Battle
{
    /// <summary>
    /// 技能运行时 reconcile 快照。由 <see cref="SkillController"/> 产生，
    /// 编入 <see cref="ReconcileData"/> 用于预测回滚校正。
    /// </summary>
    public struct SkillReconcileState
    {
        public int ActiveSkillId;
        public uint ActiveSequenceId;
        public uint StartTick;
        public ushort ElapsedTicks;
        public byte Phase;
        public bool IsActive;
    }
}
