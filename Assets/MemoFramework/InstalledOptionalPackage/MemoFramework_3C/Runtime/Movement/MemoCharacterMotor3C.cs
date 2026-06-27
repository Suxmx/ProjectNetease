using MemoFramework.Extension;
using UnityEngine;

namespace MemoFramework.ThreeC
{
    /// <summary>
    /// 基于 CharacterController 的非联网 3C Motor，提供移动、跳跃、斜坡、冲刺、爬墙和击退能力。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class MemoCharacterMotor3C : MonoBehaviour
    {
        protected const float MinDirectionSqrMagnitude = 0.0001f;
        private const int GroundProbeHitBufferSize = 8;
        private const float MinGroundSupportUpDot = 0.05f;

        [Header("Control")]
        [SerializeField] private bool _controlEnabled = true;
        [SerializeField] private bool _runByDefault = true;
        [SerializeField, Range(0f, 1f)] private float _runInputThreshold = 0.85f;

        [Header("Move")]
        [SerializeField, Min(0f)] private float _walkSpeed = 3.5f;
        [SerializeField, Min(0f)] private float _runSpeed = 6f;
        [SerializeField, Min(0f)] private float _groundAcceleration = 35f;
        [SerializeField, Min(0f)] private float _airAcceleration = 14f;
        [SerializeField, Min(0f)] private float _turnSpeed = 720f;
        [SerializeField] private bool _rotateToMoveDirection = true;

        [Header("Jump And Gravity")]
        [SerializeField, Min(0f)] private float _jumpHeight = 1.6f;
        [SerializeField] private float _gravity = -30f;
        [SerializeField, Min(0f)] private float _groundStickVelocity = 2f;
        [SerializeField, Min(0f)] private float _coyoteTime = 0.08f;
        [SerializeField, Min(0f)] private float _jumpBufferTime = 0.1f;

        [Header("Slope")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField, Min(0f)] private float _groundProbeDistance = 0.35f;
        [SerializeField, Min(0f)] private float _groundContactDistance = 0.03f;
        [SerializeField, Min(0f)] private float _groundSnapDistance = 0.12f;
        [SerializeField, Range(0f, 89f)] private float _slopeLimit = 50f;
        [SerializeField, Min(0f)] private float _steepSlopeSlideSpeed = 4f;

        [Header("Dash")]
        [SerializeField, Min(0f)] private float _dashSpeed = 12f;
        [SerializeField, Min(0.01f)] private float _dashDuration = 0.18f;
        [SerializeField, Min(0f)] private float _dashCooldown = 0.35f;
        [SerializeField] private bool _dashUsesMoveDirection = true;
        [SerializeField] private bool _dashIgnoresGravity = true;

        [Header("Wall Climb")]
        [SerializeField] private bool _wallClimbEnabled = true;
        [SerializeField] private LayerMask _wallMask = ~0;
        [SerializeField, Min(0f)] private float _wallCheckDistance = 0.75f;
        [SerializeField, Min(0f)] private float _wallClimbSpeed = 3f;
        [SerializeField, Min(0f)] private float _wallClimbDuration = 0.8f;
        [SerializeField] private bool _wallClimbRequiresForwardInput = true;

        [Header("External Force")]
        [SerializeField, Min(0f)] private float _knockbackDamping = 22f;
        [SerializeField, Min(0f)] private float _externalVelocityStopThreshold = 0.05f;

        private CharacterController _characterController;
        private bool _sprintHeld;
        private bool _wallClimbHeld;
        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private bool _dashQueued;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _wallClimbTimer;
        private Vector3 _dashVelocity;
        private bool _hasGroundSupport;
        private bool _groundSupportIsWalkable;
        private readonly RaycastHit[] _groundProbeHits = new RaycastHit[GroundProbeHitBufferSize];

        /// <summary>
        /// 当前 CharacterController。
        /// </summary>
        public CharacterController CharacterController => CacheCharacterController();

        /// <summary>
        /// 当前是否允许本地驱动该 Motor。
        /// </summary>
        public bool ControlEnabled => _controlEnabled;

        /// <summary>
        /// 当前主要运动模式。
        /// </summary>
        public ECharacterMotor3CMode MovementMode { get; protected set; } = ECharacterMotor3CMode.Airborne;

        /// <summary>
        /// 当前帧读取到的二维输入。
        /// </summary>
        public Vector2 MoveInput { get; protected set; }

        /// <summary>
        /// 当前完整速度，包含平面、垂直和外力速度。
        /// </summary>
        public Vector3 Velocity { get; protected set; }

        /// <summary>
        /// 当前由走跑输入产生的平面速度。
        /// </summary>
        public Vector3 PlanarVelocity { get; protected set; }

        /// <summary>
        /// 当前由击退、技能或其它外部来源产生的速度。
        /// </summary>
        public Vector3 ExternalVelocity { get; protected set; }

        /// <summary>
        /// 当前垂直速度。
        /// </summary>
        public float VerticalVelocity { get; protected set; }

        /// <summary>
        /// 当前是否认为角色位于地面。
        /// </summary>
        public bool IsGrounded { get; protected set; }

        /// <summary>
        /// 当前是否处于跑步速度段。
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// 当前是否处于冲刺状态。
        /// </summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>
        /// 当前是否处于爬墙状态。
        /// </summary>
        public bool IsWallClimbing => _wallClimbTimer > 0f;

        /// <summary>
        /// 当前地面法线。
        /// </summary>
        public Vector3 GroundNormal { get; protected set; } = Vector3.up;

        /// <summary>
        /// 当前脚底到地面支撑面的距离，未检测到地面时为正无穷。
        /// </summary>
        public float GroundDistance { get; protected set; } = float.PositiveInfinity;

        /// <summary>
        /// 最近一次检测到的墙面法线。
        /// </summary>
        public Vector3 WallNormal { get; protected set; } = Vector3.forward;

        /// <summary>
        /// 最近一次 CharacterController.Move 返回的碰撞标记。
        /// </summary>
        public CollisionFlags LastCollisionFlags { get; protected set; }

        /// <summary>
        /// Unity 每帧更新入口。
        /// </summary>
        protected virtual void Update()
        {
            if (!_controlEnabled)
                return;

            TickMotor(Time.deltaTime);
        }

        /// <summary>
        /// 设置该 Motor 是否接受本地输入和模拟。
        /// </summary>
        /// <param name="enabled">是否启用本地控制。</param>
        public virtual void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            if (!enabled)
                ClearTransientInput();
        }

        /// <summary>
        /// 设置 CharacterController 是否启用。
        /// </summary>
        /// <param name="enabled">是否启用 CharacterController。</param>
        public virtual void SetCharacterControllerEnabled(bool enabled)
        {
            CharacterController characterController = CacheCharacterController();
            if (characterController != null)
                characterController.enabled = enabled;
        }

        /// <summary>
        /// 设置外部输入层提供的跑步保持状态。
        /// </summary>
        /// <param name="held">是否保持跑步。</param>
        public virtual void SetSprintHeld(bool held)
        {
            _sprintHeld = held;
        }

        /// <summary>
        /// 设置外部输入层提供的爬墙保持状态。
        /// </summary>
        /// <param name="held">是否保持爬墙。</param>
        public virtual void SetWallClimbHeld(bool held)
        {
            _wallClimbHeld = held;
        }

        /// <summary>
        /// 缓冲一次跳跃输入。
        /// </summary>
        public virtual void QueueJump()
        {
            _jumpBufferTimer = _jumpBufferTime;
        }

        /// <summary>
        /// 尝试按当前移动方向触发冲刺。
        /// </summary>
        /// <returns>是否成功开始冲刺。</returns>
        public virtual bool TryDash()
        {
            Vector3 direction = ResolveDashDirection(Vector3.zero);
            return TryDash(direction);
        }

        /// <summary>
        /// 尝试按指定方向触发冲刺。
        /// </summary>
        /// <param name="direction">世界空间冲刺方向。</param>
        /// <returns>是否成功开始冲刺。</returns>
        public virtual bool TryDash(Vector3 direction)
        {
            if (_dashCooldownTimer > 0f || _dashTimer > 0f)
                return false;

            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                direction = transform.forward;

            direction.Normalize();
            _dashVelocity = direction * _dashSpeed;
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;
            return true;
        }

        /// <summary>
        /// 缓冲一次冲刺输入。
        /// </summary>
        public virtual void QueueDash()
        {
            _dashQueued = true;
        }

        /// <summary>
        /// 添加一次击退或外力速度。
        /// </summary>
        /// <param name="velocity">要叠加的世界空间速度。</param>
        public virtual void AddExternalVelocity(Vector3 velocity)
        {
            ExternalVelocity += velocity;
        }

        /// <summary>
        /// 按方向和强度施加击退。
        /// </summary>
        /// <param name="direction">击退方向。</param>
        /// <param name="strength">击退强度。</param>
        public virtual void ApplyKnockback(Vector3 direction, float strength)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude || strength <= 0f)
                return;

            AddExternalVelocity(direction.normalized * strength);
        }

        /// <summary>
        /// 清空当前外力速度。
        /// </summary>
        public virtual void ClearExternalVelocity()
        {
            ExternalVelocity = Vector3.zero;
        }

        /// <summary>
        /// 手动推进 Motor，适合测试或自定义 PlayerLoop。
        /// </summary>
        /// <param name="deltaTime">推进时间。</param>
        public virtual void TickMotor(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            CharacterController characterController = CacheCharacterController();
            if (characterController == null || !characterController.enabled)
                return;

            // 读取输入和环境状态。
            ReadFrameInput();
            ProbeGround();
            TickTimers(deltaTime);

            // 处理一次性动作。
            TryConsumeJump();
            TryStartWallClimb();
            if (_dashQueued || ReadDashInput())
            {
                TryDash();
                _dashQueued = false;
            }

            // 按当前状态计算速度。
            Vector3 moveDirection = ResolveMoveDirection(MoveInput);
            Vector3 planarVelocity = CalculatePlanarVelocity(moveDirection, deltaTime);
            Vector3 verticalVelocity = CalculateVerticalVelocity(deltaTime);
            Vector3 externalVelocity = CalculateExternalVelocity(deltaTime);

            // 组合速度并移动 CharacterController。
            Velocity = planarVelocity + verticalVelocity + externalVelocity;
            LastCollisionFlags = characterController.Move(Velocity * deltaTime);
            ProcessCollisionFlags();

            // Move 后重新检测地面支撑，并按真实碰撞/极近距离接触刷新落地状态。
            ProbeGround();
            TrySnapToGround(characterController);
            RefreshGroundedState();

            // 刷新模式和朝向。
            RefreshMovementMode();
            RotateByMotion(moveDirection, deltaTime);
            AfterMotorTick(deltaTime);
        }

        /// <summary>
        /// 读取二维移动输入。
        /// </summary>
        /// <returns>当前二维移动输入。</returns>
        protected virtual Vector2 ReadMoveInput()
        {
            return InputData.MoveInput;
        }

        /// <summary>
        /// 读取跑步输入。
        /// </summary>
        /// <returns>当前是否请求跑步。</returns>
        protected virtual bool ReadSprintInput()
        {
            return _sprintHeld;
        }

        /// <summary>
        /// 读取跳跃输入。
        /// </summary>
        /// <returns>当前是否请求跳跃。</returns>
        protected virtual bool ReadJumpInput()
        {
            return InputData.HasEventStart(InputEvent.Jump);
        }

        /// <summary>
        /// 读取冲刺输入。
        /// </summary>
        /// <returns>当前是否请求冲刺。</returns>
        protected virtual bool ReadDashInput()
        {
            return InputData.HasEventStart(InputEvent.Dash);
        }

        /// <summary>
        /// 读取爬墙输入。
        /// </summary>
        /// <returns>当前是否请求爬墙。</returns>
        protected virtual bool ReadWallClimbInput()
        {
            return _wallClimbHeld;
        }

        /// <summary>
        /// 将二维输入转换成世界空间移动方向。
        /// </summary>
        /// <param name="input">二维移动输入。</param>
        /// <returns>世界空间移动方向。</returns>
        protected virtual Vector3 GetWorldMoveDirection(Vector2 input)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        /// <summary>
        /// 获取当前期望朝向。
        /// </summary>
        /// <param name="moveDirection">当前移动方向。</param>
        /// <param name="facingDirection">输出朝向。</param>
        /// <returns>是否有有效朝向。</returns>
        protected virtual bool TryGetFacingDirection(Vector3 moveDirection, out Vector3 facingDirection)
        {
            facingDirection = moveDirection;
            return moveDirection.sqrMagnitude > MinDirectionSqrMagnitude;
        }

        /// <summary>
        /// 每帧 Motor 结束后的扩展点。
        /// </summary>
        /// <param name="deltaTime">本帧时间。</param>
        protected virtual void AfterMotorTick(float deltaTime)
        {
        }

        /// <summary>
        /// 读取并规范化本帧输入。
        /// </summary>
        protected virtual void ReadFrameInput()
        {
            MoveInput = Vector2.ClampMagnitude(ReadMoveInput(), 1f);

            if (ReadJumpInput())
                QueueJump();
        }

        /// <summary>
        /// 推进所有计时器。
        /// </summary>
        /// <param name="deltaTime">本帧时间。</param>
        protected virtual void TickTimers(float deltaTime)
        {
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - deltaTime);
            _coyoteTimer = IsGrounded ? _coyoteTime : Mathf.Max(0f, _coyoteTimer - deltaTime);
            _dashTimer = Mathf.Max(0f, _dashTimer - deltaTime);
            _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - deltaTime);
            _wallClimbTimer = Mathf.Max(0f, _wallClimbTimer - deltaTime);
        }

        /// <summary>
        /// 检测地面并缓存地面法线。
        /// </summary>
        protected virtual void ProbeGround()
        {
            CharacterController characterController = CacheCharacterController();
            GroundNormal = Vector3.up;
            GroundDistance = float.PositiveInfinity;
            _hasGroundSupport = false;
            _groundSupportIsWalkable = false;

            // 使用接近 CharacterController 底部球体的 SphereCast，避免单点 ray 在台阶和边缘处误判。
            if (!TryGetGroundSupportHit(characterController, out RaycastHit hit, out float distanceBelowFeet))
                return;

            // 缓存地面支撑信息。是否 grounded 由 Move 后的碰撞和极近距离接触统一判断。
            GroundNormal = hit.normal;
            GroundDistance = distanceBelowFeet;
            _hasGroundSupport = true;
            _groundSupportIsWalkable = IsGroundSupportWalkable(hit.normal);
        }

        /// <summary>
        /// 使用 CharacterController 底部球体近似检测地面支撑。
        /// </summary>
        /// <param name="characterController">当前 CharacterController。</param>
        /// <param name="bestHit">最近的有效地面命中。</param>
        /// <param name="distanceBelowFeet">脚底到命中面的距离。</param>
        /// <returns>是否找到有效地面支撑。</returns>
        protected virtual bool TryGetGroundSupportHit(CharacterController characterController, out RaycastHit bestHit, out float distanceBelowFeet)
        {
            bestHit = default;
            distanceBelowFeet = float.PositiveInfinity;

            float radius = characterController.radius;
            float probeRadius = Mathf.Max(0.01f, radius - Mathf.Max(0.01f, characterController.skinWidth * 0.5f));
            float sphereCenterOffset = Mathf.Max(0f, characterController.height * 0.5f - radius);
            Vector3 bottomSphereCenter = transform.TransformPoint(characterController.center + Vector3.down * sphereCenterOffset);

            // 从底部球体略上方开始检测，避免初始贴地时 SphereCast 因重叠漏报。
            float castOriginOffset = Mathf.Max(0.02f, characterController.skinWidth);
            Vector3 castOrigin = bottomSphereCenter + Vector3.up * castOriginOffset;
            float castDistance = _groundProbeDistance + castOriginOffset + (radius - probeRadius);
            int hitCount = Physics.SphereCastNonAlloc(castOrigin, probeRadius, Vector3.down, _groundProbeHits, castDistance, _groundMask, QueryTriggerInteraction.Ignore);

            // 分类所有向上的支撑面：优先可行走坡面，找不到时才保留陡坡用于滑落。
            float bestWalkableDistance = float.PositiveInfinity;
            float bestSteepDistance = float.PositiveInfinity;
            RaycastHit bestWalkableHit = default;
            RaycastHit bestSteepHit = default;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundProbeHits[i];
                if (!IsValidGroundSupportHit(characterController, hit))
                    continue;

                if (!HasUpwardGroundSupport(hit.normal))
                    continue;

                float currentDistanceBelowFeet = Mathf.Max(0f, hit.distance - castOriginOffset - (radius - probeRadius));
                if (IsGroundSupportWalkable(hit.normal))
                {
                    if (currentDistanceBelowFeet >= bestWalkableDistance)
                        continue;

                    bestWalkableHit = hit;
                    bestWalkableDistance = currentDistanceBelowFeet;
                    continue;
                }

                if (currentDistanceBelowFeet >= bestSteepDistance)
                    continue;

                bestSteepHit = hit;
                bestSteepDistance = currentDistanceBelowFeet;
            }

            // 先返回最近的可行走支撑，避免侧边或陡坡边缘污染 GroundNormal。
            if (bestWalkableDistance < float.PositiveInfinity)
            {
                bestHit = bestWalkableHit;
                distanceBelowFeet = bestWalkableDistance;
                return true;
            }

            if (bestSteepDistance < float.PositiveInfinity)
            {
                bestHit = bestSteepHit;
                distanceBelowFeet = bestSteepDistance;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断一次地面支撑命中是否可用于当前角色。
        /// </summary>
        /// <param name="characterController">当前 CharacterController。</param>
        /// <param name="hit">待检查命中。</param>
        /// <returns>是否为有效地面支撑。</returns>
        protected virtual bool IsValidGroundSupportHit(CharacterController characterController, RaycastHit hit)
        {
            if (hit.collider == null || hit.collider == characterController)
                return false;

            Transform hitTransform = hit.collider.transform;
            return hitTransform != transform && !hitTransform.IsChildOf(transform);
        }

        /// <summary>
        /// 判断命中法线是否具有可作为地面支撑的向上分量。
        /// </summary>
        /// <param name="normal">待检查法线。</param>
        /// <returns>是否可作为地面支撑。</returns>
        protected virtual bool HasUpwardGroundSupport(Vector3 normal)
        {
            return Vector3.Dot(normal, Vector3.up) > MinGroundSupportUpDot;
        }

        /// <summary>
        /// 判断地面支撑法线是否处于可行走坡度内。
        /// </summary>
        /// <param name="normal">待检查法线。</param>
        /// <returns>是否为可行走地面。</returns>
        protected virtual bool IsGroundSupportWalkable(Vector3 normal)
        {
            return Vector3.Angle(normal, Vector3.up) <= _slopeLimit + 0.1f;
        }

        /// <summary>
        /// 角色下降且离地很近时，将 CharacterController 轻微吸附到地面。
        /// </summary>
        /// <param name="characterController">当前 CharacterController。</param>
        /// <returns>是否执行了贴地移动。</returns>
        protected virtual bool TrySnapToGround(CharacterController characterController)
        {
            if (_groundSnapDistance <= 0f || !_hasGroundSupport || !_groundSupportIsWalkable)
                return false;

            if (IsWallClimbing || VerticalVelocity > 0f)
                return false;

            if ((LastCollisionFlags & CollisionFlags.Below) != 0)
                return false;

            if ((LastCollisionFlags & CollisionFlags.Sides) != 0)
                return false;

            if (GroundDistance <= _groundContactDistance || GroundDistance > _groundSnapDistance)
                return false;

            // 向下移动略多于当前间隙，让 CharacterController 产生真实 Below 碰撞并消除视觉浮空。
            float snapDistance = Mathf.Min(_groundSnapDistance, GroundDistance + _groundContactDistance);
            CollisionFlags snapFlags = characterController.Move(Vector3.down * snapDistance);
            LastCollisionFlags |= snapFlags;
            ProbeGround();
            return (snapFlags & CollisionFlags.Below) != 0;
        }

        /// <summary>
        /// 根据 Move 碰撞结果和极近距离地面支撑刷新最终 grounded 状态。
        /// </summary>
        protected virtual void RefreshGroundedState()
        {
            bool hitGround = (LastCollisionFlags & CollisionFlags.Below) != 0;
            bool hasContactSupport = _hasGroundSupport &&
                                     _groundSupportIsWalkable &&
                                     GroundDistance <= _groundContactDistance &&
                                     VerticalVelocity <= 0f;

            IsGrounded = hitGround || hasContactSupport;
            if (IsGrounded && VerticalVelocity < 0f)
                VerticalVelocity = -_groundStickVelocity;
        }

        /// <summary>
        /// 消耗跳跃缓冲并执行跳跃。
        /// </summary>
        protected virtual void TryConsumeJump()
        {
            if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f)
                return;

            VerticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _wallClimbTimer = 0f;
        }

        /// <summary>
        /// 尝试进入或维持爬墙状态。
        /// </summary>
        protected virtual void TryStartWallClimb()
        {
            if (!_wallClimbEnabled || !ReadWallClimbInput())
            {
                _wallClimbTimer = 0f;
                return;
            }

            if (_wallClimbRequiresForwardInput && MoveInput.y <= 0.1f)
            {
                _wallClimbTimer = 0f;
                return;
            }

            if (!TryDetectWall(out Vector3 wallNormal))
            {
                _wallClimbTimer = 0f;
                return;
            }

            WallNormal = wallNormal;
            if (_wallClimbTimer <= 0f)
                _wallClimbTimer = _wallClimbDuration;

            VerticalVelocity = Mathf.Max(VerticalVelocity, 0f);
        }

        /// <summary>
        /// 检测角色前方可爬墙面。
        /// </summary>
        /// <param name="wallNormal">检测到的墙面法线。</param>
        /// <returns>是否检测到墙面。</returns>
        protected virtual bool TryDetectWall(out Vector3 wallNormal)
        {
            Vector3 origin = transform.position + Vector3.up * (CharacterController.height * 0.5f);
            Vector3 direction = transform.forward;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, _wallCheckDistance, _wallMask, QueryTriggerInteraction.Ignore))
            {
                wallNormal = hit.normal;
                return Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.25f;
            }

            wallNormal = Vector3.forward;
            return false;
        }

        /// <summary>
        /// 计算当前平面移动方向。
        /// </summary>
        /// <param name="input">二维移动输入。</param>
        /// <returns>世界空间平面方向。</returns>
        protected virtual Vector3 ResolveMoveDirection(Vector2 input)
        {
            Vector3 direction = GetWorldMoveDirection(input);
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();
            return direction;
        }

        /// <summary>
        /// 计算当前平面速度。
        /// </summary>
        /// <param name="moveDirection">世界空间移动方向。</param>
        /// <param name="deltaTime">本帧时间。</param>
        /// <returns>当前平面速度。</returns>
        protected virtual Vector3 CalculatePlanarVelocity(Vector3 moveDirection, float deltaTime)
        {
            if (IsDashing)
            {
                PlanarVelocity = _dashVelocity;
                return PlanarVelocity;
            }

            if (IsWallClimbing)
            {
                PlanarVelocity = Vector3.zero;
                return PlanarVelocity;
            }

            IsRunning = ReadSprintInput() || (_runByDefault && MoveInput.magnitude >= _runInputThreshold);
            float speed = IsRunning ? _runSpeed : _walkSpeed;
            Vector3 targetVelocity = moveDirection * (speed * MoveInput.magnitude);

            if (IsGrounded && _hasGroundSupport && _groundSupportIsWalkable)
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, GroundNormal);

            float acceleration = IsGrounded ? _groundAcceleration : _airAcceleration;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, targetVelocity, acceleration * deltaTime);

            bool hasSteepGroundSupport = _hasGroundSupport &&
                                         !_groundSupportIsWalkable &&
                                         GroundDistance <= _groundSnapDistance &&
                                         VerticalVelocity <= 0f;
            if (hasSteepGroundSupport)
                PlanarVelocity += Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized * _steepSlopeSlideSpeed;

            return PlanarVelocity;
        }

        /// <summary>
        /// 计算当前垂直速度。
        /// </summary>
        /// <param name="deltaTime">本帧时间。</param>
        /// <returns>垂直速度向量。</returns>
        protected virtual Vector3 CalculateVerticalVelocity(float deltaTime)
        {
            if (IsWallClimbing)
            {
                VerticalVelocity = _wallClimbSpeed;
                return Vector3.up * VerticalVelocity;
            }

            if (IsGrounded && VerticalVelocity < 0f)
                VerticalVelocity = -_groundStickVelocity;

            if (!_dashIgnoresGravity || !IsDashing)
                VerticalVelocity += _gravity * deltaTime;

            return Vector3.up * VerticalVelocity;
        }

        /// <summary>
        /// 计算并衰减外部速度。
        /// </summary>
        /// <param name="deltaTime">本帧时间。</param>
        /// <returns>外部速度。</returns>
        protected virtual Vector3 CalculateExternalVelocity(float deltaTime)
        {
            Vector3 result = ExternalVelocity;
            ExternalVelocity = Vector3.MoveTowards(ExternalVelocity, Vector3.zero, _knockbackDamping * deltaTime);
            if (ExternalVelocity.magnitude <= _externalVelocityStopThreshold)
                ExternalVelocity = Vector3.zero;
            return result;
        }

        /// <summary>
        /// 处理 CharacterController 碰撞结果。
        /// </summary>
        protected virtual void ProcessCollisionFlags()
        {
            if ((LastCollisionFlags & CollisionFlags.Above) != 0 && VerticalVelocity > 0f)
                VerticalVelocity = 0f;
        }

        /// <summary>
        /// 刷新当前主要运动模式。
        /// </summary>
        protected virtual void RefreshMovementMode()
        {
            if (IsDashing)
                MovementMode = ECharacterMotor3CMode.Dashing;
            else if (IsWallClimbing)
                MovementMode = ECharacterMotor3CMode.WallClimbing;
            else if (ExternalVelocity.sqrMagnitude > MinDirectionSqrMagnitude)
                MovementMode = ECharacterMotor3CMode.Knockback;
            else if (IsGrounded)
                MovementMode = ECharacterMotor3CMode.Grounded;
            else
                MovementMode = ECharacterMotor3CMode.Airborne;
        }

        /// <summary>
        /// 按移动或相机方向旋转角色。
        /// </summary>
        /// <param name="moveDirection">当前移动方向。</param>
        /// <param name="deltaTime">本帧时间。</param>
        protected virtual void RotateByMotion(Vector3 moveDirection, float deltaTime)
        {
            if (!_rotateToMoveDirection)
                return;

            if (!TryGetFacingDirection(moveDirection, out Vector3 facingDirection))
                return;

            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude < MinDirectionSqrMagnitude)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * deltaTime);
        }

        /// <summary>
        /// 获取当前冲刺方向。
        /// </summary>
        /// <param name="fallbackDirection">外部传入的兜底方向。</param>
        /// <returns>世界空间冲刺方向。</returns>
        protected virtual Vector3 ResolveDashDirection(Vector3 fallbackDirection)
        {
            if (_dashUsesMoveDirection)
            {
                Vector3 moveDirection = ResolveMoveDirection(MoveInput);
                if (moveDirection.sqrMagnitude > MinDirectionSqrMagnitude)
                    return moveDirection;
            }

            if (fallbackDirection.sqrMagnitude > MinDirectionSqrMagnitude)
                return fallbackDirection;

            return transform.forward;
        }

        /// <summary>
        /// 当前地面坡度是否可正常行走。
        /// </summary>
        /// <returns>是否可行走。</returns>
        protected virtual bool IsSlopeWalkable()
        {
            return Vector3.Angle(GroundNormal, Vector3.up) <= _slopeLimit;
        }

        /// <summary>
        /// 缓存 CharacterController。
        /// </summary>
        /// <returns>当前 CharacterController。</returns>
        protected CharacterController CacheCharacterController()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();
            return _characterController;
        }

        /// <summary>
        /// 清除一次性输入缓存。
        /// </summary>
        protected virtual void ClearTransientInput()
        {
            _jumpBufferTimer = 0f;
            _dashQueued = false;
            _sprintHeld = false;
            _wallClimbHeld = false;
        }
    }
}
