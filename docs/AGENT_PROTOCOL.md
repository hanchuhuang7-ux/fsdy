# Agent Observation & Command · v1

协议标识 `frontline-agent@1`，引擎 v0.5.0。桥接入口为 `scripts/agent-bridge.mjs`，共用数据边界在 `src/agent-protocol.js`。任意能读写 UTF-8 标准输入输出的 Node、Python 或模型适配进程均可参与。网页继续只接收数据文件，本地子进程由 CLI 启动。

## 直接运行

```sh
npm run agent-battle -- --out-dir agent-results
npm run agent-battle -- --config examples/ai-duel.json --out-dir agent-results
```

默认启动两份独立的 `scripts/bots/minimal.mjs`，进行 Bo3；军令强制开启，内置 AI 统帅关闭。每局使用初始阵型，种子递增。Bot 只负责实时军令；本协议不允许临时增兵、移动单位、改 HP、预算或伤害。输出 `series.json` 与每局 `round-N.replay.json`，后者为可在网页导入的完整 v2 回放。

指定程序时传 **JSON 参数数组**，不经 shell 拼接。PowerShell 示例：

```powershell
npm.cmd run agent-battle -- --red-bot '["node","scripts/bots/minimal.mjs"]' --blue-bot '["python","scripts/bots/minimal.py"]' --timeout-ms 1000 --out-dir agent-results
```

Python 示例只用标准库，需要自己已安装 Python 3。模型适配器可以在其进程内部请求模型，读取观察并输出同一响应格式；本项目不需要或保存模型密钥。

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

只接受 `schema/kind/requestId/team/order`。军令对象只接受 `type/x/z`，类型为 `inspire`、`focus` 或 `fortify`。坐标必须有限、处于地图地面。每方每局 3 点，每道 1 点，统一 480 tick 冷却，最终都由 `Battle.issueOrder()` 检查。非法坐标、越权阵营、过期 ID、额外修改字段、冷却或额度不足均拒收，不扣点。

stdout 只写完整响应行；诊断写 stderr。每个 Bot 同时只有一个待处理请求。默认每次观察等待 1000ms，可设 5–10000ms。超时、无效 JSON、退出或管道失败时本次跳过，比赛继续；迟到的旧 ID 不会满足新请求。缓冲上限 16 KiB，越限终止该 Bot；stderr 只保留最后 2048 字符。`series.json` 记录接受、拒绝、超时及所在 tick。

## 确定性、信任边界与回放

超时只决定是否接受该次响应，绝不改物理步长或额外补偿 Bot。异步模型响应不能承诺跨次请求完全相同；**实际接受的军令 tick 日志**才是重演输入。回放使用日志关闭重新决策，复现同一固定环境下的战果与轨迹，不再运行外部 Bot。

这是游戏数据权限边界，不是操作系统进程沙箱。只启动自己信任的本地程序；协议不会给 Bot 一个可直接修改引擎的引用。网页不会执行导入 JSON 内的代码。

自动化覆盖：脱敏观察、越权队伍、非法坐标、额外字段、冷却拒收、超时与旧响应隔离、两份真实 stdio 子进程完成 Bo3、v2 包校验与按日志重演。
