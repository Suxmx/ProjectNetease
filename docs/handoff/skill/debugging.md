# 调试与诊断

## SkillDiagLogger

`Assets/Scripts/Battle/Runtime/Skill/Diag/SkillDiagLogger.cs` — 技能诊断日志器。

- 每次 Play 自动新建带时间戳和进程 ID 的日志文件
- 文件位于 `Application.persistentDataPath`（即 `C:\Users\<user>\AppData\LocalLow\DefaultCompany\NeteaseMiniProject\`）
- 把诊断输出累积到内存缓冲区，由 `SkillDiagFlushDriver` 每帧末尾一次性 flush 到文件，避免 tick 内同步 IO 卡顿
- 启动时 Debug.Log 路径
- 菜单 `Tools/Battle/Open SkillDiag Log Folder` 快速打开文件夹

### 启用诊断

在 NetPlayer prefab 的 `Player` 组件上勾选 `_debugLog` 字段。`SkillController` 和 `Player` 共享这个开关。

### 日志标签

| 标签 | 来源 | 说明 |
|------|------|------|
| `[CMD]` | SkillController.TickClientPrediction | 收到的技能命令（Press/Hold/Release/Cancel） |
| `[START]` | SkillController.TryStartSkill | 技能启动事件 |
| `[TICK]` | SkillController.TickClientPrediction | 技能活跃时的每 tick 状态 |
| `[VEL]` | MoveVelocityClipExecutor.OnTick | 施加的速度（含 curve 状态） |
| `[OWNER]` | Player.PerformReplicate | Owner 技能活跃时 rb 位置变化 |
| `[SPEC]` | Player.PerformReplicate | 旁观者技能活跃时 rb/vis 位置 |
| `[SPEC JUMP]` | Player.PerformReplicate | 旁观者单 tick 跳变 > 0.5f |
| `[RECONCILE]` | Player.PerformReconcile | reconcile 前后位置和 delta |

### RoleOf 角色标签

`SkillDiagLogger.RoleOf(player)` 返回：
- `owner`：IsOwner
- `server`：IsServerStarted && !IsClientStarted
- `spectator`：IsClientStarted && !IsServerStarted
- `host-spectator`：IsServerStarted && IsClientStarted

## 排查拉扯问题

1. 在 NetPlayer prefab 勾选 `_debugLog`
2. 运行游戏复现问题
3. 关闭游戏，打开最新 `SkillDiag_*.log`
4. 看 `[RECONCILE]` 行的 `delta` 非零的 tick —— 这是位置回退
5. 看对应 tick 的 `[VEL]` 是否缺失 —— 如果缺失说明 replay 没施加速度
6. 看 `[RECONCILE]` 的 `skillIsActive` —— 如果是 True 且 replay 跳过 Press，就是节点集合没重建

## 调试绘制（ALINE）

`Assets/Scripts/Skill/Skill/Runtime/Draw/SkillDraw.cs` — 基于 ALINE 的调试绘制工具。

- **命中判定淡出绘制**：`DrawHitBox`/`DrawHitSphere`/`DrawHitRay` 提交请求，`Tick()` 每帧重绘并按 `Time.realtimeSinceStartup` 淡出（默认 0.5s，可配 `fadeDuration`）
- **血量/CD 数字**：`HealthBar(transform, current, max)` / `CooldownBar(transform, currentTick, totalTicks)` — 用 `Draw.ingame.Label2D` 显示文字

### DrawDriver（运行时）

`Assets/Scripts/Battle/Runtime/Skill/Service/DrawDriver.cs` — `[RuntimeInitializeOnLoadMethod]` 自动创建。每帧调 `SkillDraw.Tick()` + 遍历 Player 画血量/CD（用 Visual 子级平滑位置）。

### SkillDrawEditorDriver（编辑器）

`Assets/Scripts/Battle/Editor/Skill/SkillDrawEditorDriver.cs` — `[InitializeOnLoad]`，非 playmode 下通过 `EditorApplication.update` 驱动 `SkillDraw.Tick()`。

## 关键代码位置

| 代码 | 文件 |
|------|------|
| SkillDiagLogger | `Battle/Runtime/Skill/Diag/SkillDiagLogger.cs` |
| SkillDiagFlushDriver | `Battle/Runtime/Skill/Diag/SkillDiagFlushDriver.cs` |
| SkillDraw | `Skill/Skill/Runtime/Draw/SkillDraw.cs` |
| DrawDriver | `Battle/Runtime/Skill/Service/DrawDriver.cs` |
| SkillDrawEditorDriver | `Battle/Editor/Skill/SkillDrawEditorDriver.cs` |
| SkillDiagLogOpener | `Battle/Editor/Skill/SkillDiagLogOpener.cs` |
