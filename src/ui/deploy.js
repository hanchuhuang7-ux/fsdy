import { diagnoseFormation } from "../diagnostics.js";

import { saveFavorite, loadFavorite } from "../favorites.js";

import { squad, placeSquad, hotseatView } from "../deployment.js";

import {
  UNITS,
  PROPS,
  MAPS,
  MISSIONS,
  MAX_PROPS,
  formation,
  cost,
  teamCost,
} from "../data.js";
import { $, escape } from "./dom.js";

export function createDeploy(app, actions) {
  function budget() {
    return app.missionIndex === null
      ? app.config.budget
      : MISSIONS[app.missionIndex].budget;
  }
  function renderCatalog() {
    const definitions = app.catalog === "units" ? UNITS : PROPS,
      choice = app.catalog === "units" ? app.selected : app.selectedProp;
    $("#unit-cards").innerHTML = Object.entries(definitions)
      .map(
        ([id, d]) =>
          `<button class="unit-card ${id === choice ? "active" : ""}" data-type="${id}" title="${d.description}"><span class="glyph">${d.glyph}</span><span><strong>${d.name}</strong><small>${d.subtitle}</small></span><b>${d.cost} ◇</b></button>`,
      )
      .join("");
    document.querySelectorAll("[data-type]").forEach(
      (b) =>
        (b.onclick = () => {
          if (app.catalog === "units") app.selected = b.dataset.type;
          else app.selectedProp = b.dataset.type;
          document.body.classList.remove("compact-catalog-open");
          const toggle = $(".compact-panel-toggle");
          if (toggle) toggle.textContent = "⚑ 兵种 / 布阵";
          actions.updatePanel();
        }),
    );
    $("#catalog-units").classList.toggle("active", app.catalog === "units");
    $("#catalog-props").classList.toggle("active", app.catalog === "props");
    $("#catalog-hint").textContent =
      app.catalog === "units" ? "点选后在本方场地放置" : "道具与兵种共用军费";
  }
  function updatePanel() {
    $("#touch-mode").hidden = app.mode !== "deploy";
    const visible =
      app.playMode === "hotseat"
        ? hotseatView(app.config, app.hotseatStage)
        : app.config;
    $("#tactical-hint").textContent = diagnoseFormation(visible, app.team);
    $("#start").disabled = app.planning;
    const available = actions.budget() - teamCost(app.config, app.team);
    $("#money").innerHTML =
      available.toLocaleString() +
      `<small> / ${actions.budget().toLocaleString()}</small>`;
    $("#budget-fill").style.width =
      `${Math.max(0, (available / actions.budget()) * 100)}%`;
    if (app.mode === "deploy") {
      for (const t of ["red", "blue"])
        $("#" + t + "-count").textContent =
          app.playMode === "hotseat" &&
          app.hotseatStage !== "revealed" &&
          app.hotseatStage !== t
            ? "?"
            : app.config[t].length;
    }
    $("#team-red").classList.toggle("active", app.team === "red");
    $("#team-blue").classList.toggle("active", app.team === "blue");
    $("#team-red").disabled =
      app.playMode === "hotseat" && app.hotseatStage !== "red";
    $("#team-blue").disabled =
      app.missionIndex !== null ||
      app.playMode === "human-ai" ||
      (app.playMode === "hotseat" && app.hotseatStage !== "blue");
    document.querySelectorAll(".unit-card").forEach((b) => {
      const definitions = app.catalog === "units" ? UNITS : PROPS,
        choice = app.catalog === "units" ? app.selected : app.selectedProp;
      b.classList.toggle("active", b.dataset.type === choice);
      b.disabled =
        app.mode !== "deploy" ||
        app.playMode === "ai-ai" ||
        available < definitions[b.dataset.type].cost;
    });
    for (const id of ["preset", "clear", "undo", "stance", "map"])
      $("#" + id).disabled =
        app.mode !== "deploy" ||
        (app.playMode === "ai-ai" && id !== "map") ||
        (id === "map" &&
          (app.missionIndex !== null || app.playMode === "hotseat")) ||
        (id === "stance" && app.catalog === "props");
    $("#unit-detail").textContent =
      app.catalog === "units"
        ? UNITS[app.selected].description
        : PROPS[app.selectedProp].description;
    $("#prop-count").textContent =
      `道具 ${(app.config.props || []).filter((p) => p.team === app.team).length} / ${MAX_PROPS}`;
    $("#map").value = app.config.map;
    $("#map-description").textContent = MAPS[app.config.map].description;
    $("#terrain-info").textContent = MAPS[app.config.map].hint + " ↗";
    $("#sandbox").classList.toggle("active", app.missionIndex === null);
    $("#campaign").classList.toggle("active", app.missionIndex !== null);
    $("#mission").classList.toggle("hidden", app.missionIndex === null);
    if (app.missionIndex !== null) {
      const m = MISSIONS[app.missionIndex];
      $("#mission").innerHTML =
        `<strong>${m.name}</strong><p>${m.description}</p>`;
    }
    $("#bo3-enabled").disabled =
      app.mode !== "deploy" ||
      app.missionIndex !== null ||
      !!app.series?.rounds.length;
    $("#commander-enabled").disabled =
      app.mode !== "deploy" || !!app.series?.rounds.length;
    $("#commander-enabled").checked = app.config.commander?.enabled || false;
    if (app.series?.rounds.length) {
      $("#map").disabled = true;
      $("#play-mode").disabled = true;
    }
    $("#series-status").textContent = app.series
      ? `Bo3 · 红 ${app.series.wins.red} : ${app.series.wins.blue} 蓝 · ${app.series.complete ? "已结束" : "第 " + (app.series.rounds.length + 1) + " 局"}`
      : $("#bo3-enabled").checked
        ? "Bo3 · 等待第 1 局"
        : "单局推演";
    $("#restart-series").classList.toggle("hidden", !app.series);
    $("#round-label").classList.toggle("hidden", !app.series);
    $("#round-label").textContent = $("#series-status").textContent;
    $("#play-mode").value = app.playMode;
    $("#play-mode").disabled =
      app.mode !== "deploy" || app.missionIndex !== null;
    $(".ai-settings").classList.toggle(
      "hidden",
      !["human-ai", "ai-ai"].includes(app.playMode),
    );
    $("#red-ai").disabled = app.playMode !== "ai-ai" || app.mode !== "deploy";
    for (const id of ["blue-ai", "generate-ai"])
      $("#" + id).disabled = app.mode !== "deploy";
    $("#workshop").disabled =
      app.playMode === "hotseat" && app.hotseatStage !== "revealed";
    $("#mode-description").textContent = {
      sandbox: "编辑双方，试验你的战术。",
      "human-ai": "你指挥红方。开战前蓝方 AI 依据公开编队完成反制。",
      "ai-ai": "双方各侦察一次公开草案，再同时定阵；点击开战自动演武。",
      hotseat:
        app.hotseatStage === "red"
          ? "红方秘密布阵，完成后封存并交接。"
          : app.hotseatStage === "blue"
            ? "蓝方秘密布阵；红方兵力、道具与数量已隐藏。"
            : "封存编队，等待揭晓。",
    }[app.playMode];
    for (const id of [
      "squad-builder",
      "select-tool",
      "select-type",
      "move-selection",
      "delete-selection",
      "apply-stance",
      "clear-selection",
    ])
      $("#" + id).disabled = app.mode !== "deploy" || app.playMode === "ai-ai";
    $("#selection-info").textContent = app.selection.length
      ? `已选 ${app.selection.length} 人 · ${app.editTool === "move" ? "点击目标位置移动" : "可移动、删除或改姿态"}`
      : app.editTool === "select"
        ? "拖动框选本方部队；点击可逐个选择"
        : "未选中部队";
    $("#select-tool").classList.toggle("active", app.editTool === "select");
    if (app.mode === "deploy" && app.playMode === "hotseat")
      $("#start").innerHTML =
        app.hotseatStage === "red"
          ? "封存红方 →"
          : app.hotseatStage === "blue"
            ? "揭晓并开战 ↗"
            : "等待交接";
    if (app.series?.rounds.length) {
      $("#map").disabled = true;
      $("#play-mode").disabled = true;
    }
    $("#favorites").disabled =
      app.mode !== "deploy" || app.playMode === "ai-ai";
    if ($(".compact-panel-toggle"))
      $(".compact-panel-toggle").disabled = app.mode !== "deploy";
  }
  function favoriteDialog() {
    if (!actions.editable()) return;
    actions.dialog(
      "常用阵型收藏夹",
      `<p>保存当前阵营的士兵和道具，共五槽。载入时检查当前地图与军费；载入到另一阵营会镜像 x 坐标。试炼仍遵守玩家军费。</p>${app.favorites.map((s, i) => `<div class="favorite-slot"><span><strong>槽 ${i + 1} · ${s ? escape(s.name) : "空"}</strong><br>${s ? escape(MAPS[s.map].name) + " · " + s.units.length + " 人" : ""}</span><button data-load-favorite="${i}" ${s ? "" : "disabled"}>载入</button><button data-save-favorite="${i}">保存本方</button></div>`).join("")}<label>收藏名称 <input id="favorite-name" maxlength="32" value="我的阵型"></label>`,
    );
    document.querySelectorAll("[data-save-favorite]").forEach(
      (b) =>
        (b.onclick = () => {
          try {
            app.favorites = saveFavorite(
              app.favorites,
              Number(b.dataset.saveFavorite),
              $("#favorite-name").value,
              app.config,
              app.team,
            );
            localStorage.setItem(
              "frontline-favorites-v1",
              JSON.stringify(app.favorites),
            );
            actions.favoriteDialog();
            actions.toast("阵型已存入收藏槽。");
          } catch (e) {
            actions.toast(e.message);
          }
        }),
    );
    document.querySelectorAll("[data-load-favorite]").forEach(
      (b) =>
        (b.onclick = () => {
          try {
            const next = loadFavorite(
              app.config,
              app.team,
              app.favorites[Number(b.dataset.loadFavorite)],
              actions.budget(),
            );
            actions.commit();
            app.config = next;
            app.selection = [];
            app.editTool = "place";
            actions.redraw();
            $("#modal").close();
            app.audio.play("place");
            actions.toast("收藏阵型已载入。");
          } catch (e) {
            actions.toast(e.message);
          }
        }),
    );
  }
  function buildSquad() {
    if (!actions.editable()) return;
    actions.dialog(
      "混编整队与镜像排阵",
      `<p>各兵种按列表顺序排入队形。锚点是队伍前沿中心；后排向己方纵深延伸。整队通过全部间距与军费检查后才放置。</p><div class="squad-roster">${Object.entries(
        UNITS,
      )
        .map(
          ([id, d]) =>
            `<label>${d.name} · ${d.cost}<input aria-label="${d.name}人数" data-squad="${id}" type="number" min="0" max="60" value="${id === app.selected ? 3 : 0}"></label>`,
        )
        .join(
          "",
        )}</div><div class="squad-form"><label>队形 <select id="squad-shape"><option value="square">方阵</option><option value="line">横排</option><option value="wedge">楔形阵</option></select></label><label>锚点 x <input id="squad-x" type="number" step="0.5" value="${app.team === "red" ? -6 : 6}"></label><label>锚点 z <input id="squad-z" type="number" step="0.5" value="0"></label><label><input id="squad-mirror" type="checkbox"> 沿 z=0 镜像追加</label></div><p>新单位使用左侧当前姿态；镜像队伍请把锚点移向一侧，避免与原队重叠。</p><button class="action" id="place-squad">整队放置</button>`,
    );
    $("#place-squad").onclick = () => {
      try {
        const roster = [...document.querySelectorAll("[data-squad]")].map(
          (e) => ({
            type: e.dataset.squad,
            count: Number(e.value),
            stance: $("#stance").value,
          }),
        );
        const additions = squad({
          roster,
          shape: $("#squad-shape").value,
          x: Number($("#squad-x").value),
          z: Number($("#squad-z").value),
          team: app.team,
          mirror: $("#squad-mirror").checked,
        });
        const next = placeSquad(app.config, app.team, additions);
        if (teamCost(next, app.team) > actions.budget())
          throw Error("超出试炼军费");
        actions.commit();
        app.config = next;
        app.selection = [];
        app.editTool = "place";
        actions.redraw();
        $("#modal").close();
        app.audio.play("place");
        actions.toast(`已放置 ${additions.length} 名士兵。`);
      } catch (e) {
        actions.toast(e.message);
      }
    };
  }
  function choosePreset() {
    if (!actions.editable()) return;
    actions.dialog(
      "选择阵型",
      `<p>为${app.team === "red" ? "朱砂军" : "靛蓝军"}招募一支预设军队。军费不足时会按顺序精简。</p>${[
        ["balanced", "均衡战线", "盾卫 + 长枪 + 弓手 + 爆破"],
        ["rush", "人海冲锋", "游勇与长枪，多点压迫"],
        ["volley", "火力纵深", "重盾掩护，弓箭与爆破齐发"],
        ["cavalry", "骑兵突击", "五骑冲锋，弩手与军医协同"],
        ["support", "稳固阵线", "盾枪掩护，弩手与双军医持续支援"],
        ["siege", "遗迹攻坚", "重盾、弩手与三辆投石车"],
      ]
        .map(
          ([id, name, desc]) =>
            `<button class="mission-card" data-preset="${id}"><strong>${name}</strong><span>${desc}</span></button>`,
        )
        .join("")}`,
    );
    document.querySelectorAll("[data-preset]").forEach(
      (b) =>
        (b.onclick = () => {
          actions.commit();
          app.config[app.team] = [];
          for (const u of formation(b.dataset.preset, app.team))
            if (
              teamCost(app.config, app.team) + UNITS[u.type].cost <=
              actions.budget()
            )
              app.config[app.team].push(u);
          actions.redraw();
          $("#modal").close();
          actions.toast("阵型已就位。");
        }),
    );
  }
  function chooseMission() {
    if (app.mode === "battle") return actions.toast("请先返回布阵。");
    actions.dialog(
      "战术试炼",
      `<p>对手阵型固定，用有限军费找出解法。可以反复调整，胜利进度会保存在此浏览器。</p>${MISSIONS.map(
        (m, i) => {
          let won = false;
          try {
            won = localStorage.getItem("frontline-mission-" + i) === "won";
          } catch {}
          return `<button class="mission-card" data-mission="${i}"><strong>${m.name} ${won ? "✓" : ""}</strong><span>${m.description}</span></button>`;
        },
      ).join("")}`,
    );
    document
      .querySelectorAll("[data-mission]")
      .forEach(
        (b) =>
          (b.onclick = () => actions.loadMission(Number(b.dataset.mission))),
      );
  }
  return {
    budget,
    renderCatalog,
    updatePanel,
    favoriteDialog,
    buildSquad,
    choosePreset,
    chooseMission,
  };
}
