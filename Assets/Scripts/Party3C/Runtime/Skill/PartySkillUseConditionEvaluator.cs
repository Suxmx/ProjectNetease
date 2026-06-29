using Hoshino;

namespace Party3C
{
    /// <summary>
    /// Evaluates compiled skill blackboard conditions before local skill startup.
    /// </summary>
    public static class PartySkillUseConditionEvaluator
    {
        /// <summary>
        /// Returns whether all configured skill use conditions pass.
        /// </summary>
        public static bool CanStartSkill(SkillDefinition skill, in PartySkillUseContext context)
        {
            if (skill == null)
                return false;

            SkillRuntimeSpecialData[] specialDatas = skill.SpecialDatas;
            for (int i = 0; i < specialDatas.Length; i++)
            {
                SkillRuntimeSpecialData entry = specialDatas[i];
                if (entry.SpecialDataTypeId != SkillGeneratedIds.SkillUseConditionData)
                    continue;

                if (!SkillGeneratedSerializationServices.Runtime.TryReadSpecialData(skill, entry, out RuntimeSkillUseConditionData data))
                    return false;

                if (!EvaluateUseCondition(data, context))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether one compiled use condition entry passes.
        /// </summary>
        private static bool EvaluateUseCondition(in RuntimeSkillUseConditionData data, in PartySkillUseContext context)
        {
            IPartyKccCharacterMotor motor = context.Motor;
            if (RequiresMotor(data) && motor == null)
                return false;

            if (data.BlockWhileKnockback && motor.CurrentState == EPartyKccCharacterState.Knockback)
                return false;

            if (data.BlockWhileDashing && motor.CurrentState == EPartyKccCharacterState.Dash)
                return false;

            return data.GroundingRequirement switch
            {
                EPartySkillGroundingRequirement.Grounded => motor.IsStableOnGround,
                EPartySkillGroundingRequirement.Airborne => !motor.IsStableOnGround,
                _ => true
            };
        }

        /// <summary>
        /// Returns whether a condition entry needs KCC motor state to be evaluated.
        /// </summary>
        private static bool RequiresMotor(in RuntimeSkillUseConditionData data)
        {
            return data.GroundingRequirement != EPartySkillGroundingRequirement.Any
                   || data.BlockWhileKnockback
                   || data.BlockWhileDashing;
        }
    }
}
