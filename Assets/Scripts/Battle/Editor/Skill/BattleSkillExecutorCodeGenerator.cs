#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hoshino;
using UnityEditor;
using UnityEngine;

namespace Battle
{
    public static class BattleSkillExecutorCodeGenerator
    {
        private const string OutputPath = "Assets/Scripts/Generated/Battle/Skill/BattleSkillExecutorBindings.cs";

        private sealed class ExecutorBinding
        {
            public uint ClipId;
            public SkillNodeExecutionDomain Domain;
            public string ExecutorTypeName;
        }

        [MenuItem("Tools/Battle/Generate Skill Executor Bindings")]
        public static void Generate()
        {
            List<SkillTypeInfo> clips = SkillCodeGenUtilities.GatherTypes(SkillSerializedTypeKind.Clip);
            Dictionary<uint, ExecutorBinding> bindings = GatherExecutorBindings(clips);
            ValidateBindings(clips, bindings);

            SkillCodeGenUtilities.EnsureFolder(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, GenerateCode(clips, bindings));
            AssetDatabase.Refresh();
        }

        private static Dictionary<uint, ExecutorBinding> GatherExecutorBindings(List<SkillTypeInfo> clips)
        {
            Dictionary<uint, ExecutorBinding> results = new();
            HashSet<uint> knownClipIds = new(clips.Select(c => c.Id));

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in SkillCodeGenUtilities.GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract)
                        continue;

                    BattleSkillExecutorAttribute attribute = type.GetCustomAttribute<BattleSkillExecutorAttribute>();
                    if (attribute == null)
                        continue;

                    uint clipId = attribute.ClipId;
                    if (!knownClipIds.Contains(clipId))
                        throw new InvalidOperationException($"[BattleSkillExecutor] on {type.FullName} binds to unknown clip id {clipId}.");

                    if (results.ContainsKey(clipId))
                        throw new InvalidOperationException($"Duplicate [BattleSkillExecutor] binding for clip id {clipId}.");

                    SkillNodeExecutionDomain domain = InferDomain(type);

                    results.Add(clipId, new ExecutorBinding
                    {
                        ClipId = clipId,
                        Domain = domain,
                        ExecutorTypeName = type.FullName
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// 从 Executor 类型的基类继承链推断 domain。
        /// 匹配 ClientPredictionSkillExecutor&lt;&gt;/ClientOnlySkillExecutor&lt;&gt;/ServerOnlySkillExecutor&lt;&gt; 的泛型定义。
        /// 未匹配到 domain 基类则抛错。
        /// </summary>
        private static SkillNodeExecutionDomain InferDomain(Type executorType)
        {
            Type clientPredictionBase = typeof(ClientPredictionSkillExecutor<>);
            Type clientOnlyBase = typeof(ClientOnlySkillExecutor<>);
            Type serverOnlyBase = typeof(ServerOnlySkillExecutor<>);

            Type current = executorType;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType)
                {
                    Type genericDef = current.GetGenericTypeDefinition();
                    if (genericDef == clientPredictionBase)
                        return SkillNodeExecutionDomain.ClientPrediction;
                    if (genericDef == clientOnlyBase)
                        return SkillNodeExecutionDomain.ClientOnly;
                    if (genericDef == serverOnlyBase)
                        return SkillNodeExecutionDomain.ServerOnly;
                }
                current = current.BaseType;
            }

            throw new InvalidOperationException(
                $"Executor {executorType.FullName} does not inherit from any domain base class " +
                "(ClientPredictionSkillExecutor<>/ClientOnlySkillExecutor<>/ServerOnlySkillExecutor<>). " +
                "Executors must inherit from a domain base class.");
        }

        private static void ValidateBindings(List<SkillTypeInfo> clips, Dictionary<uint, ExecutorBinding> bindings)
        {
            foreach (SkillTypeInfo clip in clips)
            {
                if (!bindings.ContainsKey(clip.Id))
                    throw new InvalidOperationException($"Clip {clip.Type.FullName} ({clip.Id}) has no [BattleSkillExecutor] binding. Add an executor with [BattleSkillExecutor({clip.Id}, domain)] or run 'Tools/Hoshino/Generate Skill Serialization Code' first to refresh the clip list.");
            }
        }

        private static string GenerateCode(List<SkillTypeInfo> clips, Dictionary<uint, ExecutorBinding> bindings)
        {
            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// Generated by Tools/Battle/Generate Skill Executor Bindings.");
            sb.AppendLine("// Requires Skill serialization code to be generated first (Tools/Hoshino/Generate Skill Serialization Code).");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Hoshino;");
            sb.AppendLine();
            sb.AppendLine("namespace Battle");
            sb.AppendLine("{");
            sb.AppendLine("    public static class SkillGeneratedExecutorMetas");
            sb.AppendLine("    {");
            sb.AppendLine("        public struct ExecutorEntry");
            sb.AppendLine("        {");
            sb.AppendLine("            public string ExecutorTypeName;");
            sb.AppendLine("            public SkillNodeExecutionDomain Domain;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Dictionary<uint, ExecutorEntry> _entries = new()");
            sb.AppendLine("        {");
            foreach (SkillTypeInfo clip in clips)
            {
                if (bindings.TryGetValue(clip.Id, out ExecutorBinding binding))
                {
                    sb.AppendLine($"            {{ SkillGeneratedIds.{SkillCodeGenUtilities.GetIdName(clip)}, new ExecutorEntry {{ ExecutorTypeName = \"{SkillCodeGenUtilities.EscapeString(binding.ExecutorTypeName)}\", Domain = SkillNodeExecutionDomain.{binding.Domain} }} }},");
                }
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetName(uint clipId, out string executorTypeName)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_entries.TryGetValue(clipId, out ExecutorEntry entry))");
            sb.AppendLine("            {");
            sb.AppendLine("                executorTypeName = entry.ExecutorTypeName;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            executorTypeName = null;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetDomain(uint clipId, out SkillNodeExecutionDomain domain)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (TryGetEntry(clipId, out ExecutorEntry entry))");
            sb.AppendLine("            {");
            sb.AppendLine("                domain = entry.Domain;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            domain = default;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetEntry(uint clipId, out ExecutorEntry entry)");
            sb.AppendLine("        {");
            sb.AppendLine("            return _entries.TryGetValue(clipId, out entry);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}

#endif
