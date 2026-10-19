# 层叠记忆城市：第一段可玩美术实现

日期：2026-09-24。当前工作区的开发预览，不是上线包。

## 视觉目标与实际范围

依据用户认可的 `ArtSource/VisualDirection/LayeredMemory-20260924/gameplay-keyframe-v2.png`，将矿物蓝、石榴红建筑，偏玫瑰紫的天空，柠檬黄玩家和淡紫回声接入实际跑酷场景。

这次完成正式街边建筑、表面材质、共享主题色及第一轮实机检查。高架和远景仍保留大部分旧模型，不能把概念稿的精细程度当作本版已经达到的效果。

### 资源与接入

- `Tools/Art/create_layered_memory_quarter.py`：Blender 离线制作入口，复用现有导出工具；只写新资源目录。
- `ArtSource/Blender/LayeredMemory/LayeredTerraceQuarter.blend`：可编辑源模型。
- `Assets/Art/LayeredMemory/Models/LayeredTerraceQuarter.fbx`：退台街屋、窗洞、拱形门、外楼梯、平台栏杆和花槽。Unity 导入后 8,904 三角面，7 个网格/共享材质，无碰撞体、骨骼和脚本。
- `Assets/Art/LayeredMemory/Textures/`：两张生成的灰泥/铺石纹理，导入上限 1024、MipMap、重复采样、非 CPU 可读。来源及完整提示词保存在 `ArtSource/Blender/LayeredMemory/texture-prompts.md`。
- Chunk0/2/4/7 通过序列化子对象 `LayeredMemoryQuarter` 引用同一个 FBX；替换对应一侧的旧屋顶花园。沿用 CityV7 分块池及转角净空处理。
- 现有道路视觉网格采用铺石材质；车道、道路根节点、连接点及碰撞几何不变。

### 代码职责

- `LayeredMemoryArtInstaller.Install` 是完整 Editor 安装入口：导入配置、材质、4 个分块引用、HUD/好友页重建和天空反射更新。可重复运行；不会重新生成玩法道路。
- `LayeredMemoryPalette` 是共享调色入口，保留原材质 GUID。旧 `NightGrapeWorldInstaller` 只作兼容转发，避免并行维护两套调色表。
- 四个现有街区使用 `StackedCityDistrictMaterials.PaletteRgb` 的同一色值表。
- `CityV7PlayableEnvironment` 仅调整天空、环境光、雾色及光源强度；`WorldStyler` 仅调整已有视觉角色的颜色。
- `EchoRunUITheme` 统一菜单、好友页和 HUD 主题色。HUD 观察文字区域由 34 提高到 40，容纳现有大字体设置；面板透明度保持既有可读性约束。
- `AITrainingDashboardUI` 仅把竖屏的「回声报告」入口移到标题右侧，修复实际截图中它与底部操作提示重叠的问题；报告内容及行为不变。
- `AIShadowRunner` 只改 `ResolveGhostBodyColor` 和 `_RimPower`，使回声有可读的半透明淡紫身体；没有调整决策、学习、节奏、动作选择或持久化。`EchoGhost.shader` 默认外观同步，未增加着色器采样或特效通路。
- `StackedCityReview` 的固定机位检查改用真实运行时的正式道路与障碍材质，避免诊断图仍显示旧的备用道路或未初始化材质。

历史场景生成工具可能覆盖其自己负责的资源；重建完整城市后，最后执行 `Tools → Echo Runner → Layered Memory → Install Art`。

## 本地验证

证据根目录：`TestResults/LayeredMemory-20260924/`。日志及构建产物不属于源资源。

### EditMode

| 记录 | 结果 | 说明 |
| --- | --- | --- |
| `06-editmode.log.xml` | 95 项中 93 通过 | 70 项城市/表面/转角/分区检查均通过；2 个 HUD 失败分别是面板过于不透明、大字体文字区域高度不足 |
| `08-hud-editmode.log.xml` | 25/25 通过 | 修正面板透明度与文字区域后重跑 HUD 两个相关测试组 |
| `11-hero-editmode.log.xml` | 3/3 通过 | 角色绑定、阴影和回声材质检查；回声颜色断言按新的可读淡紫方向更新，保留发光/扰动上限及减少动态效果的稳定性约束 |

共 98 个不同的相关检查在对应的最终修改后通过。没有运行整个仓库的全量测试。城市检查覆盖左右转角及三条车道的既有遮挡约束，未通过放宽断言处理失败。

### 构建与运行证据

Windows 开发包位于 `Windows/EchoRun.exe`，使用独立产品名 `EchoRun-LayeredMemory-Review`，使正常打开试玩包时的 PlayerPrefs/遥测目录也与正式游戏隔离。构建后恢复项目产品名。

`12-final-windows-build.log` 记录含最终场景/回声外观的成功构建；之后 `13-menu-layout-windows-build.log` 只补入上述菜单入口位置修正。运行截图与具体构建对应，不把更早截图当作后续版本重新验收。

构建 13 成功后，再次启动横屏和竖屏的正常菜单。`Windows/VisualCaptures/menu-1280x720.png`、`menu-540x960.png` 为最终菜单截图，日志 `menu-*-layout-fixed.log` 均出现 `ECHO_MENU_CAPTURE_COMPLETE`，未发现异常。竖屏报告入口已与底部提示分开。菜单验证为只读，未触发重置学习或云端操作。

`build-hashes.json` 保存最终 exe、GameAssembly.dll 与 `data.tj3d` 的 SHA-256；`Windows/试玩说明.txt` 说明正常打开方式和键盘操作。

最初的两轮实际运行分别位于 `Runtime-1280x720/` 和 `Runtime-540x960/`：均自然推进 500 米、经过 2 个转弯，执行左/中/右换道，报告未发现道路断层、上下城市层缺失或采样净空违规。它们促使我们进一步修正回声身体过淡的问题。

回声修正后的构建 12 重新完成两轮运行：

| 记录 | 距离 | 转弯 / 自动换道输入 | 道路、城市层缺失 / 采样净空违规 |
| --- | --- | --- | --- |
| `Runtime-1280x720-final/report.txt` | 500.1381 米 | 2 / 25 | 0 / 0 |
| `Runtime-540x960-final/report.txt` | 500.1086 米 | 2 / 23 | 0 / 0 |

两个日志均未发现异常或不支持的着色器记录。`distance-012.png` 是正常相机下的角色识别画面；`turn-1-left-after-0.3s.png` 是过弯后的实际视野。带 `inspection-down` 后缀的图使用诊断俯视相机，不代表玩家正常视角。

**运行诊断的范围**：复用既有固定身份/种子检查工具，为持续观察城市而关闭了障碍碰撞。截图来自真正运行的 Windows Player，但不是正常碰撞、完整 1500 米玩法、手感、手机性能或人工视觉验收的替代。诊断帧率受截图与开发构建影响，不作为性能结论。

`Before/Composition/` 与 `After/Composition/` 是真实资产的固定相机对照图，另有 Blender 单模型预览；两者都不能替代以上运行截图。

## 保护范围与后续

本轮开始时工作区已存在大量未提交修改。保留这些内容；通过 `Baseline/protected-files.csv` 和逐文件比较区分本轮视觉修改。未提交、推送、部署或恢复任何其他人的修改。

车道/碰撞、相机跟随、角色骨骼、AI 学习与冻结挑战规则、SingleContract 结算和双槽存档不在本轮修改范围。运行时源文件变化限于前述 5 个文件中的呈现部分；`protected-audit.csv` 保存 104 个文件的前后哈希比较。

仍需完成：

- 高架、远景与旧楼群的形体精修，逐步减少长条方盒和重复立面。
- 实际手机及微信竖屏安全区、材质/纹理预算和运行性能检查；本轮没有重新构建 WebGL 或微信小游戏。
- 正常游玩的碰撞、完整流程及人工视觉反馈。当前 Windows 预览是方向落地的第一段，尚非全项目美术完成。
- 好友云联调继续保持暂停；没有更改云套餐、权限或云端部署。
