using System;
using System.Collections.Generic;
using System.Reflection;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// Executor 注册表。按 ClipId 查找 Executor 实例，
    /// 首次访问时通过生成绑定表反射创建，后续缓存复用。
    /// </summary>
    public static class BattleSkillNodeExecutorRegistry
    {
        private static Dictionary<uint, IBattleSkillNodeExecutor> _executors;

        /// <summary>按 ClipId 查找或创建 Executor。</summary>
        public static bool TryGet(uint clipId, out IBattleSkillNodeExecutor executor)
        {
            EnsureInitialized();

            // --- 先查缓存 ---
            if (_executors.TryGetValue(clipId, out executor))
                return true;

            // --- 查生成绑定表获取类型名，反射创建实例 ---
            if (!SkillGeneratedExecutorMetas.TryGetName(clipId, out string executorTypeName))
                return false;

            Type type = ResolveType(executorTypeName);
            if (type == null || type.IsAbstract || !typeof(IBattleSkillNodeExecutor).IsAssignableFrom(type))
            {
                Debug.LogError($"[BattleSkill] Generated executor binding points to invalid type '{executorTypeName}'.");
                return false;
            }

            executor = (IBattleSkillNodeExecutor)Activator.CreateInstance(type);
            _executors.Add(clipId, executor);
            return true;
        }

        private static void EnsureInitialized()
        {
            if (_executors != null)
                return;

            _executors = new Dictionary<uint, IBattleSkillNodeExecutor>();
        }

        /// <summary>按全名解析类型，先 Type.GetType 再遍历程序集。</summary>
        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type candidate in GetLoadableTypes(assembly))
                {
                    if (candidate != null && candidate.FullName == typeName)
                        return candidate;
                }
            }

            return null;
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
