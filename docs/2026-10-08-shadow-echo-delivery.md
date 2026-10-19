# 影迹回声：本轮交付与检查入口

最终设计依据：`2026-10-08-shadow-echo-final-plan.md`。验收状态与五人记录表：`2026-10-08-shadow-echo-acceptance.md`。

## 可运行版本

Windows 功能验收包：`TestResults/ShadowEcho-20261008/CandidateMonoR2/Windows/EchoRun.exe`。双击即可正常操作；保留同目录全部依赖文件。该包使用单独的 QA 产品名，存档与正式产品隔离。它包含菜单、真实观察提示、结算分支、报告入口和本轮视觉试验。

操作：方向键左右换道、向上跳跃、向下滑铲，Esc 暂停。先从无回声菜单开始，检查观察提示是否随本局动作变化；自然失败后核对不足说明和“重新观察”。新代是否生效仍取决于正常生成与保存条件。

Windows 包内 `TempleRun.Runtime.dll` 的 SHA-256 为 `200F2662CDAD36FC77F765890E0FFEF985A250505A3FCD6C485AD7BB95660F37`。该包用于功能与画面，不能与旧 IL2CPP 包混算性能。

WebGL 本轮导出：`TestResults/ShadowEcho-20261008/CandidateWebGL/`。已在本机独立 Chrome 会话中实际运行；本地预览服务已在性能采样前停止，地址 `http://127.0.0.1:18768/` 只有重新启动服务后才有效。没有公开发布。导出为未压缩 Development 包，不代表上线下载体积。

微信安全区修订的新包已通过本地构建，后续开发者工具应导入：`TestResults/ShadowEcho-20261008/IsolatedWeixinProject/TestResults/ShadowEcho-20261008/CandidateWeixinSafeAreaR4/minigame/`。10月11日新QA沿用用户授权身份；实际WASM包含修订，完整93,754,215字节HTTP资源与SHA一致，主工程101关键源/递归26设置及旧导出完整集合/hash不变。使用 `http://127.0.0.1:18765` 本机资源，不能作为手机或上线版本。没有上传。

模拟器已有直接证据来自旧`CandidateWeixin`：10月10日显示真实菜单并进入正常观察局；动作、自然结算与返回未完成，“观察详情”与原生胶囊有可见重叠。10月11日切回该旧QA，菜单再现。新包修复了已确认的转换/缓存弱点，但准确截图根因及遮挡消除仍待实际复验；源/新包证据见`TestResults/ShadowEcho-20261008/WeixinSafeArea-20261011/README.md`，旧GUI记录见`WeixinRuntime-20261010/README.md`。资源服务已停止，继续运行前须重启 [新包本机服务](../TestResults/ShadowEcho-20261008/WeixinSafeArea-20261011/local-preview.cjs)。CLI要求本地访问令牌不能解释为微信未登录。

Windows 同口径性能对照已完成：10月9日前后各三次、交替采样全部有效，两项退化均未超过10%。平均帧时间中位数16.699→16.671ms，1% low 49.08→59.30 FPS。逐次数据与适用范围见 [性能记录](2026-10-09-shadow-echo-performance.md)；不能将本次差值宣传为确定的提速，也不代表手机或微信性能。

## 已有证据怎么读

- `candidate-r2-capture-index.json`：Windows 30 个场景、96 张图及实际参数。正常跑局与标有 QA RESULT LAYOUT FIXTURE 的布局案例分开记录。
- `WebGLBrowser-20261008-071209/`：真实浏览器点击、键盘跳跃/滑铲、暂停、结算、返回和横竖屏截图，附完整 console 记录。
- `candidate-test-summary.md`：相关 EditMode 159/159 与 PlayMode 21/21，以及全部失败和修复历史。旧冷白样例净距失败仍保留，不能称全量测试通过。
- `performance-summary-20261009-r2.json`：六份原始 CSV 校验后的正式性能汇总；保留基线孤立长帧，没有筛掉慢样本。
- `WebGLClips-20261009/README.md`：107.59秒菜单到自然终点及结算视频的索引、原件哈希和实际覆盖。视频内持续标注禁碰撞诊断；不证明正常碰撞、身份保存或真人理解通过。
- `WindowsMotion-20261010/index.html`、`README.md`：同一次Windows运行的并行/接近、建筑投影、高速三片，80/108/96帧；按实测时间VFR播放。高速19.60–20.31m/s与原始帧对应，实际捕获约14–16fps，不能视为正常流畅度。三片已在独立浏览器逐片播放到结束，真人辨识仍待验。
- `WeixinRuntime-20261010/README.md`：本轮微信GUI导入、真实菜单和观察起跑，SDK提示、胶囊遮挡、未证实的向上键、后续旧包切回QA以及导入配置实际变化。
- `WeixinSafeArea-20261011/README.md`：安全区转换/缓存修订，65/65定向测试、新隔离R4构建、实际WASM包含补丁及完整本机HTTP资源；实际新包GUI仍待验。
- `WebGLClips-20261009/windows-batch-menu-20261009-154925-312/result.json` 与 `WebGLClips-20261009/windows-batch-route-20261009-155158-308/result.json`：现有独立 `PerformanceCandidate` 包以 `-batchmode -force-d3d11` 完成菜单离屏预检和完整自然路线，均退出0，所有主窗口句柄采样为0，包哈希未变。没有重建，也不计性能样本。
- `WebGLClips-20261009/windows-batch-route-20261009-155158-308/`：`captures.json` 索引36张真实离屏PNG，`playtest-telemetry.json` 与 `telemetry-summary.json` 保留379个状态样本、33个关口事件。末关口呈现时82.0338秒、实测19.8453m/s；结算时85.0175秒、实测20.2035m/s。原图在 `PerformanceCandidate/Windows/StackedCityCaptures/`。

以上证据入口位于 `TestResults/ShadowEcho-20261008/`。布局 fixture 没有制造一次真实获胜或保存，不应拿它证明游戏事务成功；对应分支的业务保证来自 Runner/Presentation 测试。

原36张图的速度只对应该次Windows静帧与遥测，不能回填旧WebGL视频。后补的WindowsMotion三片另有同次实测速度；目录名不代替场景核查，建筑投影片含真实横梁遮挡。两次均为QA身份、禁碰撞诊断，不计正式性能或真人理解通过。

## 仍需独立验收

按验收表邀请五名未读方案者，逐项记录首次回答，每项至少四人正确。代理对截图的判断不能填写这张表。

专项动态素材已备，包括旧WebGL短时轮廓重叠和新增Windows并行、建筑投影及同次实测高速片段。微信胶囊遮挡须修复并在新包复验，实际滑动、结算和返回仍需完成；手机真机另需实际运行和观察这些场景。桌面竖屏浏览器、可播放素材和模拟器起跑均不替代手机触控、真机性能或五人理解。已完成的桌面性能报告独立记录。

当前修改均在本地；未提交、推送或上传。报告历史仍按最终方案独立后置。
