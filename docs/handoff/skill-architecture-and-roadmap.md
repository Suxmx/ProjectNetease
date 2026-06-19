# 技能架构与后续路线图

> 最后更新：Player/Motor 重构 + domain 重构 + 伤害中心化 + Core 目录重组之后

## 一、玩家架构

### Player / Motor / SkillController 职责分离

```
Player (TickNetworkBehaviour)          ← FishNet 预测唯一入口
├─ Motor (普通 C# 类, Player 持有)      ← 移动物理
└─ SkillController (MonoBehaviour)      ← 技能调度
```

- **Player**：`[Replicate]`/`[Reconcile]`/Tick 回调的唯一承载者。Inspector 暴露 `_moveSpeed`/`_turnSpeed` 供 Motor 构造。自身不处理移动或技能逻辑，只分发。
- **Motor**：普通 C# 类（`new` 出来），持有 `Rigidbody` + `PredictionRigidbody`。只管移动物理（速度/位移/传送/旋转/Simulate）。技能 Executor 通过 `context.Motor` 调 `AddPredictedVelocity`/`AddPredictedDisplacement`/`TeleportPredicted`。
- **SkillController**：MonoBehaviour（Inspector 配技能槽 `.bytes`）。管技能启动/停止/节点 tick 调度。`TickReplicate` 统一入口，内部按 canAct/IsServer/IsClient 分发到三个域。

### Tick 执行顺序（关键）

```
Player.PerformReplicate(data):
  1. Motor.BeginTick(data)           → 朝向更新 + ResetPredictedModifiers（清零上 tick）
  2. SkillController.TickReplicate() → Executor 在此期间累加本 tick 的预测速度/位移
  3. Motor.EndTick(data, delta)      → 算最终速度（输入 + 技能预测速度）+ Simulate
```

**顺序不能错**：Reset 必须在技能 Executor 累加之前，Simulate 必须在技能 Executor 累加之后。

### Reconcile 分发

```
Player.CreateReconcile():
  MotorReconcileState m = Motor.CaptureState()
  SkillReconcileState s = SkillController.CaptureState(currentTick)
  → PerformReconcile(new ReconcileData(m, s, isDead))

Player.PerformReconcile(data):
  Motor.ApplyState(data.MotorState)
  SkillController.ApplyState(data.SkillState)
```

## 二、Domain 体系

### 三域定义

| Domain | 执行环境 | 调度条件 | 典型用途 |
|---|---|---|---|
| ClientPrediction | 客户端+服务器同步，参与回滚 | 所有身份，含 replay tick | 位移、传送、锁定 |
| ClientOnly | 只在客户端 | `IsClientStarted && !ContainsReplayed()`（含 Host 客户端身份） | 特效、音效、动画 |
| ServerOnly | 只在服务器真实 tick | `IsServerStarted && !ContainsReplayed()` | 伤害、属性修改 |

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

`[BattleSkillExecutor(uint clipId)]` 不再带 domain 参数。生成器 `InferDomain(executorType)` 遍历基类继承链，匹配 `ClientPredictionSkillExecutor<>`/`ClientOnlySkillExecutor<>`/`ServerOnlySkillExecutor<>` 的泛型定义，返回对应 domain。未匹配 → 抛错。

### SkillController 调度流程

```
SkillController.TickReplicate(command, aim, tick, state, delta):
  if !canAct → return
  TickClientPrediction(...)  ← 处理输入 + 调度 ClientPrediction 域节点
  if IsServerStarted → TickServerOnly(...)   ← 调度 ServerOnly 域节点
  if IsClientStarted → TickClientOnly(...)   ← 调度 ClientOnly 域节点
```

每个域内部用状态跟踪 HashSet：
```
isActive && !wasActive → OnStart
isActive && wasActive  → OnTick
!isActive && wasActive → OnEnd
```

## 三、技能数据流与代码生成

### 数据流

```
编辑器 .skill (Slate 层级 Group/Track/Clip)
  → SkillDefinitionCompiler 编译
  → .bytes (二进制 SkillDefinition)
  → 运行时 SkillDefinition.FromBytes 加载
  → SkillController 调度
  → Executor 执行
```

- `.skill`：编辑器源数据，路径 `Assets/SkillData/*.skill`
- `.bytes`：编译产物，运行时权威文件，路径 `Assets/SkillData/Compiled/{SkillName}.bytes`

### 代码生成（两个菜单，都要跑）

1. `Tools/Hoshino/Generate Skill Serialization Code` — 生成序列化代码（NodeData 结构、Blob 读写、IsClipKnown）
2. `Tools/Battle/Generate Skill Executor Bindings` — 生成 Executor 绑定表 + domain 表（从继承链推断 domain）

### 新增技能节点流程

1. 新建 Clip（继承 Slate `ActionClip`），打 `[SkillClipType(id)]`，字段打 `[SkillCustomData]`
2. 新建 Executor，继承 domain 基类（`ClientPredictionSkillExecutor<TData>` 等），打 `[BattleSkillExecutor(id)]`
3. 运行两个生成菜单
4. 保存 `.skill`，编译（`Tools/Hoshino/Compile All Skill Definitions`）
5. 把 `.bytes` 拖到 `SkillController` 的技能槽

## 四、伤害中心化架构

### 链路

```
Executor → LagCompensatedHitResolver.ResolveDamage*(..., sourceClipId)
 → DamageDispatcher.Apply(DamageInfo)
   → ResolveAmount: Amount × outgoing(attacker) × incoming(target)
   → Target.ApplyDamageInternal(final, conn) → 扣血/标记死亡
   → MF.Event.Fire(Source, DamageAppliedEvent)  // 下一帧分发
    → buff/UI/音效/命中反馈等订阅者接收
```

### 核心类型（Damage/ 目录）

- `DamageInfo`：伤害请求（Type/Amount/Source/Target/SourceConnection/SourceClipId/HitPoint/Tick）
- `DamageDispatcher`：统一入口，缩放 + 扣血 + 事件分发
- `IDamageTarget`：伤害目标接口（CombatState + Destructible 实现）
- `DamageAppliedEvent`：MF 事件参数（引用池化），`MF.Event.Fire` 下一帧分发

### 命中反馈

订阅 `DamageAppliedEvent`，过滤 `e.Source == myself`，在目标位置播放命中特效。不进技能节点，通用处理。代价是晚 1 RTT。

## 五、位移与碰撞策略

- 玩家 `Rigidbody` 碰撞 LayerMask 只含**静态墙层**
- 可破坏物、其他玩家**不与玩家刚体物理碰撞**
- 技能位移走 `PredictionRigidbody.Simulate()`，接受静态墙物理的极小回滚风险
- 伤害碰撞独立：`HitResolver.ResolveDamageBox` 的 `layerMask` 查"可受击目标层"，与位移碰撞层无关

## 六、当前 Executor 状态

**所有 5 个 Executor 当前为 log-only 测试态**——`Execute` 方法内只有 `Debug.Log`，实际功能代码已注释。恢复功能时取消注释即可，但要注意：

- CollisionClipExecutor 注释里的 `ResolveDamage*` 调用已带 `context.Node.ClipId` 作为 `sourceClipId` 参数
- 位移类 Executor（MoveVelocity/MoveDisplacement）通过 `context.Motor.AddPredicted*` 施加位移
- Teleport 通过 `context.Motor.TeleportPredicted` 施加传送
- AttributeModifier 通过 `context.AttributeSet.ApplyModifier` 施加属性修改

### 当前内置 Executor

| Executor | Domain | 文件位置 | 功能 |
|---|---|---|---|
| MoveVelocityClipExecutor | ClientPrediction | `Executor/ClientPrediction/` | 每 tick 添加预测速度 |
| MoveDisplacementClipExecutor | ClientPrediction | `Executor/ClientPrediction/` | 每 tick 添加预测位移 |
| TeleportClipExecutor | ClientPrediction | `Executor/ClientPrediction/` | OnStart 传送 |
| CollisionClipExecutor | ServerOnly | `Executor/ServerOnly/` | OnStart 滞后补偿伤害 |
| AttributeModifierClipExecutor | ServerOnly | `Executor/ServerOnly/` | OnStart 属性修改 |

## 七、文件结构（当前实际）

```
Battle/Runtime/
├── Core/
│   ├── Player/
│   │   ├── Player.cs                    ← TickNetworkBehaviour, [Replicate]/[Reconcile]
│   │   ├── Motor.cs                     ← 普通 C# 类, BeginTick/EndTick
│   │   ├── BattlePlayerInput.cs         ← 输入采集（保留 Battle 前缀,避 InputSystem 冲突）
│   │   ├── OwnerCamera.cs
│   │   └── ReconcileData.cs             ← MotorReconcileState + SkillReconcileState
│   ├── Combat/
│   │   ├── CombatState.cs               ← 血量/死亡/队伍, IDamageTarget
│   │   └── AttributeSet.cs             ← 属性集 + TimedModifier
│   ├── Data/
│   │   ├── ETeam.cs
│   │   ├── SkillCommand.cs
│   │   ├── SkillReconcileState.cs
│   │   ├── ReplicateData.cs
│   │   └── MotorReconcileState.cs
│   └── Utility/
│       └── TimeUtility.cs
├── Damage/
│   ├── DamageType.cs
│   ├── DamageInfo.cs
│   ├── IDamageTarget.cs
│   ├── DamageDispatcher.cs
│   └── DamageAppliedEvent.cs
├── Skill/
│   ├── Controller/
│   │   └── SkillController.cs           ← 技能调度, TickReplicate 统一入口
│   ├── Executor/
│   │   ├── SkillExecutorAttribute.cs    ← [BattleSkillExecutor(clipId)]
│   │   ├── IBattleSkillNodeExecutor.cs
│   │   ├── SkillNodeLifecyclePhase.cs   ← enum Start/Tick/End
│   │   ├── SkillExecutionContext.cs     ← Player + Motor + Controller + ...
│   │   ├── SkillUtility.cs             ← 空间换算
│   │   ├── SkillExecutorRegistry.cs     ← Init/Clear 模式, 预扫描缓存
│   │   ├── Base/
│   │   │   ├── BattleSkillNodeExecutor.cs       ← 根基类 (abstract OnExecute)
│   │   │   ├── ClientPredictionSkillExecutor.cs ← domain 基类
│   │   │   ├── ClientOnlySkillExecutor.cs
│   │   │   └── ServerOnlySkillExecutor.cs
│   │   ├── ClientPrediction/
│   │   │   ├── MoveVelocityClipExecutor.cs
│   │   │   ├── MoveDisplacementClipExecutor.cs
│   │   │   └── TeleportClipExecutor.cs
│   │   ├── ClientOnly/                  ← 后续新增 Vfx/Animation Executor
│   │   └── ServerOnly/
│   │       ├── CollisionClipExecutor.cs
│   │       └── AttributeModifierClipExecutor.cs
│   └── Service/
│       ├── SkillRuntimeServices.cs      ← 服务容器 (HitResolver)
│       └── LagCompensatedHitResolver.cs ← 滞后补偿命中解析
├── World/
│   ├── DestructibleObject.cs
│   └── CaptureObjective.cs
Battle/Tests/
└── SkillPlaybackTester.cs               ← PlayMode 测试, 镜像 Controller 调度
Battle/Editor/Skill/
└── BattleSkillExecutorCodeGenerator.cs  ← Executor 绑定生成器
```

## 八、待实现基础设施

| Executor/组件 | Domain | 职责 |
|---|---|---|
| `LockActionExecutor` | ClientPrediction | 设 `Motor.LockedUntilTick`，锁定移动 |
| `PlayAnimationExecutor` | ClientOnly | Animancer 播放动画 |
| `PlayVfxExecutor` | ClientOnly | 特效/音效 |
| `BoxDamageExecutor` | ServerOnly | `HitResolver.ResolveDamageBox` 伤害 |
| `HitFeedbackController` | 客户端组件 | 订阅 `DamageAppliedEvent`，命中反馈 |

Motor 待加：`LockedUntilTick` 字段 + reconcile + Replicate 里检查锁定。

## 九、下一任务：测试基础移动 + 突进斩

### 目标
1. 跑通基础 WASD 移动（无技能，纯 Motor）
2. 用技能编辑器编辑一个突进斩技能，编译，运行验证

### 突进斩蓝图

```
tick 0                              tick 30 (示例)
  |                                   |
  MoveDisplacementClip                |
  (ClientPrediction)                  |
  OnTick: 每 tick 累加位移             |
  
  CollisionClip                       |
  (ServerOnly)                        |
  OnTick: 每 tick box 伤害+Rollback    |
  
  LockActionClip                      |
  (ClientPrediction)                  |
  OnTick: 锁定到 tick 30              |
```

### 实现步骤
1. 恢复 MoveDisplacementClipExecutor 功能（取消注释 `context.Motor.AddPredictedDisplacement`）
2. 恢复 CollisionClipExecutor 功能（取消注释 `HitResolver.ResolveDamageBox`）
3. 新建 LockActionClip + LockActionExecutor（ClientPrediction, OnTick 设 Motor.LockedUntilTick）
4. Motor 加 `LockedUntilTick` 字段 + BeginTick 里检查
5. 用技能编辑器编辑突进斩 `.skill`（MoveDisplacement + Collision + Lock 三个 Clip）
6. `Tools/Hoshino/Generate Skill Serialization Code`
7. `Tools/Battle/Generate Skill Executor Bindings`
8. `Tools/Hoshino/Compile All Skill Definitions`
9. `.bytes` 拖到 SkillController 技能槽
10. PlayMode 联机测试

### 验证点
- WASD 移动正常，预测回滚无 rubber-band
- 突进斩：玩家位移 + 服务器 box 伤害 + 锁定期间不能移动
- Host 模式下三个域都正常执行

## 十、注意事项

- `BattlePlayerInput` 保留 Battle 前缀（避 `UnityEngine.InputSystem.PlayerInput` 冲突）
- Damage/ 和 World/ 下的类仍带 Battle 前缀（`BattleDamageDispatcher` 等），后续可去
- `SkillNodeExecutionDomain` 枚举定义在 Skill 子模块 `SkillDefinition.cs`，值为 `ClientPrediction=0/ClientOnly=1/ServerOnly=2`
- 生成器产物文件提交进仓库，保证不跑生成器也能编译
- `SkillExecutorRegistry.Init()` 在 `SkillController.Awake` 显式调用，避免 tick 中懒初始化卡顿
- `SkillPlaybackTester` 可单独验证技能回放，不经过 Player/Motor，直接调 SkillController
- Prefab 上挂 `Player` 组件（替代旧的 PlayerMotor），移动参数在 Player Inspector 上
