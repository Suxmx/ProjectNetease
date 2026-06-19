using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 占领点目标。队伍通过不间断占领进度条夺取控制权，
    /// 夺取后进入持守倒计时，倒计时归零得分。
    /// 服务端 tick 驱动进度计算，SyncVar 同步给客户端 UI。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BattleCaptureObjective : TickNetworkBehaviour
    {
        [SerializeField] private float _captureSeconds = 6f;
        [SerializeField] private float _holdSecondsToScore = 30f;
        [SerializeField] private bool _decayWhenEmpty = true;

        private readonly SyncVar<ETeam> _ownerTeam = new();
        private readonly SyncVar<ETeam> _capturingTeam = new();
        private readonly SyncVar<float> _captureProgress = new();
        private readonly SyncVar<float> _remainingHoldSeconds = new();
        private readonly HashSet<CombatState> _occupants = new();

        public ETeam OwnerTeam => _ownerTeam.Value;
        public ETeam CapturingTeam => _capturingTeam.Value;
        public float CaptureProgress => _captureProgress.Value;
        public float RemainingHoldSeconds => _remainingHoldSeconds.Value;

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick);
        }

        /// <summary>服务端初始化占领点状态。</summary>
        public override void OnStartServer()
        {
            _ownerTeam.Value = ETeam.Neutral;
            _capturingTeam.Value = ETeam.Neutral;
            _captureProgress.Value = 0f;
            _remainingHoldSeconds.Value = _holdSecondsToScore;
        }

        /// <summary>每 tick 推进占领进度和持守倒计时。</summary>
        protected override void TimeManager_OnTick()
        {
            if (!IsServerStarted)
                return;

            float delta = (float)TimeManager.TickDelta;
            ETeam uncontestedTeam = GetUncontestedTeam();

            // --- 非归属队伍且无争议：推进占领进度 ---
            if (uncontestedTeam != ETeam.Neutral && uncontestedTeam != _ownerTeam.Value)
            {
                if (_capturingTeam.Value != uncontestedTeam)
                {
                    _capturingTeam.Value = uncontestedTeam;
                    _captureProgress.Value = 0f;
                }

                _captureProgress.Value = Mathf.Clamp01(_captureProgress.Value + delta / Mathf.Max(0.01f, _captureSeconds));

                // --- 进度满：夺取所有权 ---
                if (_captureProgress.Value >= 1f)
                {
                    _ownerTeam.Value = uncontestedTeam;
                    _capturingTeam.Value = ETeam.Neutral;
                    _captureProgress.Value = 0f;
                    _remainingHoldSeconds.Value = _holdSecondsToScore;
                }
            }
            // --- 无人占领：进度衰减 ---
            else if (uncontestedTeam == ETeam.Neutral)
            {
                if (_decayWhenEmpty)
                    _captureProgress.Value = Mathf.Max(0f, _captureProgress.Value - delta / Mathf.Max(0.01f, _captureSeconds));
                if (_captureProgress.Value <= 0f)
                    _capturingTeam.Value = ETeam.Neutral;
            }

            // --- 已归属队伍：持守倒计时递减 ---
            if (_ownerTeam.Value != ETeam.Neutral)
                _remainingHoldSeconds.Value = Mathf.Max(0f, _remainingHoldSeconds.Value - delta);
        }

        /// <summary>战斗体进入触发区域：加入占领者集合。</summary>
        private void OnTriggerEnter(Collider other)
        {
            CombatState state = other.GetComponentInParent<CombatState>();
            if (state != null)
                _occupants.Add(state);
        }

        /// <summary>战斗体离开触发区域：移出占领者集合。</summary>
        private void OnTriggerExit(Collider other)
        {
            CombatState state = other.GetComponentInParent<CombatState>();
            if (state != null)
                _occupants.Remove(state);
        }

        /// <summary>获取当前无争议的占领队伍（仅一队在场），多队在场返回 Neutral。</summary>
        private ETeam GetUncontestedTeam()
        {
            ETeam result = ETeam.Neutral;

            foreach (CombatState state in _occupants)
            {
                if (state == null || state.IsDead || state.Team == ETeam.Neutral)
                    continue;

                if (result == ETeam.Neutral)
                {
                    result = state.Team;
                    continue;
                }

                // --- 多队在场：有争议 ---
                if (result != state.Team)
                    return ETeam.Neutral;
            }

            return result;
        }
    }
}
