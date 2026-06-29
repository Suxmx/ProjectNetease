using System;

namespace Party3C
{
    /// <summary>
    /// Defines temporary KCC motor controls disabled by skill timeline nodes.
    /// </summary>
    [Flags]
    public enum EPartyKccMotorRestriction
    {
        None = 0,
        Movement = 1 << 0,
        Jump = 1 << 1,
        Dash = 1 << 2
    }
}
