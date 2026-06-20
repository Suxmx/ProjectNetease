# 技能系统文档索引

> 本目录是技能系统的架构沉淀，帮助快速上手。每个文档聚焦一个模块，可独立阅读。

## 快速导航

| 文档 | 内容 | 优先阅读 |
|------|------|----------|
| [player-architecture.md](player-architecture.md) | Player/Motor/SkillController 职责分离、Tick 执行顺序、输入架构、Prefab 配置 | ★ 第一个 |
| [network-prediction.md](network-prediction.md) | FishNet 预测回滚机制、Reconcile 数据流、Replay、reconcile 后节点集合重建 | ★ 第二个 |
| [skill-controller.md](skill-controller.md) | SkillController 调度流程、三域 tick、节点生命周期、技能停止 | ★ 第三个 |
| [executor-system.md](executor-system.md) | Executor 基类层级、Domain 推断、生命周期回调、内置 Executor 清单 | 按需 |
| [damage-system.md](damage-system.md) | 伤害链路、DamageDispatcher、伤害组、命中反馈、滞后补偿 | 按需 |
| [data-and-codegen.md](data-and-codegen.md) | 数据流（.skill→.bytes→运行时）、代码生成菜单、序列化、新增节点流程 | 按需 |
| [skill-editor.md](skill-editor.md) | 自研 SkillEditor、Tick 模式、数据黑板、Clip 预览、默认 Actor | 按需 |
| [debugging.md](debugging.md) | SkillDiagLogger 诊断日志、调试绘制（ALINE）、常见问题排查 | 排查时 |
| [roadmap.md](roadmap.md) | 待实现基础设施、ID 分配、注意事项、当前 Executor 状态 | 规划时 |

## 系统概览

```
编辑器 .skill (Slate 层级 + 数据黑板)
  → SkillDefinitionCompiler 编译 → .bytes
  → 运行时 SkillDefinition.FromBytes
  → SkillController 调度（三域：ClientPrediction/ClientOnly/ServerOnly）
  → Executor 执行（位移/伤害/属性/特效）
  → Motor 物理推进 / DamageDispatcher 伤害分发
```

## 核心概念速查

- **三域**：ClientPrediction（预测回滚）、ClientOnly（纯表现）、ServerOnly（服务器权威）
- **节点生命周期**：OnStart → OnTick → OnEnd，由 `IsActiveAt(elapsedTicks)` 和 `wasActive` 状态跟踪驱动
- **Reconcile**：FishNet 的状态校正机制，`ApplyState` 用 `dataTick` 重建节点集合，避免 replay 跳过 Press 导致的拉扯
- **Tick 顺序**：`Motor.BeginTick` → `SkillController.TickReplicate` → `Motor.EndTick`（Simulate）

## 相关文档

- [roadmap.md](roadmap.md) — 待实现基础设施、ID 分配、文件结构、注意事项
