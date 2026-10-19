# 仓库与本机资料索引

本次整理以 2026-09-28 暂定人物版本 `f73d1d5` 为基线。运行时脚本、场景、人物网格、材质、当前控制器和私有动作保留；整理不表示该版本已经达到上线要求。

## 从哪里开始

| 内容 | 当前入口 |
| --- | --- |
| 当前开发分支、CI 与干净检出的准备 | [RepositoryWorkflow.md](RepositoryWorkflow.md) |
| 当前人物动作的来源、许可边界、生成与绑定 | [RunnerMotionDependencies.md](RunnerMotionDependencies.md) |
| 尚未解决的确定缺陷 | [KnownIssues.md](KnownIssues.md) |
| 游戏逻辑 | `Assets/Scripts/` |
| 正式场景与路段 | `Assets/Scenes/SampleScene.scene`、`Assets/Prefabs/` |
| 当前人物服装 | `Assets/Art/RunnerAthleteCandidate/`，名称保留以保持已有引用稳定 |
| 当前动画控制器 | `Assets/Animations/HumanMotion/EchoRunHuman.controller` |
| 编辑器安装、生成、审查工具 | `Assets/Editor/` |
| 自动验证 | `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`、`Tools/CI/` |
| 美术源工程与创作参考 | `ArtSource/`、`Tools/Blender/`、`Tools/Art/`；不等于运行时已采用资源 |
| 历史公开发布说明 | `docs/releases/`；不要用历史发布包证明当前工作区已验证 |

## 本机产物与归档

以下目录不随公开 Git 仓库分发：

- `Builds/WeixinMiniGameV0-Clean/`：本机现有微信预览输出。整理期间保留。
- `TestResults/RunnerJumpRevision-20260926/`、`RunnerJumpRevision-20260927/`、`RunnerJumpRevision-20260928/`、`RunnerMotion-20260926/`：近期动作审查、测试与交付证据，仍在原位。
- `TestResults/RepositoryOrganization-20260928/`、`TestResults/RepositoryCleanup-20260928/`：本轮整理和后续删除的清单、校验值及测试结果。
- `LocalArchive/2026-09-28/`：保留历史 `Deck.pptx`/`Demo.zip`、任务笔记、来源凭据及少量顶层审查说明。最初归档的废弃候选、重复工具、旧构建、日志和中间截图已按用户后续要求删除；具体存留以清理清单为准。
- `Library/`、`UserSettings/` 等由引擎管理；此次没有清空以制造“干净”状态。

头像、品牌、概念图及可复用工具保留在原有目录；未将尚未审查的个人资料强行加入版本控制。原有 `.codex` 技能修改与 `.workbuddy-ai` 记录也保留。

### 查找存留资料与删除记录

`LocalArchive/2026-09-28/manifest.json` 记录首次归档时的原路径、新路径、大小和 SHA-256；其中 `status: verified` 仅表示首次移动校验成功，**不表示文件在后续清理后仍存在**。`retained-files.json` 列出存留文件，`TestResults/RepositoryCleanup-20260928/deleted-files.json` 记录实际删除及大小。首次归档仅移动；后续清理才释放被删除文件占用的空间。

只有存留文件仍可从本机归档恢复，目标已存在时先比较差异，不能直接覆盖。已删除的跟踪源码可查 Git 历史；已删除的未跟踪候选及旧生成物不承诺可恢复。任何 Unity 资源恢复都必须连同对应 `.meta`，历史编辑器脚本恢复后会重新参与编译。

当前动作的许可源、衍生动作和本机云配置继续由原有精确规则忽略，不能通过移动到公开目录或强制添加绕过这些排除。

## 防止再次混杂

1. 新测试日志、截图和复现笔记放进有任务名和日期的 `TestResults/<Task>-<Date>/`；不要继续向仓库根目录写日志。
2. 新美术候选先放在明确的候选目录。接受时核对正式绑定、来源和许可，再显式加入版本控制；废弃候选及其 meta 一起退出 `Assets`。
3. 当前动作生成步骤以依赖文档为准。五个无调用方的旧安装器已删除；仍提供共享逻辑的 `InstallEchoRunnerPhaseOne` 以及 `RunnerSilhouetteRefinement.Restore` 保留三个受保护的历史入口。它们在 `Legacy` 菜单中点击会说明并停止；确需复原历史版本时，在独立检出中使用对应入口的精确批处理参数。
4. 提交前检查逐文件差异与暂存范围。未跟踪的素材、用户资料、当前修改不能因“整理”而批量提交或删除。
5. 大型运行时类的职责拆分另行按功能、回归证据推进。本轮不将文件搬家或大范围重命名混入已暂定的玩法版本。

## 2026-09-28 整理验证

- 归档 13,859 个文件、492 个移动项目，移动后全部 SHA-256 匹配；根目录文件由整理前 455 个减少到 26 个，18 个旧 TempCodex 目录退出根目录。
- 229 个受保护的运行脚本、场景、人物网格/材质、控制器和私有动作文件，整理及测试后字节未变。
- 当前项目编译成功；完整 EditMode **917/918 通过、1 失败、0 跳过**。失败为右转筒仓净空不足，见已知问题。三个旧人物测试已修正，新增五项旧入口保护测试通过。
- CI 恢复脚本的九项合成数据检查通过。未启动远端 CI，也未在新机器上完成私有素材重建；不能将脚本检查表述为三平台 CI 通过。
- 用户随后授权删除弃用文件并提交推送；交付仅纳入本轮明确清单。原有未提交技能修改、个人资料及独有美术源资料保留。

本机完整结果见 `TestResults/RepositoryOrganization-20260928/verification.json` 和 `editmode.xml`。

## 后续删除与交付

用户明确要求实际删除无用或弃用文件后：

- 删除五个无调用方的旧人物安装器及其 meta（共十个跟踪文件）；相关历史说明和保护测试示例同步更新。保留仍被调用的共享工具、原始网格、材质、Avatar 和制作配方。
- 实际删除 13,860 个过时本机文件，合计 9,945,234,115 字节（约 9.26 GiB）：主要是旧构建、诊断、废弃候选和重复文件。删除前重新核对路径与 SHA-256。
- 保留历史演示包、演示文稿、任务笔记、独有美术源资料及当前许可动画，229 个受保护文件校验未变。
- 删除后重新运行完整 EditMode，仍为 **917/918 通过、1 失败、0 跳过**；没有新增失败。既有右转筒仓净空问题继续记录，不因清理而降低断言。

删除清单及本次测试位于 `TestResults/RepositoryCleanup-20260928/`。Git 提交只包含明确的整理、工具删除、CI、测试和说明改动；本机删除记录和私有素材不进入公开仓库。
