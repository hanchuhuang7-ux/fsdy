# 回放包 v2 与旧版兼容

`src/replay.js` 是网页与 CLI 共用的入口。新包是普通 JSON，`format="frontline-replay"`、`version=2`、`engineVersion="0.5.0"`。继续兼容 `version=1`、引擎 0.1 / 0.2 / 0.3 的旧轨迹与种子包。

## 两种包

- `kind="trajectory"`：包含 `config`、`result` 和完整 `frames`。直接播放轨迹，不重新执行物理。
- `kind="seed"`：包含 `config`，不包含帧或结算。使用同版本 Battle 重新计算并录制；这是体积小的重演配方。

种子包示例：

```json
{
  "format": "frontline-replay",
  "version": 2,
  "engineVersion": "0.5.0",
  "kind": "seed",
  "config": {
    "version": "0.5.0",
    "map": "meadow",
    "seed": 42,
    "budget": 2200,
    "red": [{ "type": "cavalry", "x": -5, "z": 0, "stance": "advance" }],
    "blue": [{ "type": "militia", "x": 5, "z": 0, "stance": "advance" }],
    "props": [],
    "commander": { "enabled": false, "aiTeams": [] }
  },
  "orderLog": []
}
```

## 轨迹

帧结构沿用 `Battle.snapshot()`：`time`、`units`、`projectiles`、`ragdolls`、`effects`、`props`，最后一帧关联结算。v2 增加 `tick`、`commander` 和 `cues`；单位增加 `stance`、`inspired`、`fortified`。单位顺序和 ID 对应初始配置的红军再蓝军。`t` 是七元素数组 `[x,y,z,qx,qy,qz,qw]`；阵亡单位 `t=null`。抛射物速度保存为普通 `{x,y,z}`，不序列化 Rapier 对象。

首帧时间为 0，每六个 60 Hz 步保存一帧；终局不在六步边界时补一帧。90 秒对局至多 902 帧。轨迹播放不插值，和已有时间轴一致。包含 `age` 与可选 `wreck` 的尸体帧负责同步残骸显示。

## 校验与限制

- 网页回放文件上限 64 MB；布阵文件仍为 1 MB。回放导出使用紧凑 JSON。
- 拒绝 `__proto__`、`constructor`、`prototype` 等危险键，限制递归深度与节点总数。
- 校验配置军费、单位、阵营、坐标和姿态；接受 0.1 / 0.2 配置后规范化。
- 校验帧数、时间单调与 0.1 秒间隔、单位 ID / 类型 / 阵营、有限数值和四元数、六部位唯一名称。
- 每帧尸体至多 24 组、抛射物至多 1,024 个、特效至多 41 个；道具数量对应地图与初始布置。
- 校验最终时间、存活人数、战果与统计结构。它不是带签名的比赛认证，不能防止人为伪造一份结构合法的战绩。
- v2 接受 0.4 / 0.5，按来源引擎重演；不把修改版本标签的 v2 包当成合法旧包。v1 接受 0.1 / 0.2 / 0.3。没有跨平台逐位一致保证。

命令行导出示例：

```sh
npm run battle -- --config examples/ai-duel.json --replay-out replay.json --out report.json
```

在战果页可分别下载轨迹或种子包，之后从策略档案重新载入。完整回放比布阵 JSON 大，请按需要选择包类型。

## 军令记录

根级 `orderLog` 保存接受的军令，每条 `{id,team,type,x,z,tick}`。每方最多三条、相邻至少 480 步，整局最多六条；坐标、枚举、冷却和点数必须合法。轨迹每帧的 `commander` 保存 `{points,readyAt,active,log}`，其日志必须是全局日志的有序前缀，点数和冷却与该前缀一致。活动军令额外保存 `endTick`。

种子重演调用 `Battle.useOrderLog()`，按整数 tick 注入同一个 `issueOrder()`，同时关闭重新决策。手动下令与 AI 下令采样当前刚体位置，避免点击时的旧缓存导致重演受益单位不同。测试会逐帧比较单位变换、统计、特效、提示事件和军令状态。

`cues` 是有界的视听提示数据，不是可执行脚本。每帧最多 24 条，带有 id、tick、类型、位置和强度；音频与相机只消费提示，不反向修改物理。

## 旧版迁移

旧完整轨迹不重新计算物理，只补显示所需的默认姿态、尸体年龄、空军令和空提示等字段。旧版没有记录贡献时标记 `legacyStatsUnavailable`，贡献数值置零，保留实际胜负与轨迹。旧地图没有预设道具时不会擅自加上当前地图道具。

旧种子包通过 `createReplayBattle()` 按需加载 `src/legacy/v0.1`、`v0.2`、`v0.3` 或 `v0.4` 的冻结引擎和数据，使用现有锁定的 Rapier 依赖。重新导出旧回放时保留来源版本，避免误标为新规则。0.1 / 0.2 原版没有提供回放文件导出，此兼容路径也接受它们合法快照封装的 v1 包。

格式校验不等于带签名的比赛认证，不能防止人为编造一份结构合法的轨迹或战报。

## v0.5 高光

回放根部可选 `highlights`，每项 `{id,type,team,tick,value}`，整局最多 24 项，ID 从 1 连续递增，tick 单调且不超过终局。type 为 order / firstKill / blast / reversal，缺省为空；结果也保存同一份稀疏记录。不会在每帧重复附加。时间轴定位事件后第一个完成的记录帧，末帧夹紧。冻结的 0.4 模拟器保留 v2 军令日志与旧战果。
