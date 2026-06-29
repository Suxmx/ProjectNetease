using Animancer;
using Hoshino;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Plays base motion and action-channel animation requests through an Animancer component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyAnimancerPresenter : MonoBehaviour
    {
        [SerializeField] private AnimancerComponent _animancer;
        [SerializeField] private PartyCharacterAnimationSet _animationSet;
        [SerializeField] private PartyCharacterAnimationProfile _animationProfile;

        private EPartyCharacterBaseMotionState _currentBaseState;
        private AnimancerState _currentBaseAnimancerState;
        private bool _hasCurrentBaseState;

        /// <summary>
        /// Gets the directly assigned animation set used when a skill node does not specify an Addressable set.
        /// </summary>
        public PartyCharacterAnimationSet AnimationSet => _animationSet;

        /// <summary>
        /// Gets the profile used to resolve semantic animation layer roles.
        /// </summary>
        public PartyCharacterAnimationProfile AnimationProfile => _animationProfile;

        /// <summary>
        /// Assigns the Animancer runtime references used by layered animation playback.
        /// </summary>
        public void Configure(AnimancerComponent animancer, PartyCharacterAnimationSet animationSet, PartyCharacterAnimationProfile animationProfile)
        {
            bool shouldResetBaseState = _animancer != animancer || _animationProfile != animationProfile;
            _animancer = animancer;
            _animationSet = animationSet;
            _animationProfile = animationProfile;

            if (shouldResetBaseState)
                ResetBaseStateCache();
        }

        /// <summary>
        /// Assigns the profile used by future layer-role animation requests.
        /// </summary>
        public void ConfigureAnimationProfile(PartyCharacterAnimationProfile animationProfile)
        {
            if (_animationProfile == animationProfile)
                return;

            _animationProfile = animationProfile;
            ResetBaseStateCache();
        }

        /// <summary>
        /// Plays or updates the base locomotion layer from a replicated animation snapshot.
        /// </summary>
        public bool PlayBaseMotion(in PartyCharacterAnimationSnapshot snapshot)
        {
            ResolveReferences();
            if (_animancer == null || _animationProfile == null)
                return false;

            AnimancerLayer layer = _animancer.Layers[_animationProfile.BaseLayerIndex];
            if (layer.IsAdditive)
                layer.IsAdditive = false;

            if (layer.Mask != null)
                layer.Mask = null;

            if (snapshot.BaseState == EPartyCharacterBaseMotionState.Locomotion)
                return PlayLocomotion(snapshot, layer);

            if (!_animationProfile.TryGetBaseTransition(snapshot.BaseState, out ClipTransition transition))
                return false;

            if (ShouldReplayBaseState(snapshot.BaseState, layer))
            {
                AnimancerState state = layer.Play(transition, _animationProfile.BaseFadeDuration);
                SetCurrentBaseState(snapshot.BaseState, state);
            }

            return true;
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

            float speed = Mathf.Approximately(data.Speed, 0f) ? 1f : data.Speed;
            float elapsedSeconds = SkillTickUtility.TicksToSeconds(Mathf.Max(0, skillLocalTick - nodeStartTick), sourceTickRate);
            PartyAnimancerPlayOptions options = new(data.FadeDuration, speed, data.NormalizedStartTime, data.RestartFromStart, elapsedSeconds);
            return PlayAction(data.LayerRole, sourceTransition, options);
        }

        /// <summary>
        /// Plays an action animation on the semantic layer role resolved by the animation profile.
        /// </summary>
        public bool PlayAction(EPartyAnimancerLayerRole role, ClipTransition sourceTransition, in PartyAnimancerPlayOptions options)
        {
            ResolveReferences();
            if (_animancer == null || _animationProfile == null)
                return false;

            if (!_animationProfile.TryGetLayerSettings(role, out PartyAnimancerLayerRoleSettings settings))
                return false;

            if (!ValidateActionLayerSettings(role, settings))
                return false;

            if (sourceTransition == null || !sourceTransition.IsValid || sourceTransition.Clip == null)
                return false;

            AnimancerLayer layer = _animancer.Layers[settings.LayerIndex];
            ApplyLayerSettings(layer, settings);

            float fadeDuration = options.FadeDuration > 0f ? options.FadeDuration : settings.DefaultFadeInDuration;
            ClipTransition runtimeTransition = new()
            {
                Clip = sourceTransition.Clip,
                FadeDuration = fadeDuration,
                Speed = options.Speed,
                NormalizedStartTime = options.RestartFromStart ? options.NormalizedStartTime : float.NaN
            };

            if (settings.LayerIndex > 0)
                layer.StartFade(1f, fadeDuration);

            AnimancerState state = layer.Play(runtimeTransition);
            if (options.RestartFromStart)
            {
                float startTime = options.NormalizedStartTime * sourceTransition.Clip.length;
                float targetTime = startTime + options.ElapsedSeconds * Mathf.Abs(options.Speed);
                state.Time = sourceTransition.Clip.isLooping ? targetTime : Mathf.Min(targetTime, sourceTransition.Clip.length);
            }

            if (settings.LayerIndex == _animationProfile.BaseLayerIndex)
                ResetBaseStateCache();

            return true;
        }

        /// <summary>
        /// Fades out one semantic action layer without changing the base locomotion layer.
        /// </summary>
        public bool FadeOutAction(EPartyAnimancerLayerRole role, float fadeDuration)
        {
            ResolveReferences();
            if (_animancer == null || _animationProfile == null || role == EPartyAnimancerLayerRole.BaseOverride)
                return false;

            if (!_animationProfile.TryGetLayerSettings(role, out PartyAnimancerLayerRoleSettings settings))
                return false;

            float duration = fadeDuration > 0f ? fadeDuration : settings.DefaultFadeOutDuration;
            _animancer.Layers[settings.LayerIndex].StartFade(0f, duration);
            return true;
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
        /// Plays and updates the configured locomotion mixer on the base layer.
        /// </summary>
        private bool PlayLocomotion(in PartyCharacterAnimationSnapshot snapshot, AnimancerLayer layer)
        {
            if (_animationProfile.LocomotionMixer == null || !_animationProfile.LocomotionMixer.IsValid)
                return false;

            if (ShouldReplayBaseState(EPartyCharacterBaseMotionState.Locomotion, layer) || _currentBaseAnimancerState is not LinearMixerState)
            {
                AnimancerState state = layer.Play(_animationProfile.LocomotionMixer, _animationProfile.BaseFadeDuration);
                SetCurrentBaseState(EPartyCharacterBaseMotionState.Locomotion, state);
            }

            if (_currentBaseAnimancerState is LinearMixerState mixerState)
                mixerState.Parameter = snapshot.NormalizedSpeed;

            return true;
        }

        /// <summary>
        /// Returns whether the base layer no longer owns the currently playing Animancer state.
        /// </summary>
        private bool ShouldReplayBaseState(EPartyCharacterBaseMotionState baseState, AnimancerLayer layer)
        {
            return !_hasCurrentBaseState
                || _currentBaseState != baseState
                || layer.CurrentState == null
                || layer.CurrentState != _currentBaseAnimancerState;
        }

        /// <summary>
        /// Stores the current base motion state and the Animancer state that owns the base layer.
        /// </summary>
        private void SetCurrentBaseState(EPartyCharacterBaseMotionState baseState, AnimancerState state)
        {
            _currentBaseState = baseState;
            _currentBaseAnimancerState = state;
            _hasCurrentBaseState = state != null;
        }

        /// <summary>
        /// Clears cached base layer ownership so the next snapshot replays base motion.
        /// </summary>
        private void ResetBaseStateCache()
        {
            _currentBaseState = default;
            _currentBaseAnimancerState = null;
            _hasCurrentBaseState = false;
        }

        /// <summary>
        /// Rejects action layer configurations that would hide incorrect blend setup.
        /// </summary>
        private bool ValidateActionLayerSettings(EPartyAnimancerLayerRole role, PartyAnimancerLayerRoleSettings settings)
        {
            if (role != EPartyAnimancerLayerRole.UpperBodyAction)
                return true;

            if (settings.LayerIndex == _animationProfile.BaseLayerIndex)
            {
                Debug.LogError(
                    $"{nameof(EPartyAnimancerLayerRole.UpperBodyAction)} in profile '{_animationProfile.name}' must not use the base layer {_animationProfile.BaseLayerIndex}.",
                    this);
                return false;
            }

            if (settings.AvatarMask == null)
            {
                Debug.LogError(
                    $"{nameof(EPartyAnimancerLayerRole.UpperBodyAction)} in profile '{_animationProfile.name}' requires an upper-body AvatarMask.",
                    this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Applies profile layer settings before playing an action transition.
        /// </summary>
        private static void ApplyLayerSettings(AnimancerLayer layer, PartyAnimancerLayerRoleSettings settings)
        {
            if (layer.IsAdditive != settings.IsAdditive)
                layer.IsAdditive = settings.IsAdditive;

            if (layer.Mask != settings.AvatarMask)
                layer.Mask = settings.AvatarMask;
        }

        /// <summary>
        /// Resolves the animation set either from Addressables cache or the directly assigned reference.
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
