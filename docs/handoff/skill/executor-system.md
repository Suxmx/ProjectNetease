# Executor 体系

## 基类层级

```
IBattleSkillNodeExecutor
└─ BattleSkillNodeExecutor<TData>           (反序列化 + abstract OnExecute)
   ├─ ClientPredictionSkillExecutor<TData>  (sealed OnExecute → OnStart/OnTick/OnEnd)
   ├─ ClientOnlySkillExecutor<TData>        (同构)
   └─ ServerOnlySkillExecutor<TData>        (同构，End phase 会调 OnTick)
      └─ DamageExecutorBase<TData>          (共享 DoHit 逻辑)
         ├─ SingleDamageClipExecutor
         └─ MultiDamageClipExecutor
```

## 设计约束

- 根基类 `OnExecute` 为 `protected abstract`，domain 基类 `sealed override`——强制 Executor 必须继承 domain 基类
- OnStart/OnTick/OnEnd 都 virtual 空实现，Executor 按需 override
- `BattleSkillNodeExecutor.Execute` 自动从 NodeDataBlob 反序列化 `TData` 传给子类

## Domain 推断

`[SkillExecutor(uint clipId)]` 不带 domain 参数。生成器 `InferDomain(executorType)` 遍历基类继承链，匹配 `ClientPredictionSkillExecutor<>`/`ClientOnlySkillExecutor<>`/`ServerOnlySkillExecutor<>` 的泛型定义，返回对应 domain。未匹配 → 抛错。

## 生命周期回调

| 回调 | 触发时机 | 典型用途 |
|------|----------|----------|
| OnStart | 节点进入 active 区间（isActive && !wasActive） | 初始化、单次伤害 |
| OnTick | active 区间内每 tick（含 StartTick 和 EndTick） | 持续位移、间隔伤害 |
| OnEnd | 节点离开 active 区间（!isActive && wasActive） | 清理、停止位移 |

**ServerOnlySkillExecutor 特殊**：End phase 会先调 OnTick 再调 OnEnd（见 `ServerOnlySkillExecutor.OnExecute`）。这样最后 tick 仍施加效果。ClientPrediction/ClientOnly 的 End phase 只调 OnEnd。

## 内置 Executor 清单

| Executor | Domain | ClipId | 状态 | 说明 |
|----------|--------|--------|------|------|
| MoveVelocityClipExecutor | ClientPrediction | 1001 | 已实装 | 每 tick AddPredictedVelocity，突进结束速度归零即硬停 |
| MoveDisplacementClipExecutor | ClientPrediction | 1002 | 已实装 | 每 tick AddPredictedDisplacement |
| TeleportClipExecutor | ClientPrediction | 1003 | log-only | 真实代码已注释，需恢复 |
| AttributeModifierClipExecutor | ServerOnly | 1006 | log-only | 真实代码已注释，需恢复 |
| SingleDamageClipExecutor | ServerOnly | 1007 | 已实装 | OnStart 调 DoHit 一次 |
| MultiDamageClipExecutor | ServerOnly | 1008 | 已实装 | OnStart + OnTick(按 HitIntervalTicks 间隔) |

## MoveVelocityClipExecutor 旁观者跳过

```csharp
if (!context.Player.IsOwner && !context.Player.IsServerStarted)
    return;
```

原因见 [network-prediction.md](network-prediction.md) 的旁观者特殊处理。

## DamageExecutorBase

封装命中判定 + 伤害组过滤的通用逻辑。`DoHit` 方法接收形状/位置/伤害/组ID参数，构造 `canHit` 回调调 `SkillController.TryConsumeGroupHit`，传给 `LagCompensatedHitResolver`。

详见 [damage-system.md](damage-system.md)。

## 新增 Executor 流程

1. 新建 Executor，继承 domain 基类，打 `[SkillExecutor(clipId)]`
2. 运行 `Tools/Battle/Generate Skill Executor Bindings`
3. 见 [data-and-codegen.md](data-and-codegen.md) 的完整流程

## 关键代码位置

| 代码 | 文件 |
|------|------|
| BattleSkillNodeExecutor (根) | `Battle/Runtime/Skill/Executor/Base/BattleSkillNodeExecutor.cs` |
| ClientPredictionSkillExecutor | `Battle/Runtime/Skill/Executor/Base/ClientPredictionSkillExecutor.cs` |
| ClientOnlySkillExecutor | `Battle/Runtime/Skill/Executor/Base/ClientOnlySkillExecutor.cs` |
| ServerOnlySkillExecutor | `Battle/Runtime/Skill/Executor/Base/ServerOnlySkillExecutor.cs` |
| DamageExecutorBase | `Battle/Runtime/Skill/Executor/ServerOnly/DamageExecutorBase.cs` |
| SkillExecutorAttribute | `Battle/Runtime/Skill/Executor/SkillExecutorAttribute.cs` |
| SkillExecutorRegistry | `Battle/Runtime/Skill/Executor/SkillExecutorRegistry.cs` |
| 生成的绑定表 | `Assets/Scripts/Generated/Battle/Skill/BattleSkillExecutorBindings.cs` |
