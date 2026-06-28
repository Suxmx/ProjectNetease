using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Party3C
{
    /// <summary>
    /// Caches Addressable PartyCharacterAnimationSet assets so skill playback can resolve animations without blocking.
    /// </summary>
    public static class PartyAnimationSetAddressableCache
    {
        private static readonly Dictionary<string, PartyCharacterAnimationSet> LoadedSets = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AsyncOperationHandle<PartyCharacterAnimationSet>> LoadedHandles = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AsyncOperationHandle<PartyCharacterAnimationSet>> LoadingHandles = new(StringComparer.Ordinal);

        /// <summary>
        /// Starts loading every non-empty animation set address that is not already cached.
        /// </summary>
        public static void Preload(IEnumerable<string> animationSetFileNames)
        {
            if (animationSetFileNames == null)
                return;

            foreach (string animationSetFileName in animationSetFileNames)
                Preload(animationSetFileName);
        }

        /// <summary>
        /// Starts loading one animation set address when it is not already cached or loading.
        /// </summary>
        public static void Preload(string animationSetFileName)
        {
            string address = NormalizeAddress(animationSetFileName);
            if (string.IsNullOrEmpty(address) || LoadedSets.ContainsKey(address) || LoadingHandles.ContainsKey(address))
                return;

            AsyncOperationHandle<PartyCharacterAnimationSet> handle = Addressables.LoadAssetAsync<PartyCharacterAnimationSet>(address);
            LoadingHandles[address] = handle;
            handle.Completed += completedHandle => OnLoadCompleted(address, completedHandle);
        }

        /// <summary>
        /// Returns true when the requested animation set has finished loading.
        /// </summary>
        public static bool IsLoaded(string animationSetFileName)
        {
            string address = NormalizeAddress(animationSetFileName);
            return string.IsNullOrEmpty(address) || LoadedSets.ContainsKey(address);
        }

        /// <summary>
        /// Returns true when every requested animation set has finished loading.
        /// </summary>
        public static bool AreLoaded(IEnumerable<string> animationSetFileNames)
        {
            if (animationSetFileNames == null)
                return true;

            foreach (string animationSetFileName in animationSetFileNames)
            {
                if (!IsLoaded(animationSetFileName))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Reads a loaded animation set from the cache without starting a new load.
        /// </summary>
        public static bool TryGet(string animationSetFileName, out PartyCharacterAnimationSet animationSet)
        {
            string address = NormalizeAddress(animationSetFileName);
            if (string.IsNullOrEmpty(address))
            {
                animationSet = null;
                return false;
            }

            return LoadedSets.TryGetValue(address, out animationSet) && animationSet != null;
        }

        /// <summary>
        /// Releases all Addressables handles owned by this cache.
        /// </summary>
        public static void ReleaseAll()
        {
            foreach (AsyncOperationHandle<PartyCharacterAnimationSet> handle in LoadingHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            foreach (AsyncOperationHandle<PartyCharacterAnimationSet> handle in LoadedHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            LoadingHandles.Clear();
            LoadedHandles.Clear();
            LoadedSets.Clear();
        }

        /// <summary>
        /// Stores a completed Addressables result or releases the failed handle.
        /// </summary>
        private static void OnLoadCompleted(string address, AsyncOperationHandle<PartyCharacterAnimationSet> handle)
        {
            LoadingHandles.Remove(address);
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                LoadedSets[address] = handle.Result;
                LoadedHandles[address] = handle;
                return;
            }

            if (handle.IsValid())
                Addressables.Release(handle);

            Debug.LogWarning($"PartyCharacterAnimationSet Addressable load failed. Address: {address}");
        }

        /// <summary>
        /// Normalizes the Addressables address used by the animation set file-name protocol.
        /// </summary>
        private static string NormalizeAddress(string animationSetFileName)
        {
            return string.IsNullOrWhiteSpace(animationSetFileName) ? string.Empty : animationSetFileName.Trim();
        }
    }
}
