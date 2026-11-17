import { assertSafeData, combatPower } from "./data.js";
export const AGENT_SCHEMA = "frontline-agent@1";
export const OBSERVATION_INTERVAL = 60;
export function observeBattle(battle, team, requestId, round = 1) {
  if (!["red", "blue"].includes(team)) throw Error("Agent 阵营无效");
  return {
    schema: AGENT_SCHEMA,
    kind: "observation",
    requestId,
    team,
    round,
    tick: battle.tick,
    interval: OBSERVATION_INTERVAL,
    map: battle.config.map,
    units: battle.units
      .filter((u) => u.alive)
      .map((u) => {
        const p = u.body.translation();
        return {
          id: u.id,
          team: u.team,
          type: u.type,
          x: p.x,
          y: p.y,
          z: p.z,
          hp: u.hp,
          maxHp: u.maxHp,
        };
      }),
    props: battle.props.map((p) => ({
      id: p.id,
      team: p.team,
      type: p.type,
      x: p.x,
      z: p.z,
      hp: Math.max(0, p.hp),
      alive: p.alive,
    })),
    commander: {
      enabled: battle.config.commander.enabled,
      points: { ...battle.orderState.points },
      readyAt: { ...battle.orderState.readyAt },
    },
    power: combatPower(battle.units),
  };
}
export function acceptAgentCommand(battle, team, requestId, response) {
  try {
    assertSafeData(response);
    if (
      !response ||
      Object.keys(response).some(
        (k) => !["schema", "kind", "requestId", "team", "order"].includes(k),
      ) ||
      response.schema !== AGENT_SCHEMA ||
      response.kind !== "command" ||
      response.team !== team ||
      response.requestId !== requestId ||
      !Object.hasOwn(response, "order")
    )
      throw Error("协议、请求编号或阵营不匹配");
    if (response.order === null) return { ok: true, skipped: true };
    if (
      !response.order ||
      Object.keys(response.order).some((k) => !["type", "x", "z"].includes(k))
    )
      throw Error("只接受军令 type/x/z");
    return battle.issueOrder(team, response.order);
  } catch (e) {
    return { ok: false, reason: e.message };
  }
}
