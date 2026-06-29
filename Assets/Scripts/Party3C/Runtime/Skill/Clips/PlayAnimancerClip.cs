using Hoshino;
using Slate;
using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace Party3C
{
    /// <summary>
    /// Skill timeline clip that plays an Animancer animation key through the party animation presenter.
    /// </summary>
    [SkillClipType(1009u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class PlayAnimancerClip : ActionClip, ISubClipContainable
    {
        [SerializeField, HideInInspector] private float _length = 1f;

        [SkillCustomData] public string AnimationSetFileName;
        [SkillCustomData] public string AnimationKey;
        [SkillCustomData] public EPartyAnimancerLayerRole LayerRole = EPartyAnimancerLayerRole.UpperBodyAction;
        [SkillCustomData] public float FadeDuration = 0.1f;
        [SkillCustomData] public float Speed = 1f;
        [SkillCustomData] public float NormalizedStartTime;
        [SkillCustomData] public bool RestartFromStart = true;

#if UNITY_EDITOR
        private TransformSnapshot _previewSnapshot;
        private bool _hasPreviewSnapshot;
#endif

        /// <summary>
        /// Gets or sets the sub-clip offset in animation seconds for Slate trimming tools.
        /// </summary>
        float ISubClipContainable.subClipOffset
        {
            get => TryGetAnimationClip(out AnimationClip clip) ? Mathf.Clamp01(NormalizedStartTime) * clip.length : 0f;
            set
            {
                if (TryGetAnimationClip(out AnimationClip clip) && clip.length > 0f)
                    NormalizedStartTime = Mathf.Clamp01(value / clip.length);
                else
                    NormalizedStartTime = 0f;
            }
        }

        /// <summary>
        /// Gets the absolute animation playback speed for Slate sub-clip length tools.
        /// </summary>
        float ISubClipContainable.subClipSpeed => ResolveSubClipSpeed();

        /// <summary>
        /// Gets the selected animation clip length for Slate sub-clip length tools.
        /// </summary>
        float ISubClipContainable.subClipLength => TryGetAnimationClip(out AnimationClip clip) ? clip.length : 0f;

        public override float length
        {
            get => _length;
            set => _length = Mathf.Max(0.0167f, value);
        }

        public override bool isValid => !string.IsNullOrWhiteSpace(AnimationKey);

        public override string info => !string.IsNullOrWhiteSpace(AnimationKey) ? $"{LayerRole}: {AnimationKey}" : base.info;

        /// <summary>
        /// Clamps timeline values after Slate validates the clip.
        /// </summary>
        protected override void OnAfterValidate()
        {
            _length = Mathf.Max(0.0167f, _length);
            FadeDuration = Mathf.Max(0f, FadeDuration);
            NormalizedStartTime = Mathf.Clamp01(NormalizedStartTime);
        }

        /// <summary>
        /// Resolves the selected animation clip for Slate sub-clip tooling.
        /// </summary>
        private bool TryGetAnimationClip(out AnimationClip clip)
        {
#if UNITY_EDITOR
            return TryResolveEditorAnimationClip(out clip);
#else
            clip = null;
            return false;
#endif
        }

        /// <summary>
        /// Returns the absolute playback speed used by Slate sub-clip length tools.
        /// </summary>
        private float ResolveSubClipSpeed()
        {
            float speed = Mathf.Approximately(Speed, 0f) ? 1f : Mathf.Abs(Speed);
            return Mathf.Max(0.0001f, speed);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Stores the current actor pose before editor preview sampling starts.
        /// </summary>
        protected override void OnEnter()
        {
            if (Application.isPlaying)
                return;

            CachePreviewSnapshot();
            SamplePreviewAtLocalTime(0f);
        }

        /// <summary>
        /// Samples the selected animation at the local timeline time while previewing in the editor.
        /// </summary>
        protected override void OnUpdate(float time, float previousTime)
        {
            if (Application.isPlaying)
                return;

            SamplePreviewAtLocalTime(time);
        }

        /// <summary>
        /// Restores the actor pose when the preview clip exits forward.
        /// </summary>
        protected override void OnExit()
        {
            if (Application.isPlaying)
                return;

            RestorePreviewSnapshot();
        }

        /// <summary>
        /// Restores the actor pose when the preview clip is left while scrubbing backward.
        /// </summary>
        protected override void OnReverse()
        {
            if (Application.isPlaying)
                return;

            RestorePreviewSnapshot();
        }

        /// <summary>
        /// Keeps the editor preview pose in sync when the skill editor scrubs the raw cutscene time.
        /// </summary>
        protected override void OnRawUpdate()
        {
            if (Application.isPlaying || root == null)
                return;

            float localTime = root.currentTime - startTime;
            if (localTime < 0f || localTime > length)
            {
                RestorePreviewSnapshot();
                return;
            }

            CachePreviewSnapshot();
            SamplePreviewAtLocalTime(localTime);
        }

        /// <summary>
        /// Draws Slate loop markers for animation lengths longer or shorter than this timeline clip.
        /// </summary>
        protected override void OnClipGUI(Rect rect)
        {
            if (!TryResolveEditorAnimationClip(out AnimationClip clip))
                return;

            EditorTools.DrawLoopedLines(rect, clip.length / ResolveSubClipSpeed(), length, Mathf.Clamp01(NormalizedStartTime) * clip.length);
        }

        /// <summary>
        /// Finds the selected animation clip from the configured editor asset.
        /// </summary>
        private bool TryResolveEditorAnimationClip(out AnimationClip clip)
        {
            clip = null;
            if (!TryResolveEditorAnimationSet(out PartyCharacterAnimationSet animationSet))
                return false;

            return animationSet.TryGetAnimationClip(AnimationKey, out clip);
        }

        /// <summary>
        /// Finds the configured animation set in the editor by Addressables file-name protocol.
        /// 结果由 <see cref="s_AnimationSetCache"/> 静态缓存，避免每帧 OnGUI 重复全量扫描 AssetDatabase。
        /// </summary>
        private bool TryResolveEditorAnimationSet(out PartyCharacterAnimationSet animationSet)
        {
            animationSet = null;
            if (string.IsNullOrWhiteSpace(AnimationSetFileName))
                return false;

            EnsureAnimationSetCache();
            return s_AnimationSetCache.TryGetValue(AnimationSetFileName, out animationSet) && animationSet != null;
        }

        /// <summary>按 name 缓存项目中所有 <see cref="PartyCharacterAnimationSet"/> 资产，避免每帧 AssetDatabase 全量扫描。</summary>
        private static Dictionary<string, PartyCharacterAnimationSet> s_AnimationSetCache;
        private static bool s_AnimationSetCacheInitialized;

        /// <summary>首次访问时建立缓存，并订阅 projectChanged 自动失效。</summary>
        private static void EnsureAnimationSetCache()
        {
            if (s_AnimationSetCacheInitialized)
                return;

            s_AnimationSetCacheInitialized = true;
            s_AnimationSetCache = new Dictionary<string, PartyCharacterAnimationSet>(System.StringComparer.Ordinal);

            // --- 订阅资源变更，自动失效缓存 ---
            EditorApplication.projectChanged -= InvalidateAnimationSetCache;
            EditorApplication.projectChanged += InvalidateAnimationSetCache;

            RebuildAnimationSetCache();
        }

        /// <summary>全量扫描并重建 name→set 索引。</summary>
        private static void RebuildAnimationSetCache()
        {
            s_AnimationSetCache.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(PartyCharacterAnimationSet)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PartyCharacterAnimationSet candidate = AssetDatabase.LoadAssetAtPath<PartyCharacterAnimationSet>(path);
                if (candidate != null && !s_AnimationSetCache.ContainsKey(candidate.name))
                    s_AnimationSetCache[candidate.name] = candidate;
            }
        }

        /// <summary>资源变更时清空缓存，下次访问时重建。</summary>
        private static void InvalidateAnimationSetCache()
        {
            s_AnimationSetCacheInitialized = false;
            s_AnimationSetCache = null;
        }

        /// <summary>
        /// Stores the actor pose once for this preview pass.
        /// </summary>
        private void CachePreviewSnapshot()
        {
            if (_hasPreviewSnapshot || actor == null)
                return;

            _previewSnapshot = new TransformSnapshot(actor, TransformSnapshot.StoreMode.All);
            _hasPreviewSnapshot = true;
        }

        /// <summary>
        /// Restores the cached actor pose after preview playback leaves the clip.
        /// </summary>
        private void RestorePreviewSnapshot()
        {
            if (!_hasPreviewSnapshot || _previewSnapshot == null)
                return;

            _previewSnapshot.Restore();
            _previewSnapshot = null;
            _hasPreviewSnapshot = false;
        }

        /// <summary>
        /// Samples the selected AnimationClip at the requested local skill time.
        /// </summary>
        private void SamplePreviewAtLocalTime(float localTime)
        {
            if (Application.isPlaying || actor == null || !TryResolveEditorAnimationClip(out AnimationClip clip))
                return;

            // Translate skill-local time into animation-local time using the same offset/speed protocol as runtime playback.
            float sampleTime = ResolveSampleTime(clip, Mathf.Max(0f, localTime));
            clip.SampleAnimation(actor, sampleTime);
        }

        /// <summary>
        /// Converts local skill time into a clamped or looped animation sample time.
        /// </summary>
        private float ResolveSampleTime(AnimationClip clip, float localTime)
        {
            if (clip.length <= 0f)
                return 0f;

            float offset = Mathf.Clamp01(NormalizedStartTime) * clip.length;
            float sampleTime = offset + localTime * ResolveSubClipSpeed();
            return clip.isLooping ? Mathf.Repeat(sampleTime, clip.length) : Mathf.Clamp(sampleTime, 0f, clip.length);
        }
#endif
    }
}
