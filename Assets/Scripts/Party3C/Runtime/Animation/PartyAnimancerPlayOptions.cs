using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Carries per-request playback overrides for an Animancer action layer.
    /// </summary>
    public readonly struct PartyAnimancerPlayOptions
    {
        /// <summary>
        /// Creates playback options for an Animancer action request.
        /// </summary>
        public PartyAnimancerPlayOptions(float fadeDuration, float speed, float normalizedStartTime, bool restartFromStart, float elapsedSeconds)
        {
            FadeDuration = Mathf.Max(0f, fadeDuration);
            Speed = Mathf.Approximately(speed, 0f) ? 1f : speed;
            NormalizedStartTime = Mathf.Clamp01(normalizedStartTime);
            RestartFromStart = restartFromStart;
            ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        }

        /// <summary>
        /// Gets the fade duration used when the action starts.
        /// </summary>
        public float FadeDuration { get; }

        /// <summary>
        /// Gets the animation playback speed.
        /// </summary>
        public float Speed { get; }

        /// <summary>
        /// Gets the normalized animation start offset.
        /// </summary>
        public float NormalizedStartTime { get; }

        /// <summary>
        /// Gets whether the action should be restarted from the configured start offset.
        /// </summary>
        public bool RestartFromStart { get; }

        /// <summary>
        /// Gets elapsed timeline seconds that should be applied after starting.
        /// </summary>
        public float ElapsedSeconds { get; }

    }
}
