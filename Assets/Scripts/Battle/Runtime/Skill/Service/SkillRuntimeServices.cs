using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 技能运行时服务容器。聚合场景级共享依赖（如命中解析器），
    /// 供 <see cref="SkillController"/> 和 Executor 通过 context.Services 访问。
    /// 挂载于场景中，由 Controller 自动查找。
    /// </summary>
    public sealed class SkillRuntimeServices : MonoBehaviour
    {
        [SerializeField] private LagCompensatedHitResolver _hitResolver;

        /// <summary>获取滞后补偿命中解析器（未指定时自动查找）。</summary>
        public LagCompensatedHitResolver HitResolver
        {
            get
            {
                if (_hitResolver == null)
                    _hitResolver = FindFirstObjectByType<LagCompensatedHitResolver>();

                return _hitResolver;
            }
        }
    }
}
