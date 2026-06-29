using Hoshino;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Stores skill-level start conditions in the MemoFramework skill blackboard.
    /// </summary>
    [SkillSpecialDataType(2002u)]
    public sealed class SkillUseConditionData
    {
        [SkillCustomData, LabelText("Grounding Requirement")]
        [Tooltip("角色必须满足的接地状态。Any 表示不限制。")]
        public EPartySkillGroundingRequirement GroundingRequirement = EPartySkillGroundingRequirement.Any;

        [SkillCustomData, LabelText("Block While Knockback")]
        [Tooltip("角色处于击退状态时禁止释放该技能。")]
        public bool BlockWhileKnockback = true;

        [SkillCustomData, LabelText("Block While Dashing")]
        [Tooltip("角色处于冲刺状态时禁止释放该技能。")]
        public bool BlockWhileDashing = true;
    }
}
