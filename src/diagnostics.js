import { UNITS, heightAt } from "./data.js";
import { scoutArmy } from "./tactics.js";
/** The caller must pass its visible deployment, never the sealed underlying roster. */
export function diagnoseFormation(config, team = "red") {
  const own = config[team] || [],
    enemy = config[team === "red" ? "blue" : "red"] || [];
  if (!own.length) return "先招募前排与输出部队，再观察阵型协同。";
  const front = own.filter((u) =>
    ["shield", "giant", "spear", "militia"].includes(u.type),
  );
  if (front.length < own.length * 0.25)
    return "前排承伤偏少，远程与军医容易直接暴露。";
  const scout = scoutArmy(own);
  if (scout.clustered) return "远程站位密集，容易被投石车或爆破集中重创。";
  const ranged = own.filter((u) => UNITS[u.type].ranged);
  if (
    config.map === "highland" &&
    ranged.length &&
    ranged.every((u) => heightAt(config.map, u.x, u.z) < 1)
  )
    return "高台射程优势尚未利用，可把远程部署到坡顶。";
  if (
    enemy.filter((u) => u.type === "cavalry").length >= 2 &&
    !own.some((u) => u.type === "spear")
  )
    return "公开敌军有骑兵突进，可考虑长枪与拒马护住侧翼。";
  return "阵型前后排基本齐备；留出冲锋距离与分散间距。";
}
