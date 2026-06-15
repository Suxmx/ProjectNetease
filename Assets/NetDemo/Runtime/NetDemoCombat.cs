using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using Hoshino;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetDemo
{
    public sealed class NetDemoCombat : NetworkBehaviour
    {
        [Header("Attack")]
        [SerializeField] private int _damage = 25;
        [SerializeField] private float _cooldown = 0.6f;
        [SerializeField] private float _windup = 0.12f;
        [SerializeField] private Vector3 _halfExtents = new(0.8f, 0.6f, 0.9f);
        [SerializeField] private float _forwardOffset = 1.2f;
        [SerializeField] private float _heightOffset = 0.6f;
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Attack Hint")]
        [SerializeField] private Color _attackHintColor = new(1f, 0.65f, 0.05f, 0.28f);
        [SerializeField] private float _attackHintPostResolveDuration = 0.1f;

        private NetDemoHealth _health;
        private NetDemoPredictedMotor _motor;
        private NetDemoPlayerVisual _visual;
        private float _nextServerAttackTime;
        private float _nextLocalAttackTime;
        private float _attackHintUntil;
        private Vector3 _attackHintDirection = Vector3.forward;

        private void Awake()
        {
            _health = GetComponent<NetDemoHealth>();
            _motor = GetComponent<NetDemoPredictedMotor>();
            _visual = GetComponentInChildren<NetDemoPlayerVisual>();
        }

        private void Update()
        {
            DrawAttackHintIfActive();

            if (!IsOwner)
                return;
            if (_health != null && _health.IsDead)
                return;
            if (Time.time < _nextLocalAttackTime)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector3 direction = _motor != null ? _motor.CurrentLookDirection : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            _visual?.PlayAttack();
            _nextLocalAttackTime = Time.time + _cooldown;
            ShowAttackHint(direction);
            uint requestTick = TimeManager == null ? 0u : TimeManager.LocalTick;
            ServerRequestAttack(direction, requestTick);
        }

        [ServerRpc]
        private void ServerRequestAttack(Vector3 lookDirection, uint requestTick)
        {
            if (_health != null && _health.IsDead)
                return;
            if (Time.time < _nextServerAttackTime)
                return;

            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude <= 0.0001f)
                lookDirection = transform.forward;
            lookDirection.Normalize();

            _nextServerAttackTime = Time.time + _cooldown;
            ObserversPlayAttack(lookDirection);
            StartCoroutine(ServerAttackRoutine(lookDirection, requestTick));
        }

        private IEnumerator ServerAttackRoutine(Vector3 direction, uint requestTick)
        {
            if (_windup > 0f)
                yield return new WaitForSeconds(_windup);

            if (_health != null && _health.IsDead)
                yield break;

            ResolveServerHit(direction, requestTick);
        }

        [Server]
        private void ResolveServerHit(Vector3 direction, uint requestTick)
        {
            Vector3 center = transform.position + direction * _forwardOffset + Vector3.up * _heightOffset;
            Quaternion orientation = Quaternion.LookRotation(direction, Vector3.up);
            Collider[] hits = Physics.OverlapBox(center, _halfExtents, orientation, _hitMask, QueryTriggerInteraction.Ignore);

            HashSet<NetDemoHealth> damagedTargets = new();
            foreach (Collider hit in hits)
            {
                NetDemoHealth target = hit.GetComponentInParent<NetDemoHealth>();
                if (target == null || target == _health)
                    continue;
                if (target.IsDead)
                    continue;
                if (!damagedTargets.Add(target))
                    continue;

                NetworkConnection attacker = Owner;
                if (target.TryTakeDamage(_damage, attacker))
                {
                    int attackerId = Owner.IsValid ? Owner.ClientId : -1;
                    int victimId = target.Owner.IsValid ? target.Owner.ClientId : -1;
                    Debug.Log($"[NetDemo] Attack tick {requestTick}: player {attackerId} hit player {victimId}.");
                }
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversPlayAttack(Vector3 lookDirection)
        {
            _visual?.PlayAttack();
            ShowAttackHint(lookDirection);
        }

        private void ShowAttackHint(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;

            _attackHintDirection = direction.normalized;
            float duration = Mathf.Max(Time.deltaTime, _windup) + _attackHintPostResolveDuration;
            _attackHintUntil = Mathf.Max(_attackHintUntil, Time.time + duration);
        }

        private void DrawAttackHintIfActive()
        {
            if (Time.time > _attackHintUntil)
                return;

            Vector3 direction = _attackHintDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            Vector3 center = transform.position + direction * _forwardOffset + Vector3.up * _heightOffset;
            Quaternion orientation = Quaternion.LookRotation(direction, Vector3.up);
            SkillDraw.BoxWithOutline(center, orientation, _halfExtents, _attackHintColor);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;
            direction.Normalize();

            Gizmos.color = Color.red;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + direction * _forwardOffset + Vector3.up * _heightOffset, Quaternion.LookRotation(direction, Vector3.up), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _halfExtents * 2f);
            Gizmos.matrix = previous;
        }
    }
}
