using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    public sealed class BattleProjectile : TickNetworkBehaviour
    {
        [SerializeField] private float _defaultSpeed = 18f;
        [SerializeField] private float _lifeSeconds = 2f;
        [SerializeField] private float _hitRadius = 0.1f;
        [SerializeField] private int _damage = 20;
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField] private bool _destroyOnHit = true;
        [SerializeField] private bool _despawnOnWorldHit = true;
        [SerializeField] private bool _clientVisualMotion = true;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly HashSet<BattleCombatState> _damagedTargets = new();
        private readonly HashSet<BattleDestructibleObject> _damagedObjects = new();
        private BattleCombatState _sourceState;
        private NetworkConnection _sourceConnection;
        private Vector3 _direction = Vector3.forward;
        private float _speed;
        private float _remainingLifeSeconds;
        private bool _clientInitialized;

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnStartServer()
        {
            if (_direction.sqrMagnitude <= 0.0001f)
                _direction = FlattenDirection(transform.forward);
            if (_speed <= 0f)
                _speed = _defaultSpeed;
            if (_remainingLifeSeconds <= 0f)
                _remainingLifeSeconds = _lifeSeconds;

            ObserversInitialize(_direction, _speed);
        }

        private void Update()
        {
            if (!_clientVisualMotion || IsServerStarted || !_clientInitialized)
                return;

            transform.position += _direction * (_speed * Time.deltaTime);
        }

        public void InitializeServer(BattleCombatState sourceState, NetworkConnection sourceConnection, Vector3 direction, float speed = -1f, int damage = -1, float lifeSeconds = -1f)
        {
            if (IsSpawned && !IsServerStarted)
                return;

            _sourceState = sourceState;
            _sourceConnection = sourceConnection ?? (sourceState != null ? sourceState.Owner : null);
            _direction = FlattenDirection(direction);
            _speed = speed > 0f ? speed : _defaultSpeed;
            _damage = damage > 0 ? damage : _damage;
            _remainingLifeSeconds = lifeSeconds > 0f ? lifeSeconds : _lifeSeconds;

            if (IsSpawned)
                ObserversInitialize(_direction, _speed);
        }

        protected override void TimeManager_OnTick()
        {
            if (!IsServerStarted)
                return;

            float delta = (float)TimeManager.TickDelta;
            float distance = _speed * delta;

            if (_direction.sqrMagnitude <= 0.0001f || _remainingLifeSeconds <= 0f)
            {
                Despawn();
                return;
            }

            Vector3 start = transform.position;
            if (TryResolveMovementHits(start, distance))
                return;

            transform.position = start + _direction * distance;
            _remainingLifeSeconds -= delta;
            if (_remainingLifeSeconds <= 0f)
                Despawn();
        }

        [ObserversRpc(BufferLast = true, ExcludeServer = true)]
        private void ObserversInitialize(Vector3 direction, float speed)
        {
            _direction = FlattenDirection(direction);
            _speed = speed;
            _clientInitialized = true;
        }

        private bool TryResolveMovementHits(Vector3 start, float distance)
        {
            int hitCount = _hitRadius > 0f
                ? Physics.SphereCastNonAlloc(start, _hitRadius, _direction, _hits, distance, _hitMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(start, _direction, _hits, distance, _hitMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i].collider;
                if (hit != null && TryApplyHit(hit))
                    return true;
            }

            return false;
        }

        private bool TryApplyHit(Collider hit)
        {
            BattleCombatState target = hit.GetComponentInParent<BattleCombatState>();
            if (target != null)
            {
                if (target == _sourceState || target.IsDead || !_damagedTargets.Add(target))
                    return false;

                target.TryTakeDamage(_damage, _sourceConnection);
                return CompleteHit();
            }

            BattleDestructibleObject destructible = hit.GetComponentInParent<BattleDestructibleObject>();
            if (destructible != null)
            {
                if (destructible.IsDestroyed || !_damagedObjects.Add(destructible))
                    return false;

                destructible.TryTakeDamage(_damage, _sourceConnection);
                return CompleteHit();
            }

            return _despawnOnWorldHit && CompleteHit();
        }

        private bool CompleteHit()
        {
            if (_destroyOnHit)
            {
                Despawn();
                return true;
            }

            return false;
        }

        private static Vector3 FlattenDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
        }
    }
}
