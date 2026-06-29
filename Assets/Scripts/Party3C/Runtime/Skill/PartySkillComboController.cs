using System.Collections.Generic;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Buffers skill inputs and consumes them through active timeline combo windows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartySkillComboController : MonoBehaviour
    {
        private struct ActiveWindow
        {
            public int Sequence;
            public int NodeId;
            public EPartySkillInputAction InputAction;
            public int NextSkillId;
            public bool CancelCurrentSkill;
        }

        [SerializeField] private PartyNetworkSkillController _networkSkillController;
        [SerializeField] private PartySkillRuntime _skillRuntime;
        [SerializeField] private int _defaultPrimarySkillId = 1080829965;
        [SerializeField, Min(0f)] private float _inputBufferSeconds = 0.2f;
        [SerializeField] private bool _consumeBufferedInputOnWindowOpen = true;

        private readonly List<ActiveWindow> _activeWindows = new();
        private EPartySkillInputAction _bufferedInputAction;
        private Vector3 _bufferedAimDirection;
        private float _bufferedInputTime;
        private bool _hasBufferedInput;

        /// <summary>
        /// Assigns runtime references used by combo input consumption.
        /// </summary>
        public void Configure(PartyNetworkSkillController networkSkillController, PartySkillRuntime skillRuntime)
        {
            _networkSkillController = networkSkillController;
            _skillRuntime = skillRuntime;
        }

        /// <summary>
        /// Queues one local skill input and consumes it immediately if a valid window is active.
        /// </summary>
        public void QueueInput(EPartySkillInputAction inputAction, Vector3 aimDirection)
        {
            ResolveReferences();
            _bufferedInputAction = inputAction;
            _bufferedAimDirection = NormalizeAimDirection(aimDirection);
            _bufferedInputTime = Time.time;
            _hasBufferedInput = true;

            TryConsumeBufferedInput();
        }

        /// <summary>
        /// Opens or updates one combo window for the current skill sequence.
        /// </summary>
        public void OpenComboWindow(int sequence, int nodeId, EPartySkillInputAction inputAction, int nextSkillId, bool cancelCurrentSkill)
        {
            if (nextSkillId == 0)
                return;

            int existingIndex = FindWindowIndex(sequence, nodeId);
            ActiveWindow window = new()
            {
                Sequence = sequence,
                NodeId = nodeId,
                InputAction = inputAction,
                NextSkillId = nextSkillId,
                CancelCurrentSkill = cancelCurrentSkill
            };

            if (existingIndex >= 0)
                _activeWindows[existingIndex] = window;
            else
                _activeWindows.Add(window);

            if (_consumeBufferedInputOnWindowOpen)
                TryConsumeBufferedInput();
        }

        /// <summary>
        /// Closes one combo window by skill sequence and timeline node id.
        /// </summary>
        public void CloseComboWindow(int sequence, int nodeId)
        {
            int index = FindWindowIndex(sequence, nodeId);
            if (index >= 0)
                _activeWindows.RemoveAt(index);
        }

        /// <summary>
        /// Expires buffered input when no active combo window consumes it in time.
        /// </summary>
        private void Update()
        {
            if (_hasBufferedInput && Time.time - _bufferedInputTime > _inputBufferSeconds)
                _hasBufferedInput = false;
        }

        /// <summary>
        /// Consumes the buffered input through an active window or starts the default attack.
        /// </summary>
        private bool TryConsumeBufferedInput()
        {
            if (!_hasBufferedInput)
                return false;

            if (Time.time - _bufferedInputTime > _inputBufferSeconds)
            {
                _hasBufferedInput = false;
                return false;
            }

            ResolveReferences();
            if (_networkSkillController == null)
                return false;

            if (TryFindMatchingWindow(_bufferedInputAction, out ActiveWindow window))
                return TryStartWindowSkill(window);

            if (_skillRuntime != null && _skillRuntime.HasActiveSkill)
                return false;

            int defaultSkillId = ResolveDefaultSkillId(_bufferedInputAction);
            if (defaultSkillId == 0)
                return false;

            Vector3 aimDirection = _bufferedAimDirection;
            _hasBufferedInput = false;
            if (_networkSkillController.TryStartSkill(defaultSkillId, aimDirection))
                return true;

            _hasBufferedInput = true;
            return false;
        }

        /// <summary>
        /// Starts the next skill selected by a combo window.
        /// </summary>
        private bool TryStartWindowSkill(ActiveWindow window)
        {
            if (!_networkSkillController.CanStartSkill(window.NextSkillId))
                return false;

            Vector3 aimDirection = _bufferedAimDirection;
            _hasBufferedInput = false;

            if (window.CancelCurrentSkill && _skillRuntime != null)
            {
                _skillRuntime.StopSkill(window.Sequence);
                _networkSkillController.NotifySkillStopped(window.Sequence);
            }

            if (_networkSkillController.TryStartSkill(window.NextSkillId, aimDirection))
                return true;

            _hasBufferedInput = true;
            return false;
        }

        /// <summary>
        /// Resolves the default starter skill for an input action.
        /// </summary>
        private int ResolveDefaultSkillId(EPartySkillInputAction inputAction)
        {
            return inputAction == EPartySkillInputAction.PrimaryAttack ? _defaultPrimarySkillId : 0;
        }

        /// <summary>
        /// Finds the first active combo window that accepts the input action.
        /// </summary>
        private bool TryFindMatchingWindow(EPartySkillInputAction inputAction, out ActiveWindow window)
        {
            for (int i = 0; i < _activeWindows.Count; i++)
            {
                if (_activeWindows[i].InputAction != inputAction)
                    continue;

                window = _activeWindows[i];
                return true;
            }

            window = default;
            return false;
        }

        /// <summary>
        /// Finds one active combo window by owning skill sequence and node id.
        /// </summary>
        private int FindWindowIndex(int sequence, int nodeId)
        {
            for (int i = 0; i < _activeWindows.Count; i++)
            {
                ActiveWindow window = _activeWindows[i];
                if (window.Sequence == sequence && window.NodeId == nodeId)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Finds optional sibling references when they were not explicitly assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_networkSkillController == null)
                _networkSkillController = GetComponent<PartyNetworkSkillController>();

            if (_skillRuntime == null)
                _skillRuntime = GetComponent<PartySkillRuntime>();
        }

        /// <summary>
        /// Returns a stable planar aim direction for queued combo inputs.
        /// </summary>
        private Vector3 NormalizeAimDirection(Vector3 aimDirection)
        {
            Vector3 planar = Vector3.ProjectOnPlane(aimDirection, transform.up);
            if (planar.sqrMagnitude <= 0.0001f)
                planar = Vector3.ProjectOnPlane(transform.forward, transform.up);

            return planar.sqrMagnitude > 0.0001f ? planar.normalized : transform.forward;
        }
    }
}
