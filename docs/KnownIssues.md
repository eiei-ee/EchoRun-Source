# 已知问题

## 右转筒仓与镜头的净空不足

**状态：2026-10-12 完成资源与生成配方修正，自动化几何验证通过；连续转弯画面与真人接受仍单独验收。**

本次仅将正式与样例右转的筒仓视觉节点 X 从 -16.5 移至 -24，不改变道路、碰撞及镜头。新增检查覆盖 16:9 和 9:16、高/低视角、速度后退及转弯角度；保留 3m 标准。完整 EditMode 967/967、PlayMode 68/68 通过。下文保留修正前的证据，不能将旧 0.96m 数值理解为当前位置。

### 已确认的证据

- `ColdWhiteFortressSampleTests.TurnLandmarksClearTheRearCameraShellOnBothSides` 的右转断言失败：镜头采样点到筒仓渲染包围盒的水平距离为 **0.959396362m**，低于既定 **3m** 净空标准。
- 本机测试记录：`TestResults/RunnerJumpRevision-20260928/Timing/all-editmode.xml`（忽略的验证产物，不随仓库分发）。失败发生于右转断言，不能据此判断后续左转断言的结果。
- 正式 `Assets/Prefabs/TurnSegment_Right.prefab` 仍启用 `OuterMemorySilo`，局部位置为 `(-16.5, 0, 10.2)`，朝向为绕 Y 轴 18°；与测试使用的 `Sample_TurnRight_20.prefab` 保持相同放置方式。
- 当前横屏高、低视角通过 `CameraFollow.ResolveLaneFramingOffset` 后，基础后退距离均为 **10.5m**，与该测试的横屏采样相符。该失败不能只归因于旧镜头参数。
- `WorldStyler.DecorateSegment` 保留右转筒仓，`OrangeEchoRoadVisuals.Apply` 只隐藏被替换的道路渲染器；`CityV7PlayableEnvironment.RefreshClearance` 仅遍历 `CityV7ChunkIdentity` 子树，未覆盖这个保留的筒仓。

### 证据边界

这里确认的是**静态镜头净空不足及正式资源仍受影响的调用路径**。尚未针对当前版本做 GPU 转弯实测，不能据此宣称所有视角已发生穿模，也未确定具体遮挡帧或频率。

竖屏镜头会根据宽高比进一步后退，应纳入验证。现有 `CityV7IntegrationTests.CityDoesNotOccludeCameraSweepAtTurn` 使用旧的 6.83m 后退距离，且仅检测 CityV7 子树，不能覆盖本问题。

### 最小后续修复

1. 用正式运行入口复现右转，记录筒仓与当前低、高视角的真实关系。
2. 若需要调整，仅将筒仓视觉节点移向弯外；保持跑道、碰撞、人物及已接受的镜头参数。
3. 通过 Unity Editor API 同步正式 `TurnSegment_Right.prefab`、`Assets/Resources/Art/Environment/ColdWhiteMemoryFortress/Sample_TurnRight_20.prefab`，并更新 `Assets/Editor/InstallColdWhiteMemoryFortress.cs` 的生成位置，防止重新安装后回退。
4. 不直接增大共享的 `TrackGeometryStandards.TurnNearDecorationCenterOffset`，因为它也参与左转旧场景的放置。

### 验收要求

- 净空测试保留 3m 标准，采用实际 `CameraFollow` 计算的横屏、竖屏及高、低档镜头位置，包含速度后退偏移。
- 验证正式左右转资源、进入转弯、转弯过渡及驶出阶段；确认路面和碰撞不变。
- 检查当前构建的真实渲染帧与连续转弯画面，确认无筒仓遮挡或穿入镜头。测试通过与玩家对画面的接受分别记录。
