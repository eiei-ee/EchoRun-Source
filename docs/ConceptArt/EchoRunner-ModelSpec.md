# Echo Runner v2 建模与接入规格

## 1. 定稿基准

- 唯一外观基准：`EchoRunner-Concept-v2.png`
- v1 仅作为历史探索，不参与最终建模判断。
- 定位：年轻、敏捷的近未来都市跑者；约 75% 运动服、25% 轻量科技装备。
- 禁止回退为机甲、机器人、圆头直筒或军事士兵造型。

## 2. 游戏内尺寸

- Unity 单位：1 unit = 1 m。
- 建议角色从鞋底到头盔顶高约 2.05 m。
- FBX 原点位于双脚落地中心，Y=0；正面朝 Unity +Z。
- 模型导入后放在玩家根节点 `player/CharacterModel` 下。
- 目标局部变换：Position `(0, -1.0, 0)`、Rotation `(0, 0, 0)`、Scale `(1, 1, 1)`。
- 现有玩家碰撞胶囊保持独立：半径 0.4、高度 2.2、中心 `(0, 1, 0)`。
- 模型不得携带额外 Collider、Rigidbody 或位移逻辑。

## 3. 轮廓与造型

- 标准双足 Humanoid，身材偏修长、自然肩宽、腰胯清晰。
- 头部为轻量运动头盔，露出下半张人脸；不使用全封闭机器人面罩。
- 主体为深海军蓝/石墨色功能运动服，胸前仅保留小型青色状态灯。
- 背包体积接近越野跑补水包，不得超过躯干宽度，不使用大型硬质尾翼。
- 护膝、护肘与手套为柔性运动护具，不做厚重装甲。
- 跑鞋轮廓必须清楚；鞋底略厚，方便高速运动时保持可读性。
- 橙色只用于拉环、扣件或安全标签，总面积不超过可见表面的 3%。

## 4. 移动端资源预算

- 目标三角面：6,000–10,000；硬上限 12,000。
- 一个主 SkinnedMeshRenderer；背包可合入主网格，最多允许一个附加蒙皮网格。
- 材质槽：目标 1 个，最多 2 个。
- 纹理：一张 1024×1024 主图集；必要时附一张同尺寸法线图。
- 发光信息尽量放入主图 Alpha 或遮罩通道，避免额外材质。
- 不使用透明布料、头发卡片、披风、布料模拟或实时反射。
- 不制作摄像机距离下无法辨认的微型机械结构。

## 5. 材质与颜色

- 主色：深海军蓝、低饱和石墨灰。
- 功能色：青色，用于护目镜、胸灯、细线和背包状态条。
- 点缀色：少量橙色安全标识。
- 皮肤与头发保持自然、低对比，不抢夺护目镜焦点。
- 优先使用适合移动端的简单 Lit 或定制轻量 Shader。
- Metallic 接近 0；Smoothness 保持低到中等，避免微信端高光闪烁。

## 6. 骨架要求

- 使用标准双足 Humanoid 层级，并确保 Unity Avatar 配置全绿。
- 必须包含：Hips、Spine、Chest、Neck、Head、Upper/Lower Arm、Hand、Upper/Lower Leg、Foot、Toes。
- 可选手指骨；若保留，建议每只手不超过 5 根简化手指链。
- 背包和头盔默认跟随 Chest/Head，不增加不必要的动态骨。
- T-Pose 或轻微 A-Pose 均可；最终导出必须保存为统一参考姿势。
- 蒙皮权重每顶点建议不超过 4 根骨骼。

## 7. 动画清单

首发必须具备：

1. Idle 循环
2. Run 循环（原地 In Place）
3. Jump Start
4. Jump Loop
5. Land
6. Slide Enter
7. Slide Loop
8. Slide Exit
9. Turn/Lean Left
10. Turn/Lean Right
11. Hit/Stumble
12. Victory/Result Idle

所有移动动画默认关闭 Root Motion，由现有 `PlayerController` 控制真实位移。

## 8. Unity 接入边界

- 保留 `player` 根节点、`PlayerController`、CapsuleCollider、碰撞检测和赛道转向逻辑。
- 新模型只替换 `CharacterModel` 视觉子节点。
- 正式动画模式由 Animator Controller 驱动，不允许 `CharacterAnimator` 同时覆盖同一组骨骼。
- Animator 参数建议：`Speed`、`Grounded`、`Jumping`、`Sliding`、`Turn`、`Hit`。
- 角色选择功能优先换材质/颜色；首发不同时加载多套完整骨架。

## 9. 验收标准

- Unity Humanoid Avatar 配置无缺失或强制骨骼错误。
- 原地跑步时脚底不漂移，左右脚没有穿插。
- 跳跃、滑铲时模型保持在现有碰撞胶囊范围内。
- 转弯时身体倾斜但角色根节点朝向仍由游戏逻辑控制。
- 普通跑动镜头下，头盔、青色状态灯、背包和跑鞋轮廓清晰。
- 微信开发者工具与真机均无材质丢失、异常高光或蒙皮抖动。
- 模型、贴图和动画来源许可允许商业小游戏发布，并保留授权记录。

## 10. 推荐工具链

1. Blender 完成建模、拓扑、UV 与材质烘焙。
2. AccuRIG 完成自动绑骨，或由美术直接制作标准 Humanoid 骨架。
3. Unity/Tuanjie 导入为 Humanoid，关闭 Root Motion，生成 Avatar。
4. 接入 EchoRun Animator Controller 并完成编辑器、WebGL、微信模拟器及真机验证。
