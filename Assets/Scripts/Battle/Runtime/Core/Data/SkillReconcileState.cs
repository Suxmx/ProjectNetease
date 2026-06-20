namespace Battle
{
    /// <summary>
    /// 技能运行时 reconcile 快照。由 <see cref="SkillController"/> 产生，
    /// 编入 <see cref="ReconcileData"/> 用于预测回滚校正。
    /// 不含 ElapsedTicks——由 ApplyState 用 FishNet 的 dataTick 和 StartTick 现算，
    /// 避免 CaptureState 在服务器端用 LocalTick 算出混合时间线的错误 elapsed。
    /// </summary>
    public struct SkillReconcileState
    {
        public int ActiveSkillId;
        public uint ActiveSequenceId;
        public uint StartTick;
        public byte Phase;
        public bool IsActive;
    }
}
