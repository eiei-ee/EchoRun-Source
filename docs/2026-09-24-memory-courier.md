# 层叠记忆角色升级

本轮目标是让人物模型接上现有场景的美术语言。采用“城市记忆信使”造型，替换服装网格，保留人物根对象、原骨架、动画、头手模型、碰撞尺寸、AI 和存档规则。

## 交付

新外套收窄肩袖，加入折领、卷袖、斜肩带、地图袋、裤袋和膝部色块；背部圆盘换成薄型记忆匣，并补上短檐帽、腕部标识、鞋带和后跟嵌片。服装继续使用六种既有材质，未增加纹理、动态布料或光源。

正式 Blender / FBX / Unity 网格路径与重建入口见 `ArtSource/Blender/LayeredMemory/MemoryCourier.md`。此轮没有重建头脸，原脸型仍是 Exo Gray；帽子和领口衔接属于新增服装。

场景绑定只更换现有 `OE_OrangeEchoClothing` 的 mesh 引用，并按导出顺序调整六个材质槽。历史 holder 名称保留以兼容换色与回声克隆。正式运行时不加载第二套骨架。

| 实测资源 | 原人物 | 新人物 |
| --- | ---: | ---: |
| 服装三角面 | 4,204 | 6,890 |
| 含原头手的总三角面 | 16,588 | 19,274 |
| 服装顶点 | 3,085 | 7,390 |
| 启用的 SkinnedMeshRenderer | 9 | 9 |
| 启用 renderer 的材质槽合计 | 14 | 14 |

面数增加约 16.2%；硬边与 UV 分割也增加了导入顶点。以上来自 `Before/binding.txt`、`After/binding.txt` 和 `install.txt`，不是帧率或手机性能结论。

## 验证与修正

证据根目录：`TestResults/LayeredMemoryRunner-20260924/`。

- `Baseline/` 保存本轮场景原件与受保护文件的 SHA-256。
- `scene-preservation.json` 比较场景的 201 个序列化对象，仅服装 SkinnedMeshRenderer 一个对象有差异，其余 200 个逐字相同，包括骨架、Avatar/controller 引用、玩家碰撞体与游戏组件。
- `protected-files-verification.json` 检查 341 个源文件和资产，仅开发诊断脚本的预期修改；其余 340 个与基线相同。
- 最初用剪辑名称找 Jump 失败，因为现有 Jump 状态实际使用 Falling 剪辑。捕获工具已改为读取实际 Animator 状态。
- 最初的编辑器动画采样显示了缓存 T-pose，不能视为动作验收。有效采样使用 `Animator.Play/Update` 和强制重新计算蒙皮矩阵；`After/motion-samples.txt` 记录骨骼旋转及实际变形范围。
- 初次安装误将临时导入模型一起保存。已恢复本轮原始场景、修正临时对象隐藏和销毁时机，再重新安装。最终场景差异仅为服装 renderer 内的网格及材质引用。新增测试检查场景只有原来的 Animator。
- 滑铲样本发现重复内层躯干穿出外套，已移除外套内不可见的重复表面，并调整肩带与衣面的间距。没有通过修改动作隐藏问题。
- 相关 EditMode **26/26 通过**，报告 `13-runner-editmode.log.xml`。新增三项按实际 Run / Jump / Slide 状态采样，并确认腿骨确实偏离 T-pose、服装变形有限且场景没有第二个 Animator；既有换色、碰撞尺寸与动作检查继续通过。首次新增测试在切换场景前创建临时 Mesh，导致它被回收，现已把创建时机移到切换后；没有放宽断言。
- `Before/RejectedClipSampling/` 保存最初无效的 T-pose 动作截图，不能用于对比动作改善。`After/` 的有效动作图由 `11-waist-capture.log` 对应流程生成。

实际玩家观察使用已有固定种子的场景诊断。新增 `-echo-runner-art-review` 仅在 Editor / Development Build 生效，复用原有跳跃和滑铲输入检查，并保留当前人物配色；原 `-echo-orange-art-review` 行为保持兼容。视觉诊断会关闭障碍碰撞，因此不能替代正常碰撞验收、人工手感验收或手机性能验证。

## Windows 实际运行

`14-windows-build.log` 记录成功构建，产品名为 `EchoRun-LayeredMemory-RunnerReview`，与正式存档分离。直接打开 `TestResults/LayeredMemoryRunner-20260924/Windows/EchoRun.exe` 是正常玩法；只有显式诊断参数才启用自动观察路线。

| 画幅 | 实际距离 | 转弯 | 实际动作截图 | 道路缺失 / 净空越界 |
| --- | ---: | ---: | --- | --- |
| 1280 × 720 | 500.05 米 | 2 | 两次 jumping=True、两次 sliding=True | 0 / 0 |
| 540 × 1080 | 500.04 米 | 2 | 两次 jumping=True、两次 sliding=True | 0 / 0 |

每条路线通过真实输入队列触发动作，不直接摆放人物或设置跳滑状态。两份 `Runtime-*/report.txt` 记录状态和截图名；运行日志未发现异常或 shader 错误。没有修改学习、结算或控制器实现。

已检查普通镜头 `distance-012.png`、竖屏滑铲截图，以及横屏 `distance-040-close-rear.png`、跳跃近景和滑铲近景。带 `close-*` 的图片临时使用检查镜头，保持同一运行帧的角色姿态与灯光，不能冒充正式游戏镜头。普通镜头下新帽子和记忆匣可辨认，回声仍正常显示。

`windows-build-hashes.json` 保存实际包内关键文件的 SHA-256；`validation-summary.json` 汇总测试、绑定保护和运行证据。完整模型的背部与侧面仍可继续由玩家评价，自动检查不能替代审美接受。

本轮不提交 Git、不部署微信或 WebGL。后续正式平台仍需构建和真机验证。
