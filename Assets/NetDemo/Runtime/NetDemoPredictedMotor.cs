using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetDemo
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetDemoPredictedMotor : TickNetworkBehaviour
    {
        public struct MoveReplicateData : IReplicateData
        {
            public Vector2 MoveInput;
            public Vector3 LookDirection;

            private uint _tick;

            public MoveReplicateData(Vector2 moveInput, Vector3 lookDirection)
            {
                MoveInput = moveInput;
                LookDirection = lookDirection;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct MoveReconcileData : IReconcileData
        {
            public PredictionRigidbody Rigidbody;
            public bool IsDead;

            private uint _tick;

            public MoveReconcileData(PredictionRigidbody rigidbody, bool isDead)
            {
                Rigidbody = rigidbody;
                IsDead = isDead;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnSpeed = 720f;

        private readonly PredictionRigidbody _predictionRigidbody = new();
        private Rigidbody _rigidbody;
        private NetDemoHealth _health;
        private Vector3 _lastLookDirection = Vector3.forward;

        public Vector3 CurrentLookDirection => _lastLookDirection;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _health = GetComponent<NetDemoHealth>();

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
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            MoveReconcileData rd = new(_predictionRigidbody, _health != null && _health.IsDead);
            PerformReconcile(rd);
        }

        private MoveReplicateData BuildMoveData()
        {
            if (!IsOwner)
                return default;

            Vector2 move = ReadMoveInput();
            Vector3 lookDirection = ReadLookDirection();
            if (lookDirection.sqrMagnitude > 0.0001f)
                _lastLookDirection = lookDirection.normalized;

            return new MoveReplicateData(move, _lastLookDirection);
        }

        private Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed)
                move.y += 1f;
            if (keyboard.sKey.isPressed)
                move.y -= 1f;
            if (keyboard.dKey.isPressed)
                move.x += 1f;
            if (keyboard.aKey.isPressed)
                move.x -= 1f;

            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        private Vector3 ReadLookDirection()
        {
            Mouse mouse = Mouse.current;
            Camera camera = Camera.main;
            if (mouse == null || camera == null)
                return _lastLookDirection;

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            Plane ground = new(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
                return _lastLookDirection;

            Vector3 point = ray.GetPoint(distance);
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : _lastLookDirection;
        }

        [Replicate]
        private void PerformReplicate(MoveReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;
            bool dead = _health != null && _health.IsDead;

            Vector3 desiredVelocity = Vector3.zero;
            if (!dead)
            {
                Vector2 move = data.MoveInput.sqrMagnitude > 1f ? data.MoveInput.normalized : data.MoveInput;
                desiredVelocity = new Vector3(move.x, 0f, move.y) * _moveSpeed;

                Vector3 lookDirection = data.LookDirection;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.0001f)
                {
                    lookDirection.Normalize();
                    _lastLookDirection = lookDirection;

                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                    Quaternion nextRotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _turnSpeed * delta);
                    _predictionRigidbody.MoveRotation(nextRotation);
                }
            }

            Vector3 velocity = GetVelocity(_rigidbody);
            velocity.x = desiredVelocity.x;
            velocity.z = desiredVelocity.z;
            if (dead)
                velocity.y = 0f;

            _predictionRigidbody.Velocity(velocity);
            _predictionRigidbody.Simulate();
        }

        [Reconcile]
        private void PerformReconcile(MoveReconcileData data, Channel channel = Channel.Unreliable)
        {
            _predictionRigidbody.Reconcile(data.Rigidbody);
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
