# Skill 生成器职责拆分

## 模块职责

将 Skill 序列化代码生成与 Battle Executor 绑定生成解耦。Skill 核心程序集（HoshinoSkill）只负责生成纯序列化代码（NodeData 结构、Blob 读写、Clip 注册表），不再感知 Battle 的 Executor/Domain 概念。Battle 侧（Assembly-CSharp）独立生成自己的 Executor 绑定与 Domain 表。

## 关键文件

### Skill 核心程序集（HoshinoSkill asmdef，子模块）

- `Skill/Editor/CodeGen/SkillCodeGenUtilities.cs` — 公共代码生成框架。下游（Battle 或其他插件）复用此类快速编写自己的生成器。提供 `GatherTypes`/`GetCustomFields`/`GetIdName`/`GetNodeDataName`/`GetTypeName`/`ReadExpression`/`WriteStatement`/各 `Validate*`/`AppendVectorHelpers`/`EnsureFolder` 等。
- `Skill/Editor/CodeGen/SkillSerializationCodeGenerator.cs` — Skill 序列化代码生成器入口（菜单 `Tools/Hoshino/Generate Skill Serialization Code`）。只生成序列化相关代码，不生成 executor/domain。
- `Skill/Runtime/Generated/SkillGeneratedSerializationServices.cs` — 桥接接口 `ISkillGeneratedRuntimeSerialization`，仅含 `WriteBoxed`/`TryRead`/`IsClipKnown`。
- `Skill/Editor/Compiler/SkillDefinitionCompiler.cs` — 编译器用 `IsClipKnown` 作为 clip 过滤门（替代旧的 `TryGetExecutionDomain`）；Debug JSON 不再含 domain 字段。

### Battle 侧（Assembly-CSharp）

- `Assets/Scripts/Battle/Editor/Skill/BattleSkillExecutorCodeGenerator.cs` — Battle Executor 绑定生成器（菜单 `Tools/Battle/Generate Skill Executor Bindings`）。复用 `SkillCodeGenUtilities`，扫描 `[BattleSkillExecutor]` 生成绑定与 Domain 表。校验每个已知 clip 必须有且仅有一个 executor。
- `Assets/Scripts/Battle/Runtime/Skill/BattleSkillController.cs` — 调度逻辑改用 `BattleSkillExecutorDomains.TryGet` 查询 domain。
- `Assets/Scripts/Battle/Runtime/Skill/BattleSkillNodeExecutor.cs` — Executor 注册表，调用 `SkillGeneratedExecutorBindings.TryGet`（现位于 Battle 命名空间）。

### 生成产物（仓库内手工同步，保持可编译）

- `Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs` — Skill 序列化生成产物。含 `SkillGeneratedIds`/`XxxNodeData`/`SkillGeneratedNodeDataBlob`（含 `IsClipKnown`）/`SkillGeneratedRuntimeSerialization`。不含 executor/domain。
- `Assets/Scripts/Generated/Battle/Skill/BattleSkillExecutorBindings.cs` — Battle 绑定生成产物。含 `SkillGeneratedExecutorBindings.TryGet`（clipId→executor 类型名）和 `BattleSkillExecutorDomains.TryGet`（clipId→domain）。

## 新增技能节点的标准流程

1. 新建 Clip（继承 Slate `ActionClip`），打 `[SkillClipType(id)]`，字段打 `[SkillCustomData]`。
2. 新建 Executor，打 `[BattleSkillExecutor(id, domain)]`。
3. 运行 `Tools/Hoshino/Generate Skill Serialization Code`（生成序列化代码）。
4. 运行 `Tools/Battle/Generate Skill Executor Bindings`（生成 executor 绑定）。
5. 保存 `.skill`，编译（`Tools/Hoshino/Compile All Skill Definitions` 或编辑器单技能编译）。
6. Battle 里把 `.bytes` 拖到 `BattleSkillController.SkillSlot.SkillBinary`。

## 上手要点

- 两个生成菜单都要跑：先 Skill 序列化，再 Battle 绑定。Battle 生成器依赖 Skill 生成的 clip 列表（通过 `SkillCodeGenUtilities.GatherTypes` 反射 `[SkillClipType]`）。
- 生成产物文件提交进仓库，保证不跑生成器也能编译。改了 Clip/Executor 后重新生成会更新这些文件。
- `SkillNodeExecutionDomain` 枚举仍定义在 `Hoshino` 命名空间（`SkillDefinition.cs`），Battle 侧 `using Hoshino;` 引用。这是共享枚举，不构成 Skill 对 Battle 的依赖。
- Skill 核心程序集零 Battle 依赖；Battle 侧通过 `SkillCodeGenUtilities` 框架快速搭建自己的生成器。
