# 回放包 v1

`src/replay.js` 是网页与 CLI 共用的唯一入口。包是普通 JSON，`format="frontline-replay"`、`version=1`、`engineVersion="0.3.0"`。

## 两种包

- `kind="trajectory"`：包含 `config`、`result` 和完整 `frames`。直接播放轨迹，不重新执行物理。
- `kind="seed"`：包含 `config`，不包含帧或结算。使用同版本 Battle 重新计算并录制；这是体积小的重演配方。

种子包示例：

```json
{
  "format": "frontline-replay",
  "version": 1,
  "engineVersion": "0.3.0",
  "kind": "seed",
  "config": {
    "version": "0.3.0",
    "map": "meadow",
    "seed": 42,
    "budget": 2200,
    "red": [{ "type": "cavalry", "x": -5, "z": 0, "stance": "advance" }],
    "blue": [{ "type": "militia", "x": 5, "z": 0, "stance": "advance" }],
    "props": []
  }
}
```

## 轨迹

帧结构沿用 `Battle.snapshot()`：`time`、`units`、`projectiles`、`ragdolls`、`effects`、`props`，最后一帧关联结算。单位顺序和 ID 对应初始配置的红军再蓝军。`t` 是七元素数组 `[x,y,z,qx,qy,qz,qw]`；阵亡单位 `t=null`。抛射物速度保存为普通 `{x,y,z}`，不序列化 Rapier 对象。

首帧时间为 0，每六个 60 Hz 步保存一帧；终局不在六步边界时补一帧。90 秒对局至多 902 帧。轨迹播放不插值，和已有时间轴一致。包含 `age` 与可选 `wreck` 的尸体帧负责同步残骸显示。

## 校验与限制

- 网页回放文件上限 64 MB；布阵文件仍为 1 MB。回放导出使用紧凑 JSON。
- 拒绝 `__proto__`、`constructor`、`prototype` 等危险键，限制递归深度与节点总数。
- 校验配置军费、单位、阵营、坐标和姿态；接受 0.1 / 0.2 配置后规范化。
- 校验帧数、时间单调与 0.1 秒间隔、单位 ID / 类型 / 阵营、有限数值和四元数、六部位唯一名称。
- 每帧尸体至多 24 组、抛射物至多 1,024 个、特效至多 41 个；道具数量对应地图与初始布置。
- 校验最终时间、存活人数、战果与统计结构。它不是带签名的比赛认证，不能防止人为伪造一份结构合法的战绩。
- 当前只接受当前 `engineVersion` 的回放，避免将未来格式误解为可播放数据。没有跨平台逐位一致保证。

命令行导出示例：

```sh
npm run battle -- --config examples/ai-duel.json --replay-out replay.json --out report.json
```

在战果页可分别下载轨迹或种子包，之后从策略档案重新载入。完整回放比布阵 JSON 大，请按需要选择包类型。
