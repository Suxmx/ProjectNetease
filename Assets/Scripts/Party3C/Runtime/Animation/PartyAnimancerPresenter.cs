using Animancer;
using Hoshino;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Plays skill timeline animation nodes through an Animancer component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyAnimancerPresenter : MonoBehaviour
    {
        [SerializeField] private AnimancerComponent _animancer;
        [SerializeField] private PartyCharacterAnimationSet _animationSet;

        /// <summary>
        /// Gets the directly assigned fallback animation set.
        /// </summary>
        public PartyCharacterAnimationSet AnimationSet => _animationSet;

        /// <summary>
        /// Assigns the Animancer runtime references used by skill playback.
        /// </summary>
        public void Configure(AnimancerComponent animancer, PartyCharacterAnimationSet animationSet)
        {
            _animancer = animancer;
            _animationSet = animationSet;
        }

        /// <summary>
        /// Plays one animation node from the skill timeline and aligns it to the current skill tick.
        /// </summary>
        public bool PlaySkillAnimation(in PlayAnimancerNodeData data, int skillLocalTick, int nodeStartTick, int sourceTickRate)
        {
            ResolveReferences();
            if (_animancer == null)
                return false;

            if (!TryResolveAnimationSet(data.AnimationSetFileName, out PartyCharacterAnimationSet animationSet))
                return false;

            if (!animationSet.TryGetTransition(data.AnimationKey, out ClipTransition sourceTransition))
                return false;

            AnimationClip clip = sourceTransition.Clip;
            if (clip == null)
                return false;

            int layerIndex = Mathf.Max(0, data.LayerIndex);
            float speed = Mathf.Approximately(data.Speed, 0f) ? 1f : data.Speed;
            float normalizedStart = Mathf.Clamp01(data.NormalizedStartTime);
            float elapsedSeconds = SkillTickUtility.TicksToSeconds(Mathf.Max(0, skillLocalTick - nodeStartTick), sourceTickRate);
            float startTime = data.RestartFromStart ? normalizedStart * clip.length : 0f;

            ClipTransition runtimeTransition = new()
            {
                Clip = clip,
                FadeDuration = Mathf.Max(0f, data.FadeDuration),
                Speed = speed,
                NormalizedStartTime = data.RestartFromStart ? normalizedStart : float.NaN
            };

            AnimancerLayer layer = _animancer.Layers[layerIndex];
            if (layerIndex > 0)
                layer.StartFade(1f, runtimeTransition.FadeDuration);

            AnimancerState state = layer.Play(runtimeTransition);
            if (data.RestartFromStart)
            {
                float targetTime = startTime + elapsedSeconds * Mathf.Abs(speed);
                state.Time = clip.isLooping ? targetTime : Mathf.Min(targetTime, clip.length);
            }

            return true;
        }

        /// <summary>
        /// Fades a temporary skill layer out after an action clip ends.
        /// </summary>
        public void FadeOutLayer(int layerIndex, float fadeDuration)
        {
            ResolveReferences();
            if (_animancer == null)
                return;

            _animancer.Layers[Mathf.Max(0, layerIndex)].StartFade(0f, Mathf.Max(0f, fadeDuration));
        }

        /// <summary>
        /// Finds the Animancer component on this object when no reference has been assigned.
        /// </summary>
        private void ResolveReferences()
        {
            if (_animancer == null)
                _animancer = GetComponentInChildren<AnimancerComponent>();
        }

        /// <summary>
        /// Resolves the animation set either from Addressables cache or the local fallback reference.
        /// </summary>
        private bool TryResolveAnimationSet(string animationSetFileName, out PartyCharacterAnimationSet animationSet)
        {
            if (!string.IsNullOrWhiteSpace(animationSetFileName))
                return PartyAnimationSetAddressableCache.TryGet(animationSetFileName, out animationSet);

            animationSet = _animationSet;
            return animationSet != null;
        }
    }
}
