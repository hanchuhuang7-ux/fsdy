import { performance } from "node:perf_hooks";
import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Battle, initPhysics } from "../src/engine.js";
import { VERSION } from "../src/data.js";

// A local, trusted v0.2 checkout with the same pinned dependencies is required.
const root = process.argv[2];
if (!root)
  throw Error(
    "Usage: node scripts/benchmark-compare.mjs <v0.2-checkout> [report.json]",
  );
const baseline = await import(
  pathToFileURL(path.resolve(root, "src/engine.js"))
);
const baselineData = await import(
  pathToFileURL(path.resolve(root, "src/data.js"))
);
if (baselineData.VERSION !== "0.2.0") throw Error("对照目录必须是干净 v0.2.0");
await initPhysics();
await baseline.initPhysics();
const types = ["militia", "shield", "spear", "archer", "bomber"];
const army = (team) =>
  Array.from({ length: 45 }, (_, i) => ({
    type: types[i % 5],
    x: (team === "red" ? -1 : 1) * (4 + Math.floor(i / 9) * 2.7),
    z: ((i % 9) - 4) * 2,
    stance: "advance",
  }));
const input = {
  version: "0.2.0",
  map: "meadow",
  seed: 42,
  budget: 10000,
  red: army("red"),
  blue: army("blue"),
  props: [],
};
const samples = { baseline: [], current: [] };
const classes = { baseline: baseline.Battle, current: Battle };
for (let round = 0; round < 6; round++)
  for (const name of round % 2
    ? ["current", "baseline"]
    : ["baseline", "current"]) {
    const battle = new classes[name](input),
      start = performance.now();
    let steps = 0;
    try {
      while (steps < 720 && !battle.result) {
        battle.step();
        if (battle.tick % 6 === 0) battle.snapshot();
        steps++;
      }
      const ms = performance.now() - start;
      samples[name].push({
        round,
        ms,
        steps,
        msPerStep: ms / steps,
        surviving: battle.units.filter((u) => u.alive).length,
        corpses: battle.ragdolls.length,
      });
    } finally {
      battle.dispose();
    }
  }
const median = (list) =>
  list
    .slice(1)
    .map((s) => s.msPerStep)
    .sort((a, b) => a - b)[2];
const before = median(samples.baseline),
  after = median(samples.current);
const report = {
  baselineVersion: "0.2.0",
  currentVersion: VERSION,
  node: process.version,
  platform: process.platform,
  fixture:
    "45v45 mixed, seed 42, 720 fixed steps + 10 Hz snapshots; alternating A/B then B/A, first pair warmup",
  samples,
  baselineMedianMsPerStep: before,
  currentMedianMsPerStep: after,
  changePercent: (after / before - 1) * 100,
  regressionLimitPercent: 10,
  passed: after <= before * 1.1,
};
console.log(JSON.stringify(report, null, 2));
if (process.argv[3])
  await fs.writeFile(process.argv[3], JSON.stringify(report, null, 2) + "\n");
if (!report.passed) process.exitCode = 1;
