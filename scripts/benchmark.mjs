import { performance } from "node:perf_hooks";
import fs from "node:fs/promises";
import { Battle, initPhysics } from "../src/engine.js";
import { VERSION } from "../src/data.js";
await initPhysics();
const types = ["militia", "shield", "spear", "archer", "bomber"];
const army = (team) =>
  Array.from({ length: 45 }, (_, i) => ({
    type: types[i % 5],
    x: (team === "red" ? -1 : 1) * (4 + Math.floor(i / 9) * 2.7),
    z: ((i % 9) - 4) * 2,
    stance: "advance",
  }));
const config = {
    version: VERSION,
    map: "meadow",
    seed: 42,
    budget: 10000,
    red: army("red"),
    blue: army("blue"),
    props: [],
  },
  samples = [];
for (let r = 0; r < 6; r++) {
  const b = new Battle(config);
  const begin = performance.now();
  let steps = 0;
  while (steps < 720 && !b.result) {
    b.step();
    if (b.tick % 6 === 0) b.snapshot();
    steps++;
  }
  const ms = performance.now() - begin;
  samples.push({ ms, steps, msPerStep: ms / steps });
  b.dispose();
}
const report = {
  version: VERSION,
  node: process.version,
  platform: process.platform,
  seed: 42,
  warmupRuns: 1,
  measuredRuns: 5,
  fixture: "45v45 mixed, 720 fixed steps + 10 Hz snapshots",
  samples,
  medianMsPerStep: samples
    .slice(1)
    .map((s) => s.msPerStep)
    .sort((a, b) => a - b)[2],
};
console.log(JSON.stringify(report, null, 2));
if (process.argv[2])
  await fs.writeFile(process.argv[2], JSON.stringify(report, null, 2) + "\n");
