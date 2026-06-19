using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 调试绘制驱动。每帧推动 <see cref="SkillDraw"/> 的命中判定淡出重绘，
    /// 并遍历场景中所有 <see cref="Player"/> 绘制头顶血量数字与技能 CD 数字。
    /// 使用 TickSmoother 平滑后的 Visual 子级位置，避免抖动。
    /// </summary>
    public class DrawDriver : MonoBehaviour
    {
        private Player[] _players = System.Array.Empty<Player>();
        private float _playerCacheTimer;

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

            // --- 血量 / CD 数字（每帧持续绘制，用平滑后位置）---
            RefreshPlayerCache();
            foreach (Player player in _players)
            {
                if (player == null)
                    continue;

                // --- 找平滑后的 Visual 子级 ---
                Transform smoothTransform = FindSmoothTransform(player.transform);

                // 血量
                CombatState combat = player.GetComponent<CombatState>();
                if (combat != null && combat.MaxHitPoints > 0)
                    SkillDraw.HealthBar(smoothTransform, combat.HitPoints, combat.MaxHitPoints);

                // 技能 CD
                SkillController skill = player.GetComponent<SkillController>();
                if (skill != null && skill.IsActive)
                {
                    var (current, total) = skill.GetCooldownTicks(player.TimeManager.LocalTick);
                    SkillDraw.CooldownBar(smoothTransform, current, total);
                }
            }
        }

        /// <summary>查找 Player 下的 Visual 子级（TickSmoother 平滑后的图形对象）。</summary>
        private static Transform FindSmoothTransform(Transform root)
        {
            Transform visual = root.Find("Visual");
            return visual != null ? visual : root;
        }

        /// <summary>每 0.5s 刷新一次 Player 缓存，避免每帧 FindObjectsByType。</summary>
        private void RefreshPlayerCache()
        {
            _playerCacheTimer -= Time.deltaTime;
            if (_playerCacheTimer > 0f)
                return;

            _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
            _playerCacheTimer = 0.5f;
        }
    }
}
