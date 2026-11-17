import { AI_STYLES } from "../tactics.js";
import { MAPS, UNITS, assertSafeData } from "../data.js";
import { runTournament } from "../tournament.js";
import { $, escape } from "./dom.js";

export function openTournament(app, actions) {
  if (app.playMode === "hotseat" && app.hotseatStage !== "revealed")
    return actions.toast("盲选阶段不可开启擂台");
  $("#modal").classList.add("tournament-modal");
  $("#modal").addEventListener(
    "close",
    () => $("#modal").classList.remove("tournament-modal"),
    { once: true },
  );
  const entrants = Object.keys(AI_STYLES).map((ai) => ({
    name: AI_STYLES[ai].name,
    ai,
  }));
  for (const [index, slot] of app.favorites.entries())
    if (slot)
      entrants.push({
        ...structuredClone(slot),
        name: `收藏 ${index + 1} · ${slot.name}`,
      });
  let controller = null,
    lastReport = null;
  actions.dialog(
    "演武擂台 · 循环赛排行",
    `<p>勾选 2–8 个参赛方。每对对手在各地图、各种子下交换红蓝各打一局。收藏和 JSON 仅在所属地图比赛，不会自动搬移到其他地形。</p><div id="arena-entrants" class="arena-entrants"></div><label class="action">添加 JSON 阵型<input id="arena-import" type="file" accept=".json,application/json"></label><label>JSON 使用阵营<select id="arena-import-team"><option value="red">红方</option><option value="blue">蓝方</option></select></label><fieldset><legend>地图集合</legend>${Object.entries(
      MAPS,
    )
      .map(
        ([id, m]) =>
          `<label><input type="checkbox" data-arena-map="${id}" ${id === app.config.map ? "checked" : ""}>${m.name}</label>`,
      )
      .join(
        "",
      )}</fieldset><label>随机种子（逗号分隔，最多 10 个）<input id="arena-seeds" value="42,43" inputmode="numeric"></label><label>每方军费<input id="arena-budget" type="number" min="100" max="10000" value="${app.config.budget}"></label><label><input id="arena-commander" type="checkbox">双方使用内置统帅军令</label><p>算策推演会增加赛程时间；计算按固定步数分片，可随时取消。排行按胜=1、平=0.5，余下按平均残余战力排序。</p><div class="arena-controls"><button id="arena-run" class="action">开始循环赛</button><button id="arena-cancel" disabled>取消计算</button><button id="arena-export" disabled>导出完整评测 JSON</button></div><p id="arena-progress" role="status">等待参赛方</p><div id="arena-results"></div>`,
  );
  const draw = () => {
    $("#arena-entrants").innerHTML = entrants
      .map(
        (e, i) =>
          `<label><input type="checkbox" data-entrant="${i}" ${i < 3 ? "checked" : ""}>${escape(e.name)} ${e.map ? "· " + escape(MAPS[e.map]?.name || e.map) : ""}</label>`,
      )
      .join("");
  };
  draw();
  $("#arena-import").onchange = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    try {
      if (file.size > 1024 * 1024) throw Error("JSON 不得超过 1 MB");
      if (entrants.length >= 12) throw Error("最多暂存 12 个参赛方");
      const raw = JSON.parse(await file.text());
      assertSafeData(raw);
      const team = $("#arena-import-team").value,
        units = Array.isArray(raw) ? raw : raw.units || raw[team];
      if (!Array.isArray(units)) throw Error("找不到所选阵营");
      entrants.push({
        name: file.name.slice(0, 48),
        units,
        props: (raw.props || []).filter((p) => !p.team || p.team === team),
        team,
        map: raw.map || app.config.map,
      });
      draw();
      actions.toast("JSON 已加入参赛列表，请勾选。");
    } catch (error) {
      actions.toast(error.message);
    }
  };
  $("#arena-cancel").onclick = () => controller?.abort();
  $("#modal").addEventListener("close", () => controller?.abort(), {
    once: true,
  });
  $("#arena-export").onclick = () =>
    lastReport && actions.saveJson("frontline-tournament.json", lastReport);
  $("#arena-run").onclick = async () => {
    const selected = [
        ...document.querySelectorAll("[data-entrant]:checked"),
      ].map((x) => entrants[Number(x.dataset.entrant)]),
      maps = [...document.querySelectorAll("[data-arena-map]:checked")].map(
        (x) => x.dataset.arenaMap,
      ),
      seeds = $("#arena-seeds")
        .value.split(",")
        .map((x) => Number(x.trim()));
    controller = new AbortController();
    $("#arena-run").disabled = true;
    $("#arena-cancel").disabled = false;
    $("#arena-results").textContent = "";
    $("#arena-export").disabled = true;
    try {
      lastReport = await runTournament(
        {
          entrants: selected,
          maps,
          seeds,
          budget: Number($("#arena-budget").value),
          commander: $("#arena-commander").checked,
        },
        {
          signal: controller.signal,
          onProgress: (p) => {
            if ($("#arena-progress"))
              $("#arena-progress").textContent =
                `${p.completed} / ${p.total} 局 · ${p.phase}`;
          },
        },
      );
      if (!$("#arena-results")) return;
      const f = (n) => n.toFixed(1),
        rank = lastReport.ranking
          .map(
            (r, i) =>
              `<tr><td>${i + 1}</td><td>${escape(r.name)}</td><td>${r.wins}/${r.draws}/${r.losses}</td><td>${f(r.winRate * 100)}%</td><td>${f(r.scoreRate * 100)}%</td><td>${f(r.averageRemaining)}</td><td>${f(r.averageTime)}s</td><td>${r.stableMvp ? UNITS[r.stableMvp].name : "—"}</td></tr>`,
          )
          .join(""),
        matrix = lastReport.matrix
          .map(
            (row, i) =>
              `<tr><th>${escape(lastReport.entrants[i].name)}</th>${row.map((r, j) => `<td>${i === j ? "—" : `${r.wins}/${r.draws}/${r.losses}`}</td>`).join("")}</tr>`,
          )
          .join("");
      $("#arena-results").innerHTML =
        `<h3>循环赛排行</h3><div class="table-scroll"><table><thead><tr><th>名次</th><th>参赛方</th><th>胜/平/负</th><th>胜率</th><th>得分率</th><th>平均残余战力</th><th>场均模拟用时</th><th>稳定 MVP 兵种</th></tr></thead><tbody>${rank}</tbody></table></div><h3>对阵矩阵 · 行对列的胜 / 平 / 负</h3><div class="table-scroll"><table><thead><tr><th>行 vs 列</th>${lastReport.entrants.map((e) => `<th>${escape(e.name)}</th>`).join("")}</tr></thead><tbody>${matrix}</tbody></table></div><p>MVP 为每局本方贡献最高兵种的众数；不代表所有地图与种子的总体强度。完整结果含每局战报，可导出。</p>`;
      $("#arena-export").disabled = false;
    } catch (error) {
      if ($("#arena-progress"))
        $("#arena-progress").textContent = error.message;
    } finally {
      if ($("#arena-run")) {
        $("#arena-run").disabled = false;
        $("#arena-cancel").disabled = true;
      }
    }
  };
}
