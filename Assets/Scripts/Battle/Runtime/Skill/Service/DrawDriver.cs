using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 调试绘制驱动。每帧推动 <see cref="SkillDraw"/> 的命中判定淡出重绘。
    /// 血条/CD 条已迁移到 <see cref="PlayerHudView"/>（UGUI，烘焙在 Prefab 上自管理），
    /// 本驱动仅保留命中判定请求的淡出循环。
    /// </summary>
    public class DrawDriver : MonoBehaviour
    {
        /// <summary>运行时自动创建 DrawDriver，无需手动挂载。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            GameObject go = new GameObject(nameof(DrawDriver));
            go.AddComponent<DrawDriver>();
        }

        private void Update()
        {
            // --- 命中判定请求淡出重绘 ---
            SkillDraw.Tick();
        }
    }
}
