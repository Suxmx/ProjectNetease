using Hoshino;
using Hoshino.Skill.Executor;

namespace Party3C
{
    /// <summary>
    /// Executes skill timeline Animancer playback nodes.
    /// </summary>
    [SkillExecutor(SkillGeneratedIds.PlayAnimancerClip)]
    public sealed class PlayAnimancerExecutor : PartyPresentationSkillExecutor<PlayAnimancerNodeData>
    {
        /// <summary>
        /// Starts the configured Animancer animation when the timeline node becomes active.
        /// </summary>
        protected override void OnStart(in PartySkillExecutionContext context, in PlayAnimancerNodeData data)
        {
            context.AnimancerPresenter?.PlaySkillAnimation(data, context.LocalSkillTick, context.Node.StartTick, context.Skill.SourceTickRate);
        }

        /// <summary>
        /// Fades out the target semantic action layer when the timeline node ends.
        /// </summary>
        protected override void OnEnd(in PartySkillExecutionContext context, in PlayAnimancerNodeData data)
        {
            context.AnimancerPresenter?.FadeOutAction(data.LayerRole, data.FadeDuration);
        }
    }
}
