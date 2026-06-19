# 技能架构与后续路线图

## 一、技能架构总览

### 数据流

```
编辑器 .skill (Slate 层级 Group/Track/Clip)
  → SkillDefinitionCompiler 编译
  → .bytes (二进制 SkillDefinition)
  → 运行时 SkillDefinition.FromBytes 加载
  → BattleSkillController 调度
  → Executor 执行
```

- `.skill`：编辑器源数据，保留 Slate 层级，路径 `Assets/SkillData/*.skill`
- `.bytes`：编译产物，运行时权威文件，路径 `Assets/SkillData/Compiled/{SkillName}.bytes`
- Debug JSON：可选辅助查看，不参与运行时

### 代码生成架构（已拆分）

- **Skill 序列化生成器**（`Skill/Editor/CodeGen/SkillSerializationCodeGenerator.cs`）：只生成序列化代码（NodeData 结构、Blob 读写、IsClipKnown），不感知 Battle
- **Battle Executor 绑定生成器**（`Battle/Editor/Skill/BattleSkillExecutorCodeGenerator.cs`）：复用 `SkillCodeGenUtilities` 框架，扫描 `[BattleSkillExecutor]` 生成绑定表 + domain 表
- 公共框架：`SkillCodeGenUtilities`（Skill 子模块），下游可复用快速写自己的生成器

### 类型注册（Attribute 驱动）

- `[SkillClipType(uint id)]`：标记 Clip 类型
- `[SkillCustomData]`：标记 Clip 需要序列化的字段
- `[BattleSkillExecutor(uint clipId)]`：标记 Executor，绑定 ClipId（domain 从继承链推断，不再手打）

### 反射桥接

- `ISkillGeneratedRuntimeSerialization`：Skill 核心通过反射调用生成代码，避免 asmdef 引用方向问题
- `SkillGeneratedSerializationServices`：桥接服务定位器

## 二、Domain 体系

### 三域定义

| Domain | 执行环境 | 调度条件 | 典型用途 |
|---|---|---|---|
| ClientPrediction | 客户端+服务器同步，参与回滚 | 所有身份，含 replay tick | 位移、传送、锁定 |
| ClientOnly | 只在客户端 | `IsClientStarted && !ContainsReplayed()`（含 Host 客户端身份） | 特效、音效、动画 |
| ServerOnly | 只在服务器真实 tick | `IsServerStarted && !ContainsReplayed()` | 伤害、属性修改、生成 |

### 生命周期

所有 domain 基类统一提供：
- `OnStart(context, data)`：节点进入 active 区间时调用一次
- `OnTick(context, data)`：active 区间内每 tick 调用（含 StartTick）
- `OnEnd(context, data)`：节点离开 active 区间时调用一次

OnEnd 通过状态跟踪 HashSet 实现：技能提前停止（取消/超时）时也能触发。

### 基类层级

```
IBattleSkillNodeExecutor
└─ BattleSkillNodeExecutor<TData>           (反序列化 + abstract OnExecute)
   ├─ ClientPredictionSkillExecutor<TData>  (sealed OnExecute → OnStart/OnTick/OnEnd)
   ├─ ClientOnlySkillExecutor<TData>        (同构)
   └─ ServerOnlySkillExecutor<TData>        (同构)
```

- 根基类 `OnExecute` 为 `protected abstract`，domain 基类 `sealed override`——强制 Executor 必须继承 domain 基类
- OnStart/OnTick/OnEnd 都 virtual 空实现，Executor 按需 override

### domain 推断

生成器 `InferDomain(executorType)` 遍历基类继承链，匹配 `ClientPredictionSkillExecutor<>` / `ClientOnlySkillExecutor<>` / `ServerOnlySkillExecutor<>` 的泛型定义，返回对应 domain。未匹配到 domain 基类 → 抛错。

### 调度核心逻辑（状态跟踪）

```
每 tick 每 domain:
  isActive = node.IsActiveAt(elapsedTicks)
  wasActive = _activeNodeIds.Contains(nodeId)
  
  isActive && !wasActive → _activeNodeIds.Add → ExecuteNode(phase=Start)
  isActive && wasActive  → ExecuteNode(phase=Tick)
  !isActive && wasActive → _activeNodeIds.Remove → ExecuteNode(phase=End)
```

- `ApplyState`（reconcile）时清空所有 HashSet
- ClientOnly 跳过 replay tick
- ServerOnly 跳过 replay tick，只在真实 tick 执行

## 三、伤害中心化架构

### 链路

```
Executor → Resolver.ResolveDamage*(..., sourceClipId)
 → BattleDamageDispatcher.Apply(BattleDamageInfo)
  → ResolveAmount: Amount × outgoing(attacker) × incoming(target)
  → Target.ApplyDamageInternal(final, conn) → 扣血/标记死亡
  → MF.Event.Fire(Source, BattleDamageAppliedEvent)  // 下一帧分发
   → buff/UI/音效/命中反馈等订阅者接收
```

### 核心类型

- `BattleDamageInfo`：伤害请求描述（Type/Amount/Source/Target/SourceConnection/SourceClipId/HitPoint/Tick）
- `BattleDamageDispatcher`：统一入口，缩放 + 扣血 + 事件分发
- `IBattleDamageTarget`：伤害目标接口（CombatState + Destructible 实现）
- `BattleDamageAppliedEvent`：MF 事件参数（引用池化），通过 `MF.Event.Fire` 下一帧分发

### 命中反馈

订阅 `BattleDamageAppliedEvent`，过滤 `e.Source == myself`，在目标位置播放命中特效/音效。不进技能节点，通用处理所有来源的命中反馈。代价是晚 1 RTT（网络固有延迟）。

## 四、位移与碰撞策略

### 物理碰撞层收窄

- 玩家 `Rigidbody` 的碰撞 LayerMask 只含**静态墙层**（地形/建筑/永久障碍）
- 可破坏物、其他玩家**不与玩家刚体物理碰撞**（不挡位移）
- 动态障碍交互通过伤害节点（ServerOnly）处理，不走物理碰撞

### 回滚风险

- 技能位移走 `PredictionRigidbody.Simulate()`，接受静态墙物理的极小回滚风险
- 静态墙客户端服务器一致，浮点差异极小
- 可破坏物/玩家不碰撞，消除主要回滚源

### 伤害碰撞独立

`HitResolver.ResolveDamageBox` 的 `layerMask` 参数查"可受击目标层"（玩家+可破坏物），与位移碰撞层无关。

## 五、三个技能实现蓝图

### 技能 1：传送

```
tick 0                tick 5
  |                      |
  TeleportClip           |
  (ClientPrediction)     |
  OnStart: 传送位置      |
  
  TeleportVfxClip        |
  (ClientOnly)           |
  OnStart: 特效+音效     |
  
  LockActionClip         |
  (ClientPrediction)     |
  OnTick: 锁定到 tick 5  |
```

无 ServerOnly 节点——传送无伤害，纯位置+表现。位置进 reconcile 刚体状态，服务器确认或拉回。

### 技能 2：突进斩

```
tick 0                              tick 30
  |                                   |
  DashMoveClip                        |
  (ClientPrediction)                  |
  OnTick: 每 tick 累加位移             |
  
  DashDamageClip                      |
  (ServerOnly)                        |
  OnTick: 每 tick box 伤害+Rollback    |
  
  DashVfxClip                         |
  (ClientOnly)                        |
  OnStart: 突进起手特效                |
  OnTick: 拖尾粒子(可选)               |
  OnEnd: 收尾特效                     |
  
  LockActionClip                      |
  (ClientPrediction)                  |
  OnTick: 锁定到 tick 30              |
```

位移和伤害分两个节点：位移是预测的（客户端可见即时位移），伤害是权威的（服务器判定）。攻击者 box 中心用服务器 Motor 位置，目标用 Rollback 回滚。

### 技能 3：平砍

```
tick 0           tick 10(前摇结束)        tick 25(后摇结束)
  |                 |                        |
  |                 SlashDamageClip          |
  |                 (ServerOnly)             |
  |                 OnStart: 一次性box伤害    |
  |                                          |
  SwingVfxClip                                |
  (ClientOnly)                                |
  OnStart: 挥砍音效+特效                       |
  |                                           |
  PlayAnimationClip                           |
  (ClientOnly)                                |
  OnStart: 播放挥砍动画(时长=25 tick)          |
  |                                           |
  LockActionClip                              |
  (ClientPrediction)                          |
  OnTick: 锁定到 tick 25                       |
```

伤害节点 StartTick = 前摇结束时刻，不是技能开始。前摇期间玩家锁定不能移动。动画从 tick 0 播放，时长覆盖前后摇。

## 六、通用基础设施待实现

| Executor/组件 | Domain | 生命周期 | 职责 |
|---|---|---|---|
| `LockActionExecutor` | ClientPrediction | OnTick | 设 `Motor.LockedUntilTick = node.EndTick`，锁定移动/新技能 |
| `PlayAnimationExecutor` | ClientOnly | OnStart | 调 Animancer 播放指定动画 clip |
| `PlayVfxExecutor` | ClientOnly | OnStart/OnTick | 播放特效/音效 |
| `BoxDamageExecutor` | ServerOnly | OnStart 或 OnTick | 调 `HitResolver.ResolveDamageBox` 做滞后补偿伤害 |
| `BattleHitFeedbackController` | 客户端组件 | — | 订阅 `BattleDamageAppliedEvent`，播放命中反馈 |

Motor 改动：
- 新增 `LockedUntilTick` 字段，进 reconcile 状态
- Replicate 里检查锁定状态决定是否处理移动输入

## 七、文件结构

```
Battle/Runtime/Skill/
├── Controller/
│   └── BattleSkillController.cs
├── Executor/
│   ├── BattleSkillExecutorAttribute.cs      ← [BattleSkillExecutor(clipId)]
│   ├── IBattleSkillNodeExecutor.cs
│   ├── BattleSkillNodeExecutor.cs           ← 根基类（abstract OnExecute）
│   ├── BattleSkillNodeExecutorRegistry.cs
│   ├── BattleSkillNodeLifecyclePhase.cs     ← enum Start/Tick/End
│   ├── BattleSkillExecutionContext.cs
│   ├── BattleSkillNodeUtility.cs
│   ├── ClientPredictionSkillExecutor.cs     ← domain 基类
│   ├── ClientOnlySkillExecutor.cs
│   ├── ServerOnlySkillExecutor.cs
│   ├── ClientPrediction/
│   │   ├── MoveVelocityClipExecutor.cs
│   │   ├── MoveDisplacementClipExecutor.cs
│   │   └── TeleportClipExecutor.cs
│   ├── ClientOnly/                          ← 后续新增 Vfx/Animation Executor
│   └── ServerOnly/
│       ├── CollisionClipExecutor.cs
│       └── AttributeModifierClipExecutor.cs
└── Service/
    ├── BattleSkillRuntimeServices.cs
    └── BattleLagCompensatedHitResolver.cs
```

新增 Executor 时按其继承的 domain 基类放对应文件夹。
