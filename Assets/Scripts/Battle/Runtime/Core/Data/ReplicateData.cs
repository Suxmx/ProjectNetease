using FishNet.Object.Prediction;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 玩家输入的 Replicate 数据。包含移动、瞄准和技能指令，
    /// 由 FishNet 预测系统同步和回放。
    /// </summary>
    public struct ReplicateData : IReplicateData
    {
        public Vector2 MoveInput;
        public Vector3 AimDirection;
        public SkillCommand SkillCommand;

        private uint _tick;

        public ReplicateData(Vector2 moveInput, Vector3 aimDirection, SkillCommand skillCommand)
        {
            MoveInput = moveInput;
            AimDirection = aimDirection;
            SkillCommand = skillCommand;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }
}
