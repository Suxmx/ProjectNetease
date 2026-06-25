# Skill 运行时读取与执行链路

> 从 `SkillDefinition.FromBytes` 到 Executor 拿到节点数据并执行的完整流程。
> 供后续开发者和 AI 理解架构、在新项目快速搭建读取层。

## 一、全景链路

```
.bytes 文件
  │
  ▼ SkillDefinition.FromBytes(bytes)          ← 框架层，通用，不感知具体 Clip
  │
  ├─ SkillRuntimeNode[] Nodes                 每个节点：NodeId/ClipId/StartTick/EndTick/DataOffset/DataLength
  ├─ byte[] NodeDataBlob                      所有节点自定义数据的原始字节拼在一起的 blob
  │
  ▼ SkillController 调度（按 tick 遍历 Nodes）
  │
  ├─ SkillGeneratedExecutorMetas.TryGetDomain(clipId)   ← 生成产物，查 domain 表
  ├─ SkillExecutorRegistry.TryGet(clipId)               ← 运行时注册表，查 Executor 实例
  │
  ▼ Executor.Execute(context)
  │
  ├─ SkillGeneratedNodeDataBlob.TryRead<TData>(skill, node, out data)   ← 生成产物，按 ClipId switch 读 blob
  │   ├─ 查缓存（SkillDefinition 内部 object[] 缓存）
  │   ├─ 从 NodeDataBlob 切片 [DataOffset, DataLength]
  │   ├─ ReadBoxed(reader, clipId) → switch 按 ClipId 读字段 → 返回 object
  │   └─ is TData 检查 + 缓存
  │
  ▼ domain 基类 OnExecute(context, data)
  │
  ├─ context.LifecyclePhase == Start → OnStart(context, data)
  ├─ context.LifecyclePhase == Tick  → OnTick(context, data)
  └─ context.LifecyclePhase == End   → OnEnd(context, data)
```

## 二、第一层：`.bytes` → `SkillDefinition`

### 二进制格式

```
[magic: 0x4B534348 "HCSK", 4B]
[version: 1, 4B]
[skillId: int, 4B]
[skillKey: string]
[sourceTickRate: int, 4B]
[lengthTicks: int, 4B]

[ nodes 段 ]
  [nodeCount: int]
  × nodeCount:
    [NodeId: int][SourceTrackName: string][ClipId: uint]
    [SourceLine: int][StartTick: int][EndTick: int]
    [DataOffset: int][DataLength: int]

[ nodeDataBlob 段 ]
  [blobLength: int]
  [raw bytes]

[ specialDatas 段 ]        ← 技能级特殊数据（如伤害组配置）
  [sdCount: int]
  × sdCount:
    [SpecialDataId: int][SpecialDataTypeId: uint]
    [DataOffset: int][DataLength: int]

[ specialDataBlob 段 ]
  [sdBlobLength: int]
  [raw bytes]
```

### 关键点

- `FromBytes` 是纯 `BinaryReader` 反序列化，**不知道**每个 node 的自定义数据是什么结构
- 每个 node 通过 `DataOffset` + `DataLength` 指向 `NodeDataBlob` 里的一段原始字节
- `ClipId` 只是一个 `uint`，`FromBytes` 不知道它对应什么 C# 类型
- `SkillDefinition` 提供 `GetCachedNodeData(nodeId)` / `SetCachedNodeData(nodeId, value)` 缓存机制，避免每 tick 重复反序列化

### 代码位置

- `SkillDefinition.cs`（Skill 子模块 `Runtime/Compiled/`）
- `SkillRuntimeNode` / `SkillRuntimeSpecialData` 结构体定义在同一文件内

## 三、第二层：`SkillRuntimeNode` → 具体 `XxxNodeData` 结构体

### 生成代码做什么

生成器扫描所有 `[SkillClipType(id)]` 标记的 Clip 类，读取其 `[SkillCustomData]` 字段，为每个 Clip 生成：

1. **`XxxNodeData` struct**：字段与 Clip 的 `[SkillCustomData]` 字段一一对应
2. **`WriteBoxed` case**：按字段顺序写 BinaryWriter
3. **`ReadBoxed` case**：按字段顺序读 BinaryReader，构造 struct
4. **`IsClipKnown` case**：ClipId 存在性检查

### TryRead 完整流程

```csharp
public static bool TryRead<TData>(SkillDefinition skill, SkillRuntimeNode node, out TData data)
    where TData : struct
{
    // 1. 查缓存
    object cached = skill.GetCachedNodeData(node.NodeId);
    if (cached is TData cachedData) { data = cachedData; return true; }
    if (cached != null) { data = default; return false; }  // 缓存了但类型不对

    // 2. 从 blob 切片
    byte[] blob = skill.NodeDataBlob;
    using MemoryStream stream = new(blob, node.DataOffset, node.DataLength, false);
    using BinaryReader reader = new(stream);

    // 3. 按 ClipId switch 读字段（生成代码的核心）
    object value = ReadBoxed(reader, node.ClipId);
    //   case SkillGeneratedIds.SetVelocityClip:
    //       return new SetVelocityNodeData {
    //           Space = (SkillSpace)reader.ReadInt32(),
    //           Velocity = ReadVector3(reader),
    //           VelocityCurve = ReadCurve(reader)
    //       };

    // 4. 流完整性检查
    if (stream.Position != node.DataLength) { data = default; return false; }

    // 5. 类型检查 + 缓存
    if (!(value is TData typed)) { data = default; return false; }
    skill.SetCachedNodeData(node.NodeId, typed);
    data = typed;
    return true;
}
```

### 生成器支持的字段类型

| 类型 | 读写方式 |
|---|---|
| `int/uint/short/byte/long/float/double/bool/char` | `BinaryWriter.Write` / `BinaryReader.ReadXxx` |
| `string` | `Write(string)` / `ReadString()`，null 写为空串 |
| 枚举 | `Write((int)value)` / `(EnumType)ReadInt32()` |
| `Vector2/3/4` | 逐分量 `float` |
| `Quaternion` | `x/y/z/w` 逐分量 |
| `Color` | `r/g/b/a` 逐分量 |
| `LayerMask` | `Write(value.value)` / `ReadInt32()` |
| `AnimationCurve` | `keys.Length` + 逐 keyframe 的 `time/value/inTangent/outTangent` |
| 一维数组 `T[]` | `count(int)` + 逐元素，null 写为 `-1` |

不支持以上列表外的类型，生成时 `ValidateCustomField` 会报错。

### 生成产物文件

- `Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs`
  - `SkillGeneratedIds` — 所有 ClipId/TrackId/GroupId 常量
  - `XxxNodeData` struct — 每个 Clip 的数据结构
  - `SkillGeneratedNodeDataBlob` — `WriteBoxed` / `TryRead<TData>` / `ReadBoxed` / `IsClipKnown`
  - `SkillGeneratedSpecialDataBlob` — 特殊数据（如伤害组）的读写
  - `SkillGeneratedRuntimeSerialization` — 实现 `ISkillGeneratedRuntimeSerialization`

### 桥接层（asmdef 跨程序集）

```
Skill 核心程序集 (HoshinoSkill asmdef)
  ├─ ISkillGeneratedRuntimeSerialization 接口
  └─ SkillGeneratedSerializationServices.Runtime    ← 反射查找实现

生成代码 (Assembly-CSharp 默认程序集)
  └─ SkillGeneratedRuntimeSerialization : ISkillGeneratedRuntimeSerialization
      └─ 转调 SkillGeneratedNodeDataBlob（实际干活）
```

- `SkillGeneratedSerializationServices.Runtime` 用 `FindImplementation<T>()` 遍历所有程序集，找实现了接口的类，`Activator.CreateInstance` 创建
- **Compiler 和 Editor** 通过桥接调生成代码（它们在 HoshinoSkill asmdef 里，不能直接引用 Assembly-CSharp）
- **运行时 Executor** 直接调 `SkillGeneratedNodeDataBlob.TryRead`（它也在 Assembly-CSharp 里，不需要桥接）

## 四、第三层：Executor 绑定与注册

### `[SkillExecutor(clipId)]` Attribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillExecutorAttribute : Attribute
{
    public uint ClipId { get; }
    // 注意：没有 domain 参数，domain 从继承链推断
}
```

### domain 推断（编译期 + 生成期）

Executor 必须继承 domain 基类：

```
IBattleSkillNodeExecutor
└─ BattleSkillNodeExecutor<TData>           (abstract OnExecute)
   ├─ ClientPredictionSkillExecutor<TData>  (sealed OnExecute → OnStart/OnTick/OnEnd)
   ├─ ClientOnlySkillExecutor<TData>        (同构)
   └─ ServerOnlySkillExecutor<TData>        (同构)
```

生成器 `InferDomain(executorType)` 遍历基类继承链：

```csharp
Type current = executorType;
while (current != null && current != typeof(object))
{
    if (current.IsGenericType)
    {
        Type genericDef = current.GetGenericTypeDefinition();
        if (genericDef == typeof(ClientPredictionSkillExecutor<>))
            return SkillNodeExecutionDomain.ClientPrediction;
        if (genericDef == typeof(ClientOnlySkillExecutor<>))
            return SkillNodeExecutionDomain.ClientOnly;
        if (genericDef == typeof(ServerOnlySkillExecutor<>))
            return SkillNodeExecutionDomain.ServerOnly;
    }
    current = current.BaseType;
}
throw new InvalidOperationException("Executor must inherit from a domain base class.");
```

### 生成产物：Executor 绑定表

`Assets/Scripts/Generated/Battle/Skill/BattleSkillExecutorBindings.cs`：

```csharp
public static class SkillGeneratedExecutorMetas
{
    public struct ExecutorEntry
    {
        public string ExecutorTypeName;       // 全限定类名，供反射创建
        public SkillNodeExecutionDomain Domain;
    }

    private static readonly Dictionary<uint, ExecutorEntry> _entries = new()
    {
        { SkillGeneratedIds.SetVelocityClip, new ExecutorEntry {
            ExecutorTypeName = "Battle.SetVelocityClipExecutor",
            Domain = SkillNodeExecutionDomain.ClientPrediction } },
        // ... 每个 Clip 一个 entry
    };

    public static bool TryGetDomain(uint clipId, out SkillNodeExecutionDomain domain);
    public static bool TryGetName(uint clipId, out string executorTypeName);
    public static bool TryGetEntry(uint clipId, out ExecutorEntry entry);
}
```

### 运行时注册表：`SkillExecutorRegistry`

```csharp
public static class SkillExecutorRegistry
{
    // Init() 在 SkillController.Awake() 显式调用
    // 一次性扫描所有 [SkillExecutor] 标记的类型，Activator.CreateInstance 预创建
    public static void Init();

    // TryGet 是纯字典查找，无运行时反射
    public static bool TryGet(uint clipId, out IBattleSkillNodeExecutor executor);

    // 场景切换/热重载时清空
    public static void Clear();
}
```

**注意**：`SkillExecutorRegistry.Init()` 自己扫描 `[SkillExecutor]` attribute 创建实例，**不依赖** `SkillGeneratedExecutorMetas`。生成产物 `SkillGeneratedExecutorMetas` 主要给 `SkillController` 查 domain 用。两者独立：

- `SkillController` 用 `SkillGeneratedExecutorMetas.TryGetDomain(clipId)` 判断节点属于哪个 domain → 决定是否调度
- `SkillController` 用 `SkillExecutorRegistry.TryGet(clipId)` 拿到 Executor 实例 → 调 `Execute(context)`

## 五、第四层：Executor 执行

### 根基类反序列化 + 派发

```csharp
public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
    where TData : struct
{
    public void Execute(in SkillExecutionContext context)
    {
        // 反序列化节点数据
        if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
        {
            Debug.LogWarning($"Node data mismatch. Expected {typeof(TData).Name} for clip id {context.Node.ClipId}.");
            return;
        }

        OnExecute(context, in data);
    }

    protected abstract void OnExecute(in SkillExecutionContext context, in TData data);
}
```

### domain 基类按 phase 分发

```csharp
public abstract class ClientPredictionSkillExecutor<TData> : BattleSkillNodeExecutor<TData>
{
    protected sealed override void OnExecute(in SkillExecutionContext context, in TData data)
    {
        switch (context.LifecyclePhase)
        {
            case SkillNodeLifecyclePhase.Start: OnStart(context, data); break;
            case SkillNodeLifecyclePhase.Tick:  OnTick(context, data);  break;
            case SkillNodeLifecyclePhase.End:   OnEnd(context, data);   break;
        }
    }

    protected virtual void OnStart(in SkillExecutionContext context, in TData data) { }
    protected virtual void OnTick(in SkillExecutionContext context, in TData data) { }
    protected virtual void OnEnd(in SkillExecutionContext context, in TData data) { }
}
```

`ClientOnlySkillExecutor` 和 `ServerOnlySkillExecutor` 结构完全相同，只是 domain 标签不同。

### 生命周期 phase 由 SkillController 状态跟踪决定

```
每 tick 每 domain:
  isActive = node.IsActiveAt(elapsedTicks)
  wasActive = _activeNodeIds.Contains(nodeId)

  isActive && !wasActive → phase = Start, _activeNodeIds.Add(nodeId)
  isActive && wasActive  → phase = Tick
  !isActive && wasActive → phase = End,   _activeNodeIds.Remove(nodeId)
```

- 技能提前停止（取消/超时）时，对所有 active 节点触发 `phase = End`
- reconcile 时清空所有 HashSet

## 六、SkillController 调度流程

### TickReplicate 统一入口

```csharp
public void TickReplicate(SkillCommand command, Vector3 aimDirection, uint currentTick, ReplicateState state, float delta)
{
    bool canAct = _combatState == null || _combatState.CanAct;
    if (!canAct) return;

    TickClientPrediction(command, aimDirection, currentTick, state, delta);
    if (_player.IsServerStarted)
        TickServerOnly(command, aimDirection, currentTick, state);
    if (_player.IsClientStarted)
        TickClientOnly(command, aimDirection, currentTick, state, delta);
}
```

### 三个域的调度条件

| 域 | 调度条件 | replay tick |
|---|---|---|
| ClientPrediction | 所有身份 | 含 replay（参与回滚） |
| ClientOnly | `IsClientStarted` | 跳过 replay |
| ServerOnly | `IsServerStarted` | 跳过 replay，且 `state.ContainsTicked()` |

### ExecuteNode 构造 context

```csharp
private void ExecuteNode(SkillCommand command, Vector3 aimDirection, uint currentTick,
    int elapsedTicks, float delta, ReplicateState state, SkillRuntimeNode node, SkillNodeLifecyclePhase phase)
{
    if (!SkillExecutorRegistry.TryGet(node.ClipId, out IBattleSkillNodeExecutor executor))
    {
        // 报错 + 返回
        return;
    }

    SkillExecutionContext context = new(
        _player,           // Player（NetworkBehaviour, Owner/TimeManager）
        _player.Motor,     // Motor（位移操作）
        this,              // SkillController
        _combatState,      // CombatState（血量/死亡）
        _attributeSet,     // AttributeSet（属性）
        ResolveServices(), // SkillRuntimeServices（HitResolver 等）
        _activeSkill,      // SkillDefinition
        command,           // SkillCommand
        node,              // SkillRuntimeNode
        aimDirection,      // Vector3
        currentTick,       // uint
        elapsedTicks,      // int
        delta,             // float
        state,             // ReplicateState
        phase);            // SkillNodeLifecyclePhase

    executor.Execute(context);
}
```

## 七、SkillExecutionContext 字段速查

| 字段 | 类型 | 用途 |
|---|---|---|
| `Player` | `Player` (TickNetworkBehaviour) | `Owner`/`TimeManager` 等网络属性 |
| `Motor` | `Motor` (普通 C# 类) | `AddPredictedVelocity`/`AddPredictedDisplacement`/`TeleportPredicted`/`Position`/`Transform` |
| `Controller` | `SkillController` | 技能调度器引用 |
| `CombatState` | `CombatState` | `IsDead`/`Team`/`HitPoints` |
| `AttributeSet` | `AttributeSet` | `ApplyModifier`/`MoveSpeedMultiplier` 等 |
| `Services` | `SkillRuntimeServices` | `HitResolver`（滞后补偿命中查询） |
| `Skill` | `SkillDefinition` | 当前技能定义 |
| `Command` | `SkillCommand` | 当前输入指令（`AimDirection`/`TargetPoint`/`Slot` 等） |
| `Node` | `SkillRuntimeNode` | 当前节点（`ClipId`/`StartTick`/`EndTick`） |
| `AimDirection` | `Vector3` | 瞄准方向 |
| `CurrentTick` | `uint` | 当前网络 tick |
| `ElapsedTicks` | `int` | 技能已过 tick 数 |
| `Delta` | `float` | 本 tick 时长（秒） |
| `ReplicateState` | `ReplicateState` | FishNet 预测状态 |
| `LifecyclePhase` | `SkillNodeLifecyclePhase` | Start/Tick/End |

辅助方法：
- `IsNodeStartTick` → `ElapsedTicks == Node.StartTick`
- `ScaleOutgoingDamage(int)` → 返回非负原始值（缩放归 DamageDispatcher）
- `CreateDamageInfo(amount, type, hitPoint)` → 构造 DamageInfo，自动填 Source/SourceConnection/SourceClipId/Tick
- `GetCurrentPreciseTick()` → 获取 PreciseTick 用于滞后补偿查询

## 八、两个代码生成菜单

### 菜单 1：Skill 序列化代码

```
Tools/Hoshino/Generate Skill Serialization Code
（或 Skill/生成序列化代码）
```

- 生成器：`SkillSerializationCodeGenerator.cs`（Skill 子模块 Editor）
- 扫描所有 `[SkillClipType]` + `[SkillCustomData]` 字段
- 产出：`SkillGeneratedSerialization.cs`（NodeData struct + Blob 读写 + IsClipKnown）
- 公共框架：`SkillCodeGenUtilities.cs`（下游可复用）

### 菜单 2：Battle Executor 绑定

```
Tools/Battle/Generate Skill Executor Bindings
```

- 生成器：`BattleSkillExecutorCodeGenerator.cs`（Battle Editor）
- 扫描所有 `[SkillExecutor]` + 继承链推断 domain
- 产出：`BattleSkillExecutorBindings.cs`（`SkillGeneratedExecutorMetas`，含 domain 表）

### 生成顺序

**先跑菜单 1，再跑菜单 2**。菜单 2 依赖菜单 1 产出的 `SkillGeneratedIds` 常量。

## 九、新增技能节点的标准流程

### 1. 定义 Clip（编辑器侧）

```csharp
[SkillClipType(2001u)]
[Attachable(typeof(SkillActionTrack))]
public sealed class MyDashClip : ActionClip
{
    [SerializeField, HideInInspector] private float _length = 0.5f;
    [SkillCustomData] public float Speed = 10f;
    [SkillCustomData] public Vector3 Direction = Vector3.forward;

    public override float length { get => _length; set => _length = value; }
    public override bool isValid => true;
}
```

### 2. 定义 Executor（运行时侧）

```csharp
[SkillExecutor(SkillGeneratedIds.MyDashClip)]  // ClipId 与 Clip 对应
public sealed class MyDashExecutor : ClientPredictionSkillExecutor<MyDashNodeData>
{
    protected override void OnTick(in SkillExecutionContext context, in MyDashNodeData data)
    {
        Vector3 displacement = SkillUtility.ResolveVector(
            SkillSpace.AimDirection, data.Direction * data.Speed * context.Delta,
            context.Motor.Transform, context.AimDirection);
        context.Motor.AddPredictedDisplacement(displacement);
    }
}
```

### 3. 运行代码生成

```
Tools/Hoshino/Generate Skill Serialization Code    ← 生成 MyDashNodeData + Blob 读写
Tools/Battle/Generate Skill Executor Bindings      ← 生成 Executor 绑定 + domain 表
```

### 4. 编辑技能

1. 打开 SkillEditor，新建 `.skill` 文件
2. 添加 SkillActionTrack，添加 MyDashClip，设置 Speed/Direction/时长
3. 保存

### 5. 编译

```
Tools/Hoshino/Compile All Skill Definitions
```

产出 `Assets/SkillData/Compiled/{SkillName}.bytes`。

### 6. 运行时加载

把 `.bytes` 拖到 `SkillController` 的技能槽（Inspector 里 `SkillSlot.SkillBinary` 字段）。`SkillController.Awake` 时自动 `FromBytes` 缓存。

## 十、在新项目搭建读取层的最小集

### 需要复制的框架文件

从 Skill 子模块复制：
- `Runtime/Compiled/SkillDefinition.cs` — `.bytes` 读写 + 节点缓存
- `Runtime/Generated/SkillSerializationAttributes.cs` — `[SkillClipType]`/`[SkillCustomData]` 等
- `Runtime/Generated/SkillGeneratedSerializationServices.cs` — 桥接接口 + 反射查找
- `Editor/CodeGen/SkillCodeGenUtilities.cs` — 公共代码生成框架
- `Editor/CodeGen/SkillSerializationCodeGenerator.cs` — 序列化代码生成器

从 Battle 复制（如需 Executor 体系）：
- `SkillExecutorAttribute.cs` — `[SkillExecutor(clipId)]`
- `SkillExecutorRegistry.cs` — Init/Clear 注册表
- `SkillNodeLifecyclePhase.cs` — enum Start/Tick/End
- `BattleSkillNodeExecutor.cs` — 根基类
- `ClientPredictionSkillExecutor.cs` / `ClientOnlySkillExecutor.cs` / `ServerOnlySkillExecutor.cs` — domain 基类
- `SkillExecutionContext.cs` — 执行上下文
- `SkillController.cs` — 调度控制器
- `BattleSkillExecutorCodeGenerator.cs` — Executor 绑定生成器

### 生成产物的位置

```
Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs       ← 菜单 1 产出
Assets/Scripts/Generated/Battle/Skill/BattleSkillExecutorBindings.cs        ← 菜单 2 产出
```

生成产物提交进仓库，保证不跑生成器也能编译。改了 Clip/Executor 后重新生成会更新这些文件。

### 最小验证步骤

1. 定义 1 个 Clip + 1 个 Executor
2. 跑两个生成菜单
3. 用 SkillEditor 编辑 `.skill`，编译成 `.bytes`
4. 写测试组件加载 `.bytes`，调 `SkillDefinition.FromBytes`，遍历 Nodes，调 `SkillExecutorRegistry.TryGet` + `executor.Execute(context)`，确认数据正确
5. 参考 `SkillPlaybackTester.cs`（镜像 Controller 调度逻辑的测试组件）
