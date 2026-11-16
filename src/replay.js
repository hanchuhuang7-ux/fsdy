import {
  VERSION,
  MAX_TIME,
  UNITS,
  PROPS,
  MAPS,
  assertSafeData,
  validateConfig,
} from "./data.js";

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
export function validateReplay(input) {
  assertSafeData(input);
  if (
    !input ||
    input.format !== "frontline-replay" ||
    input.version !== 1 ||
    input.engineVersion !== VERSION ||
    !["seed", "trajectory"].includes(input.kind)
  )
    throw Error("不支持的回放格式或引擎版本");
  const config = validateConfig(input.config);
  if (input.kind === "seed") {
    if (
      input.frames !== undefined &&
      (!Array.isArray(input.frames) || input.frames.length)
    )
      fail();
    return {
      format: input.format,
      version: 1,
      engineVersion: VERSION,
      kind: "seed",
      config,
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
      f.props.length !== MAPS[config.map].props.length + config.props.length
    )
      fail();
    last = f.time;
    for (const [i, u] of f.units.entries())
      if (
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
        !["explosion", "slam", "heal", "charge", "spikes", "spring"].includes(
          e.type,
        ) ||
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
    Math.abs(final.time - input.result.time) > 0.00001 ||
    final.units.filter((u) => u.alive && u.team === "red").length !==
      input.result.red ||
    final.units.filter((u) => u.alive && u.team === "blue").length !==
      input.result.blue
  )
    fail();
  return structuredClone({
    format: input.format,
    version: 1,
    engineVersion: VERSION,
    kind: "trajectory",
    config,
    result: input.result,
    frames,
  });
}

export function replayPackage(config, frames, result, kind = "trajectory") {
  return validateReplay({
    format: "frontline-replay",
    version: 1,
    engineVersion: VERSION,
    kind,
    config,
    frames: kind === "seed" ? undefined : frames,
    result: kind === "seed" ? undefined : result,
  });
}
