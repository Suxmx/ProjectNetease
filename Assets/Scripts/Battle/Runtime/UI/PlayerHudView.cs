using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Battle
{
    /// <summary>
    /// 角色头顶血条与技能 CD 条的 UGUI 显示组件。
    /// 替代旧的 ALINE Label2D 文字方案，改用 World Space Canvas + Image 填充条 + TMP 数字。
    /// HUD 的 Canvas/Image/TMP 层级由编辑器工具烘焙在 NetPlayer Prefab 的 Visual 子级下，
    /// 本组件只持有引用，运行时零动态构建：每帧根据 <see cref="CombatState"/> 与
    /// <see cref="SkillController"/> 状态更新填充与文字，并做 Billboard 始终面向本地相机。
    /// </summary>
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GameObject _healthBar;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private GameObject _cooldownBar;
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private TextMeshProUGUI _cooldownText;
        [SerializeField] private bool _showLocalPlayer = true;

        private CombatState _combat;
        private SkillController _skill;
        private Player _player;
        private Camera _mainCamera;
        private float _cameraRefreshTimer;

        private void Awake()
        {
            _combat = GetComponent<CombatState>();
            _skill = GetComponent<SkillController>();
            _player = GetComponent<Player>();
        }

        private void Update()
        {
            // --- 服务器/headless 无相机，不渲染 HUD ---
            Camera cam = GetMainCamera();
            if (cam == null)
            {
                if (_canvas != null) _canvas.enabled = false;
                return;
            }

            if (_canvas == null)
                return;

            // --- 本地玩家可视性 ---
            if (_player != null && _player.IsOwner && !_showLocalPlayer)
            {
                _canvas.gameObject.SetActive(false);
                return;
            }
            _canvas.gameObject.SetActive(true);

            // --- 血条 ---
            UpdateHealth();

            // --- CD 条 ---
            UpdateCooldown();

            // --- Billboard：Canvas 正面（-Z）朝向本地相机，故 +Z 背对相机 ---
            _canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - cam.transform.position);
        }

        /// <summary>更新血条填充、颜色、文字，死亡时隐藏。</summary>
        private void UpdateHealth()
        {
            if (_combat == null || _combat.MaxHitPoints <= 0)
            {
                _healthBar.SetActive(false);
                return;
            }

            bool dead = _combat.IsDead;
            _healthBar.SetActive(!dead);
            if (dead)
                return;

            float ratio = Mathf.Clamp01((float)_combat.HitPoints / _combat.MaxHitPoints);
            _healthFill.fillAmount = ratio;
            _healthFill.color = Color.Lerp(Color.red, Color.green, ratio);
            _healthText.text = $"{_combat.HitPoints}/{_combat.MaxHitPoints}";
        }

        /// <summary>更新 CD 条：技能激活时显示，填充随剩余进度递减。</summary>
        private void UpdateCooldown()
        {
            if (_skill == null || !_skill.IsActive)
            {
                _cooldownBar.SetActive(false);
                return;
            }

            _cooldownBar.SetActive(true);
            uint tick = _player != null ? _player.TimeManager.LocalTick : 0;
            var (current, total) = _skill.GetCooldownTicks(tick);
            float progress = _skill.GetCooldownProgress(tick);
            _cooldownFill.fillAmount = 1f - progress;
            _cooldownText.text = $"{current}/{total}";
        }

        /// <summary>获取主相机，每 0.5s 刷新一次缓存。</summary>
        private Camera GetMainCamera()
        {
            _cameraRefreshTimer -= Time.deltaTime;
            if (_mainCamera == null || _cameraRefreshTimer <= 0f)
            {
                _mainCamera = Camera.main;
                _cameraRefreshTimer = 0.5f;
            }
            return _mainCamera;
        }
    }
}
