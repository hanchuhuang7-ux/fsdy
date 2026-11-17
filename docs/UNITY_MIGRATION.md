# Unity 版迁移说明

Unity 工程位于 `Unity/`，固定编辑器 **6000.6.9f1**，游戏版本 **0.9.0**。目标为 Windows x64 与 WebGL。正式游戏的规则、物理、渲染、音效、输入和界面均在 Unity C# 内执行。

## 环境与构建

在 Unity Hub 安装该编辑器及 Web Build Support，激活本机许可证。Node 22.12+ 用于旧版本转换、回归测试和构建命令；独立 C# 规则检查另需 .NET 8 SDK。

```powershell
npm ci
# 路径不同才需要设置
$env:UNITY_EDITOR_PATH='C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe'
npm test
npm run build
npm run dev
```

Windows 成品为 `Builds/Windows/FrontlineLab.exe`，网页成品为 `dist/`。分开构建可用 `npm run build:windows` 和 `npm run build:web`。也可在 Unity 菜单 Frontline 中准备工程、构建。场景极小；启动时按职责创建运行时组件。构建工具显式保留运行时 UI 着色器、GPU 实例变体，并将基础几何保存为 Resources 资源。

## 代码边界

| 目录 / 模块 | 责任 |
| --- | --- |
| `Core/Json.cs`, `Models.cs`, `Rules.cs` | 严格 JSON 边界、数据、合法布阵、收藏、诊断与 Bo3 |
| `Core/Battle.cs`, `PhysicsContracts.cs` | 固定 60Hz 战斗、3 米空间桶、贡献、高光、物理接口 |
| `Core/Tactics.cs` | 三类规则 AI、算策多候选推演、双循环赛和统计 |
| `Core/Replay.cs`, `AgentProtocol.cs` | v3 回放与旧轨迹验证、公开 Agent 协议 |
| `Runtime/UnityPhysics.cs` | 原生 PhysX，独立本地物理场景，有限数量布娃娃 |
| `Runtime/BattleView.cs` | 原生实例合批、低多边形兵种 / 地图 / 道具、镜头 |
| `Runtime/FrontlineApp.cs` | 主状态协调与固定步更新 |
| `Runtime/NativeHud.cs`, `BattleInput.cs` | 原生 uGUI、键鼠、触控门控 |
| `Runtime/FileBridge.cs`, WebGL `.jslib` | Windows 文件框、浏览器上传和下载 |
| `Runtime/HeadlessRunner.cs` | 原生擂台、双 Bot、基准和真实物理检查 |
| `Tools/CoreChecks/` | 独立规则检查；参考物理只作测试替身，绝不进入成品 |

`Core` 不依赖 Unity。`Runtime` 依赖 Core，Editor 依赖两者，没有反向依赖。渲染不读取或消耗战斗随机数。物理世界按试跑创建和销毁，推演可分片、可取消。高光最多 24 条；界面快报最多 5 条；尸体最多 24 具、12 秒寿命。完整回放以 10Hz 保存，最终帧单独补齐。

## 兼容与确定性

- 0.1–0.6 布阵保持坐标、兵种、姿态和预算语义，导入后规范到存档格式 0.6（产品版本独立为0.7）。
- 0.1–0.5 完整轨迹回放可由 Unity 直接播放，不会重算旧物理。
- 旧种子包需用保留的旧版 Rapier 引擎展开：

```powershell
node Tools/convert-legacy-replay.mjs old-seed.json full-trajectory.json
```

- Unity 新回放为 v3，`engineVersion: unity-0.9.0`。可导出完整轨迹或原生种子包。
- 更换为 PhysX 后，战斗结果不会与 Rapier 逐帧相等。不能承诺不同平台、编辑器版本或 CPU 的 PhysX 种子包位级一致；跨平台分享使用完整轨迹。检查同一构建重复运行的完整战报，避免把此检查误述成跨平台确定性保证。
- 根目录 `src/`、`test/` 与旧脚本是冻结的旧版转换与回归工具，不会打入 Unity WebGL 游戏，也不会在 Unity 内嵌网页。

## 原生无头运行

```powershell
npm run test:physics
node Tools/unity.mjs smoke
npm run benchmark
node Tools/unity.mjs tournament --maps meadow,bridge --seeds 42,77
node Tools/unity.mjs tournament --config Tools/fixtures/oracle-config.json --entrants Tools/fixtures/oracle-entrants.json --seeds 42
```

擂台接受 2–8 方、最多 6 张地图与 10 个种子，总计不超过 600 局。每对参赛方交换红蓝两局。平局积分 0.5；胜率单列胜局比例；排行按积分率、平均残余战力与固定参赛顺序排序。MVP 统计各兵种贡献最高者在各局出现次数，平手按固定 ID 排序。

## 外部 Bot

协议继续使用 `frontline-agent@1`。用独立 JSON 配置执行文件和参数，不经 shell 拼接。示例：

```json
{"command":"node","args":["scripts/bots/minimal.mjs"]}
```

将其保存为 red-bot.json 与 blue-bot.json 后运行：

```powershell
node Tools/unity.mjs agent --red-bot red-bot.json --blue-bot blue-bot.json --timeout 250 --out Builds/Agents
```

也可把 `command` 换为 Python，将参数指向 `scripts/bots/minimal.py`。所有相对路径以工程根目录为基准。每 60 步向两个进程先发同一时点的公开观察，再将通过校验的回应按固定红蓝顺序送入引擎。每条 stdout JSON 最多 16KB，最多积压 4 条；stderr 不进入协议。超时或退出后跳过该进程后续指挥，比赛继续；Bot 的数据不得改变部队属性、控制另一队或绕过冷却。输出 Bo3 战报、桥接统计与每局 v3 轨迹。

Windows 可以启动外部进程；WebGL 浏览器版提供导入这些对战回放的能力。网页中的算策和擂台在主线程按固定模拟步分片执行，进度与停止按钮可用。

## 验收命令

`npm test` 运行冻结的 77 项 JS 回归和新增 C# 核心检查。`npm run test:physics` 在 Unity Play Mode 中跑真实物理重复对局；`smoke` 遍历六图并验证回放；`benchmark` 运行 720 步和每 6 步一次快照，热身一轮、测量五轮。换引擎后的性能数据应单独报告 CPU 模拟耗时与实际画面帧率，不把旧物理和新物理的结果等同。

Noto Sans CJK 字体遵守随附 OFL；其余游戏源码沿用 MIT。不要提交 Library、Temp、Builds、缓存、许可证或本机 Bot 绝对路径配置。

随包字体是 1.93MB 的子集，覆盖常用 GB2312 字、拉丁、标点、符号和所有随包 C#/JSON 文字。罕用字名称可能显示缺字，原始 JSON 字符仍被保留。可选维护工具位于 `Tools/FontSubset/`，其 manifest 记录原字体与子集哈希；普通构建无需安装该维护工具或重新下载字体。

v0.7 实测、不同测法及限制见 [Unity 0.7 验证记录](V0.7_VALIDATION.md)。0.6历史性能基准仍保留于旧报告。

## v0.7 视觉与性能管线

`Editor/ModelBaker.Bake()`预生成128个带顶点色/平面法线的Mesh资产，存于`Models/Resources/Models`；构建会确定性重烘焙。Runtime仅加载资产并以共享材质实例化，保留双LOD与矩阵动作。`MapEcology`使用独立装饰种子，不创建GameObject/Collider，不修改地图高度或寻路。策略菜单的流畅/均衡/精细控制草木、粒子和阴影距离。

新物理适配器预热投射物和24套六刚体布娃娃，历史帧与显示帧各有独立快照存储。战斗开始前预分配最多902帧，会增加开始时的内存和初始化工作；性能基准不包含这部分准备时间。

0.6 Unity完整轨迹继续直接校验；0.6种子通过`LegacyUnityPhysics.cs`冻结适配器重建，保留旧浮点距离求值。0.7优化使用新的engineVersion；不要把新结果标成0.6。跨平台继续交换完整轨迹，不保证PhysX位级一致。

验收专用：`node Tools/unity.mjs profile`生成独立Development构建；`node Tools/serve-qa.mjs`配合`?frontlineQA=1`运行本地截图/遥测。`QA_PROFILE=1`选Development网页。遥测上传只允许loopback。GC计数器不可用时写-1，不把不可用值当成零分配。

## v0.8 受击、移动与碰撞

`Core/CombatMotion.cs` 只含引擎无关规则；`Battle.cs` 处理速度约束转向、前摇、分级硬直、受击瞬态与通道避让。`BattleView.cs` 使用六个可省略的轻量浮点通道驱动矩阵步态、来向后仰与实例闪白；命中特效和音效提示均使用有界池。

`UnityPhysics.cs` 对战马使用随 Y 轴旋转的双胶囊，对投石车使用底座盒体；地图岩石使用与视觉同朝向、最多64三角面的低模凸包。高速度启用 ContinuousSpeculative，普通速度回到 Discrete。骑兵/车轮滚动接触与高台冰面有独立轻量物理材质；60Hz、地形高度与地图结构不变。

发射从肩部或抬高的投石车抛臂前方开始。仅对发射点1.15米内的友方/中立旗杆暂免0.10秒碰撞；普通友军盾卫和远处旗杆仍真实阻挡。对象池启用后重新写入发射姿态和速度，避免禁用刚体写入丢失。

`Core/HistoricalBattle07.cs` 冻结0.6/0.7规则；`LegacyUnityPhysics.cs` 对应0.6，`Unity07Physics.cs` 对应0.7。旧种子展开与再次导出保留原 engineVersion；新增规则仅使用 unity-0.8.0。原0.6/0.7完整轨迹SHA在真实PhysX中逐字一致。跨平台仍交换完整轨迹。

完整验收、所有性能测法、动态原图与限制见 [v0.8 验证记录](V0.8_VALIDATION.md)。`npm test` 为87项JS与84项C#；`checks` 额外执行实际碰撞、接触冲锋、三图8人纵队和历史完整轨迹黄金校验。`node Tools/unity.mjs benchmark --timings 1` 可额外查看物理/快照耗时分解与GC回收次数；默认验收不启用计时探针。


## v0.9 精修与历史冻结

0.8 的 Battle、CombatMotion 与完整物理适配器分别冻结到 HistoricalBattle08、HistoricalMotion08、Unity08Physics；当前规则使用 unity-0.9.0。旧种子身份与完整 JSON 的 SHA 均由真实 PhysX 检查。未改变60Hz、3米网格、碰撞轮廓和求解迭代数。

格挡/重击使用粗亮条、盾牌回弹，长枪/大锤/冲锋有实例化闪影。强击退后60步内碰撞实际墙/岩/拒马可生成一次尘雾与震荡提示，地面与普通行走接触不触发。骑兵地面/友军接触在回调入口过滤；MoveRotation 接受规则角速度限制。窄口连续受阻后允许有限反向与切线试探。HUD 使用真实残余战力比例，军令关闭时底板紧凑，状态纳入底板。

完整验收与原始测量见 [v0.9 验证记录](V0.9_VALIDATION.md)。
