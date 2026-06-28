using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Stores Animancer transitions addressable by skill animation keys.
    /// </summary>
    [CreateAssetMenu(menuName = "Party3C/Character Animation Set")]
    public sealed class PartyCharacterAnimationSet : ScriptableObject
    {
        [Serializable]
        private sealed class AnimationEntry
        {
            public string Key;
            public ClipTransition Transition;
        }

        [SerializeField] private List<AnimationEntry> _entries = new();

        /// <summary>
        /// Gets the number of animation entries configured in this set.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Reads one animation entry by index without exposing the mutable backing list.
        /// </summary>
        public bool TryGetEntry(int index, out string key, out ClipTransition transition)
        {
            key = string.Empty;
            transition = null;
            if (index < 0 || index >= _entries.Count)
                return false;

            AnimationEntry entry = _entries[index];
            if (entry == null)
                return false;

            key = entry.Key;
            transition = entry.Transition;
            return !string.IsNullOrWhiteSpace(key) && transition != null && transition.IsValid;
        }

        /// <summary>
        /// Finds the Animancer transition registered for a timeline animation key.
        /// </summary>
        public bool TryGetTransition(string key, out ClipTransition transition)
        {
            transition = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                AnimationEntry entry = _entries[i];
                if (entry == null || !string.Equals(entry.Key, key, StringComparison.Ordinal))
                    continue;

                transition = entry.Transition;
                return transition != null && transition.IsValid;
            }

            return false;
        }

        /// <summary>
        /// Finds the AnimationClip registered for a timeline animation key.
        /// </summary>
        public bool TryGetAnimationClip(string key, out AnimationClip clip)
        {
            clip = null;
            if (!TryGetTransition(key, out ClipTransition transition))
                return false;

            clip = transition.Clip;
            return clip != null;
        }

        /// <summary>
        /// Appends all valid animation keys in this set to the provided list.
        /// </summary>
        public void CollectKeys(List<string> keys)
        {
            if (keys == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                AnimationEntry entry = _entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                    keys.Add(entry.Key);
            }
        }
    }
}
