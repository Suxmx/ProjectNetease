using MemoFramework.ThreeC;
using UnityEngine;

namespace MemoFramework.Net3C
{
    /// <summary>
    /// 第一人称客户端权威网络 Motor，自动要求同物体存在第一人称 3C Motor。
    /// </summary>
    [RequireComponent(typeof(MemoFirstPersonMotor3C))]
    public sealed class MemoNetFirstPersonMotor : MemoNetLocalAuthorityMotor
    {
    }
}
