using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    public readonly struct BattleSkillExecutionContext
    {
        public BattleSkillExecutionContext(
            BattlePlayerMotor motor,
            BattleSkillController controller,
            BattleCombatState combatState,
            BattleAttributeSet attributeSet,
            BattleSkillRuntimeServices services,
            SkillDefinition skill,
            BattleSkillCommand command,
            SkillRuntimeNode node,
            Vector3 aimDirection,
            uint currentTick,
            int elapsedTicks,
            float delta,
            ReplicateState replicateState)
        {
            Motor = motor;
            Controller = controller;
            CombatState = combatState;
            AttributeSet = attributeSet;
            Services = services;
            Skill = skill;
            Command = command;
            Node = node;
            AimDirection = aimDirection;
            CurrentTick = currentTick;
            ElapsedTicks = elapsedTicks;
            Delta = delta;
            ReplicateState = replicateState;
        }

        public BattlePlayerMotor Motor { get; }
        public BattleSkillController Controller { get; }
        public BattleCombatState CombatState { get; }
        public BattleAttributeSet AttributeSet { get; }
        public BattleSkillRuntimeServices Services { get; }
        public SkillDefinition Skill { get; }
        public BattleSkillCommand Command { get; }
        public SkillRuntimeNode Node { get; }
        public Vector3 AimDirection { get; }
        public uint CurrentTick { get; }
        public int ElapsedTicks { get; }
        public float Delta { get; }
        public ReplicateState ReplicateState { get; }

        public bool IsNodeStartTick => ElapsedTicks == Node.StartTick;

        public int ScaleOutgoingDamage(int amount)
        {
            float multiplier = AttributeSet != null ? AttributeSet.OutgoingDamageMultiplier : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(amount * Mathf.Max(0f, multiplier)));
        }

        public PreciseTick GetCurrentPreciseTick()
        {
            uint queryTick = CurrentTick != 0u ? CurrentTick : Command.InputTick;
            return Motor.TimeManager.GetPreciseTick(queryTick);
        }
    }
}
