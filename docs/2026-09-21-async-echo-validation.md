# 异步好友影子挑战：实施与验收记录

实施依据：[最终方案](2026-09-20-async-echo-challenge-final-plan.md)。本记录只表示已实际执行的检查；构建通过不等于微信双账号端到端验收。

当前已完成真实单身份的 Unity 模拟器邀请冷启动、拉取已发布测试影子、实际挑战、结算上报及云端最佳成绩更新。最新 cloud-05 完整构建退出码为 0，构建前快照的 28 个场景/设置/profile 文件已全部恢复并核对一致，最新产物和关键源码指纹已保存。邀请界面的字体修复和实际榜单显示均已通过截图验收；2026-09-21 22:47—22:51 在默认 GPU 启动的工具中，菜单、好友入口和榜单连续响应。此前卡顿根因仍不明，不能据此认定永久修复。对手仍是 SDK 发布的序列化测试 fixture；双账号真实卡片送达、手机运行、实际存档前后对照、真实云空榜及网络错误态尚未完成。

## 已实现

- 独立 `echo` 云函数：不可变影子快照、上下文 OPENID、版本/字段/16 KiB 校验、事务报告去重、最佳完整一局和最小字段挑战榜。三个集合及部署索引见 `Tools/CloudFunctions/echo/README.md`。
- `AsyncChallenge` 复用正常单合约玩法，注入冻结 identity；关闭本局学习、重新学习、晋升和长期收益。本局结果通过事件交给微信层，本地双槽存档仍只管理玩家自己的进度。
- 微信层独立程序集，使用已安装官方 SDK；核心程序集已移除 `Wx` 引用。纯 query、冷热启动、最近邀请、分享、榜单、真实时间超时、最多一次自动重试、过期回调失效和非微信降级。
- 暂停恢复/重开保留运行中收到的邀请。重开生成新 challengeId，报告重试复用原 ID。异步结果界面不提供发布好友影子的入口。
- 回归中发现既有视觉 `Mathf.SmoothDamp` 在零 deltaTime 下产生 NaN，已添加仅视觉平滑的零时间保护；正常帧结果保持相同。没有改变学习算法或正常单合约晋升判定。
- 构建时定位既有 `BuildConfig.TryRunGit` 重定向管道死锁：Git 换行警告为 5,213 字节，编辑器等待 stdout，Git 等待 stderr 被读取。已改为同时读取两路输出，并让超时能真正结束本次 Git 子进程；保留构建脚本其他已有修改。
- 真实客户端联调确认当前 SDK 初始化成功回调为 `Inited=200`；已修正原先只接受 0 导致云入口隐藏的判断，其他状态仍按失败处理。
- 构建收尾不再直接重写 `ProjectSettings.asset`：通过公开 MiniGame API 关闭构建用子平台、保存资产，再只读校验序列化值；profile 字段相同则不写入，必要写入保留原换行并释放 Unity 缓存句柄。
- 异步界面使用的字体子集已补齐 10 个缺字，保留原字体字符和资源 GUID；静态覆盖检查通过，最新 cloud-05 邀请界面的字体视觉效果已由截图确认。

## 本地验证

日志和 XML 位于 `TestResults/AsyncEcho-20260921/`。以下时间按北京时间区分：完整 859 项、3 项图形场景和初次三目标构建是 2026-09-21 凌晨的初始基线（测试 XML 使用 UTC，日期为 2026-09-20）；当天晚间的 63 项针对性回归和 cloud-03/05 构建为后续修复证据。没有把初始 Windows/WebGL 产物描述成包含全部后续修改的重建产物。

| 项目 | 实际结果 | 证据 |
| --- | --- | --- |
| 完整 EditMode 初始基线 | 859/859 通过，退出码 0 | `all-editmode-final-01.xml` / `.log` |
| 场景回归初始基线 | 3/3 通过，退出码 0；使用图形设备 | `async-playmode-final-01.xml` / `.log` |
| 真实生产载荷导出 | 成功，使用 `ActiveEchoIdentity.ToJson()` | `fixture-export-01.log`；`Tools/CloudFunctions/echo/test/fixtures/identity-v1.json` |
| Node 云业务及 SDK 包装层 | 25/25 通过，0 跳过；没有真实云请求 | `cloud-final.txt` |
| 云入口诊断补丁回归 | 31/31 通过；包含真实 SDK 入口的无凭证、无网络测试 | `pending-cloud-diagnostics-deploy.json`；`cloud-diagnostics-source-sha256.json` |
| SDK/异步流程针对性回归 | 63/63 通过，退出码 0；后续回归，不是重新运行全部 859 项 | `async-platform-regression-cloud-02.xml` / `.log` / `.exit` |
| Windows | 成功，退出码 0；`Builds/Windows/EchoRun.exe` | `build-windows-01.log` |
| WebGL | 成功，退出码 0；导出清单 6 个文件大小/SHA-256 全部匹配 | `build-webgl-04.log`；`Builds/WebGL/build-info.json` |
| WeixinMiniGame 初次构建 | 成功，退出码 0；SDK 转换 `All done!`；当时云功能关闭 | `build-weixin-01.log` |
| WeixinMiniGame cloud-02（历史失败） | SDK 导出完成，最终退出码 1；恢复 profile 时 Win32 IO 1224 | `build-weixin-cloud-02.log` / `.exit` |
| WeixinMiniGame cloud-03 | 完整成功，退出码 0；用于已通过的真实 Unity fixture 挑战 | `build-weixin-cloud-03.log` / `.exit` |
| WeixinMiniGame cloud-04（历史失败） | SDK 导出完成，恢复 ProjectSettings 时再次 IO 1224；不计为完整成功 | `build-weixin-cloud-04.log` / `.exit` |
| WeixinMiniGame cloud-05 | 公开设置 API 修复后完整成功，退出码 0；28 个快照文件恢复一致；产物与关键源码指纹已保存 | `build-weixin-cloud-05.log` / `.exit`；`pre-cloud-build-state/manifest.json`；`cloud05-artifacts.json`；`cloud05-source-sha256.json` |
| 字体子集覆盖 | 扫描 8 个源文件、188 个字符串，1031 个字符、缺字 0；旧字体负例失败符合预期，meta 未变 | `font-applied-verification.json`；`font-coverage-old-negative.txt` |
| 最新邀请界面字体 | cloud-05 截图中的中文正常显示，字体视觉验收通过；不包含榜单界面验收 | `invitation-font-fixed.png`；`font-applied-verification.json` 的 `visualAcceptancePending:false` |
| 默认 GPU 下工具交互与榜单 | 实际点击打开工具菜单、好友入口和榜单，榜单显示本人 152.2 米、领先 -46.6 米，文字完整 | `devtools-recovery-and-board-ui.json`；`devtools-menu-default-gpu-recovered.png`；`leaderboard-cloud05-verified.png` |
| 无邀请的榜单错误提示 | 无 boardId 时显示“暂时无法加载挑战榜，请重试”；不是网络故障或云端空榜验证 | `board-no-invitation-ui.png` |

三个场景用例分别验证异步重开/暂停恢复/真实销毁/回菜单与存档隔离、普通重开、普通无输入校准自然结算。未声称运行了完整 PlayMode 套件，或完成手机交互验收。

最初无图形场景测试在现有截图代码 `Camera.Render` 中触发引擎原生崩溃；改为启用图形设备运行，没有删减旧测试或忽略错误。零时间平滑的原始 NaN 已由确定性测试复现，修复后完整 EditMode 和上述三个场景测试通过。

WebGL 修复 Git 管道后完成编译，但首轮引擎 Bee 构建图重算达到内部 6 次上限；保留缓存再次构建即成功，没有改动玩法源码来规避构建检查。

三个初次成功构建日志的 C# 编译错误均为 0。产物大小和 SHA-256 记录在 `TestResults/AsyncEcho-20260921/artifacts.json`；112 个实现/程序集/云协议文件的源码指纹记录在 `source-sha256.json`，初次构建期间没有变化。后续云入口诊断补丁另记于 `cloud-diagnostics-source-sha256.json`，原指纹和构建成功记录不能代替新版部署或云开启客户端的验证。

cloud-05 的五个构建文件大小和 SHA-256 已记录在 `cloud05-artifacts.json`；其中外部数据文件和原始 `webgl.data` 均为 79,473,617 字节，SHA-256 均为 `0AD773F537C30DDC077E9B970E017BA77317A936DBDB2FA6B2A5E66C4681828C`。`cloud05-source-sha256.json` 另记录 BuildConfig、微信运行时、字体及两份字体工具输入/脚本共五个关键文件的指纹；这是后续修改的补充记录，不替代初始完整源码清单，也不代表手机已加载这些产物。

构建收尾的两次 IO 1224 已按实际失败层修复。cloud-02 在重写已正确的 `WeChatV0.asset` 时失败；跳过语义相同的字段、保留换行和必要写前释放缓存句柄后，cloud-03 成功。cloud-04 随后证明单靠释放缓存句柄仍不足以安全截断 `ProjectSettings.asset`。最终改为在 `SaveAssets` 前调用公开 `PlayerSettings.MiniGame.SetActiveSubplatform(MiniGameBuildSubtarget.WeChat, false)`，对 ProjectSettings 只读校验字段唯一且为预期 0，失败明确报错，不再强写该映射文件。cloud-05 完整退出 0；28 个快照文件的 SHA-256 全部一致，API 设置的 `weixinMiniGameTemplate: APPLICATION:Default` 也与构建前快照一致。两次失败日志保留，不将它们追记为成功。

## 部署准备与待验收

初次部署版本备份：`Builds/AsyncEchoCloud-20260921/echo.zip`，包含函数入口、纯 handler、仓库适配器、协议、锁文件和已安装依赖；不包含整个仓库或个人资料。包 SHA-256、字节数记录在同目录 `manifest.json`。业务代码已通过官方 CLI 从函数目录上传；该旧压缩包不含之后新增的诊断补丁，不代表当前云端源码。

用户已授权继续云环境联调，并提供 `cloud1-d5g7uavtm2276f384`。官方 CLI 已核实该环境属于现有 AppID `wx0d080ce970cf217c`；首次查询没有集合和云函数。真实 SDK 业务检查完成后，本机被 Git 忽略的配置资源已设 `featureEnabled=true`。云开启后的 cloud-03 已完成实际 Unity 挑战联调，cloud-05 为包含后续字体和构建收尾修复的最新成功构建；先前 feature-off 构建不代替这些证据。

云端 `echo_shadows`、`echo_results`、`echo_scores` 创建均已由官方异步任务返回 `status=success / execution_success`。排行榜索引 `echo_board_ranking_v1` 创建成功，随后 `listIndexes` 返回的五个字段、顺序和升降序均与部署 JSON 一致（requestId `34fde55e-f2dd-41ce-ad72-165ca634f739`）。实际云端榜单查询已返回最小字段，top-50 与 top-1 的首项一致；最初用合成成绩验证，后续已由同一身份的真实 Unity 成绩更新最佳记录。尚不能据单身份结果证明多玩家并列排序已实测。

权限结论已纠正：先前仅凭三个控制台页面显示“所有用户不可读写”就判定全部生效，证据不足。真实客户端 SDK `get` 检查中，`echo_results`、`echo_scores` 返回 `-502003 permission denied`，但 `echo_shadows` 一度仍可读取。09:01 在控制台重新设置 `echo_shadows` 为“所有用户不可读写”，并看到权限变更完成；09:04 后真实 SDK 复测返回 `readAllowed:false`、`errCode:-502003`、`permissionDenied:true`。至此三个集合的客户端读取拒绝均已实测。客户端写入拒绝已有控制台配置，尚未单独通过 SDK 写入验证；此前 `cloud-permissions-echo-*.png` 截图不能独立证明最终权限生效。

首次业务部署已由官方任务返回 `success / execution_success`，上传 2,091 个文件、约 4.5 MB，替换了控制台默认模板。部署后 `cloud_fn_info` 复核状态为 `Active`、运行时 `Nodejs20.19`、超时 5 秒；配置和上传成功不等于业务调用通过。

初次真实 SDK 冒烟失败于首次 `publish`：返回 `TRANSIENT_UNAVAILABLE`，`callCount=1`，只完成本地 `fixture_version` 检查。独立 `get` 请求在 1,453 ms 后返回同样错误；`apiVersion:0` 在 633 ms 后仍返回该错误，而非预期的 `API_VERSION_UNSUPPORTED`。当时管理端 `echo_shadows total=0`。这些早期失败记录保留在 `cloud-integration-first-probes.json`，已不是当前云业务状态。

云日志服务原先未开启，现已在控制台开启，无法追溯此前失败请求的日志。最小脱敏阶段诊断通过 31/31 项 Node 回归，包含真实 SDK 入口的无凭证、无网络测试；其官方部署任务 `confirmation_cloud_fn_deploy_ce0dbeb4-453d-414d-ba35-f35f84125e3d` 已返回 `success`，记录见 `pending-cloud-diagnostics-deploy.json`。

本机 Windows 官方全量打包已复现嵌套 ZIP 条目保留反斜杠。随后通过已注册的 `cloud_fn_inc_deploy --file node_modules` 补传 2,077 个规范路径依赖，任务 `confirmation_cloud_fn_inc_deploy_eaef0030-f28c-423c-b850-d43ebdacf8bd` 返回 `success`；相同 `apiVersion:0` 请求恢复为 `API_VERSION_UNSUPPORTED`，请求 ID `f62457da-5bf8-4126-a617-532f63f4477d`。这是“全量路径初始化失败、规范路径补传后恢复”的实证，未取得云端原始 `MODULE_NOT_FOUND` 日志，不把该异常或所有工具版本受影响写成事实。增量不删除旧条目，错误反斜杠条目可能仍保留。依据见 `windows-deploy-path-audit.md` 和 `pending-cloud-dependency-repair.json`；维护步骤已更新到两份部署 README。

真实 SDK 单身份检查已验证：上下文归属发布、重复发布、冻结载荷原字符串返回、接受报告、重复报告保持相同回执，以及管理端相应挑战只有一条结果和一条最佳成绩。该阶段使用测试快照和距离 123.5 米、领先 -2 米的合成成绩，榜单最小字段、匿名标签及 top-1 一致性均已补验。函数保持 `Active / Nodejs20.19 / 5 秒`。脱敏汇总证据为 `TestResults/AsyncEcho-20260921/cloud-real-sdk-verified.json`；后续真实 Unity 成绩另见下文，不将合成数据冒充实际游戏结果。

部署预检发现本机微信 CLI 首次创建函数固定默认 Nodejs16.13 / 3 秒，且不读取 CloudBase CLI 的运行时配置。已更新部署说明：先控制台创建并核实目标配置，再上传本地锁定依赖；不得把 package.json engines 或本地配置文件当成云端运行时已生效的证据。

上述 SDK 脚本结论来自多次实际请求和只读补验，**不是一次完整脚本 9/9 成功**。恢复后的首次完整脚本曾因 Windows CLI 改变中文比较常量而误报榜单断言；逐码点比对证明服务端标签正确，辅助脚本已改用 ASCII Unicode 转义。随后一次完整脚本的自动化响应超时，剩余榜单断言由独立、有时限的只读请求验证通过。该阶段的合成成绩与下文实际 Unity 挑战分别记载，均只有一个真实登录身份。

## 真实 Unity 单身份挑战

cloud-03 修正 SDK 初始化成功值后，实际 Unity 云入口可用。微信开发者工具曾出现工具栏不响应、SDK 优化提示遮挡模拟器；首次正常重启后菜单短暂恢复，用户随后确认工具整体再次无响应。后台接口仍有响应只说明工具未完全死锁，不能据此确定卡顿原因。仅在生成项目的公开监控配置中设 `showSuggestModal:false`，监控本身仍开启；配置备份为 `unity-namespace-before-simulator-smoke.js`。

随后仅做一次关闭 GPU 的对照：用户确认菜单响应，但该诊断启动下 Unity 无可用 WebGL，不能用于游戏验收。通过正常 Alt+F4 和已核实的关闭按钮退出诊断进程，没有强制结束进程或清缓存。22:46 已恢复默认 GPU 启动，主进程 238476 的参数核实 `DisableGpu=False`；22:47 实际点击打开工具菜单，22:48 好友按钮打开界面，22:49 无邀请时点击榜单显示错误提示，22:50 选择既有邀请并核实实际 launch query 含 inviter、预期 fixture identity 和 rules=1，22:51 影子就绪后实际点击榜单成功显示成绩。新鲜截图证明本次默认 GPU 会话已恢复交互；不把对照结果归因为 GPU 故障，也不宣称卡顿已永久修复。

本次恢复和榜单 UI 的正式汇总为 `devtools-recovery-and-board-ui.json`；官方 `simulator_screenshot` 生成的 `leaderboard-cloud05-verified.png`（546×1179）已实际查看，榜单标题、本人标签、152.2 米及领先 -46.6 米均完整显示。该 cloud-05 UI 证据与 cloud-03 的实际游戏、报告证据分别保留。

通过官方支持的 `condition.game.list[].query` 添加本机编译条件，并在恢复响应的工具栏选择该条件。真实 `wx.getLaunchOptionsSync().query` 核实了邀请的 owner、fixture identity 和 rules=1。Unity 自动拉取云端冻结影子，界面显示第 1 代影子就绪；实际点击开始后进入 `AsyncChallenge`，HUD 显示好友影子和冻结说明。配置依据及 `current` 不保证自动选中的限制见 `invitation-compile-schema.md`。这是模拟器自定义邀请冷启动，尚不是另一账号从收到的真实分享卡片启动。

实际一局已结算并确认上报：距离 **152.151 米**、领先 **-46.57535 米**、未获胜；UI 四舍五入显示 152.2 / -46.6。`challengeId=13d2a2f089304c11baa2cb7c9cc071fd` 的管理端查询返回恰好一条结果（requestId `f42be0b6-f834-4e41-bef6-0851f4aea448`）。随后最佳成绩查询（requestId `060a20f1-c2b5-4daa-92b4-f5c882f7f898`）确认 `echo_scores` 更新为本次距离/领先和 challengeId，对应最佳成绩只有一条。22:51 的独立榜单 UI 验收显示玩家本人 152.2 米、领先 -46.6 米，与本次真实成绩一致，截图为 `leaderboard-ui-recovered.png`。

脱敏汇总为 `unity-cloud-run-verified.json`，截图包括 `cloud-client-menu-fixed.png`、`invitation-cold-start-01.png`、`async-run-started.png`、`async-run-result-01.png`、`async-result-panel.png`。对手来自 SDK 发布的真实序列化 fixture，并非另一玩家完成正常单合约后发布；未验证真实卡片送达、第二身份归属、手机运行或实际双槽存档字节前后对照。早期榜单点击曾被鼠标保护中断，另一次截图仍停在邀请界面，这些均未算通过；没有绕过鼠标保护。后续 `leaderboard-ui-recovered.png` 才补齐实际打开和成绩呈现证据。`board-no-invitation-ui.png` 仅验证没有邀请、缺少 boardId 时的错误提示，不冒充网络错误态；真实云空榜 UI 尚未验证。

本次实际界面发现字体子集缺少“刷友我括榜稍络绩绪邀”10 字，已补齐并保留全部原字符。`font-applied-verification.json` 记录新字体 447,860 字节、1031 个字符，8 个源文件/188 个字符串覆盖缺字为 0、meta 未变、可重建字节相同；旧字体负例仍能检出缺字。cloud-05 已打入该修复，最新 `invitation-font-fixed.png` 确认邀请界面中文字正常显示，验证记录已更新为 `visualAcceptancePending:false`；后续 `leaderboard-ui-recovered.png` 中榜单标题、本人标签和成绩也正常显示。

默认 GPU 会话的实际菜单和榜单交互已恢复，卡顿根因及长期稳定性仍不能由本次恢复推断。后续按下列顺序继续，保留每层实际结果：

1. 保留已通过的 SDK 云业务证据及合成成绩标记，补验三集合客户端写入被拒绝；需要多玩家/并列排序证据时使用不同真实身份，不能伪造 OPENID。
2. 补验真实云空榜和网络错误态 UI；准备手机可访问的资源地址，验证手机实际加载成功。cloud-05 字体视觉、榜单正常成绩显示、无邀请错误提示、产物指纹、构建收尾及单身份 Unity 上报已通过，不重复把它们列为未完成项目。
3. 使用两个不同登录身份。玩家 A 在真实 Unity 游戏内完成正常单合约、发布自己的 identity、发出含正确 query 的卡片；玩家 B 从卡片冷启动及热启动进入，界面和运行上下文都确认 `AsyncChallenge`，对手与 A 已发布的冻结快照一致。B 实际完成一局，验证报告的距离/领先/胜负对应本局、榜单归属 B。
4. 比较 B 挑战前后的本地 identity、进度、钱包及双槽存档，确认异步挑战不晋升、不发放长期收益、不覆盖自己的进度。验证对手在本局保持冻结；A 后续发布新 identity 后，旧卡片仍定位原快照。
5. 验证重复结束/重试只产生同一回执，重开产生新的 challengeId；暂停恢复、运行中收到邀请和回菜单行为正常。断网、云未开通、超时及切后台时应有不阻塞提示并可继续单机；迟到响应不得启动过期挑战或污染存档。

没有真实验证的项目不得从本地假 transport、单身份合成成绩或编译结果推断为通过。

初次 feature-off 微信导出的外部资源文件为 79,469,654 字节；该历史数值不作为 cloud-05 产物指纹，最新版以 `cloud05-artifacts.json` 为准。手机验收还需要手机可访问的资源地址，本机 localhost 预览不能证明手机链路可用。三个集合读取权限、SDK 云业务、单身份 Unity fixture 挑战与上报、cloud-05 构建收尾和设置恢复、字体视觉、榜单正常成绩显示和无邀请错误提示均已有实证。工具本次恢复交互不等于已查明根因；剩余验收包括客户端写入权限拒绝实测、真实云空榜和网络错误态 UI、实际存档对照、双账号真实分享及手机链路。

## 2026-09-22 HTTPS 静态资源准备

用户确认复用现有微信云开发存储的方向，保留腾讯大赛网站。只读 HTTP 检查确认 `http://110.42.222.236/` 返回现有 WebGL 页面；HTTPS 请求握手失败。没有修改服务器、执行旧部署脚本或覆盖大赛站点。微信产物不能直接复用该站点的旧 WebGL 数据文件。

已准备 `Builds/WeixinResources/20260922-cloud05-0ad773f5/`，上传白名单只有 `d215bba3a068ec5c.webgl.data.unityweb.bin.txt`。79,473,617 字节、SHA-256 `0ad773f537c30ddc077e9b970e017ba77317a936dbdb2fa6b2a5e66c4681828c` 与 cloud-05 记录及当前导出引用一致。WASM 已在小游戏分包，无额外外部 StreamingAssets/纹理目录。另有 45,866,500 字节 gzip 候选，仅在响应具备正确 `Content-Encoding: gzip` 时可替代原 URL 响应，不能直接伪装成原始文件。清单和候选不在默认上传范围。

此节点仅完成本地准备，尚未上传。云存储实际权限、固定公开 HTTPS 域名、额度、匿名下载及手机启动均待验证；不将 CLI 临时链接写入正式包。旧 CLI 内存会话已结束，待用户重新提供本次进程的鉴权。未改变游戏源码或当前导出资源地址，也没有启动 Unity 或宣称新的编译/手机验收通过。

## 工作区保护

原有美术、音频、界面和构建脚本修改保留，没有提交或推送。旧 Windows 和微信产物已保存到 `Builds/Windows-BeforeAsyncEcho-20260921`、`Builds/WeixinMiniGameV0-Clean-BeforeAsyncEcho-20260921`。初次三目标验证后曾恢复质量档 3→2 的构建差异并核对初始快照；后续 cloud-02/04 失败及 cloud-05 成功分别保留，不混用恢复结论。cloud-05 后重新核对 `pre-cloud-build-state/manifest.json` 中 28 个场景、工程设置和 profile 文件，SHA-256 全部一致，默认 MiniGame 模板也与快照一致。原本已有的未提交工程设置修改仍保留；字体修复属于已明确记录的源码变更，不在这 28 项设置恢复中回滚。

## 2026-09-22 资源上传后续实证

用户在开发者工具确认上传后，任务 `confirmation_cloud_manage_storage_962331d7-bc2f-43be-ace6-a947b8ef7f2a` 最终返回 `failed / execution_failed`，错误为 uploadFile 30 秒超时。不能把该任务记作 success；但独立 `info/list` 确認对象已经落库，版本前缀只有 1 个对象，79,473,617 字节，ETag `efb5d9aad215bba3a068ec5c52401d2b` 与本地 MD5 一致。没有重复上传。

通过官方 `cloud_manage_storage action=download` 下载回验时，管理调用同样在 30 秒超时后返回；实际传输继续，最终下载文件为完整 79,473,617 字节，SHA-256 `0ad773f537c30ddc077e9b970e017ba77317a936dbdb2fa6b2a5e66c4681828c` 与本地原始包完全一致。原始云对象 Content-Type 是 `text/plain`，没有 Content-Encoding；扩展名造成的类型推断没有改变下载字节。

官方返回存储 CDN 域名 `636c-cloud1-d5g7uavtm2276f384-1492594595.tcb.qcloud.la`。该域名下实际文件的无签名 HEAD 与有大小上限的 GET 均返回 403，COS 源站去签名 HEAD 也返回 403。带授权的管理下载成功不能替代公开下载验证。临时签名只用于本次运行，没有写入源码、正式包或长期证据。`DATA_CDN` 未更改，手机启动未通过；控制台一直加载，公开读取权限和额度仍未核实，未改变套餐或任何数据库/存储权限。脱敏记录已更新在 `pending-cloud-resource-upload.json`，其文件名保留用于追踪原任务，但内部状态已标记真实超时终态及独立文件核验结果。
