# 记忆信使轮廓收形

> **历史记录（2026-09-24），不作为当前版本的恢复步骤。** 当前人物已采用 Athlete 网格，Jump / Land 已采用后续 Flow 动作；当前依赖与恢复流程见 [RunnerMotionDependencies.md](RunnerMotionDependencies.md)。下文保留当时的尺寸、验证结果、命令和原路径，不代表当前版本状态。旧 `TestResults` 产物先归档，后于 2026-09-28 按用户要求清理；不保证历史构建和中间截图仍可查阅，存留说明见 [RepositoryOrganization.md](RepositoryOrganization.md)。
>
> `LayeredMemoryRunnerInstaller.InstallAndCapture` 属于旧版服装安装流程，该安装器已于 2026-09-28 删除，可从 Git 历史查阅。下文命令仅记录当时的制作过程，当前仓库不再提供该入口；当前人物和动画恢复请使用 [RunnerMotionDependencies.md](RunnerMotionDependencies.md) 所述流程，不要执行历史安装步骤。

用户指出上一版人物仍然臃肿。实机侧后视图显示，圆厚袖管、直筒外套、宽裤腿和突出的背包共同放大了体积。这次直接重塑服装截面与装备厚度，保留已确定的城市与人物配色。

## 修改

- 外套收腰并削薄胸背；上臂改为窄袖管，袖口、前臂、腕部饰件同步调整。
- 记忆匣缩为贴背的小匣，肩带和胸袋按新衣面重新贴合。
- 裤腿、裤袋和鞋身收窄，移除两条突出腿部轮廓的装饰线。
- 领口保留覆盖余量，袜口在原小腿与脚部骨骼间过渡，避免衣鞋之间露缝。
- 原头脸、手部、身高、骨架、动画和脚长保持原样。此轮没有修改游戏控制、碰撞、AI、存档、界面或场景设计。

下列尺寸直接测量同一坐标系下的 Blender 源网格截面，不是屏幕像素或人物整体缩放。原件当时保存在 `TestResults/LayeredMemoryRunnerSlim-20260924/Baseline/`；该历史中间产物已在 2026-09-28 清理。生成器将测量写入 `courier-manifest.json`。

| 部位 | 前一版 | 收形后 | 减少 |
| --- | ---: | ---: | ---: |
| 外套胸背厚度 | 25.20 cm | 18.80 cm | 25.4% |
| 外套腰宽 | 37.07 cm | 29.50 cm | 20.4% |
| 上臂袖管厚度 | 16.98 cm | 11.38 cm | 32.9% |
| 大腿裤管厚度 | 23.14 cm | 17.35 cm | 25.0% |
| 小腿裤管宽度 | 13.61 cm | 10.41 cm | 23.5% |
| 记忆匣主体厚度 | 8.00 cm | 3.20 cm | 60.0% |
| 鞋身最大宽度 | 13.16 cm | 10.37 cm | 21.2% |

源骨架的 112 根骨骼矩阵经导出器逐一核对未变。脚底最低点的差异小于 0.04 mm，来自保留的细微布面起伏；角色高度、脚长、碰撞体与接地控制未改。

## 复现

1. Blender 后台执行 `Tools/Blender/build_memory_courier.py`，生成 `.blend`、FBX 与清单。
2. 团结编辑器执行 `LayeredMemoryRunnerInstaller.InstallAndCapture`，更新已有持久网格并生成动作采样。
3. 执行 `LayeredMemoryRunnerReview.BuildWindows` 构建本轮独立预览。

证据根目录是 `TestResults/LayeredMemoryRunnerSlim-20260924/`。`Before/` 对应用户反馈时的原模型；`Iteration1/` 对应首轮收形，`After/` 对应最终采样。不要拿前一轮的构建、测试或截图证明本轮通过。

## 当前资产与回归

- Unity 最终导入服装为 6,968 三角面、7,430 顶点，含保留的头手后共 19,352 三角面。对比上一版全角色 19,274 三角面增加 78 面，主要来自补齐脚踝衔接；并未增加 renderer、材质槽、贴图或运行时计算组件。
- 仍是 9 个启用的 SkinnedMeshRenderer、合计 14 个材质槽；其中衣服沿用原六种材质。
- 最终动作采样由 `04-final-install.log` 对应流程生成。检查了跑步侧面、背面、滑铲和跳跃采样，收形后移除了凸出的裤线，并补齐裤脚连接。
- 现有 26 项 EditMode 检查全部通过，`05-editmode.log.xml`：26 passed、0 failed、0 skipped。范围覆盖人物绑定、Run / Jump / Slide 变形、换色、镜头动作反馈与垂直动作等，未新增镜像实现的测试。
- `verify-scope.py` 对照本轮 378 个受保护文件。只有模型导入 meta 的顶点数、三角面数自动更新；同一个文件的 GUID 与其余导入选项保持一致。场景文件逐字节一致，游戏脚本、动画与受保护资源没有改动。详细清单见 `protected-files-verification.json`。

这些检查证明绑定和回归结果，不等于用户对造型的审美认可，也不是手机性能测试。

## 实机预览

`06-windows-build.log` 记录成功构建；预览可执行文件是 `TestResults/LayeredMemoryRunnerSlim-20260924/Windows/EchoRun.exe`，独立产品名为 `EchoRun-LayeredMemory-RunnerSlimReview`，与正式进度分离。

| 画幅 | 诊断距离 | 转弯 | 真实输入动作 | 道路缺失 / 净空越界 |
| --- | ---: | ---: | --- | --- |
| 1280 × 720 | 500.21 m | 2 | 2 次跳跃、2 次滑铲 | 0 / 0 |
| 540 × 1080 | 500.16 m | 2 | 2 次跳跃、2 次滑铲 | 0 / 0 |

两份 `Runtime-*/report.txt` 与运行日志记录完成状态，未发现运行异常或 shader 错误。已检查普通竖屏镜头的 `distance-012.png` 和滑铲帧，以及横屏的跑步侧后近景、跳跃与滑铲近景。普通镜头下跑者轮廓仍可辨识。

`close-*` 是保留实际姿态、场景和灯光的临时检查镜头；不是正式玩法镜头。自动诊断关闭了障碍碰撞，不能替代正常碰撞玩法、人工手感或手机性能验收。双击预览 EXE 不带诊断参数时进入正常游戏。

本轮没有构建或部署微信与 WebGL，没有提交或推送 Git。`validation-summary.json` 与 `windows-build-hashes.json` 记录本轮最终证据。
