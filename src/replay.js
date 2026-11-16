import {
  VERSION,
  MAX_TIME,
  UNITS,
  PROPS,
  MAPS,
  assertSafeData,
  validateConfig,
  validateOrderLog,
  STANCES,
  ORDERS,
  ORDER_COOLDOWN,
} from "./data.js";
import {
  Battle as CurrentBattle,
  initPhysics as initCurrentPhysics,
} from "./engine.js";

const keys = [
  "damageDealt",
  "damageTaken",
  "healing",
  "kills",
  "falls",
  "fallKills",
  "friendlyFire",
  "friendlyKills",
];
const fail = () => {
  throw Error("回放包字段、轨迹或统计无效");
};
const finite = (n, min = -100000, max = 100000) =>
  typeof n === "number" && Number.isFinite(n) && n >= min && n <= max;
const id = (n) => Number.isInteger(n) && n > 0 && n < 10000000;
const transform = (t) =>
  Array.isArray(t) &&
  t.length === 7 &&
  t.every((n) => finite(n)) &&
  Math.abs(Math.hypot(...t.slice(3)) - 1) < 0.02;
const team = (t) => ["red", "blue", "neutral"].includes(t);
const counters = (r) => r && keys.every((k) => finite(r[k], 0, 1e9));

function validResult(r, config) {
  if (
    !r ||
    !["red", "blue", "draw"].includes(r.winner) ||
    !["timeout", "elimination"].includes(r.reason) ||
    !finite(r.time, 0, MAX_TIME + 0.02) ||
    !Number.isInteger(r.red) ||
    r.red < 0 ||
    r.red > config.red.length ||
    !Number.isInteger(r.blue) ||
    r.blue < 0 ||
    r.blue > config.blue.length ||
    !r.remainingValue ||
    !["red", "blue"].every((t) => finite(r.remainingValue[t], 0, 10000))
  )
    return false;
  const report = r.report;
  if (
    !report ||
    !Array.isArray(report.units) ||
    report.units.length !== config.red.length + config.blue.length ||
    !Array.isArray(report.props) ||
    report.props.length > 40
  )
    return false;
  const roster = [
    ...config.red.map((u) => ({ ...u, team: "red" })),
    ...config.blue.map((u) => ({ ...u, team: "blue" })),
  ];
  if (
    !report.units.every(
      (u, i) =>
        u.id === i + 1 &&
        u.team === roster[i].team &&
        u.type === roster[i].type &&
        typeof u.alive === "boolean" &&
        finite(u.hp, 0, UNITS[u.type].hp) &&
        counters(u),
    )
  )
    return false;
  for (const p of report.props)
    if (
      !id(p.id) ||
      !team(p.team) ||
      !Object.hasOwn(PROPS, p.type) ||
      !counters(p) ||
      typeof p.alive !== "boolean"
    )
      return false;
  for (const t of ["red", "blue"]) {
    const s = report.teams?.[t];
    if (
      !s ||
      !counters(s) ||
      !finite(s.remainingValue, 0, 10000) ||
      !["initial", "survived", "lost"].every(
        (k) => Number.isInteger(s[k]) && s[k] >= 0 && s[k] <= 60,
      ) ||
      !s.byType
    )
      return false;
    for (const [type, row] of Object.entries(s.byType))
      if (
        !Object.hasOwn(UNITS, type) ||
        !counters(row) ||
        !["initial", "survived", "lost"].every(
          (k) => Number.isInteger(row[k]) && row[k] >= 0 && row[k] <= 60,
        )
      )
        return false;
  }
  return (
    report.mvp === null ||
    (report.mvp &&
      report.units.some(
        (u) =>
          u.id === report.mvp.id &&
          u.type === report.mvp.type &&
          u.team === report.mvp.team,
      ) &&
      counters(report.mvp) &&
      finite(report.mvp.score, -1e9, 1e9))
  );
}

/** Strict, bounded plain-JSON boundary. Trajectory playback never runs imported code. */
export function validateReplay(input, legacy = false) {
  assertSafeData(input);
  if (
    input?.version === 1 &&
    ["0.1.0", "0.2.0", "0.3.0"].includes(input.engineVersion)
  )
    return validateLegacy(input);
  if (
    !input ||
    input.format !== "frontline-replay" ||
    input.version !== 2 ||
    input.engineVersion !== VERSION ||
    !["seed", "trajectory"].includes(input.kind)
  )
    throw Error("不支持的回放格式或引擎版本");
  const config = validateConfig(input.config);
  const orderLog = validateOrderLog(input.orderLog || [], config);
  if (input.kind === "seed") {
    if (
      input.frames !== undefined &&
      (!Array.isArray(input.frames) || input.frames.length)
    )
      fail();
    return {
      format: input.format,
      version: 2,
      engineVersion: VERSION,
      kind: "seed",
      config,
      orderLog,
    };
  }
  if (
    !validResult(input.result, config) ||
    !Array.isArray(input.frames) ||
    input.frames.length < 2 ||
    input.frames.length > 902
  )
    fail();
  const roster = [
    ...config.red.map((u) => ({ ...u, team: "red" })),
    ...config.blue.map((u) => ({ ...u, team: "blue" })),
  ];
  let last = -1;
  const frames = input.frames.map((f, index) => {
    if (
      !f ||
      !finite(f.time, 0, MAX_TIME + 0.02) ||
      f.time <= last ||
      (index === 0 && f.time !== 0) ||
      (index > 0 &&
        index < input.frames.length - 1 &&
        Math.abs(f.time - last - 0.1) > 0.00001) ||
      !Array.isArray(f.units) ||
      f.units.length !== roster.length ||
      !Array.isArray(f.ragdolls) ||
      f.ragdolls.length > 24 ||
      !Array.isArray(f.projectiles) ||
      f.projectiles.length > 1024 ||
      !Array.isArray(f.effects) ||
      f.effects.length > 41 ||
      !Array.isArray(f.props) ||
      (legacy
        ? f.props.length > 40
        : f.props.length !==
          MAPS[config.map].props.length + config.props.length)
    )
      fail();
    last = f.time;
    if (
      !Number.isInteger(f.tick) ||
      Math.abs(f.tick / 60 - f.time) > 0.00001 ||
      !validCommander(f.commander, orderLog, f.tick) ||
      !Array.isArray(f.cues) ||
      f.cues.length > 24
    )
      fail();
    for (const c of f.cues)
      if (
        !id(c.id) ||
        !Number.isInteger(c.tick) ||
        c.tick < 0 ||
        c.tick > f.tick ||
        ![
          "bow",
          "crossbow",
          "melee",
          "block",
          "slam",
          "charge",
          "catapult",
          "explosion",
          "heal",
          "order",
          "bomb",
        ].includes(c.type) ||
        ![c.x, c.y, c.z].every((n) => finite(n)) ||
        !finite(c.strength, 0, 1)
      )
        fail();
    for (const [i, u] of f.units.entries())
      if (
        !Object.hasOwn(STANCES, u.stance) ||
        typeof u.inspired !== "boolean" ||
        typeof u.fortified !== "boolean" ||
        u.id !== i + 1 ||
        u.type !== roster[i].type ||
        u.team !== roster[i].team ||
        typeof u.alive !== "boolean" ||
        !finite(u.hp, 0, UNITS[u.type].hp) ||
        !finite(u.angle) ||
        !finite(u.attack, 0, 1) ||
        !finite(u.charge, 0, 1.5) ||
        (u.alive ? !transform(u.t) : u.t !== null)
      )
        fail();
    const dolls = new Set();
    for (const d of f.ragdolls) {
      const u = roster[d.id - 1];
      if (
        !u ||
        dolls.has(d.id) ||
        d.type !== u.type ||
        d.team !== u.team ||
        !finite(d.age, 0, 12.1) ||
        !Array.isArray(d.parts) ||
        d.parts.length !== 6
      )
        fail();
      dolls.add(d.id);
      if (
        d.wreck !== null &&
        (!Array.isArray(d.wreck) ||
          d.wreck.length !== 4 ||
          !d.wreck.every((n) => finite(n)))
      )
        fail();
      const names = d.parts.map((p) => p.name);
      if (
        new Set(names).size !== 6 ||
        !names.every((n) =>
          ["torso", "head", "armL", "armR", "legL", "legR"].includes(n),
        ) ||
        !d.parts.every((p) => transform(p.t))
      )
        fail();
    }
    for (const p of f.projectiles)
      if (
        !id(p.id) ||
        !["bomb", "stone", "arrow", "bolt"].includes(p.type) ||
        !transform(p.t) ||
        !p.v ||
        ![p.v.x, p.v.y, p.v.z].every((n) => finite(n))
      )
        fail();
    for (const e of f.effects)
      if (
        !id(e.id) ||
        ![
          "explosion",
          "slam",
          "heal",
          "charge",
          "spikes",
          "spring",
          "hit",
          "block",
          "order",
        ].includes(e.type) ||
        ![e.x, e.y, e.z].every((n) => finite(n)) ||
        !finite(e.maxLife, 0.01, 1) ||
        !finite(e.life, 0, e.maxLife)
      )
        fail();
    for (const p of f.props)
      if (
        !id(p.id) ||
        !Object.hasOwn(PROPS, p.type) ||
        !team(p.team) ||
        ![p.x, p.z, p.floor].every((n) => finite(n)) ||
        !finite(p.hp, 0, PROPS[p.type].hp) ||
        typeof p.alive !== "boolean" ||
        typeof p.active !== "boolean"
      )
        fail();
    return {
      ...f,
      result: index === input.frames.length - 1 ? input.result : null,
    };
  });
  const final = frames.at(-1);
  if (
    final.commander.log.length !== orderLog.length ||
    orderLog.some((o) => o.tick >= final.tick)
  )
    fail();
  if (
    Math.abs(final.time - input.result.time) > 0.00001 ||
    final.units.filter((u) => u.alive && u.team === "red").length !==
      input.result.red ||
    final.units.filter((u) => u.alive && u.team === "blue").length !==
      input.result.blue
  )
    fail();
  return structuredClone({
    format: input.format,
    version: 2,
    engineVersion: VERSION,
    kind: "trajectory",
    config,
    result: input.result,
    frames,
    orderLog,
  });
}

export function replayPackage(config, frames, result, kind = "trajectory") {
  return validateReplay({
    format: "frontline-replay",
    version: 2,
    engineVersion: VERSION,
    kind,
    config,
    frames: kind === "seed" ? undefined : frames,
    result: kind === "seed" ? undefined : result,
    orderLog: result?.orders || frames?.at(-1)?.commander?.log || [],
  });
}

function validCommander(state, log, tick) {
  if (
    !state ||
    !state.points ||
    !state.readyAt ||
    !Array.isArray(state.log) ||
    state.log.length > log.length ||
    !Array.isArray(state.active) ||
    state.active.length > 6
  )
    return false;
  const same = (a, b) =>
    ["id", "team", "type", "x", "z", "tick"].every((k) => a[k] === b[k]);
  if (!state.log.every((o, i) => same(o, log[i]) && o.tick <= tick))
    return false;
  for (const t of ["red", "blue"]) {
    const own = state.log.filter((o) => o.team === t);
    if (
      state.points[t] !== 3 - own.length ||
      state.readyAt[t] !==
        (own.at(-1)?.tick ?? -ORDER_COOLDOWN) + ORDER_COOLDOWN
    )
      return false;
  }
  return state.active.every(
    (o) =>
      state.log.some((e) => same(o, e)) &&
      o.endTick === o.tick + ORDERS[o.type].duration &&
      o.endTick >= tick,
  );
}

function validateLegacy(input) {
  const copy = structuredClone(input),
    version = copy.engineVersion;
  // Historical builds did not expose commander orders; no hidden rule can enter through a v1 package.
  if (copy.orderLog?.length || copy.config?.commander?.enabled) fail();
  copy.config = validateConfig(copy.config);
  copy.version = 2;
  copy.engineVersion = VERSION;
  copy.orderLog = [];
  if (copy.kind === "trajectory") {
    if (
      !Array.isArray(copy.frames) ||
      copy.frames.length < 2 ||
      copy.frames.length > 902 ||
      !copy.result
    )
      fail();
    const roster = [...copy.config.red, ...copy.config.blue];
    const metrics = {
      damageDealt: 0,
      damageTaken: 0,
      healing: 0,
      kills: 0,
      falls: 0,
      fallKills: 0,
      friendlyFire: 0,
      friendlyKills: 0,
    };
    for (const f of copy.frames) {
      if (
        !Array.isArray(f.units) ||
        f.units.length > 120 ||
        !Array.isArray(f.ragdolls) ||
        f.ragdolls.length > 24
      )
        fail();
      f.tick = Math.round(f.time * 60);
      f.commander = {
        points: { red: 3, blue: 3 },
        readyAt: { red: 0, blue: 0 },
        active: [],
        log: [],
      };
      f.cues = [];
      f.props ||= [];
      for (const [i, u] of (f.units || []).entries()) {
        u.charge ??= 0;
        u.stance = roster[i]?.stance || "advance";
        u.inspired = false;
        u.fortified = false;
      }
      for (const d of f.ragdolls || []) {
        d.age ??= 0;
        d.wreck ??= null;
      }
    }
    const final = copy.frames.at(-1);
    // v0.1/v0.2 predate portable replay/statistics. Accept their bounded snapshots and synthesize explicitly empty contributions.
    if (!copy.result.report) {
      const units = final.units.map((u) => ({
        ...metrics,
        id: u.id,
        type: u.type,
        team: u.team,
        alive: u.alive,
        hp: u.hp,
      }));
      const teams = {};
      for (const t of ["red", "blue"]) {
        const own = units.filter((u) => u.team === t),
          byType = {};
        for (const u of own) {
          const row = (byType[u.type] ||= {
            ...metrics,
            initial: 0,
            survived: 0,
            lost: 0,
          });
          row.initial++;
          row[u.alive ? "survived" : "lost"]++;
        }
        teams[t] = {
          ...metrics,
          initial: own.length,
          survived: own.filter((u) => u.alive).length,
          lost: own.filter((u) => !u.alive).length,
          remainingValue: own.reduce(
            (s, u) => s + (u.hp / UNITS[u.type].hp) * UNITS[u.type].cost,
            0,
          ),
          byType,
        };
      }
      copy.result.report = { teams, units, props: [], mvp: null };
      copy.result.legacyStatsUnavailable = true;
    }
    copy.result.remainingValue ||= {
      red: copy.result.report.teams.red.remainingValue,
      blue: copy.result.report.teams.blue.remainingValue,
    };
    copy.result.orders = [];
    // Old maps had no preset props. Validation uses a temporary marker rather than adding new props to old footage.
  }
  const normalized = validateReplay(copy, true);
  return {
    ...normalized,
    version: 1,
    engineVersion: version,
    legacyStatsUnavailable: copy.result?.legacyStatsUnavailable || false,
  };
}

export async function createReplayBattle(pkg) {
  let module;
  if (pkg.engineVersion === "0.1.0")
    module = await import("./legacy/v0.1/engine.js");
  else if (pkg.engineVersion === "0.2.0")
    module = await import("./legacy/v0.2/engine.js");
  else if (pkg.engineVersion === "0.3.0")
    module = await import("./legacy/v0.3/engine.js");
  else module = { Battle: CurrentBattle, initPhysics: initCurrentPhysics };
  await module.initPhysics();
  const battle = new module.Battle({
    ...pkg.config,
    version: pkg.engineVersion,
  });
  if (pkg.engineVersion === VERSION) battle.useOrderLog(pkg.orderLog || []);
  return battle;
}
