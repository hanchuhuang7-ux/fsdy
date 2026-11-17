import { assertSafeData } from "./data.js";
export const HIGHLIGHT_LIMIT = 24;
export const HIGHLIGHT_LABELS = {
  order: "军令释放",
  firstKill: "首名阵亡",
  blast: "范围重创",
  reversal: "局势反超",
};
export function validateHighlights(input = [], maxTick = 5400) {
  assertSafeData(input);
  if (!Array.isArray(input) || input.length > HIGHLIGHT_LIMIT)
    throw Error("高光记录超限");
  let previous = -1;
  return input.map((e, i) => {
    if (
      !e ||
      e.id !== i + 1 ||
      !Object.hasOwn(HIGHLIGHT_LABELS, e.type) ||
      !["red", "blue", "neutral"].includes(e.team) ||
      !Number.isInteger(e.tick) ||
      e.tick < previous ||
      e.tick > maxTick ||
      !Number.isFinite(e.value) ||
      e.value < 0 ||
      e.value > 1e7
    )
      throw Error("高光记录无效");
    previous = e.tick;
    return {
      id: e.id,
      type: e.type,
      team: e.team,
      tick: e.tick,
      value: e.value,
    };
  });
}
/** Events occur inside a tick: seek its first completed recorded frame, or the final partial frame. */
export function highlightFrameIndex(frames, tick) {
  let lo = 0,
    hi = frames.length - 1;
  while (lo < hi) {
    const m = (lo + hi) >> 1;
    if (Math.round(frames[m].time * 60) <= tick) lo = m + 1;
    else hi = m;
  }
  return lo;
}
export function highlightText(e) {
  return `${(e.tick / 60).toFixed(1)}s · ${e.team === "red" ? "红方" : e.team === "blue" ? "蓝方" : "战场"} ${HIGHLIGHT_LABELS[e.type]}${e.type === "blast" ? " · " + Math.round(e.value) + " 伤害" : ""}`;
}
