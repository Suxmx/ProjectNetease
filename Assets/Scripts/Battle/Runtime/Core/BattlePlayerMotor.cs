using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BattlePlayerMotor : TickNetworkBehaviour
    {
        public struct BattleReconcileData : IReconcileData
        {
            public PredictionRigidbody Rigidbody;
            public BattleSkillReconcileState SkillState;
            public Vector3 AimDirection;
            public bool IsDead;

            private uint _tick;

            public BattleReconcileData(PredictionRigidbody rigidbody, BattleSkillReconcileState skillState, Vector3 aimDirection, bool isDead)
            {
                Rigidbody = rigidbody;
                SkillState = skillState;
                AimDirection = aimDirection;
                IsDead = isDead;
                _tick = 0;
            }

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
            public void Dispose() { }
        }

        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnSpeed = 720f;

        private readonly PredictionRigidbody _predictionRigidbody = new();
        private Rigidbody _rigidbody;
        private BattlePlayerInput _input;
        private BattleSkillController _skillController;
        private BattleCombatState _combatState;
        private BattleAttributeSet _attributeSet;
        private Vector3 _aimDirection = Vector3.forward;
        private Vector3 _predictedVelocity;
        private Vector3 _predictedDisplacement;
        private bool _hasPendingTeleport;
        private Vector3 _pendingTeleportPosition;

        public Vector3 AimDirection => _aimDirection;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _input = GetComponent<BattlePlayerInput>();
            _skillController = GetComponent<BattleSkillController>();
            _combatState = GetComponent<BattleCombatState>();
            _attributeSet = GetComponent<BattleAttributeSet>();

            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _predictionRigidbody.Initialize(_rigidbody);
        }

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildReplicateData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            BattleSkillReconcileState skillState = _skillController != null
                ? _skillController.CaptureState(TimeManager.LocalTick)
                : default;

            BattleReconcileData data = new(_predictionRigidbody, skillState, _aimDirection, _combatState != null && _combatState.IsDead);
            PerformReconcile(data);
        }

        public void AddPredictedVelocity(Vector3 value)
        {
            _predictedVelocity += value;
        }

        public void AddPredictedDisplacement(Vector3 value)
        {
            _predictedDisplacement += value;
        }

        public void TeleportPredicted(Vector3 position)
        {
            _hasPendingTeleport = true;
            _pendingTeleportPosition = position;
        }

        private BattleReplicateData BuildReplicateData()
        {
            if (!IsOwner || _input == null)
                return default;

            Vector2 move = _input.ReadMove();
            Vector3 aim = _input.ReadAimDirection(transform.position, _aimDirection);
            BattleSkillCommand command = _input.ConsumeSkillCommand(aim, TimeManager.LocalTick);

            if (command.Type != BattleSkillCommandType.None && _skillController != null && command.SkillId == 0)
            {
                Hoshino.SkillDefinition skill = _skillController.FindSkillBySlot(command.Slot);
                if (skill != null)
                    command.SkillId = skill.SkillId;
            }

            return new BattleReplicateData(move, aim, command);
        }

        [Replicate]
        private void PerformReplicate(BattleReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;
            bool canAct = _combatState == null || _combatState.CanAct;

            Vector3 aim = data.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
                _aimDirection = aim.normalized;

            ResetPredictedModifiers();

            if (canAct)
                _skillController?.TickPredicted(this, data.SkillCommand, _aimDirection, data.GetTick(), state, delta);
            if (canAct && IsServerStarted)
                _skillController?.TickServerAuthority(this, data.SkillCommand, _aimDirection, data.GetTick(), state);

            Vector3 desiredVelocity = Vector3.zero;
            if (canAct)
            {
                Vector2 input = data.MoveInput.sqrMagnitude > 1f ? data.MoveInput.normalized : data.MoveInput;
                float moveMultiplier = _attributeSet != null ? _attributeSet.MoveSpeedMultiplier : 1f;
                desiredVelocity = new Vector3(input.x, 0f, input.y) * (_moveSpeed * moveMultiplier);
            }

            desiredVelocity += _predictedVelocity;

            if (_hasPendingTeleport)
                _predictionRigidbody.MovePosition(_pendingTeleportPosition);
            else if (_predictedDisplacement.sqrMagnitude > 0.000001f)
                _predictionRigidbody.MovePosition(_rigidbody.position + _predictedDisplacement);

            if (_aimDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_aimDirection, Vector3.up);
                Quaternion nextRotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _turnSpeed * delta);
                _predictionRigidbody.MoveRotation(nextRotation);
            }

            Vector3 velocity = GetVelocity(_rigidbody);
            velocity.x = desiredVelocity.x;
            velocity.z = desiredVelocity.z;
            if (!canAct)
                velocity.y = 0f;

            _predictionRigidbody.Velocity(velocity);
            _predictionRigidbody.Simulate();
        }

        [Reconcile]
        private void PerformReconcile(BattleReconcileData data, Channel channel = Channel.Unreliable)
        {
            _predictionRigidbody.Reconcile(data.Rigidbody);
            _aimDirection = data.AimDirection.sqrMagnitude > 0.0001f ? data.AimDirection.normalized : _aimDirection;
            _skillController?.ApplyState(data.SkillState);
        }

        private void ResetPredictedModifiers()
        {
            _predictedVelocity = Vector3.zero;
            _predictedDisplacement = Vector3.zero;
            _hasPendingTeleport = false;
            _pendingTeleportPosition = Vector3.zero;
        }

        private static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
    }
}
