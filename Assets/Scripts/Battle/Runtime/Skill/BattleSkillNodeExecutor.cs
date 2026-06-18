using System;
using System.Collections.Generic;
using System.Reflection;
using Hoshino;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 标记一个类为技能节点 Executor，绑定 ClipId 和执行域。
    /// 代码生成器扫描此特性生成 <see cref="SkillGeneratedExecutorBindings"/>。
    /// </summary>
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

    /// <summary>Executor 统一接口，由 <see cref="BattleSkillController"/> 调度。</summary>
    public interface IBattleSkillNodeExecutor
    {
        void Execute(in BattleSkillExecutionContext context);
    }

    /// <summary>
    /// 泛型 Executor 基类。自动从 NodeDataBlob 反序列化节点数据并传给子类。
    /// 新增技能节点时继承此类并实现抽象 <see cref="Execute(in BattleSkillExecutionContext, in TData)"/>。
    /// </summary>
    /// <typeparam name="TData">节点数据结构（生成的 XxxNodeData）。</typeparam>
    public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
        where TData : struct
    {
        /// <summary>反序列化节点数据并分发给子类。</summary>
        public void Execute(in BattleSkillExecutionContext context)
        {
            if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
            {
                Debug.LogWarning($"[BattleSkill] Node data mismatch. Expected {typeof(TData).Name} for clip id {context.Node.ClipId}.");
                return;
            }

            Execute(context, in data);
        }

        /// <summary>子类实现具体节点逻辑。</summary>
        protected abstract void Execute(in BattleSkillExecutionContext context, in TData data);
    }

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
            if (!SkillGeneratedExecutorBindings.TryGet(clipId, out string executorTypeName))
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
