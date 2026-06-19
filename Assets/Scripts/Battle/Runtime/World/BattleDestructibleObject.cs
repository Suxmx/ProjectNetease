using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 可破坏场景物体。拥有独立血量，被摧毁时切换碰撞体和显示状态。
    /// 实现 <see cref="IBattleDamageTarget"/> 接收统一伤害分发（无属性集，返回 null）。
    /// 适用于箱子、水晶、障碍物等可被技能/攻击破坏的环境元素。
    /// </summary>
    public sealed class BattleDestructibleObject : NetworkBehaviour, IBattleDamageTarget
    {
        [SerializeField] private int _maxHitPoints = 100;
        [SerializeField] private Collider[] _collidersToDisable;
        [SerializeField] private GameObject[] _objectsToDisable;
        [SerializeField] private GameObject[] _objectsToEnableOnDestroyed;

        private readonly SyncVar<int> _hitPoints = new();
        private readonly SyncVar<bool> _destroyed = new();

        public bool IsDestroyed => _destroyed.Value;

        private void Awake()
        {
            _destroyed.OnChange += Destroyed_OnChange;
        }

        private void OnDestroy()
        {
            _destroyed.OnChange -= Destroyed_OnChange;
        }

        /// <summary>服务端初始化血量和破坏状态。</summary>
        public override void OnStartServer()
        {
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _destroyed.Value = false;
        }

        /// <summary>客户端启动时同步当前破坏状态。</summary>
        public override void OnStartClient()
        {
            ApplyDestroyedState(_destroyed.Value);
        }

        /// <summary>可破坏物无属性集，返回 null。</summary>
        public AttributeSet GetAttributeSet() => null;

        /// <summary>
        /// 施加最终伤害（已缩放），返回是否造成破坏。
        /// 仅由 <see cref="BattleDamageDispatcher.Apply"/> 调用。
        /// </summary>
        bool IBattleDamageTarget.ApplyDamageInternal(int amount, NetworkConnection attacker)
        {
            if (_destroyed.Value || amount <= 0)
                return false;

            int next = Mathf.Max(0, _hitPoints.Value - amount);
            _hitPoints.Value = next;
            if (next == 0)
                _destroyed.Value = true;

            return _destroyed.Value;
        }

        /// <summary>SyncVar 变更回调：同步破坏视觉效果。</summary>
        private void Destroyed_OnChange(bool previous, bool next, bool asServer)
        {
            ApplyDestroyedState(next);
        }

        /// <summary>切换碰撞体启用/禁用和显示对象。</summary>
        private void ApplyDestroyedState(bool destroyed)
        {
            // --- 禁用碰撞体 ---
            foreach (Collider item in _collidersToDisable)
            {
                if (item != null)
                    item.enabled = !destroyed;
            }

            // --- 隐藏正常状态对象 ---
            foreach (GameObject item in _objectsToDisable)
            {
                if (item != null)
                    item.SetActive(!destroyed);
            }

            // --- 显示破坏残骸 ---
            foreach (GameObject item in _objectsToEnableOnDestroyed)
            {
                if (item != null)
                    item.SetActive(destroyed);
            }
        }
    }
}
