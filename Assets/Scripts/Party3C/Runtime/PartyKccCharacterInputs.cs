using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// 保存一帧移动意图，等待 KCC Motor 消费。
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
