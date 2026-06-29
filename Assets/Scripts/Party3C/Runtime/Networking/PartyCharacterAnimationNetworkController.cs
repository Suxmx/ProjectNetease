using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Replicates owner-produced base animation snapshots to remote character proxies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCharacterAnimationNetworkController : NetworkBehaviour
    {
        [SerializeField] private PartyCharacterAnimationController _animationController;
        [SerializeField, Min(0.02f)] private float _sendInterval = 0.08f;
        [SerializeField, Min(0f)] private float _normalizedSpeedTolerance = 0.03f;
        [SerializeField, Min(0f)] private float _verticalSpeedTolerance = 0.15f;
        [SerializeField, Min(0f)] private float _localMoveTolerance = 0.03f;

        private PartyCharacterAnimationSnapshot _lastSentSnapshot;
        private float _sendTimer;
        private bool _hasLastSentSnapshot;

        /// <summary>
        /// Applies owner or remote animation driving when this client starts observing the object.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveReferences();
            ApplyLocalAuthority(IsOwner);
        }

        /// <summary>
        /// Refreshes animation driving when FishNet ownership changes.
        /// </summary>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyLocalAuthority(IsOwner);
        }

        /// <summary>
        /// Disables local driving when this client stops observing the object.
        /// </summary>
        public override void OnStopClient()
        {
            ApplyLocalAuthority(false);
            base.OnStopClient();
        }

        /// <summary>
        /// Sends changed owner base animation snapshots at a fixed cadence.
        /// </summary>
        private void Update()
        {
            if (!IsOwner)
                return;

            ResolveReferences();
            if (_animationController == null)
                return;

            _sendTimer += Time.deltaTime;
            if (_sendTimer < _sendInterval)
                return;

            _sendTimer = 0f;
            PartyCharacterAnimationSnapshot snapshot = _animationController.CurrentSnapshot;
            if (_hasLastSentSnapshot && !snapshot.DiffersFrom(_lastSentSnapshot, _normalizedSpeedTolerance, _verticalSpeedTolerance, _localMoveTolerance))
                return;

            _hasLastSentSnapshot = true;
            _lastSentSnapshot = snapshot;
            SendAnimationSnapshotServerRpc((int)snapshot.BaseState, snapshot.IsGrounded, snapshot.NormalizedSpeed, snapshot.VerticalSpeed, snapshot.LocalMove, snapshot.Sequence);
        }

        /// <summary>
        /// Forwards an owner animation snapshot to non-owner observers.
        /// </summary>
        [ServerRpc]
        private void SendAnimationSnapshotServerRpc(int baseState, bool isGrounded, float normalizedSpeed, float verticalSpeed, Vector2 localMove, int sequence)
        {
            ReceiveAnimationSnapshotObserversRpc(baseState, isGrounded, normalizedSpeed, verticalSpeed, localMove, sequence);
        }

        /// <summary>
        /// Applies a replicated base animation snapshot on remote proxies.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void ReceiveAnimationSnapshotObserversRpc(int baseState, bool isGrounded, float normalizedSpeed, float verticalSpeed, Vector2 localMove, int sequence)
        {
            ResolveReferences();
            if (_animationController == null)
                return;

            PartyCharacterAnimationSnapshot snapshot = new((EPartyCharacterBaseMotionState)baseState, isGrounded, normalizedSpeed, verticalSpeed, localMove, sequence);
            _animationController.ApplySnapshot(snapshot);
        }

        /// <summary>
        /// Applies local or remote animation driving for the current ownership state.
        /// </summary>
        private void ApplyLocalAuthority(bool hasAuthority)
        {
            ResolveReferences();
            if (_animationController != null)
                _animationController.SetLocalDrivingEnabled(hasAuthority);
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_animationController == null)
                _animationController = GetComponent<PartyCharacterAnimationController>();
        }
    }
}
