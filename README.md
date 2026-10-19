# EchoRun / 影迹

团结引擎与 C# 实验性跑酷项目：玩家留下自己的路线与动作习惯，再与冻结的旧回声对决。本地学习和决策不依赖托管大模型服务。

## 源码阅读入口

- [AI 个人影子](Assets/Scripts/AIShadowRunner.cs)：玩家行为学习、影子运行与回声对局。
- [赛道导演](Assets/Scripts/AITrackDirector.cs)：根据对局状态调整后续赛道节奏。
- [决策与安全过滤](Assets/Scripts/ShadowDecisionMaker.cs)：偏好加权、动作可行性与决策记录。
- [运行时脚本](Assets/Scripts)、[编辑器工具](Assets/Editor)、[测试](Assets/Tests)。
- [系统架构](docs/ARCHITECTURE.md)与[AI 系统说明](docs/AI_SYSTEM_ARCHITECTURE.md)。

## 公开范围

这是从已提交开发版本导出的独立源码快照。Git 历史从本次快照开始，不迁移原仓库的提交、PR、CI 日志或 Release 历史，也不包含本地未提交修改。

项目源码采用 [MIT License](LICENSE)。第三方字体、音频、SDK 及其他随附材料保留各自条款，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

为避免公开再分发许可尚未明确的可复用素材，本快照排除了 Mixamo 角色及相关衍生网格、部分 Unity Standard Assets 动作与衍生片段，以及未记录再分发许可的动作文件。原始文件清单和处理范围见 [公开快照说明](PUBLIC_SOURCE_SNAPSHOT.md)。

## 开发与资源依赖

源项目使用 Tuanjie 1.9.0 / 2022.3.62t8。保留的场景、预制体和控制器可能引用被排除的角色或动作，因此此快照不是资源齐全、克隆后可直接运行的发行包。

请先阅读 [公开源码资源依赖](docs/PublicSourceDependencies.md)、[构建说明](docs/BUILDING.md)与[人物动作准备步骤](docs/RunnerMotionDependencies.md)。开发者需要自行取得适用许可的资源，或替换为自己有权使用的模型和动作，并重新检查引用。

本仓库的 GitHub Actions 初始禁用，没有关联自托管构建机。工作流文件保留为配置参考；随附的旧测试、构建、试玩与发布记录属于原开发项目，不代表本仓库已经完成新的引擎构建、设备或真人体验验证。

## 参与和许可

- [贡献说明](CONTRIBUTING.md)
- [安全政策](SECURITY.md)
- [MIT 源码许可证](LICENSE)
- [第三方材料说明](THIRD_PARTY_NOTICES.md)
