using MemoFramework.Extension;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Converts MemoFramework gameplay input into owner skill start requests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartySkillInputDriver : MonoBehaviour
    {
        [SerializeField] private PartyNetworkSkillController _networkSkillController;
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _autoFindCamera = true;
        [SerializeField] private bool _inputEnabled = true;
        [SerializeField] private int _defaultSkillId = 1;

        /// <summary>
        /// Assigns the network skill controller used by this input bridge.
        /// </summary>
        public void Configure(PartyNetworkSkillController networkSkillController)
        {
            _networkSkillController = networkSkillController;
        }

        /// <summary>
        /// Enables or disables local skill input collection.
        /// </summary>
        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
        }

        /// <summary>
        /// Polls the attack input and starts the configured skill on the local owner.
        /// </summary>
        private void Update()
        {
            if (!_inputEnabled)
                return;

            ResolveReferences();
            if (_networkSkillController == null)
                return;

            if (InputData.HasEventStart(InputEvent.Shoot))
                _networkSkillController.TryStartSkill(_defaultSkillId, ResolveAimDirection());
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_networkSkillController == null)
                _networkSkillController = GetComponent<PartyNetworkSkillController>();

            if (_autoFindCamera && _camera == null)
                _camera = Camera.main;
        }

        /// <summary>
        /// Resolves a planar skill aim direction from the mouse cursor when possible.
        /// </summary>
        private Vector3 ResolveAimDirection()
        {
            Vector3 up = transform.up;
            if (_camera != null)
            {
                Ray ray = _camera.ScreenPointToRay(InputData.MouseScreenPosition);
                Plane groundPlane = new(up, transform.position);
                if (groundPlane.Raycast(ray, out float distance))
                {
                    Vector3 cursorDirection = Vector3.ProjectOnPlane(ray.GetPoint(distance) - transform.position, up);
                    if (cursorDirection.sqrMagnitude > 0.0001f)
                        return cursorDirection.normalized;
                }

                Vector3 cameraForward = Vector3.ProjectOnPlane(_camera.transform.forward, up);
                if (cameraForward.sqrMagnitude > 0.0001f)
                    return cameraForward.normalized;
            }

            Vector3 actorForward = Vector3.ProjectOnPlane(transform.forward, up);
            return actorForward.sqrMagnitude > 0.0001f ? actorForward.normalized : Vector3.forward;
        }
    }
}
