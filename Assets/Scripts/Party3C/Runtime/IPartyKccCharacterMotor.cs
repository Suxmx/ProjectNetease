using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// 提供派对 KCC 角色面向玩法层的控制接口。
    /// </summary>
    public interface IPartyKccCharacterMotor
    {
        EPartyKccCharacterState CurrentState { get; }
        Vector3 CurrentVelocity { get; }
        int CurrentDashCharges { get; }
        int MaxDashCharges { get; }
        bool IsStableOnGround { get; }

        /// <summary>
        /// 保存玩家、AI 或网络所有者提供的移动意图，等待下一次 KCC 模拟消费。
        /// </summary>
        void SetInputs(in PartyKccCharacterInputs inputs);

        /// <summary>
        /// 施加最高优先级击退速度，并在指定时间内锁定普通移动输入。
        /// </summary>
        void ApplyKnockback(Vector3 velocity, float lockTime);

        /// <summary>
        /// 缓存一段额外速度，在下一次 KCC 速度更新末尾叠加。
        /// </summary>
        void AddExternalVelocity(Vector3 velocity);

        /// <summary>
        /// 修改冲刺次数上限，并补足新增的可用次数。
        /// </summary>
        void SetDashChargeLimit(int dashChargeLimit);
    }
}
