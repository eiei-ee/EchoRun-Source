# echo 云函数

微信异步影子挑战的独立函数包。只发布冻结 identity、取得精确影子、幂等报告结果和读取该影子的挑战榜。没有付费、奖励、微信好友关系查询或服务器权威计分。

## 本地验证

固定依赖 `wx-server-sdk 3.0.1`，完整传递依赖由 `package-lock.json` 锁定。部署目标为 **Nodejs20.19**；本地可以用 Node 20.19–24（本次开发环境为 Node 24.15.0）。进入本目录执行：

```powershell
npm.cmd ci --ignore-scripts --no-audit --no-fund
npm.cmd test
```

无 SDK、无网络时也能运行纯业务测试：

```powershell
node --test test/handler.test.js
```

测试不访问真实云。fake repository 带读版本校验、私有写集、提交屏障、冲突和故障注入；覆盖并发发布 seed、重复报告、原子回滚、最佳一局竞争和榜单投影。`repository.test.js` 还使用已安装 SDK 的真实包装层，替换底层数据库 I/O，核对 `get/set/transaction/orderBy` 参数和返回形状。未安装 SDK 时仅该包装层测试跳过，不代表部署已验证。

`test/fixtures/identity-v1.json` 由 Unity 编辑器 `AsyncEchoFixtureExport.Export` 使用真实 `ActiveEchoIdentity.ToJson()` 导出。纯业务测试必须读取该真实载荷；文件缺失会直接失败。修改 identity 序列化后应重新导出并运行测试，以验证 C# 与云端字段契约一致。

## 部署

1. 在有权限的微信小游戏 AppID 下开通云开发环境，使用单独测试环境先验收。先在控制台创建或核实 `echo`：入口 `index.main`，运行时 **Nodejs20.19**，函数超时 **5 秒**。部署前确认目标控制台确实提供此运行时；不静默降级到不支持 Node 内置测试/语法的版本。
2. 创建下文三个集合、权限和索引。服务端 SDK 用当前环境 `cloud.DYNAMIC_CURRENT_ENV`；本包不嵌入 AppID、环境 ID 或任何密钥。客户端的环境配置必须指向同一环境。
3. 在本目录执行 `npm.cmd ci --omit=dev --ignore-scripts --no-audit --no-fund`，保留本地锁定依赖。推荐先用下方打包工具生成规范 ZIP，再通过已核实支持 ZIP 全量替换的部署入口上传；本机尚未实测控制台具体 ZIP 上传入口，不能假定某个菜单存在。当前 Windows 官方 CLI 的可用替代路径见下方增量部署说明。
4. 包根必须直接包含 `index.js`、`handler.js`、`protocol.js`、`repository.js`、`package.json`、`package-lock.json` 及安装后的 `node_modules/`，所有嵌套条目使用 `/`。不要打包其他源码、个人文件或开发日志，不使用 `latest` 依赖；若采用云端安装，必须另行确认实际构建遵循 lockfile 的 `npm ci`，不能由 `remote-npm-install=true` 推断。
5. 部署后先验证单身份 SDK 的发布、原载荷获取、重复报告和最小字段榜单，再用两个获准访问测试版本的微信身份执行真实 Unity 发布→分享→冷/热启动→挑战→report→榜单。确认相同挑战只有一条结果、旧卡片仍定位旧快照。上传成功、SDK 合成成绩及本地测试均不等于双账号玩家验收。

**当前本机工具限制（2026-09-21 核对）**：此版本 `wechatide cloud_fn_deploy` 在函数不存在时默认创建为 **Nodejs16.13 / 3 秒**；重新上传已有函数会保留运行时和超时。该入口不读取 `cloudbaserc.json` 的 runtime/timeout，也不会用 `package.json.engines` 设置云运行时；函数 `config.json` 在此部署路径仅处理 `permissions.openapi`。因此必须先在控制台配置上述目标值，不能靠新增配置字段改变默认创建行为。其他版本应重新核对本机帮助及部署后配置。

**当前 Windows 打包路径限制**：本机这一版本的官方 `cloud_fn_deploy` 全量打包已复现把嵌套 ZIP 条目的反斜杠原样保留。真实全量部署后初始化失败；通过官方增量入口 `--file node_modules` 补传 2,077 个规范路径的依赖文件后，同一个 `apiVersion:0` 请求由 `TRANSIENT_UNAVAILABLE` 恢复为预期的 `API_VERSION_UNSUPPORTED`。未取得云端原始 `MODULE_NOT_FOUND` 日志或解包清单，不把未观察到的异常写成事实，也不推断所有开发者工具版本都受影响。因此当前 Windows 环境不能以 `cloud_fn_deploy --remote-npm-install=false` 返回成功作为依赖正确部署的依据。

从仓库根目录生成新的规范 ZIP（输出父目录须已存在，文件名不得重复）：

```powershell
python Tools/CloudFunctions/package_echo.py --output Builds/echo-NEW.zip
```

工具只收集上述生产根文件和已安装的 `node_modules`，将条目规范化为 `/`，校验必要 SDK 文件、版本及 ZIP CRC，生成 `echo-NEW.manifest.json`（SHA-256、字节数、条目数和 lockfile 指纹），拒绝覆盖已有 ZIP 或 manifest。它只打包，不部署；源码更新后须生成新包，不能拿早期归档覆盖新补丁。规范 ZIP 全量替换可避免依赖历史增量内容，但必须先核实实际部署入口及其替换语义。

如使用本机已核对的官方 CLI，先在控制台创建空 `echo` 并设为 Nodejs20.19 / 5 秒，再用 `cloud_fn_inc_deploy --file .` 上传整个函数目录。该目录路径经过当前工具的条目规范化；`--file` 必须是相对于函数目录的路径。无需依赖 Unity 导出项目的 `cloudfunctionRoot`。若改用项目管理云函数，应在持久部署项目/生成流程中配置 `cloudfunctionRoot`，不能只改一次会被 Unity 导出覆盖的文件。

先完成集合、索引和函数配置，并核对目标 AppID/环境 ID。使用已登录、已授权的工具执行（变量填本人测试环境，不填密钥）：

```powershell
& 'D:\微信开发\微信web开发者工具\wechatide.cmd' -c codex cloud_fn_info `
  --env $echoEnvironmentId --appid $echoAppId --names echo

& 'D:\微信开发\微信web开发者工具\wechatide.cmd' -c codex cloud_fn_inc_deploy `
  --env $echoEnvironmentId `
  --path 'C:\Users\zzz\Desktop\TempleRun\Tools\CloudFunctions\echo' `
  --appid $echoAppId `
  --file .
```

本机 `cloud_fn_inc_deploy --help` 已确认 `--appid/--env/--path/--file` 参数；仅修复依赖时可将最后一项改为 `--file node_modules`，生产根文件仍须已正确部署。增量上传只追加/覆盖，**不删除旧条目**；当前环境已通过补传依赖恢复，但全量打包遗留的错误反斜杠条目可能仍存在。文件重命名、删除或清除历史条目需要另行核实全量替换流程，不能认为一次 `--file .` 会清空旧包。

部署仍须完成工具弹出的云写入确认；返回 pending/taskId 时等待用户在开发者工具确认并取得最终任务结果，不重复提交、不将待确认状态当作成功。之后重复 `cloud_fn_info`，核对 `status/runtime/timeout`，再实际调用验证。其他机器使用其实际安装路径，并重新检查该版本帮助和打包行为。

本次规范路径补传后，真实 SDK 已验证重复发布、原载荷获取、重复报告及榜单最小字段。第一次榜单脚本误报来自 Windows CLI 传递脚本时中文常量被转码，不能据此修改正确的服务端显示字段。命令中的脚本使用 ASCII 源码和 Unicode 转义（如 `"\u73a9\u5bb6-"`），复杂参数使用官方工具支持的 `--args-file`；比较实际返回字段与脚本收到的常量后再归因。上述单身份、合成成绩验证不代表真实 Unity 玩法、卡片送达或双账号验收通过。

运行时与调用依据：[CloudBase 运行环境](https://docs.cloudbase.net/cloud-function/runtime-support)、[微信云函数身份与部署示例](https://docs.cloudbase.net/recipes/add-cloud-function-wechat-miniprogram)、[数据库事务](https://docs.cloudbase.net/database/transaction)。已在安装的 SDK 3.0.1 上检查包装层签名；真实环境权限、索引和网络仍需上述集成验收。

## 集合与索引

所有集合对客户端直读、直写均关闭：自定义安全规则为 `{"read":false,"write":false}`，或控制台等价的仅管理端可访问配置。云函数管理端有读写权，所以每个 action 仍检查 `cloud.getWXContext().OPENID`，绝不使用请求里的 openid 作为本人身份。不要给这四个 action 配置无需微信身份的公开 HTTP 入口。

将 [deployment/server-only-rules.json](deployment/server-only-rules.json) 的内容分别设置为三个集合的安全规则。本文件是控制台配置输入，不会随函数上传自动生效。

| 集合 | 文档唯一键 | 作用 |
| --- | --- | --- |
| `echo_shadows` | SHA-256 编码的 `(ownerOpenid, identityId, rulesVersion)`，即 boardId | 原始 payloadJson 和首次发布生成的 runSeed；完全不可变 |
| `echo_results` | SHA-256 编码的 `(challengerOpenid, challengeId)` | 所有已受理结果及去重回执，包括主动放弃 |
| `echo_scores` | SHA-256 编码的 `(boardId, challengerOpenid)`，即 entryId | 该玩家在该榜的最佳完整一局 |

三个集合都含 `schemaVersion:1`。数据库默认 `_id` 唯一索引承载幂等，不需要先 `where` 再 `add`。键采用域标记和 UTF-8 长度前缀编码，避免元组歧义；此 SHA-256 只生成存储键，不复刻客户端的 StableHash/CreateIdentityId，不证明分数真实。

在 `echo_scores` 创建以下排序复合索引，并用测试环境实际查询验证：

```text
boardId ASC
distanceMeters DESC
playerLeadMeters DESC
achievedAt ASC
_id ASC
```

[deployment/echo-scores-index.json](deployment/echo-scores-index.json) 提供已核对的 `CreateIndexes` 参数，适用于 `wechatide cloud_db_write_struct --action updateCollection --collection-name echo_scores --update-options-file <文件绝对路径>`，另须明确传入 `-c <已授权客户端名>`、`--appid` 和 `--env`。索引名为 `echo_board_ranking_v1`，非唯一；创建前检查是否已存在，创建后用 `cloud_db_read_struct --action listIndexes` 核对字段顺序及就绪状态。

没有该索引/权限或数据库配置失败必须显示错误，不可伪装成空榜。若目标环境不支持此索引组合，需连同并列规则一起明确调整并重测，不能先截断 top-N 再在客户端重排。

MVP 无 TTL、无自动删除。结果和去重记录共同保留；快照和结果会持续增长，应在云控制台设置用量/费用告警、定期检查函数错误与集合规模。删除 results 或复用 boardId 会破坏去重，不属于普通清理操作。

## 协议

请求公共字段：`apiVersion:1` 和 `action`。响应只有以下两种形状：

```json
{"ok":true,"apiVersion":1,"data":{}}
```

```json
{"ok":false,"apiVersion":1,"error":{"code":"RULES_UNSUPPORTED","retryable":false}}
```

`apiVersion=1`、云文档 `schemaVersion=1`、identity `version=1`、style `version=3`、memoryContract `version=1`、挑战 `rulesVersion=1` 各自验证。未知版本明确拒绝；不会通过 Normalize 修正。identity 原字符串按 UTF-8 最多 **16,384 字节**，服务端不重新序列化。字段校验仅保证大小、结构、有限值、数组维度和可挑战路线记忆，不进行 identity 哈希验证或权威模拟。

| action | 请求字段 | 成功 data |
| --- | --- | --- |
| `publish` | payloadJson, rulesVersion | boardId, inviterOpenid, identityId, generation, rulesVersion, runSeed |
| `get` | inviterOpenid, identityId, rulesVersion | found；找到时另有 boardId, payloadJson, ownerOpenid, identityId, payloadVersion, generation, rulesVersion, runSeed |
| `report` | boardId, challengeId, rulesVersion, distanceMeters, playerLeadMeters, endReason, playerWon | receiptId, acceptedAt, duplicate, eligible |
| `leaderboard` | boardId, limit（默认20，整数限幅1–50） | boardId, rulesVersion, items |

`items` 每项仅有 `rank, entryId, displayLabel, isMe, distanceMeters, playerLeadMeters, playerWon`。匿名编号不代表微信昵称，不返回完整 openid、identity 载荷或历史结果。合法但无成绩的 board 返回空数组；不存在的 board 返回 `BOARD_NOT_FOUND`。

时间字段均为服务端 Unix 毫秒整数（C# `long`）。runSeed 是 `[1, 2147483647]` 内整数。challengeId 由每次实际开局生成一次，自动/手动确认同一结果必须复用原 ID 和原字段；新开一局才换 ID。字段校验不接受非法 ASCII 标识、超长字符串、非有限数或错误类型。

### 发布与分享

publish 只按上下文本人发布，identityId/generation/pace 从 payload 提取。相同键和精确相同原字符串返回已存 seed；相同键不同字符串返回 `SNAPSHOT_CONFLICT`。并发首次发布只保留事务胜出的 seed。

分享前必须取得成功回执；使用回执构造并编码 `inviter=<inviterOpenid>&shadow=<identityId>&rules=<rulesVersion>`。get 严格按三元组获取，不回退最新影子。发布新 identity 不影响旧卡片。

### 上报与排名

report 归属上下文 challenger；新玩家无需先发布自己的影子。`endReason` 为 `finish_reached/collision/abandoned`；胜利严格等于 `finish_reached && playerLeadMeters >= 0`。距离非负，距离及领先绝对值不得超过宽松传输上限 10,000,000 米；该上限不证明分数可信。

同一事务读取已有结果、board、当前最好成绩后，写入结果和必要的最好成绩。相同业务内容返回原回执，任何变化（包括 board/rules）返回 `CHALLENGE_CONFLICT`。放弃只记录结果，不入榜。最佳一局按 `distance DESC, lead DESC, acceptedAt ASC, challengeId ASC` 整体选择；跨玩家榜按 `distance DESC, lead DESC, achievedAt ASC, entryId ASC` 排序。

客户端每次尝试等待最多5秒、逻辑操作最多2次；服务端底层网络重试关闭、请求超时4000ms，事务在4000ms预算内最多重试一次明确冲突，函数部署硬超时5秒。不对模糊 commit 失败盲目重试，不承诺请求被中止就没有写入。两次响应均丢失时客户端状态为“未确认”，可以用原 challengeId 重新确认。

V1 内容不能原地改成另一套规则。版本升级通过显式修改支持/退役列表并部署，不迁移旧卡片。退役规则不接受新发布/成绩，已受理的相同报告仍可得到旧回执；历史榜仍可读。维护停用可以关闭客户端 featureEnabled，再禁用云入口；在途异步局仍按异步结束，不得回退成本地晋升局。

## 降级与剩余验收

初始化/权限/云未开通/超时等错误只影响可选云功能。发布失败禁用分享、get失败保留单机入口、report失败保留纯本局结果、榜单失败显示错误态；不修改本地成长存档、不阻塞游戏。客户端没有持久 outbox，强杀进程后未确认成绩可能丢失。

上线前仍需真实验证运行时、数据库索引、两微信身份、冷热启动和离线错误路径。不同玩家本地赛道导演基线仍可能不同，因此该无奖励挑战榜不承诺竞技公平。

云控制台需开启函数日志。客户端仍只收到稳定协议错误；服务端 `echo.diagnostic` 额外记录固定初始化/数据库操作阶段、受限格式的 SDK `code/errCode`、白名单 `errorName` 和有限 `reason` 分类（如 `missing_module/configuration/timeout`）。不记录原始消息、堆栈、请求事件、载荷、OPENID 或令牌；不认识的字段值记为 `unknown`。结合阶段和 SDK 错误码排查配置或运行时问题，不应把所有 `TRANSIENT_UNAVAILABLE` 直接当作网络超时。纯测试可注入诊断 sink，默认不输出；诊断失败不会改变客户端响应或增加重试次数。
