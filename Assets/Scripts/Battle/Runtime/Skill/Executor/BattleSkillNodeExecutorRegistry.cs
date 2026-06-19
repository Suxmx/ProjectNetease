using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Executor 注册表。按 ClipId 查找 Executor 实例。
    /// 通过 <see cref="Init"/> 一次性扫描所有 [BattleSkillExecutor] 标记的类型并预创建实例，
    /// 之后 <see cref="TryGet"/> 为纯字典查找，无运行时反射开销。
    /// 场景切换或热重载时调用 <see cref="Clear"/> 清空缓存。
    /// </summary>
    public static class BattleSkillNodeExecutorRegistry
    {
        private static Dictionary<uint, IBattleSkillNodeExecutor> _executors;
        private static bool _initialized;

        /// <summary>
        /// 预扫描所有 [BattleSkillExecutor] 标记的类型，创建实例并缓存。
        /// 应在场景初始化时显式调用一次，避免 tick 中懒初始化卡顿。
        /// 重复调用安全（已初始化则跳过）。
        /// </summary>
        public static void Init()
        {
            if (_initialized)
                return;

            _executors = new Dictionary<uint, IBattleSkillNodeExecutor>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract)
                        continue;

                    BattleSkillExecutorAttribute attribute = type.GetCustomAttribute<BattleSkillExecutorAttribute>();
                    if (attribute == null)
                        continue;

                    if (!typeof(IBattleSkillNodeExecutor).IsAssignableFrom(type))
                    {
                        Debug.LogError($"[BattleSkill] Executor type '{type.FullName}' does not implement {nameof(IBattleSkillNodeExecutor)}.");
                        continue;
                    }

                    if (_executors.ContainsKey(attribute.ClipId))
                    {
                        Debug.LogError($"[BattleSkill] Duplicate executor for clip id {attribute.ClipId}: '{_executors[attribute.ClipId].GetType().FullName}' vs '{type.FullName}'.");
                        continue;
                    }

                    _executors.Add(attribute.ClipId, (IBattleSkillNodeExecutor)Activator.CreateInstance(type));
                }
            }

            _initialized = true;
        }

        /// <summary>清空缓存，允许重新 <see cref="Init"/>。场景切换或热重载时调用。</summary>
        public static void Clear()
        {
            _executors?.Clear();
            _executors = null;
            _initialized = false;
        }

        /// <summary>按 ClipId 查找 Executor（纯字典查找，无反射）。未初始化时自动 Init。</summary>
        public static bool TryGet(uint clipId, out IBattleSkillNodeExecutor executor)
        {
            if (!_initialized)
                Init();

            if (_executors.TryGetValue(clipId, out executor))
                return true;

            executor = default;
            return false;
        }

        /// <summary>安全获取程序集类型（处理 ReflectionTypeLoadException）。</summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types;
            }
        }
    }
}
