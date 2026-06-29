using System;
using System.Collections.Generic;
using Hoshino;
using UnityEngine;

namespace Party3C
{
    /// <summary>
    /// Loads compiled MemoFramework skill binaries and resolves them by runtime skill id.
    /// </summary>
    [CreateAssetMenu(menuName = "Party3C/Skill Library")]
    public sealed class PartySkillLibrary : ScriptableObject
    {
        [SerializeField] private List<TextAsset> _compiledSkills = new();

        [NonSerialized] private readonly Dictionary<int, SkillDefinition> _skillsById = new();
        [NonSerialized] private bool _loaded;

        /// <summary>
        /// Finds a compiled skill by its runtime id.
        /// </summary>
        public bool TryGetSkill(int skillId, out SkillDefinition skill)
        {
            EnsureLoaded();
            return _skillsById.TryGetValue(skillId, out skill);
        }

        /// <summary>
        /// Starts Addressables preloading for all animation sets referenced by configured skills.
        /// </summary>
        public void PreloadAnimationSets()
        {
            HashSet<string> animationSetFileNames = new(StringComparer.Ordinal);
            CollectAnimationSetFileNames(animationSetFileNames);
            PartyAnimationSetAddressableCache.Preload(animationSetFileNames);
        }

        /// <summary>
        /// Collects all Addressable animation set file names referenced by configured skills.
        /// </summary>
        public void CollectAnimationSetFileNames(HashSet<string> animationSetFileNames)
        {
            if (animationSetFileNames == null)
                return;

            EnsureLoaded();
            foreach (SkillDefinition skill in _skillsById.Values)
                CollectAnimationSetFileNames(skill, animationSetFileNames);
        }

        /// <summary>
        /// Returns true when every Addressable animation set used by the skill id is loaded.
        /// </summary>
        public bool AreAnimationSetsLoaded(int skillId)
        {
            EnsureLoaded();
            return _skillsById.TryGetValue(skillId, out SkillDefinition skill) && AreAnimationSetsLoaded(skill);
        }

        /// <summary>
        /// Returns true when every Addressable animation set used by the skill is loaded.
        /// </summary>
        public bool AreAnimationSetsLoaded(SkillDefinition skill)
        {
            if (skill == null)
                return false;

            SkillRuntimeNode[] nodes = skill.Nodes;
            if (nodes == null)
                return true;

            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (node.ClipId != SkillGeneratedIds.PlayAnimancerClip)
                    continue;

                if (!SkillGeneratedSerializationServices.Runtime.TryRead(skill, node, out PlayAnimancerNodeData data))
                    return false;

                if (!string.IsNullOrWhiteSpace(data.AnimationSetFileName) && !PartyAnimationSetAddressableCache.IsLoaded(data.AnimationSetFileName))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Loads all configured compiled skill binaries once.
        /// </summary>
        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            _skillsById.Clear();
            for (int i = 0; i < _compiledSkills.Count; i++)
            {
                TextAsset asset = _compiledSkills[i];
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    continue;

                SkillDefinition skill = SkillDefinition.FromBytes(asset.bytes);
                if (skill != null)
                    _skillsById[skill.SkillId] = skill;
            }
        }

        /// <summary>
        /// Adds the Addressable animation set names referenced by one compiled skill.
        /// </summary>
        private static void CollectAnimationSetFileNames(SkillDefinition skill, HashSet<string> animationSetFileNames)
        {
            if (skill == null)
                return;

            SkillRuntimeNode[] nodes = skill.Nodes;
            if (nodes == null)
                return;

            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (node.ClipId != SkillGeneratedIds.PlayAnimancerClip)
                    continue;

                if (!SkillGeneratedSerializationServices.Runtime.TryRead(skill, node, out PlayAnimancerNodeData data))
                    continue;

                if (!string.IsNullOrWhiteSpace(data.AnimationSetFileName))
                    animationSetFileNames.Add(data.AnimationSetFileName);
            }
        }
    }
}
