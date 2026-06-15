using Drawing;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NetDemo
{
    public sealed class NetDemoHealth : NetworkBehaviour
    {
        [SerializeField] private int _maxHitPoints = 100;

        [Header("ALINE Health Label")]
        [SerializeField] private bool _drawHealthLabel = true;
        [SerializeField] private float _healthLabelVerticalOffset = 1.05f;
        [SerializeField] private float _healthLabelSize = 16f;

        private readonly SyncVar<int> _hitPoints = new(100);
        private readonly SyncVar<bool> _isDead = new(false);

        private NetDemoPlayerVisual _visual;
        private Transform _healthLabelAnchor;

        public int CurrentHitPoints => _hitPoints.Value;
        public bool IsDead => _isDead.Value;

        private void Awake()
        {
            _visual = GetComponentInChildren<NetDemoPlayerVisual>();
            _healthLabelAnchor = ResolveHealthLabelAnchor();
            _hitPoints.OnChange += HitPoints_OnChange;
            _isDead.OnChange += IsDead_OnChange;
        }

        private void OnDestroy()
        {
            _hitPoints.OnChange -= HitPoints_OnChange;
            _isDead.OnChange -= IsDead_OnChange;
        }

        private void Update()
        {
            if (!_drawHealthLabel)
                return;

            Transform anchor = _healthLabelAnchor != null ? _healthLabelAnchor : transform;
            Vector3 position = anchor.position + Vector3.up * _healthLabelVerticalOffset;
            Draw.ingame.Label2D(position, $"HP {CurrentHitPoints}/{_maxHitPoints}", _healthLabelSize, LabelAlignment.Center, GetHealthLabelColor());
        }

        public override void OnStartServer()
        {
            _hitPoints.Value = _maxHitPoints;
            _isDead.Value = false;
        }

        [Server]
        public bool TryTakeDamage(int amount, NetworkConnection attacker)
        {
            if (_isDead.Value)
                return false;
            if (amount <= 0)
                return false;

            int previous = _hitPoints.Value;
            int next = Mathf.Max(0, previous - amount);
            _hitPoints.Value = next;

            int attackerId = attacker == null ? -1 : attacker.ClientId;
            int victimId = Owner.IsValid ? Owner.ClientId : -1;
            Debug.Log($"[NetDemo] Player {victimId} took {amount} damage from player {attackerId}. HP {previous} -> {next}.");

            if (next <= 0)
            {
                _isDead.Value = true;
                Debug.Log($"[NetDemo] Player {victimId} died.");
            }

            return true;
        }

        private void HitPoints_OnChange(int previous, int next, bool asServer)
        {
            if (next < previous)
                _visual?.PlayHit();
        }

        private void IsDead_OnChange(bool previous, bool next, bool asServer)
        {
            if (next)
                _visual?.PlayDeath();
        }

        private Color GetHealthLabelColor()
        {
            if (_isDead.Value)
                return Color.gray;

            float ratio = _maxHitPoints <= 0 ? 0f : Mathf.Clamp01((float)_hitPoints.Value / _maxHitPoints);
            return Color.Lerp(Color.red, Color.green, ratio);
        }

        private Transform ResolveHealthLabelAnchor()
        {
            if (_visual != null)
                return _visual.transform;

            return transform;
        }
    }
}
