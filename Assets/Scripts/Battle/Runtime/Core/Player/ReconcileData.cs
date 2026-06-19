using FishNet.Object.Prediction;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Reconcile 快照数据。包含刚体状态、技能状态、朝向和死亡标记，
    /// 由服务端产生并回滚客户端预测。
    /// </summary>
    public struct ReconcileData : IReconcileData
    {
        public PredictionRigidbody Rigidbody;
        public SkillReconcileState SkillState;
        public Vector3 AimDirection;
        public bool IsDead;

        private uint _tick;

        public ReconcileData(PredictionRigidbody rigidbody, SkillReconcileState skillState, Vector3 aimDirection, bool isDead)
        {
            Rigidbody = rigidbody;
            SkillState = skillState;
            AimDirection = aimDirection;
            IsDead = isDead;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }
}
