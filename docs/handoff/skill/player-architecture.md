# 玩家架构：Player / Motor / SkillController

## 组合结构

```
NetPlayer (Prefab, Layer 6=Player)
├─ Player (TickNetworkBehaviour)          ← FishNet 预测唯一入口
│  ├─ Motor (普通 C# 类, Player 持有)      ← 移动物理
│  ├─ SkillController (MonoBehaviour)      ← 技能调度
│  ├─ BattlePlayerInput (MonoBehaviour)    ← 输入采集
│  ├─ CombatState (NetworkBehaviour)       ← 血量/死亡/队伍
│  ├─ AttributeSet (TickNetworkBehaviour)  ← 属性集 + TimedModifier
│  └─ OwnerCamera (NetworkBehaviour)       ← 本地玩家相机
├─ Presentation                            ← NetworkTickSmoother 挂在这
│  └─ Visual                               ← TickSmoother 的 graphicalObject
│     ├─ ForwardMarker
│     └─ CameraTarget                      ← Cinemachine Follow/LookAt
└─ Owner Cinemachine Camera                ← 仅拥有者激活
```

## 职责分离

| 组件 | 类型 | 职责 | 不做什么 |
|------|------|------|----------|
| Player | TickNetworkBehaviour | `[Replicate]`/`[Reconcile]` 入口，分发到子组件 | 不处理移动或技能逻辑 |
| Motor | 普通 C# 类（`new`） | 移动物理（速度/位移/传送/旋转/Simulate） | 不感知技能调度和网络生命周期 |
| SkillController | MonoBehaviour | 技能启动/停止/节点 tick 调度/伤害组 | 不直接处理移动物理 |
| BattlePlayerInput | 非网络组件 | Update 采集按键事件，tick 消费 | 不在网络回调里读按键 |

## Tick 执行顺序（关键）

```
Player.PerformReplicate(data, state):
  1. Motor.BeginTick(data)           → 朝向更新 + ResetPredictedModifiers（清零上 tick）
  2. SkillController.TickReplicate() → Executor 在此期间累加本 tick 的预测速度/位移
  3. Motor.EndTick(data, delta)      → 算最终速度（输入 + 技能预测速度）+ Simulate
```

**顺序不能错**：Reset 必须在技能 Executor 累加之前，Simulate 必须在技能 Executor 累加之后。

## 输入架构（Update 采集 + Tick 消费）

```
Update()（Unity 帧回调）:
  1. 缓存瞄准方向 _cachedAim（鼠标 Raycast 到地面）
  2. 遍历 6 个技能槽，读 wasPressedThisFrame/wasReleasedThisFrame
  3. 按下 → _heldSlots[slot]=true, _pendingCommand = Press
  4. 松开 → _heldSlots[slot]=false, _pendingCommand = Release

TimeManager_OnTick()（FishNet tick 回调）:
  1. PerformReplicate(BuildReplicateData())
  2. BuildReplicateData: ReadMove() + GetCachedAim() + ConsumeSkillCommand(tick)
  3. ConsumeSkillCommand: 返回并清空 _pendingCommand；无事件时检查 _heldSlots 生成 Hold
```

**为什么不在 tick 里直接读按键**：`wasPressedThisFrame` 是 Unity InputSystem 在 Update 帧设置的状态，只在下一次 Update 之前有效。FishNet tick（60/s）和 Unity Update 不同步——tick 里读会丢失按键事件或重复读取。

## Reconcile 分发

```
Player.CreateReconcile():
  MotorReconcileState m = Motor.CaptureState()
  SkillReconcileState s = SkillController.CaptureState(currentTick)
  → PerformReconcile(new ReconcileData(m, s, isDead))

Player.PerformReconcile(data):
  Motor.ApplyState(data.MotorState)
  SkillController.ApplyState(data.SkillState, data.GetTick())  ← dataTick 用于重建节点集合
```

## 关键代码位置

| 代码 | 文件 |
|------|------|
| Player 类 | `Assets/Scripts/Battle/Runtime/Core/Player/Player.cs` |
| Motor 类 | `Assets/Scripts/Battle/Runtime/Core/Player/Motor.cs` |
| BattlePlayerInput | `Assets/Scripts/Battle/Runtime/Core/Player/BattlePlayerInput.cs` |
| ReplicateData | `Assets/Scripts/Battle/Runtime/Core/Data/ReplicateData.cs` |
| ReconcileData | `Assets/Scripts/Battle/Runtime/Core/Player/ReconcileData.cs` |
| MotorReconcileState | `Assets/Scripts/Battle/Runtime/Core/Data/MotorReconcileState.cs` |
| SkillReconcileState | `Assets/Scripts/Battle/Runtime/Core/Data/SkillReconcileState.cs` |

## Prefab 配置

```
NetworkObject: _enablePrediction=1, _predictionType=1(Rigidbody),
  _graphicalObject=Visual, _enableStateForwarding=1
Rigidbody: UseGravity=false, Constraints=FreezeRotationXZ
CapsuleCollider: radius=0.35, height=1.8
Player: _moveSpeed=6, _turnSpeed=720
SkillController: _skillSlots[0]={Slot=0, SkillBinary=DashAttack.bytes}
CombatState: _maxHitPoints=100
NetworkTickSmoother: TargetTransform=root, DetachOnStart=1
```

## Motor API（Executor 通过 context.Motor 调用）

| 方法 | 说明 |
|------|------|
| `AddPredictedVelocity(Vector3)` | 累加预测速度（不乘 delta，速度直接累加） |
| `AddPredictedDisplacement(Vector3)` | 累加预测位移 |
| `TeleportPredicted(Vector3)` | 设置待执行的传送位置 |
| `Position` / `Transform` / `AimDirection` | 只读属性 |

Motor 内部用 `PredictionRigidbody.Simulate()` 推进物理，接受静态墙物理的极小回滚风险。
