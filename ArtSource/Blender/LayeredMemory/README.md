# 层叠记忆：正式场景资源

方向依据：`ArtSource/VisualDirection/LayeredMemory-20260924/gameplay-keyframe-v2.png`。
首段可运行资源已继续细化为两种街屋和拱券高架，尚未完成整座城市的模型更新。

## 模型

- 源文件 `LayeredTerraceQuarter.blend` 包含高台街屋和低台拱廊两个独立根对象；由 `Tools/Art/create_layered_memory_quarter.py` 在 Blender 离线制作。
- 导入文件 `Assets/Art/LayeredMemory/Models/LayeredTerraceQuarter.fbx` 和 `LayeredGalleryQuarter.fbx`，各 7 个按材质合并的网格，共享原有的 7 个材质。
- 细化版 Blender 导出前分别为 18,160 和 17,448 三角面；Unity 导入后的实际数字在 `TestResults/LayeredMemoryDetail-20260924/`。导入器三角化会造成小幅差异。
- Blender Z 向上、-Y 朝街；Unity Y 向上、+Z 朝街。模型以街面为高度基准，地下为封闭基座。
- 高台街屋尺寸约 18.6 × 45.36 × 18.2 米，地上最高 23.46 米；低台拱廊约 18.6 × 41.76 × 18.2 米，地上最高 19.86 米。两者地下基座最低均为 -21.9 米。
- 退台、窗洞、拱形门框、屋外折返楼梯、屋顶花槽和栏杆均为正式网格。无碰撞体、脚本、骨骼或动画。
- 细化版增加雨棚、折边、百叶窗、阳台托座、落水管、屋顶水箱、灯具、遮阳棚和垂落植物。花槽底面落在实际屋面上；收边延伸包住板材端部，避免异材质共面闪烁。
- 模型和材质由本项目生成，无外部下载模型。复用项目现有 Blender 导出工具，不改写它原有的资源。

## 高架

`Tools/Art/create_layered_memory_rail.py` 输出 `LayeredArchViaduct.blend`、24 米的 `LayeredArchViaduct.fbx` 和 1 米源长的 `LayeredRailCurve.fbx`。直桥含分段石拱、桥栏、枕木和无独立光源的灯具。

`LayeredMemoryRailInstaller` 从现有 `UpperTransit` 的 `CityTransitLoop.pathPoints` 读取路径，把正式桥段沿原路径放置并离线合并为 4 个持久网格。只替换 `StaticRoot`；列车、路线、速度、相位和音源保持原样。弯道仅缩放视觉连接段的长度，宽度及轨顶高度不变。

桥底最低约 8.65 米，仍高于玩法/相机净空。单组静态桥体由 196,160 降至 38,016 三角面；含原来的 3 节列车为 78,600 三角面（原 236,744）。静态 Renderer 从 3 个变为 4 个；面数减少不是手机性能验收。

## 材质和贴图

`Assets/Art/LayeredMemory/Textures/` 的两张贴图通过内置 image_gen 生成；提示词见 `texture-prompts.md`。导入为 1024、重复采样、MipMap、非 CPU 可读，使用已有 `EchoRun/MineralCladding` 着色器。

灰泥按不同墙面着色；铺石只用于正式道路视觉网格。两者不使用旧金属板法线细节。所有模型面均有有效 UV 和切线，避免世界投影纹理看似正常而光照出错。

## Unity 接入

`Tools → Echo Runner → Layered Memory → Install Art` 是当前完整安装入口，对应 `LayeredMemoryArtInstaller.Install`。

它更新共享材质，在 Chunk0/4 引用高台街屋、Chunk2/7 引用低台拱廊，并更新高架静态网格。现有 CityV7 对象池和转角净空逻辑继续管理可见性，不增加运行时几何生成器。

`LayeredMemoryPalette` 是当前调色入口；旧 `NightGrapeWorldInstaller` 仅作兼容转发。四个既有街区的色值由 `StackedCityDistrictMaterials` 统一提供。历史场景重建工具可能重建资源，因此完整重建后要最后运行当前安装入口。

已有道路 Prefab 根节点、车道、碰撞体、相机跟随、角色骨骼、AI 学习/决策、冻结挑战和存档逻辑不属于本资源的修改范围。`AIShadowRunner` 仅调整身体颜色、透明度和轮廓宽度，让淡紫回声在实际道路上可辨认。

## 验证入口

- `LayeredMemoryReview.CaptureBefore / CaptureAfter`：当前道路和角色的固定机位渲染，不等同于真实操作。
- `LayeredMemoryReview.CaptureDetailBefore / CaptureDetailAfter`：细化前后的同机位对照。
- `LayeredMemoryReview.BuildDetailWindows`：独立的细化版验证包，输出到 `TestResults/LayeredMemoryDetail-20260924/Windows/`。
- CityV7 转角遮挡、城市表面、共享材质和 HUD 字符测试检查现有边界。Windows 诊断跑动另行记录。

当前远景楼群、下层街区和转角旧设施仍有待继续精修；手机端预算及人工跑动验收需要另行检查。不得将概念图、Blender 单模型预览或一次构建成功当成最终视觉验收。
