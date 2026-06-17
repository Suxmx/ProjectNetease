using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Battle
{
    public sealed class BattleDestructibleObject : NetworkBehaviour
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

        public override void OnStartServer()
        {
            _hitPoints.Value = Mathf.Max(1, _maxHitPoints);
            _destroyed.Value = false;
        }

        public override void OnStartClient()
        {
            ApplyDestroyedState(_destroyed.Value);
        }

        [Server]
        public bool TryTakeDamage(int amount, NetworkConnection attacker)
        {
            if (_destroyed.Value || amount <= 0)
                return false;

            int next = Mathf.Max(0, _hitPoints.Value - amount);
            _hitPoints.Value = next;
            if (next == 0)
                _destroyed.Value = true;

            return true;
        }

        private void Destroyed_OnChange(bool previous, bool next, bool asServer)
        {
            ApplyDestroyedState(next);
        }

        private void ApplyDestroyedState(bool destroyed)
        {
            foreach (Collider item in _collidersToDisable)
            {
                if (item != null)
                    item.enabled = !destroyed;
            }

            foreach (GameObject item in _objectsToDisable)
            {
                if (item != null)
                    item.SetActive(!destroyed);
            }

            foreach (GameObject item in _objectsToEnableOnDestroyed)
            {
                if (item != null)
                    item.SetActive(destroyed);
            }
        }
    }
}
