namespace Party3C
{
    /// <summary>
    /// Provides runtime state needed to evaluate whether a skill can start.
    /// </summary>
    public readonly struct PartySkillUseContext
    {
        public IPartyKccCharacterMotor Motor { get; }

        /// <summary>
        /// Creates a context for skill use condition evaluation.
        /// </summary>
        public PartySkillUseContext(IPartyKccCharacterMotor motor)
        {
            Motor = motor;
        }
    }
}
