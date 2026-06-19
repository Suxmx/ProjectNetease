using System;

namespace MemoFramework.Extension
{
    [Flags]
    public enum InputEvent
    {
        None = 0,
        Move = 1,
        Shoot = 1 << 1,
    }


    [Flags]
    public enum UIInputEvent
    {
        None = 0,
        UIReturn = 1,
        ViewStat = 1 << 1,
    }
}