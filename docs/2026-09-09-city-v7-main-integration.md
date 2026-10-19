# 第七版城市接入主项目（保留现有人物）

## 范围

来源为独立 CityAfterimage FullPlayable 的 V7_RoadTurnFix 修正版。本次仅迁入 CityAfterimageV7 建筑模型/材质、Resources/CityV7 九段城市预制体和天空、CityV7PlayableEnvironment 呈现代码及路面/转弯测试。

WorldStyler 接入城市呈现：直道恢复 GroundPlane 与车道线后隐藏旧环境；随既有赛道池加载城市段；排除侵入道路与弯道相机空间的建筑；使用 V7 天空与光照。弯道保留主项目布局。

未迁入独立版本的人物模型、角色材质、动画、控制器或主场景。未修改玩家控制、相机、碰撞尺寸、AI、门数、时长、结算或存档规则。此前的核心体验改进继续保留。

## 构建与验证

构建入口：CityV7DeliveryBuild.Build。输出 TestResults/CityV7Main/Windows/EchoRun.exe，独立产品名 EchoRun-CityV7-MainCharacter，不覆盖旧包或正式存档。构建后恢复主项目产品名。

继续原存档使用 CityV7DeliveryBuild.BuildWithExistingSave，输出 TestResults/CityV7Main/Windows-ExistingSave/EchoRun.exe。该入口保持原 Eiei-ee / EchoRun 存档身份，不迁移或重置数据；启动后正常游戏会继续更新原存档。首次交付前将原存档注册表导出备份到 TestResults/CityV7Main，备份不纳入 Git。

新增测试覆盖直道路面、同变体复用，以及九种序列位置的左右转弯相机净空，共 20 项。更新三项原环境契约测试为 V7 城市/天空与重开局检查，而非继续要求旧堡垒环境可见。

测试、构建和新启动证据保存在 TestResults/CityV7Main；中间失败结果保留。通过测试与启动不等于两局自然通关、陌生玩家理解或全地图人工验收。

本轮实际结果：EditMode-final.xml 为 680/680；PlayMode-final2.xml 为 45/45。首次 Windows 构建在旧缓存编译失败后自动刷新成功；随后 Build-final.log 再构建成功，无 C# 编译错误或异常匹配。此前失败的三项旧环境测试改为验证 V7 环境契约后重新通过。

新包通过显式核查参数自动开始，输出 15 米与 35 米两张 1920×1080 帧；检查 35 米画面确认 V7 建筑、道路、原人物、回声与正式 HUD 同时显示。Player-QA.log 无异常或 error 匹配。这是固定身份的短程核查，不是陌生玩家测试；交付启动不携带固定身份、自动开始或截图冻结参数。

工作区原有脚步音效/粒子、动画试验、导入设置、工具和素材保留，但不纳入本次城市提交。当前工作区构建可能包含这些既有本地变更；不宣称等同于干净检出的构建。
