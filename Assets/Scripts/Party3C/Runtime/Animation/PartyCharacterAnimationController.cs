using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Drives the local layered Animancer state machine from KCC data or replicated animation snapshots.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCharacterAnimationController : MonoBehaviour
    {
        [SerializeField] private PartyKccCharacterController _character;
        [SerializeField] private PartyAnimancerPresenter _presenter;
        [SerializeField] private PartyCharacterAnimationProfile _profile;
        [SerializeField] private bool _driveFromLocalCharacter = true;
        [SerializeField, Min(0f)] private float _idleSpeedThreshold = 0.05f;

        private int _snapshotSequence;

        /// <summary>
        /// Gets the animation profile used by this controller.
        /// </summary>
        public PartyCharacterAnimationProfile Profile => _profile;

        /// <summary>
        /// Gets the most recently applied base motion snapshot.
        /// </summary>
        public PartyCharacterAnimationSnapshot CurrentSnapshot { get; private set; }

        /// <summary>
        /// Assigns runtime references used by the animation controller.
        /// </summary>
        public void Configure(PartyKccCharacterController character, PartyAnimancerPresenter presenter, PartyCharacterAnimationProfile profile)
        {
            _character = character;
            _presenter = presenter;
            _profile = profile;

            if (_presenter != null && _profile != null)
                _presenter.ConfigureAnimationProfile(_profile);
        }

        /// <summary>
        /// Enables or disables local KCC-driven animation sampling.
        /// </summary>
        public void SetLocalDrivingEnabled(bool enabled)
        {
            _driveFromLocalCharacter = enabled;
        }

        /// <summary>
        /// Builds a base motion snapshot from the current local KCC state.
        /// </summary>
        public PartyCharacterAnimationSnapshot BuildSnapshotFromCharacter()
        {
            ResolveReferences();
            if (_character == null)
                return CurrentSnapshot.WithSequence(++_snapshotSequence);

            Vector3 up = _character.CharacterUp;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(_character.CurrentVelocity, up);
            float planarSpeed = planarVelocity.magnitude;
            float normalizedSpeed = _character.RunSpeed > 0.0001f ? Mathf.Clamp01(planarSpeed / _character.RunSpeed) : 0f;
            Vector3 localPlanarVelocity = transform.InverseTransformDirection(planarVelocity);
            EPartyCharacterBaseMotionState baseState = ResolveBaseMotionState(planarSpeed);

            return new PartyCharacterAnimationSnapshot(
                baseState,
                _character.IsStableOnGround,
                normalizedSpeed,
                _character.VerticalSpeed,
                new Vector2(localPlanarVelocity.x, localPlanarVelocity.z),
                ++_snapshotSequence);
        }

        /// <summary>
        /// Applies a replicated or locally sampled base motion snapshot to Animancer.
        /// </summary>
        public void ApplySnapshot(in PartyCharacterAnimationSnapshot snapshot)
        {
            CurrentSnapshot = snapshot;
            ResolveReferences();
            if (_presenter != null)
                _presenter.PlayBaseMotion(snapshot);
        }

        /// <summary>
        /// Applies local KCC data to the base layer when this object has owner authority.
        /// </summary>
        private void Update()
        {
            if (!_driveFromLocalCharacter)
                return;

            ApplySnapshot(BuildSnapshotFromCharacter());
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_character == null)
                _character = GetComponent<PartyKccCharacterController>();

            if (_presenter == null)
                _presenter = GetComponentInChildren<PartyAnimancerPresenter>();

            if (_presenter != null && _profile != null && _presenter.AnimationProfile != _profile)
                _presenter.ConfigureAnimationProfile(_profile);
        }

        /// <summary>
        /// Converts gameplay movement state into the base animation state.
        /// </summary>
        private EPartyCharacterBaseMotionState ResolveBaseMotionState(float planarSpeed)
        {
            if (_character.CurrentState == EPartyKccCharacterState.Dash)
                return EPartyCharacterBaseMotionState.Dash;

            if (_character.CurrentState == EPartyKccCharacterState.Knockback)
                return EPartyCharacterBaseMotionState.Knockback;

            if (!_character.IsStableOnGround)
                return EPartyCharacterBaseMotionState.Airborne;

            return planarSpeed > _idleSpeedThreshold ? EPartyCharacterBaseMotionState.Locomotion : EPartyCharacterBaseMotionState.Idle;
        }
    }
}
