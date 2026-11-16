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

/** Pure seeded deployment planner. It cannot see HP, simulation RNG or hidden hotseat armies. */
export function planArmy({
  map = "meadow",
  team = "blue",
  budget = 2200,
  seed = 42,
  style = "counter",
  opponent = [],
} = {}) {
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
  const sign = team === "red" ? -1 : 1;
  const propPlan =
    style === "guardian"
      ? ["banner", "barricade", "barricade"]
      : map === "bridge"
        ? ["barrel", "barricade"]
        : ["banner", "spikes"];
  const reserve =
    budget >= 650
      ? Math.min(
          budget * 0.18,
          propPlan.reduce((s, t) => s + PROPS[t].cost, 0),
        )
      : 0;
  const lanes =
    style === "maneuver" && ["ruins", "marsh"].includes(map)
      ? [9.6, -9.6, 7.1, -7.1, 4.7, -4.7, 2.3, -2.3, 0]
      : [0, 2.4, -2.4, 4.8, -4.8, 7.2, -7.2, 9.6, -9.6];
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
                  : "advance";
          config[team].push(item);
        } else config.props.push({ ...item, team });
        return true;
      }
    return false;
  };
  for (
    let i = 0, missed = 0;
    i < 180 && missed < cycle.length * 2 && config[team].length < MAX_UNITS;
    i++
  ) {
    const type = cycle[i % cycle.length];
    if (
      teamCost(config, team) + UNITS[type].cost <= budget - reserve &&
      place(type)
    )
      missed = 0;
    else missed++;
  }
  for (const type of propPlan)
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
  };
}

export function withAI(input, team, style, opponent = input[other(team)]) {
  const config = validateConfig(input);
  const plan = planArmy({
    ...config,
    team,
    style,
    opponent,
    seed: (config.seed + (team === "blue" ? 7919 : 0)) >>> 0,
  });
  config[team] = plan.units;
  config.props = [
    ...config.props.filter((p) => p.team !== team),
    ...plan.props,
  ];
  return validateConfig(config);
}

export function aiDuel(input, redStyle = "guardian", blueStyle = "counter") {
  const base = validateConfig({ ...input, red: [], blue: [], props: [] });
  const redDraft = planArmy({
    ...base,
    team: "red",
    style: redStyle,
    opponent: [],
  });
  const blueDraft = planArmy({
    ...base,
    team: "blue",
    style: blueStyle,
    opponent: [],
    seed: (base.seed + 7919) >>> 0,
  });
  // Both sides get the same one-pass scouting opportunity, against public drafts.
  const red = planArmy({
    ...base,
    team: "red",
    style: redStyle,
    opponent: blueDraft.units,
  });
  const blue = planArmy({
    ...base,
    team: "blue",
    style: blueStyle,
    opponent: redDraft.units,
    seed: (base.seed + 7919) >>> 0,
  });
  return validateConfig({
    ...base,
    red: red.units,
    blue: blue.units,
    props: [...red.props, ...blue.props],
  });
}
