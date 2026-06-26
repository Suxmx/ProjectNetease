# MemoFramework_Net3C Local Authority Movement Sample

这个 Sample 演示 `MemoFramework_3C` + `MemoFramework_Net3C` 的 FishNet 客户端权威移动：

- 本地 owner 读取 `InputData.MoveInput`，由 `MemoCharacterMotor3C` 驱动 `CharacterController`。
- FishNet `NetworkTransform` 用 client-authoritative 模式同步位置和旋转。
- 非 owner 默认关闭本地控制和 `CharacterController`。
- 本地 owner 可以通过 `MemoNet3COwnerCameraActivator` 激活自己的 Cinemachine 摄像机。

## Dependencies

导入顺序：

1. MemoFramework core dependencies
2. `MemoFramework_Net`
3. `MemoFramework_3C`
4. `MemoFramework_Net3C`

## MF Root

如果场景里没有 `MF` 根对象，需要在 Unity Editor 中手动添加：

1. 新建 GameObject，命名为 `MF`。
2. 添加 `MemoFramework.Extension.MF`。
3. 在 `MF` 下新建子物体 `GameStateComponent`。
4. 添加 `MemoFramework.GameState.GameStateComponent`。
5. 将 launcher 类型设置为 `MemoFramework.Net3C.Samples.LocalAuthorityMovement.MemoNetSampleLauncher`。

## Player Prefab

玩家 prefab 需要：

1. `FishNet.Object.NetworkObject`
2. `FishNet.Component.Transforming.NetworkTransform`
3. `CharacterController`
4. `MemoTopDownMotor3C`、`MemoFirstPersonMotor3C` 或 `MemoThirdPersonMotor3C`
5. `MemoNetTopDownMotor`、`MemoNetFirstPersonMotor` 或 `MemoNetThirdPersonMotor`
6. 可选：`MemoCameraTarget3C`、CinemachineCamera、`MemoCinemachineCameraBinder3C`、`MemoNet3COwnerCameraActivator`

推荐 `NetworkTransform` 设置：

- `Client Authoritative`：开启
- `Synchronize Position`：开启
- `Synchronize Rotation`：开启
- `Synchronize Scale`：关闭
- `Component Configuration`：`CharacterController`

## FishNet Scene

1. 场景中创建或复用 FishNet `NetworkManager`。
2. 添加 `FishNet.Component.Spawning.PlayerSpawner`。
3. 将玩家 prefab 登记到 FishNet prefab collection。
4. 将玩家 prefab 指给 `PlayerSpawner`。
5. 用 FishNet HUD、自己的 UI，或 API 启动 Host/Client。

## Verification

- Host 启动后本地玩家可以走跑、跳跃、冲刺、爬墙、受击退并转向。
- 第二客户端连接后，每个客户端只能控制自己的玩家。
- 远端玩家通过 `NetworkTransform` 显示位置和朝向变化。
- 本地 Cinemachine 摄像机只跟随 owner 玩家。

这个 Sample 不包含反作弊、服务器校验、预测回滚、战斗、分数或回合状态。
