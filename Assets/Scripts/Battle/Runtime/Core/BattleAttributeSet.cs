using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Utility.Template;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 角色属性集。管理移动速度、伤害加成、受伤加成等属性的运行时值，
    /// 通过 TimedModifier 机制支持临时属性修改（未来 buff 系统的数据载体）。
    /// 服务端权威 tick 驱动到期清理和重算，SyncVar 同步最终值给客户端。
    /// </summary>
    public sealed class BattleAttributeSet : TickNetworkBehaviour
    {
        private const string MoveSpeedMultiplierKey = "MoveSpeedMultiplier";
        private const string OutgoingDamageMultiplierKey = "OutgoingDamageMultiplier";
        private const string IncomingDamageMultiplierKey = "IncomingDamageMultiplier";

        /// <summary>临时属性修改条目，按 tick 到期自动清除。</summary>
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

        /// <summary>
        /// 服务端施加临时属性修改。
        /// </summary>
        /// <param name="key">属性 key（支持别名：MoveSpeed/Damage/Defense）。</param>
        /// <param name="addValue">加法值。</param>
        /// <param name="multiplyValue">乘法值（0 视为 1）。</param>
        /// <param name="durationSeconds">持续时间（秒）。</param>
        [Server]
        public void ApplyModifier(string key, float addValue, float multiplyValue, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            // --- 计算持续 tick 数，最少 1 tick ---
            float safeMultiplier = Mathf.Approximately(multiplyValue, 0f) ? 1f : multiplyValue;
            uint durationTicks = TimeManager != null
                ? BattleTimeUtility.SecondsToTicks((float)TimeManager.TickDelta, Mathf.Max(0f, durationSeconds))
                : 1u;
            if (durationTicks == 0u)
                durationTicks = 1u;

            // --- 添加 modifier 并立即重算 ---
            _modifiers.Add(new TimedModifier
            {
                Key = key,
                AddValue = addValue,
                MultiplyValue = safeMultiplier,
                EndTick = TimeManager.LocalTick + durationTicks
            });

            Recalculate();
        }

        /// <summary>每 tick 清理过期 modifier 并重算属性。</summary>
        protected override void TimeManager_OnTick()
        {
            if (!IsServerStarted)
                return;

            // --- 从后向前删除已过期的 modifier ---
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

        /// <summary>遍历所有 active modifier，按 key 分类累加，写回 SyncVar。</summary>
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

        /// <summary>对单个属性值应用加法 + 乘法。</summary>
        private static void Apply(ref float value, TimedModifier modifier)
        {
            value = (value + modifier.AddValue) * modifier.MultiplyValue;
        }

        /// <summary>忽略大小写匹配属性 key。</summary>
        private static bool Matches(string key, string expected)
        {
            return string.Equals(key, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
