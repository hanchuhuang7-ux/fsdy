# Agent Observation & Command · v1

协议标识 `frontline-agent@1`。Unity 0.6.0 的桥接入口为 `Runtime/HeadlessRunner.cs`，边界为 `Core/AgentProtocol.cs`。旧引擎的协议文档保留于 [LEGACY_AGENT_PROTOCOL.md](LEGACY_AGENT_PROTOCOL.md)。Node、Python 或大模型适配进程均通过 UTF-8 标准输入输出参与。

## 直接运行 Unity 版

先构建 Windows 版，再为红蓝双方分别保存启动配置，如 `red-bot.json`：

```json
{"command":"node","args":["scripts/bots/minimal.mjs"]}
```

```powershell
npm run agent-battle -- --red-bot red-bot.json --blue-bot blue-bot.json --timeout 250 --out Builds/Agents
```

不经 shell 拼接命令。Python 配置可使用 `{"command":"python","args":["scripts/bots/minimal.py"]}`。模型适配器在自身进程内访问模型；游戏不需要或保存模型密钥。程序和脚本路径相对工程根目录，命令行可执行文件也可使用绝对路径。

桥接强制开启军令、关闭内置 AI 统帅，按同一初始阵型和递增种子完成 Bo3。输出 `series.json`、`agent-stats.json` 与每局 `round-N.replay.json`。Unity 完整轨迹为 **v3**；旧 JS 桥仍输出 v2。WebGL 可导入这些公开轨迹，不能启动本机子进程。

## 时序与观察

每局从 tick 0 开始，在 **0、60、120……** 步、执行下一物理步之前，双方收到同一时刻的公开观察。观察已同时采样，双方响应完成或超时后，再按红、蓝固定顺序提交同一个整数 tick。等待期间物理不前进，不以墙上时间推进模拟。

每行一份 JSON，观察示意：

```json
{
  "schema": "frontline-agent@1",
  "kind": "observation",
  "requestId": "1:480:red",
  "team": "red",
  "round": 1,
  "tick": 480,
  "interval": 60,
  "map": "meadow",
  "units": [{"id":1,"team":"red","type":"shield","x":-4,"y":0.8,"z":0,"hp":200,"maxHp":220}],
  "props": [{"id":3,"team":"neutral","type":"barrel","x":0,"z":2,"hp":55,"alive":true}],
  "commander": {"enabled":true,"points":{"red":2,"blue":2},"readyAt":{"red":480,"blue":480}},
  "power": {"red":81.81818181818181,"blue":90}
}
```

`units` 只包含存活单位；双方位置、当前 HP、最大 HP、兵种和 ID 都是公开信息。`props` 包含地图与玩家道具的公开位置、HP 和生死。`power` 为双方实时战力。`readyAt` 是冷却结束的绝对 tick，剩余秒数为 `max(0, readyAt[team] - tick) / 60`。

不暴露随机数状态、未来轨迹、寻敌目标、刚体句柄、内存对象、布阵历史或隐藏热座编队。CLI 开战后的战场已公开；这个接口不能在热座封存阶段使用。

## 响应与军令规则

响应必须回显当前 `requestId` 和分配的 `team`：

```json
{"schema":"frontline-agent@1","kind":"command","requestId":"1:480:red","team":"red","order":{"type":"fortify","x":-4,"z":0}}
```

主动跳过：

```json
{"schema":"frontline-agent@1","kind":"command","requestId":"1:480:red","team":"red","order":null}
```

只接受 `schema/kind/requestId/team/order`。军令对象只接受 `type/x/z`，类型为 `inspire`、`focus` 或 `fortify`。坐标必须有限、处于地图地面。每方每局 3 点，每道 1 点，统一 480 tick 冷却，最终都由 `Battle.IssueOrder()` 检查。非法坐标、越权阵营、过期 ID、额外修改字段、冷却或额度不足均拒收，不扣点。

stdout 只写完整响应行；诊断写 stderr。每个 Bot 同时只有一个待处理请求。Unity 原生桥默认每次观察等待 250ms，可设 10–5000ms。无效 JSON 本次拒收；超时、退出或管道失败后停用该进程，后续军令跳过，比赛继续。迟到的旧 ID 不会满足新请求。JSON 边界最多 16 KiB，最多积压四行；stderr 被排空但不进入协议。`agent-stats.json` 记录观察、接受、拒绝和未响应次数，实际获准 tick 见回放 orderLog。

## 确定性、信任边界与回放

超时只决定是否接受该次响应，绝不改物理步长或额外补偿 Bot。异步模型响应不能承诺跨次请求完全相同；**实际接受的军令 tick 日志**才是重演输入。回放使用日志关闭重新决策，复现同一固定环境下的战果与轨迹，不再运行外部 Bot。

这是游戏数据权限边界，不是操作系统进程沙箱。只启动自己信任的本地程序；协议不会给 Bot 一个可直接修改引擎的引用。网页不会执行导入 JSON 内的代码。

自动化覆盖：脱敏观察、越权队伍、非法坐标、额外字段、冷却拒收、超时与旧响应隔离、两份真实 stdio 子进程完成 Bo3、Unity v3 包校验与按日志重演；旧引擎 v2 检查保留于冻结的 JS 测试。
