# 弹射物（子弹）框架设计

> 状态：已评审，待开工
> 范式：Effect-Spawning（逻辑/视觉分离）+ 通用技能拒绝机制（Executor.OnReject + IRejectHandler 双路径）
> 目标：在现有 FishNet 预测回滚 + SkillExecutor 体系上，做出手感好、判定公平、可扩展的弹射物框架

## 1. 设计目标与非目标

### 目标
- 非瞬间判定，有飞行时间
- 直线匀速弹道（MVP）；抛物线/追踪预留接口
- Owner 0 RTT 视觉手感
- 服务器权威判定（100% 公平）
- Spectator 通过 RPC 看到他人 projectile
- 框架层为爆炸 AOE / Buff / 连锁等扩展留接口
- 通用技能拒绝机制：服务器拒绝技能时，客户端能回收所有预测产物（projectile/VFX/动画）

### 非目标（本次不做）
- 抛物线/追踪实装（字段留位）
- 爆炸 AOE 实装（字段 + 钩子留位）
- Buff 系统实装（字段 + TODO 留位）
- hitscan（不适用本框架，瞬间判定走 SingleDamageClipExecutor）

## 2. 关键决策与依据

### 2.1 不用 FishNet PredictedSpawn（范式 A），改用 Effect-Spanning（范式 B）

**FishNet 自带 `PredictedSpawn` 组件** 能力：客户端预测 Spawn NetworkObject → 服务器 `OnTrySpawnServer` 校验 → 失败时 `ParsePredictedSpawnResult` 自动 `Destroy(nob.gameObject)`（`ClientObjects.cs:340-349`）。

**不适合子弹的原因**：
1. 每个 projectile 一个 NetworkObject → 大量 projectile 时带宽/prefab 注册爆炸
2. `ClientObjects.cs:343` 源码注释明确说 `TODO support pooling` → **当前不支持对象池**，频繁 spawn/despawn 性能炸
3. NetworkObject 的 reconcile 是"位置推送"，不是 tick-replay，高速 projectile 插值不自然
4. 子弹是技能系统触发，技能状态在 `SkillReconcileState` 里，projectile 若另起 NetworkObject 同步体系则两套割裂

**业界标准**：CS:GO / Apex / Overwatch / Valorant 的 projectile 都是 Effect-Spanning。Fortnite 用范式 A 做载具，但子弹/可破坏物用范式 B。

### 2.2 逻辑/视觉分离架构

```
服务器权威 projectile = 纯逻辑 struct（位置/速度/寿命/命中参数）
  ↓
- 服务器: ProjectileManager.TickServer → 推进 struct 列表 → SphereCast 命中 → BattleDamageDispatcher
- 客户端: 收到 SpawnRpc(params) → 本地纯视觉 projectile（对象池 MonoBehaviour）
- 客户端视觉: 本地按相同参数预测飞行（非 authoritative）
- 服务器: 命中/到期 → ImpactRpc(hitPoint, hitTargetId) → 所有客户端销毁视觉
- 服务器: 拒绝技能 → 通用 OnReject 机制 → owner 回收预测产物（见 2.7）
- Reconcile: 完全不碰 projectile（不在玩家 reconcile state 里）
```

### 2.3 通用技能拒绝机制（双路径）

**业界标准做法**：服务器是 source of truth，拒绝由服务器主动通知。**但拒绝的回收要做通用化**，因为预测产物不止 projectile——未来还有 VFX/动画等。

**双路径并行设计**：

```
路径1: Executor.OnReject（产物生命周期 ≤ 技能）
  → 服务器拒绝 → TargetRpc → SkillController.HandleSkillRejected
  → 校验 _activeSequenceId == seqId
    - 技能还在跑 → 对 active 节点调 Executor.OnReject
    - 技能已结束 → 忽略（无操作）
  → 用途：停 VFX/动画（技能结束产物就没了）

路径2: IRejectHandler（产物生命周期 > 技能）
  → 服务器拒绝 → TargetRpc → SkillManager 遍历 IRejectHandler 列表
  → ProjectileManager.OnRejectSkill(seqId) → 查 _visualBySequence 淡出
  → 不依赖 SkillController 状态——技能结束也能回收
  → 用途：回收 projectile 视觉（飞出去了技能也结束了）

幂等保证：
  两条路径对 projectile 都调 RejectVisualsForSequence(seqId)
  视觉不存在则无操作，重复无害
```

**为什么不靠客户端 reconcile 检测 `_isActive: true→false`**：
- FishNet reconcile 主要修位置/速度，不保证把 `_isActive` 抹成 false
- 即使抹成 false，客户端也无法区分"服务器拒绝"vs"技能正常结束"vs"replay 临时状态"
- 服务器主动 `TargetRpc` 才是明确语义

### 2.4 Projectile 生命周期与技能生命周期解耦

`ProjectileClip` 长度固定 1 tick（只 spawn），spawn 后由 `ProjectileManager` 接管飞行。**技能可立即做其他动作，不被 projectile 飞行时间锁住技能槽**。

这带来一个副作用：技能可能先于 projectile 结束，导致路径1（Executor.OnReject）无法回收 projectile。**路径2（IRejectHandler）专门解决此场景**——见 2.7。

### 2.5 客户端视觉 projectile 不进 reconcile

视觉 projectile 是"尽力预测"：用 clip 数据里的固定参数本地模拟，drift 只影响视觉。服务器 `ImpactProjectileRpc` 携带 hitPoint，视觉 impact 时 snap 到权威位置后归池。Replay 时 `OnStart` 用 `ContainsReplayed` 过滤 + `_spawnedIds` 去重，避免重复 spawn。

### 2.6 服务器命中不做滞后补偿

Projectile 的飞行时间天然消化 RTT。服务器在当前 tick 用 `Physics.SphereCast`（连续检测防穿透）做命中，无需回滚目标位置。与 hitscan 不同，是 projectile 的标准做法。

### 2.7 技能已结束 + Reject 晚到的场景

**核心矛盾**：ProjectileClip 长度=1 tick，但视觉产物生命周期=60 tick。技能结束后 `_activeSequenceId` 清零，Reject 到达时路径1 校验失败。

```
Owner RTT = 100ms
ProjectileClip 长度 = 1 tick (16ms)
Projectile maxLifetime = 60 tick (1s)

时间线：
  t=0ms    Owner 按 Press → 预测 spawn 视觉 projectile（技能启动）
  t=16ms   技能正常结束 → StopSkill → _activeSequenceId=0
  t=50ms   服务器收到 Press → 校验失败 → 发 RejectSkillRpc
  t=100ms  Reject 到达 owner
           → 路径1: _activeSequenceId != seqId → 忽略
           → 路径2: ProjectileManager.OnRejectSkill → 淡出视觉 ✓
  t=1000ms 若无路径2，视觉会靠 maxLifetime 兜底凭空消失（手感差）
```

**VFX/Anim 没这个问题**——它们的产物生命周期 ≤ 技能生命周期，技能结束时产物自然消失，路径1 足够。

| 产物类型 | 生命周期 | 回收路径 | 例子 |
|----------|----------|----------|------|
| VFX/动画 | ≤ 技能 | 路径1 Executor.OnReject（技能还在跑时生效） | 突进拖尾、施法动画 |
| Projectile | > 技能 | 路径2 IRejectHandler（不管技能状态） | 子弹、箭 |
| 位移 | 在 Motor 状态里 | Motor reconcile 自动修正 | 突进 |

**不需要缓存历史 sequenceId**——两条路径独立工作，各取所需。

## 3. 文件结构

### 新增文件

```
Skill/Skill/Runtime/Clip/
└─ ProjectileClip.cs                      ← Slate ActionClip, clipId=1009, ClientPrediction 域

Battle/Runtime/Projectile/
├─ ProjectileManager.cs                   ← NetworkBehaviour, 服务器权威 + 客户端视觉 + RPC
├─ Projectile.cs                          ← 服务器权威 projectile 状态 struct
├─ VisualProjectile.cs                    ← 客户端视觉 MonoBehaviour, 对象池, Update 推进
├─ ProjectileParams.cs                    ← 共享参数 struct, 服务器/客户端模拟共用
├─ ProjectileIdentity.cs                  ← projectileId struct, 去重 + RPC 关联
├─ ProjectilePrefabDatabase.cs            ← ScriptableObject, projectilePrefabId → prefab 映射
└─ IProjectileImpactHandler.cs            ← 命中后处理扩展点（爆炸/buff/连锁等未来接入）

Battle/Runtime/Skill/
├─ SkillManager.cs                        ← 场景级 NetworkBehaviour, 专管技能网络 RPC（Reject 等）
├─ IRejectHandler.cs                      ← 产物生命周期 > 技能的回收接口（路径2）
├─ Event/
│  ├─ SkillRejectedEvent.cs               ← MF.Event 事件参数
│  └─ ERejectReason.cs                    ← 拒绝原因 enum, 第一版只有 Unknown

Battle/Runtime/Skill/Executor/ClientPrediction/
└─ ProjectileClipExecutor.cs              ← 角色分流 OnStart + OnReject
```

### 修改文件

```
Battle/Runtime/Skill/Executor/Base/BattleSkillNodeExecutor.cs
  ← 加 protected virtual void OnReject(...) 默认空实现

Battle/Runtime/Skill/Executor/Base/ClientPredictionSkillExecutor.cs
  ← sealed override OnReject → owner+非replay 时调子类

Battle/Runtime/Skill/Executor/Base/ClientOnlySkillExecutor.cs
  ← sealed override OnReject → owner+非replay 时调子类

Battle/Runtime/Skill/Executor/Base/ServerOnlySkillExecutor.cs
  ← sealed override OnReject → 空实现（服务器无预测可回收）

Battle/Runtime/Skill/Controller/SkillController.cs
  ← 服务器检测失败调 SkillManager.RejectSkill
  ← 新增 HandleSkillRejected(seqId) 客户端入口（路径1）
  ← 新增 RejectDomainNodes 遍历 active 节点调 OnReject

Battle/Runtime/Skill/Service/SkillRuntimeServices.cs
  ← 加 _skillManager 引用 + 自动查找
  ← 加 _projectileManager 引用 + 自动查找
```

### 不动的部分

- 现有伤害链路（`LagCompensatedHitResolver` / `BattleDamageDispatcher` / `BattleDamageAppliedEvent`）—— 直接复用
- `SkillController` 的 reconcile / state 重建逻辑
- 既有 Executor 的 OnStart/OnTick/OnEnd 逻辑
- NetDemo 那 4 个文件

## 4. 核心类型设计

### 4.1 ProjectileClip（clip data）

```csharp
[SkillClipType(1009u)]
[Attachable(typeof(SkillActionTrack))]
public sealed class ProjectileClip : ActionClip
{
    // --- 弹道（直线 MVP，gravity/homing 预留）---
    [SkillCustomData] public float Speed = 30f;
    [SkillCustomData] public Vector3 Gravity = Vector3.zero;        // 预留：抛物线
    [SkillCustomData] public byte MaxLifetimeTicks = 60;
    [SkillCustomData] public ushort HomingTargetNetId = 0;           // 预留：追踪

    // --- 命中 ---
    [SkillCustomData] public SkillHitShape HitShape = SkillHitShape.Sphere;
    [SkillCustomData] public float HitRadius = 0.3f;
    [SkillCustomData] public LayerMask HitMask = ~0;

    // --- spawn ---
    [SkillCustomData] public SkillSpace Space = SkillSpace.AimDirection;
    [SkillCustomData] public Vector3 SpawnOffset = new(0, 0.8f, 0.5f);
    [SkillCustomData] public bool InheritAim = true;

    // --- 伤害 ---
    [SkillCustomData] public int Damage = 15;
    [SkillCustomData] public byte DamageGroupId = 0;

    // --- 预留扩展点（值=0 表示不触发）---
    [SkillCustomData] public float ExplosionRadius = 0f;             // 预留：AOE
    [SkillCustomData] public int ExplosionDamage = 0;
    [SkillCustomData] public uint BuffIdOnHit = 0;                   // 预留：buff

    // --- 视觉 ---
    [SkillCustomData] public byte ProjectilePrefabId = 1;            // 查 PrefabDatabase

    public override float length { get => 0.0167f; set {} }          // 固定 1 tick
}
```

### 4.2 ProjectileIdentity

```csharp
public readonly struct ProjectileIdentity : IEquatable<ProjectileIdentity>
{
    public readonly uint SequenceId;     // 来自 SkillCommand.SequenceId，唯一标识本次技能施放
    public readonly int NodeId;          // 来自 SkillRuntimeNode.NodeId，区分同技能多 projectile clip
    public readonly uint SpawnTick;      // Owner 本地 spawn tick

    // 关键：同一技能多次施放 → SequenceId 不同
    //       同一技能多个 ProjectileClip → NodeId 不同
    //       reconcile replay 重放同一 tick → 三者全等 → _spawnedIds 去重
}
```

### 4.3 ProjectileParams

```csharp
public struct ProjectileParams
{
    public Vector3 Origin;
    public Vector3 Direction;           // 单位向量
    public float Speed;
    public Vector3 Gravity;             // 预留
    public byte MaxLifetimeTicks;
    public float HitRadius;
    public LayerMask HitMask;
    public int Damage;
    public byte DamageGroupId;
    public byte ProjectilePrefabId;
    public ushort AttackerNetId;        // 服务器端用，避免重新查 CombatState

    // 预留
    public float ExplosionRadius;
    public int ExplosionDamage;
    public uint BuffIdOnHit;
    public ushort HomingTargetNetId;
}
```

### 4.4 Projectile（服务器权威逻辑状态）

```csharp
public struct Projectile
{
    public ProjectileIdentity Id;
    public ProjectileParams Params;
    public Vector3 Position;
    public Vector3 Velocity;            // 当前速度向量（已含方向）
    public uint BirthTick;
    public CombatState Attacker;        // 缓存引用
    public NetworkConnection AttackerConn;
    public bool IsAlive;
}
```

### 4.5 VisualProjectile（客户端视觉）

```csharp
// 实现 MemoFramework 的 IObject 接口以接入 MF ObjectPoolComponent
public sealed class VisualProjectile : MonoBehaviour, MemoFramework.ObjectPool.IObject
{
    public string Name { get; set; }              // IObject: 池名，由 ObjectPoolComponent 设置
    public void OnSpawned(object userData = null) // IObject: 从池中取出时回调（Init 在此之后由 Manager 调）
    {
        gameObject.SetActive(true);
    }
    public void OnDespawned()                    // IObject: 归池时回调
    {
        gameObject.SetActive(false);
    }

    public void Init(in ProjectileParams p, in ProjectileIdentity id);
    public void TickVisual(float delta);          // Update 内调用，按 Params 本地推进
    public void SnapToImpact(Vector3 hitPoint, Vector3 hitNormal);  // ImpactRpc 到达时
    public void FadeOutAndReturn(float duration);  // Reject 时调，淡出后归池
    // 归池统一走 MF.ObjectPool.Despawn(this)，不自己 SetActive
}
```

**对象池使用 MemoFramework 自带的 `ObjectPoolComponent`**：
- `VisualProjectile` 实现 `MemoFramework.ObjectPool.IObject` 接口（`OnSpawned`/`OnDespawned`）
- `ProjectileManager` 启动时通过 `MF.ObjectPool.CreateObjectPool(poolName, prefab)` 按 prefabId 注册池
- Spawn: `MF.ObjectPool.Spawn(poolName, pos, rot, userData)` → 取出后调 `Init`
- Despawn: `MF.ObjectPool.Despawn(visualProjectile)` → 自动调 `OnDespawned` 归池
- 访问入口统一为 `MF.ObjectPool`（`MemoFramework/Framework/Extension/MF.cs` 已注册为静态属性）
- 不自建 Stack/Dictionary 池，复用 MF 既有设施

### 4.6 ProjectileManager（核心 NetworkBehaviour）

```csharp
// 实现 IRejectHandler：技能被拒绝时回收视觉 projectile（路径2）
public sealed class ProjectileManager : NetworkBehaviour, IRejectHandler
{
    [SerializeField] private ProjectilePrefabDatabase _prefabDb;
    [SerializeField] private int _prepoolPerPrefab = 16;

    // --- 服务器权威 ---
    private readonly List<Projectile> _serverProjectiles = new();

    // --- 客户端视觉 ---
    private readonly Dictionary<ProjectileIdentity, VisualProjectile> _clientVisuals = new();
    private readonly HashSet<ProjectileIdentity> _spawnedIds = new();  // 去重（防 replay 重复）
    private readonly Dictionary<uint, List<VisualProjectile>> _visualBySequence = new();  // 撤销用

    // --- 对象池：复用 MemoFramework.ObjectPoolComponent，按 projectilePrefabId 注册池 ---
    // 访问入口: MF.ObjectPool.CreateObjectPool / Spawn / Despawn
    // VisualProjectile 实现 IObject 接口，池内自动 SetActive

    // --- 扩展点：命中后处理（爆炸/buff/连锁等未来挂这里）---
    private readonly List<IProjectileImpactHandler> _impactHandlers = new();

    private void Awake()
    {
        // --- 自动注册到 SkillManager 的 IRejectHandler 列表 ---
        SkillManager sm = FindFirstObjectByType<SkillManager>();
        sm?.RegisterRejectHandler(this);
    }

    // === 客户端预测 spawn（由 ProjectileClipExecutor 调用）===
    public void SpawnVisualLocal(in ProjectileIdentity id, in ProjectileParams p)
    {
        if (!_spawnedIds.Add(id)) return;   // 去重
        string poolName = PoolNameFor(p.ProjectilePrefabId);
        Transform tr = MF.ObjectPool.Spawn(poolName, p.Origin, Quaternion.LookRotation(p.Direction));
        VisualProjectile v = tr.GetComponent<VisualProjectile>();
        v.Init(p, id);
        _clientVisuals[id] = v;
        _visualBySequence.GetOrAdd(id.SequenceId).Add(v);
    }

    // === 服务器权威 spawn（由 ProjectileClipExecutor 在 IsServerStarted 时调用）===
    [Server]
    public void SpawnAuthority(in ProjectileIdentity id, in ProjectileParams p,
        CombatState attacker, NetworkConnection attackerConn)
    {
        // --- 服务器校验 spawn 合法性（位置/参数等）---
        // 注：技能级合法性（cooldown/沉默/死亡）由 SkillController 检测，走通用 RejectSkill
        if (!ValidateSpawn(attacker, p))
        {
            // spawn 级拒绝 → 走通用 SkillManager.RejectSkill（统一入口）
            SkillManager sm = FindFirstObjectByType<SkillManager>();
            sm?.RejectSkill(attackerConn, id.SequenceId, 0);
            return;
        }

        _serverProjectiles.Add(new Projectile
        {
            Id = id,
            Params = p,
            Position = p.Origin,
            Velocity = p.Direction * p.Speed,
            BirthTick = TimeManager.LocalTick,
            Attacker = attacker,
            AttackerConn = attackerConn,
            IsAlive = true
        });

        // 通知 spectator 生成视觉（排除 owner，owner 已本地预测）
        SpawnProjectileRpc(id, p);
    }

    // === RPC：spawn 视觉给 spectator ===
    [ObserversRpc]
    private void SpawnProjectileRpc(in ProjectileIdentity id, in ProjectileParams p)
    {
        if (IsOwner(p.AttackerNetId)) return;   // owner 已本地预测，跳过
        SpawnVisualLocal(id, p);
    }

    // === RPC：impact 通知所有客户端销毁视觉 ===
    [ObserversRpc]
    private void ImpactProjectileRpc(in ProjectileIdentity id, Vector3 hitPoint,
        Vector3 hitNormal, ushort hitTargetNetId, bool hitOccurred)
    {
        if (_clientVisuals.TryGetValue(id, out VisualProjectile v))
        {
            if (hitOccurred) v.SnapToImpact(hitPoint, hitNormal);
            // SnapToImpact 播完命中特效后内部调 MF.ObjectPool.Despawn(this)
            else MF.ObjectPool.Despawn(v);
            _clientVisuals.Remove(id);
            RemoveFromSequenceIndex(id, v);
            _spawnedIds.Remove(id);  // 清理去重表
        }
    }

    // === IRejectHandler 实现（路径2：技能已结束也能回收）===
    public void OnRejectSkill(uint sequenceId)
    {
        RejectVisualsForSequence(sequenceId);
    }

    // === 公开方法：按 sequenceId 淡出所有相关视觉 ===
    // 路径1（Executor.OnReject）和路径2（IRejectHandler）都调此方法，幂等
    public void RejectVisualsForSequence(uint sequenceId)
    {
        if (!_visualBySequence.TryGetValue(sequenceId, out var list))
            return;
        foreach (var v in list)
        {
            v.FadeOutAndReturn(0.15f);
            // FadeOutAndReturn 淡出动画结束后内部调 MF.ObjectPool.Despawn(this)
            _clientVisuals.Remove(v.Identity);
            _spawnedIds.Remove(v.Identity);
        }
        list.Clear();
        _visualBySequence.Remove(sequenceId);
    }

    // === 服务器 Tick 推进 ===
    private void TimeManager_OnTick()
    {
        if (IsServerStarted) TickServer();
        if (IsClientStarted) TickClient();
    }

    private void TickServer()
    {
        float delta = (float)TimeManager.TickDelta;
        for (int i = _serverProjectiles.Count - 1; i >= 0; i--)
        {
            Projectile p = _serverProjectiles[i];
            Vector3 prev = p.Position;
            Vector3 next = prev + p.Velocity * delta;  // 预留：+ Gravity * delta

            // 连续检测防高速穿透
            if (Physics.SphereCast(prev, p.Params.HitRadius, (next - prev).normalized,
                out RaycastHit hit, Vector3.Distance(prev, next), p.Params.HitMask))
            {
                // 命中玩家
                CombatState target = hit.collider.GetComponentInParent<CombatState>();
                if (target != null && target != p.Attacker && !target.IsDead)
                {
                    ApplyDamage(p, target, hit.point, hit.normal);
                    OnServerImpact(p, hit, target);
                    ImpactProjectileRpc(p.Id, hit.point, hit.normal, target.ObjectId, true);
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }
                // 命中墙/可破坏物
                BattleDestructibleObject dest = hit.collider.GetComponentInParent<BattleDestructibleObject>();
                if (dest != null && !dest.IsDestroyed)
                {
                    ApplyDamage(p, dest, hit.point, hit.normal);
                    OnServerImpact(p, hit, null);
                    ImpactProjectileRpc(p.Id, hit.point, hit.normal, dest.ObjectId, true);
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }
                // 命中纯墙
                OnServerImpact(p, hit, null);
                ImpactProjectileRpc(p.Id, hit.point, hit.normal, 0, true);
                _serverProjectiles.RemoveAt(i);
                continue;
            }

            // 寿命到期
            uint age = TimeManager.LocalTick - p.BirthTick;
            if (age >= p.Params.MaxLifetimeTicks)
            {
                ImpactProjectileRpc(p.Id, next, Vector3.up, 0, false);
                _serverProjectiles.RemoveAt(i);
                continue;
            }

            p.Position = next;
            _serverProjectiles[i] = p;
        }
    }

    private void TickClient()
    {
        // 客户端视觉 projectile 由 VisualProjectile.Update() 自己推进，这里只清理孤儿
        // 兜底：超过 maxLifetime + RTT 容差仍存活 → 强制归池（防 RPC 丢包）
        //       调 MF.ObjectPool.Despawn(v) 触发 IObject.OnDespawned
    }

    // === 命中后处理扩展点（爆炸/buff/连锁未来挂这里）===
    private void OnServerImpact(in Projectile p, RaycastHit hit, CombatState target)
    {
        // 预留：ExplosionRadius > 0 → LagCompensatedHitResolver.ResolveDamageSphere
        // 预留：BuffIdOnHit != 0 → 未来 buff 系统接入
        foreach (var h in _impactHandlers)
            h.OnProjectileImpact(p.Id, p.Params, hit.point, hit.normal, p.Attacker, target);
    }

    private void ApplyDamage(in Projectile p, IBattleDamageTarget target, Vector3 hitPoint, Vector3 hitNormal)
    {
        // 走既有伤害链路，DamageGroupId 防重复
        BattleDamageDispatcher.Apply(new BattleDamageInfo
        {
            Type = BattleDamageType.Skill,
            Amount = p.Params.Damage,
            Source = p.Attacker,
            Target = target,
            SourceConnection = p.AttackerConn,
            SourceClipId = SkillGeneratedIds.ProjectileClip,
            HitPoint = hitPoint,
            HitNormal = hitNormal,
            Tick = TimeManager.LocalTick
        });
    }
}
```

### 4.7 IProjectileImpactHandler（命中扩展点）

```csharp
/// <summary>
/// Projectile 命中后的扩展处理接口。未来爆炸 AOE / Buff 触发 / 连锁闪电等
/// 通过实现此接口注册到 ProjectileManager._impactHandlers。
/// </summary>
public interface IProjectileImpactHandler
{
    void OnProjectileImpact(
        in ProjectileIdentity id,
        in ProjectileParams p,
        Vector3 hitPoint,
        Vector3 hitNormal,
        CombatState attacker,
        CombatState target);
}
```

### 4.8 ProjectileClipExecutor

```csharp
[SkillExecutor(SkillGeneratedIds.ProjectileClip)]
public sealed class ProjectileClipExecutor : ClientPredictionSkillExecutor<ProjectileNodeData>
{
    protected override void OnStart(in SkillExecutionContext context, in ProjectileNodeData data)
    {
        // --- 防止 reconcile replay 重复 spawn ---
        if (context.ReplicateState.ContainsReplayed()) return;

        ProjectileManager mgr = context.Services?.ProjectileManager;
        if (mgr == null) return;

        // --- 旁观者跳过：等服务器 RPC 同步 ---
        if (!context.Player.IsOwner && !context.Player.IsServerStarted) return;

        ProjectileParams p = BuildParams(context, data);
        ProjectileIdentity id = new(context.Command.SequenceId, context.Node.NodeId, context.CurrentTick);

        // --- Owner: 本地预测视觉（0 RTT）---
        if (context.Player.IsOwner)
            mgr.SpawnVisualLocal(id, p);

        // --- 服务器: 权威 spawn（含校验，失败会调 SkillManager.RejectSkill）---
        if (context.Player.IsServerStarted)
            mgr.SpawnAuthority(id, p, context.CombatState, context.Player.Owner);
    }

    // === 路径1：技能还在跑时，服务器拒绝 → 调此处回收视觉 ===
    // 幂等：与路径2（IRejectHandler）都调 RejectVisualsForSequence，重复无害
    protected override void OnReject(in SkillExecutionContext context, in ProjectileNodeData data)
    {
        context.Services?.ProjectileManager?.RejectVisualsForSequence(context.Command.SequenceId);
    }

    private static ProjectileParams BuildParams(in SkillExecutionContext context, in ProjectileNodeData data)
    {
        Transform t = context.Motor.Transform;
        Vector3 origin = t.position + SkillUtility.ResolveVector(
            data.Space, data.SpawnOffset, t, context.AimDirection, false);
        Vector3 dir = data.InheritAim
            ? context.AimDirection
            : SkillUtility.ResolveRotation(data.Space, t, context.AimDirection) * Vector3.forward;

        return new ProjectileParams
        {
            Origin = origin,
            Direction = dir.normalized,
            Speed = data.Speed,
            Gravity = data.Gravity,
            MaxLifetimeTicks = data.MaxLifetimeTicks,
            HitRadius = data.HitRadius,
            HitMask = data.HitMask,
            Damage = data.Damage,
            DamageGroupId = data.DamageGroupId,
            ProjectilePrefabId = data.ProjectilePrefabId,
            AttackerNetId = (ushort)(context.Player?.NetworkObjectId ?? 0),
            ExplosionRadius = data.ExplosionRadius,
            ExplosionDamage = data.ExplosionDamage,
            BuffIdOnHit = data.BuffIdOnHit,
            HomingTargetNetId = data.HomingTargetNetId
        };
    }
}
```

### 4.9 SkillManager（场景级 NetworkBehaviour，专管技能网络 RPC）

```csharp
/// <summary>
/// 技能系统网络 RPC 中转。场景级单例，专管技能相关网络通信（拒绝、未来 cooldown 同步等）。
/// 不承载技能状态/调度——那些仍在 SkillController。
/// 通过 SkillRuntimeServices.SkillManager 被访问。
/// </summary>
public sealed class SkillManager : NetworkBehaviour
{
    // --- 路径2：产物生命周期 > 技能的回收 handler 注册表 ---
    private readonly List<IRejectHandler> _rejectHandlers = new();

    /// <summary>注册 IRejectHandler。ProjectileManager.Awake 时自动调用。</summary>
    public void RegisterRejectHandler(IRejectHandler handler)
    {
        if (handler != null && !_rejectHandlers.Contains(handler))
            _rejectHandlers.Add(handler);
    }

    /// <summary>[Server] 服务器检测到技能启动失败时调用。发 TargetRpc + MF.Event。</summary>
    [Server]
    public void RejectSkill(NetworkConnection ownerConn, uint sequenceId, int skillId)
    {
        // --- 跨系统事件（服务器侧同步分发）---
        // UI/buff 等非 Executor 订阅者通过 MF.Event 接收
        MF.Event.Fire(new SkillRejectedEvent(sequenceId, skillId, ERejectReason.Unknown));

        // --- TargetRpc 精确发 owner ---
        RejectSkillRpc(ownerConn, sequenceId, skillId);
    }

    /// <summary>TargetRpc：服务器拒绝技能 → 通知 owner 回收预测产物。</summary>
    [TargetRpc]
    private void RejectSkillRpc(NetworkConnection conn, uint sequenceId, int skillId)
    {
        // --- 路径1：Executor.OnReject（产物 ≤ 技能生命周期，技能还在跑时生效）---
        SkillController controller = FindLocalSkillController();
        controller?.HandleSkillRejected(sequenceId);

        // --- 路径2：IRejectHandler（产物 > 技能生命周期，不管技能状态）---
        foreach (IRejectHandler h in _rejectHandlers)
            h.OnRejectSkill(sequenceId);

        // --- 客户端侧也分发 MF.Event，让本地 UI 订阅 ---
        MF.Event.Fire(new SkillRejectedEvent(sequenceId, skillId, ERejectReason.Unknown));
    }

    private SkillController FindLocalSkillController()
    {
        // 本地玩家（owner）的 SkillController
        // 实现细节：通过 Player.LocalPlayer 或 FindFirstObjectByType<Player> 拿到本地 Player
        // 然后 GetComponent<SkillController>()
        // 具体看项目里 owner player 怎么定位（FishNet 通常有 LocalConnection）
        return null;  // TODO: 实现时填
    }
}
```

### 4.10 IRejectHandler（路径2 接口）

```csharp
/// <summary>
/// 产物生命周期 > 技能生命周期的回收接口。
/// 服务器拒绝技能时，SkillManager 遍历所有 IRejectHandler 调 OnRejectSkill。
/// 不依赖 SkillController 状态——技能已结束也能回收产物。
/// 
/// 与 Executor.OnReject（路径1）的区别：
/// - 路径1 依赖 _activeSequenceId == seqId 校验，技能结束则失效
/// - 路径2 自管产物注册表（如 ProjectileManager._visualBySequence），独立工作
/// 
/// 幂等保证：两条路径对同一产物都调回收方法（如 RejectVisualsForSequence），
/// 产物不存在则无操作，重复无害。
/// </summary>
public interface IRejectHandler
{
    void OnRejectSkill(uint sequenceId);
}
```

### 4.11 SkillRejectedEvent + ERejectReason

```csharp
/// <summary>技能拒绝原因。第一版只有 Unknown，未来扩展 CanAct/SkillNotFound/Cooldown/Mana 等。</summary>
public enum ERejectReason : byte
{
    Unknown = 0,
}

/// <summary>
/// 技能被服务器拒绝的事件。通过 MF.Event 分发，供 UI/buff 等跨系统订阅。
/// 服务器侧和客户端侧各分发一次。
/// </summary>
public sealed class SkillRejectedEvent
{
    public uint SequenceId;
    public int SkillId;
    public ERejectReason Reason;
}
```

### 4.12 Executor 基类 OnReject 改动

```csharp
// BattleSkillNodeExecutor.cs（根基类）
public abstract class BattleSkillNodeExecutor<TData> : IBattleSkillNodeExecutor
{
    // 既有
    public void Execute(in SkillExecutionContext context) { ... }
    protected abstract void OnExecute(in SkillExecutionContext context, in TData data);

    // === 新增：服务器拒绝技能时调，默认空实现 ===
    // 只在客户端 owner 侧 + 非 replay 调用（domain 基类负责过滤）
    // 用途：回收产物生命周期 ≤ 技能的预测产物（VFX/动画）
    protected virtual void OnReject(in SkillExecutionContext context, in TData data) { }

    // 供 SkillController 调用
    public void ExecuteReject(in SkillExecutionContext context)
    {
        if (!SkillGeneratedNodeDataBlob.TryRead(context.Skill, context.Node, out TData data))
            return;
        OnReject(context, in data);
    }
}

// ClientPredictionSkillExecutor.cs（sealed override）
protected sealed override void OnReject(in SkillExecutionContext context, in TData data)
{
    // --- 只在 owner 侧 + 非 replay 调子类 ---
    if (!context.Player.IsOwner) return;
    if (context.ReplicateState.ContainsReplayed()) return;
    OnReject(context, in data);  // 调子类实现
}

// ClientOnlySkillExecutor.cs（sealed override）
protected sealed override void OnReject(in SkillExecutionContext context, in TData data)
{
    if (!context.Player.IsOwner) return;
    if (context.ReplicateState.ContainsReplayed()) return;
    OnReject(context, in data);
}

// ServerOnlySkillExecutor.cs（sealed override）
protected sealed override void OnReject(in SkillExecutionContext context, in TData data)
{
    // 服务器是 source of truth，无预测可回收
}
```

### 4.13 SkillController 拒绝相关改动

```csharp
// SkillController.cs 修改点（伪代码，保留既有逻辑）

public void TickReplicate(SkillCommand command, ...)
{
    bool canAct = _combatState == null || _combatState.CanAct;
    if (!canAct)
    {
        // === 服务器检测到 Press 但不能行动 → 通用拒绝 ===
        if (_player.IsServerStarted && !state.ContainsReplayed()
            && command.Type == SkillCommandType.Press)
        {
            ResolveServices()?.SkillManager?.RejectSkill(_player.Owner, command.SequenceId, command.SkillId);
        }
        return;
    }
    // ... 既有 TickClientPrediction/TickServerOnly/TickClientOnly
}

private void TryStartSkill(SkillCommand command, uint currentTick, ReplicateState state)
{
    SkillDefinition skill = ...;
    if (skill == null)
    {
        // === 服务器找不到技能 → 通用拒绝 ===
        if (_player.IsServerStarted && !state.ContainsReplayed())
            ResolveServices()?.SkillManager?.RejectSkill(_player.Owner, command.SequenceId, command.SkillId);
        return;
    }
    // ... 既有启动逻辑
    // 未来：cooldown/mana 校验失败时同理调 RejectSkill
}

// === 客户端入口：收到 RejectSkillRpc 后被 SkillManager 调用 ===
public void HandleSkillRejected(uint sequenceId)
{
    // --- 校验是当前技能 ---
    if (_activeSequenceId != sequenceId) return;  // 技能已结束 → 路径1 忽略，路径2 兜底

    // --- 对所有 active 节点调 OnReject（路径1）---
    RejectDomainNodes(_activeClientPredictionNodeIds);
    if (_player.IsClientStarted)
        RejectDomainNodes(_activeClientOnlyNodeIds);
    // ServerOnly 节点不调（无预测可回收）

    // --- 不清 activeNodeIds，等服务器 reconcile 自然处理 ---
}

private void RejectDomainNodes(HashSet<int> activeNodeIds)
{
    if (_activeSkill == null || activeNodeIds.Count == 0) return;
    foreach (SkillRuntimeNode node in _activeSkill.Nodes)
    {
        if (!activeNodeIds.Contains(node.NodeId)) continue;
        ExecuteRejectNode(node);  // 调 executor.ExecuteReject
    }
}
```

## 5. 完整数据流

```
1. Owner 按下技能键 → BattlePlayerInput 缓存 Press
2. Tick: PerformReplicate → SkillController.TickClientPrediction
   → TryStartSkill → TickDomain → ProjectileClip OnStart
     • ContainsReplayed? → skip
     • 旁观者? → skip
     • IsOwner → ProjectileManager.SpawnVisualLocal(id, params)
                → MF.ObjectPool.Spawn → 立即实例化 VisualProjectile，0 RTT 手感
                → _clientVisuals[id] = v
                → _visualBySequence[sequenceId].Add(v)
     • IsServerStarted → ProjectileManager.SpawnAuthority(id, params, attacker, conn)
                → ValidateSpawn 校验
                  - 通过: _serverProjectiles.Add(new Projectile{...})
                          SpawnProjectileRpc → spectator 生成视觉
                  - 失败: SkillManager.RejectSkill(conn, seqId, skillId)
                          → 走通用拒绝链路（见 5a）

3. 后续 tick: ProjectileManager.TickServer
   • 推进每个 _serverProjectiles 位置
   • Physics.SphereCast(prev → curr, radius, hitMask)
   • 命中玩家 → ApplyDamage（走 BattleDamageDispatcher）
              → OnServerImpact（_impactHandlers，未来爆炸/buff）
              → ImpactProjectileRpc(hitPoint, targetNetId, hit=true) → 所有客户端
   • 命中墙 → ImpactProjectileRpc(hitPoint, hit=true) → 所有客户端
   • 寿命到期 → ImpactProjectileRpc(lastPos, hit=false) → 所有客户端

4. 客户端 Update: VisualProjectile.Update
   • 按 Params 本地推进（Time.deltaTime 平滑）
   • drift 只影响飞行中视觉

5. ImpactProjectileRpc 到达所有客户端
   • 查 _clientVisuals[id]
   • hit=true → SnapToImpact(hitPoint, hitNormal) + 播命中特效 + 归池
   • hit=false → 直接归池
   • 从 _visualBySequence / _spawnedIds 移除

6. BattleDamageAppliedEvent 下一帧分发
   → UI / 命中反馈 / 未来 buff 系统订阅

兜底:
   • 视觉 projectile 超 maxLifetime + RTT 容差仍存活 → TickClient 强制归池
     (防 ImpactRpc 丢包)
   • _spawnedIds 防止 reconcile replay 重放 OnStart 重复 spawn
```

### 5a. 通用技能拒绝链路（服务器检测失败时）

```
服务器侧:
  SkillController.TickReplicate 检测失败
    - !CanAct + Press 命令（晕眩/死亡/沉默）
    - TryStartSkill 里 skill == null
    - 未来：cooldown / mana 失败
  → 调 Services.SkillManager.RejectSkill(ownerConn, seqId, skillId)
  → SkillManager [Server] RejectSkill:
      • MF.Event.Fire<SkillRejectedEvent>(seqId, skillId, Unknown)
        → 服务器侧跨系统订阅（UI 显示"技能失败"等）
      • TargetRpc RejectSkillRpc(conn, seqId, skillId)

  ProjectileManager.SpawnAuthority 校验失败（spawn 级拒绝）
  → 调 SkillManager.RejectSkill（统一入口，同上）

客户端侧（owner 收到 RejectSkillRpc）:
  SkillManager.RejectSkillRpc:
    路径1: SkillController.HandleSkillRejected(seqId)
           → 校验 _activeSequenceId == seqId
             - 技能还在跑 → 对 active 节点调 Executor.OnReject
               → ProjectileClipExecutor.OnReject → RejectVisualsForSequence（幂等）
               → 未来 VfxClipExecutor.OnReject → 停特效
               → 未来 AnimClipExecutor.OnReject → 停动画
               → MoveVelocityClipExecutor 不 override（Motor reconcile 自动处理）
             - 技能已结束 → 忽略（路径2 兜底）
           → 不清 activeNodeIds，等 reconcile 自然处理

    路径2: 遍历 IRejectHandler 列表
           → ProjectileManager.OnRejectSkill(seqId)
             → RejectVisualsForSequence(seqId)
             → 查 _visualBySequence 淡出（不管技能状态）
           → 未来其他 Manager 实现 IRejectHandler

    MF.Event.Fire<SkillRejectedEvent> → 客户端侧跨系统订阅

  幂等保证:
    路径1 和路径2 都调 RejectVisualsForSequence(seqId)
    视觉不存在则无操作，重复无害

  服务器 reconcile 后到达客户端:
    → ApplyState 抹掉 _isActive → ClearActiveNodeIds 自然清理
```

## 6. 回滚场景覆盖矩阵

| 场景 | 服务器权威 projectile | 客户端视觉 projectile | 处理 |
|------|----------------------|----------------------|------|
| 服务器拒绝技能启动（!CanAct / skill==null） | 不 spawn | 已 spawn | 通用 RejectSkill → 路径1（技能还在跑）+ 路径2（不管技能状态）→ 淡出归池 ✓ |
| 服务器拒绝 spawn（位置不合法） | 不 spawn | 已 spawn | SpawnAuthority 调 RejectSkill → 同上 ✓ |
| 技能已结束 + Reject 晚到 | 不影响 | 视觉在飞 | 路径1 校验失败忽略；路径2（IRejectHandler）查 _visualBySequence 淡出 ✓ |
| spawnTick 被修正（晚 N tick） | 用新 tick | 用旧 tick | 不撤销，drift 靠 ImpactRpc 的 hitPoint snap 兜底 ✓ |
| Cancel 取消技能 | 服务器不停止已飞 projectile | 视觉继续飞 | **不撤销** —— 发射后技能立即结束是正常设计 ✓ |
| reconcile replay 重放 OnStart | 不再 SpawnAuthority（服务器非 replay） | 被 `ContainsReplayed` 拦截 | `_spawnedIds` 双保险去重 ✓ |
| ImpactRpc 丢包 | 不影响服务器 | 视觉继续飞 | maxLifetime + RTT 容差强制归池 ✓ |
| 客户端自己 reconcile 抹掉 `_isActive` | 不影响 | 不影响 | 极罕见；maxLifetime 兜底 ✓ |
| RejectSkillRpc 丢包 | — | 视觉继续飞 | maxLifetime 兜底；TargetRpc 走 Reliable 概率极低 ✓ |
| 技能无 ProjectileClip | — | 无视觉 | Reject 发出但 _visualBySequence 无记录 → 无操作 ✓ |

## 7. ID 分配

| 类型 | ID | 备注 |
|------|-----|------|
| ProjectileClip | 1009 | 新增，跟在 MultiDamageClip=1008 后 |
| Executor | Battle.ProjectileClipExecutor | ClientPrediction 域 |
| 无新 SpecialData | — | 不新增数据黑板类型 |

## 8. 实施顺序

1. `ERejectReason` + `SkillRejectedEvent`（事件，无依赖）
2. `IRejectHandler`（接口，无依赖）
3. `IProjectileImpactHandler`（接口，无依赖）
4. `ProjectileIdentity` + `ProjectileParams` + `Projectile`（纯 struct，无依赖）
5. 改 `BattleSkillNodeExecutor` 加 `OnReject` virtual（根基类）
6. 改 `ClientPredictionSkillExecutor` / `ClientOnlySkillExecutor` / `ServerOnlySkillExecutor`（sealed override 分发）
7. `SkillManager`（场景级 NetworkBehaviour，依赖 1/2）
8. `ProjectilePrefabDatabase` ScriptableObject + `VisualProjectile`（依赖 4）
9. `ProjectileManager`（核心，依赖 4/8，实现 IRejectHandler 自动注册到 SkillManager）
10. 改 `SkillController`（服务器检测失败调 RejectSkill；客户端 HandleSkillRejected；RejectDomainNodes）
11. 改 `SkillRuntimeServices`（加 _skillManager + _projectileManager 引用）
12. `ProjectileClip`（Slate clip，无 Battle 依赖）
13. `ProjectileClipExecutor`（依赖 4/9，override OnStart + OnReject）
14. 用户跑代码生成（见第 9 节）

## 9. 用户需要手动操作的步骤

实施完成后，按顺序在 Unity 里执行：

1. `Tools/Hoshino/Generate Skill Serialization Code`
2. `Tools/Battle/Generate Skill Executor Bindings`
3. `Tools/Hoshino/Compile All Skill Definitions`
4. 创建 `ProjectilePrefabDatabase` ScriptableObject 资源
   （`Create > Battle > Projectile Prefab Database`），配置 prefabId → prefab 映射
5. 制作 1~N 个 projectile 视觉 prefab（挂 `VisualProjectile` 组件，组件实现 `MemoFramework.ObjectPool.IObject`）
6. 场景里挂一个 `ProjectileManager` GameObject，引用 PrefabDatabase
   - `ProjectileManager` 启动时会用 `MF.ObjectPool.CreateObjectPool` 为每个 prefab 注册池
   - `ProjectileManager.Awake` 自动注册到 SkillManager 的 IRejectHandler 列表
   - 无需手动配置对象池
7. 场景里挂一个 `SkillManager` GameObject（场景级单例，专管技能网络 RPC）
8. 在 `SkillRuntimeServices` 上引用 SkillManager 和 ProjectileManager
   （或让它们自动 `FindFirstObjectByType`）
9. 在 `NetworkManager` 的 `ServerManager` 组件上**无需**勾选 `Allow Predicted Spawning`
   （本方案不用 FishNet PredictedSpawn，走自定义 RPC）
10. SkillEditor 里新建技能，拖一个 `ProjectileClip` 到 tick 0，配参数，编译出 .bytes
11. .bytes 拖到 SkillController 技能槽

## 10. 预留扩展点清单

| 扩展点 | 当前状态 | 未来接入方式 |
|--------|----------|--------------|
| 抛物线弹道 | `Gravity` 字段已留 | TickServer 推进时 `+ Gravity * delta`；VisualProjectile 同步加 |
| 追踪导弹 | `HomingTargetNetId` 字段已留 | TickServer 每 tick 调整 Velocity 方向朝目标 |
| 爆炸 AOE | `ExplosionRadius`/`ExplosionDamage` 字段已留 | 实现 `IProjectileImpactHandler`，在 `OnProjectileImpact` 里调 `LagCompensatedHitResolver.ResolveDamageSphere` |
| Buff 触发 | `BuffIdOnHit` 字段已留 | 未来 buff 系统实装后，在 `OnServerImpact` 里 `if (BuffId != 0) BuffSystem.Apply(target, BuffId)` |
| 连锁闪电 | 无 | 实现 `IProjectileImpactHandler`，在 impact 时再 spawn 新 projectile |
| 命中反馈 | 无 | 订阅 `BattleDamageAppliedEvent`（既有机制），在 SourceClipId == ProjectileClip 时播放命中特效 |
| VFX/Anim 拒绝回收 | Executor.OnReject 已留 | 未来 VfxClipExecutor/AnimClipExecutor override OnReject，停止特效/动画 |
| 拒绝原因细化 | ERejectReason 只有 Unknown | 未来扩展 CanAct/SkillNotFound/Cooldown/Mana 等，UI 可显示具体原因 |
| 其他产物拒绝回收 | IRejectHandler 已留 | 未来任何"产物 > 技能生命周期"的 Manager 实现此接口注册到 SkillManager |

## 11. 性能预估

- 服务器 N 个 projectile = 1 个 manager + N 个 struct，每 tick N 次 SphereCast
- 客户端 N 个视觉 projectile = N 个 MonoBehaviour Update，对象池复用（MF ObjectPoolComponent）
- RPC：spawn 1 次/发射 + impact 1 次/消失 + reject 极少，Unreliable/Reliable 混合
- 无 NetworkObject 同步开销，无 SyncVar 带宽
- 复用 MF 对象池避免重复造轮子，IObject 接口统一 lifecycle
- 拒绝链路：TargetRpc 精确发 owner，不广播；MF.Event 仅在拒绝时分发

## 12. 风险与待验证点

| 风险 | 严重度 | 缓解 |
|------|--------|------|
| `SphereCast` 在高速 + 小半径下仍可能穿透 | 中 | HitRadius 设最小阈值；必要时改用 `Raycast` + `OverlapSphere` 双检 |
| 视觉 projectile drift 在长飞行时间下累积 | 低 | ImpactRpc 的 hitPoint snap 兜底；飞行时间一般 < 2s |
| RejectSkillRpc 走 Reliable，可能晚到 | 低 | 路径2（IRejectHandler）不依赖技能状态，晚到也能回收；maxLifetime 兜底 |
| 客户端 _spawnedIds / _visualBySequence 永不清理会内存泄漏 | 中 | 需在 ImpactRpc / RejectVisualsForSequence / maxLifetime 兜底里清理对应 entry |
| 多 projectile clip 同 tick spawn 时 NodeId 唯一性 | 低 | SkillRuntimeNode.NodeId 由编译器保证全局唯一 |
| SkillManager.FindLocalSkillController 实现细节 | 中 | 需确认项目里 owner player 的定位方式（FishNet LocalConnection） |
| 服务器侧 SkillController 调 RejectSkill 时 SkillManager 可能为 null | 低 | ResolveServices 已有自动查找 fallback；null 时跳过拒绝（极罕见） |
| 两条路径重复调 RejectVisualsForSequence 性能 | 低 | 幂等，第二次查 _visualBySequence 无记录直接返回 |
