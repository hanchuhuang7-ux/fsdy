import {
  VERSION,
  MAPS,
  UNITS,
  validateConfig,
  defaultConfig,
  assertSafeData,
} from "./data.js";
import { Battle, initPhysics } from "./engine.js";
import { AI_STYLES, aiDuelAsync, withAIAsync } from "./tactics.js";

export function normalizeEntrant(raw, index = 0) {
  assertSafeData(raw);
  if (
    !raw ||
    typeof raw.name !== "string" ||
    !raw.name.trim() ||
    raw.name.length > 48
  )
    throw Error("参赛名称无效");
  if (raw.ai) {
    if (!Object.hasOwn(AI_STYLES, raw.ai)) throw Error("未知 AI");
    return { id: String(index), name: raw.name, ai: raw.ai };
  }
  const roster = raw.units || raw.config?.[raw.team || "red"];
  if (
    !Array.isArray(roster) ||
    roster.length < 1 ||
    roster.length > 60 ||
    !["red", "blue"].includes(raw.team || "red")
  )
    throw Error("阵型参赛方无效");
  return {
    id: String(index),
    name: raw.name,
    units: structuredClone(roster),
    props: structuredClone(
      raw.props ||
        raw.config?.props?.filter((p) => p.team === (raw.team || "red")) ||
        [],
    ),
    team: raw.team || "red",
    map: raw.map || raw.config?.map || null,
  };
}
function placeEntrant(config, entrant, team) {
  if (entrant.map && entrant.map !== config.map)
    throw Error(entrant.name + " 只适用于 " + entrant.map + "；请选择共同地图");
  const flip = entrant.team === team ? 1 : -1;
  return validateConfig({
    ...config,
    [team]: entrant.units.map((u) => ({ ...u, x: u.x * flip })),
    props: [
      ...config.props.filter((p) => p.team !== team),
      ...entrant.props.map((p) => ({ ...p, team, x: p.x * flip })),
    ],
  });
}
export function summarizeTournament(entrants, matches) {
  const empty = () => ({
      wins: 0,
      draws: 0,
      losses: 0,
      games: 0,
      remaining: 0,
      time: 0,
      mvps: {},
    }),
    matrix = entrants.map(() => entrants.map(empty)),
    totals = entrants.map(empty);
  for (const m of matches) {
    if (
      !Number.isInteger(m.red) ||
      !Number.isInteger(m.blue) ||
      !entrants[m.red] ||
      !entrants[m.blue] ||
      m.red === m.blue ||
      !["red", "blue", "draw"].includes(m.result?.winner) ||
      !Number.isFinite(m.result.time) ||
      m.result.time < 0
    )
      throw Error("比赛统计输入无效");
    for (const [side, index, opponent] of [
      ["red", m.red, m.blue],
      ["blue", m.blue, m.red],
    ]) {
      const remaining = m.result.remainingValue?.[side];
      if (!Number.isFinite(remaining) || remaining < 0)
        throw Error("残余战力无效");
      const byType = m.result.report?.teams?.[side]?.byType || {};
      const mvp = Object.entries(byType)
        .filter(([t]) => Object.hasOwn(UNITS, t))
        .sort(
          (a, b) =>
            b[1].damageDealt -
              2 * (b[1].friendlyFire || 0) +
              0.75 * b[1].healing -
              (a[1].damageDealt -
                2 * (a[1].friendlyFire || 0) +
                0.75 * a[1].healing) || a[0].localeCompare(b[0]),
        )[0]?.[0];
      for (const out of [matrix[index][opponent], totals[index]]) {
        out.games++;
        out[
          m.result.winner === "draw"
            ? "draws"
            : m.result.winner === side
              ? "wins"
              : "losses"
        ]++;
        out.remaining += remaining;
        out.time += m.result.time;
        if (mvp) out.mvps[mvp] = (out.mvps[mvp] || 0) + 1;
      }
    }
  }
  const finish = (r) => ({
    ...r,
    winRate: r.games ? r.wins / r.games : 0,
    scoreRate: r.games ? (r.wins + 0.5 * r.draws) / r.games : 0,
    averageRemaining: r.games ? r.remaining / r.games : 0,
    averageTime: r.games ? r.time / r.games : 0,
    stableMvp:
      Object.entries(r.mvps).sort(
        (a, b) => b[1] - a[1] || a[0].localeCompare(b[0]),
      )[0]?.[0] || null,
  });
  return {
    schema: "frontline-tournament@1",
    engine: VERSION,
    matches,
    entrants,
    matrix: matrix.map((row) => row.map(finish)),
    ranking: totals
      .map((r, i) => ({ ...entrants[i], ...finish(r) }))
      .sort(
        (a, b) =>
          b.scoreRate - a.scoreRate ||
          b.averageRemaining - a.averageRemaining ||
          Number(a.id) - Number(b.id),
      ),
  };
}
export async function runTournament(
  {
    entrants: raw,
    maps = ["meadow"],
    seeds = [42],
    budget = 2200,
    commander = false,
  },
  { onProgress = () => {}, signal } = {},
) {
  if (
    !Array.isArray(raw) ||
    raw.length < 2 ||
    raw.length > 8 ||
    !Array.isArray(maps) ||
    !maps.length ||
    maps.length > 6 ||
    new Set(maps).size !== maps.length ||
    maps.some((m) => !Object.hasOwn(MAPS, m)) ||
    !Array.isArray(seeds) ||
    !seeds.length ||
    seeds.length > 10 ||
    new Set(seeds).size !== seeds.length ||
    seeds.some((s) => !Number.isInteger(s) || s < 0 || s > 0xffffffff)
  )
    throw Error("擂台需要 2–8 方、1–6 地图、1–10 个唯一种子");
  const entrants = raw.map(normalizeEntrant),
    matches = [],
    total =
      entrants.length * (entrants.length - 1) * maps.length * seeds.length;
  if (total > 600) throw Error("本次赛程超过 600 局，请缩小范围");
  await initPhysics();
  const abort = () => {
    if (signal?.aborted) throw Error("擂台已取消");
  };
  // Home/away on every map/seed avoids treating the red side as an entrant advantage.
  for (let a = 0; a < entrants.length; a++)
    for (let b = a + 1; b < entrants.length; b++)
      for (const map of maps)
        for (const seed of seeds)
          for (const [red, blue] of [
            [a, b],
            [b, a],
          ]) {
            abort();
            let c = {
              ...defaultConfig(map),
              seed,
              budget,
              red: [],
              blue: [],
              props: [],
              commander: {
                enabled: commander,
                aiTeams: commander ? ["red", "blue"] : [],
              },
            };
            const r = entrants[red],
              l = entrants[blue],
              control = {
                signal,
                onProgress: (p) =>
                  onProgress({
                    completed: matches.length,
                    total,
                    phase: p.phase,
                  }),
              };
            if (r.ai && l.ai)
              c = await aiDuelAsync(c, r.ai, l.ai, null, control);
            else {
              for (const [t, e] of [
                ["red", r],
                ["blue", l],
              ])
                if (!e.ai) c = placeEntrant(c, e, t);
              for (const [t, e] of [
                ["red", r],
                ["blue", l],
              ])
                if (e.ai)
                  c = await withAIAsync(c, t, e.ai, undefined, null, control);
            }
            c = validateConfig(c);
            const battle = new Battle(c);
            try {
              while (!battle.result) {
                battle.step();
                if (battle.tick % 120 === 0) {
                  abort();
                  onProgress({
                    completed: matches.length,
                    total,
                    phase: "对局",
                    tick: battle.tick,
                  });
                  await new Promise((resolve) => setTimeout(resolve, 0));
                }
              }
              matches.push({
                red,
                blue,
                map,
                seed,
                result: structuredClone(battle.result),
              });
            } finally {
              battle.dispose();
            }
            onProgress({ completed: matches.length, total, phase: "完成" });
          }
  return summarizeTournament(entrants, matches);
}
