export const VERSION = "0.1.0";
export const DT = 1 / 60;
export const MAX_TIME = 90;
export const MAX_UNITS = 45;
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
    description: "正面受到的伤害减少 60%，背后依然脆弱。",
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
    description: "攻击距离长，能将敌人推离战线。",
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
    description: "弓箭走真实弹道，会被盾卫挡住，也会误伤友军。",
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
    description: "投掷延时炸弹。爆炸伤害与冲击波不分敌我。",
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
    description: "高血量，重锤横扫周围敌军；容易被远程消耗。",
  },
};
export const MAPS = {
  meadow: {
    name: "风起草甸",
    description: "开阔平原。让阵型与兵种决定胜负。",
    color: 0xb5c59a,
    platforms: [{ x: 0, y: -0.7, z: 0, hx: 18, hy: 0.7, hz: 12 }],
    rocks: [
      { x: 0, z: 7, r: 0.8 },
      { x: 0, z: -7, r: 1 },
    ],
  },
  bridge: {
    name: "独木危桥",
    description: "狭桥、深谷与友军拥挤。小心脚下。",
    color: 0xb7bc97,
    platforms: [
      { x: -10.5, y: -0.9, z: 0, hx: 7.5, hy: 0.9, hz: 12 },
      { x: 10.5, y: -0.9, z: 0, hx: 7.5, hy: 0.9, hz: 12 },
      { x: 0, y: -0.25, z: 0, hx: 3.1, hy: 0.25, hz: 1.8, bridge: true },
    ],
    rocks: [],
  },
  island: {
    name: "浮岛角斗场",
    description: "四面临空。把对手打下去，也是一种胜利。",
    color: 0xc6b89a,
    platforms: [{ x: 0, y: -1, z: 0, hx: 15, hy: 1, hz: 10 }],
    rocks: [
      { x: 0, z: 4, r: 1.1 },
      { x: 0, z: -4, r: 1.1 },
    ],
  },
};
export const MISSIONS = [
  {
    name: "01 / 破阵",
    description: "用 900 军费，突破盾卫与长枪组成的防线。",
    map: "meadow",
    budget: 900,
    enemy: "phalanx",
  },
  {
    name: "02 / 桥头堡",
    description: "用 1,200 军费，攻下狭窄桥头。爆破可能是把双刃剑。",
    map: "bridge",
    budget: 1200,
    enemy: "battery",
  },
  {
    name: "03 / 巨人之岛",
    description: "用 1,500 军费，对抗双大锤。别让重锤把你扫下浮岛。",
    map: "island",
    budget: 1500,
    enemy: "titans",
  },
];
export function formation(preset = "balanced", team = "red") {
  const sign = team === "red" ? -1 : 1;
  const list = [];
  const line = (type, x, n, spacing = 1.65, stance = "advance") => {
    for (let i = 0; i < n; i++)
      list.push({ type, x: x * sign, z: (i - (n - 1) / 2) * spacing, stance });
  };
  if (preset === "phalanx") {
    line("shield", 5, 4);
    line("spear", 7, 5);
    line("archer", 10, 2);
  } else if (preset === "battery") {
    line("shield", 5, 3, 1);
    line("spear", 7, 3, 1.2);
    line("bomber", 10, 2, 2);
    line("archer", 12, 3, 1.5);
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
  } else {
    line("shield", 5, 3, 2);
    line("spear", 7, 4);
    line("archer", 10, 4);
    line("bomber", 12, 1);
  }
  return list;
}
export function cost(units) {
  return units.reduce((s, u) => s + (UNITS[u.type]?.cost || 0), 0);
}
export function groundAt(map, x, z) {
  return MAPS[map].platforms.some(
    (p) => Math.abs(x - p.x) <= p.hx - 0.1 && Math.abs(z - p.z) <= p.hz - 0.1,
  );
}
export function validateConfig(config) {
  if (!config || config.version !== VERSION)
    throw Error("不支持的文件版本，需要 " + VERSION);
  if (!MAPS[config.map]) throw Error("未知战场");
  if (
    !Number.isInteger(config.seed) ||
    config.seed < 0 ||
    config.seed > 0xffffffff
  )
    throw Error("随机种子须为 0–4294967295 的整数");
  if (
    !Number.isInteger(config.budget) ||
    config.budget < 100 ||
    config.budget > 10000
  )
    throw Error("军费须为 100–10000 的整数");
  for (const team of ["red", "blue"]) {
    const army = config[team];
    if (!Array.isArray(army) || army.length > MAX_UNITS)
      throw Error("每方最多 " + MAX_UNITS + " 个单位");
    if (cost(army) > config.budget) throw Error("阵型超出军费");
    for (const u of army) {
      if (!UNITS[u.type] || !["advance", "hold", "flank"].includes(u.stance))
        throw Error("兵种或姿态无效");
      if (
        !Number.isFinite(u.x) ||
        !Number.isFinite(u.z) ||
        Math.abs(u.z) > 10.8 ||
        Math.abs(u.x) > 16.8 ||
        !groundAt(config.map, u.x, u.z)
      )
        throw Error("布阵坐标越界");
      if (team === "red" ? u.x > -2.5 : u.x < 2.5)
        throw Error("请在本方半场布阵");
    }
  }
  return config;
}
export function defaultConfig(map = "meadow") {
  return {
    version: VERSION,
    map,
    seed: 20261116,
    budget: 1500,
    red: formation(),
    blue: formation("balanced", "blue"),
  };
}
