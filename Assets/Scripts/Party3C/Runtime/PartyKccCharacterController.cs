using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Owns KCC locomotion for party brawler characters and exposes extension points for combat and networking.
    /// </summary>
    public sealed class PartyKccCharacterController : MonoBehaviour, ICharacterController, IPartyKccCharacterMotor
    {
        [Header("References")]
        [SerializeField] private KinematicCharacterMotor _motor;
        [SerializeField] private Transform _meshRoot;
        [SerializeField] private Transform _cameraFollowPoint;

        [Header("Camera Target")]
        [SerializeField] private bool _stabilizeCameraFollowPointRotation = true;
        [SerializeField] private float _cameraYawSensitivity = 0.12f;
        [SerializeField] private float _cameraPitchSensitivity = 0.08f;
        [SerializeField] private float _cameraMinPitch = -25f;
        [SerializeField] private float _cameraMaxPitch = 65f;

        [Header("Stable Movement")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 8f;
        [SerializeField] private float _stableMovementSharpness = 15f;
        [SerializeField] private float _rotationSharpness = 12f;

        [Header("Air Movement")]
        [SerializeField] private float _maxAirMoveSpeed = 7f;
        [SerializeField] private float _airAccelerationSpeed = 18f;
        [SerializeField] private float _airDrag = 0.1f;
        [SerializeField] private Vector3 _gravity = new Vector3(0f, -30f, 0f);

        [Header("Jump")]
        [SerializeField] private int _maxJumpCount = 2;
        [SerializeField] private float _jumpSpeed = 10f;
        [SerializeField] private float _coyoteTime = 0.12f;
        [SerializeField] private float _jumpRequestBufferTime = 0.12f;

        [Header("Dash")]
        [SerializeField] private int _maxDashCharges = 1;
        [SerializeField] private float _dashSpeed = 13f;
        [SerializeField] private float _dashDuration = 0.18f;
        [SerializeField] private float _dashRechargeTime = 1f;

        [Header("Knockback")]
        [SerializeField] private float _knockbackDrag = 0.02f;

        [Header("Collision")]
        [SerializeField] private List<Collider> _ignoredColliders = new();

        private PartyKccCharacterInputs _inputs;
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private Vector3 _dashDirection;
        private Vector3 _knockbackVelocity;
        private Vector3 _externalVelocityAdd;
        private bool _jumpRequested;
        private bool _dashRequested;
        private bool _jumpedThisFrame;
        private float _jumpRequestAge = Mathf.Infinity;
        private float _timeSinceLastStableGrounded = Mathf.Infinity;
        private float _dashTimeRemaining;
        private float _dashRechargeTimer;
        private float _knockbackTimeRemaining;
        private int _jumpCountUsed;
        private int _dashCharges;
        private float _cameraYaw;
        private float _cameraPitch;

        public EPartyKccCharacterState CurrentState { get; private set; } = EPartyKccCharacterState.Default;
        public Vector3 CurrentVelocity => _motor != null ? _motor.BaseVelocity : Vector3.zero;
        public int CurrentDashCharges => _dashCharges;
        public int MaxDashCharges => _maxDashCharges;
        public bool IsStableOnGround => _motor != null && _motor.GroundingStatus.IsStableOnGround;
        public Transform CameraFollowPoint => _cameraFollowPoint;
        public Vector3 CharacterUp => _motor != null ? _motor.CharacterUp : transform.up;

        /// <summary>
        /// Assigns serialized references created by editor tooling or custom spawners.
        /// </summary>
        public void ConfigureReferences(KinematicCharacterMotor motor, Transform meshRoot, Transform cameraFollowPoint)
        {
            _motor = motor;
            _meshRoot = meshRoot;
            _cameraFollowPoint = cameraFollowPoint;

            if (_motor != null)
                _motor.CharacterController = this;
        }

        /// <summary>
        /// Finds the KCC motor and initializes reusable charge counters.
        /// </summary>
        private void Awake()
        {
            if (_motor == null)
                _motor = GetComponent<KinematicCharacterMotor>();

            if (_motor != null)
                _motor.CharacterController = this;

            _maxJumpCount = Mathf.Max(0, _maxJumpCount);
            _maxDashCharges = Mathf.Max(0, _maxDashCharges);
            _dashCharges = _maxDashCharges;
            InitializeCameraFollowRotation();
            ApplyCameraFollowPointRotation();
        }

        /// <summary>
        /// Keeps inspector values in valid ranges while editing.
        /// </summary>
        private void OnValidate()
        {
            _walkSpeed = Mathf.Max(0f, _walkSpeed);
            _cameraYawSensitivity = Mathf.Max(0f, _cameraYawSensitivity);
            _cameraPitchSensitivity = Mathf.Max(0f, _cameraPitchSensitivity);
            if (_cameraMinPitch > _cameraMaxPitch)
            {
                float previousMinPitch = _cameraMinPitch;
                _cameraMinPitch = _cameraMaxPitch;
                _cameraMaxPitch = previousMinPitch;
            }

            _runSpeed = Mathf.Max(_walkSpeed, _runSpeed);
            _stableMovementSharpness = Mathf.Max(0f, _stableMovementSharpness);
            _rotationSharpness = Mathf.Max(0f, _rotationSharpness);
            _maxAirMoveSpeed = Mathf.Max(0f, _maxAirMoveSpeed);
            _airAccelerationSpeed = Mathf.Max(0f, _airAccelerationSpeed);
            _airDrag = Mathf.Max(0f, _airDrag);
            _maxJumpCount = Mathf.Max(0, _maxJumpCount);
            _jumpSpeed = Mathf.Max(0f, _jumpSpeed);
            _coyoteTime = Mathf.Max(0f, _coyoteTime);
            _jumpRequestBufferTime = Mathf.Max(0f, _jumpRequestBufferTime);
            _maxDashCharges = Mathf.Max(0, _maxDashCharges);
            _dashSpeed = Mathf.Max(0f, _dashSpeed);
            _dashDuration = Mathf.Max(0.01f, _dashDuration);
            _dashRechargeTime = Mathf.Max(0.01f, _dashRechargeTime);
            _knockbackDrag = Mathf.Max(0f, _knockbackDrag);
        }

        /// <summary>
        /// Reapplies the camera target rotation after movement so follow cameras do not inherit character yaw.
        /// </summary>
        private void LateUpdate()
        {
            ApplyCameraFollowPointRotation();
        }

        /// <summary>
        /// Stores player, AI, or network-owned movement intent for the next KCC simulation tick.
        /// </summary>
        public void SetInputs(in PartyKccCharacterInputs inputs)
        {
            _inputs = inputs;
            _moveInputVector = ClampToCharacterPlane(inputs.MoveWorld);
            _lookInputVector = ClampToCharacterPlane(inputs.LookWorld);

            if (_lookInputVector.sqrMagnitude <= 0.0001f)
                _lookInputVector = _moveInputVector.sqrMagnitude > 0.0001f ? _moveInputVector : transform.forward;

            if (CurrentState == EPartyKccCharacterState.Knockback)
                return;

            if (inputs.JumpPressed)
            {
                _jumpRequested = true;
                _jumpRequestAge = 0f;
            }

            if (inputs.DashPressed)
                _dashRequested = true;
        }

        /// <summary>
        /// Applies a high-priority knockback velocity and locks regular movement for the supplied time.
        /// </summary>
        public void ApplyKnockback(Vector3 velocity, float lockTime)
        {
            _knockbackVelocity = velocity;
            _knockbackTimeRemaining = Mathf.Max(0f, lockTime);
            _jumpRequested = false;
            _dashRequested = false;

            if (_motor != null && Vector3.Dot(velocity, CharacterUp) > 0.01f)
                _motor.ForceUnground();

            TransitionToState(EPartyKccCharacterState.Knockback);
        }

        /// <summary>
        /// Queues an additive velocity to be consumed during the next KCC velocity update.
        /// </summary>
        public void AddExternalVelocity(Vector3 velocity)
        {
            _externalVelocityAdd += velocity;

            if (_motor != null && Vector3.Dot(velocity, CharacterUp) > 0.01f)
                _motor.ForceUnground();
        }

        /// <summary>
        /// Changes the dash charge cap and fills any newly added capacity.
        /// </summary>
        public void SetDashChargeLimit(int dashChargeLimit)
        {
            int previousLimit = _maxDashCharges;
            _maxDashCharges = Mathf.Max(0, dashChargeLimit);
            _dashCharges = Mathf.Clamp(_dashCharges + Mathf.Max(0, _maxDashCharges - previousLimit), 0, _maxDashCharges);

            if (_dashCharges == _maxDashCharges)
                _dashRechargeTimer = 0f;
        }

        /// <summary>
        /// Rotates the independent camera follow target from local look input without rotating the character body directly.
        /// </summary>
        public void AddCameraLookInput(Vector2 lookDelta)
        {
            if (!_stabilizeCameraFollowPointRotation || _cameraFollowPoint == null || lookDelta.sqrMagnitude <= 0f)
                return;

            _cameraYaw += lookDelta.x * _cameraYawSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch - lookDelta.y * _cameraPitchSensitivity, _cameraMinPitch, _cameraMaxPitch);

            if (_cameraYaw > 360f || _cameraYaw < -360f)
                _cameraYaw = Mathf.Repeat(_cameraYaw, 360f);

            ApplyCameraFollowPointRotation();
        }

        /// <summary>
        /// Runs before KCC updates grounding and movement.
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// Rotates the character toward the active movement state's facing direction.
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Vector3 targetLook = CurrentState == EPartyKccCharacterState.Dash ? _dashDirection : _lookInputVector;
            if (CurrentState == EPartyKccCharacterState.Knockback)
                targetLook = ClampToCharacterPlane(_knockbackVelocity);

            if (targetLook.sqrMagnitude <= 0.0001f || _rotationSharpness <= 0f)
                return;

            Vector3 smoothedLook = Vector3.Slerp(_motor.CharacterForward, targetLook.normalized, 1f - Mathf.Exp(-_rotationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLook, _motor.CharacterUp);
        }

        /// <summary>
        /// Calculates the authoritative KCC base velocity for the current simulation tick.
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (CurrentState != EPartyKccCharacterState.Knockback)
                TryStartDash();

            switch (CurrentState)
            {
                case EPartyKccCharacterState.Knockback:
                    UpdateKnockbackVelocity(ref currentVelocity, deltaTime);
                    break;
                case EPartyKccCharacterState.Dash:
                    UpdateDashVelocity(ref currentVelocity, deltaTime);
                    break;
                default:
                    UpdateDefaultVelocity(ref currentVelocity, deltaTime);
                    break;
            }

            ConsumeExternalVelocity(ref currentVelocity);
        }

        /// <summary>
        /// Tracks post-simulation timers and performs time-based state exits.
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            UpdateJumpTimers(deltaTime);
            UpdateDashRecharge(deltaTime);
            ApplyCameraFollowPointRotation();

            if (CurrentState == EPartyKccCharacterState.Dash)
            {
                _dashTimeRemaining -= deltaTime;
                if (_dashTimeRemaining <= 0f)
                    TransitionToState(EPartyKccCharacterState.Default);
            }

            if (CurrentState == EPartyKccCharacterState.Knockback)
            {
                _knockbackTimeRemaining -= deltaTime;
                if (_knockbackTimeRemaining <= 0f)
                    TransitionToState(EPartyKccCharacterState.Default);
            }
        }

        /// <summary>
        /// Detects landing and leaving stable ground after KCC refreshes grounding.
        /// </summary>
        public void PostGroundingUpdate(float deltaTime)
        {
            if (_motor.GroundingStatus.IsStableOnGround && !_motor.LastGroundingStatus.IsStableOnGround)
            {
                _jumpCountUsed = 0;
                _timeSinceLastStableGrounded = 0f;
            }
        }

        /// <summary>
        /// Filters colliders that should not block this character.
        /// </summary>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            return _ignoredColliders == null || !_ignoredColliders.Contains(coll);
        }

        /// <summary>
        /// Receives stable ground hits from KCC; reserved for later surface-specific extensions.
        /// </summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// Receives movement hits from KCC; reserved for later wall or hazard extensions.
        /// </summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// Allows later extensions to override KCC hit stability classification.
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// Receives discrete collision notifications when enabled on the KCC motor.
        /// </summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        /// <summary>
        /// Switches movement state and clears state-local transient requests.
        /// </summary>
        private void TransitionToState(EPartyKccCharacterState newState)
        {
            if (CurrentState == newState)
                return;

            CurrentState = newState;
        }

        /// <summary>
        /// Projects and clamps a vector onto the character movement plane.
        /// </summary>
        private Vector3 ClampToCharacterPlane(Vector3 vector)
        {
            Vector3 planar = Vector3.ProjectOnPlane(vector, CharacterUp);
            return Vector3.ClampMagnitude(planar, 1f);
        }

        /// <summary>
        /// Initializes the independent camera target angles from the current follow point orientation.
        /// </summary>
        private void InitializeCameraFollowRotation()
        {
            Vector3 sourceForward = _cameraFollowPoint != null ? _cameraFollowPoint.forward : transform.forward;
            Vector3 planarSourceForward = Vector3.ProjectOnPlane(sourceForward, CharacterUp);
            Vector3 referenceForward = GetCameraReferenceForward();

            if (planarSourceForward.sqrMagnitude <= 0.0001f)
                planarSourceForward = referenceForward;

            _cameraYaw = Vector3.SignedAngle(referenceForward, planarSourceForward.normalized, CharacterUp);
            _cameraPitch = Mathf.Clamp(_cameraPitch, _cameraMinPitch, _cameraMaxPitch);
        }

        /// <summary>
        /// Keeps the camera target rotation controlled by stored look angles instead of inherited character yaw.
        /// </summary>
        private void ApplyCameraFollowPointRotation()
        {
            if (!_stabilizeCameraFollowPointRotation || _cameraFollowPoint == null)
                return;

            Vector3 referenceForward = GetCameraReferenceForward();
            Vector3 planarForward = Quaternion.AngleAxis(_cameraYaw, CharacterUp) * referenceForward;
            if (planarForward.sqrMagnitude <= 0.0001f)
                return;

            // Apply pitch around the camera target's right axis so Cinemachine can orbit vertically.
            Quaternion yawRotation = Quaternion.LookRotation(planarForward.normalized, CharacterUp);
            Vector3 pitchAxis = Vector3.Cross(CharacterUp, planarForward).normalized;
            Quaternion pitchRotation = Quaternion.AngleAxis(_cameraPitch, pitchAxis);
            _cameraFollowPoint.rotation = pitchRotation * yawRotation;
        }

        /// <summary>
        /// Returns the stable world forward used as the zero-yaw basis for the camera target.
        /// </summary>
        private Vector3 GetCameraReferenceForward()
        {
            Vector3 referenceForward = Vector3.ProjectOnPlane(Vector3.forward, CharacterUp);
            if (referenceForward.sqrMagnitude <= 0.0001f)
                referenceForward = Vector3.ProjectOnPlane(transform.forward, CharacterUp);

            return referenceForward.sqrMagnitude > 0.0001f ? referenceForward.normalized : Vector3.forward;
        }

        /// <summary>
        /// Applies regular grounded or airborne movement, then consumes jump requests.
        /// </summary>
        private void UpdateDefaultVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Resolve the base movement path first so walk and run share identical collision behavior.
            if (_motor.GroundingStatus.IsStableOnGround)
                UpdateStableMovement(ref currentVelocity, deltaTime);
            else
                UpdateAirMovement(ref currentVelocity, deltaTime);

            // Consume jump after movement so the upward velocity cleanly overrides ground snapping.
            TryConsumeJump(ref currentVelocity);
        }

        /// <summary>
        /// Smooths grounded velocity toward the current walk or run target speed.
        /// </summary>
        private void UpdateStableMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            float currentVelocityMagnitude = currentVelocity.magnitude;
            Vector3 effectiveGroundNormal = _motor.GroundingStatus.GroundNormal;
            currentVelocity = _motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, _motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
            float targetSpeed = _inputs.RunHeld ? _runSpeed : _walkSpeed;
            Vector3 targetMovementVelocity = reorientedInput * targetSpeed;

            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-_stableMovementSharpness * deltaTime));
        }

        /// <summary>
        /// Adds planar air control, gravity, and drag while respecting the air speed cap.
        /// </summary>
        private void UpdateAirMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = _moveInputVector * (_airAccelerationSpeed * deltaTime);
                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, _motor.CharacterUp);

                if (currentVelocityOnInputsPlane.magnitude < _maxAirMoveSpeed)
                {
                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, _maxAirMoveSpeed);
                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                }
                else if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                }

                currentVelocity += addedVelocity;
            }

            currentVelocity += _gravity * deltaTime;
            currentVelocity *= 1f / (1f + _airDrag * deltaTime);
        }

        /// <summary>
        /// Starts a dash when a buffered dash request and charge are both available.
        /// </summary>
        private void TryStartDash()
        {
            if (!_dashRequested)
                return;

            _dashRequested = false;
            if (_dashCharges <= 0 || _maxDashCharges <= 0)
                return;

            _dashCharges--;
            _dashTimeRemaining = Mathf.Max(0.01f, _dashDuration);
            _dashDirection = ResolveDashDirection();

            if (_dashCharges < _maxDashCharges && _dashRechargeTimer <= 0f)
                _dashRechargeTimer = 0f;

            TransitionToState(EPartyKccCharacterState.Dash);
        }

        /// <summary>
        /// Locks horizontal dash speed while preserving and updating vertical velocity.
        /// </summary>
        private void UpdateDashVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 verticalVelocity = Vector3.Project(currentVelocity, _motor.CharacterUp);
            verticalVelocity += Vector3.Project(_gravity * deltaTime, _motor.CharacterUp);
            currentVelocity = (_dashDirection * _dashSpeed) + verticalVelocity;
        }

        /// <summary>
        /// Applies the active knockback velocity and lets gravity/drag decay it over time.
        /// </summary>
        private void UpdateKnockbackVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            currentVelocity = _knockbackVelocity;
            _knockbackVelocity += _gravity * deltaTime;
            _knockbackVelocity *= 1f / (1f + _knockbackDrag * deltaTime);
        }

        /// <summary>
        /// Chooses a dash direction from input, current velocity, or character facing.
        /// </summary>
        private Vector3 ResolveDashDirection()
        {
            if (_moveInputVector.sqrMagnitude > 0.0001f)
                return _moveInputVector.normalized;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(CurrentVelocity, _motor.CharacterUp);
            if (planarVelocity.sqrMagnitude > 0.0001f)
                return planarVelocity.normalized;

            Vector3 forward = Vector3.ProjectOnPlane(_motor.CharacterForward, _motor.CharacterUp);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        /// <summary>
        /// Consumes a jump request when grounded, within coyote time, or below the configured jump count.
        /// </summary>
        private void TryConsumeJump(ref Vector3 currentVelocity)
        {
            if (!_jumpRequested || _jumpRequestAge > _jumpRequestBufferTime || _maxJumpCount <= 0)
                return;

            bool canGroundJump = _motor.GroundingStatus.IsStableOnGround || _timeSinceLastStableGrounded <= _coyoteTime;
            bool canAirJump = _jumpCountUsed < _maxJumpCount && !canGroundJump;
            if (!canGroundJump && !canAirJump)
                return;

            Vector3 jumpDirection = _motor.CharacterUp;
            _motor.ForceUnground();
            currentVelocity += (jumpDirection * _jumpSpeed) - Vector3.Project(currentVelocity, _motor.CharacterUp);

            _jumpRequested = false;
            _jumpedThisFrame = true;
            _jumpCountUsed = canGroundJump ? 1 : _jumpCountUsed + 1;
        }

        /// <summary>
        /// Ages jump/coyote timers and expires jump requests that were not consumed.
        /// </summary>
        private void UpdateJumpTimers(float deltaTime)
        {
            if (_jumpRequested)
            {
                _jumpRequestAge += deltaTime;
                if (_jumpRequestAge > _jumpRequestBufferTime)
                    _jumpRequested = false;
            }

            if (_motor.GroundingStatus.IsStableOnGround)
            {
                _timeSinceLastStableGrounded = 0f;
                if (!_jumpedThisFrame)
                    _jumpCountUsed = 0;
            }
            else
            {
                _timeSinceLastStableGrounded += deltaTime;
                if (_timeSinceLastStableGrounded > _coyoteTime && _jumpCountUsed == 0)
                    _jumpCountUsed = 1;
            }

            _jumpedThisFrame = false;
        }

        /// <summary>
        /// Restores dash charges one by one on the configured cooldown.
        /// </summary>
        private void UpdateDashRecharge(float deltaTime)
        {
            if (_maxDashCharges <= 0 || _dashCharges >= _maxDashCharges || _dashRechargeTime <= 0f)
                return;

            _dashRechargeTimer += deltaTime;
            while (_dashRechargeTimer >= _dashRechargeTime && _dashCharges < _maxDashCharges)
            {
                _dashRechargeTimer -= _dashRechargeTime;
                _dashCharges++;
            }

            if (_dashCharges >= _maxDashCharges)
                _dashRechargeTimer = 0f;
        }

        /// <summary>
        /// Adds queued extension velocity after the active state has calculated its own velocity.
        /// </summary>
        private void ConsumeExternalVelocity(ref Vector3 currentVelocity)
        {
            if (_externalVelocityAdd.sqrMagnitude <= 0f)
                return;

            currentVelocity += _externalVelocityAdd;
            _externalVelocityAdd = Vector3.zero;
        }
    }
}
