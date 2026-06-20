# SkillController 调度

## 统一入口

```csharp
public void TickReplicate(SkillCommand command, Vector3 aim, uint currentTick, ReplicateState state, float delta)
{
    if (!canAct) return;
    TickClientPrediction(...);              // 处理输入 + 调度 ClientPrediction 域
    if (IsServerStarted) TickServerOnly(...);  // 调度 ServerOnly 域
    if (IsClientStarted) TickClientOnly(...);  // 调度 ClientOnly 域
}
```

## ClientPrediction 域 tick 流程

```
TickClientPrediction(command, aim, currentTick, state, delta):
  1. 诊断 log（CMD/TICK）
  2. 处理输入：
     - Press → TryStartSkill(command, currentTick)
     - Release + _isActive → _phase = 2
     - Cancel + _isActive → StopSkill(state)
  3. if !_isActive → return
  4. 算 elapsedTicks = currentTick - _startTick
  5. if elapsedTicks > LengthTicks → StopSkill(state); return
  6. TickDomain(ClientPrediction, _activeClientPredictionNodeIds)
```

## 节点生命周期调度（TickDomain）

对每个节点根据 `isActive` 和 `wasActive` 状态变化触发回调：

```
isActive && !wasActive → 进入区间：OnStart + OnTick
isActive && wasActive  → 区间内：OnTick
!isActive && wasActive → 离开区间：OnTick + OnEnd
!isActive && !wasActive → Skip
```

**关键**：进入和离开区间时都调 OnTick——这样第一 tick 和最后 tick 都施加位移。

## 技能启动（TryStartSkill）

```
TryStartSkill(command, currentTick):
  按 SkillId 或 Slot 查找 SkillDefinition
  _activeSkill = skill
  _activeSequenceId = command.SequenceId
  _startTick = currentTick
  _phase = 1
  _isActive = true
  ClearActiveNodeIds()
  BuildDamageGroups()
```

## 技能停止（StopSkill）

```
StopSkill(state):
  StopAllActiveNodes(state)  ← 按域过滤，见 network-prediction.md
  _activeSkill = null
  _isActive = false
  ClearActiveNodeIds()
  ClearDamageGroups()
```

**超时停止**：`elapsedTicks > LengthTicks` 时自动触发。技能长度 = 编译时取所有节点 EndTick 的最大值。

## 伤害组运行时状态

技能启动时 `BuildDamageGroups()` 扫描 SpecialDatas 中 `DamageGroupData`（id=2001），读出 `GroupId + MaxHitsPerTarget`，存入 `_groupMaxHits`。

`TryConsumeGroupHit(groupId, target)`：groupId=0 直接放行；否则检查该组内该目标已命中次数，超限返回 false。

## 关键代码位置

| 代码 | 文件 |
|------|------|
| SkillController | `Battle/Runtime/Skill/Controller/SkillController.cs` |
| SkillExecutionContext | `Battle/Runtime/Skill/Executor/SkillExecutionContext.cs` |
| SkillRuntimeNode.IsActiveAt | `Skill/Skill/Runtime/Compiled/SkillDefinition.cs` |
