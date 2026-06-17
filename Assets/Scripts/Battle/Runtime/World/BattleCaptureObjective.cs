using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    [RequireComponent(typeof(Collider))]
    public sealed class BattleCaptureObjective : TickNetworkBehaviour
    {
        [SerializeField] private float _captureSeconds = 6f;
        [SerializeField] private float _holdSecondsToScore = 30f;
        [SerializeField] private bool _decayWhenEmpty = true;

        private readonly SyncVar<BattleTeam> _ownerTeam = new();
        private readonly SyncVar<BattleTeam> _capturingTeam = new();
        private readonly SyncVar<float> _captureProgress = new();
        private readonly SyncVar<float> _remainingHoldSeconds = new();
        private readonly HashSet<BattleCombatState> _occupants = new();

        public BattleTeam OwnerTeam => _ownerTeam.Value;
        public BattleTeam CapturingTeam => _capturingTeam.Value;
        public float CaptureProgress => _captureProgress.Value;
        public float RemainingHoldSeconds => _remainingHoldSeconds.Value;

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnStartServer()
        {
            _ownerTeam.Value = BattleTeam.Neutral;
            _capturingTeam.Value = BattleTeam.Neutral;
            _captureProgress.Value = 0f;
            _remainingHoldSeconds.Value = _holdSecondsToScore;
        }

        protected override void TimeManager_OnTick()
        {
            if (!IsServerStarted)
                return;

            float delta = (float)TimeManager.TickDelta;
            BattleTeam uncontestedTeam = GetUncontestedTeam();

            if (uncontestedTeam != BattleTeam.Neutral && uncontestedTeam != _ownerTeam.Value)
            {
                if (_capturingTeam.Value != uncontestedTeam)
                {
                    _capturingTeam.Value = uncontestedTeam;
                    _captureProgress.Value = 0f;
                }

                _captureProgress.Value = Mathf.Clamp01(_captureProgress.Value + delta / Mathf.Max(0.01f, _captureSeconds));
                if (_captureProgress.Value >= 1f)
                {
                    _ownerTeam.Value = uncontestedTeam;
                    _capturingTeam.Value = BattleTeam.Neutral;
                    _captureProgress.Value = 0f;
                    _remainingHoldSeconds.Value = _holdSecondsToScore;
                }
            }
            else if (uncontestedTeam == BattleTeam.Neutral)
            {
                if (_decayWhenEmpty)
                    _captureProgress.Value = Mathf.Max(0f, _captureProgress.Value - delta / Mathf.Max(0.01f, _captureSeconds));
                if (_captureProgress.Value <= 0f)
                    _capturingTeam.Value = BattleTeam.Neutral;
            }

            if (_ownerTeam.Value != BattleTeam.Neutral)
                _remainingHoldSeconds.Value = Mathf.Max(0f, _remainingHoldSeconds.Value - delta);
        }

        private void OnTriggerEnter(Collider other)
        {
            BattleCombatState state = other.GetComponentInParent<BattleCombatState>();
            if (state != null)
                _occupants.Add(state);
        }

        private void OnTriggerExit(Collider other)
        {
            BattleCombatState state = other.GetComponentInParent<BattleCombatState>();
            if (state != null)
                _occupants.Remove(state);
        }

        private BattleTeam GetUncontestedTeam()
        {
            BattleTeam result = BattleTeam.Neutral;

            foreach (BattleCombatState state in _occupants)
            {
                if (state == null || state.IsDead || state.Team == BattleTeam.Neutral)
                    continue;

                if (result == BattleTeam.Neutral)
                {
                    result = state.Team;
                    continue;
                }

                if (result != state.Team)
                    return BattleTeam.Neutral;
            }

            return result;
        }
    }
}
