# MemoFramework_Net3C

`MemoFramework_Net3C` 是 MemoFramework 的可选联网 3C 包。它依赖 `MemoFramework_3C` 和 `MemoFramework_Net`，只提供 FishNet 客户端权威移动适配，不复制非联网移动逻辑。

## Requirements

导入顺序：

1. `Tools/MemoFramework/Setup -> Setup Core Dependencies`
2. 导入 `MemoFramework_Net`
3. 导入 `MemoFramework_3C`
4. 导入 `MemoFramework_Net3C`

## Runtime

- `MemoNetLocalAuthorityMotor`：按 FishNet ownership 开关同物体上的 `MemoCharacterMotor3C`。
- `MemoNetTopDownMotor`：要求同物体有 `MemoTopDownMotor3C`。
- `MemoNetFirstPersonMotor`：要求同物体有 `MemoFirstPersonMotor3C`。
- `MemoNetThirdPersonMotor`：要求同物体有 `MemoThirdPersonMotor3C`。
- `MemoNet3COwnerCameraActivator`：只在本地 owner 端激活关联 Cinemachine 摄像机，避免远端玩家抢相机。

## Network Setup

玩家 prefab 需要：

1. `NetworkObject`
2. `NetworkTransform`
3. `CharacterController`
4. 一个 `MemoFramework_3C` 基础 Motor
5. 对应的 `MemoFramework_Net3C` 网络 Motor

`NetworkTransform` 推荐使用 client-authoritative，开启 Position/Rotation，同步组件配置选择 `CharacterController`。

这个包面向好友派对/轻竞技原型，不包含反作弊、服务器权威校验、预测回滚、战斗、分数或回合状态。
