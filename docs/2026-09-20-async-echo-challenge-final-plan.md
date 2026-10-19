# 异步好友影子挑战：最终实施方案

创建：2026-09-20；最终复核：2026-09-21。状态：设计定稿，尚未实施、部署或通过验收。

本文是后续实施的完整工作约束。以当前工作区代码为基线；本方案不授权覆盖现有改动，不自动授权提交、推送、云部署或发布小游戏。实施时完成本地代码、测试和构建；外部部署及真实账号验证按届时已有授权执行。

**1. 产品范围与已确定的取舍**

在微信小游戏中增加“异步好友影子挑战”：普通单合约完成结算后，发布玩家自己已经提交的 identity；玩家主动分享；接收者取得冻结影子后确认开始挑战；挑战结束上报结果，并查看该影子的挑战榜。

最终采用以下规则，不留给实施者自行猜测：

| 项目 | 最终决定 |
| --- | --- |
| 后端 | 一个微信云函数 `echo`，云数据库；不自建服务 |
| 对手载荷 | 唯一模型 `ActiveEchoIdentity` 的原始 `ToJson()` 字符串，UTF-8 不超过 16,384 字节 |
| 榜单 | “该影子的挑战榜”：同一发布者、identity、规则版本下的参与者；不是微信通讯录好友榜，也不是全服榜 |
| 卡片 | `inviter=<openid>&shadow=<identityId>&rules=1`，全部按 query 规则编码 |
| 云快照 | 按 `(ownerOpenid, identityId, rulesVersion)` 唯一且不可变；重新发布同一份内容返回原记录 |
| 跨规则版本 | 新榜、新快照记录；允许重复保存同一份 identity JSON，不创建第二套 identity 模型 |
| 对局模式 | 追加 `AsyncChallenge=2`，保留 `SixPhaseLegacy=0`、`SingleContract=1`；默认仍为 SingleContract |
| 玩法 | 复用正常单合约赛道、预测门、安全约束、碰撞和胜负规则；不复制玩法引擎 |
| 冻结含义 | 对手权重、序列、风格、pace、路线记忆不更新；门预测不重学；动作仍根据当前场景和既有安全逻辑产生 |
| 本地收益 | 本局分数、金币照常显示；不累计本地最高分、钱包、训练资料、身份晋升或重试进度 |
| 允许本地写入 | 原有 runSequence、带模式标记的遥测、用户主动修改的设置；不修改双槽存档 schema |
| 排序 | 最佳一局的距离降序、领先米数降序、首次受理时间升序、稳定 entryId 升序 |
| 计分范围 | 到达终点或碰撞结束可以入榜；主动放弃只记录结果，不入榜 |
| 反作弊 | 云上下文身份、请求/字段/大小/引用关系校验；客户端分数尽力展示，无奖励，不做服务器权威模拟 |
| 失败策略 | 云功能失败不阻塞游戏；已开始的异步局保持异步结算，永不因断网改成本地晋升局 |
| 持久重试队列 | MVP 不做；只允许内存中的一次自动重试 |
| 数据保留 | MVP 不自动过期、不自动删除；去重记录与结果共同保留，监测用量后再独立设计清理 |

相较最初提示词，改动明确为：可变的个人最新影子改为不可变挑战快照；好友榜明确为邀请参与者榜；分享增加规则版本；优先复用已安装微信 SDK；异步局对本地长期进度只读；测试覆盖整条生命周期。

不做实时 PvP、匹配、聊天、内购、派奖、昵称/头像授权、跨设备同步本地成长、深度反作弊、后台无限补传、通用后端框架、全项目顺手重构。

**2. 当前代码事实与必须保护的边界**

已核对的当前源码事实：

- [GameManager.NormalizeGameplayFlowMode](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/GameManager.cs:409) 会把非 SingleContract 模式归到旧六阶段，需要明确识别新模式。
- [SingleContractGatePlan.RecordSettlement](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/SingleContractGatePlan.cs:74) 在连续反制后重学并重排未来预测，异步必须关闭这项适应。
- [ActiveEchoIdentity.FromJson](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/EchoIdentityData.cs:219) 会归一化版本，外部导入必须在归一化前校验版本。
- [AIShadowRunner.FinishSingleContractRun](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/AIShadowRunner.cs:1668) 在调用存档事务之前已经计算晋升及文案，异步必须提前分流。
- [StyleTracker.EndRun](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/StyleTracker.cs:143) 会持久化风格，不能只跳过 identity commit。
- [AIShadowRunner.OnDestroy](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/AIShadowRunner.cs:3788) 的 legacy 保存分支必须排除 AsyncChallenge。
- [TempleRun.Runtime.asmdef](C:/Users/zzz/Desktop/归档/TempleRun/Assets/Scripts/TempleRun.Runtime.asmdef:4) 已引用 Wx；本次包含小范围迁移安全区 SDK 读取，收回这一依赖。
- 当前工作区有 GameManager、UI、构建配置、微信运行层等既有改动。实施前记录差异和基线结果，禁止 reset、clean、整库暂存或擅自丢弃文件。

现有 `TryCommitSingleContractSettlement` 的事务、普通模式晋升条件、存档恢复与迁移语义保持不变。允许为接入调整调用方和抽取小段共同结算逻辑；不得借机重写存档系统或学习算法。

**3. 组件、程序集与依赖方向**

只新增一个生产程序集 `TempleRun.WeixinMiniGame`，位于 `Assets/WeixinMiniGame/`，引用 `TempleRun.Runtime` 和当前已有的 `Wx`。它始终参与编译，只有 SDK 的 using、类型和具体调用受 `MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR` 守卫。

依赖方向固定：

```text
EditMode/PlayMode Tests ──> TempleRun.Runtime
EditMode Tests ───────────> TempleRun.WeixinMiniGame ──> TempleRun.Runtime
                                                   └─> Wx
TempleRun.Runtime 不引用 Wx、WeixinMiniGame 或 Assembly-CSharp
```

核心只知道纯 C# 对手对象、规则、seed、challengeId 和纯结果。openid、云环境、分享 query、请求状态和 SDK 回调仅在微信层。

建议文件责任如下；命名可在不改变职责的前提下调整，不强制每个小类型单独成文件：

| 位置 | 职责 |
| --- | --- |
| `Assets/Scripts/GameplayFlowMode.cs` | 枚举、纯运行上下文；保持旧枚举编号 |
| 新增 `Assets/Scripts/EchoRunRules.cs` | 按模式生成不可变权限配置；调用方不能任意组合布尔开关 |
| 新增 `Assets/Scripts/AsyncChallengeRun.cs` | 纯异步启动参数、结果、规则 V1；不引用平台 |
| `Assets/Scripts/EchoIdentityData.cs` 或相邻小文件 | 严格外部读取及结构校验，仍使用同一 identity 类型 |
| `Assets/WeixinMiniGame/AsyncEchoCloud.cs` | 平台操作协调器、请求生命周期、发布/拉取/报告/榜单状态 |
| `Assets/WeixinMiniGame/AsyncEchoContracts.cs` | 云请求响应信封、错误码；不复制 identity 字段模型 |
| `Assets/WeixinMiniGame/AsyncEchoInvitation.cs` | 纯 query 编码、解析、规范化和验证 |
| `Assets/WeixinMiniGame/IAsyncEchoTransport.cs` | 最小云 transport 接口以及 unavailable 实现 |
| `Assets/WeixinMiniGame/WeixinAsyncEchoTransport.cs` | 当前官方 SDK 适配、异常转结果 |
| `Assets/WeixinMiniGame/WeixinSafeAreaProvider.cs` | 微信窗口和胶囊读取，回传 Unity 数值类型 |
| `Assets/WeixinMiniGame/WeixinMiniGameRuntime.cs` | 单一持久宿主、事件绑定、场景重绑、主线程派发 |
| `Assets/WeixinMiniGame/AsyncEchoPanel.cs` | 发布/分享、邀请确认、上传状态、榜单最小 UI |
| `Tools/CloudFunctions/echo/` | 一个可部署函数包，含适配入口、handler、校验/排名逻辑和测试 |

核心现有 `WeChatSafeArea.Resolve` 调用边界可保留；把其中 `WX.GetWindowInfo/GetMenuButtonBoundingClientRect` 移到 provider。核心只保留纯坐标转换及默认 fallback，微信层注册 provider。不得重新设计安全区布局。

EditMode asmdef 增加对微信程序集的引用，以 fake transport 测纯逻辑；不得从自定义 asmdef 反向引用默认 Assembly-CSharp。非微信无需 SDK 初始化，使用 unavailable transport，显示层默认隐藏微信功能。

当前 SDK 已有 `WX.cloud.Init/CallFunction`、`ShareAppMessage`、`GetLaunchOptionsSync`、`OnShow/OffShow`。默认不新增 jslib、不升级 SDK。只有实测存在接口缺口才补最小桥，并配置 PluginImporter、补目标构建与运行证据。

**4. Identity 外部输入协议**

`payloadJson` 永远是 `ActiveEchoIdentity.ToJson()` 字符串。云函数按原字符串保存，不拆成另一套影子 DTO，也不修改、修复或重新序列化其内容。

客户端新增 `TryFromExternalJson` 一类严格入口：

1. 检查非空及 UTF-8 字节数 ≤ 16,384；对收到的云响应同样检查。
2. 识别原始 JSON 中的 version；缺失、不为整数或未知版本直接拒绝，不能先 Normalize。style.version、memoryContract.version 同样按现有对应常量检查，不能让嵌套 Normalize 吞掉未来版本。
3. 直接反序列化到已有 ActiveEchoIdentity；检查顶层对象和必要字段，再检查语义。
4. 验证通过后才允许归一化/克隆；归一化不能把原本错误的输入变成可接受数据。
5. 要求 `RequiresRouteCalibration == false`，身份和 memoryContract 的归属一致；不将不可挑战的好友身份导入本地校准流程。

V1 必要结构：正整数 generation；非空有界 identityId；有效 parentIdentityId 关系；40 项有限 policyWeights；25 项有限且非负 sequenceTransitions；非负整数计数；有效 style；有限且为正的 pace；有限且非负的 sourceCourseDuration；合法且精确的路线记忆。数组维度在 C# 用现有常量，Node 的 V1 校验以跨语言 fixture 验证一致性。

字符串 ID 上限：身份、父身份、合约 ID 各 128 个字符；ownerOpenid 最大 128 字符；challengeId 最大 128 字符。邀请和存储键中的 ID 只接受安全的 ASCII 标识字符，不把它们当可执行路径或数据库查询表达式。合法现有 `echo-`、`legacy-` 标识必须兼容。

数值护栏属于防崩溃校验，不属于反作弊：不接受 NaN/Infinity、整数溢出、错误数组长度；权重范围使用现有模型可接受范围，禁止依赖构造器偷偷重置默认权重。具体范围由现有 identity/模型定义提取并写入 fixture，不凭空收紧合法历史数据。

本地旧档继续使用已有兼容入口。不得让外部严格检查改变旧档迁移、回退或 recovery 的行为。C# 可复用已有身份一致性校验；Node 不复刻 StableHash/CreateIdentityId。

发布时只读取 `EchoRunSaveSystem.GetActiveEchoIdentity()` 返回的克隆，不序列化当前对手、草稿或旧局残留引用。普通结算未产生有效可挑战身份时不发布。

**5. 四种版本各自负责什么**

| 字段 | V1 值 | 含义 |
| --- | --- | --- |
| `apiVersion` | 1 | 云请求/响应信封协议 |
| `schemaVersion` | 1 | 云集合文档结构，三个集合都带此字段 |
| `payloadJson.version` | 当前 ActiveEchoIdentity.CurrentVersion，即 1 | identity 数据结构 |
| `rulesVersion` | 1 | 挑战玩法和计分语义；不是存档版本 |

读写遇到不支持的版本时返回明确错误，不归一化为当前版本，不用最新记录替代。云 metadata 的 identityId、generation、pace 从解析后的 payload 提取，不采信客户端另传同名值。

Rules V1 使用：标准难度、95 秒目标赛程、起速 10、最高速 24、加速度 0.12，沿用当前单合约预测门时间表、碰撞和领先结算。数值依据当前 `Assets/Resources/Config/game-balance.json` 和 SingleContractFlow；异步配置不得修改共享配置文件或用户难度偏好。

这组配置是生产规则，不开启 `SingleContractValidationConfig`。在开局统一冻结速度配置，同源供玩家速度、赛程、预测门窗口、影子 pace 归一化及影子速度读取；不能只覆盖 CourseDistance。退出 Async 后普通模式重新读取当前 balance。赛程、胜负、预测门、对手动作规则或影响排名的平衡数据发生变化时必须评估并提升 rulesVersion。美术、文案等不影响成绩的变化无需提升。

旧规则不得原地换内容。升级后可以继续支持旧规则，或明确返回 `RULES_RETIRED/RULES_UNSUPPORTED`；旧卡片不得无声改绑新版。保留历史数据不等于承诺永久维护所有旧客户端玩法。

**6. 云数据模型**

三个集合默认禁止客户端直读直写，仅通过云函数访问；客户端不持有服务端密钥。openid 是定位信息，不是权限凭证。

`echo_shadows`：

```text
_id                 boardId：确定性编码/散列(ownerOpenid, identityId, rulesVersion)
schemaVersion       1
ownerOpenid         cloud.getWXContext().OPENID
identityId          从 payload 提取
payloadVersion      从 payload 提取
payloadJson         原始字符串
generation          从 payload 提取
pace                从 payload 提取
rulesVersion        请求的受支持规则版本
runSeed             首次成功发布时服务端生成并存储的非零正整数
createdAt           服务端时间
```

记录不可变，不放 bestDistance。相同键相同 payload 返回原记录、原 seed；相同键不同 payload 返回 `SNAPSHOT_CONFLICT`。不同规则版本允许保存同一 JSON 的独立记录，以保持一个文档对应一个榜，避免可变规则绑定字典。

确定性数据库键可使用 Node 标准 crypto 对带长度前缀的元组做 SHA-256。这只是存储键，不是验证 identity 内容，更不是移植客户端哈希。runSeed 生成器可注入；并发首次发布必须返回事务最终胜出的已存 seed。

`echo_results`：

```text
_id                 确定性键(challengerOpenid, challengeId)
schemaVersion       1
challengerOpenid     云上下文身份
challengeId         客户端开局创建一次、重试复用
boardId             已存在的目标快照
rulesVersion        与目标一致
distanceMeters      本局实际距离
playerLeadMeters    本局结算后的领先值，可为负
endReason           finish_reached / collision / abandoned
playerWon           核心结果；必须满足基本字段一致性
eligible            服务端按 endReason 决定
acceptedAt          首次受理的服务端时间
```

`echo_scores`：

```text
_id                 确定性键(boardId, challengerOpenid)，也是对外 entryId
schemaVersion       1
boardId
challengerOpenid
bestChallengeId
distanceMeters
playerLeadMeters
endReason
playerWon
achievedAt          最佳这一局的 acceptedAt，不是最近刷新时间
```

最佳结果必须整体替换。不能分别 max(distance) 和 max(lead)，拼出从未发生过的一局。同一玩家的最佳一局按完整比较键 `(distance DESC, lead DESC, acceptedAt ASC, challengeId ASC)` 选择，避免并发下按请求提交先后与时间排序产生歧义。acceptedAt 在实际成功创建结果的事务尝试内取服务端注入时钟；失败尝试的时间不入库，重复请求沿用已存时间。不同玩家在榜内剩余并列按稳定 entryId 排序。

排行榜只查询 echo_scores，不扫描或临时聚合全部 results。创建复合索引：`boardId ASC, distanceMeters DESC, playerLeadMeters DESC, achievedAt ASC, _id ASC`；由目标数据库实际支持的索引配置验证。若数据库不支持该排序组合，调整为有明确并列语义的等价方案，不能在截断 top-N 后随意重排造成边界漏项。

MVP 无 TTL。记录数量会随发布、挑战和参与者增加；部署说明必须记录用量、费用监测和停用方式。未来删去重记录或复用榜 ID 会影响幂等，不能当普通清理脚本随意执行。

**7. 云函数 API 和事务**

函数目录 `Tools/CloudFunctions/echo/` 自包含：`package.json`、lockfile、`index.js`、handler、校验/排名辅助文件、`test/` 和 README。SDK 版本及 Node runtime 必须在部署预检确认并固定，不能用 `latest`。使用 Node 内置测试运行器即可，不引入 Web 框架。

`index.js` 只负责初始化 SDK、获取云上下文、构造数据库适配器及调用 handler。handler 接收 repository、context、clock、seedFactory 等少量依赖；字段校验、排名比较为纯函数。SDK 异常映射成协议错误，不把原始堆栈、完整 openid 或 payload 返回用户。

统一响应：

```json
{"ok":true,"apiVersion":1,"data":{}}
```

```json
{"ok":false,"apiVersion":1,"error":{"code":"RULES_UNSUPPORTED","retryable":false}}
```

所有 action 都要求云上下文 OPENID。缺失即 `UNAUTHENTICATED`，绝不回退请求中的 openid。客户端传入的 inviter 只用作读目标；report 的分数归属永远是上下文中的 challenger。

| action | 请求字段（另含 apiVersion/action） | 成功返回 |
| --- | --- | --- |
| publish | payloadJson、rulesVersion | boardId、inviterOpenid、identityId、generation、rulesVersion、runSeed |
| get | inviterOpenid、identityId、rulesVersion | found；找到时附 boardId、payloadJson、ownerOpenid、identityId、payloadVersion、generation、rulesVersion、runSeed |
| report | boardId、challengeId、rulesVersion、distanceMeters、playerLeadMeters、endReason、playerWon | receiptId、acceptedAt、duplicate、eligible |
| leaderboard | boardId、limit | boardId、rulesVersion、items；每项 rank、entryId、displayLabel、isMe、distanceMeters、playerLeadMeters、playerWon |

publish：先检查字符串大小及结构，metadata 从 payload 提取；事务中按固定 key 获取，存在则比较原 payload，不存在则创建。必须是原 payload 精确一致才视为幂等，客户端重试复用已冻结的同一字符串。

get：精确匹配三元组；缺失正常返回 `{found:false}`。未知文档版本、未知规则、损坏载荷返回明确错误；不返回其他身份作替代。服务端和客户端均验证返回目标与请求一致。

report：只接受有限距离/领先值，距离非负，字符串有界、枚举合法、playerWon 为布尔。校验胜利只能发生在 `finish_reached && playerLeadMeters >= 0`，其余为失败；这只是字段一致性，不证明客户端成绩真实。为防异常数值，对距离与领先的绝对值设宽松传输上限 10,000,000 米，超过直接拒绝；不上服务器模拟。

report 必须在同一事务中完成：

1. 读取固定结果 key。已有且规范化业务内容一致时返回原回执；不同则 `CHALLENGE_CONFLICT`。不重复更新榜单。
2. 对新结果读取 board，检查存在、schema 和规则；未知/退役规则不接受新成绩。
3. 创建结果；`finish_reached/collision` 可计入榜，`abandoned` 记录但不更新最佳成绩。
4. 读取固定 score key，以完整结果比较并更新。
5. 一起提交；任意失败整体回滚。事务内部不得发网络请求或生成外部副作用。

数据库事务冲突由适配器作有界重试，最多一次；重新执行回调仍使用同一业务输入，不生成新的 challengeId。用时耗尽返回可重试错误，不能无限循环。客户端重试与服务端冲突重试分别计数并写清日志。

重复回执查询优先于规则停用检查，保证已成功受理的旧请求仍能得到原回执；不允许以旧回执为由提交不同内容。

leaderboard：先检查 board 存在及 schema；不存在返回 BOARD_NOT_FOUND，存在但无成绩才返回成功空数组。规则退役后可继续读取历史榜，无需运行旧玩法，但未知文档 schema 仍拒绝。limit 默认 20，合法整数范围 1–50，超界限幅，错误类型拒绝。只返回最小投影，不返回 payload、完整 openid、style、权重或全部历史结果。显示名使用服务端生成的匿名编号，`isMe` 由云上下文判断，不引入昵称授权。查询失败返回错误，不伪装成空榜。

错误码至少区分：`INVALID_ARGUMENT`、`PAYLOAD_TOO_LARGE`、`PAYLOAD_VERSION_UNSUPPORTED`、`PAYLOAD_INVALID`、`IDENTITY_NOT_CHALLENGE_READY`、`API_VERSION_UNSUPPORTED`、`SCHEMA_UNSUPPORTED`、`RULES_UNSUPPORTED`、`RULES_RETIRED`、`UNAUTHENTICATED`、`SNAPSHOT_CONFLICT`、`CHALLENGE_CONFLICT`、`BOARD_NOT_FOUND`、`TRANSIENT_UNAVAILABLE`。普通 get 未找到不包装成内部异常。

云事务与上下文身份能力依据：[CloudBase 事务说明](https://docs.cloudbase.net/database/transaction)、[微信云函数身份说明](https://docs.cloudbase.net/recipes/add-cloud-function-wechat-miniprogram)。具体 SDK 调用形式在锁定版本上验证。

**8. 核心运行配置与身份隔离**

RunRules 由模式生成，而不是让各组件自行猜测：

| 权限 | SingleContract | AsyncChallenge |
| --- | --- | --- |
| UsesSingleContractRules | true | true |
| AllowIdentityTraining | 现有行为 | false |
| AllowGateRelearning | 现有行为 | false |
| AllowPlayerTraining | 现有行为 | false |
| AllowDirectorTraining | 现有行为 | false |
| PersistRunProgress | 现有行为 | false |
| AllowIdentityCommit | 现有行为 | false |

SixPhaseLegacy 保持原行为。验证模式仍保留原用途，不把异步伪装成 transient validation。

核心入口采用类似以下语义，不要求逐字复制签名：

```text
TryConfigureAsyncChallenge(ActiveEchoIdentity opponent,
                           AsyncChallengeRunParameters parameters,
                           out failure)
TryStartConfiguredRun(out failure)
RunFinalized(RunFinalizedData result)       // 同步结算结束后、最多一次
```

Parameters 只含 challengeId、rulesVersion、runSeed 等纯值；不带 openid/SDK/云回调。失败用返回结果表达，不把坏云输入作为未处理异常。只允许在 Menu 配置，成功时深克隆身份，并在开局时冻结完整上下文。协调器在开局前按 challengeId 固定对应的 boardId 和邀请元数据，上传使用该映射，不能读取之后可能已被新邀请替换的“当前 board”。

本地 identity 与本局 opponent 使用不同变量/访问器。菜单、普通局发布、档案展示始终读取自己的 identity；HUD 的对手代数、路线记忆、pace 读取本局 opponent。不可把好友身份写入 `_activeSingleContractIdentity` 后再靠退出时恢复来规避污染。

GameManager 在发布 Playing 状态之前确定完整上下文、模式、规则、对手代数、赛程和 seed；其他组件不能再根据本地存档自行推断本局是否有对手。SingleContractFlow 允许由上下文表达实际 Mode，普通构造/调用的默认行为维持不变。

Async 不创建 RunIdentityDraft；保留门结算与领先累计，但关闭 GatePlan 的适应动作。连续反制后，hypothesisVersion、预测策略、未出现门的预测不得因学习改变。无需禁掉门的生命周期或反馈统计。

**9. 导演、风格、分数与本地存档的具体处理**

保留正常赛道导演：它从本地存档快照构造局内策略对象，正常 Select、执行安全约束和 BuildPlan；禁止 Update、训练计数增长和 SaveDirectorModel。不要复用验证用 FrozenObserve，因为该分支会取消普通障碍，不符合正常单合约玩法。

导演从本地策略读取仅影响出题基线；本次不做统一服务器赛道。相同 board 的 runSeed 相同，可以复现预测门随机性，但由于本地导演基线和运行输入不同，**不承诺所有玩家赛道完全一致或排行榜具有竞技公平性**。这是无奖励 MVP 的已接受边界，不通过额外改造 AI 算法掩盖它。

Async 下 AIShadowRunner 获取影子导演指令时使用现成 `ShadowAIDirective.Neutral`，避免当前玩家表现通过动态 risk/noise 指令调整好友影子。对手仍使用已存权重、序列、风格及现有场景安全决策；冻结不等于逐帧录像回放。

StyleTracker、AIPlayerSkillEstimator 只扩展生命周期启用参数，默认保持 true；Async 设为 false，使 Record/Tick/EndRun 无训练、无 profile 内存变化、无持久化。不能只跳过最后一次保存，否则下一局会读到已污染的静态 profile。

GameManager.SaveHighScore 在修改 HighScore/TotalCoins 之前检查 PersistRunProgress。Async 将 IsNewHighScore 置 false，保持 Score/Coins/Distance 的局内显示，但不累计长期进度。结果页清楚标注“本局不影响你的回声成长和本地纪录”。

允许 ReserveRunSequence 按现有机制递增，允许遥测；因此测试比较受保护数据和单合约双槽，不要求所有 PlayerPrefs 或 legacy 存档 envelope 的每个字节都不变。禁止全局关闭存档写入，避免设置、遥测和序号一起受损。

Pause/ReturnToMenu/OnDestroy 继续正常清理；Async 绝不进入 legacy SaveProfile，也不能更新本地 retry、晋升结果或 lastOutcome。已有存档初始化、恢复、迁移仍正常运行；隔离断言在测试完成基线初始化后开始。

**10. 结算、再次挑战与退出**

复用现有 `_runFinalized` 或等价机制，所有结算入口共用一次性门闩。顺序固定为：取消/完成末门 → 消费待处理结算 → 固定距离和领先 → 计算胜负 → 保存纯结果 → 发布一次完成事件。

Async 在构建 promotion、认知升级文案、本地 commit/retry 之前分支退出。胜利仍是到达终点且领先不小于零；碰撞、放弃不算胜利。结果快照含 challengeId、rulesVersion、opponentIdentityId、runSeed、distanceMeters、playerLeadMeters、reason、won；UI 和微信协调器只读此快照，不在稍后读取已经复用的 GameManager 数值。

普通 SingleContract 的晋升事务仍按原顺序执行。普通局用于发布的通知只能在实际本地结算完成之后产生，不能依赖多个 OnStateChanged 监听器的注册顺序。UI 显示和云发布不能决定本地事务是否发生。

再次挑战保留同一已取得的冻结对手和 board/runSeed，真正开始新局时生成新 challengeId。推荐运行时用 Guid，测试注入固定 ID，不依赖 timestamp 唯一性。网络重试、手动补传同一已结束结果均复用原 ID 和原结果。

返回菜单或选择“单机开始”清除当前挑战配置，默认 SingleContract；不得只恢复界面不恢复模式。场景重载若用于再次挑战，携带完整纯运行配置，不只携带 enum；静态中转消费后立即清空，避免第三局误继承。

GameManager 销毁不应取消已形成结果的内存上传；持久平台协调器持有小型不可变结果。强杀应用导致没有结果/未送达是 MVP 明确接受的边界，不为此写入双槽存档或增加恢复队列。

**11. 发布、分享、冷热启动与 UI**

正常 SingleContract 结束并完成本地结算后：若有挑战就绪 identity，异步发起 publish。失败不改变本地结算；没有 identity 或仍需路线校准则提示继续完成本地校准，不发布伪对手。

同一会话成功发布同一快照后可复用回执。自动发布在每个结算结果上最多触发一次；前一个发布未完成时不重复发同一操作。若出现更新的本地 identity，UI 以当前选择为准，旧发布成功只能缓存自己的回执，不能覆盖新身份的分享按钮。

分享按钮必须在 publish 成功、拿到云确认的 inviter/identity/rules 后启用。由用户主动点击调用 ShareAppMessage，发布回调不得自动弹分享。分享只代表请求打开微信分享界面，不能显示“已发送”或“好友已收到”。用户取消不影响已发布影子。

Query 规范示例：

```text
inviter=o_example&shadow=echo-example&rules=1
```

编码和解析是同一套纯函数。SDK 提供的 query 对象和测试字符串最终进入同一规范化校验。字符串解析拒绝重复的必需字段、缺失/空字段、非法百分号编码、超长值、非法 ID 和不支持的 rules；未知非冲突字段可忽略。总 query 最多 1,024 UTF-8 字节。V1 必须有 rules，不猜测缺失版本。遇到普通启动参数、没有任何邀请字段时正常启动单机且不弹错误。

同时处理 GetLaunchOptionsSync 和 OnShow；绑定一次，走同一入口。首次启动两个事件携带同一邀请时合并，去重仅针对同次进入的重复通知和正在进行的请求，不永久屏蔽某个 board。菜单保留“最近邀请”入口，可显式重新接纳同一影子；消费/忽略后普通切回前台不反复自动弹窗。若 SDK 无法可靠区分再次点同卡与普通 OnShow，不承诺自动识别，但入口必须始终可达。结果页“再挑战”另走明确按钮。

收到邀请时：菜单里拉取并显示确认面板；跑局、暂停或结果页中只保留一个最新待处理邀请，给不阻塞提示，不抢占当前局。回到菜单后再展示。多个邀请 A/B 切换时增加 request epoch，A 的迟到结果不得替换 B。

邀请面板：影子代数、冻结对手说明、“开始挑战”“单机开始”；无须显示完整 openid。拉取中仍可进入普通单机，开始单机即使本次 get 失效。拉取失败、版本不支持或影子失效显示具体短文案，不自动开局。

挑战 HUD 复用现有单合约展示，增加“好友影子”标记；不展示“重学/晋升”反馈。结果页显示胜负、距离、领先、上传状态、“再挑战”“挑战榜”“返回菜单”。上传失败或未确认时显示“重新确认上传”，复用原 challengeId 和不可变结果，创建新的有界逻辑操作；进行中禁用重复点击。异步结果页不提供“发布我的影子”按钮，回普通模式发布入口只读取自己的本地 identity，不能把好友对手作为自己的影子再发布。

榜单必须覆盖：加载、成功、空态、错误、重试五种状态。空态“还没有挑战成绩”；错误态“暂时无法加载”；旧列表若保留显示须标记未刷新，不能当成新结果。自动上传成功后刷新当前对应榜一次，不轮询。

**12. 请求生命周期、超时和优雅降级**

一个持久 WeixinMiniGameRuntime 承载协调器；Awake 防重复，DontDestroyOnLoad；场景变化重新绑定 GameManager 前先解绑旧实例。启动/销毁时准确注册/注销同一个 OnShow delegate。禁用域重载的 Editor 测试也必须重置静态状态。

每个逻辑云操作保存 operationId、attempt、deadline、完成标记和上下文 epoch。它们不进入本地身份存档。SDK 回调转成纯消息，在 Unity 主线程统一处理；不得直接从回调改场景、GameManager 或 UI。

一次尝试最多等待 5 秒，整个逻辑操作最多 2 次尝试，总预算 10 秒，无额外自动重试。永久错误（参数、版本、权限、冲突、缺少配置等）不重试；临时网络/服务错误或超时最多重试一次。第二次只使用剩余总预算。SDK 若自带重试，应关闭可配置的额外自动重试或将其纳入预算，不允许多个包装层各自重试造成无界请求。

时钟可注入，运行使用不受 timeScale 影响的单调实时时钟；进入后台后代码可能不执行，恢复前台先检查原 deadline，再处理排队结果。不得声称操作系统挂起期间仍能保证准时执行回调。

第一次尝试超时后，其 attempt 回调失效；重试使用同一业务幂等键。整个操作结束后所有迟到成功/失败都忽略。若云端已成功但两次响应都丢失，客户端状态是“未确认”，不能断言服务器未收到；用户可用同一结果再次确认，仍复用原 challengeId。

手动重试是用户发起的新逻辑操作，仍有同样上限；publish/report 的业务键和已冻结业务内容保持不变。后台不保存 outbox，不每帧请求，不为等待云暂停游戏。只保留当前邀请、最近邀请和最近一局的可手动确认结果；已发出的在途上传保留到终态后释放，不能让跨场景请求表和历史结果无限积累。

| 故障阶段 | 行为 |
| --- | --- |
| 非微信、无 AppID、无云配置/能力 | unavailable；默认隐藏功能，主动调用返回结构化不可用 |
| 云初始化或 publish 失败 | 本地成绩照常结算，分享按钮不可用，至多一个短 toast |
| get 失败/载荷无效/版本不支持 | 不进入 Async；保留单机入口，不使用半解析对象 |
| 挑战中断网 | 继续跑已经取得的对手，不切换模式 |
| report 超时/失败 | 保留本局结果和“不确定/未上传”状态，不晋升、不回滚、不阻塞重开 |
| 榜单错误 | 错误态和用户重试，不能伪装成空榜 |
| 销毁/换邀请/开了另一局 | 过期 UI/get 回调忽略；已形成结果的上传仅更新自己的操作记录 |

一个操作最多一个用户提示；后台自动重试不重复 toast。异常在平台边界记录简短 code/action/operationId，日志不输出完整载荷、openid、AppID、token。处理已知云故障并不意味着吞掉所有核心编程异常。

**13. 测试矩阵**

测试不依赖真实云、不读取生产数据库。优先扩充既有隔离/流程/存档测试；fake repository 必须能注入事务冲突与中间失败，不能只有一个串行 Map 就宣称并发幂等已验证。

| 层 | 必须覆盖的行为 |
| --- | --- |
| Identity EditMode | ToJson→严格读取→ToJson 稳定；数组深拷贝；缺失/未知顶层及嵌套 version；超限 UTF-8、多字节边界；错误数组和非有限数；路线记忆不足；不改变原对象/旧档读取 |
| 核心 EditMode | 本地有/无 identity 都能注入好友对手；赛程由好友挑战决定而非本地校准状态；连续反制不重学；权重/序列/风格/pace 不变；普通 SingleContract 仍重学与晋升；共享 balance 改变时 Async 仍用 V1 同源参数，下一局 SingleContract 恢复普通配置 |
| 存档隔离 | 胜、负、碰撞、放弃、菜单、重开、销毁后 identity/generation/parent/retry/lastOutcome、单合约双槽、legacy shadow、风格/技能/导演模型不变；内存 profile 也不变 |
| 进度隔离 | Score/Coins 可增长但 HighScore/TotalCoins 不变；runSequence 和带模式遥测允许改变；退出 Async 后正常训练、保存和晋升恢复 |
| 结算生命周期 | 多入口结算只产生一次结果；对 UI 无依赖；新局新 challengeId、网络重试同 ID；重新加载场景后对手和规则不丢失 |
| Query EditMode | 正常编码往返；query 字符串/字典一致；必需字段重复、缺失、非法编码、长度、未知 rules；普通启动无邀请无提示 |
| 平台协调器 | unavailable；fake 成功/失败/超时；暂停时计时；最大两次；后台超过总预算恢复后不再自动尝试；迟到/重复回调；A 邀请被 B 替换；同卡再次进入仍有最近邀请入口；开单机后 get 回调无效；场景重绑不重复订阅 |
| UI/PlayMode | 加载/空/错误状态；发布成功前不能分享；普通结果无变化；异步胜利不出现晋升文案；普通障碍仍存在；再挑战/菜单实际恢复正确 |
| Node | 四 action 分派；伪造 openid 无效；原载荷边界与版本；同发布幂等、冲突和并发 seed；get 精确目标；缺少本地影子的新用户可 report |
| Node 事务 | 同挑战并发只一条；相同 ID 异内容、改 board 或 rules 冲突；创建结果后注入失败全部回滚；失败重试可成功；不同挑战竞争最佳值不丢更新；放弃不入榜；成功后规则退役再重试仍返回原回执 |
| Node 排名 | 同一局整体替换；距离/领先/时间/ID 排序与并发一致；top-N；不同 board/rules 不混排；已存在 board 空数组；不存在 board 返回错误；退役规则历史榜仍可读；最小投影不含 payload/openid |
| 回归 | 既有 SingleContractFlow、SingleContractRuntime、EchoIdentityPersistence、AIShadowSingleContractIsolation、GameState、UI/PlayMode 相关测试 |

在本地测试初始化、迁移完成后采样基线，避免把正常首次建档算作异步污染。测试清理必须恢复已有 PlayerPrefs，不擦真实玩家数据。

同版本协议 fixture 由实际 ActiveEchoIdentity 导出，供 C# 和 Node 共同读取。Node 不实现新的 identity 生成器；fixture 不含真实 openid/玩家隐私。测试重复报告时检查数据库最终状态与回执，不只检查返回 ok。

**14. 分阶段实施与通过条件**

每阶段通过自己的检查再推进依赖阶段。已有明确授权的实施会话无需每阶段重复请求确认；遇到外部条件不足先完成可独立验证工作并如实记录。

| 阶段 | 交付 | 通过条件 |
| --- | --- | --- |
| 0 基线 | 当前 branch/HEAD/差异、相关程序集/版本、当前针对性测试结果、真实环境前提列表 | 既有失败与新改动可区分，用户文件得到保护；无并行编辑器冲突 |
| 1 纯协议与规则 | 微信新 asmdef 骨架及测试引用、严格 identity 读取、RunRules、启动/结果纯类型、query、fixture | EditMode 可直接引用纯平台逻辑，覆盖版本、拷贝、校验、解析；旧存档回归不变 |
| 2 核心离线闭环 | 假对手注入、普通赛道、冻结预测、异步结算、无长期收益、恢复普通模式 | 隔离/生命周期 EditMode 及必要 PlayMode 通过；云尚未接入也能跑完 |
| 3 云函数 | 四 actions、事务适配、三集合与索引说明、Node 测试、lockfile | node 单跑测试通过，包括并发/部分失败/排行最小字段；函数目录可独立部署 |
| 4 平台接入 | SDK 适配、safe-area 迁移及移除 Runtime 的 Wx 引用、超时/回调、冷热启动、分享、UI | fake transport 测试和跨程序集编译通过；普通安全区/布局回归通过 |
| 5 本地验收 | 完整 EditMode、相关 PlayMode、Windows/WebGL/Weixin 构建日志与产物 | 当前源码的新结果通过；每个目标有独立日志，不能用旧产物或泛称三平台替代 |
| 6 微信集成验收 | 经授权部署、两个测试微信身份、冷/热启动分享、上报及榜单、失败演练 | 真实云文档及画面证明闭环；缺任何前提则明确未完成，不勾选全链路 |

构建入口沿用 BuildConfig.BuildWindows、BuildWebGL、BuildWeixinMiniGameV0。微信使用当前正确 buildTarget/subplatform；不要仅手动加宏当作微信构建。现有 CI 的第三目标是 Android，不是微信，需为微信添加独立构建/人工验收记录。保留既有 Android CI；核心触及共享生命周期后的现有回归失败不得忽略。

不每改一行就跑三平台完整构建；每阶段做针对性验证，核心/平台集成后统一完成目标构建，修改引入新风险再重跑相关目标。测试、构建、实际启动、实际玩法、两个账户云链路分别报告。

**15. 部署和运行说明的交付要求**

必须更新 `Assets/WeixinMiniGame/README.md`，并在函数目录提供短 README，至少覆盖：

- 精确 Node runtime、wx-server-sdk 固定版本、npm 安装及 node 测试命令。
- 从独立函数目录部署 echo 的步骤；如开发者工具需要 cloudfunctionRoot，使用明确配置，不能只改一次即被构建覆盖的导出文件。
- 三个集合、权限和复合索引；api/schema/payload/rules 版本表、不可变快照、结果去重和数据保留。
- 云环境 ID 与 AppID 的注入方式、开关、SDK init 顺序；敏感值不提交，配置缺失时功能不可用而主游戏正常。
- 真实预览前提：有效小游戏 AppID、云环境及权限、已部署函数/数据库、两位测试身份、双方可访问的游戏资源地址。
- 当前小游戏资源可能走外部加载；本机 localhost 预览不能证明好友手机可打开。按当前构建 README 配置可达资源与域名后再做真实分享。
- 失败文案、重试上限、无持久 outbox、成绩不权威、不同本地导演基线可能影响赛道、通关距离并列规则。
- 用量与错误监测、客户端 featureEnabled 开关、云停用方案；停用只关闭可选云入口，不删除用户本地档案、不让在途 Async 变成晋升局。
- 手工验证清单：A 普通局发布并分享；B 无本地 identity 冷启动挑战；B 已打开时热启动挑战；A 发布新代后旧卡片仍指向旧影子；重复上报只有一条结果；发布/拉取/上报/榜单错误路径；退出后普通玩法恢复。

**16. 完成定义与交付格式**

- [ ] 核心 Runtime 无 Wx/WeChatWASM 依赖；微信代码在平台目录，Windows/普通 WebGL 为 unavailable。
- [ ] identity 外部读取严格、单一载荷、版本和 UTF-8 上限验证通过；普通存档恢复行为不变。
- [ ] 正常单合约规则被复用；普通障碍保留，好友预测不学习，普通 SingleContract 学习和晋升回归通过。
- [ ] Async 胜负/放弃/重试/销毁不会污染本地身份、训练或长期进度；重新回单机后全部正常恢复。
- [ ] 云函数独立可部署，三集合/权限/索引文档完整；并发发布/report 幂等及失败原子性通过本地测试。
- [ ] 卡片经过 publish 确认，冷/热启动统一处理，旧快照和规则不会被替换；迟到回调不会改变新局。
- [ ] 榜单仅该影子参与者，最佳结果来自同一局，有明确排序和最小字段，空态/错误态均可用。
- [ ] EditMode 与必要 PlayMode 有新测试报告；Windows、WebGL、WeixinMiniGame 分别有当前构建证据。
- [ ] 微信真实环境两账号全链路和离线/未配置云演练完成，有对应截图/日志及云结果；未执行则保持未勾选。
- [ ] 两份 README、变更文件清单、残余限制与运营成本说明齐全。

最终实施报告分别列出：实际改动和边界；测试结果文件；各平台产物及日志；微信部署/两账号验证证据；未完成项及原因。没有真实云条件时可以交付本地实现，但不能称为端到端完成。不得为了让清单全绿伪造测试、放松核心隔离或把未验证项写成通过。

**17. 给后续实施会话的执行说明**

以本文件为确定的产品与技术约束，从阶段 0 开始逐步实现。开始时核对工作区最新代码和未提交差异；发现本方案与实际代码的新冲突时，以保护普通玩法、本地存档、单向依赖和最小改动为原则报告差异并调整具体调用点。不要重新开放已经确定的产品范围，也不要扩展为微信真实好友关系、权威竞技或通用框架。

允许必要的模式路由、生命周期参数、小型纯数据类型及测试；禁止改 AI 学习算法本身，禁止绕过现有存档事务、复制第二份玩法引擎、复制 identity DTO、将云身份写回本地成长档案。保持 SingleContract 为默认，保留普通玩法行为及已有发布平台。所有待外部环境验证的事项必须明确标注。
