import test from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { Battle, initPhysics } from "../src/engine.js";
import {
  VERSION,
  UNITS,
  ORDERS,
  ORDER_COOLDOWN,
  defaultConfig,
  validateConfig,
  combatPower,
} from "../src/data.js";
import { planArmy, scoutArmy, reviewBattle } from "../src/tactics.js";
import { BattleSeries } from "../src/series.js";
import { saveFavorite, loadFavorite, readFavorites } from "../src/favorites.js";
import {
  replayPackage,
  validateReplay,
  createReplayBattle,
} from "../src/replay.js";
import { BattleAudio } from "../src/audio.js";
await initPhysics();
const unit = (type, x, z = 0, stance = "hold") => ({ type, x, z, stance });
const fixture = (commander = true) => ({
  ...defaultConfig(),
  budget: 10000,
  seed: 42,
  red: [unit("shield", -6), unit("spear", -8, 2), unit("medic", -10)],
  blue: [unit("shield", 6), unit("spear", 8, 2), unit("medic", 10)],
  props: [],
  commander: { enabled: commander, aiTeams: [] },
});
const run = (b, record = false) => {
  const frames = record ? [b.snapshot()] : [];
  while (!b.result) {
    b.step();
    if (record && b.tick % 6 === 0) frames.push(b.snapshot());
  }
  if (record && b.tick % 6 !== 0) frames.push(b.snapshot());
  return { result: b.result, frames };
};
const share = (plan, types) =>
  plan.units
    .filter((u) => types.includes(u.type))
    .reduce((s, u) => s + UNITS[u.type].cost, 0) /
  Math.max(
    1,
    plan.units.reduce((s, u) => s + UNITS[u.type].cost, 0),
  );

test("counter scouting ignores live HP and analyses public armor, rush, clustering and spread", () => {
  const army = [
    unit("shield", 5),
    unit("giant", 6, 2),
    unit("archer", 10),
    unit("medic", 11, 1),
    unit("crossbow", 12, 2),
  ];
  const a = scoutArmy(army),
    b = scoutArmy(
      army.map((u) => ({ ...u, hp: 0, alive: false, future: { x: 99 } })),
    );
  assert.deepEqual(a, b);
  assert.equal(a.heavyShare, 0.4);
  assert.ok(a.clustered);
  assert.equal(a.spread, 2);
});
test("counter improves armor penetration, backline assault and rush defense over multiple seeds", () => {
  const empty = [],
    armor = Array.from({ length: 8 }, (_, i) =>
      unit(i % 2 ? "giant" : "shield", 5 + (i % 2), i - 4),
    ),
    cluster = Array.from({ length: 8 }, (_, i) =>
      unit(i % 2 ? "archer" : "medic", 10 + (i % 2), i % 3),
    ),
    rush = Array.from({ length: 8 }, (_, i) => unit("cavalry", 5, i - 4));
  let anti = 0,
    normalAnti = 0,
    mobile = 0,
    normalMobile = 0,
    spears = 0,
    normalSpears = 0;
  for (let seed = 1; seed <= 12; seed++) {
    const base = {
      team: "red",
      map: "meadow",
      budget: 4000,
      seed,
      style: "counter",
    };
    const normal = planArmy({ ...base, opponent: empty }),
      a = planArmy({ ...base, opponent: armor }),
      c = planArmy({ ...base, opponent: cluster }),
      r = planArmy({ ...base, opponent: rush });
    anti += share(a, ["crossbow", "bomber", "catapult"]);
    normalAnti += share(normal, ["crossbow", "bomber", "catapult"]);
    mobile += share(c, ["cavalry", "militia"]);
    normalMobile += share(normal, ["cavalry", "militia"]);
    spears += share(r, ["spear"]);
    normalSpears += share(normal, ["spear"]);
    assert.ok(r.props.some((p) => p.type === "barricade"));
    assert.ok(
      c.units.some(
        (u) => ["cavalry", "militia"].includes(u.type) && u.stance === "hunt",
      ),
    );
  }
  assert.ok(anti > normalAnti);
  assert.ok(mobile > normalMobile);
  assert.ok(spears > normalSpears);
});
test("spatial scouting and prior casualties alter legal seeded layouts without hidden information", () => {
  const c = fixture(false),
    report = {
      units: [
        {
          id: 1,
          team: "red",
          type: "shield",
          alive: false,
          damageDealt: 0,
          healing: 0,
          spawn: { x: -5, z: 7 },
        },
        {
          id: 4,
          team: "blue",
          type: "cavalry",
          alive: true,
          damageDealt: 500,
          healing: 0,
        },
      ],
    };
  const previous = { config: c, result: { report } },
    args = {
      map: "ruins",
      team: "red",
      style: "counter",
      budget: 2200,
      seed: 42,
      opponent: c.blue,
    };
  assert.deepEqual(reviewBattle(previous, "red"), {
    mvpType: "cavalry",
    lossLane: 7,
  });
  assert.notDeepEqual(
    planArmy(args).units,
    planArmy({ ...args, previous }).units,
  );
  assert.deepEqual(
    planArmy({ ...args, previous }),
    planArmy({ ...args, previous }),
  );
  assert.notDeepEqual(
    planArmy(args).units,
    planArmy({ ...args, seed: 43 }).units,
  );
});
test("Bo3 records public formations and reports, stops at two wins and preserves independent history", () => {
  const c = fixture(false),
    series = new BattleSeries(c),
    b = new Battle(c);
  let r;
  try {
    r = run(b).result;
  } finally {
    b.dispose();
  }
  series.record(c, { ...r, winner: "red" });
  assert.equal(series.complete, false);
  assert.throws(() => series.record(c, r));
  const next = series.nextConfig();
  assert.equal(next.seed, c.seed + 1);
  next.red[0].x = -15;
  assert.equal(series.last.config.red[0].x, -6);
  series.record(next, { ...r, winner: "red" });
  assert.equal(series.winner, "red");
  assert.equal(series.rounds.length, 2);
  assert.throws(() => series.nextConfig());
  assert.throws(() => series.record({ ...next, seed: 44 }, r));
});
test("Bo3 has three-game cap, deterministic draws and fixed map/budget/commander rules", () => {
  const c = fixture(false),
    b = new Battle(c);
  let r;
  try {
    r = run(b).result;
  } finally {
    b.dispose();
  }
  const series = new BattleSeries(c);
  assert.throws(() => series.record({ ...c, budget: 9000 }, r));
  assert.throws(() =>
    series.record({ ...c, commander: { enabled: true, aiTeams: [] } }, r),
  );
  for (let i = 0; i < 3; i++)
    series.record({ ...c, seed: c.seed + i }, { ...r, winner: "draw" });
  assert.equal(series.complete, true);
  assert.equal(series.winner, "draw");
  const cli = JSON.parse(
    execFileSync(
      process.execPath,
      [
        "scripts/battle.mjs",
        "--config",
        "examples/bridge-battle.json",
        "--bo3",
      ],
      {
        cwd: new URL("..", import.meta.url),
        encoding: "utf8",
        maxBuffer: 5 * 1024 * 1024,
      },
    ),
  );
  for (const round of cli.series.rounds) {
    assert.deepEqual(round.config.red, cli.config.red);
    assert.deepEqual(round.config.blue, cli.config.blue);
  }
});
test("orders use identical point/cooldown validation for red and blue and reject malformed commands", () => {
  const b = new Battle(fixture());
  try {
    for (const team of ["red", "blue"]) {
      const x = team === "red" ? -6 : 6;
      assert.equal(b.issueOrder(team, { type: "inspire", x, z: 0 }).ok, true);
      assert.equal(b.orderState.points[team], 2);
      assert.equal(b.issueOrder(team, { type: "fortify", x, z: 0 }).ok, false);
    }
    for (const o of [
      { type: "constructor", x: 0, z: 0 },
      { type: "focus", x: NaN, z: 0 },
      { type: "focus", x: 99, z: 0 },
    ])
      assert.equal(b.issueOrder("red", o).ok, false);
    for (let i = 0; i < ORDER_COOLDOWN; i++) b.step();
    assert.equal(
      b.issueOrder("red", { type: "fortify", x: -6, z: 0 }).ok,
      true,
    );
    for (let i = 0; i < ORDER_COOLDOWN; i++) b.step();
    assert.equal(b.issueOrder("red", { type: "focus", x: -6, z: 0 }).ok, true);
    for (let i = 0; i < ORDER_COOLDOWN; i++) b.step();
    assert.equal(
      b.issueOrder("red", { type: "inspire", x: -6, z: 0 }).ok,
      false,
    );
  } finally {
    b.dispose();
  }
  const off = new Battle(fixture(false));
  try {
    assert.equal(
      off.issueOrder("red", { type: "inspire", x: -6, z: 0 }).ok,
      false,
    );
  } finally {
    off.dispose();
  }
});
test("inspiration is local and temporary; fortify reduces front-line damage and physical knockback", () => {
  const b = new Battle(fixture());
  try {
    b.issueOrder("red", { type: "inspire", x: -6, z: 0 });
    assert.equal(b.units[0].inspireUntil, 300);
    assert.equal(b.units[3].inspireUntil, 0);
    for (let i = 0; i < 301; i++) b.step();
    assert.equal(b.snapshot().units[0].inspired, false);
  } finally {
    b.dispose();
  }
  const damage = [];
  for (const fortified of [false, true]) {
    const c = fixture(),
      sim = new Battle(c);
    try {
      if (fortified) sim.issueOrder("red", { type: "fortify", x: -6, z: 0 });
      const target = sim.units[1];
      sim.hit(target, 40, sim.units[3], 10);
      damage.push([
        target.maxHp - target.hp,
        Math.hypot(target.body.linvel().x, target.body.linvel().z),
      ]);
    } finally {
      sim.dispose();
    }
  }
  assert.equal(damage[1][0], damage[0][0] * 0.75);
  assert.ok(damage[1][1] < damage[0][1]);
});
test("focus orders target enemies in the selected region without altering other teams", () => {
  const c = fixture();
  c.blue = [unit("militia", 3), unit("catapult", 6.8, 3)];
  c.red = [unit("archer", -3)];
  const b = new Battle(c);
  try {
    const archer = b.units[0];
    assert.equal(b.target(archer).type, "militia");
    assert.ok(b.issueOrder("red", { type: "focus", x: 1, z: 0 }).ok);
    assert.ok(archer.focus);
    assert.equal(b.target(archer).type, "militia");
    b.orderState.readyAt.red = 0;
    b.issueOrder("red", { type: "focus", x: 3.5, z: 4.2 });
    assert.equal(b.target(archer).type, "catapult");
    assert.equal(b.units[1].focus, null);
  } finally {
    b.dispose();
  }
});
test("AI commander and recorded manual orders reproduce identical transforms, stats, effects and snapshots", async () => {
  const c = fixture();
  c.commander.aiTeams = ["blue"];
  const b = new Battle(c);
  let first;
  try {
    const frames = [b.snapshot()];
    for (let i = 0; i < 103; i++) {
      b.step();
      if (b.tick % 6 === 0) frames.push(b.snapshot());
    }
    assert.ok(b.issueOrder("red", { type: "inspire", x: -6, z: 0 }).ok);
    while (!b.result) {
      b.step();
      if (b.tick % 6 === 0) frames.push(b.snapshot());
    }
    if (b.tick % 6 !== 0) frames.push(b.snapshot());
    first = { frames, result: b.result };
  } finally {
    b.dispose();
  }
  const seed = validateReplay(
      replayPackage(c, first.frames, first.result, "seed"),
    ),
    sim = await createReplayBattle(seed);
  try {
    assert.deepEqual(run(sim, true), first);
  } finally {
    sim.dispose();
  }
  const trajectory = replayPackage(c, first.frames, first.result);
  assert.ok(trajectory.orderLog.some((o) => o.team === "blue"));
  assert.deepEqual(
    validateReplay(JSON.parse(JSON.stringify(trajectory))),
    trajectory,
  );
});
test("replay rejects overspent orders, illegal cooldowns, forged commander state and future ticks", () => {
  const c = fixture(),
    b = new Battle(c);
  let r;
  try {
    b.issueOrder("red", { type: "inspire", x: -6, z: 0 });
    r = run(b, true);
  } finally {
    b.dispose();
  }
  const pkg = replayPackage(c, r.frames, r.result);
  for (const change of [
    (p) => p.orderLog.push({ ...p.orderLog[0], id: 2, tick: 2 }),
    (p) => (p.frames.at(-1).commander.points.red = 99),
    (p) => (p.orderLog[0].type = "__proto__"),
    (p) => (p.orderLog[0].tick = 99999),
  ]) {
    const clone = structuredClone(pkg);
    change(clone);
    assert.throws(() => validateReplay(clone));
  }
});
test("real-time combat power exactly agrees with timeout scoring and ignores props/dead units", () => {
  const c = fixture(false),
    b = new Battle(c);
  try {
    b.units[0].hp = 110;
    assert.equal(combatPower(b.snapshot().units).red, 45 + 75 + 160);
    run(b);
    assert.deepEqual(
      b.result.remainingValue,
      Object.fromEntries(
        Object.entries(combatPower(b.snapshot().units)).map(([k, v]) => [
          k,
          Math.round(v * 1000) / 1000,
        ]),
      ),
    );
  } finally {
    b.dispose();
  }
});
test("legacy 0.1/0.2/0.3 seed simulators and full trajectories remain importable", async () => {
  for (const version of ["0.1.0", "0.2.0", "0.3.0"]) {
    const config = {
      version,
      map: "meadow",
      seed: 42,
      budget: 1500,
      red: [unit("militia", -3, 0, "advance")],
      blue: [unit("militia", 3, 0, "advance")],
    };
    const seed = validateReplay({
        format: "frontline-replay",
        version: 1,
        engineVersion: version,
        kind: "seed",
        config,
      }),
      b = await createReplayBattle(seed);
    let old;
    try {
      old = run(b, true);
    } finally {
      b.dispose();
    }
    const migrated = validateReplay({
      format: "frontline-replay",
      version: 1,
      engineVersion: version,
      kind: "trajectory",
      config,
      ...old,
    });
    assert.equal(migrated.result.winner, old.result.winner);
    assert.equal(migrated.frames.length, old.frames.length);
    assert.equal(migrated.engineVersion, version);
  }
});
test("five favorite slots validate old data, mirror teams and reject over-budget trial loads", () => {
  const c = fixture(false),
    slots = saveFavorite([], 4, "我的盾阵", c, "red");
  assert.equal(slots.length, 5);
  assert.equal(
    readFavorites(JSON.parse(JSON.stringify(slots)))[4].name,
    "我的盾阵",
  );
  const loaded = loadFavorite({ ...c, blue: [] }, "blue", slots[4]);
  assert.equal(loaded.blue[0].x, 6);
  assert.equal(loaded.blue[0].stance, "hold");
  assert.throws(() => loadFavorite(c, "red", slots[4], 100));
  assert.throws(() => loadFavorite({ ...c, map: "ruins" }, "red", slots[4]));
  assert.throws(() => readFavorites(JSON.parse('[{"__proto__":{}}]')));
});
test("procedural audio stays lazy, throttles same-frame voices and respects mute/volume", async () => {
  let created = 0;
  const sources = [];
  class Context {
    constructor() {
      created++;
      this.currentTime = 0;
      this.state = "suspended";
      this.destination = {};
    }
    createGain() {
      return {
        gain: {
          setTargetAtTime() {},
          setValueAtTime() {},
          exponentialRampToValueAtTime() {},
        },
        connect() {},
        disconnect() {},
      };
    }
    createDynamicsCompressor() {
      return { connect() {} };
    }
    createOscillator() {
      const source = {
        frequency: { setValueAtTime() {}, exponentialRampToValueAtTime() {} },
        connect() {},
        disconnect() {},
        start() {},
        stop() {},
      };
      sources.push(source);
      return source;
    }
    async resume() {
      this.state = "running";
    }
  }
  globalThis.AudioContext = Context;
  try {
    const a = new BattleAudio();
    assert.equal(created, 0);
    a.play("start");
    assert.equal(created, 0);
    await a.unlock();
    assert.equal(created, 1);
    for (const type of ["bow", "melee", "block", "slam", "heal", "explosion"])
      a.play(type);
    assert.equal(sources.length, 4);
    a.setMuted(true);
    a.play("start");
    assert.equal(sources.length, 4);
    a.setVolume(2);
    assert.equal(a.volume, 1);
    for (const s of sources) s.onended();
    assert.equal(a.active, 0);
  } finally {
    delete globalThis.AudioContext;
  }
});
