import fs from "node:fs/promises";
import { Battle, initPhysics } from "../src/engine.js";
import { defaultConfig, formation, MAPS, validateConfig } from "../src/data.js";

const args = process.argv.slice(2);
let map = "meadow",
  red = "balanced",
  blue = "balanced",
  seed = 20261116,
  input = null,
  output = null,
  rounds = 1;
function value(index) {
  if (!args[index + 1] || args[index + 1].startsWith("--"))
    throw Error("缺少参数值：" + args[index]);
  return args[index + 1];
}
try {
  for (let i = 0; i < args.length; i++)
    switch (args[i]) {
      case "--map":
        map = value(i++);
        break;
      case "--red":
        red = value(i++);
        break;
      case "--blue":
        blue = value(i++);
        break;
      case "--seed":
        seed = Number(value(i++));
        break;
      case "--config":
        input = value(i++);
        break;
      case "--out":
        output = value(i++);
        break;
      case "--rounds":
        rounds = Number(value(i++));
        break;
      case "--help":
        console.log(
          "npm run battle -- [--map meadow|bridge|island] [--red balanced|rush|volley] [--blue balanced|rush|volley] [--seed N] [--rounds N] [--config battle.json] [--out result.json]",
        );
        process.exit(0);
      default:
        throw Error("未知参数：" + args[i]);
    }
  if (!MAPS[map]) throw Error("未知地图");
  const presets = [
    "balanced",
    "rush",
    "volley",
    "phalanx",
    "battery",
    "titans",
  ];
  if (!presets.includes(red) || !presets.includes(blue))
    throw Error("未知阵型");
  if (!Number.isInteger(rounds) || rounds < 1 || rounds > 100)
    throw Error("rounds 须为 1–100");
  const base = input
    ? JSON.parse(await fs.readFile(input, "utf8"))
    : {
        ...defaultConfig(map),
        seed,
        red: formation(red),
        blue: formation(blue, "blue"),
      };
  validateConfig(base);
  await initPhysics();
  const results = [];
  for (let i = 0; i < rounds; i++) {
    const config = { ...base, seed: (base.seed + i) >>> 0 };
    const b = new Battle(config);
    try {
      while (!b.result) b.step();
      results.push({ seed: config.seed, ...b.result });
    } finally {
      b.dispose();
    }
  }
  const report = {
    engine: "frontline-lab@0.1.0",
    map: base.map,
    rounds,
    wins: {
      red: results.filter((r) => r.winner === "red").length,
      blue: results.filter((r) => r.winner === "blue").length,
      draw: results.filter((r) => r.winner === "draw").length,
    },
    results,
  };
  console.log(JSON.stringify(report, null, 2));
  if (output)
    await fs.writeFile(output, JSON.stringify(report, null, 2) + "\n");
} catch (e) {
  console.error("Battle error:", e.message);
  process.exitCode = 1;
}
