# 网络预测与回滚

## FishNet 预测基础

本项目用 FishNet 的 CSP（Client-Side Prediction）+ Reconcile 机制。核心概念：

- **Replicate**：客户端采集输入，本地预测执行；服务器收到相同输入执行权威结果
- **Reconcile**：服务器定期发回权威状态，客户端校正本地状态并 replay 未确认的 tick
- **Replay**：reconcile 后，客户端从 `ClientStateTick+1` 到 `LocalTick` 之间重放每个 tick 的 Replicate

## ReplicateState 标志位

| 标志 | 含义 | 谁用 |
|------|------|------|
| `Ticked` | 数据在 OnTick 中正常运行（非 reconcile） | 服务器+客户端 |
| `Replayed` | 数据在 reconcile 的 replay 中运行 | 仅客户端 |
| `Created` | 数据由服务器或客户端有意创建 | 服务器+客户端 |

判断方法：`state.ContainsReplayed()`、`state.ContainsTicked()`。

## 三域在预测中的行为

| Domain | 何时执行 | replay 中执行？ |
|--------|----------|-----------------|
| ClientPrediction | 所有身份，含 replay tick | ✅ 是 |
| ClientOnly | `IsClientStarted && !ContainsReplayed()` | ❌ 跳过 |
| ServerOnly | `IsServerStarted && !ContainsTicked() \|\| ContainsReplayed()` → 跳过 | ❌ 跳过 |

## Reconcile 数据流

```
Player.CreateReconcile()  [PostTick]
  → Motor.CaptureState()           → MotorReconcileState (PredictionRigidbody + AimDirection)
  → SkillController.CaptureState() → SkillReconcileState (ActiveSkillId/StartTick/Phase/IsActive)
  → PerformReconcile(ReconcileData)

Player.PerformReconcile(data)  [服务器发的 Reliable 或本地 history]
  → Motor.ApplyState(data.MotorState)            → PredictionRigidbody.Reconcile
  → SkillController.ApplyState(state, dataTick)  → 重建节点集合
```

## 关键：reconcile 后节点集合重建

**问题背景**：FishNet replay 从 `ClientStateTick+1` 开始，会跳过技能启动 tick（Press 命令所在 tick）。如果 `ApplyState` 只恢复 `_isActive/_startTick` 但不恢复 `_activeClientPredictionNodeIds`，replay 中所有节点 `wasActive=false` + `isActive=false` → 全部 Skip，MoveVelocity 不施加速度，导致拉扯。

**解决方案**：`ApplyState` 用 `dataTick - _startTick` 算出正确的 elapsedTicks，调 `RebuildActiveNodeIds(elapsedTicks)` 重建三个域的节点集合：

```csharp
public void ApplyState(SkillReconcileState state, uint dataTick)
{
    // ... 设置 _isActive/_startTick/_phase ...
    if (!_isActive) { ClearActiveNodeIds(); return; }
    int elapsedTicks = dataTick >= _startTick ? (int)(dataTick - _startTick) : 0;
    RebuildActiveNodeIds(elapsedTicks);
}
```

**为什么 dataTick 和 _startTick 同时间线**：
- owner：`dataTick = ClientStateTick`，和 `command.InputTick`（`_startTick`）同时间线
- spectator：`dataTick = ServerStateTick`，也是服务器处理该 replicate 时的 owner tick 编号

## 时间线混淆陷阱

**不要在 `CaptureState` 里用 `LocalTick` 算 elapsed**：host 作为服务器，`LocalTick` 跑在服务器时间线上，比 owner tick 快约 50。`_startTick` 是 owner tick，`LocalTick - _startTick` 会算出错误的 elapsed（如 50 而非 0）。

旧代码的 `SkillReconcileState.ElapsedTicks` 字段因此删除——改由 `ApplyState` 用 `dataTick` 现算。

## StopSkill 的域过滤

`StopSkill` 在 replay 中也会被调用（如技能超时）。`StopAllActiveNodes` 必须按各域的执行条件过滤，否则会在客户端 replay 中对 ServerOnly 节点调 End → DoHit → "server is not active" 错误：

```csharp
private void StopAllActiveNodes(ReplicateState state)
{
    // ClientPrediction：无过滤
    StopDomainNodes(..., _activeClientPredictionNodeIds);
    // ClientOnly：IsClientStarted && !Replayed
    if (_player.IsClientStarted && !state.ContainsReplayed())
        StopDomainNodes(..., _activeClientOnlyNodeIds);
    // ServerOnly：IsServerStarted && !Replayed
    if (_player.IsServerStarted && !state.ContainsReplayed())
        StopDomainNodes(..., _activeServerOnlyNodeIds);
}
```

## 旁观者（Spectator）特殊处理

纯 Client（非 Owner 非 Server）的 `MoveVelocityClipExecutor.OnTick` 直接 return：

```csharp
if (!context.Player.IsOwner && !context.Player.IsServerStarted)
    return;
```

原因：FishNet 的 state forwarding + Appended state order 会让 spectator 先 replay 预跑多个 tick 导致视觉瞬跳。旁观者改由服务器 reconcile 传播 + TickSmoother 插值。

Host 旁观（`IsServerStarted`）不跳过——Host 是服务器权威直接跑，无 replay 问题。

## 关键代码位置

| 代码 | 文件 |
|------|------|
| PerformReplicate | `Battle/Runtime/Core/Player/Player.cs` |
| PerformReconcile | `Battle/Runtime/Core/Player/Player.cs` |
| CaptureState / ApplyState / RebuildActiveNodeIds | `Battle/Runtime/Skill/Controller/SkillController.cs` |
| SkillReconcileState | `Battle/Runtime/Core/Data/SkillReconcileState.cs` |
| FishNet ReplicateState | `FishNet/Runtime/Object/Prediction/ReplicateState.cs` |
| FishNet PredictionManager | `FishNet/Runtime/Managing/Prediction/PredictionManager.cs` |

## 排查拉扯问题的方法

见 [debugging.md](debugging.md) 的 SkillDiagLogger 部分。
