# 城市记忆信使

> **历史美术原稿说明。** 当前人物已使用 Athlete 网格与后续 Flow 动作，保留此处原稿、源模型和网格作为现有资源的制作来源。旧 `LayeredMemoryRunnerInstaller` 已于 2026-09-28 删除，可从 Git 历史查阅；不要将下文历史绑定记录作为当前版本恢复步骤。当前版本应保留仓库中的 Athlete 网格、材质和场景绑定，动画恢复见 `docs/RunnerMotionDependencies.md`。

与层叠记忆街区配套的贴身跑者服装。用柠檬黄主体、靛蓝活动区、淡紫折领和嵌片建立与场景的联系，背部使用圆弧软包形记忆匣。肩袖和躯干连成一张曲面，裤装采用连续的胯部、膝部与小腿轮廓。已去掉方形胸袋、裤袋和膝部色块，保留短帽檐、窄袖口、斜肩带、少量缝制标识与鞋带。

## 源与绑定

- `Tools/Blender/build_memory_courier.py`：Blender 离线建模脚本。
- `MemoryCourier.blend`：保留 38 个可编辑部件；导出时才合并副本，不把建模部件直接堆进游戏。
- `Assets/Art/LayeredMemory/Models/MemoryCourier.fbx`：一张带权重的服装网格与原始 Mixamo 骨架。
- `Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing.asset`：Unity 离线转入场景空间后的运行时网格。
- 历史上由 `LayeredMemoryRunnerInstaller.Install` 将网格绑定到场景原来的骨骼，不复制骨架；该旧安装器现已删除。

场景中的 `OrangeEchoOutfit/OE_OrangeEchoClothing` 名称是已有的换色和回声克隆接口，因此继续保留。它不是当前美术方向的名字。六个 `OE_*` 材质沿用现有层叠记忆配色；材质名中的历史 Orange 命名也不意味着角色仍使用橙色。

原头部、面部、手部、Avatar、Animator Controller 和动作文件继续使用项目内的 Exo Gray / HumanMotion 资产。此次没有重建人物脸型。来源与授权说明沿用项目的 `THIRD_PARTY_NOTICES.md`。服装和装备为本项目离线制作，没有下载新的第三方模型或贴图。

## 约束

- 不更换 player 根对象、CharacterModel、碰撞体、根运动设置或控制器引用。
- 一张服装网格、六个已有材质槽，网格预算不超过 11,000 三角面。没有额外材质、贴图、光源、布料模拟或运行时造型脚本。
- 可编辑源骨架有 112 根骨骼；每次导出比较原始骨骼矩阵，导入后再映射到场景骨骼。头饰绑定 Head，记忆匣绑定 Spine2，衣裤跟随原有躯干和四肢权重。
- 原有回声通过克隆 CharacterModel 获得相同造型，颜色及学习规则仍由原流程控制。
- 轮廓修订直接调整衣服截面和附件尺寸，不整体缩放角色或移动骨骼。袜口在原 Leg / Foot 骨骼间过渡，覆盖脚踝弯折时的衣鞋接缝；保留原脚长和接地位置。
- 上衣与裤装的体素合并、表面松弛和减面只在 Blender 离线执行。导出前检查两张主体网格均为一个连通块、零非流形边；实际运行不再合并或细分网格。肩部和胯部权重沿用原骨骼，并在新连续表面上平滑过渡。
- `courier-manifest.json` 记录源网格规模、连通性和实际截面尺寸。`silhouette_measurements()` 用网格边与截面的交点测量，不能用减面后稀疏顶点的邻域代替截面，也不通过镜头变化宣称轮廓改善。
- 生成器不覆盖旧 OrangeEcho 源模型。它可以重建本历史版本的源模型，但旧 Install Courier 步骤已弃用；重新导出源模型不应覆盖当前已验收的 Athlete 场景绑定。

## 验证

`LayeredMemoryRunnerReview.CaptureAfter` 用实际 Animator 状态求值和强制更新蒙皮矩阵采集跑步、跳跃、滑铲三个时间点的侧后视图。这些是静态动作采样，不能代替游戏中的输入与动作衔接。

`LayeredMemoryRunnerReview.BuildWindows` 用于构建历史独立存档产品 `EchoRun-LayeredMemory-RunnerFormReview`。连续曲面修订说明见 `docs/2026-09-24-memory-courier-form.md`；当时的结果位于 `TestResults/LayeredMemoryRunnerForm-20260924/`。这些旧构建和中间截图已在 2026-09-28 清理，历史文档保留作为制作记录；不能用当时的结果证明当前版本通过。
