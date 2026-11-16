# 给 AI 的布阵任务

你正在参加《阵线实验室》3D 物理战斗。请阅读 README.md、src/data.js 和 examples/bridge-battle.json，为朱砂军生成更强的布阵。

1. 只修改 JSON 中的 red 数组，可更改 seed 进行测试；保持 map、budget 和 blue 不变。
2. 每个单位为 `{ "type": "spear", "x": -7, "z": 0, "stance": "advance" }`。兵种须来自 UNITS；stance 仅支持 advance / hold / flank。
3. 红方 x ≤ -2.5，蓝方 x ≥ 2.5；|x| ≤ 16.8，|z| ≤ 10.8，并且位于当前地图平台内。浮岛实际边界更小，见 MAPS。
4. 每方最多 60 个单位和 12 件道具。红方兵种和道具的总价不得超过 budget。建议间距至少 1.25 米，大锤需要更多空间。
5. 利用盾卫正面减伤、长枪克制骑兵、军医治疗、弩箭破盾、投石车抛射、爆炸友伤和击退。研究遗迹开口、沼泽侧路、高台与冰面。
6. 用 `npm run battle -- --config your-battle.json --rounds 5` 本地评估，选择多个种子；也在网页里导入检查布阵。
7. 最后交付合法 JSON，附上实际运行的命令和胜率。区分已测试结果与猜测。

引擎不会执行 JSON 里的 JavaScript、Python 或 C++。额外字段不能改变物理规则。请通过调整军队和姿态优化策略。

0.2 支持根级 `props` 数组。例如：`{"type":"barricade","team":"red","x":-4,"z":0}`。可用类型为 barrel / barricade / spikes / spring / banner。坐标必须在本方半场，和军队共用军费。地图自带的中立道具来自 MAPS，不需要在 JSON 重复添加。参阅 examples/highland-skirmish.json。
