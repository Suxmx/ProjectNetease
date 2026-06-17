using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Battle
{
    public sealed class BattleCombatState : NetworkBehaviour
    {
        [SerializeField] private int _maxHitPoints = 100;
        [SerializeField] private BattleTeam _initialTeam = BattleTeam.Neutral;

        private readonly SyncVar<int> _hitPoints = new();
        private readonly SyncVar<bool> _isDead = new();
        private readonly SyncVar<BattleTeam> _team = new();
        private BattleAttributeSet _attributeSet;

        public int MaxHitPoints => _maxHitPoints;
        public int HitPoints => _hitPoints.Value;
        public bool IsDead => _isDead.Value;
        public BattleTeam Team => _team.Value;
        public bool CanAct => !IsDead;

        private void Awake()
        {
            _attributeSet = GetComponent<BattleAttributeSet>();
        }

        public override void OnStartServer()
        {
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _isDead.Value = false;
            _team.Value = _initialTeam;
        }

        [Server]
        public void SetTeam(BattleTeam team)
        {
            _team.Value = team;
        }

        [Server]
        public bool TryTakeDamage(int amount, NetworkConnection attacker)
        {
            float incomingMultiplier = _attributeSet != null ? _attributeSet.IncomingDamageMultiplier : 1f;
            int scaledAmount = Mathf.CeilToInt(amount * Mathf.Max(0f, incomingMultiplier));
            if (_isDead.Value || scaledAmount <= 0)
                return false;

            int next = Mathf.Max(0, _hitPoints.Value - scaledAmount);
            _hitPoints.Value = next;

            if (next == 0)
                _isDead.Value = true;

            return true;
        }

        [Server]
        public void Revive(Vector3 position)
        {
            transform.position = position;
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _isDead.Value = false;
        }
    }
}
