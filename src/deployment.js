import {
  UNITS,
  STANCES,
  MAX_UNITS,
  canPlace,
  teamCost,
  validateConfig,
} from "./data.js";

/** Build a mixed squad in local coordinates: depth faces away from the enemy. */
export function squad({
  roster,
  shape = "square",
  x,
  z,
  team = "red",
  mirror = false,
}) {
  if (
    !["square", "line", "wedge"].includes(shape) ||
    !["red", "blue"].includes(team)
  )
    throw Error("未知队形或阵营");
  const units = [];
  for (const r of roster) {
    if (
      !Object.hasOwn(UNITS, r.type) ||
      !Number.isInteger(r.count) ||
      r.count < 0 ||
      r.count > 60 ||
      !Object.hasOwn(STANCES, r.stance || "advance")
    )
      throw Error("无效的整队兵种、姿态或数量");
    for (let n = 0; n < r.count; n++)
      units.push({ type: r.type, stance: r.stance || "advance" });
  }
  if (!units.length || units.length > 60) throw Error("整队需要 1–60 人");
  const sign = team === "red" ? -1 : 1;
  let row = 0,
    index = 0;
  while (index < units.length) {
    const width =
      shape === "line"
        ? units.length
        : shape === "wedge"
          ? row * 2 + 1
          : Math.ceil(Math.sqrt(units.length));
    const n = Math.min(width, units.length - index);
    for (let j = 0; j < n; j++)
      Object.assign(units[index++], {
        x: x + sign * row * 2.5,
        z: z + (j - (n - 1) / 2) * 2.5,
      });
    row++;
  }
  return mirror
    ? [
        ...units,
        ...units
          .filter((u) => Math.abs(u.z) > 0.01)
          .map((u) => ({ ...u, z: -u.z })),
      ]
    : units;
}

// Atomic edit: failed moves/whole squads leave the input untouched.
export function placeSquad(input, team, additions, remove = []) {
  const c = structuredClone(validateConfig(input)),
    removing = new Set(remove);
  c[team] = c[team].filter((_, i) => !removing.has(i));
  if (c[team].length + additions.length > MAX_UNITS)
    throw Error("每方最多 60 人");
  for (const u of additions) {
    if (!canPlace(c, team, u))
      throw Error(
        "整队与现有单位、道具或地形重叠，或超出半场；请调整锚点、人数或队形",
      );
    c[team].push({ ...u, stance: u.stance || "advance" });
  }
  if (teamCost(c, team) > c.budget) throw Error("整队超出军费");
  return validateConfig(c);
}
export function moveSquad(input, team, indices, dx, dz) {
  if (
    !indices.length ||
    indices.some((i) => !Number.isInteger(i) || !input[team][i]) ||
    new Set(indices).size !== indices.length
  )
    throw Error("没有有效的选中部队");
  return placeSquad(
    input,
    team,
    indices.map((i) => ({
      ...input[team][i],
      x: input[team][i].x + dx,
      z: input[team][i].z + dz,
    })),
    indices,
  );
}

/** Hotseat privacy is a UI handoff, not encryption against browser developer tools. */
export function hotseatView(config, stage) {
  const visible = structuredClone(config);
  if (stage !== "revealed") {
    const t = stage === "red" ? "red" : stage === "blue" ? "blue" : null;
    for (const side of ["red", "blue"]) if (side !== t) visible[side] = [];
    visible.props = visible.props.filter((p) => p.team === t);
  }
  return visible;
}
export function nextHotseat(stage, config) {
  if (stage === "red") {
    if (!config.red.length) throw Error("红方至少需要一名士兵");
    return "handoff";
  }
  if (stage === "handoff") return "blue";
  if (stage === "blue") {
    if (!config.blue.length) throw Error("蓝方至少需要一名士兵");
    validateConfig(config);
    return "revealed";
  }
  throw Error("盲选已经结束");
}
