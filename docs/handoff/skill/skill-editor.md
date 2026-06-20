# 技能编辑器

## 入口

`Window/Hoshino/Skill Editor`（自研 EditorWindow，非 Slate 默认编辑器，非双击 .skill 打开）

## 运行器

- 播放/暂停/停止/单步：工具栏播放控制
- **Tick 模式**（工具栏下拉，持久化到 `Prefs.skillTickRate`）：关 / 30/s / 60/s
  - tick 模式下用固定步进累积器推进 `cutscene.currentTime`，每步调 `DispatchFixedTick(tick, totalTicks)`
  - `DispatchFixedTick` 遍历所有 active 的 group/track/clip 调 `IDirectable.FixedTick`
- 非 tick 模式下用 `Time.realtimeSinceStartup` 连续秒推进

## FixedTick 接口

Slate 核心接口 `IDirectable` 新增 `void FixedTick(int tick, int totalTicks)`，逐层透传：
- `CutsceneGroup.FixedTick` → 遍历子 tracks
- `CutsceneTrack.FixedTick` → 遍历子 clips
- `ActionClip.FixedTick` → 调 `protected virtual void OnFixedTick(int tick, int totalTicks) {}`

Clip 按需 override `OnFixedTick`。目前 `MoveDisplacementClip` override 了它（tick 模式下按 tick 计算位移）。

## 默认 Actor

打开技能文件时，`EnsureDefaultActors` 为每个无 actor 的 `ActorGroup` 创建白色胶囊体 GameObject（`CreatePrimitive(Capsule)`），挂到 Cutscene 根物体下（不挂 ActorGroup 下，避免被 group 的 active 状态隐藏），y 偏移 1（底部贴地）。不进 .skill 序列化（每次打开重建）。

## 数据黑板

工具栏"数据黑板"按钮 → `SkillBlackboardWindow`（Odin PropertyTree 编辑字段）。通过 `SkillBlackboardCache` 按 Cutscene 实例缓存 specialDatas 列表，保存时由 `SkillSerializer` 序列化进 .skill 文件。

## Clip 预览

- `OnRawUpdate`：每帧调用，用于持续绘制判定范围（暂停时也可见）
- `OnClipGUI(Rect)`：clip 时间轴上的自定义绘制（竖线标记判定时刻）
- `OnFixedTick`：tick 模式下每 tick 调用

## SkillPreviewUtility

`Assets/Scripts/Skill/Skill/Runtime/Draw/SkillPreviewUtility.cs` — 编辑器预览空间换算工具，与运行时 `Battle.SkillUtility` 逻辑相同但独立于 Battle 层（避免 Skill→Battle 依赖）。

## 内置 Clip

| Clip | ID | 文件 | 特点 |
|------|-----|------|------|
| MoveVelocityClip | 1001 | `Skill/Skill/Runtime/Clip/MoveVelocityClip.cs` | 速度型位移 |
| MoveDisplacementClip | 1002 | `Skill/Skill/Runtime/Clip/MoveDisplacementClip.cs` | 位移型，编辑器预览移动 Actor，支持 OnFixedTick |
| TeleportClip | 1003 | `Skill/Skill/Runtime/Clip/TeleportClip.cs` | 瞬移 |
| AttributeModifierClip | 1006 | `Skill/Skill/Runtime/Clip/AttributeModifierClip.cs` | 属性修改 |
| SingleDamageClip | 1007 | `Skill/Skill/Runtime/Clip/SingleDamageClip.cs` | 单次伤害（固定 1 tick 长度），OnRawUpdate 绘制判定范围 |
| MultiDamageClip | 1008 | `Skill/Skill/Runtime/Clip/MultiDamageClip.cs` | 多次伤害（按 HitIntervalTicks 间隔），OnRawUpdate 仅在判定 tick 附近绘制，Odin 显示总次数/DPS/当前 Tick |

**已废弃**：CollisionClip (1004) 已删除，拆分为 SingleDamageClip + MultiDamageClip。CollisionTrack (102) 已删除。

## 关键代码位置

| 代码 | 文件 |
|------|------|
| SkillEditor | `Skill/Skill/Editor/SkillEditor.cs` |
| SkillSerializer | `Skill/Skill/Editor/SkillSerializer.cs` |
| SkillFileManager | `Skill/Skill/Editor/SkillFileManager.cs` |
| SkillBlackboardWindow | `Skill/Skill/Editor/SkillBlackboardWindow.cs` |
| SkillBlackboardCache | `Skill/Skill/Editor/SkillBlackboardCache.cs` |

## SLATE 修改

Slate 库在 `Assets/Scripts/Skill/SLATE Cinematic Sequencer/`，已修改以下文件加 FixedTick：
- `Framework/IDirectable.cs` — 加了 FixedTick 接口方法
- `Framework/ActionClip.cs` — 加了 OnFixedTick 虚方法
- `Framework/CutsceneGroup.cs` — 加了 FixedTick 透传
- `Framework/CutsceneTrack.cs` — 加了 FixedTick 透传
