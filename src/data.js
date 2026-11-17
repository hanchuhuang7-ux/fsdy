export const VERSION = "0.5.0";
export const ORDERS = {
  inspire: { name: "全军鼓舞", duration: 300, radius: 6, cost: 1 },
  focus: { name: "集火令", duration: 360, radius: 8, cost: 1 },
  fortify: { name: "固守盾阵", duration: 300, radius: 6, cost: 1 },
};
export const ORDER_POINTS = 3;
export const ORDER_COOLDOWN = 480;
export function validateOrderLog(log, config, maxTick = 5399) {
  assertSafeData(log);
  if (
    !Array.isArray(log) ||
    log.length > 6 ||
    (!config.commander.enabled && log.length)
  )
    throw Error("军令日志无效");
  const points = { red: 3, blue: 3 },
    ready = { red: 0, blue: 0 };
  let previous = -1;
  return log.map((o, i) => {
    if (
      !o ||
      !["red", "blue"].includes(o.team) ||
      !Object.hasOwn(ORDERS, o.type) ||
      !Number.isInteger(o.tick) ||
      o.tick < previous ||
      o.tick < 0 ||
      o.tick > maxTick ||
      o.tick < ready[o.team] ||
      points[o.team] < 1 ||
      !Number.isFinite(o.x) ||
      !Number.isFinite(o.z) ||
      Math.abs(o.x) > 18 ||
      Math.abs(o.z) > 12 ||
      !groundAt(config.map, o.x, o.z) ||
      (o.id !== undefined && o.id !== i + 1)
    )
      throw Error("军令日志越界、超额或违反冷却");
    previous = o.tick;
    points[o.team]--;
    ready[o.team] = o.tick + ORDER_COOLDOWN;
    return {
      id: i + 1,
      team: o.team,
      type: o.type,
      x: o.x,
      z: o.z,
      tick: o.tick,
    };
  });
}
export const unitPower = (u) =>
  (Math.max(0, u.hp) / (u.maxHp || UNITS[u.type].hp)) * UNITS[u.type].cost;
export function combatPower(units) {
  const value = { red: 0, blue: 0 };
  for (const u of units)
    if (u.alive && Object.hasOwn(value, u.team)) value[u.team] += unitPower(u);
  return value;
}
export const STANCES = {
  advance: "常规推进",
  hold: "坚守阵地",
  flank: "侧翼迂回",
  hunt: "猎杀远程",
};
export const DT = 1 / 60;
export const MAX_TIME = 90;
export const MAX_UNITS = 60;
export const MAX_PROPS = 12;
export const UNITS = {
  militia: {
    name: "游勇",
    subtitle: "轻装近战",
    glyph: "Ⅰ",
    cost: 40,
    hp: 80,
    damage: 18,
    range: 1.35,
    speed: 3.2,
    cooldown: 0.8,
    mass: 5,
    scale: 1,
    knock: 3.5,
    description: "便宜、跑得快。适合冲锋和分散火力。",
  },
  shield: {
    name: "盾卫",
    subtitle: "正面防御",
    glyph: "▣",
    cost: 90,
    hp: 220,
    damage: 14,
    range: 1.45,
    speed: 1.9,
    cooldown: 1.1,
    mass: 12,
    scale: 1.12,
    knock: 5,
    description: "正面减伤 60%；掩护后排，但要小心背袭。",
  },
  spear: {
    name: "长枪兵",
    subtitle: "长距突刺",
    glyph: "↟",
    cost: 75,
    hp: 100,
    damage: 32,
    range: 2.55,
    speed: 2.5,
    cooldown: 1.25,
    mass: 6,
    scale: 1,
    knock: 7,
    description: "远距离突刺。对高速冲来的骑兵造成额外伤害。",
  },
  archer: {
    name: "弓手",
    subtitle: "弹道远射",
    glyph: "⌁",
    cost: 110,
    hp: 65,
    damage: 28,
    range: 17,
    speed: 2.2,
    cooldown: 2.2,
    mass: 4,
    scale: 1,
    knock: 3,
    ranged: true,
    description: "真实弹道，可被掩体挡住，也会误伤友军。高地增加射程。",
  },
  bomber: {
    name: "掷弹兵",
    subtitle: "范围爆破",
    glyph: "✹",
    cost: 180,
    hp: 85,
    damage: 65,
    range: 11,
    speed: 2,
    cooldown: 3.8,
    mass: 6,
    scale: 1.05,
    knock: 16,
    ranged: true,
    description: "投掷延时炸弹；爆炸可引爆炸药桶、炸毁拒马，伤害不分敌我。",
  },
  giant: {
    name: "大锤",
    subtitle: "重装横扫",
    glyph: "⚒",
    cost: 320,
    hp: 620,
    damage: 50,
    range: 2.9,
    speed: 1.7,
    cooldown: 2.3,
    mass: 32,
    scale: 1.8,
    knock: 22,
    description: "重锤横扫和强力击退。适合抢夺高台和桥头。",
  },
  cavalry: {
    name: "骑兵",
    subtitle: "高速冲锋",
    glyph: "♞",
    cost: 190,
    hp: 245,
    damage: 27,
    range: 1.85,
    speed: 5.5,
    cooldown: 1.3,
    mass: 19,
    scale: 1.2,
    knock: 10,
    description: "连续奔跑后冲锋，伤害与击退加强；沼泽和长枪会克制骑兵。",
  },
  crossbow: {
    name: "弩手",
    subtitle: "破甲直射",
    glyph: "⌖",
    cost: 160,
    hp: 90,
    damage: 48,
    range: 20,
    speed: 1.8,
    cooldown: 2.8,
    mass: 5,
    scale: 1,
    knock: 6,
    ranged: true,
    description: "低弧度弩箭，部分穿透盾牌减伤；易被拒马和废墟挡住。",
  },
  medic: {
    name: "军医",
    subtitle: "战地支援",
    glyph: "✚",
    cost: 160,
    hp: 100,
    damage: 10,
    range: 1.3,
    speed: 2.5,
    cooldown: 1,
    mass: 5,
    scale: 1,
    knock: 1,
    heal: 16,
    description: "每秒为六米内受伤最重的友军治疗 16 点；不会复活阵亡者。",
  },
  catapult: {
    name: "投石车",
    subtitle: "重型抛射",
    glyph: "◒",
    cost: 280,
    hp: 240,
    damage: 80,
    range: 24,
    speed: 1,
    cooldown: 4.8,
    mass: 26,
    scale: 1.3,
    knock: 24,
    ranged: true,
    description: "高弧度石弹，落地造成范围冲击。可越过低矮掩体，注意友伤。",
  },
};
export const PROPS = {
  barrel: {
    name: "炸药桶",
    subtitle: "连锁爆炸",
    glyph: "◉",
    cost: 65,
    hp: 45,
    radius: 0.6,
    description:
      "敌军靠近后延时引爆；受击也会爆炸，可连锁引爆附近桶。双方都会受伤。",
  },
  barricade: {
    name: "拒马",
    subtitle: "可破坏掩体",
    glyph: "╳",
    cost: 80,
    hp: 240,
    radius: 1.25,
    description: "挡住士兵与直射弹药。敌方近战会拆除，炸弹和石弹也能摧毁它。",
  },
  spikes: {
    name: "地刺",
    subtitle: "伏击陷阱",
    glyph: "⋀",
    cost: 70,
    hp: 100,
    radius: 1.05,
    description: "敌军进入时受到持续伤害；地图自带地刺对双方都生效。",
  },
  spring: {
    name: "弹射板",
    subtitle: "借力跃进",
    glyph: "⇈",
    cost: 85,
    hp: 100,
    radius: 1,
    description:
      "踩上后向前上方弹射。可以越过战线，也可能把人送下悬崖。每次触发间隔 1.5 秒。",
  },
  banner: {
    name: "治疗旗",
    subtitle: "阵地补给",
    glyph: "⚑",
    cost: 100,
    hp: 120,
    radius: 0.5,
    description:
      "持续治疗 3.2 米内友军；地图中立旗对双方有效。旗杆可被炮火摧毁。",
  },
};
const field = (color = 0xb5c59a) => ({
  color,
  platforms: [{ x: 0, y: -0.7, z: 0, hx: 18, hy: 0.7, hz: 12 }],
  rocks: [],
  walls: [],
  zones: [],
  props: [],
});
const rampAngle = Math.atan2(2.2, 5),
  rampHalf = Math.hypot(5, 2.2) / 2;
export const MAPS = {
  meadow: {
    ...field(),
    name: "风起草甸",
    description: "开阔平原。用道具搭建自己的战线。",
    hint: "开阔地 · 自由布置掩体与陷阱",
    rocks: [
      { x: 0, z: 7, r: 0.8 },
      { x: 0, z: -7, r: 1 },
    ],
  },
  bridge: {
    ...field(0xb7bc97),
    name: "独木危桥",
    description: "狭桥、深谷与拥挤。爆破能改变桥头战局。",
    hint: "狭窄通道 · 爆炸与击退可造成坠落",
    platforms: [
      { x: -10.5, y: -0.9, z: 0, hx: 7.5, hy: 0.9, hz: 12 },
      { x: 10.5, y: -0.9, z: 0, hx: 7.5, hy: 0.9, hz: 12 },
      { x: 0, y: -0.25, z: 0, hx: 3.1, hy: 0.25, hz: 1.8, bridge: true },
    ],
    passage: { approach: 4.2, lanes: [0] },
    props: [
      { type: "barrel", x: -2, z: 1.05 },
      { type: "barrel", x: 2, z: -1.05 },
    ],
  },
  island: {
    ...field(0xc6b89a),
    name: "浮岛角斗场",
    description: "四面临空。弹射板与重锤都是双刃剑。",
    hint: "四面临空 · 控制击退方向",
    platforms: [{ x: 0, y: -1, z: 0, hx: 15, hy: 1, hz: 10 }],
    rocks: [
      { x: 0, z: 4, r: 1.1 },
      { x: 0, z: -4, r: 1.1 },
    ],
    props: [
      { type: "spring", x: -2, z: 3, team: "red" },
      { type: "spring", x: 2, z: -3, team: "blue" },
    ],
  },
  ruins: {
    ...field(0xc5b493),
    name: "烽火遗迹",
    description: "废墙挡住直射；绕侧翼，或用抛射与爆破清场。",
    hint: "不可破坏废墙 · 可破坏拒马 · 连锁炸药桶",
    walls: [
      { x: 0, z: -5.6, hx: 0.55, hy: 1.2, hz: 3.4 },
      { x: 0, z: 5.6, hx: 0.55, hy: 1.2, hz: 3.4 },
    ],
    passage: { approach: 3, lanes: [0, -10.2, 10.2] },
    props: [
      { type: "barrel", x: 0, z: -1.5 },
      { type: "barrel", x: 0, z: 1.5 },
      { type: "barricade", x: 0, z: 0 },
    ],
  },
  marsh: {
    ...field(0x9bad82),
    name: "雾沼营地",
    description: "沼泽减速，侧路更快。争夺补给旗，避开中立地刺。",
    hint: "沼泽速度 ×0.45 · 侧路通行 · 中立治疗旗",
    zones: [{ type: "mud", x: 0, z: 0, hx: 4, hz: 7.5 }],
    rocks: [
      { x: 1, z: -5, r: 0.8 },
      { x: -1, z: 5, r: 0.8 },
    ],
    props: [
      { type: "banner", x: 0, z: 9.4 },
      { type: "banner", x: 0, z: -9.4 },
      { type: "spikes", x: 0, z: -2.5 },
      { type: "spikes", x: 0, z: 2.5 },
    ],
  },
  highland: {
    ...field(0xb2bea0),
    name: "霜岭高台",
    description: "沿坡道抢占高地，远程射程增加。冰面上更难刹车。",
    hint: "高地射程 +18% · 坡道通行 · 冰面惯性",
    platforms: [
      ...field().platforms,
      { x: 0, y: 1, z: 0, hx: 3, hy: 1.2, hz: 4.5 },
      {
        x: -5.5,
        y: 1.1 - 0.18 / Math.cos(rampAngle),
        z: 0,
        hx: rampHalf,
        hy: 0.18,
        hz: 4.5,
        angle: rampAngle,
        ramp: true,
      },
      {
        x: 5.5,
        y: 1.1 - 0.18 / Math.cos(rampAngle),
        z: 0,
        hx: rampHalf,
        hy: 0.18,
        hz: 4.5,
        angle: -rampAngle,
        ramp: true,
      },
    ],
    passage: { approach: 8.5, lanes: [0] },
    zones: [{ type: "ice", x: 0, z: 0, hx: 2.8, hz: 4.3 }],
    props: [
      { type: "barrel", x: 0, z: 3.4 },
      { type: "banner", x: 0, z: -2.5 },
    ],
  },
};
export const MISSIONS = [
  {
    name: "01 / 破阵",
    description: "用 1,300 军费突破盾枪防线。",
    map: "meadow",
    budget: 1300,
    enemy: "phalanx",
  },
  {
    name: "02 / 桥头堡",
    description: "用 1,400 军费攻下桥头，注意炸药桶。",
    map: "bridge",
    budget: 1400,
    enemy: "battery",
  },
  {
    name: "03 / 巨人之岛",
    description: "用 1,500 军费对抗双大锤。",
    map: "island",
    budget: 1500,
    enemy: "titans",
  },
  {
    name: "04 / 遗迹攻坚",
    description: "用 2,200 军费击破废墟后的弩炮阵线。",
    map: "ruins",
    budget: 2200,
    enemy: "siege",
  },
  {
    name: "05 / 沼泽突围",
    description: "用 1,900 军费突破营地。军医和侧路都能派上用场。",
    map: "marsh",
    budget: 1900,
    enemy: "support",
  },
  {
    name: "06 / 争夺霜岭",
    description: "用 2,200 军费抢占高台，抵挡骑兵与远射。",
    map: "highland",
    budget: 2200,
    enemy: "cavalry",
  },
];
export function formation(preset = "balanced", team = "red") {
  const sign = team === "red" ? -1 : 1,
    list = [];
  const line = (type, x, n, spacing = 1.8, stance = "advance") => {
    for (let i = 0; i < n; i++)
      list.push({
        type,
        x: x * sign,
        z: Math.round((i - (n - 1) / 2) * spacing * 100) / 100,
        stance,
      });
  };
  if (preset === "phalanx") {
    line("shield", 5, 4);
    line("spear", 7, 5);
    line("archer", 10, 2);
  } else if (preset === "battery") {
    line("shield", 5, 3, 1.2);
    line("spear", 7, 3, 1.3);
    line("bomber", 10, 2, 2);
    line("archer", 12, 3);
  } else if (preset === "titans") {
    line("giant", 5, 2, 4);
    line("archer", 10, 4);
    line("militia", 7.5, 4);
  } else if (preset === "rush") {
    line("militia", 5, 7);
    line("spear", 7, 5);
    line("militia", 9, 7);
  } else if (preset === "volley") {
    line("shield", 5, 4);
    line("archer", 9, 6);
    line("bomber", 12, 2, 3);
  } else if (preset === "cavalry") {
    line("cavalry", 5, 5, 2.4);
    line("crossbow", 10, 4);
    line("medic", 12.5, 1);
  } else if (preset === "support") {
    line("shield", 5, 4);
    line("spear", 7.3, 4);
    line("crossbow", 10, 4);
    line("medic", 12.5, 2, 3);
  } else if (preset === "siege") {
    line("shield", 5, 4);
    line("crossbow", 9, 5);
    line("catapult", 12.5, 3, 3);
  } else {
    line("shield", 5, 3, 2);
    line("spear", 7, 4);
    line("archer", 10, 3);
    line("bomber", 12, 1);
    line("cavalry", 5, 2, 9);
    line("medic", 13.5, 1);
  }
  return list;
}
export function missionArmy(mission) {
  return formation(mission.enemy, "blue").map((u) => ({
    ...u,
    stance:
      u.type === "cavalry"
        ? "flank"
        : UNITS[u.type].ranged
          ? "hunt"
          : u.type === "medic"
            ? "hold"
            : "advance",
  }));
}
export function cost(units) {
  return units.reduce((s, u) => s + (UNITS[u.type]?.cost || 0), 0);
}
export function teamCost(config, team) {
  return (
    cost(config[team]) +
    (config.props || [])
      .filter((p) => p.team === team)
      .reduce((s, p) => s + PROPS[p.type].cost, 0)
  );
}
export function heightAt(map, x, z) {
  let height = -Infinity;
  for (const p of MAPS[map].platforms) {
    const a = p.angle || 0,
      extent = p.hx * Math.cos(a) + p.hy * Math.abs(Math.sin(a));
    if (Math.abs(x - p.x) <= extent - 0.01 && Math.abs(z - p.z) <= p.hz - 0.01)
      height = Math.max(
        height,
        p.y + p.hy / Math.cos(a) + Math.tan(a) * (x - p.x),
      );
  }
  return height;
}
export function groundAt(map, x, z) {
  return Number.isFinite(heightAt(map, x, z));
}
export function terrainAt(map, x, z) {
  return (
    MAPS[map].zones.find(
      (p) => Math.abs(x - p.x) < p.hx && Math.abs(z - p.z) < p.hz,
    )?.type || "ground"
  );
}
export function validateConfig(input) {
  assertSafeData(input);
  if (!input || !["0.1.0", "0.2.0", "0.3.0", "0.4.0", VERSION].includes(input.version))
    throw Error("不支持的文件版本，需要 " + VERSION);
  const config = {
    version: VERSION,
    map: input.map,
    seed: input.seed,
    budget: input.budget,
    red: input.red,
    blue: input.blue,
    props: input.props || [],
    commander: { enabled: false, aiTeams: [] },
  };
  if (input.commander !== undefined) {
    const c = input.commander;
    if (
      !c ||
      typeof c.enabled !== "boolean" ||
      !Array.isArray(c.aiTeams) ||
      c.aiTeams.length > 2 ||
      new Set(c.aiTeams).size !== c.aiTeams.length ||
      c.aiTeams.some((t) => !["red", "blue"].includes(t))
    )
      throw Error("军令模式配置无效");
    config.commander = { enabled: c.enabled, aiTeams: [...c.aiTeams] };
  }
  if (!Object.hasOwn(MAPS, config.map)) throw Error("未知战场");
  if (
    !Number.isInteger(config.seed) ||
    config.seed < 0 ||
    config.seed > 0xffffffff
  )
    throw Error("随机种子须为 32 位非负整数");
  if (
    !Number.isInteger(config.budget) ||
    config.budget < 100 ||
    config.budget > 10000
  )
    throw Error("军费须为 100–10000 的整数");
  if (!Array.isArray(config.props) || config.props.length > MAX_PROPS * 2)
    throw Error("道具数量超限");
  const legalPosition = (u) =>
    Number.isFinite(u.x) &&
    Number.isFinite(u.z) &&
    Math.abs(u.x) <= 16.8 &&
    Math.abs(u.z) <= 10.8 &&
    groundAt(config.map, u.x, u.z);
  for (const p of config.props) {
    if (
      !Object.hasOwn(PROPS, p.type) ||
      !["red", "blue"].includes(p.team) ||
      !legalPosition(p)
    )
      throw Error("道具类型、阵营或坐标无效");
    if (p.team === "red" ? p.x > -2.5 : p.x < 2.5)
      throw Error("请在本方半场放置道具");
  }
  for (const team of ["red", "blue"]) {
    const army = config[team];
    if (!Array.isArray(army) || army.length > MAX_UNITS)
      throw Error("每方最多 " + MAX_UNITS + " 个单位");
    if (config.props.filter((p) => p.team === team).length > MAX_PROPS)
      throw Error("每方最多 " + MAX_PROPS + " 件道具");
    if (teamCost(config, team) > config.budget)
      throw Error("兵力与道具总价超出军费");
    for (const u of army) {
      if (
        !Object.hasOwn(UNITS, u.type) ||
        (u.stance !== undefined && !Object.hasOwn(STANCES, u.stance))
      )
        throw Error("兵种或姿态无效");
      if (!legalPosition(u)) throw Error("布阵坐标越界");
      if (team === "red" ? u.x > -2.5 : u.x < 2.5)
        throw Error("请在本方半场布阵");
    }
  }
  for (const t of ["red", "blue"])
    config[t] = config[t].map((u) => ({
      type: u.type,
      x: u.x,
      z: u.z,
      stance: u.stance || "advance",
    }));
  config.props = config.props.map((p) => ({
    type: p.type,
    team: p.team,
    x: p.x,
    z: p.z,
  }));
  return config;
}
// Used at all JSON boundaries; reject dangerous keys even in otherwise unused fields.
export function assertSafeData(value) {
  let nodes = 0;
  const visit = (v, depth = 0) => {
    if (++nodes > 6000000 || depth > 20) throw Error("输入数据过大或嵌套过深");
    if (!v || typeof v !== "object") return;
    const proto = Object.getPrototypeOf(v);
    if (!Array.isArray(v) && proto !== Object.prototype && proto !== null)
      throw Error("只接受普通 JSON 对象");
    for (const k of Object.keys(v)) {
      if (["__proto__", "constructor", "prototype"].includes(k))
        throw Error("不安全的对象键");
      visit(v[k], depth + 1);
    }
  };
  visit(value);
}

// New placements use the same geometry/spacing rules in browser, AI and batch tools.
// Legacy configuration import deliberately keeps its original overlap tolerance.
export function canPlace(config, team, item, kind = "unit") {
  const defs = kind === "unit" ? UNITS : PROPS;
  if (!Object.hasOwn(defs, item.type) || !["red", "blue"].includes(team))
    return false;
  const { x, z } = item,
    radius =
      kind === "unit" ? 0.62 * UNITS[item.type].scale : PROPS[item.type].radius;
  if (
    !Number.isFinite(x) ||
    !Number.isFinite(z) ||
    Math.abs(x) > 16.8 ||
    Math.abs(z) > 10.8 ||
    (team === "red" ? x > -2.5 : x < 2.5) ||
    !groundAt(config.map, x, z)
  )
    return false;
  if (
    MAPS[config.map].walls.some(
      (w) =>
        Math.abs(x - w.x) < w.hx + radius && Math.abs(z - w.z) < w.hz + radius,
    )
  )
    return false;
  if (
    MAPS[config.map].rocks.some(
      (r) => Math.hypot(x - r.x, z - r.z) < r.r + radius,
    )
  )
    return false;
  if (
    [...MAPS[config.map].props, ...config.props].some(
      (q) =>
        !(kind === "unit" && ["spring", "spikes"].includes(q.type)) &&
        Math.hypot(x - q.x, z - q.z) < radius + PROPS[q.type].radius,
    )
  )
    return false;
  return ![...config.red, ...config.blue].some(
    (u) => Math.hypot(x - u.x, z - u.z) < radius + 0.62 * UNITS[u.type].scale,
  );
}

export function tacticalPreview(config, item, kind = "unit", team = "red") {
  const floor = heightAt(config.map, item.x, item.z);
  if (kind === "order")
    return { radius: ORDERS[item.type].radius, floor, bonus: false };
  if (kind === "prop")
    return {
      radius: {
        barrel: 4.2,
        spikes: 1.15,
        banner: 3.2,
        spring: 0.95,
        barricade: 1.25,
      }[item.type],
      floor,
      bonus: false,
    };
  const d = UNITS[item.type],
    opponents = config[team === "red" ? "blue" : "red"];
  const target = opponents.reduce(
    (best, u) =>
      !best ||
      Math.hypot(u.x - item.x, u.z - item.z) <
        Math.hypot(best.x - item.x, best.z - item.z)
        ? u
        : best,
    null,
  );
  const targetFloor = target ? heightAt(config.map, target.x, target.z) : 0;
  const bonus = Boolean(d.ranged && floor > targetFloor + 1);
  return {
    radius: d.range * (bonus ? 1.18 : 1),
    floor,
    bonus,
    charge: item.type === "cavalry" ? (team === "red" ? 1 : -1) : 0,
  };
}
export function defaultConfig(map = "meadow") {
  return {
    version: VERSION,
    map,
    seed: 20261116,
    budget: 2200,
    red: formation(),
    blue: formation("balanced", "blue"),
    props: [],
    commander: { enabled: false, aiTeams: [] },
  };
}
