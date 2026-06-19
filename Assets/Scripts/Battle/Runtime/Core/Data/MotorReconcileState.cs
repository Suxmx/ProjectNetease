using FishNet.Object.Prediction;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Motor 的 reconcile 快照。包含刚体预测状态和朝向，
    /// 由 Motor 产生，通过 Player 编入 ReconcileData 回滚。
    /// </summary>
    public struct MotorReconcileState
    {
        public PredictionRigidbody Rigidbody;
        public Vector3 AimDirection;
    }
}
