# 数据流与代码生成

## 数据流总览

```
编辑器 .skill (Slate 层级 Group/Track/Clip + 数据黑板 SpecialDatas)
  → SkillDefinitionCompiler 编译
  → .bytes (二进制 SkillDefinition: nodes + specialDatas)
  → 运行时 SkillDefinition.FromBytes 加载
  → SkillController 调度
  → Executor 执行
```

## 文件格式

| 文件 | 路径 | 格式 | 说明 |
|------|------|------|------|
| .skill | `Assets/SkillData/*.skill` | 二进制，BinaryVersion=2 | 编辑器源数据 |
| .bytes | `Assets/SkillData/Compiled/{SkillName}.bytes` | 二进制，BinaryVersion=1 | 运行时权威文件 |

**技能长度**：编译时取所有节点 EndTick 的最大值（而非编辑器设定的总长度）。

## SkillDefinition 结构

```
SkillDefinition
├─ SkillId / SkillKey / Version / SourceTickRate
├─ LengthTicks (节点 EndTick 最大值)
├─ Nodes[] (SkillRuntimeNode)
│  └─ NodeId / ClipId / StartTick / EndTick / DataOffset / DataLength
├─ NodeDataBlob (二进制数据块，各节点数据紧凑排列)
├─ SpecialDatas[] (SkillRuntimeSpecialData)
│  └─ SpecialDataId / SpecialDataTypeId / DataOffset / DataLength
└─ SpecialDataBlob (二进制数据块)
```

`SkillRuntimeNode.IsActiveAt(localTick)`：`localTick >= StartTick && localTick < EndTick`

## 数据黑板（SpecialData）

技能级特殊数据容器，独立于 Group/Track/Clip 层级。通过 SkillEditor 工具栏"数据黑板"按钮编辑。

- `[SkillSpecialDataType(id)]` 标记类（id 范围 2001+），字段打 `[SkillCustomData]`
- 生成 `RuntimeXxxData` struct + Blob 读写
- 与 Clip 的 `[SkillCustomData]` 同构

当前内置：`DamageGroupData`（id=2001，GroupId + MaxHitsPerTarget）

## 代码生成（两个菜单，都要跑）

| 菜单 | 生成内容 | 生成器 |
|------|----------|--------|
| `Tools/Hoshino/Generate Skill Serialization Code` | NodeData/SpecialData 结构、Blob 读写、IsClipKnown/IsSpecialDataKnown | `SkillSerializationCodeGenerator` |
| `Tools/Battle/Generate Skill Executor Bindings` | Executor 绑定表 + domain 表（从继承链推断 domain） | `BattleSkillExecutorCodeGenerator` |

**生成器产物文件提交进仓库**，保证不跑生成器也能编译。

## 生成产物位置

```
Generated/
├─ Battle/Skill/
│  └─ BattleSkillExecutorBindings.cs   ← Executor 绑定表 + domain 表
└─ Skill/
   ├─ Runtime/
   │  └─ SkillGeneratedSerialization.cs   ← NodeData/SpecialData struct + Blob 读写
   └─ Editor/
      └─ SkillGeneratedEditorSerialization.cs
```

## 新增技能节点流程

1. 新建 Clip（继承 Slate `ActionClip`），打 `[SkillClipType(id)]`，字段打 `[SkillCustomData]`
2. 新建 Executor，继承 domain 基类（`ClientPredictionSkillExecutor<TData>` 等），打 `[SkillExecutor(id)]`
3. 运行 `Tools/Hoshino/Generate Skill Serialization Code`
4. 运行 `Tools/Battle/Generate Skill Executor Bindings`
5. 保存 `.skill`，编译（`Tools/Hoshino/Compile All Skill Definitions`）
6. 把 `.bytes` 拖到 `SkillController` 的技能槽

## 新增特殊数据类型流程

1. 新建类，打 `[SkillSpecialDataType(id)]`（id 范围 2001+），字段打 `[SkillCustomData]`
2. 运行 `Tools/Hoshino/Generate Skill Serialization Code`
3. 在 SkillEditor 数据黑板面板里可添加该类型条目

## 关键代码位置

| 代码 | 文件 |
|------|------|
| SkillDefinition | `Skill/Skill/Runtime/Compiled/SkillDefinition.cs` |
| SkillDefinitionCompiler | `Skill/Skill/Editor/Compiler/SkillDefinitionCompiler.cs` |
| SkillSerializer (.skill 读写) | `Skill/Skill/Editor/SkillSerializer.cs` |
| SkillSerializationCodeGenerator | `Skill/Skill/Editor/CodeGen/SkillSerializationCodeGenerator.cs` |
| BattleSkillExecutorCodeGenerator | `Battle/Editor/Skill/BattleSkillExecutorCodeGenerator.cs` |
| SkillCodeGenUtilities | `Skill/Skill/Editor/CodeGen/SkillCodeGenUtilities.cs` |

## 生成器职责拆分

Skill 核心程序集（HoshinoSkill asmdef，子模块）只生成纯序列化代码（NodeData 结构、Blob 读写、Clip 注册表），不感知 Battle 的 Executor/Domain 概念。Battle 侧独立生成自己的 Executor 绑定与 Domain 表。

- `SkillSerializationCodeGenerator` — 只生成序列化相关代码，不生成 executor/domain
- `BattleSkillExecutorCodeGenerator` — 复用 `SkillCodeGenUtilities`，扫描 `[SkillExecutor]` 生成绑定与 Domain 表。校验每个已知 clip 必须有且仅有一个 executor
