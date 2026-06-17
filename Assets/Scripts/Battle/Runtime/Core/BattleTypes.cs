using FishNet.Object.Prediction;
using UnityEngine;

namespace Battle
{
    public enum BattleTeam : byte
    {
        Neutral = 0,
        TeamA = 1,
        TeamB = 2,
        TeamC = 3,
        TeamD = 4
    }

    public enum BattleSkillCommandType : byte
    {
        None = 0,
        Press = 1,
        Hold = 2,
        Release = 3,
        Cancel = 4
    }

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

    public struct BattleSkillReconcileState
    {
        public int ActiveSkillId;
        public uint ActiveSequenceId;
        public uint StartTick;
        public ushort ElapsedTicks;
        public byte Phase;
        public bool IsActive;
    }

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
