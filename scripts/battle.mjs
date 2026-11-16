import fs from "node:fs/promises";
import { Battle, initPhysics } from "../src/engine.js";
import {
  VERSION,
  defaultConfig,
  formation,
  MAPS,
  validateConfig,
  assertSafeData,
} from "../src/data.js";
import { AI_STYLES, withAI, aiDuel } from "../src/tactics.js";
import { replayPackage } from "../src/replay.js";
import { BattleSeries } from "../src/series.js";

const options = {
  map: "meadow",
  red: "ai:guardian",
  blue: "ai:counter",
  seed: 20261116,
  budget: 2200,
  rounds: 1,
};
const args = process.argv.slice(2);
const presets = [
  "balanced",
  "rush",
  "volley",
  "phalanx",
  "battery",
  "titans",
  "cavalry",
  "support",
  "siege",
];
async function read(path) {
  const stat = await fs.stat(path);
  if (stat.size > 1024 * 1024) throw Error("阵型文件须小于 1 MB");
  const raw = JSON.parse(await fs.readFile(path, "utf8"));
  assertSafeData(raw);
  return raw;
}
async function applySide(base, team, spec) {
  if (spec.startsWith("ai:")) return withAI(base, team, spec.slice(3));
  if (presets.includes(spec))
    return {
      ...base,
      [team]: formation(spec, team),
      props: base.props.filter((p) => p.team !== team),
    };
  const raw = await read(spec.startsWith("file:") ? spec.slice(5) : spec);
  const units = Array.isArray(raw) ? raw : raw.units || raw[team];
  const props = (raw.props || [])
    .filter((p) => !p.team || p.team === team)
    .map((p) => ({ ...p, team }));
  return validateConfig({
    ...base,
    [team]: units,
    props: [...base.props.filter((p) => p.team !== team), ...props],
  });
}
try {
  for (let i = 0; i < args.length; i++) {
    const name = args[i].slice(2);
    if (["bo3", "commander"].includes(name)) {
      options[name] = true;
      continue;
    }
    if (name === "help") {
      console.log(
        "npm run battle -- [--red ai:guardian|ai:maneuver|ai:counter|PRESET|file:army.json] [--blue SPEC] [--red-ai STYLE] [--blue-ai STYLE] [--config battle.json] [--map " +
          Object.keys(MAPS).join("|") +
          "] [--budget 2200] [--seed N] [--rounds 1..100] [--out report.json] [--replay-out replay.json] [--bo3] [--commander]",
      );
      process.exit(0);
    }
    if (
      ![
        "red",
        "blue",
        "red-ai",
        "blue-ai",
        "map",
        "budget",
        "seed",
        "rounds",
        "config",
        "out",
        "replay-out",
      ].includes(name)
    )
      throw Error("未知参数：" + args[i]);
    const val = args[++i];
    if (!val || val.startsWith("--")) throw Error("缺少参数值：" + name);
    if (name.endsWith("-ai")) options[name.slice(0, -3)] = "ai:" + val;
    else
      options[name] = ["budget", "seed", "rounds"].includes(name)
        ? Number(val)
        : val;
  }
  if (
    !Number.isInteger(options.rounds) ||
    options.rounds < 1 ||
    options.rounds > 100
  )
    throw Error("rounds 须为 1–100");
  if (options["replay-out"] && (options.rounds !== 1 || options.bo3))
    throw Error("回放导出要求 rounds=1");
  if (!Object.hasOwn(MAPS, options.map)) throw Error("未知地图");
  const raw = options.config
    ? await read(options.config)
    : {
        ...defaultConfig(options.map),
        red: [],
        blue: [],
        props: [],
        budget: options.budget,
        seed: options.seed,
      };
  let base = validateConfig(raw);
  // --config preserves both armies unless an explicit side override is supplied.
  if (options.config) {
    for (const team of ["red", "blue"])
      if (args.includes("--" + team) || args.includes("--" + team + "-ai"))
        base = await applySide(base, team, options[team]);
  } else if (options.red.startsWith("ai:") && options.blue.startsWith("ai:"))
    base = aiDuel(base, options.red.slice(3), options.blue.slice(3));
  else {
    const order = options.red.startsWith("ai:")
      ? ["blue", "red"]
      : ["red", "blue"];
    for (const t of order) base = await applySide(base, t, options[t]);
  }
  base = validateConfig(base);
  if (options.commander)
    base.commander = { enabled: true, aiTeams: ["red", "blue"] };
  await initPhysics();
  const results = [];
  const series = options.bo3 ? new BattleSeries(base) : null;
  const isAI = (t) =>
    options[t].startsWith("ai:") &&
    (!options.config ||
      args.includes("--" + t) ||
      args.includes("--" + t + "-ai"));
  for (let i = 0; i < (series ? 3 : options.rounds); i++) {
    let config = { ...base, seed: (base.seed + i) >>> 0 };
    if (series?.last) {
      config = series.nextConfig();
      if (isAI("red") && isAI("blue"))
        config = aiDuel(
          config,
          options.red.slice(3),
          options.blue.slice(3),
          series.last,
        );
      else
        for (const t of ["red", "blue"])
          if (isAI(t))
            config = withAI(
              config,
              t,
              options[t].slice(3),
              config[t === "red" ? "blue" : "red"],
              series.last,
            );
    }
    const b = new Battle(config),
      frames = options["replay-out"] ? [b.snapshot()] : null;
    try {
      while (!b.result) {
        b.step();
        if (frames && b.tick % 6 === 0) frames.push(b.snapshot());
      }
      if (frames) {
        if (b.tick % 6 !== 0) frames.push(b.snapshot());
        await fs.writeFile(
          options["replay-out"],
          JSON.stringify(replayPackage(config, frames, b.result)) + "\n",
        );
      }
      results.push({ seed: config.seed, ...b.result });
      if (series) series.record(config, b.result);
    } finally {
      b.dispose();
    }
    if (series?.complete) break;
  }
  const report = {
    schema: "frontline-evaluation@1",
    engine: "frontline-lab@" + VERSION,
    map: base.map,
    budget: base.budget,
    participants: options.config
      ? {
          config: options.config,
          red:
            args.includes("--red") || args.includes("--red-ai")
              ? options.red
              : "config:red",
          blue:
            args.includes("--blue") || args.includes("--blue-ai")
              ? options.blue
              : "config:blue",
        }
      : { red: options.red, blue: options.blue },
    rounds: results.length,
    formationPolicy: series
      ? "Bo3 public report adaptation; seed increments per game"
      : "fixed formations; seed increments per round",
    series: series?.export() || null,
    config: base,
    wins: Object.fromEntries(
      ["red", "blue", "draw"].map((t) => [
        t,
        results.filter((r) => r.winner === t).length,
      ]),
    ),
    results,
  };
  console.log(JSON.stringify(report, null, 2));
  if (options.out)
    await fs.writeFile(options.out, JSON.stringify(report, null, 2) + "\n");
} catch (e) {
  console.error("Battle error:", e.message);
  process.exitCode = 1;
}
