# 节点生命周期与预测回滚分析

## 三域在 replay 中的执行策略

| Domain | replay 中执行？ | 过滤条件 | 调度入口 |
|--------|-----------------|----------|----------|
| ClientPrediction | ✅ 执行 | 无过滤 | `TickClientPrediction`（所有身份都调） |
| ClientOnly | ❌ 跳过 | `state.ContainsReplayed()` → return | `TickClientOnly`（仅 `IsClientStarted`） |
| ServerOnly | ❌ 跳过 | `!ContainsTicked() \|\| ContainsReplayed()` → return | `TickServerOnly`（仅 `IsServerStarted`） |

**结论：replay 期间只有 ClientPrediction 域的节点重放。**

## Reconcile 后节点集合重建

`ApplyState(state, dataTick)` 用 `dataTick - _startTick` 算 elapsed，调 `RebuildActiveNodeIds(elapsed)` 重建**三个域**的节点集合。

重建三个域的目的：
- **ClientPrediction**：replay 中需要正确的 `wasActive` 来走 Tick/End 分支
- **ClientOnly**：replay 中跳过，但真实 tick 恢复后需要正确的 `wasActive` 避免误触发 OnStart
- **ServerOnly**：客户端不调度此域，但 Host（IsServerStarted && IsClientStarted）的客户端身份收到 reconcile 后，服务器身份的真实 tick 需要正确的 `wasActive`

## 逐场景分析

### ClientPrediction 域

#### 场景1：reconcile 时节点 active，replay 中继续 active

```
ApplyState: IsActiveAt(elapsed)=true → wasActive=true
replay tick: isActive=true, wasActive=true → OnTick ✓
```
正确，不重复 OnStart。

#### 场景2：reconcile 时节点 active，replay 中离开区间

```
ApplyState: wasActive=true
replay tick (elapsed=EndTick): isActive=false, wasActive=true → OnTick+OnEnd ✓
```
正确，正常触发 OnEnd。

#### 场景3：reconcile 时节点未进入，replay 中进入区间

```
ApplyState: IsActiveAt(elapsed)=false → wasActive=false
replay tick (elapsed=StartTick): isActive=true, wasActive=false → OnStart+OnTick ✓
```
正确。**前提**：技能已启动（`_isActive=true`）。如果 reconcile 的 dataTick < Press tick，replay 包含 Press → `TryStartSkill` 重启技能 → `ClearActiveNodeIds` → 走正常进入流程。

#### 场景4：reconcile 时技能已超时

```
ApplyState: _isActive=true, elapsed > LengthTicks
replay 第一个 tick: elapsedTicks > LengthTicks → StopSkill(state)
  → StopAllActiveNodes: 对 ClientPrediction 调 End（对集合中的节点）
  → ClearActiveNodeIds
```
正确。如果节点不在集合中（IsActiveAt(elapsed)=false），StopDomainNodes 不会调 End。

**ClientPrediction 结论：无问题。**

### ClientOnly 域

#### 场景5：reconcile 时节点 active，replay 中跳过，真实 tick 恢复

```
ApplyState: wasActive=true
replay tick: 跳过（ContainsReplayed）
真实 tick: isActive=true, wasActive=true → OnTick ✓（不重新 OnStart）
```
正确。

#### 场景6：reconcile 时节点未进入，replay 中应进入但跳过，真实 tick 恢复

```
ApplyState: wasActive=false
replay tick (应 OnStart): 跳过
真实 tick: isActive=true, wasActive=false → OnStart+OnTick
```
**OnStart 延迟到真实 tick 触发**。特效/动画会晚几个 tick 播放。对纯表现节点可接受。

#### 场景7：节点整个 active 区间在 replay 范围内

```
ApplyState: wasActive=false（reconcile 时未进入）
replay 中应 OnStart→OnTick→OnEnd：全部跳过
真实 tick: isActive=false, wasActive=false → Skip
```
**节点完全没执行**。OnStart/OnTick/OnEnd 都没触发。

影响：特效没播放、动画没播放。但 replay 期间的表现本就不需要展示给玩家看（replay 是内部校正过程，瞬间完成）。真实 tick 恢复时节点已结束，不播放是正确的。

#### 场景8：技能在 replay 中超时停止，ClientOnly 节点未触发 OnEnd

```
replay tick: elapsed > LengthTicks → StopSkill(state)
  → StopAllActiveNodes: ClientOnly 被 ContainsReplayed 过滤跳过 → 不调 OnEnd
  → ClearActiveNodeIds
真实 tick: _isActive=false → TickClientOnly 直接 return
```
**OnEnd 未触发**。

影响：如果 ClientOnly 节点的 OnEnd 做清理（如停止特效），清理没做。但：
- replay 中 OnStart 也跳过了（特效没播放），所以不需要停止
- StopSkill 清空了节点集合，真实 tick 不会重新触发

**潜在风险**：如果未来 ClientOnly 节点的 OnEnd 有非幂等的清理逻辑（如移除一个全局 buff 视觉），可能出问题。目前无此场景。

**ClientOnly 结论：当前无问题，但 OnEnd 在 replay 超时停止时不触发，未来需注意。**

### ServerOnly 域

#### 场景9：客户端不调度 ServerOnly

```
TickReplicate: if (_player.IsServerStarted) TickServerOnly(...)
```
纯客户端（spectator/owner）`IsServerStarted=false`，根本不调 `TickServerOnly`。ServerOnly 节点在客户端完全不执行。

`RebuildActiveNodeIds` 重建 `_activeServerOnlyNodeIds` 对纯客户端无用，但无害（不会被调度）。

#### 场景10：Host 的 ServerOnly

Host（IsServerStarted && IsClientStarted）：
- 服务器身份：不跑 replay，每个 tick 都是真实 tick，ServerOnly 正常执行
- 客户端身份：收到 reconcile → ApplyState → 重建 _activeServerOnlyNodeIds
  - 但 TickServerOnly 在 `TickReplicate` 中只调一次（`if IsServerStarted`），用服务器身份的 state
  - Host 的服务器身份不跑 replay，所以 ServerOnly 不受 replay 影响

`StopAllActiveNodes` 中 ServerOnly 的过滤：`if (_player.IsServerStarted && !state.ContainsReplayed())`。Host 在 replay 中 `ContainsReplayed=true` → 跳过。真实 tick 恢复后如果 StopSkill → 调 End。但 Host 的服务器身份不跑 replay，所以这个分支对 Host 也不触发。

**ServerOnly 结论：无问题。**

## 总结

| Domain | replay 中 | 真实 tick 恢复后 | 问题？ |
|--------|-----------|------------------|--------|
| ClientPrediction | 正常重放 OnStart/OnTick/OnEnd | 状态连续 | ✅ 无问题 |
| ClientOnly | 跳过 | wasActive 正确→不误触发 OnStart；wasActive=false→OnStart 延迟 | ⚠️ OnEnd 在 replay 超时时可能不触发 |
| ServerOnly | 客户端不调度；Host 服务器身份不跑 replay | 服务器正常执行 | ✅ 无问题 |

## 唯一潜在风险

**ClientOnly 节点在 replay 中超时停止时不触发 OnEnd**。

当前无实际影响（ClientOnly 节点未实装，且 replay 中 OnStart 也跳过）。如果未来 ClientOnly 节点的 OnEnd 有非幂等的清理逻辑，需要处理。

**解决方案（如果未来需要）**：在 `StopAllActiveNodes` 中对 ClientOnly 的过滤改为 `if (_player.IsClientStarted)`（去掉 `!ContainsReplayed`），让 replay 超时停止时也触发 ClientOnly 的 OnEnd。但这会导致 replay 中调 OnEnd 而 OnStart 没调过（因为 replay 跳过了 OnStart），需要 Executor 内部做防御。
