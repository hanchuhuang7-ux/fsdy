import { Battle } from "./engine.js";
import { combatPower } from "./data.js";
import {
  VERSION,
  UNITS,
  PROPS,
  MAPS,
  MAX_UNITS,
  MAX_PROPS,
  canPlace,
  teamCost,
  validateConfig,
} from "./data.js";

export const AI_STYLES = {
  oracle: {
    name: "算策 · 推演择优",
    description: "多候选固定步数快筛，再完整验算前两名；仅使用公开初始编队。",
  },
  guardian: {
    name: "磐石 · 稳健守阵",
    description: "盾枪护住弓弩与军医，拒马、治疗旗稳住阵线。",
  },
  maneuver: {
    name: "疾风 · 机动迂回",
    description: "骑兵与游勇走侧翼，远程猎杀后排；沼泽与遗迹优先侧路。",
  },
  counter: {
    name: "观势 · 环境反制",
    description: "只读取公开编队：见骑补枪，高台远射，危桥重锤爆破。",
  },
};
const other = (t) => (t === "red" ? "blue" : "red");

/** Only public type/coordinates are read; HP, alive flags and runtime bodies are ignored. */
export function scoutArmy(opponent = []) {
  const army = opponent.filter(
    (u) =>
      Object.hasOwn(UNITS, u.type) &&
      Number.isFinite(u.x) &&
      Number.isFinite(u.z),
  );
  const counts = Object.fromEntries(Object.keys(UNITS).map((t) => [t, 0]));
  let ranged = 0,
    heavy = 0,
    rush = 0,
    clusterPairs = 0;
  for (const u of army) {
    counts[u.type]++;
    heavy += ["shield", "giant"].includes(u.type) ? 1 : 0;
    rush += ["cavalry", "militia", "bomber"].includes(u.type) ? 1 : 0;
    if (UNITS[u.type].ranged || u.type === "medic") ranged++;
  }
  const backline = army.filter(
    (u) => UNITS[u.type].ranged || u.type === "medic",
  );
  for (let i = 0; i < backline.length; i++)
    for (let j = i + 1; j < backline.length; j++)
      if (
        Math.hypot(
          backline[i].x - backline[j].x,
          backline[i].z - backline[j].z,
        ) <= 3.5
      )
        clusterPairs++;
  return {
    counts,
    size: army.length,
    heavyShare: heavy / (army.length || 1),
    rushShare: rush / (army.length || 1),
    backlineShare: ranged / (army.length || 1),
    clustered:
      backline.length >= 3 &&
      clusterPairs >= Math.max(2, backline.length * 0.6),
    centerZ: army.reduce((s, u) => s + u.z, 0) / (army.length || 1),
    spread: army.length
      ? Math.max(...army.map((u) => u.z)) - Math.min(...army.map((u) => u.z))
      : 0,
  };
}
export function reviewBattle(previous, team) {
  if (!previous?.result?.report || !previous?.config)
    return { mvpType: null, lossLane: null };
  const units = previous.result.report.units || [],
    enemy = other(team);
  const threat = units
    .filter((u) => u.team === enemy)
    .sort(
      (a, b) =>
        b.damageDealt + b.healing - (a.damageDealt + a.healing) || a.id - b.id,
    )[0];
  const lost = units.filter((u) => u.team === team && !u.alive);
  const roster = [...previous.config.red, ...previous.config.blue];
  let weighted = 0,
    weight = 0;
  for (const u of lost) {
    const spawn = u.spawn || roster[u.id - 1];
    if (spawn && Number.isFinite(spawn.z)) {
      const w = UNITS[u.type]?.cost || 1;
      weighted += spawn.z * w;
      weight += w;
    }
  }
  return {
    mvpType: threat?.type || null,
    lossLane: weight ? weighted / weight : null,
  };
}

/** Pure seeded deployment planner. It cannot see HP, simulation RNG or hidden hotseat armies. */
export function planArmy({
  map = "meadow",
  team = "blue",
  budget = 2200,
  seed = 42,
  style = "counter",
  opponent = [],
  previous = null,
} = {}) {
  if (style === "oracle")
    return planOracle({ map, team, budget, seed, opponent, previous });
  if (
    !Object.hasOwn(AI_STYLES, style) ||
    !Object.hasOwn(MAPS, map) ||
    !["red", "blue"].includes(team)
  )
    throw Error("未知 AI 策略、地图或阵营");
  const config = {
    version: VERSION,
    map,
    budget,
    seed,
    red: [],
    blue: [],
    props: [],
  };
  validateConfig(config);
  let rng = seed >>> 0;
  const random = () => {
    rng = (Math.imul(rng, 1664525) + 1013904223) >>> 0;
    return rng / 4294967296;
  };
  const cavalry = opponent.filter((u) => u.type === "cavalry").length;
  const scout = scoutArmy(opponent),
    review = reviewBattle(previous, team);
  let cycle =
    style === "guardian"
      ? [
          "shield",
          "spear",
          "archer",
          "crossbow",
          "medic",
          "shield",
          "spear",
          "archer",
        ]
      : style === "maneuver"
        ? [
            "cavalry",
            "militia",
            "cavalry",
            "crossbow",
            "spear",
            "medic",
            "militia",
          ]
        : cavalry >= 2
          ? ["spear", "spear", "shield", "crossbow", "medic", "spear", "archer"]
          : map === "bridge"
            ? ["giant", "shield", "bomber", "spear", "archer", "medic"]
            : map === "highland"
              ? ["crossbow", "archer", "shield", "spear", "medic", "archer"]
              : map === "marsh"
                ? ["crossbow", "spear", "militia", "archer", "medic"]
                : [
                    "shield",
                    "crossbow",
                    "catapult",
                    "spear",
                    "medic",
                    "archer",
                  ];
  // Counter composition is a weighted response to several simultaneous threats.
  if (style === "counter") {
    const weights = {
      shield: 3,
      spear: 3,
      archer: 3,
      crossbow: 3,
      medic: 2,
      catapult: 2,
      bomber: 1,
      militia: 1,
      cavalry: 1,
      giant: 1,
    };
    if (scout.heavyShare >= 0.34) {
      weights.crossbow += 5;
      weights.bomber += 3;
      weights.catapult += 3;
      weights.archer = 1;
    }
    if (scout.clustered) {
      weights.cavalry += 5;
      weights.militia += 4;
      weights.catapult += 3;
    }
    if (cavalry >= 2 || scout.rushShare >= 0.45) {
      weights.spear += 9;
      weights.shield += 2;
    }
    if (map === "highland") {
      weights.archer += 5;
      weights.crossbow += 4;
    }
    if (map === "bridge") {
      weights.giant += 4;
      weights.bomber += 3;
    }
    if (map === "marsh") {
      weights.cavalry = 1;
      weights.spear += 2;
      weights.archer += 2;
    }
    if (review.mvpType) {
      if (["cavalry", "militia"].includes(review.mvpType)) weights.spear += 6;
      else if (UNITS[review.mvpType]?.ranged || review.mvpType === "medic") {
        weights.cavalry += 3;
        weights.catapult += 3;
      } else {
        weights.crossbow += 3;
        weights.bomber += 2;
      }
    }
    const pool = Object.entries(weights),
      total = pool.reduce((s, [, w]) => s + w, 0);
    cycle = Array.from({ length: 40 }, () => {
      let draw = random() * total;
      return pool.find(([, w]) => (draw -= w) < 0)?.[0] || "spear";
    });
    // Ensure a usable opening at small budgets and preserve map specialisation.
    cycle.unshift(
      cavalry >= 2
        ? "spear"
        : map === "highland"
          ? "archer"
          : map === "bridge"
            ? "giant"
            : scout.heavyShare >= 0.34
              ? "crossbow"
              : "shield",
    );
    if (scout.heavyShare >= 0.34 && budget <= 1500)
      cycle = [
        "shield",
        "crossbow",
        "catapult",
        "spear",
        "medic",
        "archer",
        "shield",
        "crossbow",
        ...cycle,
      ];
  }
  const sign = team === "red" ? -1 : 1;
  let propPlan =
    style === "guardian"
      ? ["banner", "barricade", "barricade"]
      : map === "bridge"
        ? ["barrel", "barricade"]
        : ["banner", "spikes"];
  if (style === "counter" && (cavalry >= 2 || scout.rushShare >= 0.45))
    propPlan = ["barricade", "barricade", "banner"];
  else if (style === "counter" && scout.clustered)
    propPlan = ["barrel", "banner"];
  if (map === "bridge" && !propPlan.includes("barrel")) propPlan.push("barrel");
  const reserve =
    budget >= 650
      ? Math.min(
          budget * 0.18,
          propPlan.reduce((s, t) => s + PROPS[t].cost, 0),
        )
      : 0;
  let lanes =
    (style === "maneuver" || (style === "counter" && scout.clustered)) &&
    ["ruins", "marsh"].includes(map)
      ? [9.6, -9.6, 7.1, -7.1, 4.7, -4.7, 2.3, -2.3, 0]
      : [0, 2.4, -2.4, 4.8, -4.8, 7.2, -7.2, 9.6, -9.6];
  // Seeded ordering and a shared row offset give distinct legal formations.
  const offset = (random() - 0.5) * 0.7;
  if (style === "counter")
    lanes = lanes
      .map((z) => Math.max(-10.1, Math.min(10.1, z + offset)))
      .sort((a, b) => {
        const loss = review.lossLane;
        const score = (z) =>
          (loss === null
            ? Math.abs(z - scout.centerZ * 0.4)
            : -Math.abs(z - loss)) +
          (map === "ruins" || map === "marsh" ? -0.25 * Math.abs(z) : 0);
        return score(a) - score(b);
      });
  if (style === "counter" && random() < 0.5) lanes = lanes.map((z) => -z);
  if (style !== "counter" && review.lossLane !== null) {
    lanes.sort(
      (a, b) => Math.abs(b - review.lossLane) - Math.abs(a - review.lossLane),
    );
    if (review.mvpType)
      cycle.unshift(
        ["cavalry", "militia"].includes(review.mvpType)
          ? "spear"
          : UNITS[review.mvpType]?.ranged
            ? "cavalry"
            : "crossbow",
      );
  }
  // Find legal slots rather than relaxing spacing when funds or room run short.
  const place = (type, kind = "unit") => {
    const d = kind === "unit" ? UNITS[type] : PROPS[type];
    const mobile = kind === "unit" && ["cavalry", "militia"].includes(type);
    const depths =
      kind === "prop"
        ? type === "banner"
          ? [9.8, 12.2, 14.7, 7.4]
          : [3.1, 4.2, 6.6, 9, 12]
        : d.ranged || type === "medic"
          ? map === "highland" && style === "counter"
            ? [3.2, 5.8, 8.4, 11, 13.6, 16.1]
            : scout.heavyShare >= 0.34 && style === "counter" && budget > 1500
              ? [10.7, 13.3, 8.1, 15.9, 5.5, 3.2]
              : [8.1, 10.7, 13.3, 15.9, 5.5, 3.2]
          : [4.5, 7.1, 9.7, 12.3, 14.9];
    const zSlots =
      kind === "prop" ? [1.4, -1.4, 4, -4, 6.6, -6.6, 9.2, -9.2, 0] : lanes;
    const flip = random() < 0.5 ? -1 : 1;
    for (const depth of depths)
      for (const lane of zSlots) {
        const item = { type, x: sign * depth, z: lane * flip };
        if (!canPlace(config, team, item, kind)) continue;
        if (kind === "unit") {
          item.stance =
            style === "guardian"
              ? "hold"
              : mobile && style === "maneuver"
                ? "flank"
                : d.ranged && style === "maneuver"
                  ? "hunt"
                  : style === "counter" && mobile && scout.clustered
                    ? ["ruins", "marsh"].includes(map)
                      ? "flank"
                      : "hunt"
                    : "advance";
          config[team].push(item);
        } else config.props.push({ ...item, team });
        return true;
      }
    return false;
  };
  let prepaid = 0,
    prepaidCount = 0;
  if (style === "counter" && (cavalry >= 2 || scout.rushShare >= 0.45)) {
    for (const type of propPlan) {
      if (
        type !== "barricade" ||
        teamCost(config, team) + PROPS[type].cost > budget - 40 ||
        !place(type, "prop")
      )
        break;
      prepaid += PROPS[type].cost;
      prepaidCount++;
    }
  }
  for (
    let i = 0, missed = 0;
    i < 180 && missed < cycle.length * 2 && config[team].length < MAX_UNITS;
    i++
  ) {
    const type = cycle[i % cycle.length];
    if (
      teamCost(config, team) + UNITS[type].cost <=
        budget - Math.max(0, reserve - prepaid) &&
      place(type)
    )
      missed = 0;
    else missed++;
  }
  for (const type of propPlan.slice(prepaidCount))
    if (
      config.props.length < MAX_PROPS &&
      teamCost(config, team) + PROPS[type].cost <= budget
    )
      place(type, "prop");
  // Spend affordable remainder without creating overlaps or exceeding either cap.
  for (const type of ["spear", "militia"])
    for (let n = 0; n < MAX_UNITS; n++) {
      if (
        config[team].length >= MAX_UNITS ||
        teamCost(config, team) + UNITS[type].cost > budget ||
        !place(type)
      )
        break;
    }
  validateConfig(config);
  return {
    units: config[team],
    props: config.props,
    style,
    spent: teamCost(config, team),
    unused: budget - teamCost(config, team),
    scouting: scout,
    review,
  };
}

export function withAI(
  input,
  team,
  style,
  opponent = input[other(team)],
  previous = null,
) {
  const config = validateConfig(input);
  const plan = planArmy({
    ...config,
    team,
    style,
    opponent,
    previous,
    seed: (config.seed + (team === "blue" ? 7919 : 0)) >>> 0,
  });
  config[team] = plan.units;
  config.props = [
    ...config.props.filter((p) => p.team !== team),
    ...plan.props,
  ];
  return validateConfig(config);
}

export function aiDuel(
  input,
  redStyle = "guardian",
  blueStyle = "counter",
  previous = null,
) {
  const base = validateConfig({ ...input, red: [], blue: [], props: [] });
  const redDraft = planArmy({
    ...base,
    team: "red",
    style: redStyle === "oracle" ? "counter" : redStyle,
    opponent: [],
  });
  const blueDraft = planArmy({
    ...base,
    team: "blue",
    style: blueStyle === "oracle" ? "counter" : blueStyle,
    opponent: [],
    seed: (base.seed + 7919) >>> 0,
  });
  // Both sides get the same one-pass scouting opportunity, against public drafts.
  const red = planArmy({
    ...base,
    team: "red",
    style: redStyle,
    opponent: blueDraft.units,
    previous,
  });
  const blue = planArmy({
    ...base,
    team: "blue",
    style: blueStyle,
    opponent: redDraft.units,
    previous,
    seed: (base.seed + 7919) >>> 0,
  });
  return validateConfig({
    ...base,
    red: red.units,
    blue: blue.units,
    props: [...red.props, ...blue.props],
  });
}

/** Bounded generator: both sync CLI and asynchronously sliced web searches execute identical steps. */
export function* oracleSearch(options = {}) {
  const {
    map = "meadow",
    team = "blue",
    budget = 2200,
    seed = 42,
    opponent = [],
    previous = null,
    candidateCount = 4,
    screenSteps = 360,
    finalSteps = 5400,
  } = options;
  if (
    !Number.isInteger(candidateCount) ||
    candidateCount < 3 ||
    candidateCount > 6 ||
    !Number.isInteger(screenSteps) ||
    screenSteps < 60 ||
    screenSteps > 720 ||
    !Number.isInteger(finalSteps) ||
    finalSteps < screenSteps ||
    finalSteps > 5400
  )
    throw Error("推演预算无效");
  const enemy = other(team),
    styles = ["counter", "guardian", "maneuver"],
    candidates = [];
  // Deliberately copy only public initial placement fields. No object from a running battle crosses this boundary.
  const visible = opponent
    .filter((u) => Object.hasOwn(UNITS, u.type))
    .map((u) => ({
      type: u.type,
      x: u.x,
      z: u.z,
      stance: u.stance || "advance",
    }));
  const foes = visible.length
    ? visible
    : planArmy({
        map,
        team: enemy,
        budget,
        seed: seed >>> 0,
        style: "guardian",
      }).units;
  for (let i = 0; i < candidateCount; i++)
    candidates.push(
      planArmy({
        map,
        team,
        budget,
        seed: (seed + Math.imul(i, 104729)) >>> 0,
        style: styles[i % 3],
        opponent: visible,
        previous,
      }),
    );
  const results = [];
  let done = 0;
  const total = candidateCount * screenSteps + 2 * finalSteps;
  function* trial(index, steps, phase) {
    const plan = candidates[index],
      config = validateConfig({
        version: VERSION,
        map,
        budget,
        seed: seed >>> 0,
        [team]: plan.units,
        [enemy]: foes,
        props: plan.props,
        commander: { enabled: false, aiTeams: [] },
      }),
      b = new Battle(config);
    let used = 0;
    try {
      while (used < steps && !b.result) {
        b.step();
        used++;
        if (used % 60 === 0)
          yield { done: done + used, total, phase, candidate: index + 1 };
      }
      const power = combatPower(b.units);
      return {
        index,
        score: power[team],
        advantage: power[team] - power[enemy],
        remaining: power[team],
        steps: used,
      };
    } finally {
      done += steps;
      b.dispose();
    }
  }
  for (let i = 0; i < candidates.length; i++)
    results.push(yield* trial(i, screenSteps, "快筛"));
  results.sort(
    (a, b) =>
      b.score - a.score || b.advantage - a.advantage || a.index - b.index,
  );
  const finals = [];
  for (const row of results.slice(0, 2))
    finals.push(yield* trial(row.index, finalSteps, "验算"));
  finals.sort(
    (a, b) =>
      b.score - a.score || b.advantage - a.advantage || a.index - b.index,
  );
  const chosen = finals[0];
  return {
    ...candidates[chosen.index],
    style: "oracle",
    search: {
      candidateCount,
      screenSteps,
      finalSteps,
      chosen: chosen.index,
      trials: results,
      finalists: finals,
      maxSteps: total,
    },
  };
}
export function planOracle(options) {
  const g = oracleSearch(options);
  let r;
  do {
    r = g.next();
  } while (!r.done);
  return r.value;
}
export async function planArmyAsync(
  options,
  { onProgress = () => {}, signal } = {},
) {
  if (options.style !== "oracle") return planArmy(options);
  const g = oracleSearch(options);
  try {
    let r;
    do {
      if (signal?.aborted) throw Error("推演已取消");
      r = g.next();
      if (!r.done) {
        onProgress(r.value);
        await new Promise((resolve) => setTimeout(resolve, 0));
      }
    } while (!r.done);
    return r.value;
  } finally {
    g.return();
  }
}
export async function withAIAsync(
  input,
  team,
  style,
  opponent = input[other(team)],
  previous = null,
  control = {},
) {
  const c = validateConfig(input),
    plan = await planArmyAsync(
      {
        ...c,
        team,
        style,
        opponent,
        previous,
        seed: (c.seed + (team === "blue" ? 7919 : 0)) >>> 0,
      },
      control,
    );
  return validateConfig({
    ...c,
    [team]: plan.units,
    props: [...c.props.filter((p) => p.team !== team), ...plan.props],
  });
}
export async function aiDuelAsync(
  input,
  redStyle = "guardian",
  blueStyle = "counter",
  previous = null,
  control = {},
) {
  const base = validateConfig({ ...input, red: [], blue: [], props: [] });
  // Drafts use cheap public plans. Oracle spends its bounded budget on the final, fair one-pass scout.
  const rd = planArmy({
      ...base,
      team: "red",
      style: redStyle === "oracle" ? "counter" : redStyle,
    }),
    bd = planArmy({
      ...base,
      team: "blue",
      style: blueStyle === "oracle" ? "counter" : blueStyle,
      seed: (base.seed + 7919) >>> 0,
    });
  const red = await withAIAsync(
    { ...base, blue: bd.units },
    "red",
    redStyle,
    bd.units,
    previous,
    control,
  );
  const blue = await withAIAsync(
    { ...base, red: rd.units },
    "blue",
    blueStyle,
    rd.units,
    previous,
    control,
  );
  return validateConfig({
    ...base,
    red: red.red,
    blue: blue.blue,
    props: [...red.props, ...blue.props],
  });
}
