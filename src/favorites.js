import { VERSION, assertSafeData, validateConfig, canPlace } from "./data.js";
export const FAVORITE_SLOTS = 5;
export function saveFavorite(slots, index, name, input, team) {
  if (
    !Number.isInteger(index) ||
    index < 0 ||
    index >= FAVORITE_SLOTS ||
    !["red", "blue"].includes(team)
  )
    throw Error("收藏槽位或阵营无效");
  const config = validateConfig(input),
    list = Array.from({ length: 5 }, (_, i) => slots?.[i] || null);
  list[index] = {
    name: String(name).trim().slice(0, 32) || "自定义阵型",
    map: config.map,
    team,
    units: config[team],
    props: config.props.filter((p) => p.team === team),
  };
  return structuredClone(list);
}
export function loadFavorite(input, team, slot, limit = input.budget) {
  assertSafeData(slot);
  if (
    !slot ||
    !Array.isArray(slot.units) ||
    !Array.isArray(slot.props) ||
    slot.map !== input.map
  )
    throw Error("请先切换到收藏时的地图");
  const c = validateConfig(input),
    flip = slot.team === team ? 1 : -1;
  c[team] = slot.units.map((u) => ({ ...u, x: u.x * flip }));
  c.props = [
    ...c.props.filter((p) => p.team !== team),
    ...slot.props.map((p) => ({ ...p, team, x: p.x * flip })),
  ];
  // Trial budgets may be smaller than the shared simulation budget.
  validateConfig({ ...c, budget: limit });
  return validateConfig(c);
}
export function readFavorites(value) {
  assertSafeData(value);
  if (!Array.isArray(value) || value.length > 5) throw Error("收藏数据无效");
  return Array.from({ length: 5 }, (_, i) => {
    const s = value[i];
    if (!s) return null;
    if (
      typeof s.name !== "string" ||
      s.name.length > 32 ||
      !["red", "blue"].includes(s.team)
    )
      throw Error("收藏数据无效");
    validateConfig({
      version: VERSION,
      map: s.map,
      seed: 0,
      budget: 10000,
      red: s.team === "red" ? s.units : [],
      blue: s.team === "blue" ? s.units : [],
      props: s.props,
    });
    return structuredClone(s);
  });
}
