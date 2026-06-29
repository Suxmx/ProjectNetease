using System;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Defines how one semantic animation layer role maps to an Animancer layer and mask.
    /// </summary>
    [Serializable]
    public struct PartyAnimancerLayerRoleSettings
    {
        [SerializeField] private EPartyAnimancerLayerRole _role;
        [SerializeField, Min(0)] private int _layerIndex;
        [SerializeField] private AvatarMask _avatarMask;
        [SerializeField] private bool _isAdditive;
        [SerializeField, Min(0f)] private float _defaultFadeInDuration;
        [SerializeField, Min(0f)] private float _defaultFadeOutDuration;

        /// <summary>
        /// Creates a layer role mapping for an animation profile.
        /// </summary>
        public PartyAnimancerLayerRoleSettings(EPartyAnimancerLayerRole role, int layerIndex, AvatarMask avatarMask, bool isAdditive, float defaultFadeInDuration, float defaultFadeOutDuration)
        {
            _role = role;
            _layerIndex = Mathf.Max(0, layerIndex);
            _avatarMask = avatarMask;
            _isAdditive = isAdditive;
            _defaultFadeInDuration = Mathf.Max(0f, defaultFadeInDuration);
            _defaultFadeOutDuration = Mathf.Max(0f, defaultFadeOutDuration);
        }

        /// <summary>
        /// Gets the semantic role this settings entry configures.
        /// </summary>
        public EPartyAnimancerLayerRole Role => _role;

        /// <summary>
        /// Gets the Animancer layer index used for this role.
        /// </summary>
        public int LayerIndex => Mathf.Max(0, _layerIndex);

        /// <summary>
        /// Gets the AvatarMask applied to the Animancer layer.
        /// </summary>
        public AvatarMask AvatarMask => _avatarMask;

        /// <summary>
        /// Gets whether this role should use additive blending.
        /// </summary>
        public bool IsAdditive => _isAdditive;

        /// <summary>
        /// Gets the default fade-in duration for action requests that do not override it.
        /// </summary>
        public float DefaultFadeInDuration => Mathf.Max(0f, _defaultFadeInDuration);

        /// <summary>
        /// Gets the default fade-out duration for this action role.
        /// </summary>
        public float DefaultFadeOutDuration => Mathf.Max(0f, _defaultFadeOutDuration);
    }
}
