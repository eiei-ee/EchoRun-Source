# 玩家体验样板 V1

目标：让现有 CityV7 实际跑道的建筑、天空远景、道路与角色、局内 HUD 形成一致的玩家体验。

## 第一阶段范围

- 保留现有建筑轮廓与正式模型，加入矿物板材纹理、金属窗框与少量暖光窗格。
- 用专门生成的 2:1 天空素材替代透视概念画的拉伸背景；复用现有远景模型制作环形城市轮廓。
- 调整环境补光、雾层次和路面底色，提高角色与路面的分离度。
- 预判提示靠近前方视线，常驻信息留在左上；受伤状态变色，暂停点击区域增大。
- 保留车道、碰撞、路线生成、角色控制、相机跟随位置/FOV 和 AI 规则。远裁剪距离为城市远景扩展到 420m。

## 资源与重建

- `Assets/Art/ExperienceSlice/`：两张生成素材、共享材质、烘焙窗框网格。
- `Assets/Resources/CityV7/ExperienceSky.mat`：全景天空，源地平线位于 0.5。
- `Assets/Resources/CityV7/ExperienceSkyline.prefab`：复用原有远景建筑制作的远景。
- 编辑器菜单：`Tools/EchoRun/Art/Install Experience Slice V1`。安装器使用 Unity 序列化 API 更新正式 Prefab，重复执行保留资源 GUID。
- 构建入口：`ExperienceSliceTools.BuildAfter`，输出 `TestResults/ExperienceSliceV1/After/EchoRun.exe`。
- 样板使用独立产品存档身份 `EchoRun-ExperienceSliceV1-Review`。

## 素材来源

两张纹理由本次内置 image_gen 工具生成，未使用第三方下载素材。原始生成结果保留在 Codex generated_images；生产副本位于项目内。

天空提示词：

> Create a production game sky texture, 2:1 aspect ratio, true full 360 by 180 degree latitude-longitude EQUIRECTANGULAR ENVIRONMENT MAP, not a perspective illustration. This is for EchoRun, a pale mineral futuristic city runner. Sky only: layered crisp but restrained blue-grey cloud banks at early blue hour, a muted warm ivory break of light low in one portion of the horizon, richer desaturated steel blue at zenith. The horizon is exactly at the vertical midpoint of the texture and continuous all the way across; entire bottom half is nearly uniform blue-grey atmospheric haze with no ground details. All cloud detail is in upper half; clouds get stretched horizontally near zenith as appropriate to spherical mapping. Left and right edges match seamlessly in brightness and cloud shapes. No buildings, no mountains, no ground, no roads, no characters, no text, no stars, no sci-fi rings, no sun disk. Painterly-realistic premium stylized game art with readable large cloud forms, subtle smaller wisps, clear layers and no blur filter. Gentle contrast so nearby buildings remain readable. Texture asset only, edge to edge, no frame. Wide 2:1 output, highest available resolution.

板材提示词：

> Production tileable square albedo texture for pale mineral architectural cladding in a premium stylized futuristic city game. Orthographic front view with perfectly even neutral diffuse lighting, no perspective, no cast shadow, no highlights. Restrained light warm grey limestone or ceramic concrete surface with extremely fine mineral grain and subtle tonal variation, a few tiny pores. Four large rectangular precision-cut panels arranged as two columns by two rows, vertical joint staggered in lower row, hairline dark grey recessed seams, tiny restrained chamfer at edges. Seam width about 0.5 percent image width. No large cracks, no dirt splatters, no graffiti, no damage, no text, no metallic ornaments, no emissive lines. Designed to repeat seamlessly at all four edges, square texture asset fills entire frame. Large clean calm surfaces dominate, subtle material specificity, realistic PBR base-color style, high resolution.

生成结果的球面接缝与贴图重复效果仍以实际游戏画面为准。

## 验证边界

`-echo-slice-review` 只在 Editor / Development Build 中存在，必须搭配固定种子的 validation 模式。该检查临时禁用障碍碰撞，保留道路、门和障碍的可视对象，采集实际运行镜头与转弯画面后自动退出。它证明视觉路线，不代表碰撞回归或真人手感验收。

## 本轮验证结果（2026-09-10）

- 最终 Windows IL2CPP Development 审阅包构建成功，日志 `TestResults/ExperienceSliceV1/facade-build.log`，标记 `EXPERIENCE_SLICE_BUILD_OK After`。未发现 C# 或 shader 编译错误。
- 最终 EditMode：685/685 通过，结果 `TestResults/ExperienceSliceV1/EditMode-delivery.xml`。包含 20 项 CityV7 道路可见性与左右转相机净空检查，以及新资源无碰撞组件检查。
- 三个跑道 Prefab 原有碰撞/Rigidbody 序列化段与 HEAD 完全一致，分别为 1/3/3 个；新增桥墩只有可视网格。记录 `collision-binding-check.json`。
- 最终固定种子 1337、42 分别采集 700m，合计 6 次转弯，覆盖左右转；两组 `missingRoadFrames=0`。
- 修改前的 12/45/90m 游戏镜头位于 `Before/VisualCaptures/`；最终同镜头路线位于 `FinalSeed1337/`，另一组路线位于 `FinalSeed42/`。两组均用真实角色、相机、道路池和预测门运行，诊断模式关闭了障碍碰撞。
- 独立 HUD 组件在 1280 与 1920 宽度的画面已采集到 `Captures/`；预判提示、受伤强调、瞬时反馈均可见，没有观察到这些参考尺寸下的文字裁切。
- 所有本轮测试游戏/编辑器均已退出。
- 构建 `GameAssembly.dll` SHA-256：`81D8FB89E2C63AB8350003B7A3DD2B4970E66BD58244CF261BD8DFC089883A08`。

审阅入口：`TestResults/ExperienceSliceV1/After/EchoRun.exe`。正常双击不启用自动路线或碰撞豁免；它使用独立的样板存档，不代表生产发布包。

## 2026-09-12 场景与人物修复

- 背包缩小并下移，保持胸部骨骼绑定；人物边缘光改用世界法线与视线计算，消除转弯后黑灰交替。
- 左转旧建筑替换接入路线池，保留原有道路与碰撞；增加桥体、桥墩、下层街区和建筑窗带。
- 裁除建筑墙面、楼顶与窗框的共面重叠区域；下层建筑保留 FBX 导入朝向，最终合并网格后再次消除交叠。
- `CityLayerUpgrade.InstallAndReview` 从原始 FBX 重建场景资源；运行时复用烘焙网格，不执行裁切。
- 本地 Windows 修复版通过 58 项针对性测试，固定种子视觉巡检完成 500 米、左右转各一次，道路与街区缺失帧均为 0。视觉巡检关闭障碍碰撞，不等于真人完整玩法验收。

## 后续范围

这一轮是已接入实际跑道的视觉与 HUD 样板。建筑基础轮廓仍沿用 CityV7，远景仍为简化轮廓；后续可继续制作更有辨识度的地标与街道道具。主菜单、设置、外观和结算页的整体重设计不在这一阶段内。尚未进行真人手感验收、音频验收或移动端/WebGL 性能验证。
