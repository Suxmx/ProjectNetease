using System.Collections;
using Animancer;
using UnityEngine;

namespace NetDemo
{
    public sealed class NetDemoPlayerVisual : MonoBehaviour
    {
        [Header("Animancer")]
        [SerializeField] private AnimancerComponent _animancer;
        [SerializeField] private AnimationClip _idleClip;
        [SerializeField] private AnimationClip _moveClip;
        [SerializeField] private AnimationClip _attackClip;
        [SerializeField] private AnimationClip _hitClip;
        [SerializeField] private AnimationClip _deathClip;

        [Header("Locomotion")]
        [SerializeField] private Rigidbody _sourceRigidbody;
        [SerializeField] private float _moveThreshold = 0.15f;
        [SerializeField] private float _fadeDuration = 0.12f;

        [Header("Hit Flash")]
        [SerializeField] private Color _hitFlashColor = Color.red;
        [SerializeField] private float _hitFlashDuration = 0.12f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private AnimationClip _currentClip;
        private float _lockedUntil;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _hitFlashRoutine;
        private bool _warnedMissingAnimancer;
        private bool _warnedIdle;
        private bool _warnedMove;
        private bool _warnedAttack;
        private bool _warnedHit;
        private bool _warnedDeath;

        private void Awake()
        {
            if (_animancer == null)
                _animancer = GetComponentInChildren<AnimancerComponent>();
            if (_sourceRigidbody == null)
                _sourceRigidbody = GetComponentInParent<Rigidbody>();

            _renderers = GetComponentsInChildren<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            ClearHitFlash();
            _hitFlashRoutine = null;
        }

        private void Update()
        {
            if (Time.time < _lockedUntil)
                return;

            float speed = 0f;
            if (_sourceRigidbody != null)
            {
                Vector3 velocity = GetVelocity(_sourceRigidbody);
                velocity.y = 0f;
                speed = velocity.magnitude;
            }

            PlayLoop(speed > _moveThreshold ? _moveClip : _idleClip);
        }

        public void PlayAttack()
        {
            PlayOneShot(_attackClip, 0.35f, ref _warnedAttack, "attack");
        }

        public void PlayHit()
        {
            FlashHit();
            PlayOneShot(_hitClip, 0.25f, ref _warnedHit, "hit");
        }

        public void PlayDeath()
        {
            PlayOneShot(_deathClip, 0.8f, ref _warnedDeath, "death");
        }

        private void PlayLoop(AnimationClip clip)
        {
            if (clip == null)
            {
                WarnMissingLoopClip();
                return;
            }

            if (!CanPlay())
                return;
            if (_currentClip == clip)
                return;

            _animancer.Play(clip, _fadeDuration);
            _currentClip = clip;
        }

        private void PlayOneShot(AnimationClip clip, float fallbackDuration, ref bool warned, string label)
        {
            if (clip == null)
            {
                WarnOnce(ref warned, $"NetDemo Animancer {label} clip is not assigned on {name}; skipping placeholder animation.");
                return;
            }

            if (!CanPlay())
                return;

            _animancer.Play(clip, _fadeDuration);
            _currentClip = clip;
            _lockedUntil = Time.time + Mathf.Max(clip.length, fallbackDuration);
        }

        private void FlashHit()
        {
            if (_hitFlashDuration <= 0f)
                return;

            if (_hitFlashRoutine != null)
                StopCoroutine(_hitFlashRoutine);

            _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            SetHitFlash(_hitFlashColor);
            yield return new WaitForSeconds(_hitFlashDuration);
            ClearHitFlash();
            _hitFlashRoutine = null;
        }

        private void SetHitFlash(Color color)
        {
            if (_renderers == null)
                return;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private void ClearHitFlash()
        {
            if (_renderers == null)
                return;

            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }
        }

        private bool CanPlay()
        {
            if (_animancer != null)
                return true;

            WarnOnce(ref _warnedMissingAnimancer, $"NetDemo AnimancerComponent is missing on {name}; animation playback is disabled.");
            return false;
        }

        private void WarnMissingLoopClip()
        {
            if (_sourceRigidbody != null)
            {
                Vector3 velocity = GetVelocity(_sourceRigidbody);
                velocity.y = 0f;
                if (velocity.magnitude > _moveThreshold)
                {
                    WarnOnce(ref _warnedMove, $"NetDemo Animancer move clip is not assigned on {name}; skipping placeholder animation.");
                    return;
                }
            }

            WarnOnce(ref _warnedIdle, $"NetDemo Animancer idle clip is not assigned on {name}; skipping placeholder animation.");
        }

        private static void WarnOnce(ref bool flag, string message)
        {
            if (flag)
                return;

            flag = true;
            Debug.LogWarning(message);
        }

        private static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }
    }
}
