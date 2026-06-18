using FishNet.Object.Prediction;
using UnityEngine;

namespace Battle
{
    /// <summary>队伍标识。大乱斗最多 4 队 + 中立。</summary>
    public enum BattleTeam : byte
    {
        Neutral = 0,
        TeamA = 1,
        TeamB = 2,
        TeamC = 3,
        TeamD = 4
    }

    /// <summary>技能指令类型，描述玩家对技能槽的操作阶段。</summary>
    public enum BattleSkillCommandType : byte
    {
        None = 0,
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4
    }

    /// <summary>
    /// 技能指令。由输入层产生，随 Replicate 数据同步，
    /// 驱动 <see cref="BattleSkillController"/> 的技能启动/停止。
    /// </summary>
    public struct BattleSkillCommand
    {
        public BattleSkillCommandType Type;
        public byte Slot;
        public int SkillId;
        public uint SequenceId;
        public uint InputTick;
        public Vector3 AimDirection;
        public Vector3 TargetPoint;
        public ushort ChargeTicks;

        public bool IsSet => Type != BattleSkillCommandType.None && SkillId != 0;
        public static BattleSkillCommand None => default;
    }

    /// <summary>
    /// 技能运行时 reconcile 快照。由 <see cref="BattleSkillController"/> 产生，
    /// 编入 <see cref="BattlePlayerMotor.BattleReconcileData"/> 用于预测回滚校正。
    /// </summary>
    public struct BattleSkillReconcileState
    {
        public int ActiveSkillId;
        public uint ActiveSequenceId;
        public uint StartTick;
        public ushort ElapsedTicks;
        public byte Phase;
        public bool IsActive;
    }

    /// <summary>
    /// 玩家输入的 Replicate 数据。包含移动、瞄准和技能指令，
    /// 由 FishNet 预测系统同步和回放。
    /// </summary>
    public struct BattleReplicateData : IReplicateData
    {
        public Vector2 MoveInput;
        public Vector3 AimDirection;
        public BattleSkillCommand SkillCommand;

        private uint _tick;

        public BattleReplicateData(Vector2 moveInput, Vector3 aimDirection, BattleSkillCommand skillCommand)
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
