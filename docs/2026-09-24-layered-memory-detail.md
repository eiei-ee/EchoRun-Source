# 层叠记忆场景细化

本轮继续已认可的矿物蓝 / 石榴红城市方向，重点是跑动中能看见的街屋与高架。没有改变主题配色、游戏规则或云端状态。

## 交付内容

- 原高台街屋增加阳台与托座、浅雨棚和折边、百叶窗、落水管、屋顶水箱、门灯、遮阳棚及垂落植物。
- 新增低台拱廊街屋，与高台版共用 7 个材质。Chunk0/4 使用高台版，Chunk2/7 使用拱廊版；街道占地和既有转角剔除方式保留。
- 原平直高架改为浅拱券结构，补齐栏杆、检修道、石质压顶和小灯具。桥面宽度、轨顶高度、列车路径和运动逻辑保持原样。
- 模型由 Blender 离线制作并导出 FBX，Unity Editor 只处理导入、绑定及静态合并。没有新的运行时几何生成器、物理碰撞体或额外实时灯光。

主要文件：`Tools/Art/create_layered_memory_quarter.py`、`create_layered_memory_rail.py`；`Assets/Editor/LayeredMemoryArtInstaller.cs`、`LayeredMemoryRailInstaller.cs`；`Assets/Art/LayeredMemory/Models/` 与 `Meshes/`。

完整安装入口仍是 `Tools → Echo Runner → Layered Memory → Install Art`。重复安装保留已有 Prefab holder 和网格资产身份；列车的 `LightRail` 子树不参与重建。源模型、清单和预览见 `ArtSource/Blender/LayeredMemory/`。

## 实测资源开销

| 项目 | 细化前 | 细化后 |
| --- | ---: | ---: |
| 高台街屋 Unity 三角面 | 8,904 | 17,952 |
| 低台拱廊 Unity 三角面 | — | 17,300 |
| 单组静态高架三角面 | 196,160 | 38,016 |
| 单组高架含三节原列车 | 236,744 | 78,600 |
| 静态高架 Renderer | 3 | 4 |

高架桥体面数减少约 80.6%，包含列车的整组减少约 66.8%；新街屋增加局部细节成本。没有增加纹理或材质资产。原列车使用另外 5 个材质，高架用街屋共享材质中的 4 个，因此整组高架的唯一材质数由 5 变为 9。以上是模型统计，不是帧率或手机性能结论。

## 验证范围

证据目录：`TestResults/LayeredMemoryDetail-20260924/`。

- `Before/Composition/` 和 `After/Composition/` 为同一套真实资产、同机位的场景对照，不是手动游玩验收。
- `train-preservation.json` 比较了 `LightRail` 子树的 77 个序列化对象/组件，均与本轮开始前逐字相同，包括列车、路径、速度及音源。
- `Baseline/protected-files.csv` 记录本轮开始前 104 个运行时源文件、玩法道路 Prefab、主场景和项目设置的哈希；构建后再次比较，104 个全部不变，见 `protected-files-verification.json`。
- 新的 `LayeredMemoryArtTests` 检查模型预算和正式资产引用；通过测试专用的临时 MeshCollider 射线，确认实际合并网格在每个列车路径弦段的两侧轨顶处有支撑，并验证桥体最低处高于相机净空。
- 首轮相关 EditMode 82 项中 78 项通过，4 项报告新雨棚及拱廊檐口的异材质端面重合。模型已改为包边搭接形式，不放宽既有表面审计。修复后同组 **82/82 通过**，见 `09-city-editmode.log.xml`；重新导入与测试日志均未发现 C# 或 shader 编译错误。

Windows 开发包构建成功，见 `10-windows-build.log`。包使用独立产品名 `EchoRun-LayeredMemory-DetailReview`，隔离正式存档。直接打开 `Windows/EchoRun.exe` 是正常玩法；`Run-Player.ps1` 启用的固定种子视觉诊断为持续观察场景而关闭障碍碰撞。诊断只能证明该路段的呈现、换道和转角连续性，不代表正常碰撞、完整流程或手机端验收。

### 实际运行结果

| 观察路线 | 距离 | 转弯 | 道路缺失帧 | 净空越界采样 | 运动列车 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1280 × 720 | 500.17 米 | 2 | 0 | 0 / 21 | 27 / 27 |
| 540 × 1080 | 500.01 米 | 2 | 0 | 0 / 21 | 27 / 27 |

两条路线覆盖左、中、右三车道，上下层环境实例均持续存在。日志没有发现运行时异常或 shader 错误。数据来自 `Runtime-1280x720/report.txt` 与 `Runtime-540x1080/report.txt`；观察路线采用定时采样，不能推导出所有相机位置都无任何遮挡。诊断帧率不作为性能验收指标。

已逐张检查两种画幅的 `distance-012.png` 和转弯截图，以及横屏 `distance-070.png`。拱桥、拱廊、楼梯、阳台与挂绿在实际跑动中可见；未在这些帧中观察到新增建筑遮住车道。远景与重复块的旧轮廓仍然明显。

预览包：`TestResults/LayeredMemoryDetail-20260924/Windows/EchoRun.exe`。`windows-build-hashes.json` 保存了 EXE、GameAssembly.dll、游戏数据和 app.info 的 SHA-256。最后仅清理了四个本轮街区 Prefab 新增序列化空值的 28 处尾空格；模型引用、数值和实际构建内容未变。

## 保留项

远景天际线、下层街区和旧转角设施还有重复轮廓。此次没有更换全部旧建筑，也没有改主菜单版式、角色动作、AI 学习、异步挑战、结算和存档实现。微信 / WebGL 尚需在正式目标上重新构建和检查；没有部署、推送或升级云套餐。
