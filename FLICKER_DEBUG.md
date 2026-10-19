# EchoRun 微信小游戏 — 段交界闪烁问题完整记录

> 最后更新 2026-08-08。当前 v0-27b (恢复完整功能，保留 ribbon + EchoRun/Road shader)。

## 症状

**WebGL/minigame 渲染器独有**（Windows 原生 exe 无）。道路段交界处白色/黑色块状条状快速闪烁。

## 构建历史

| 版本 | 改动 | 结果 |
|---|---|---|
| v0-1~11 | z-fighting修复/路面哑光/相机near/WebGL2/软阴影/段重叠0.05m | ❌ |
| v0-12 | 起跳修复+120fps按钮 (稳定基线) | ❌ 基线闪烁 |
| v0-16 | Unlit/Color shader 路面 (待测，未实测) | 未知 |
| v0-17 | Unlit + 关装饰 + 关雾 + 关天空盒 + **1m重叠** | ❌ 整条路条状闪烁 (重叠太大) |
| v0-19 | 还原重叠0.05m + Plane y=-0.05 + 恢复装饰/雾/天空盒 | ❌ 边界黑条+白闪 |
| v0-20 | 自定义 EchoRun/Road shader (真Unlit) | ❌ 细白条纹+边界闪 |
| v0-21 | **Cube→Quad** (无侧面/底面) | ❌ 白闪+影子坏了(缺collider) |
| v0-22 | Quad+collider + **全关**(装饰/雾/天空盒/Plane/阴影/影子) | ❌ 仍闪 |
| v0-23 | Quad **零重叠** (20m精确) | 细纹消失 ✅ / 边界白条仍在 ❌ |
| v0-24 | **连续Mesh ribbon** (RoadRibbon) | ❌ 仍闪 |
| v0-25 | 路面纯红诊断 | ❌ 闪白光 (证明非路面) |
| v0-26 | 跳过障碍物/金币 specular 材质 | ❌ 仍闪 |
| v0-27b | **回退恢复**: 天空+影子+障碍物+装饰+雾+Plane; 保留ribbon+EchoRun/Road | 待测 |

## 已排除的假说 (14项)

### 1. Shader/光照
- Standard shader specular/Fresnel → 尝试了 Standard matte (smoothness=metallic=0)、Unlit/Color、自定义 EchoRun/Road (真vertex/fragment无光照) → ❌
- 方向光阴影 → LightShadows.None → ❌
- 雾 → RenderSettings.fog = false → ❌

### 2. 几何/Mesh
- Cube 原语侧面z-fighting → 换 Quad (无侧面) → ❌
- 段间重叠z-fighting → 试了 0.05m/1m/0m 重叠 → ❌ (0m 消了细纹但边界还在)
- 段间Mesh边界 → 连续 RoadRibbon (单Mesh共享顶点) → ❌

### 3. 场景对象
- WorldStyler 装饰物 (beams/rails/pylons) → 全关 → ❌
- Plane 地板 z-fighting → renderer disabled + y=-0.05 → ❌
- 影子幽灵 semi-transparent → 禁用 AIShadowRunner → ❌
- 障碍物/金币 specular 材质 → 跳过 StyleCoin/StyleObstacle → ❌
- **路面本身** → v0-25 纯红路面，闪烁仍为白色 → **证明白光不来自路面**

### 4. 渲染管线
- 天空盒背景漏光 → CameraClearFlags.SolidColor → ❌
- 相机 near plane 深度精度 → near 0.3→2.0 → ❌
- WebGL 2.0 → 黑屏回滚 → ❌
- 构建缓存 ($COMPRESS_DATA_PACKAGE) → CleanBuild=1 + 清 Bee → ✅ (构建问题)

## 当前代码状态

### 保留的改进 (vs 基线 v0-12)
- `Assets/Shaders/EchoRoad.shader` — 自定义真Unlit shader
- `TrackManager.cs` — RoadRibbon 连续Mesh、Quad+collider路面、零重叠
- `WorldStyler.cs` — Plane y=-0.05 (防z-fighting)
- 路面颜色恢复暗色 `(0.25, 0.28, 0.35)`

### 已恢复 (v0-27b)
- 天空盒、雾、装饰物、Plane渲染、方向光软阴影
- 影子幽灵 runner (AIShadowRunner)
- 障碍物/金币 specular 材质 (StyleCoin/StyleObstacle)

## 可能忽略的点

### 1. 启动台 (BuildStartDeck)
`WorldStyler.BuildStartDeck()` **不受 DisableDecorations 控制**。创建 LaunchRoad、LaunchRail、Pylons、SignalArch — 全部用 Standard shader metallic 材质。这些物体在玩家后方，但它们的 **specular 高光反射** 可能投射到前方路面上（通过环境反射或间接光照）。

### 2. 填充光 (EchoFillLight)
WorldStyler 创建第二个 Directional Light（fill light），intensity=0.72，color 偏向蓝白。即使路面用 Unlit shader，**其他物体（障碍物/装饰/角色）的 specular 高光来自填充光**，可能产生白色反射。

### 3. 环境光 (Ambient)
`RenderSettings.ambientSkyColor = (0.72, 0.78, 0.88)` — **浅蓝白色**。Trilight ambient mode。即使 Unlit 路面不受影响，但场景中 Standard shader 材质物体会受 ambient 影响产生白色调。

### 4. 反射探针 (Reflection Probe)
`RenderSettings.reflectionIntensity = 0.62`。如果场景有反射探针（默认或自动生成的），Standard shader 材质的 metallic 表面会采样反射。反射内容可能包含天空盒的亮区域，产生白色 specular。

### 5. 半透明渲染顺序
影子幽灵使用 alpha=0.28 半透明材质。即使路面本身不闪，**幽灵渲染在路面上方**时，alpha blending 可能与 WebGL 深度缓冲区交互产生条带。已通过禁用影子排除（v0-22），但恢复后仍需注意。

### 6. WebGL 1.0 精度限制
devtools 日志确认使用 **WebGL 1.0 (OpenGL ES 2.0)**。关键限制：
- 深度缓冲区可能仅 16-bit（某些实现），在 140m far plane 下精度严重不足
- `mediump` fragment shader 精度可能导致 sub-pixel 颜色偏移
- 无 `OES_texture_float` 等扩展时的回退行为

### 7. MSAA / 抗锯齿
WebGL 1.0 的 MSAA 在 Mesh 边界处可能产生混合 artifact。两个相邻但独立的 draw call 在边界像素处可能有不同的 MSAA 采样结果。

### 8. 粒子系统
未检查项目中是否有 ParticleSystem 组件。如果有任何粒子效果（灰尘、尾迹、金币收集特效），它们可能在段边界处错误触发或渲染异常。

### 9. 碰撞体可视化
Unity 的 Physics Debug 或 Gizmos 在某些配置下可能渲染碰撞体线框。白色线框在 WebGL 中如果被错误渲染可能表现为闪光。

### 10. 微信 SDK 渲染层
快适配插件 (v1.2.91) 可能在 WebGL canvas 上方叠加了额外的渲染层（coverview、loading page 等）。这些层与 Unity 渲染的交互可能导致边界 artifact。

## 下一步建议

1. **最小化启动台** — 临时禁用 `BuildStartDeck()` 排除启动台装饰 specular
2. **检查 ParticleSystem** — 全局搜索 ParticleSystem 组件，临时禁用
3. **WebGL 深度格式** — 尝试 `WebGL2: 1` 配合 minimal shader (之前黑屏可能是 shader 不兼容，现在有 EchoRun/Road 极简 shader)
4. **截图对比** — 截图闪烁区域 vs 非闪烁区域，逐像素分析颜色来源
5. **真机测试** — devtools 模拟器可能有自己的渲染 bug，在真实微信客户端测试
