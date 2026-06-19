using FishNet.Object.Prediction;

namespace Battle
{
    /// <summary>
    /// 玩家 Reconcile 快照数据。包含 Motor 状态和技能状态，
    /// 由 Player 产生，服务端回滚时分发到各子组件。
    /// </summary>
    public struct ReconcileData : IReconcileData
    {
        public MotorReconcileState MotorState;
        public SkillReconcileState SkillState;
        public bool IsDead;

        private uint _tick;

        public ReconcileData(MotorReconcileState motorState, SkillReconcileState skillState, bool isDead)
        {
            MotorState = motorState;
            SkillState = skillState;
            IsDead = isDead;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }
}
