using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    public sealed class BattleAttributeSet : TickNetworkBehaviour
    {
        private const string MoveSpeedMultiplierKey = "MoveSpeedMultiplier";
        private const string OutgoingDamageMultiplierKey = "OutgoingDamageMultiplier";
        private const string IncomingDamageMultiplierKey = "IncomingDamageMultiplier";

        private struct TimedModifier
        {
            public string Key;
            public float AddValue;
            public float MultiplyValue;
            public uint EndTick;
        }

        private readonly SyncVar<float> _moveSpeedMultiplier = new(1f);
        private readonly SyncVar<float> _outgoingDamageMultiplier = new(1f);
        private readonly SyncVar<float> _incomingDamageMultiplier = new(1f);
        private readonly List<TimedModifier> _modifiers = new();

        public float MoveSpeedMultiplier => _moveSpeedMultiplier.Value;
        public float OutgoingDamageMultiplier => _outgoingDamageMultiplier.Value;
        public float IncomingDamageMultiplier => _incomingDamageMultiplier.Value;

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick);
        }

        [Server]
        public void ApplyModifier(string key, float addValue, float multiplyValue, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            float safeMultiplier = Mathf.Approximately(multiplyValue, 0f) ? 1f : multiplyValue;
            uint durationTicks = SecondsToTicks(Mathf.Max(0f, durationSeconds));
            if (durationTicks == 0u)
                durationTicks = 1u;
            _modifiers.Add(new TimedModifier
            {
                Key = key,
                AddValue = addValue,
                MultiplyValue = safeMultiplier,
                EndTick = TimeManager.LocalTick + durationTicks
            });

            Recalculate();
        }

        protected override void TimeManager_OnTick()
        {
            if (!IsServerStarted)
                return;

            bool changed = false;
            uint tick = TimeManager.LocalTick;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (tick < _modifiers[i].EndTick)
                    continue;

                _modifiers.RemoveAt(i);
                changed = true;
            }

            if (changed)
                Recalculate();
        }

        private void Recalculate()
        {
            float moveSpeed = 1f;
            float outgoingDamage = 1f;
            float incomingDamage = 1f;

            foreach (TimedModifier modifier in _modifiers)
            {
                if (Matches(modifier.Key, MoveSpeedMultiplierKey) || Matches(modifier.Key, "MoveSpeed"))
                    Apply(ref moveSpeed, modifier);
                else if (Matches(modifier.Key, OutgoingDamageMultiplierKey) || Matches(modifier.Key, "Damage"))
                    Apply(ref outgoingDamage, modifier);
                else if (Matches(modifier.Key, IncomingDamageMultiplierKey) || Matches(modifier.Key, "Defense"))
                    Apply(ref incomingDamage, modifier);
            }

            _moveSpeedMultiplier.Value = Mathf.Max(0f, moveSpeed);
            _outgoingDamageMultiplier.Value = Mathf.Max(0f, outgoingDamage);
            _incomingDamageMultiplier.Value = Mathf.Max(0f, incomingDamage);
        }

        private uint SecondsToTicks(float seconds)
        {
            if (TimeManager == null)
                return 1u;

            return (uint)Mathf.CeilToInt(seconds / Mathf.Max(0.0001f, (float)TimeManager.TickDelta));
        }

        private static void Apply(ref float value, TimedModifier modifier)
        {
            value = (value + modifier.AddValue) * modifier.MultiplyValue;
        }

        private static bool Matches(string key, string expected)
        {
            return string.Equals(key, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
