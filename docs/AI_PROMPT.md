# 给 AI 的布阵任务

你正在参加《阵线实验室》3D 物理战斗。请阅读 README.md、src/data.js 和 examples/bridge-battle.json，为朱砂军生成更强的布阵。

1. 只修改 JSON 中的 red 数组，可更改 seed 进行测试；保持 map、budget 和 blue 不变。
2. 每个单位为 `{ "type": "spear", "x": -7, "z": 0, "stance": "advance" }`。兵种须来自 UNITS；stance 支持 advance / hold / flank / hunt，省略时默认 advance。hold 保持阵位并在被推离后归位；flank 走外侧路线；hunt 优先高价值远程与军医。
3. 红方 x ≤ -2.5，蓝方 x ≥ 2.5；|x| ≤ 16.8，|z| ≤ 10.8，并且位于当前地图平台内。浮岛实际边界更小，见 MAPS。
4. 每方最多 60 个单位和 12 件道具。红方兵种和道具的总价不得超过 budget。建议间距至少 1.25 米，大锤需要更多空间。
5. 利用盾卫正面减伤、长枪克制骑兵、军医治疗、弩箭破盾、投石车抛射、爆炸友伤和击退。研究遗迹开口、沼泽侧路、高台与冰面。
6. 用 `npm run battle -- --config your-battle.json --rounds 5` 本地评估，选择多个种子；也在网页里导入检查布阵。
7. 最后交付合法 JSON，附上实际运行的命令和胜率。区分已测试结果与猜测。

引擎不会执行 JSON 里的 JavaScript、Python 或 C++。额外字段不能改变物理规则。请通过调整军队和姿态优化策略。

0.2 支持根级 `props` 数组。例如：`{"type":"barricade","team":"red","x":-4,"z":0}`。可用类型为 barrel / barricade / spikes / spring / banner。坐标必须在本方半场，和军队共用军费。地图自带的中立道具来自 MAPS，不需要在 JSON 重复添加。参阅 examples/highland-skirmish.json。

## v0.4 布阵—复盘—调阵接口

```sh
npm run battle -- --red file:red-army.json --blue ai:guardian --map ruins --budget 2200 --seed 42 --rounds 5 --out report.json
npm run battle -- --red file:red-army.json --blue file:blue-army.json --map highland --out report.json
npm run battle -- --red-ai maneuver --blue-ai counter --map marsh --out report.json
npm run battle -- --config examples/ai-duel.json --out report.json --replay-out replay.json
npm run battle -- --red-ai counter --blue-ai maneuver --map ruins --bo3 --commander --out bo3.json
```

四种内置策略名为 guardian / maneuver / counter / oracle。oracle 在固定预算内试跑候选，选残余战力最高者，不能据单次推演声称总体必胜。双方文件可以是单位数组、`{units,props}` 或完整对局；完整对局按 red / blue 提取对应一方。阵营坐标必须正确，不会自动镜像。`--config` 默认保留原双方，显式单方参数才覆盖。`--rounds` 固定编队，逐局递增物理种子，不是在每一局重新规划 AI。

输出 schema 为 `frontline-evaluation@1`。每局在 `results` 中包含 `seed`、`winner`、`reason`、`time`、`remainingValue.red/blue` 和 `report`。读取 `report.teams[team].byType` 研究兵种伤亡，读取 `report.units` 研究单兵贡献：damageDealt、damageTaken、healing、kills、falls、fallKills、friendlyFire、friendlyKills。伤害按实际扣血计，不含过量；damageDealt 包含友伤，friendlyFire 是其子集。桶的爆炸贡献归桶，见 V0.3.md。

每轮保留同样的对手与地图，修改自己的配兵、位置、姿态或道具，使用多个种子比较胜率、剩余战力与误伤。避免只对一个种子调参。若要导出网页可播放的完整回放，使用单局 `--replay-out`，不要把评测报告误当回放包。不要在结果未实际运行时声称获胜。

为新阵型复用 `src/data.js` 的 `canPlace()` 或 `src/deployment.js` 的 `placeSquad()` 校验间距；网页与内置 AI 使用这些函数。旧 JSON 兼容导入保留历史重叠容忍，不应把重叠当成优化手段。JSON 只携带配置数据，不会执行任意程序，也不会接受修改伤害、生命或预算规则的额外字段。

v0.4 的 `counter` 不只检查骑兵，也读取公开的重甲、突进、远程/军医集群和空间展开。Bo3 中可使用 `series.rounds[n].config` 和 `.result.report` 复盘；下一局地图、军费和军令开关固定，种子递增。最多三局，先两胜提前结束，含平局时按最终胜局数判断。

可选 `commander: {enabled:true,aiTeams:["red","blue"]}` 让 CLI 双方自动使用统帅军令。人类与 AI 统一调用 `Battle.issueOrder()`，各 3 点、统一 8 秒冷却；军令日志是可复现输入的一部分。`--commander` 是该配置的快捷开关，`--bo3` 启用公开战报调阵。单局批量 `--rounds` 仍保持固定阵型。

## v0.5 擂台与实时智能体

多 AI / JSON / 收藏循环赛见 `docs/V0.5.md` 与 `examples/tournament*.json`；实时 Python / Node / 模型适配器使用 `docs/AGENT_PROTOCOL.md`。静态布阵 JSON 与实时军令协议是不同的数据边界。不要把战报当回放或把 JSON 当可执行脚本。
