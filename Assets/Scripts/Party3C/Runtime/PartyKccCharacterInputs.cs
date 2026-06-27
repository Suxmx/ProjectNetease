using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Stores one frame of movement intent before the KCC motor consumes it.
    /// </summary>
    public struct PartyKccCharacterInputs
    {
        public Vector3 MoveWorld;
        public Vector3 LookWorld;
        public bool RunHeld;
        public bool JumpPressed;
        public bool DashPressed;
    }
}
