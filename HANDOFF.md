# EchoRun 微信小游戏 — 交接文档

> 2026-08-08。仓库已公开 `eiei-ee/EchoRun`。闪烁问题暂停，等待新思路。

## 一句话

AI影子跑酷微信小游戏构建成功、能玩。**唯一卡点：道路段交界处白色块状/条状闪烁**，仅 WebGL 渲染器（Windows 原生 exe 干净）。15 次尝试全败。

## 环境

| 项 | 值 |
|---|---|
| 仓库 | `eiei-ee/EchoRun` (PUBLIC) |
| 本地 | `C:\Users\zzz\Desktop\TempleRun` |
| 分支 | `wechat/minigame` (HEAD: `f2918f7`) |
| 引擎 | 团结引擎 Tuanjie 2022.3.62t8 (`D:/unity/tuanjie/2022.3.62t8/Editor/Tuanjie.exe`) |
| 微信SDK | `com.qq.weixin.minigame` 0.1.32, 快适配 v1.2.91 |
| AppID | 构建时通过 `WECHAT_MINIGAME_APPID` 注入（不要提交实值） |
| devtools | `D:\微信开发\微信web开发者工具\微信开发者工具.exe` (2.02.2608031) |
| 图形 | **WebGL 1.0** (OpenGL ES 2.0), WebGL2 试过黑屏 |

## 当前工作区状态

**已回退到 `f2918f7`**（闪烁诊断前基线）。所有 15 次诊断改动在 **`git stash@{0}`** 中。

```bash
# 恢复诊断改动
git stash pop

# 或创建分支保留
git stash branch flicker-debug
```

stash 包含：TrackManager.cs (RoadRibbon/Quad/纯红诊断)、WorldStyler.cs (StartDeck关/Plane关/雾开关)、EchoRoad.shader (自定义Unlit)、GameManager.cs、InputManager.cs、FLICKER_DEBUG.md。

## 构建流程

```bash
# 1. 杀干净
taskkill //F //IM 微信开发者工具.exe
taskkill //F //IM Tuanjie.exe

# 2. 清缓存（必须，否则 $COMPRESS_DATA_PACKAGE 报错）
rm -rf Builds/WeixinMiniGameV0-Profile Library/Bee

# 3. 构建
export WECHAT_MINIGAME_APPID=wx...
"D:/unity/tuanjie/2022.3.62t8/Editor/Tuanjie.exe" -batchmode -quit \
  -projectPath C:/Users/zzz/Desktop/TempleRun -buildTarget WeixinMiniGame \
  -minigamesubplatform weixin -executeMethod BuildConfig.BuildWeixinMiniGameV0 \
  -logFile C:/tmp/TempleRun-Weixin-Build-v0-N.log

# 4. 打补丁（SDK 模板每次覆盖）
#    game.json: parallelPreloadSubpackages 从对象数组改成字符串数组
#    game.js:   gameManager.startGame() 后加 wx.setPreferredFramesPerSecond(60)
#    CleanBuild: 1 (WeChatV0.asset + MiniGameConfig.asset)
```

输出：`Builds/WeixinMiniGameV0-Profile/minigame`。devtools 打开时选"小游戏"，等 2-3 分钟 wasm 编译（别点任何东西，否则卡死）。

## 闪烁问题：已排除清单

### 路面（9次）— 全排除
| # | 假设 | 做法 | 结论 |
|---|------|------|------|
| 1 | z-fighting 弯道重叠 | 截断覆盖+删转角 | ❌ |
| 2 | Standard shader specular | smoothness=metallic=0 | ❌ |
| 3 | 相机近平面精度 | near 0.3→2.0 | ❌ |
| 4 | WebGL shader精度 | WebGL2:1 | 黑屏回滚 |
| 5 | 方向光阴影 | shadows=None | ❌ |
| 6 | 段间缝隙漏光 | 0.05m重叠+转角填补 | ❌ |
| 7 | Unlit 未生效 | Unlit/Color shader | ❌ |
| 16 | 自定义真Unlit | EchoRun/Road (vert/frag直出色) | ❌ |
| — | **v0-25 红路诊断** | 路面纯红(1,0,0)，闪烁仍白 | **白光不来自路面** |
| — | **连续Mesh** | RoadRibbon 单Mesh共享顶点 | ❌ |

### 场景对象（6次）— 全排除
| # | 假设 | 做法 | 结论 |
|---|------|------|------|
| 8 | WorldStyler装饰 specular | DisableDecorations=true + 关雾+关天空盒 | ❌ |
| 9 | Plane地板 z-fighting | renderer.enabled=false + y=-0.05 | ❌ |
| 10 | 影子幽灵半透明 | 注释 AIShadowRunner | ❌ |
| 11 | 障碍物/金币 specular | 跳过 StyleCoin/StyleObstacle | ❌ |
| 12 | StartDeck (LaunchRoad) | 全部 Renderer enabled=false | ❌ |
| 13 | 障碍物/金币本体 | SpawnObstaclesAndCoins return | ❌ |

### 渲染管线（3次）
| # | 假设 | 做法 | 结论 |
|---|------|------|------|
| 14 | 大重叠导致z-fighting带 | 1m重叠 | ❌ 更严重 |
| 15 | 零重叠/Quad | 20m精确 | 细纹消，边界仍在 |
| — | WebGL2+深度精度 | near=1 far=60 WebGL2:1 | v0-30 待测 |

## 关键洞察

1. **v0-25 红路测试**：路面纯红 `(1,0,0)`，闪烁仍白色。绝对证明非路面 shader 问题。
2. **v0-23 零重叠**：Quad 精确 20m 消除了细纹，边界白条残存 → z-fighting 是细纹源，但不是白块源。
3. **v0-24 连续 Mesh**：即使单 Mesh 无内部边界，闪烁仍在 → 排除 "Mesh 间栅格化间隙" 假设。
4. **关联段交界**：闪烁规律出现在每 20m 的段交界处。连续 Mesh 覆盖了所有直段，但弯道覆盖（EntryCoverage/ExitCoverage/CornerFiller）仍然单独存在。**弯道交界可能是遗漏点。**

## 接下来做什么

### 优先（新思路）

1. **弯道覆盖面的渲染检查**
   - `EnsureTurnCoverage` 创建的 EntryCoverage/ExitCoverage/CornerFiller 是用 `CreateRoadQuad` 创建的单独 Mesh
   - 这些 Quad 与 RoadRibbon 的边界可能产生 z-fighting
   - 验证：跳过所有 `EnsureTurnCoverage` 调用，只用 ribbon 跑（弯道没有路面，但能诊断）
   - 如果闪烁消失 → 弯道覆盖面是根因

2. **玩家角色渲染器**
   - `StyleCharacter` 创建 metallic 材质（Sphere/Capsule/Cylinder 组合）
   - 角色在路面上方，其 specular 可能被相机捕捉为白色碎块
   - 验证：`StyleCharacter` 中给所有 renderer 设 `_Smoothness=0` + `_Metallic=0`
   - 或者：临时禁用角色所有 Renderer（用 invisible player 跑一段看闪烁是否在）

3. **微信 SDK 渲染层**
   - 快适配插件 v1.2.91 在 canvas 上方有 coverview 层
   - 可能与 Unity 渲染输出产生像素交互
   - 验证：查阅微信 SDK 文档中关于 coverview 与 WebGL canvas 的已知问题

4. **真机测试**
   - devtools 模拟器可能有自己的渲染 bug
   - 扫码在真实微信客户端运行，确认问题在真机也存在还是一样
   - 如果真机不存在 → 可以忽略（devtools 模拟器 bug）
   - 如果真机也一样 → 继续排查

### 备用

5. **Shadow Map 精度**
   - 方向光 shadow map 在段交界处可能有 acne 或偏移
   - 虽然 LightShadows.None 排除了阴影直接闪烁，但 shadow map 的渲染 pass 可能有间接影响

6. **Graphics.Blit / 后处理**
   - 搜索项目中是否有后处理效果（PostProcessing、Blit、RenderTexture）
   - WebGL 1.0 的后处理可能有精度问题

7. **完全去掉 Turn**
   - `turnChance = 0` 禁止转弯，只跑直道
   - 如果闪烁只在有转弯时才出现 → 弯道是根因
   - 如果纯直道也有 → 弯道无关

## 代码关键路径

```
TrackManager.Update()
  → SpawnSegment()          # 生成新段
    → CreateProcStraight()  # 直段：Quad collider (renderEnabled=false)
    → CreateProcTurn()      # 弯段：EnsureTurnCoverage (Entry/Exit/CornerFiller)
    → SpawnObstaclesAndCoins()  # 障碍物/金币
    → WorldStyler.DecorateSegment()  # 装饰 (beams/rails/pylons)
  → RebuildRoadRibbon()     # 重建连续路面 mesh

WorldStyler.Start()
  → ConfigureAtmosphere()   # 雾、天空盒、环境光
  → ConfigureLighting()     # 主光+填充光
  → BuildStartDeck()        # 启动台 (LaunchRoad/rails/pylons) — 已排
  → StyleCharacter()        # 玩家角色材质
  → DecorateSegment()       # 每段装饰

MakeMatteRoadMaterial()     # 路面材质 (EchoRun/Road shader)
CreateRoadQuad()            # 创建单面 Quad + BoxCollider
```

## 相关文件

- `FLICKER_DEBUG.md` — 15 次尝试完整记录
- `HANDOFF_CODEX.md` — 原始交接文档（v0-16 时）
- `FLICKER_DEBUG.md` → 闪烁诊断详情
- Stash `stash@{0}` → 所有诊断代码改动
