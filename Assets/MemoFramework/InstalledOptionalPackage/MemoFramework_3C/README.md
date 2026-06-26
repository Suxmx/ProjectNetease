# MemoFramework_3C

`MemoFramework_3C` 是 MemoFramework 的可选 3C 包，包含非联网角色 Motor、输入扩展和 Cinemachine 3 摄像机跟随脚本。它不属于 MemoFramework 核心框架。

## Requirements

导入和使用前，先在 Unity Editor 执行：

1. `Tools/MemoFramework/Setup`
2. `Setup Core Dependencies`

核心依赖安装完成后才会有 Cinemachine 和 Input System。

## Movement

玩家角色 prefab 至少需要：

1. `CharacterController`
2. `MemoTopDownMotor3C`、`MemoFirstPersonMotor3C` 或 `MemoThirdPersonMotor3C` 三选一

核心基类 `MemoCharacterMotor3C` 提供走跑、跳跃、坡面、冲刺、爬墙、击退/外力，以及阶段覆写接口。默认移动输入兼容 `MemoFramework.Extension.InputData.MoveInput`。

## Sample Generator

可以在 Unity Editor 手动执行：

`Tools/MemoFramework/3C/Generate Local 3C Sample Assets`

生成内容：

1. `Samples/Local3C/Prefabs/Memo3C_TopDownPlayer.prefab`
2. `Samples/Local3C/Prefabs/Memo3C_ThirdPersonPlayer.prefab`
3. `Samples/Local3C/Scenes/Memo3C_LocalSample.unity`
4. `Samples/Local3C/Materials/*`

生成的场景包含 `MF` 根对象、`GameStateComponent` 样例 launcher、`InputComponent`、主相机、`CinemachineBrain`、`MemoThreeCLookInputProvider`、地面、障碍物、斜坡和可爬墙面。场景默认启用 `Mode_ThirdPerson_Active`，`Mode_TopDown_Inactive` 默认关闭；如果要测试 TopDown，关闭 ThirdPerson 根物体并启用 TopDown 根物体即可。

如果已经生成过旧版样例，修复或更新脚本后需要重新点击该菜单，旧场景和 prefab 才会被新的生成器配置覆盖。

## Camera

建议摄像机装配：

1. 主相机添加 `CinemachineBrain`。
2. 场景中添加 `MemoThreeCLookInputProvider`，写入 `MemoThreeCInputData.LookInput`。
3. 玩家上添加 `MemoCameraTarget3C`，配置 Follow/LookAt/Head 挂点。
4. CinemachineCamera 上添加 `MemoCinemachineCameraBinder3C`，绑定玩家的 `MemoCameraTarget3C`。
5. 需要自由旋转的相机再添加 `MemoCinemachineInputAxisController3C`。

推荐模式：

- TopDown：`CinemachineCamera + CinemachineFollow + CinemachineRotationComposer`
- ThirdPerson：`CinemachineCamera + CinemachineOrbitalFollow + CinemachineRotationComposer + MemoCinemachineInputAxisController3C`
- FirstPerson：`CinemachineCamera + CinemachineFollow + CinemachinePanTilt + MemoCinemachineInputAxisController3C`

第一人称和第三人称 Motor 会从 `MemoCinemachineCameraBinder3C` 获得主相机 Transform，用于相机相对移动方向。

## Notes

- 这个包不包含联网、预测、服务器校验或 FishNet 代码。
- Prefab、CinemachineCamera 组件参数、相机碰撞、肩切换和镜头震动按项目需求在 Unity Editor 中装配。
