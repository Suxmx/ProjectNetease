# 路线图与注意事项

## 待实现基础设施

| 组件 | Domain | 职责 | 状态 |
|------|--------|------|------|
| `LockActionClip` + `LockActionExecutor` | ClientPrediction | 设 `Motor.LockedUntilTick`，锁定移动 | 未实现 |
| `Motor.LockedUntilTick` | — | tick 锁定字段 + BeginTick 检查 + reconcile | 未实现 |
| `PlayAnimationExecutor` | ClientOnly | Animancer 播放动画 | 未实现 |
| `PlayVfxExecutor` | ClientOnly | 特效/音效 | 未实现 |
| `HitFeedbackController` | 客户端组件 | 订阅 `DamageAppliedEvent`，命中反馈 | 未实现 |
| MoveVelocity/Teleport/AttributeModifier Executor | — | 恢复功能（取消注释） | log-only |
| Player×Player 碰撞矩阵 | — | 架构要求关闭，当前仍开 | 需在 Physics 设置里关闭 |
| 血条/CD 条 UGUI | — | 从 ALINE Label2D 迁移到 UGUI/TMP | 待迁移 |

## ID 分配

| 类型 | ID 范围 | 当前已用 |
|------|---------|----------|
| Group | 1+ | ActorGroup=1 |
| Track | 101+ | SkillActionTrack=101 |
| Clip | 1001+ | MoveVelocity=1001, MoveDisplacement=1002, Teleport=1003, AttributeModifier=1006, SingleDamage=1007, MultiDamage=1008 |
| SpecialData | 2001+ | DamageGroupData=2001 |

ID 全局唯一（生成器校验）。已废弃：CollisionClip=1004, CollisionTrack=102。

## 当前 Executor 状态

| Executor | Domain | ClipId | 状态 | 说明 |
|----------|--------|--------|------|------|
| MoveVelocityClipExecutor | ClientPrediction | 1001 | 已实装 | 每 tick AddPredictedVelocity |
| MoveDisplacementClipExecutor | ClientPrediction | 1002 | 已实装 | 每 tick AddPredictedDisplacement |
| TeleportClipExecutor | ClientPrediction | 1003 | log-only | 需恢复 |
| AttributeModifierClipExecutor | ServerOnly | 1006 | log-only | 需恢复 |
| SingleDamageClipExecutor | ServerOnly | 1007 | 已实装 | OnStart 调 DoHit 一次 |
| MultiDamageClipExecutor | ServerOnly | 1008 | 已实装 | OnStart + OnTick(按间隔) |

## 注意事项

- `BattlePlayerInput` 保留 Battle 前缀（避 `UnityEngine.InputSystem.PlayerInput` 冲突）
- Damage/ 和 World/ 下的类仍带 Battle 前缀（`BattleDamageDispatcher` 等），后续可去
- `SkillNodeExecutionDomain` 枚举定义在 Skill 子模块 `SkillDefinition.cs`，值为 `ClientPrediction=0/ClientOnly=1/ServerOnly=2`
- 生成器产物文件提交进仓库，保证不跑生成器也能编译
- `SkillExecutorRegistry.Init()` 在 `SkillController.Awake` 显式调用，避免 tick 中懒初始化卡顿
- `SkillPlaybackTester` 可单独验证技能回放，不经过 Player/Motor，直接调 SkillController
- Prefab 上挂 `Player` 组件（替代旧的 PlayerMotor），移动参数在 Player Inspector 上
- SkillSerializer `.skill` BinaryVersion=2，SkillDefinition `.bytes` BinaryVersion=1（两者独立）
- Skill 子模块是独立 git 仓库（`Assets/Scripts/Skill/`），主仓库通过 submodule 引用
- ALINE 库在 `Assets/Scripts/Skill/ALINE/`，通过 `Drawing` 命名空间提供 `Draw.ingame`/`Draw.editor` 绘制 API
- Slate 库在 `Assets/Scripts/Skill/SLATE Cinematic Sequencer/`，已修改 IDirectable/ActionClip/CutsceneGroup/CutsceneTrack 加 FixedTick
- 技能编辑器路径 `Window/Hoshino/Skill Editor`，编译菜单 `Tools/Hoshino/Compile All Skill Definitions`
- 数据黑板按钮在 SkillEditor 工具栏"添加轨道"旁
- Tick 模式设置持久化到 `Prefs.skillTickRate`（EditorPrefs）

## 文件结构（当前实际）

```
Battle/Runtime/
├── Core/
│   ├── Player/
│   │   ├── Player.cs                    ← TickNetworkBehaviour, [Replicate]/[Reconcile]
│   │   ├── Motor.cs                     ← 普通 C# 类, BeginTick/EndTick
│   │   ├── BattlePlayerInput.cs         ← Update 采集事件 + tick 消费
│   │   ├── OwnerCamera.cs               ← 本地玩家相机
│   │   └── ReconcileData.cs
│   ├── Combat/
│   │   ├── CombatState.cs               ← 血量/死亡/队伍, IBattleDamageTarget
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
│   ├── BattleDamageType.cs
│   ├── BattleDamageInfo.cs
│   ├── IBattleDamageTarget.cs
│   ├── BattleDamageDispatcher.cs
│   └── BattleDamageAppliedEvent.cs
├── Skill/
│   ├── Controller/
│   │   └── SkillController.cs           ← 技能调度 + 伤害组状态
│   ├── Executor/
│   │   ├── SkillExecutorAttribute.cs    ← [SkillExecutor(clipId)]
│   │   ├── IBattleSkillNodeExecutor.cs
│   │   ├── SkillNodeLifecyclePhase.cs   ← enum Start/Tick/End
│   │   ├── SkillExecutionContext.cs     ← Player + Motor + Controller + ...
│   │   ├── SkillUtility.cs             ← 运行时空间换算
│   │   ├── SkillExecutorRegistry.cs     ← Init/Clear 模式, 预扫描缓存
│   │   ├── Base/
│   │   │   ├── BattleSkillNodeExecutor.cs       ← 根基类 (abstract OnExecute)
│   │   │   ├── IBattleSkillNodeExecutor.cs
│   │   │   ├── ClientPredictionSkillExecutor.cs ← domain 基类
│   │   │   ├── ClientOnlySkillExecutor.cs
│   │   │   └── ServerOnlySkillExecutor.cs
│   │   ├── ClientPrediction/
│   │   │   ├── MoveVelocityClipExecutor.cs      ← 已实装
│   │   │   ├── MoveDisplacementClipExecutor.cs  ← 已实装
│   │   │   └── TeleportClipExecutor.cs          ← log-only
│   │   ├── ClientOnly/                  ← 后续新增 Vfx/Animation Executor
│   │   └── ServerOnly/
│   │       ├── DamageExecutorBase.cs           ← 共享 DoHit 逻辑
│   │       ├── SingleDamageClipExecutor.cs     ← 已实装
│   │       ├── MultiDamageClipExecutor.cs      ← 已实装
│   │       └── AttributeModifierClipExecutor.cs ← log-only
│   ├── Diag/
│   │   ├── SkillDiagLogger.cs           ← 诊断日志器
│   │   └── SkillDiagFlushDriver.cs      ← 每帧 flush 驱动
│   └── Service/
│       ├── SkillRuntimeServices.cs      ← 服务容器 (HitResolver)
│       ├── LagCompensatedHitResolver.cs ← 滞后补偿命中解析 + canHit 回调
│       └── DrawDriver.cs               ← 运行时调试绘制驱动
├── World/
│   ├── BattleDestructibleObject.cs
│   └── BattleCaptureObjective.cs
Battle/Tests/
└── SkillPlaybackTester.cs               ← PlayMode 测试, 镜像 Controller 调度
Battle/Editor/Skill/
├── BattleSkillExecutorCodeGenerator.cs  ← Executor 绑定生成器
├── SkillDrawEditorDriver.cs             ← 编辑器侧 SkillDraw 驱动
└── SkillDiagLogOpener.cs                ← 打开诊断日志文件夹

Skill/Skill/Runtime/
├── Clip/
│   ├── MoveVelocityClip.cs              (1001)
│   ├── MoveDisplacementClip.cs          (1002, 预览移动 Actor)
│   ├── TeleportClip.cs                  (1003)
│   ├── AttributeModifierClip.cs         (1006)
│   ├── SingleDamageClip.cs             (1007, 固定 1 tick)
│   └── MultiDamageClip.cs              (1008, 间隔判定, Odin InfoBox)
├── Track/
│   └── SkillActionTrack.cs             (101)
├── SpecialData/
│   └── DamageGroupData.cs              (2001)
├── Compiled/
│   ├── SkillDefinition.cs              ← 含 SkillRuntimeSpecialData
│   ├── SkillNodePayloads.cs            ← SkillSpace/SkillHitShape 枚举
│   └── SkillTickUtility.cs             ← 支持 30/60 tick 率
├── Draw/
│   ├── SkillDraw.cs                    ← ALINE 调试绘制
│   └── SkillPreviewUtility.cs          ← 编辑器预览空间换算
├── Generated/
│   ├── SkillSerializationAttributes.cs ← [SkillClipType]/[SkillTrackType]/[SkillSpecialDataType]/[SkillCustomData]
│   ├── SkillGeneratedSerializationServices.cs
│   └── SkillBuiltInExternalTypes.cs
└── SkillFileRef.cs

Skill/Skill/Editor/
├── SkillEditor.cs                      ← 技能编辑器 (tick 模式 + 数据黑板 + 默认 Actor)
├── SkillSerializer.cs                  ← .skill 二进制读写 (BinaryVersion=2)
├── SkillFileManager.cs
├── SkillData.cs                        ← SkillFileData/GroupEntry/TrackEntry/ClipEntry/SpecialDataEntry
├── SkillBlackboardCache.cs             ← 按 Cutscene 缓存 specialDatas
├── SkillBlackboardWindow.cs            ← 数据黑板编辑面板 (Odin PropertyTree)
├── SkillGeneratedEditorSerializationServices.cs
├── CodeGen/
│   ├── SkillCodeGenUtilities.cs
│   └── SkillSerializationCodeGenerator.cs  ← 生成 Clip/Track/SpecialData 序列化代码
└── Compiler/
    └── SkillDefinitionCompiler.cs      ← 编译 .skill → .bytes (长度取节点 EndTick 最大值)

Generated/
├── Battle/Skill/
│   └── BattleSkillExecutorBindings.cs  ← Executor 绑定表 + domain 表
└── Skill/
    ├── Runtime/
    │   └── SkillGeneratedSerialization.cs  ← NodeData/SpecialData struct + Blob 读写
    └── Editor/
        └── SkillGeneratedEditorSerialization.cs

SLATE (已修改):
├── Framework/IDirectable.cs            ← 加了 FixedTick 接口方法
├── Framework/ActionClip.cs             ← 加了 OnFixedTick 虚方法
├── Framework/CutsceneGroup.cs          ← 加了 FixedTick 透传
└── Framework/CutsceneTrack.cs          ← 加了 FixedTick 透传
```
