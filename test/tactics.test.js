import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { Battle, initPhysics } from "../src/engine.js";
import {
  VERSION,
  UNITS,
  MAPS,
  defaultConfig,
  canPlace,
  teamCost,
  validateConfig,
  tacticalPreview,
  MISSIONS,
  missionArmy,
  formation,
  cost,
} from "../src/data.js";
import { AI_STYLES, planArmy, aiDuel } from "../src/tactics.js";
import {
  squad,
  placeSquad,
  moveSquad,
  hotseatView,
  nextHotseat,
} from "../src/deployment.js";
import { replayPackage, validateReplay } from "../src/replay.js";
await initPhysics();
const u = (type, x, z = 0, stance = "hold") => ({ type, x, z, stance });
const config = () => ({
  ...defaultConfig(),
  red: [u("militia", -5)],
  blue: [u("giant", 15)],
  props: [],
  budget: 10000,
});
function run(c, record = false) {
  const b = new Battle(c),
    frames = record ? [b.snapshot()] : [];
  try {
    while (!b.result) {
      b.step();
      if (record && b.tick % 6 === 0) frames.push(b.snapshot());
    }
    if (record && b.tick % 6 !== 0) frames.push(b.snapshot());
    return { result: b.result, frames };
  } finally {
    b.dispose();
  }
}
test("all AI styles obey budget, unit/prop caps, half-field and exact shared spacing on every map", () => {
  for (const map of Object.keys(MAPS))
    for (const style of Object.keys(AI_STYLES))
      for (const team of ["red", "blue"])
        for (const budget of [100, 650, 2200, 10000]) {
          const plan = planArmy({ map, style, team, budget, seed: 42 });
          assert.ok(plan.units.length > 0);
          assert.ok(plan.units.length <= 60);
          assert.ok(plan.props.length <= 12);
          assert.ok(plan.spent <= budget);
          const c = {
            ...config(),
            map,
            budget,
            seed: 42,
            red: [],
            blue: [],
            props: [],
          };
          for (const item of plan.units) {
            assert.ok(
              canPlace(c, team, item),
              `${map}/${style}/${team}/${budget}`,
            );
            c[team].push(item);
          }
          for (const p of plan.props) {
            assert.ok(canPlace(c, team, p, "prop"));
            c.props.push(p);
          }
          validateConfig(c);
          assert.equal(teamCost(c, team), plan.spent);
        }
});
test("AI plans are seeded, styles differ, and cavalry scouting increases spear share", () => {
  const args = {
    map: "meadow",
    team: "red",
    seed: 81,
    budget: 2200,
    style: "counter",
  };
  assert.deepEqual(planArmy(args), planArmy(args));
  const cavalry = Array.from({ length: 5 }, () => ({ type: "cavalry" }));
  const normal = planArmy(args),
    counter = planArmy({ ...args, opponent: cavalry });
  assert.ok(
    counter.units.filter((u) => u.type === "spear").length >
      normal.units.filter((u) => u.type === "spear").length,
  );
  assert.notDeepEqual(
    planArmy({ ...args, style: "guardian" }),
    planArmy({ ...args, style: "maneuver" }),
  );
});
test("environment AI changes highland/bridge composition, mobile marsh units use flanking", () => {
  const high = planArmy({ map: "highland", style: "counter" }),
    bridge = planArmy({ map: "bridge", style: "counter" }),
    mobile = planArmy({ map: "marsh", style: "maneuver" });
  assert.ok(high.units.some((u) => u.type === "archer" && Math.abs(u.x) < 4));
  assert.ok(bridge.units.some((u) => u.type === "giant"));
  assert.ok(bridge.props.some((p) => p.type === "barrel"));
  assert.ok(
    mobile.units.some(
      (u) => u.type === "cavalry" && u.stance === "flank" && Math.abs(u.z) > 9,
    ),
  );
});
test("legacy 0.1 and 0.2 fixtures migrate with default stance and retain army coordinates", async () => {
  for (const file of [
    "bridge-battle.json",
    "highland-skirmish.json",
    "large-battle.json",
  ]) {
    const raw = JSON.parse(
      await fs.readFile(
        new URL("../examples/" + file, import.meta.url),
        "utf8",
      ),
    );
    for (const version of ["0.1.0", "0.2.0"]) {
      const c = structuredClone(raw);
      c.version = version;
      delete c.red[0].stance;
      const n = validateConfig(c);
      assert.equal(n.version, VERSION);
      assert.equal(n.red[0].stance, "advance");
      assert.equal(n.red[0].x, raw.red[0].x);
    }
  }
});
test("hunt targets costly ranged backline while advance chooses the nearest soldier", () => {
  const c = config();
  c.red = [u("cavalry", -5, 0, "hunt")];
  c.blue = [u("militia", 3), u("catapult", 7, 2)];
  const b = new Battle(c);
  try {
    const a = b.units[0];
    assert.equal(b.target(a).type, "catapult");
    a.stance = "advance";
    assert.equal(b.target(a).type, "militia");
  } finally {
    b.dispose();
  }
});
test("hold returns to its anchor after displacement, and does not advance with no threat", () => {
  const b = new Battle(config());
  try {
    const a = b.units[0];
    a.body.setTranslation({ x: -9, y: 0.8, z: 0 }, true);
    for (let i = 0; i < 120; i++) b.step();
    assert.ok(a.position.x > -8);
    assert.ok(a.position.x < -5.5);
  } finally {
    b.dispose();
  }
});
test("flanking on marsh uses dry outer lanes before crossing", () => {
  const c = config();
  c.map = "marsh";
  c.red = [u("cavalry", -10, 6, "flank")];
  const b = new Battle(c);
  try {
    let side = false,
      crossed = false;
    for (let i = 0; i < 1000 && !b.result; i++) {
      b.step();
      const p = b.units[0].position;
      side ||= Math.abs(p.z) > 9;
      crossed ||= p.x > 1;
      if (crossed) break;
    }
    assert.ok(side && crossed);
  } finally {
    b.dispose();
  }
});
test("hotseat hides sealed units and props through handoff; reveal is explicit and empty armies cannot seal", () => {
  const c = config();
  c.props = [{ type: "banner", team: "red", x: -9, z: 5 }];
  assert.throws(() => nextHotseat("red", { ...c, red: [] }));
  assert.equal(hotseatView(c, "red").blue.length, 0);
  const handoff = nextHotseat("red", c);
  assert.equal(handoff, "handoff");
  assert.equal(hotseatView(c, handoff).red.length, 0);
  const blue = nextHotseat(handoff, c),
    visible = hotseatView(c, blue);
  assert.equal(visible.red.length, 0);
  assert.deepEqual(visible.props, []);
  assert.equal(c.red.length, 1);
  assert.equal(nextHotseat(blue, c), "revealed");
  assert.deepEqual(hotseatView(c, "revealed"), c);
});
test("mixed squares, lines, wedges, mirrors and group moves are atomic and respect spacing", () => {
  for (const shape of ["square", "line", "wedge"]) {
    const c = { ...config(), red: [], blue: [] };
    const added = squad({
      roster: [
        { type: "shield", count: 2 },
        { type: "archer", count: 2, stance: "hunt" },
      ],
      shape,
      x: -5,
      z: 0,
    });
    const placed = placeSquad(c, "red", added);
    assert.equal(placed.red.length, 4);
    assert.equal(placed.red[3].stance, "hunt");
    const moved = moveSquad(placed, "red", [0, 1, 2, 3], -1, 0);
    assert.equal(moved.red[0].x, placed.red[0].x - 1);
    assert.throws(() => moveSquad(placed, "red", [0, 1], 20, 0));
    assert.equal(placed.red[0].x, -5);
  }
  const mirrored = squad({
    roster: [{ type: "militia", count: 2 }],
    shape: "line",
    x: -6,
    z: 4,
    mirror: true,
  });
  assert.equal(mirrored.length, 4);
  assert.equal(mirrored[0].z, -mirrored[2].z);
  assert.equal(
    placeSquad({ ...config(), red: [] }, "red", mirrored).red.length,
    4,
  );
});
test("tactical previews match elevated attack range and exact prop trigger/blast/heal radii", () => {
  const c = config();
  c.map = "highland";
  const archer = u("archer", -2.5);
  assert.equal(tacticalPreview(c, archer).radius, UNITS.archer.range * 1.18);
  assert.equal(tacticalPreview(c, u("cavalry", -7)).charge, 1);
  for (const [type, radius] of Object.entries({
    barrel: 4.2,
    banner: 3.2,
    spikes: 1.15,
  }))
    assert.equal(
      tacticalPreview(c, { type, x: -6, z: 0 }, "prop").radius,
      radius,
    );
});
test("contributions count effective damage, healing, enemy kills and friendly damage without overkill", () => {
  const c = config();
  c.red = [u("militia", -5), u("medic", -8)];
  c.blue = [u("militia", 5)];
  const b = new Battle(c);
  try {
    const [a, m, e] = b.units;
    e.hp = 20;
    b.hit(e, 999, a, 0);
    assert.equal(a.contribution.damageDealt, 20);
    assert.equal(a.contribution.kills, 1);
    b.hit(a, 10, m, 0);
    assert.equal(m.contribution.friendlyFire, 10);
    b.heal(a, 999, m);
    assert.equal(m.contribution.healing, 10);
    b.heal(e, 999, m);
    assert.equal(e.hp, 0);
    b.step();
    const r = b.result.report;
    assert.equal(r.teams.blue.lost, 1);
    assert.equal(r.teams.red.friendlyFire, 10);
    assert.equal(r.mvp.id, a.id);
  } finally {
    b.dispose();
  }
});
test("falls and recent knockback kills have explicit attribution; barrel owner reports its friendly fire", () => {
  const c = config();
  c.props = [{ type: "barrel", team: "red", x: -5, z: 1 }];
  const b = new Battle(c);
  try {
    const [a, e] = b.units;
    b.hit(e, 1, a, 2);
    e.body.setTranslation({ x: 15, y: -5, z: 0 }, true);
    b.step();
    assert.equal(e.contribution.falls, 1);
    assert.equal(a.contribution.fallKills, 1);
  } finally {
    b.dispose();
  }
  const d = new Battle(c);
  try {
    d.damageProp(d.props[0], 999);
    assert.ok(d.props[0].contribution.friendlyFire > 0);
    assert.equal(
      d.report().teams.red.friendlyFire,
      Math.round(d.props[0].contribution.friendlyFire * 1000) / 1000,
    );
  } finally {
    d.dispose();
  }
});
test("AI battle, statistics and complete 10 Hz replay are exactly reproducible", () => {
  const c = aiDuel(
    { ...defaultConfig("bridge"), budget: 1000 },
    "maneuver",
    "counter",
  );
  assert.deepEqual(run(c, true), run(c, true));
});
test("trajectory replay and seed package roundtrip preserve physics and final result", () => {
  const c = config();
  c.red = [u("cavalry", -7, 0, "advance")];
  c.blue = [u("militia", 5)];
  const first = run(c, true),
    pkg = replayPackage(c, first.frames, first.result);
  assert.deepEqual(validateReplay(JSON.parse(JSON.stringify(pkg))), pkg);
  const seed = validateReplay(
    JSON.parse(
      JSON.stringify(replayPackage(c, first.frames, first.result, "seed")),
    ),
  );
  assert.deepEqual(run(seed.config, true), first);
});
test("replay validation rejects prototype keys, mismatched IDs, NaNs, huge arrays and corrupt metadata", () => {
  const c = config();
  c.red[0].stance = "advance";
  const r = run(c, true),
    pkg = replayPackage(c, r.frames, r.result);
  for (const mutate of [
    (p) => (p.engineVersion = "0.2.0"),
    (p) => (p.frames[0].units[0].t[0] = NaN),
    (p) => (p.frames[1].time = -1),
    (p) => (p.frames[0].units[0].id = 99),
    (p) => (p.frames = Array(903).fill(p.frames[0])),
    (p) => (p.result.report.teams.red.byType.constructor = {}),
  ]) {
    const copy = structuredClone(pkg);
    mutate(copy);
    assert.throws(() => validateReplay(copy));
  }
  assert.throws(() =>
    validateReplay(
      JSON.parse('{"format":"frontline-replay","__proto__":{"polluted":true}}'),
    ),
  );
  assert.equal({}.polluted, undefined);
});
test("stuck infantry escape a friendly barricade and keep making progress", () => {
  const c = config();
  c.red = [u("militia", -6, 0, "advance")];
  c.props = [{ type: "barricade", team: "red", x: -3.5, z: 0 }];
  const b = new Battle(c);
  try {
    let passed = false;
    for (let i = 0; i < 1000 && !b.result; i++) {
      b.step();
      if (b.units[0].position.x > -2) passed = true;
    }
    assert.ok(b.units[0].progress.escapes > 0);
    assert.ok(
      passed,
      "must get around cover rather than repeatedly reverse into it",
    );
  } finally {
    b.dispose();
  }
});
test("escape tangents never deliberately point into the bridge chasm", () => {
  const c = config();
  c.map = "bridge";
  c.red = [u("militia", -2.5, 0, "advance")];
  const b = new Battle(c);
  try {
    const a = b.units[0];
    a.position = { x: 0, y: 0.8, z: 1.4 };
    a.progress.side = 1;
    const d = b.escapeDirection(a, 1, 0);
    assert.ok(d);
    assert.ok(
      Number.isFinite(
        tacticalPreview(
          { ...c, blue: [] },
          {
            type: "militia",
            x: a.position.x + d.x * 0.85,
            z: a.position.z + d.z * 0.85,
          },
        ).floor,
      ),
    );
  } finally {
    b.dispose();
  }
});
test("cavalry and catapult wreck visuals share the bounded doll lifecycle without extra rigid bodies", () => {
  const c = config();
  c.red = [u("cavalry", -6), u("catapult", -10, 3)];
  const b = new Battle(c);
  try {
    b.kill(b.units[0], { x: 0, y: 0, z: 0 });
    b.kill(b.units[1], { x: 0, y: 0, z: 0 });
    assert.ok(b.snapshot().ragdolls.every((d) => d.wreck.length === 4));
    assert.equal(b.world.impulseJoints.len(), 10);
    assert.ok(b.ragdolls.every((d) => d.parts.length === 6));
  } finally {
    b.dispose();
  }
});
test("CLI AI evaluation accepts strategies and emits versioned battle contribution JSON", () => {
  const output = execFileSync(
    process.execPath,
    [
      "scripts/battle.mjs",
      "--red-ai",
      "guardian",
      "--blue-ai",
      "counter",
      "--budget",
      "650",
      "--rounds",
      "1",
    ],
    { cwd: new URL("..", import.meta.url), encoding: "utf8" },
  );
  const report = JSON.parse(output);
  assert.equal(report.schema, "frontline-evaluation@1");
  assert.equal(report.results.length, 1);
  assert.ok(report.results[0].report.mvp);
  validateConfig(report.config);
});

test("trial enemies use tactical stances and each updated trial has a legal winning reference", () => {
  const refs = ["ai:counter", "volley", "volley", "siege", "volley", "siege"];
  for (const [i, m] of MISSIONS.entries()) {
    const red = [];
    for (const unit of formation(refs[i]))
      if (cost(red) + UNITS[unit.type].cost <= m.budget) red.push(unit);
    const blue = missionArmy(m);
    let props = [];
    if (refs[i].startsWith("ai:")) {
      const plan = planArmy({
        map: m.map,
        team: "red",
        budget: m.budget,
        seed: 20261116,
        style: refs[i].slice(3),
        opponent: blue,
      });
      red.length = 0;
      red.push(...plan.units);
      props = plan.props;
    }
    assert.ok(blue.some((u) => ["flank", "hunt", "hold"].includes(u.stance)));
    const result = run({
      ...defaultConfig(m.map),
      budget: Math.max(m.budget, cost(blue)),
      red,
      blue,
      props,
    }).result;
    assert.equal(result.winner, "red", m.name);
  }
});

test("all six AI battlefields produce importable bounded trajectory packages", () => {
  for (const map of Object.keys(MAPS)) {
    const c = aiDuel(
      { ...defaultConfig(map), budget: 1000 },
      "counter",
      "maneuver",
    );
    const r = run(c, true);
    assert.equal(
      replayPackage(c, r.frames, r.result).frames.length,
      r.frames.length,
      map,
    );
  }
});

test("CLI accepts independent legacy JSON armies on both sides and labels config participants accurately", () => {
  const output = execFileSync(
    process.execPath,
    [
      "scripts/battle.mjs",
      "--red",
      "file:examples/bridge-battle.json",
      "--blue",
      "file:examples/highland-skirmish.json",
      "--map",
      "meadow",
    ],
    { cwd: new URL("..", import.meta.url), encoding: "utf8" },
  );
  const r = JSON.parse(output);
  assert.equal(r.config.red.length, 12);
  assert.equal(r.config.blue.length, 12);
  assert.ok(r.results[0].report.units.length === 24);
  const fromConfig = JSON.parse(
    execFileSync(
      process.execPath,
      ["scripts/battle.mjs", "--config", "examples/bridge-battle.json"],
      { cwd: new URL("..", import.meta.url), encoding: "utf8" },
    ),
  );
  assert.equal(fromConfig.participants.red, "config:red");
  assert.equal(fromConfig.participants.blue, "config:blue");
});
