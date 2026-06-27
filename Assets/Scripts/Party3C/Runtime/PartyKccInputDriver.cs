using MemoFramework.Extension;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Converts MemoFramework input data into world-space intent for a party KCC character.
    /// </summary>
    public sealed class PartyKccInputDriver : MonoBehaviour
    {
        [SerializeField] private PartyKccCharacterController _character;
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _autoFindCamera = true;
        [SerializeField] private bool _inputEnabled = true;
        [SerializeField] private bool _driveCameraLook = true;
        [SerializeField] private bool _faceCameraForwardWhenNoMove = true;

        private Vector2 _lastMouseScreenPosition;
        private bool _hasLastMouseScreenPosition;

        /// <summary>
        /// Assigns the driven character reference from editor tooling or custom spawners.
        /// </summary>
        public void ConfigureReferences(PartyKccCharacterController character)
        {
            _character = character;
        }

        /// <summary>
        /// Enables or disables local input collection, which can later be controlled by network ownership.
        /// </summary>
        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
        }

        /// <summary>
        /// Finds the character on the same object when no explicit reference was assigned.
        /// </summary>
        private void Awake()
        {
            if (_character == null)
                _character = GetComponent<PartyKccCharacterController>();
        }

        /// <summary>
        /// Reads MF input each frame and forwards one movement intent snapshot to the KCC controller.
        /// </summary>
        private void Update()
        {
            if (!_inputEnabled || _character == null)
                return;

            if (_autoFindCamera && _camera == null)
                _camera = Camera.main;

            if (_driveCameraLook)
                _character.AddCameraLookInput(ReadMouseDelta());

            PartyKccCharacterInputs inputs = new()
            {
                MoveWorld = BuildCameraRelativeMove(InputData.MoveInput),
                RunHeld = InputData.HasEvent(InputEvent.Run),
                JumpPressed = InputData.HasEventStart(InputEvent.Jump),
                DashPressed = InputData.HasEventStart(InputEvent.Dash)
            };
            inputs.LookWorld = ResolveLookWorld(inputs.MoveWorld);

            _character.SetInputs(inputs);
        }

        /// <summary>
        /// Calculates one frame of mouse movement from MF's stored pointer data.
        /// </summary>
        private Vector2 ReadMouseDelta()
        {
            if (InputData.MouseDelta.sqrMagnitude > 0f)
            {
                _lastMouseScreenPosition = InputData.MouseScreenPosition;
                _hasLastMouseScreenPosition = true;
                return InputData.MouseDelta;
            }

            Vector2 currentMousePosition = InputData.MouseScreenPosition;
            if (!_hasLastMouseScreenPosition)
            {
                _lastMouseScreenPosition = currentMousePosition;
                _hasLastMouseScreenPosition = true;
                return Vector2.zero;
            }

            Vector2 delta = currentMousePosition - _lastMouseScreenPosition;
            _lastMouseScreenPosition = currentMousePosition;
            return delta;
        }

        /// <summary>
        /// Chooses the character facing direction from movement or the independent camera target.
        /// </summary>
        private Vector3 ResolveLookWorld(Vector3 moveWorld)
        {
            if (moveWorld.sqrMagnitude > 0.0001f)
                return moveWorld;

            return _faceCameraForwardWhenNoMove ? BuildCameraTargetForward() : Vector3.zero;
        }

        /// <summary>
        /// Converts a 2D move vector into a world-space vector on the character plane.
        /// </summary>
        private Vector3 BuildCameraRelativeMove(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <= 0f)
                return Vector3.zero;

            Vector3 up = _character.CharacterUp;
            Vector3 forward = BuildCameraForward();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 move = (forward * moveInput.y) + (right * moveInput.x);
            return Vector3.ClampMagnitude(Vector3.ProjectOnPlane(move, up), 1f);
        }

        /// <summary>
        /// Gets a usable planar camera forward direction, including topdown camera fallback.
        /// </summary>
        private Vector3 BuildCameraForward()
        {
            Vector3 up = _character != null ? _character.CharacterUp : Vector3.up;
            Transform cameraTransform = _camera != null ? _camera.transform : null;
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, up) : Vector3.zero;

            if (forward.sqrMagnitude <= 0.0001f && cameraTransform != null)
                forward = Vector3.ProjectOnPlane(cameraTransform.up, up);

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.ProjectOnPlane(transform.forward, up);

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// Gets the independent camera target forward direction for idle character facing.
        /// </summary>
        private Vector3 BuildCameraTargetForward()
        {
            Vector3 up = _character != null ? _character.CharacterUp : Vector3.up;
            Transform followPoint = _character != null ? _character.CameraFollowPoint : null;
            Vector3 forward = followPoint != null ? Vector3.ProjectOnPlane(followPoint.forward, up) : Vector3.zero;

            if (forward.sqrMagnitude <= 0.0001f)
                forward = BuildCameraForward();

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.zero;
        }
    }
}
