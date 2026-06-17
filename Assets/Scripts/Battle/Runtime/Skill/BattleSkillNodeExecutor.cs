using System;
using System.Collections.Generic;
using System.Reflection;
using Hoshino;
using UnityEngine;

namespace Battle
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BattleSkillExecutorAttribute : Attribute
    {
        public BattleSkillExecutorAttribute(uint clipId, SkillNodeExecutionDomain domain)
        {
            ClipId = clipId;
            Domain = domain;
        }

        public uint ClipId { get; }
        public SkillNodeExecutionDomain Domain { get; }
    }

    public interface IBattleSkillNodeExecutor
    {
        void Execute(in BattleSkillExecutionContext context);
    }

    public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
        where TData : struct
    {
        public void Execute(in BattleSkillExecutionContext context)
        {
            if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
            {
                Debug.LogWarning($"[BattleSkill] Node data mismatch. Expected {typeof(TData).Name} for clip id {context.Node.ClipId}.");
                return;
            }

            Execute(context, in data);
        }

        protected abstract void Execute(in BattleSkillExecutionContext context, in TData data);
    }

    public static class BattleSkillNodeExecutorRegistry
    {
        private static Dictionary<string, IBattleSkillNodeExecutor> _executors;

        public static bool TryGet(uint clipId, SkillNodeExecutionDomain domain, out IBattleSkillNodeExecutor executor)
        {
            EnsureInitialized();
            string key = BuildKey(clipId, domain);
            if (_executors.TryGetValue(key, out executor))
                return true;

            if (!SkillGeneratedExecutorBindings.TryGet(clipId, domain, out string executorTypeName))
                return false;

            Type type = ResolveType(executorTypeName);
            if (type == null || type.IsAbstract || !typeof(IBattleSkillNodeExecutor).IsAssignableFrom(type))
            {
                Debug.LogError($"[BattleSkill] Generated executor binding points to invalid type '{executorTypeName}'.");
                return false;
            }

            executor = (IBattleSkillNodeExecutor)Activator.CreateInstance(type);
            _executors.Add(key, executor);
            return true;
        }

        private static void EnsureInitialized()
        {
            if (_executors != null)
                return;

            _executors = new Dictionary<string, IBattleSkillNodeExecutor>();
        }

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

        private static string BuildKey(uint clipId, SkillNodeExecutionDomain domain)
        {
            return $"{clipId}|{(byte)domain}";
        }

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
