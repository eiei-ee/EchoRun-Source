# EchoRun 微信小游戏 — 交接给 Codex

> 生成 2026-08-08。给 Codex 排查**白色光闪烁**问题。读取本文件 + 下方源码即可开工。

## 一句话

AI影子跑酷已跑通微信小游戏（真AppID、构建成功、能玩），但**每个道路段交界处有白色光块状/条状快速闪烁**，仅 WebGL 渲染器出现（Windows 原生不闪）。已试 3 个修复均无效，需要 Codex 换思路。

## 项目背景

- **游戏**: EchoRun（原 TempleRun），"击败不断进化的AI影子"跑酷。双AI架构：bandit导演(AITrackDirector) + 影子(AIShadowRunner 贝叶斯行为克隆)
- **仓库**: `eiei-ee/EchoRun`（私有），本地 `C:\Users\zzz\Desktop\TempleRun`
- **分支**: `wechat/minigame`（微信迁移分支）
- **引擎**: 团结引擎 Tuanjie 2022.3.62t8（Unity 2022.3 分叉）
- **微信SDK**: 官方 `com.qq.weixin.minigame` 0.1.32（WX-WASM-SDK-V2）
- **AppID**: 构建时通过 `WECHAT_MINIGAME_APPID` 注入（不要提交实值）
- **截止**: 腾讯游戏创作大赛2026 AI赛道 9/15（优先级低于修bug）

## 当前进度（已通）

- [x] 正式 AppID 注册成功并注入（`WeChatV0.asset` 的 `Appid` 字段 + 构建时 `WECHAT_MINIGAME_APPID` 环境变量）
- [x] 构建成功，输出 `Builds/WeixinMiniGameV0-Profile/minigame`
- [x] 游戏能跑（devtools 模拟器 + 手机真机预览）
- [x] 数据包分包加载修复：`WeChatV0.asset` `assetLoadType: 0→1`（之前 CDN 加载空 URL 失败）。构建后 `loadDataPackageFromSubpackage: true`，数据包 6.7MB 进 `data-package/` 分包
- [x] 快适配插件已开通（mp后台 能力地图→开发提效包→快适配），解决 `UnityManager is not a constructor`
- [x] 主包 0.937MiB < 4MiB
- [ ] 未测: 跳跃成功率、影子收敛、截图（被闪烁bug卡住）

## ⚠️ 卡点：段交界白色光闪烁

**现象**（用户描述）:
- 位置: **每条道路段与段的交界处**（"第一条道路结束后第二条道路开始的那一块"）
- 颜色: **白色光**
- 形状: **块状 + 条状，不规则，闪烁很快**（太快看不清具体形状）
- 规律: 每一处交界都出现
- 渲染器: **仅 WebGL 渲染器**（devtools 模拟器 + 手机真机都有）；**Windows 原生 exe 没有**
- 路面: 纯色平板（程序化生成的立方块，不是带贴图的 prefab）

### 已试的修复（全部无效，别再重复）

1. **弯道覆盖面 z-fighting 修复** — `TrackManager.cs` `EnsureTurnCoverage()`:
   - 截断 ExitCoverage 使其不覆盖 EntryCoverage（原三者同面重叠：Entry/Exit top=0, Corner top=0.02）
   - 删除冗余 CornerCoverage
   - 结论: 没用 → 说明**不是弯道覆盖面的几何重叠**

2. **路面材质哑光** — `TrackManager.cs` 新增 `MakeMatteRoadMaterial()`:
   - 直线路面 + 弯道覆盖面统一 smoothness/metallic=0，去 Standard shader 的 specular + 天空盒反射
   - 结论: 没用 → **不是 specular/反射**

3. **相机 near 0.3→2.0** — `SampleScene.scene`:
   - 大幅提升 15-40m 深度精度（~6.7x），针对 WebGL 深度精度不足导致接缝/z-fighting
   - **状态: 最新构建 v0-8 已含此修复，但尚未验证**（devtools 反复卡死，用户没测成功）

### 几何分析结论（已确认）

- 相邻段表面 **精确相切、无重叠**（segmentLength=20 = 表面长 20，段间距 20）
- 弯道覆盖面与相邻直线也相切不重叠
- 玩家 Y 固定（`PlayerController.UpdateForwardDirection` 的 `newPos.y = _rb.position.y`），**不能降低路面高度**（会导致玩家悬浮）

### 剩余嫌疑（给 Codex 的方向）

1. **阴影（shadow acne/精度）**: 路面大平面接收软阴影（`WorldStyler.ConfigureLighting` key light `LightShadows.Soft`）。WebGL 阴影贴图精度差 + 掠射角相机 → 段缝处阴影自伪影 → 亮块条状。**下一个最该试的方向**:
   - 路面临时 `renderer.receiveShadows = false` 验证是否消失
   - 或调 `light.shadowBias` / `shadowNormalBias`
   - 注意: 角色影子在路面上是重要视觉，别直接全关
2. **WebGL 网格接缝**: 两个独立 20m 立方块相切，WebGL 光栅化在掠射角下接缝闪烁。对策: 段间加小重叠+高度偏移，或合并成连续网格
3. **雾/天空盒在 WebGL 下的交互**: `RenderSettings.fog` 线性雾 58-138m，`EchoSky` 天空盒
4. **游戏内其他运行时面**: `WorldStyler` 装饰物（pylons/rails/LaunchRoad）— LaunchRoad(smoothness 0.74) 很亮但被路面盖住

## 构建管线（改完 C#/场景后重建）

```bash
# 环境变量注入 AppID
export WECHAT_MINIGAME_APPID=wx...

# 批处理构建（约5分钟）
"D:/unity/tuanjie/2022.3.62t8/Editor/Tuanjie.exe" -batchmode -quit \
  -projectPath "C:/Users/zzz/Desktop/TempleRun" \
  -buildTarget WeixinMiniGame -minigamesubplatform weixin \
  -executeMethod BuildConfig.BuildWeixinMiniGameV0 \
  -logFile "C:/tmp/TempleRun-Weixin-Build.log"
```

- 构建方法: `Assets/Editor/BuildConfig.cs` → `BuildWeixinMiniGameV0()`
- 输出: `Builds/WeixinMiniGameV0-Profile/minigame`

### ⚠️ 每次重建后必须重打的补丁

SDK 模板把 `parallelPreloadSubpackages` 生成成**对象数组**（无效格式），要改回**字符串数组**：

`minigame/game.json`:
```json
"parallelPreloadSubpackages": ["wasmcode", "data-package"]
```
（SDK 生成的是 `[{"name":"wasmcode"},{"name":"data-package"}]` — 无效）

## 测试流程（devtools）

- devtools 路径: `D:\微信开发\微信web开发者工具\微信开发者工具.exe`
- 打开项目: `C:\Users\zzz\Desktop\TempleRun\Builds\WeixinMiniGameV0-Profile\minigame`，选**小游戏**
- ⚠️ **devtools 反复卡死**: 打开项目后 wasm 编译会阻塞 UI 线程（鼠标点不动，键盘能用）。**打开后干等 2-3 分钟别点**。卡死就 `taskkill //F //IM 微信开发者工具.exe` 杀掉重启
- 预览上传若报代理错误 `ECONNREFUSED 127.0.0.1:7890` → devtools 设置里改**不使用代理**（微信服务器国内直连）
- 手机测试需**重新扫码预览**（旧预览是旧包）

## 未提交改动（当前工作区）

```
M Assets/Scenes/SampleScene.scene              (near 0.3→2.0, 未验证)
M Assets/Scripts/TrackManager.cs               (z-fight修复 + 哑光材质)
M Assets/WeixinMiniGame/BuildProfiles/WeChatV0.asset  (assetLoadType=1 + AppID)
```
分支: `wechat/minigame`，最新提交 94000a4

## 已知工具链问题（与闪烁无关）

- GFW 下 git push 常超时
- devtools 加载项目卡死（见上）
- 调试日志位置: `C:\tmp\TempleRun-Weixin-Build-*.log`
- 上次验证记录: `Builds/WeixinMiniGameV0-Profile/minigame/verification/build-verification.md`（旧，AppID 未通过时生成）
