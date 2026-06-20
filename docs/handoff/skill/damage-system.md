# 伤害系统

## 伤害链路

```
DamageExecutorBase.DoHit
  → LagCompensatedHitResolver.ResolveDamage{Box/Sphere/Ray}(..., canHit callback)
    → canHit: SkillController.TryConsumeGroupHit(groupId, target) — 伤害组命中次数过滤
    → BattleDamageDispatcher.Apply(BattleDamageInfo)
      → ResolveAmount: Amount × outgoing(attacker) × incoming(target)
      → Target.ApplyDamageInternal(final, conn) → 扣血/标记死亡
      → MF.Event.Fire(Source, DamageAppliedEvent)  // 下一帧分发
        → buff/UI/音效/命中反馈等订阅者接收
```

## 核心类型

| 类型 | 文件 | 说明 |
|------|------|------|
| BattleDamageInfo | `Battle/Runtime/Damage/BattleDamageInfo.cs` | 伤害请求（Type/Amount/Source/Target/SourceConnection/SourceClipId/HitPoint/Tick） |
| BattleDamageDispatcher | `Battle/Runtime/Damage/BattleDamageDispatcher.cs` | 统一入口，缩放 + 扣血 + 事件分发 |
| IBattleDamageTarget | `Battle/Runtime/Damage/IBattleDamageTarget.cs` | 伤害目标接口（CombatState + BattleDestructibleObject 实现） |
| BattleDamageAppliedEvent | `Battle/Runtime/Damage/BattleDamageAppliedEvent.cs` | MF 事件参数（引用池化），下一帧分发 |
| LagCompensatedHitResolver | `Battle/Runtime/Skill/Service/LagCompensatedHitResolver.cs` | 滞后补偿命中解析 + canHit 回调 |

## 伤害组

- **DamageGroupData**（SpecialData, id=2001）：定义 `GroupId`（byte）+ `MaxHitsPerTarget`（byte）
- 技能启动时 `SkillController.BuildDamageGroups()` 扫描 SpecialDatas 建组配置
- `SingleDamageClip`/`MultiDamageClip` 通过 `DamageGroupId` 字段绑定到组
- `DamageExecutorBase.DoHit` 构造 `canHit` 回调，调 `TryConsumeGroupHit` 做命中次数限制
- `LagCompensatedHitResolver` 三个 Resolve 方法都支持 `Func<IBattleDamageTarget, bool> canHit` 可选参数

## 命中反馈

订阅 `BattleDamageAppliedEvent`，过滤 `e.Source == myself`，在目标位置播放命中特效。不进技能节点，通用处理。代价是晚 1 RTT。

## 位移与碰撞策略

- 玩家 `Rigidbody` 碰撞 LayerMask 只含**静态墙层**（Layer 7 `StaticWall`）
- 可破坏物、其他玩家**不与玩家刚体物理碰撞**（Player×Player 碰撞矩阵当前仍开着，需关闭）
- 技能位移走 `PredictionRigidbody.Simulate()`，接受静态墙物理的极小回滚风险
- 伤害碰撞独立：`HitResolver.ResolveDamageBox` 的 `layerMask` 查"可受击目标层"，与位移碰撞层无关

## 关键代码位置

| 代码 | 文件 |
|------|------|
| DamageExecutorBase | `Battle/Runtime/Skill/Executor/ServerOnly/DamageExecutorBase.cs` |
| SingleDamageClipExecutor | `Battle/Runtime/Skill/Executor/ServerOnly/SingleDamageClipExecutor.cs` |
| MultiDamageClipExecutor | `Battle/Runtime/Skill/Executor/ServerOnly/MultiDamageClipExecutor.cs` |
| DamageGroupData | `Skill/Skill/Runtime/SpecialData/DamageGroupData.cs` |
| LagCompensatedHitResolver | `Battle/Runtime/Skill/Service/LagCompensatedHitResolver.cs` |
| SkillRuntimeServices | `Battle/Runtime/Skill/Service/SkillRuntimeServices.cs` |
