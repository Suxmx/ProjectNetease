using Hoshino;
using Hoshino.Skill.Executor;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Provides project services and timeline state to MemoFramework skill executors.
    /// </summary>
    public readonly struct PartySkillExecutionContext : ISkillExecutionContext
    {
        public SkillDefinition Skill { get; }
        public SkillRuntimeNode Node { get; }
        public ESkillNodeLifecyclePhase LifecyclePhase { get; }
        public Transform Actor { get; }
        public PartyAnimancerPresenter AnimancerPresenter { get; }
        public PartyNetworkSkillController NetworkController { get; }
        public int LocalSkillTick { get; }
        public Vector3 AimDirection { get; }

        /// <summary>
        /// Creates an execution context for a skill node lifecycle call.
        /// </summary>
        public PartySkillExecutionContext(SkillDefinition skill, SkillRuntimeNode node, ESkillNodeLifecyclePhase lifecyclePhase, Transform actor, PartyAnimancerPresenter animancerPresenter, PartyNetworkSkillController networkController, int localSkillTick, Vector3 aimDirection)
        {
            Skill = skill;
            Node = node;
            LifecyclePhase = lifecyclePhase;
            Actor = actor;
            AnimancerPresenter = animancerPresenter;
            NetworkController = networkController;
            LocalSkillTick = localSkillTick;
            AimDirection = aimDirection;
        }
    }
}
