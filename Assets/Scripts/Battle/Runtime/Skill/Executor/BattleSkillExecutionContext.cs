using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能节点执行上下文。Executor 通过此结构访问运行时依赖
    /// （Motor/CombatState/AttributeSet/Services/技能数据/输入/时间等），
    /// 保持 Executor 插件化，不直接依赖场景单例。
    /// </summary>
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

        /// <summary>当前 tick 是否为节点起始 tick。</summary>
        public bool IsNodeStartTick => ElapsedTicks == Node.StartTick;

        /// <summary>返回非负的伤害原始值（缩放由 <see cref="BattleDamageDispatcher"/> 统一处理）。</summary>
        public int ScaleOutgoingDamage(int amount)
        {
            return Mathf.Max(0, amount);
        }

        /// <summary>构造伤害信息，自动填充 Source/SourceConnection/SourceClipId/Tick。</summary>
        public BattleDamageInfo CreateDamageInfo(int amount, BattleDamageType type, Vector3 hitPoint)
        {
            return new BattleDamageInfo
            {
                Type = type,
                Amount = amount,
                Source = CombatState,
                Target = null,
                SourceConnection = Motor != null ? Motor.Owner : default,
                SourceClipId = Node.ClipId,
                HitPoint = hitPoint,
                Tick = CurrentTick
            };
        }

        /// <summary>获取当前 tick 对应的 PreciseTick，用于滞后补偿查询。</summary>
        public PreciseTick GetCurrentPreciseTick()
        {
            uint queryTick = CurrentTick != 0u ? CurrentTick : Command.InputTick;
            return Motor.TimeManager.GetPreciseTick(queryTick);
        }
    }
}
