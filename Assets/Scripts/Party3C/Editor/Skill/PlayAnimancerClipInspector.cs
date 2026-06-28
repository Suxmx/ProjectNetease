using System;
using System.Collections.Generic;
using Slate;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Party3C.Editor
{
    /// <summary>
    /// Provides Addressable animation set and key selection UI for PlayAnimancerClip timeline nodes.
    /// </summary>
    [CustomEditor(typeof(PlayAnimancerClip))]
    public sealed class PlayAnimancerClipInspector : ActionClipInspector<PlayAnimancerClip>
    {
        private const string NoneOption = "<None>";

        /// <summary>
        /// Draws the custom PlayAnimancerClip inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            ShowCommonInspector(false);

            PartyCharacterAnimationSet animationSet = FindAnimationSetByFileName(action.AnimationSetFileName, out int matchingSetCount);
            DrawAnimationSetField(animationSet);
            DrawStoredAddressField();

            if (matchingSetCount > 1)
                EditorGUILayout.HelpBox($"Multiple PartyCharacterAnimationSet assets named '{action.AnimationSetFileName}' were found. The first match is used for editor preview.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(action.AnimationSetFileName) && animationSet == null)
                EditorGUILayout.HelpBox($"PartyCharacterAnimationSet asset '{action.AnimationSetFileName}' was not found in the project.", MessageType.Error);

            DrawAddressableValidation(animationSet);
            DrawAnimationKeyField(animationSet);
            DrawPlaybackFields();
            ShowAnimatableParameters();

            if (GUI.changed)
                action.Validate();
        }

        /// <summary>
        /// Draws the PartyCharacterAnimationSet object selector and writes its file name.
        /// </summary>
        private void DrawAnimationSetField(PartyCharacterAnimationSet currentSet)
        {
            EditorGUI.BeginChangeCheck();
            PartyCharacterAnimationSet selectedSet = (PartyCharacterAnimationSet)EditorGUILayout.ObjectField("Animation Set", currentSet, typeof(PartyCharacterAnimationSet), false);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(action, "Change Animancer Animation Set");
            action.AnimationSetFileName = selectedSet != null ? selectedSet.name : string.Empty;
            if (selectedSet == null || !ContainsKey(selectedSet, action.AnimationKey))
                action.AnimationKey = string.Empty;

            MatchLengthToSelectedAnimation(selectedSet);
            ApplyClipChanges();
        }

        /// <summary>
        /// Shows the stored Addressables address derived from the selected animation set file name.
        /// </summary>
        private void DrawStoredAddressField()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Address/File Name", action.AnimationSetFileName ?? string.Empty);
        }

        /// <summary>
        /// Draws Addressables configuration errors for the selected animation set.
        /// </summary>
        private static void DrawAddressableValidation(PartyCharacterAnimationSet animationSet)
        {
            if (animationSet == null || !TryGetAddressableError(animationSet, out string error))
                return;

            EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        /// <summary>
        /// Draws the animation key popup for the selected animation set.
        /// </summary>
        private void DrawAnimationKeyField(PartyCharacterAnimationSet animationSet)
        {
            if (animationSet == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Animation Key", action.AnimationKey ?? string.Empty);
                return;
            }

            List<string> keys = new();
            animationSet.CollectKeys(keys);
            if (keys.Count == 0)
            {
                EditorGUILayout.HelpBox("The selected PartyCharacterAnimationSet has no valid animation keys.", MessageType.Warning);
                return;
            }

            string[] options = BuildKeyOptions(keys, action.AnimationKey, out int selectedIndex, out bool selectedKeyMissing);
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Animation Key", selectedIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(action, "Change Animancer Animation Key");
                action.AnimationKey = ResolveSelectedKey(options[nextIndex]);
                MatchLengthToSelectedAnimation(animationSet);
                ApplyClipChanges();
            }

            if (selectedKeyMissing)
                EditorGUILayout.HelpBox($"Animation key '{action.AnimationKey}' was not found in the selected animation set.", MessageType.Error);
        }

        /// <summary>
        /// Draws playback parameters that are serialized into PlayAnimancerNodeData.
        /// </summary>
        private void DrawPlaybackFields()
        {
            EditorGUI.BeginChangeCheck();
            int layerIndex = Mathf.Max(0, EditorGUILayout.IntField("Layer Index", action.LayerIndex));
            float fadeDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Fade Duration", action.FadeDuration));
            float speed = EditorGUILayout.FloatField("Speed", action.Speed);
            float normalizedStartTime = EditorGUILayout.Slider("Normalized Start Time", action.NormalizedStartTime, 0f, 1f);
            bool restartFromStart = EditorGUILayout.Toggle("Restart From Start", action.RestartFromStart);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(action, "Edit Animancer Playback");
            action.LayerIndex = layerIndex;
            action.FadeDuration = fadeDuration;
            action.Speed = speed;
            action.NormalizedStartTime = normalizedStartTime;
            action.RestartFromStart = restartFromStart;
            ApplyClipChanges();
        }

        /// <summary>
        /// Applies validation, dirty state, and editor resampling after a direct inspector edit.
        /// </summary>
        private void ApplyClipChanges()
        {
            action.Validate();
            EditorUtility.SetDirty(action);
            action.root?.ReSample();
        }

        /// <summary>
        /// Updates the timeline clip length from the selected animation key.
        /// </summary>
        private void MatchLengthToSelectedAnimation(PartyCharacterAnimationSet animationSet)
        {
            if (animationSet == null || string.IsNullOrWhiteSpace(action.AnimationKey))
                return;

            if (!animationSet.TryGetAnimationClip(action.AnimationKey, out AnimationClip clip) || clip.length <= 0f)
                return;

            float startTime = Mathf.Clamp01(action.NormalizedStartTime) * clip.length;
            float remainingLength = Mathf.Max(0f, clip.length - startTime);
            float speed = Mathf.Approximately(action.Speed, 0f) ? 1f : Mathf.Abs(action.Speed);
            action.length = remainingLength / Mathf.Max(0.0001f, speed);
        }

        /// <summary>
        /// Builds the displayed popup options and selected index for animation keys.
        /// </summary>
        private static string[] BuildKeyOptions(List<string> keys, string currentKey, out int selectedIndex, out bool selectedKeyMissing)
        {
            selectedKeyMissing = !string.IsNullOrWhiteSpace(currentKey) && !keys.Contains(currentKey);
            List<string> options = new() { NoneOption };
            if (selectedKeyMissing)
                options.Add($"<Missing: {currentKey}>");
            options.AddRange(keys);

            if (string.IsNullOrWhiteSpace(currentKey))
            {
                selectedIndex = 0;
            }
            else if (selectedKeyMissing)
            {
                selectedIndex = 1;
            }
            else
            {
                selectedIndex = keys.IndexOf(currentKey) + 1;
            }

            return options.ToArray();
        }

        /// <summary>
        /// Converts a popup label back to the serialized animation key value.
        /// </summary>
        private static string ResolveSelectedKey(string option)
        {
            return string.Equals(option, NoneOption, StringComparison.Ordinal) || option.StartsWith("<Missing:", StringComparison.Ordinal) ? string.Empty : option;
        }

        /// <summary>
        /// Returns true when the animation set contains the requested key.
        /// </summary>
        private static bool ContainsKey(PartyCharacterAnimationSet animationSet, string key)
        {
            return animationSet != null && !string.IsNullOrWhiteSpace(key) && animationSet.TryGetAnimationClip(key, out _);
        }

        /// <summary>
        /// Finds a PartyCharacterAnimationSet asset by its file name without extension.
        /// </summary>
        private static PartyCharacterAnimationSet FindAnimationSetByFileName(string fileName, out int matchCount)
        {
            matchCount = 0;
            PartyCharacterAnimationSet firstMatch = null;
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(PartyCharacterAnimationSet)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PartyCharacterAnimationSet candidate = AssetDatabase.LoadAssetAtPath<PartyCharacterAnimationSet>(path);
                if (candidate == null || !string.Equals(candidate.name, fileName, StringComparison.Ordinal))
                    continue;

                matchCount++;
                if (firstMatch == null)
                    firstMatch = candidate;
            }

            return firstMatch;
        }

        /// <summary>
        /// Checks whether the selected animation set is configured with the expected Addressables address.
        /// </summary>
        private static bool TryGetAddressableError(PartyCharacterAnimationSet animationSet, out string error)
        {
            error = string.Empty;
            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                error = "Addressables settings do not exist. Create Addressables settings and mark this animation set Addressable.";
                return true;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                error = "Addressables settings could not be loaded.";
                return true;
            }

            string path = AssetDatabase.GetAssetPath(animationSet);
            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid, false);
            if (entry == null)
            {
                error = $"'{animationSet.name}' is not marked Addressable.";
                return true;
            }

            if (!string.Equals(entry.address, animationSet.name, StringComparison.Ordinal))
            {
                error = $"Addressables address must be '{animationSet.name}', current address is '{entry.address}'.";
                return true;
            }

            return false;
        }
    }
}
