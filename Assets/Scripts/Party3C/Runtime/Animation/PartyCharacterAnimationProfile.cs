using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Configures base motion transitions and semantic action layers for Party3C Animancer playback.
    /// </summary>
    [CreateAssetMenu(menuName = "Party3C/Character Animation Profile")]
    public sealed class PartyCharacterAnimationProfile : ScriptableObject
    {
        [Header("Base Motion")]
        [SerializeField, Min(0)] private int _baseLayerIndex;
        [SerializeField, Min(0f)] private float _baseFadeDuration = 0.15f;
        [SerializeField] private ClipTransition _idle;
        [SerializeField] private LinearMixerTransition _locomotionMixer;
        [SerializeField] private ClipTransition _airborne;
        [SerializeField] private ClipTransition _dash;
        [SerializeField] private ClipTransition _knockback;
        [SerializeField] private ClipTransition _crouch;
        [SerializeField] private ClipTransition _wallClimb;

        [Header("Action Layers")]
        [SerializeField] private List<PartyAnimancerLayerRoleSettings> _layerRoles = new()
        {
            new PartyAnimancerLayerRoleSettings(EPartyAnimancerLayerRole.UpperBodyAction, 1, null, false, 0.05f, 0.1f),
            new PartyAnimancerLayerRoleSettings(EPartyAnimancerLayerRole.FullBodyAction, 2, null, false, 0.05f, 0.1f),
            new PartyAnimancerLayerRoleSettings(EPartyAnimancerLayerRole.AdditiveAction, 3, null, true, 0.05f, 0.1f),
            new PartyAnimancerLayerRoleSettings(EPartyAnimancerLayerRole.Reaction, 4, null, false, 0.03f, 0.08f)
        };

        /// <summary>
        /// Gets the base layer index used by locomotion.
        /// </summary>
        public int BaseLayerIndex => Mathf.Max(0, _baseLayerIndex);

        /// <summary>
        /// Gets the default fade duration for base motion transitions.
        /// </summary>
        public float BaseFadeDuration => Mathf.Max(0f, _baseFadeDuration);

        /// <summary>
        /// Gets the configured locomotion mixer.
        /// </summary>
        public LinearMixerTransition LocomotionMixer => _locomotionMixer;

        /// <summary>
        /// Finds the clip transition used by a non-mixer base motion state.
        /// </summary>
        public bool TryGetBaseTransition(EPartyCharacterBaseMotionState state, out ClipTransition transition)
        {
            transition = state switch
            {
                EPartyCharacterBaseMotionState.Idle => _idle,
                EPartyCharacterBaseMotionState.Airborne => _airborne,
                EPartyCharacterBaseMotionState.Dash => _dash,
                EPartyCharacterBaseMotionState.Knockback => _knockback,
                EPartyCharacterBaseMotionState.Crouch => _crouch,
                EPartyCharacterBaseMotionState.WallClimb => _wallClimb,
                _ => null
            };

            return transition != null && transition.IsValid;
        }

        /// <summary>
        /// Finds the Animancer layer settings for a semantic role.
        /// </summary>
        public bool TryGetLayerSettings(EPartyAnimancerLayerRole role, out PartyAnimancerLayerRoleSettings settings)
        {
            if (role == EPartyAnimancerLayerRole.BaseOverride)
            {
                settings = new PartyAnimancerLayerRoleSettings(role, BaseLayerIndex, null, false, BaseFadeDuration, BaseFadeDuration);
                return true;
            }

            for (int i = 0; i < _layerRoles.Count; i++)
            {
                PartyAnimancerLayerRoleSettings candidate = _layerRoles[i];
                if (candidate.Role != role)
                    continue;

                settings = candidate;
                return true;
            }

            settings = default;
            return false;
        }

    }
}
