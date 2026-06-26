using MemoFramework.ThreeC;
using UnityEngine;

namespace MemoFramework.Net3C
{
    /// <summary>
    /// 第三人称客户端权威网络 Motor，自动要求同物体存在第三人称 3C Motor。
    /// </summary>
    [RequireComponent(typeof(MemoThirdPersonMotor3C))]
    public sealed class MemoNetThirdPersonMotor : MemoNetLocalAuthorityMotor
    {
    }
}
