using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能指令。由输入层产生，随 Replicate 数据同步，
    /// 驱动 <see cref="SkillController"/> 的技能启动/停止。
    /// </summary>
    public struct SkillCommand
    {
        public SkillCommandType Type;
        public byte Slot;
        public int SkillId;
        public uint SequenceId;
        public uint InputTick;
        public Vector3 AimDirection;
        public Vector3 TargetPoint;
        public ushort ChargeTicks;

        public bool IsSet => Type != SkillCommandType.None && SkillId != 0;
        public static SkillCommand None => default;
    }
}
