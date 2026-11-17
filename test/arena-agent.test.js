import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { Battle, initPhysics } from "../src/engine.js";
import {
  VERSION,
  validateConfig,
  defaultConfig,
  canPlace,
  combatPower,
} from "../src/data.js";
import {
  planOracle,
  planArmyAsync,
  aiDuel,
  aiDuelAsync,
} from "../src/tactics.js";
import {
  normalizeEntrant,
  summarizeTournament,
  runTournament,
} from "../src/tournament.js";
import {
  observeBattle,
  acceptAgentCommand,
  AGENT_SCHEMA,
} from "../src/agent-protocol.js";
import { StdioAgent, runAgentSeries } from "../scripts/agent-bridge.mjs";
import {
  validateReplay,
  replayPackage,
  createReplayBattle,
} from "../src/replay.js";
import { validateHighlights, highlightFrameIndex } from "../src/highlights.js";
import { diagnoseFormation } from "../src/diagnostics.js";
import { shortcutAction } from "../src/ui/shortcuts.js";
await initPhysics();
const config = () => ({
  ...defaultConfig(),
  budget: 500,
  red: [{ type: "shield", x: -4, z: 0 }],
  blue: [{ type: "spear", x: 4, z: 0 }],
  props: [],
  commander: { enabled: true, aiTeams: [] },
});
test("oracle uses bounded public trials and selects the best deterministic finalist with a legal deployment", () => {
  const options = {
    team: "red",
    map: "ruins",
    budget: 500,
    seed: 117,
    opponent: [{ type: "shield", x: 5, z: 0 }],
    candidateCount: 3,
    screenSteps: 60,
    finalSteps: 180,
  };
  const a = planOracle(options),
    b = planOracle({
      ...options,
      opponent: [
        { ...options.opponent[0], hp: -123, alive: false, future: "ignored" },
      ],
    });
  assert.deepEqual(a, b);
  assert.equal(a.style, "oracle");
  assert.equal(a.search.maxSteps, 540);
  assert.ok(a.search.finalists[0].score >= a.search.finalists[1].score);
  assert.equal(a.search.chosen, a.search.finalists[0].index);
  const c = { ...config(), map: "ruins", red: [], blue: [], props: [] };
  for (const u of a.units) {
    assert.ok(canPlace(c, "red", u));
    c.red.push(u);
  }
  for (const p of a.props) {
    assert.ok(canPlace(c, "red", p, "prop"));
    c.props.push(p);
  }
  validateConfig(c);
  assert.throws(() => planOracle({ ...options, finalSteps: 5401 }));
});
test("oracle async slices match synchronous plans and cancellation disposes the trial", async () => {
  const options = {
      style: "oracle",
      team: "blue",
      map: "meadow",
      budget: 320,
      seed: 8,
      candidateCount: 3,
      screenSteps: 60,
      finalSteps: 120,
    },
    progress = [];
  assert.deepEqual(
    await planArmyAsync(options, { onProgress: (p) => progress.push(p) }),
    planOracle(options),
  );
  assert.ok(progress.length > 1);
  const controller = new AbortController();
  await assert.rejects(
    planArmyAsync(options, {
      signal: controller.signal,
      onProgress: () => controller.abort(),
    }),
    /取消/,
  );
});
test("sync and sliced dual AI share the same public draft policy", async () => {
  const c = { ...config(), budget: 100 };
  assert.deepEqual(
    await aiDuelAsync(c, "oracle", "guardian"),
    aiDuel(c, "oracle", "guardian"),
  );
});
test("round robin matrix counts perspective wins, draws, residual power, time and stable type MVP", () => {
  const e = [
    { name: "A", ai: "guardian" },
    { name: "B", ai: "counter" },
  ].map(normalizeEntrant);
  const result = (winner, red, blue, time) => ({
    winner,
    remainingValue: { red, blue },
    time,
    report: {
      teams: {
        red: { byType: { shield: { damageDealt: 100, healing: 0 } } },
        blue: { byType: { spear: { damageDealt: 50, healing: 0 } } },
      },
    },
  });
  const r = summarizeTournament(e, [
    { red: 0, blue: 1, result: result("red", 100, 0, 20) },
    { red: 1, blue: 0, result: result("draw", 40, 40, 90) },
    { red: 0, blue: 1, result: result("blue", 0, 70, 30) },
  ]);
  for (const row of r.ranking) {
    assert.equal(row.wins, 1);
    assert.equal(row.draws, 1);
    assert.equal(row.losses, 1);
    assert.equal(row.scoreRate, 0.5);
    assert.equal(row.winRate, 1 / 3);
    assert.equal(row.averageTime, 140 / 3);
  }
  assert.equal(r.matrix[0][1].averageRemaining, 140 / 3);
  assert.equal(r.matrix[1][0].averageRemaining, 110 / 3);
  assert.equal(r.matrix[0][1].stableMvp, "shield");
  assert.equal(r.matrix[0][0].games, 0);
  assert.throws(() =>
    summarizeTournament(e, [
      { red: 0, blue: 0, result: result("draw", 0, 0, 0) },
    ]),
  );
});
test("tournament runs both colors, rejects invalid schedules, and supports fixed mirrored JSON rosters", async () => {
  const c = config(),
    entrants = [
      { name: "JSON", units: c.red, team: "red", map: "meadow" },
      { name: "guard", ai: "guardian" },
    ],
    r = await runTournament({
      entrants,
      budget: 500,
      maps: ["meadow"],
      seeds: [4],
    });
  assert.equal(r.matches.length, 2);
  assert.deepEqual(
    r.matches.map((m) => [m.red, m.blue]),
    [
      [0, 1],
      [1, 0],
    ],
  );
  assert.equal(r.ranking[0].games, 2);
  await assert.rejects(
    runTournament({ entrants, maps: ["ruins"], seeds: [4] }),
    /只适用/,
  );
  await assert.rejects(runTournament({ entrants, seeds: [4, 4] }));
});
test("Agent observation exposes public live state without RNG, hidden configuration, target or physics internals", () => {
  const b = new Battle(config());
  try {
    const o = observeBattle(b, "red", "1:0:red");
    assert.equal(o.tick, 0);
    assert.equal(o.units.length, 2);
    assert.deepEqual(o.power, combatPower(b.units));
    assert.deepEqual(Object.keys(o.units[0]).sort(), [
      "hp",
      "id",
      "maxHp",
      "team",
      "type",
      "x",
      "y",
      "z",
    ]);
    for (const key of ["config", "rng", "orderLog", "targets", "bodies"])
      assert.equal(o[key], undefined);
    o.commander.points.red = 0;
    assert.equal(b.orderState.points.red, 3);
  } finally {
    b.dispose();
  }
});
test("Agent boundary rejects cross-team, stale IDs, bad coordinates, mutation fields and cooldown without consuming points", () => {
  const b = new Battle(config()),
    reply = () => ({
      schema: AGENT_SCHEMA,
      kind: "command",
      requestId: "q",
      team: "red",
      order: { type: "inspire", x: -4, z: 0 },
    });
  try {
    for (const mutate of [
      (r) => (r.team = "blue"),
      (r) => (r.requestId = "stale"),
      (r) => (r.order.x = 999),
      (r) => (r.order.z = NaN),
      (r) => (r.order.hp = 999),
      (r) => (r.config = {}),
    ]) {
      const r = reply();
      mutate(r);
      assert.equal(acceptAgentCommand(b, "red", "q", r).ok, false);
      assert.equal(b.orderState.points.red, 3);
    }
    assert.equal(acceptAgentCommand(b, "red", "q", reply()).ok, true);
    assert.equal(acceptAgentCommand(b, "red", "q", reply()).ok, false);
    assert.equal(b.orderState.points.red, 2);
  } finally {
    b.dispose();
  }
});
test("stdio timeouts degrade to skipped action and late responses cannot satisfy a newer request", async () => {
  const program = `process.stdin.setEncoding('utf8');let buf='';process.stdin.on('data',s=>{buf+=s;let i;while((i=buf.indexOf('\\n'))>=0){const o=JSON.parse(buf.slice(0,i));buf=buf.slice(i+1);setTimeout(()=>console.log(JSON.stringify({requestId:o.requestId})),o.delay);}});`;
  const agent = new StdioAgent([process.execPath, "-e", program]);
  try {
    assert.equal(
      (await agent.request({ requestId: "old", delay: 100 }, 30)).status,
      "timeout",
    );
    const reply = await agent.request({ requestId: "new", delay: 150 }, 1000);
    assert.equal(reply.response.requestId, "new");
  } finally {
    await agent.close();
  }
});
test("two external stdio bots finish Bo3 and produce valid v2 replays reproducible with the recorded commands", async () => {
  const bot = [
      process.execPath,
      fileURLToPath(new URL("../scripts/bots/minimal.mjs", import.meta.url)),
    ],
    { report, replays } = await runAgentSeries({
      config: config(),
      commands: { red: bot, blue: bot },
      timeoutMs: 1000,
    });
  assert.ok(
    report.series.rounds.length >= 2 && report.series.rounds.length <= 3,
  );
  assert.ok(report.diagnostics.some((d) => d.entry && d.team === "red"));
  assert.ok(report.diagnostics.some((d) => d.entry && d.team === "blue"));
  assert.equal(
    report.diagnostics.filter((d) => d.status === "timeout").length,
    0,
  );
  for (const replay of replays) {
    const p = validateReplay(replay);
    assert.equal(p.version, 2);
    const b = await createReplayBattle({ ...p, kind: "seed" });
    try {
      while (!b.result) b.step();
      assert.deepEqual(b.result, p.result);
      assert.deepEqual(b.snapshot().units, p.frames.at(-1).units);
    } finally {
      b.dispose();
    }
  }
});
test("highlights remain bounded deterministic sparse events and markers align with the first recorded frame after each tick", () => {
  const c = config(),
    b = new Battle(c),
    frames = [b.snapshot()];
  try {
    b.issueOrder("red", { type: "inspire", x: -4, z: 0 });
    while (!b.result) {
      b.step();
      if (b.tick % 6 === 0) frames.push(b.snapshot());
    }
    if (b.tick % 6) frames.push(b.snapshot());
    const p = replayPackage(c, frames, b.result);
    assert.ok(p.highlights.some((e) => e.type === "order"));
    assert.ok(p.highlights.some((e) => e.type === "firstKill"));
    assert.ok(p.highlights.length <= 24);
    for (const e of p.highlights) {
      const i = highlightFrameIndex(frames, e.tick);
      assert.ok(Math.round(frames[i].time * 60) >= e.tick);
      if (i > 0) assert.ok(Math.round(frames[i - 1].time * 60) <= e.tick);
    }
    const bad = structuredClone(p);
    bad.highlights[0].tick = 5401;
    assert.throws(() => validateReplay(bad));
    assert.throws(() => validateHighlights(Array(25).fill({})));
    for (let i = 0; i < 40; i++) {
      b.tick += 180;
      b.highlight("blast", "red", 200);
    }
    assert.equal(b.highlights.length, 24);
  } finally {
    b.dispose();
  }
});
test("v0.4 seed and trajectory files preserve v2 commands and use the frozen v0.4 simulator", async () => {
  const { Battle: Old, initPhysics: oldInit } = await import(
    "../src/legacy/v0.4/engine.js"
  );
  await oldInit();
  const c = { ...config(), version: "0.4.0" },
    old = new Old(c),
    frames = [old.snapshot()];
  let result;
  try {
    old.issueOrder("red", { type: "inspire", x: -4, z: 0 });
    while (!old.result) {
      old.step();
      if (old.tick % 6 === 0) frames.push(old.snapshot());
    }
    if (old.tick % 6) frames.push(old.snapshot());
    result = structuredClone(old.result);
  } finally {
    old.dispose();
  }
  const p = validateReplay({
    format: "frontline-replay",
    version: 2,
    engineVersion: "0.4.0",
    kind: "trajectory",
    config: c,
    frames,
    result,
    orderLog: result.orders,
  });
  assert.equal(p.engineVersion, "0.4.0");
  const b = await createReplayBattle(p);
  try {
    assert.ok(b instanceof Old);
    while (!b.result) b.step();
    assert.deepEqual(b.result, result);
    assert.deepEqual(b.snapshot().units, frames.at(-1).units);
  } finally {
    b.dispose();
  }
});
test("diagnostics only use the passed visible roster and do not infer a sealed enemy", () => {
  const c = config();
  c.red = [{ type: "archer", x: -5, z: 0 }];
  assert.match(diagnoseFormation(c, "red"), /前排/);
  c.red = [{ type: "shield", x: -5, z: 0 }];
  c.blue = [
    { type: "cavalry", x: 6, z: 0 },
    { type: "cavalry", x: 7, z: 2 },
  ];
  assert.match(diagnoseFormation(c, "red"), /骑兵/);
  assert.doesNotMatch(diagnoseFormation({ ...c, blue: [] }, "red"), /骑兵/);
});
test("UI modules have no circular relative imports and main remains a coordinator under 600 lines", async () => {
  const visited = new Set(),
    active = new Set();
  async function visit(url) {
    if (active.has(url.href)) throw Error("circular " + url.href);
    if (visited.has(url.href)) return;
    active.add(url.href);
    const text = await fs.readFile(url, "utf8");
    for (const m of text.matchAll(
      /(?:from\s*|import\s*\()\s*['"](\.[^'"]+\.js)['"]/g,
    ))
      await visit(new URL(m[1], url));
    active.delete(url.href);
    visited.add(url.href);
  }
  await visit(new URL("../src/main.js", import.meta.url));
  assert.ok(
    (
      await fs.readFile(new URL("../src/main.js", import.meta.url), "utf8")
    ).split("\n").length < 600,
  );
});

test("shortcuts ignore modal input, editable content and auto-repeat while supporting Cmd/Ctrl undo and all commands", () => {
  const event = (key, code = key) => ({ key, code });
  assert.equal(
    shortcutAction(event(" ", "Space"), { tagName: "BODY" }),
    "pause",
  );
  assert.equal(shortcutAction(event("Tab"), { tagName: "BUTTON" }), "camera");
  assert.deepEqual(
    ["1", "2", "3"].map((k) => shortcutAction(event(k), { tagName: "BODY" })),
    ["inspire", "focus", "fortify"],
  );
  for (const modifier of ["ctrlKey", "metaKey"])
    assert.equal(
      shortcutAction({ ...event("z"), [modifier]: true }, { tagName: "BODY" }),
      "undo",
    );
  for (const tagName of ["INPUT", "TEXTAREA", "SELECT"])
    assert.equal(shortcutAction(event("1"), { tagName }), null);
  assert.equal(shortcutAction(event("1"), { isContentEditable: true }), null);
  assert.equal(
    shortcutAction(
      { ...event(" ", "Space"), repeat: true },
      { tagName: "BODY" },
    ),
    null,
  );
  assert.equal(shortcutAction(event("1"), { tagName: "BODY" }, true), null);
});

test("v0.5 metadata never changes v0.4 physics transforms or RNG across all six maps", async () => {
  const { Battle: Old, initPhysics: oldInit } = await import(
    "../src/legacy/v0.4/engine.js"
  );
  await oldInit();
  for (const map of [
    "meadow",
    "bridge",
    "island",
    "ruins",
    "marsh",
    "highland",
  ]) {
    const c = {
        ...defaultConfig(map),
        version: "0.4.0",
        seed: 42,
        commander: { enabled: true, aiTeams: ["red", "blue"] },
      },
      a = new Old(c),
      b = new Battle(c);
    try {
      for (let i = 0; i < 720; i++) {
        a.step();
        b.step();
      }
      assert.equal(b.rng, a.rng);
      for (const key of [
        "units",
        "projectiles",
        "ragdolls",
        "commander",
        "cues",
      ])
        assert.deepEqual(b.snapshot()[key], a.snapshot()[key]);
    } finally {
      a.dispose();
      b.dispose();
    }
  }
});
