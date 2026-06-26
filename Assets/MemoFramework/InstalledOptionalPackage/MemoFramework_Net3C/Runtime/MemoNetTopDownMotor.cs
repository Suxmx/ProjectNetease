using MemoFramework.ThreeC;
using UnityEngine;

namespace MemoFramework.Net3C
{
    /// <summary>
    /// TopDown 客户端权威网络 Motor，自动要求同物体存在 TopDown 3C Motor。
    /// </summary>
    [RequireComponent(typeof(MemoTopDownMotor3C))]
    public sealed class MemoNetTopDownMotor : MemoNetLocalAuthorityMotor
    {
    }
}
