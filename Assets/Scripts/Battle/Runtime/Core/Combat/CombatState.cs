using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 角色战斗状态。管理血量、死亡、队伍归属，
    /// 实现 <see cref="IBattleDamageTarget"/> 接收统一伤害分发。
    /// 挂载于角色 prefab，与 <see cref="AttributeSet"/> 同级。
    /// </summary>
    public sealed class CombatState : NetworkBehaviour, IBattleDamageTarget
    {
        [SerializeField] private int _maxHitPoints = 100;
        [SerializeField] private ETeam _initialTeam = ETeam.Neutral;

        private readonly SyncVar<int> _hitPoints = new();
        private readonly SyncVar<bool> _isDead = new();
        private readonly SyncVar<ETeam> _team = new();
        private AttributeSet _attributeSet;

        public int MaxHitPoints => _maxHitPoints;
        public int HitPoints => _hitPoints.Value;
        public bool IsDead => _isDead.Value;
        public ETeam Team => _team.Value;
        public bool CanAct => !IsDead;

        private void Awake()
        {
            _attributeSet = GetComponent<AttributeSet>();
        }

        /// <summary>服务端初始化血量和队伍。</summary>
        public override void OnStartServer()
        {
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _isDead.Value = false;
            _team.Value = _initialTeam;
        }

        /// <summary>服务端设置队伍归属。</summary>
        [Server]
        public void SetTeam(ETeam team)
        {
            _team.Value = team;
        }

        /// <summary>获取挂载的属性集，供 <see cref="DamageDispatcher"/> 查询抗性。</summary>
        public AttributeSet GetAttributeSet() => _attributeSet;

        /// <summary>
        /// 施加最终伤害（已缩放），返回是否致命。
        /// 仅由 <see cref="DamageDispatcher.Apply"/> 调用，外部不直接调。
        /// </summary>
        bool IBattleDamageTarget.ApplyDamageInternal(int amount, NetworkConnection attacker)
        {
            if (_isDead.Value || amount <= 0)
                return false;

            int next = Mathf.Max(0, _hitPoints.Value - amount);
            _hitPoints.Value = next;

            bool lethal = next == 0;
            if (lethal)
                _isDead.Value = true;

            return lethal;
        }

        /// <summary>服务端复活：重置血量、位置、死亡状态。</summary>
        [Server]
        public void Revive(Vector3 position)
        {
            transform.position = position;
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _isDead.Value = false;
        }
    }
}
