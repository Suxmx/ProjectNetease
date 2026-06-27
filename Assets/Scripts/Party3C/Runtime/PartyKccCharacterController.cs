using System.Collections.Generic;
using KinematicCharacterController;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// 负责派对大乱斗角色的 KCC 移动模拟，并向战斗、输入和联网层暴露扩展入口。
    /// </summary>
    public sealed class PartyKccCharacterController : MonoBehaviour, ICharacterController, IPartyKccCharacterMotor
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private KinematicCharacterMotor _motor;
        [SerializeField] private Transform _meshRoot;
        [SerializeField] private Transform _cameraFollowPoint;

        [Header("Camera Target")]
        [SerializeField] private bool _stabilizeCameraFollowPointRotation = true;
        [SerializeField, MinValue(0f)] private float _cameraYawSensitivity = 0.12f;
        [SerializeField, MinValue(0f)] private float _cameraPitchSensitivity = 0.08f;
        [SerializeField, ValidateInput(nameof(IsCameraPitchRangeValid), "最小俯仰角不能大于最大俯仰角。")]
        private float _cameraMinPitch = -25f;
        [SerializeField, ValidateInput(nameof(IsCameraPitchRangeValid), "最大俯仰角不能小于最小俯仰角。")]
        private float _cameraMaxPitch = 65f;

        [Header("Stable Movement")]
        [SerializeField, MinValue(0f)] private float _walkSpeed = 5f;
        [SerializeField, MinValue(0f), ValidateInput(nameof(IsRunSpeedValid), "跑步速度不能小于走路速度。")]
        private float _runSpeed = 8f;
        [SerializeField, MinValue(0f)] private float _stableMovementSharpness = 15f;
        [SerializeField, MinValue(0f)] private float _rotationSharpness = 12f;

        [Header("Air Movement")]
        [SerializeField, MinValue(0f)] private float _maxAirMoveSpeed = 7f;
        [SerializeField, MinValue(0f)] private float _airAccelerationSpeed = 18f;
        [SerializeField, MinValue(0f)] private float _airDrag = 0.1f;
        [SerializeField] private Vector3 _gravity = new(0f, -30f, 0f);

        [Header("Jump")]
        [SerializeField, MinValue(0)] private int _maxJumpCount = 2;
        [SerializeField, MinValue(0f)] private float _jumpSpeed = 10f;
        [SerializeField, MinValue(0f)] private float _coyoteTime = 0.12f;
        [SerializeField, MinValue(0f)] private float _jumpRequestBufferTime = 0.12f;

        [Header("Dash")]
        [SerializeField, MinValue(0)] private int _maxDashCharges = 1;
        [SerializeField, MinValue(0f)] private float _dashSpeed = 13f;
        [SerializeField, MinValue(0.01f)] private float _dashDuration = 0.18f;
        [SerializeField, MinValue(0.01f)] private float _dashRechargeTime = 1f;

        [Header("Knockback")]
        [SerializeField, MinValue(0f)] private float _knockbackDrag = 0.02f;

        [Header("Collision")]
        [SerializeField] private List<Collider> _ignoredColliders = new();

        #endregion

        #region Runtime State

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

        #endregion

        #region Properties

        public EPartyKccCharacterState CurrentState { get; private set; } = EPartyKccCharacterState.Default;
        public Vector3 CurrentVelocity => _motor != null ? _motor.BaseVelocity : Vector3.zero;
        public int CurrentDashCharges => _dashCharges;
        public int MaxDashCharges => _maxDashCharges;
        public bool IsStableOnGround => _motor != null && _motor.GroundingStatus.IsStableOnGround;
        public Transform CameraFollowPoint => _cameraFollowPoint;
        public Vector3 CharacterUp => _motor != null ? _motor.CharacterUp : transform.up;

        #endregion

        #region Setup

        /// <summary>
        /// 设置由编辑器工具或自定义生成器创建的运行时引用。
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
        /// 查找 KCC Motor，并初始化冲刺次数与摄像机跟随点角度。
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
        /// 在移动更新后重设摄像机跟随点旋转，避免它继承角色身体朝向。
        /// </summary>
        private void LateUpdate()
        {
            ApplyCameraFollowPointRotation();
        }

        #endregion

        #region Input

        /// <summary>
        /// 保存玩家、AI 或网络所有者提供的移动意图，等待下一次 KCC 模拟消费。
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
        /// 清空当前移动输入和仍在等待消费的跳跃、冲刺请求。
        /// </summary>
        public void ClearInputs()
        {
            _inputs = default;
            _moveInputVector = Vector3.zero;
            _lookInputVector = Vector3.zero;
            _jumpRequested = false;
            _dashRequested = false;
            _jumpRequestAge = Mathf.Infinity;
        }

        /// <summary>
        /// 根据本地视角输入旋转独立摄像机跟随点，不直接旋转角色身体。
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

        #endregion

        #region External Forces

        /// <summary>
        /// 施加最高优先级击退速度，并在指定时间内锁定普通移动输入。
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
        /// 缓存一段额外速度，在下一次 KCC 速度更新末尾叠加。
        /// </summary>
        public void AddExternalVelocity(Vector3 velocity)
        {
            _externalVelocityAdd += velocity;

            if (_motor != null && Vector3.Dot(velocity, CharacterUp) > 0.01f)
                _motor.ForceUnground();
        }

        /// <summary>
        /// 修改冲刺次数上限，并补足新增的可用次数。
        /// </summary>
        public void SetDashChargeLimit(int dashChargeLimit)
        {
            int previousLimit = _maxDashCharges;
            _maxDashCharges = Mathf.Max(0, dashChargeLimit);
            _dashCharges = Mathf.Clamp(_dashCharges + Mathf.Max(0, _maxDashCharges - previousLimit), 0, _maxDashCharges);

            if (_dashCharges == _maxDashCharges)
                _dashRechargeTimer = 0f;
        }

        #endregion

        #region KCC Callbacks

        /// <summary>
        /// KCC 刷新接地和移动前调用，当前保留给后续扩展。
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// 根据当前移动状态把角色转向目标朝向。
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
        /// 计算当前模拟帧的权威 KCC 基础速度。
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
        /// 维护模拟后的计时器，并处理按时间退出的状态。
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
        /// 在 KCC 刷新接地后检测落地和离地。
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
        /// 过滤不应该阻挡当前角色的碰撞体。
        /// </summary>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            return _ignoredColliders == null || !_ignoredColliders.Contains(coll);
        }

        /// <summary>
        /// 接收稳定地面命中，保留给后续按地表类型扩展。
        /// </summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 接收移动命中，保留给后续墙面、机关或受击扩展。
        /// </summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 允许后续扩展重写 KCC 的命中稳定性判断。
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 接收离散碰撞通知，需在 KCC Motor 上启用对应选项。
        /// </summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        #endregion

        #region Camera

        /// <summary>
        /// 从当前跟随点旋转初始化独立摄像机角度。
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
        /// 用缓存的视角角度驱动摄像机跟随点旋转，避免它继承角色身体朝向。
        /// </summary>
        private void ApplyCameraFollowPointRotation()
        {
            if (!_stabilizeCameraFollowPointRotation || _cameraFollowPoint == null)
                return;

            Vector3 referenceForward = GetCameraReferenceForward();
            Vector3 planarForward = Quaternion.AngleAxis(_cameraYaw, CharacterUp) * referenceForward;
            if (planarForward.sqrMagnitude <= 0.0001f)
                return;

            // 先构造水平偏航，再绕跟随点右轴叠加俯仰，供 Cinemachine 读取。
            Quaternion yawRotation = Quaternion.LookRotation(planarForward.normalized, CharacterUp);
            Vector3 pitchAxis = Vector3.Cross(CharacterUp, planarForward).normalized;
            Quaternion pitchRotation = Quaternion.AngleAxis(_cameraPitch, pitchAxis);
            _cameraFollowPoint.rotation = pitchRotation * yawRotation;
        }

        /// <summary>
        /// 获取作为摄像机零偏航基准的稳定世界前向。
        /// </summary>
        private Vector3 GetCameraReferenceForward()
        {
            Vector3 referenceForward = Vector3.ProjectOnPlane(Vector3.forward, CharacterUp);
            if (referenceForward.sqrMagnitude <= 0.0001f)
                referenceForward = Vector3.ProjectOnPlane(transform.forward, CharacterUp);

            return referenceForward.sqrMagnitude > 0.0001f ? referenceForward.normalized : Vector3.forward;
        }

        #endregion

        #region Movement

        /// <summary>
        /// 应用常规地面或空中移动，然后消费跳跃请求。
        /// </summary>
        private void UpdateDefaultVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // 先处理走跑共用的基础移动路径，保持碰撞行为一致。
            if (_motor.GroundingStatus.IsStableOnGround)
                UpdateStableMovement(ref currentVelocity, deltaTime);
            else
                UpdateAirMovement(ref currentVelocity, deltaTime);

            // 最后处理跳跃，让向上速度明确覆盖地面吸附。
            TryConsumeJump(ref currentVelocity);
        }

        /// <summary>
        /// 将地面速度平滑到当前走路或跑步目标速度。
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
        /// 应用空中平面控制、重力和阻力，并限制平面空中速度。
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
        /// 切换移动状态，并清理状态内的临时请求。
        /// </summary>
        private void TransitionToState(EPartyKccCharacterState newState)
        {
            if (CurrentState == newState)
                return;

            CurrentState = newState;
        }

        /// <summary>
        /// 在当前状态计算完速度后叠加外部扩展速度。
        /// </summary>
        private void ConsumeExternalVelocity(ref Vector3 currentVelocity)
        {
            if (_externalVelocityAdd.sqrMagnitude <= 0f)
                return;

            currentVelocity += _externalVelocityAdd;
            _externalVelocityAdd = Vector3.zero;
        }

        #endregion

        #region Jump

        /// <summary>
        /// 在接地、土狼时间内或剩余多段跳次数可用时消费跳跃请求。
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
        /// 推进跳跃缓冲和土狼时间计时，并清理过期跳跃请求。
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

        #endregion

        #region Dash

        /// <summary>
        /// 在冲刺请求和可用次数同时满足时进入冲刺状态。
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
        /// 锁定水平冲刺速度，同时保留并继续更新竖直速度。
        /// </summary>
        private void UpdateDashVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 verticalVelocity = Vector3.Project(currentVelocity, _motor.CharacterUp);
            verticalVelocity += Vector3.Project(_gravity * deltaTime, _motor.CharacterUp);
            currentVelocity = (_dashDirection * _dashSpeed) + verticalVelocity;
        }

        /// <summary>
        /// 从移动输入、当前速度或角色朝向中选择冲刺方向。
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
        /// 按配置冷却逐个恢复冲刺次数。
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

        #endregion

        #region Knockback

        /// <summary>
        /// 应用当前击退速度，并通过重力和阻力让击退逐步衰减。
        /// </summary>
        private void UpdateKnockbackVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            currentVelocity = _knockbackVelocity;
            _knockbackVelocity += _gravity * deltaTime;
            _knockbackVelocity *= 1f / (1f + _knockbackDrag * deltaTime);
        }

        #endregion

        #region Validation

        /// <summary>
        /// 验证跑步速度不低于走路速度。
        /// </summary>
        private bool IsRunSpeedValid(float value)
        {
            return value >= _walkSpeed;
        }

        /// <summary>
        /// 验证摄像机俯仰角范围有效。
        /// </summary>
        private bool IsCameraPitchRangeValid()
        {
            return _cameraMinPitch <= _cameraMaxPitch;
        }

        #endregion

        #region Utility

        /// <summary>
        /// 将向量投影并限制到角色移动平面上。
        /// </summary>
        private Vector3 ClampToCharacterPlane(Vector3 vector)
        {
            Vector3 planar = Vector3.ProjectOnPlane(vector, CharacterUp);
            return Vector3.ClampMagnitude(planar, 1f);
        }

        #endregion
    }
}
