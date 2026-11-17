import { bindBattlefield } from "./battlefield.js";
import { shortcutAction } from "./shortcuts.js";

import { readFavorites } from "../favorites.js";
import { AI_STYLES } from "../tactics.js";
import { moveSquad, hotseatView, nextHotseat } from "../deployment.js";

import {
  UNITS,
  PROPS,
  MAPS,
  MAX_UNITS,
  MAX_PROPS,
  cost,
  teamCost,
  validateConfig,
  defaultConfig,
  STANCES,
  ORDERS,
} from "../data.js";
import { $, clone, storageKey } from "./dom.js";

export function createEvents(app, actions) {
  function initializeUI() {
    try {
      app.favorites = readFavorites(
        JSON.parse(localStorage.getItem("frontline-favorites-v1") || "[]"),
      );
    } catch {}
    try {
      const p = JSON.parse(localStorage.getItem("frontline-feedback") || "{}");
      if (p.volume !== undefined) app.audio.setVolume(p.volume);
      app.audio.setMuted(p.muted);
      $("#camera-shake").checked = p.shake !== false;
    } catch {}
    $("#volume").value = Math.round(app.audio.volume * 100);
    const saveFeedback = () => {
      try {
        localStorage.setItem(
          "frontline-feedback",
          JSON.stringify({
            volume: app.audio.volume,
            muted: app.audio.muted,
            shake: $("#camera-shake").checked,
          }),
        );
      } catch {}
    };
    const syncAudio = () => {
      $("#audio-toggle").textContent = app.audio.muted ? "♫×" : "♫";
      $("#audio-toggle").setAttribute("aria-pressed", String(app.audio.muted));
      $("#audio-toggle").title = app.audio.muted ? "开启声音" : "静音";
    };
    syncAudio();
    document.addEventListener(
      "pointerdown",
      () => {
        if (!app.audio.context || app.audio.context.state === "suspended")
          app.audio.unlock();
      },
      { capture: true },
    );
    $("#audio-toggle").onclick = () => {
      app.audio.setMuted(!app.audio.muted);
      syncAudio();
      saveFeedback();
    };
    $("#volume").oninput = (e) => {
      app.audio.setVolume(Number(e.target.value) / 100);
      saveFeedback();
    };
    for (const id of ["red-ai", "blue-ai"])
      $("#" + id).innerHTML = Object.entries(AI_STYLES)
        .map(([key, s]) => `<option value="${key}">${s.name}</option>`)
        .join("");
    $("#red-ai").value = "guardian";
    $("#blue-ai").value = "counter";
    const cards = $("#unit-cards");
    const detail = document.createElement("div");
    detail.id = "unit-detail";
    cards.after(detail);
    actions.renderCatalog();
    $("#map").innerHTML = Object.entries(MAPS)
      .map(([id, m]) => `<option value="${id}">${m.name}</option>`)
      .join("");
    try {
      const stored = JSON.parse(localStorage.getItem(storageKey));
      if (stored) app.config = validateConfig(stored);
    } catch {}

    return { saveFeedback };
  }
  function bindEvents() {
    $("#team-red").onclick = () => {
      if (!actions.editable()) return;
      app.team = "red";
      app.selection = [];
      actions.redraw();
    };
    $("#catalog-units").onclick = () => {
      app.catalog = "units";
      actions.renderCatalog();
      actions.updatePanel();
    };
    $("#catalog-props").onclick = () => {
      app.catalog = "props";
      actions.renderCatalog();
      actions.updatePanel();
    };
    $("#quality").onchange = (e) => {
      app.view.setQuality(e.target.value);
      try {
        localStorage.setItem("frontline-quality", e.target.value);
      } catch {}
    };
    $("#terrain-info").onclick = actions.terrainInfo;
    $("#camera-shake").onchange = (e) => {
      app.view.shakeEnabled = e.target.checked;
      actions.saveFeedback();
    };
    $("#camera-preset").onchange = (e) => app.view.cameraPreset(e.target.value);
    $("#viewport").addEventListener("camera-manual", () => {
      $("#camera-preset").value = "overview";
    });
    $("#favorites").onclick = actions.favoriteDialog;
    $("#commander-enabled").onchange = (e) => {
      app.config.commander = { enabled: e.target.checked, aiTeams: [] };
      actions.persist();
    };
    $("#bo3-enabled").onchange = () => {
      app.series = null;
      actions.updatePanel();
    };
    $("#restart-series").onclick = () => {
      if (app.mode === "battle") return actions.toast("请先返回布阵。");
      const initial = app.series?.initial;
      app.series = null;
      actions.reset();
      if (initial) app.config = clone(initial);
      actions.redraw();
    };
    $("#command-team").onchange = () => {
      app.pendingOrder = null;
      actions.updateCommandUI();
    };
    document.querySelectorAll("[data-order]").forEach(
      (b) =>
        (b.onclick = () => {
          app.pendingOrder = b.dataset.order;
          actions.updateCommandUI();
          actions.toast(
            "点选战场地面，指定" + ORDERS[app.pendingOrder].name + "区域。",
          );
        }),
    );
    $("#cancel-order").onclick = () => {
      app.pendingOrder = null;
      app.view.preview(app.config, null);
      actions.updateCommandUI();
    };
    const compact = document.createElement("button");
    compact.className = "compact-panel-toggle";
    compact.textContent = "⚑ 兵种 / 布阵";
    compact.onclick = () => {
      const open = document.body.classList.toggle("compact-catalog-open");
      compact.textContent = open ? "收起兵种 ×" : "⚑ 兵种 / 布阵";
      if (open) {
        app.catalog = "units";
        actions.renderCatalog();
        actions.updatePanel();
        window.scrollTo({ top: 0, behavior: "smooth" });
      }
    };
    document.body.append(compact);
    $("#team-blue").onclick = () => {
      if (!actions.editable()) return;
      app.team = "blue";
      app.selection = [];
      actions.redraw();
    };
    $("#map").onchange = (e) => actions.changeMap(e.target.value);
    $("#play-mode").onchange = (e) => actions.changePlayMode(e.target.value);
    $("#generate-ai").onclick = () => {
      actions.commit();
      actions.refreshAI();
    };
    for (const id of ["red-ai", "blue-ai"])
      $("#" + id).onchange = () => actions.refreshAI();
    $("#hotseat-ready").onclick = () => {
      app.hotseatStage = nextHotseat(app.hotseatStage, app.config);
      app.team = "blue";
      app.history = [];
      $("#hotseat-cover").classList.add("hidden");
      actions.redraw();
    };
    $("#squad-builder").onclick = actions.buildSquad;
    $("#select-tool").onclick = () => {
      if (!actions.editable()) return;
      app.editTool = app.editTool === "select" ? "place" : "select";
      actions.redraw();
    };
    $("#select-type").onclick = () => {
      if (!actions.editable()) return;
      app.selection = app.config[app.team].flatMap((u, i) =>
        u.type === app.selected ? [i] : [],
      );
      app.editTool = "select";
      actions.redraw();
    };
    $("#clear-selection").onclick = () => {
      app.selection = [];
      app.editTool = "place";
      actions.redraw();
    };
    $("#move-selection").onclick = () => {
      if (!actions.editable()) return;
      if (!app.selection.length)
        return actions.toast("请先框选或选中同兵种部队。");
      app.editTool = "move";
      app.view.controls.enabled = true;
      actions.updatePanel();
      actions.toast("点击本方地面，选中部队中心将移动到该处。");
    };
    $("#delete-selection").onclick = () => {
      if (!actions.editable() || !app.selection.length) return;
      actions.commit();
      app.config[app.team] = app.config[app.team].filter(
        (u, i) => !app.selection.includes(i),
      );
      app.selection = [];
      actions.redraw();
    };
    $("#apply-stance").onclick = () => {
      if (!actions.editable() || !app.selection.length)
        return actions.toast("请先选中部队。");
      actions.commit();
      for (const i of app.selection)
        app.config[app.team][i].stance = $("#stance").value;
      actions.redraw();
      actions.toast("选中部队姿态已更新。");
    };
    $("#preset").onclick = actions.choosePreset;
    $("#start").onclick = actions.start;
    $("#pause").onclick = () => {
      app.paused = !app.paused;
      $("#pause").textContent = app.paused ? "▶ 继续" : "Ⅱ 暂停";
      if (app.mode === "battle")
        app.view.frame(app.battle.snapshot(), app.paused);
      actions.updateCommandUI();
    };
    $("#clear").onclick = () => {
      if (!actions.editable()) return;
      actions.commit();
      app.config[app.team] = [];
      app.config.props = (app.config.props || []).filter(
        (p) => p.team !== app.team,
      );
      actions.redraw();
    };
    $("#undo").onclick = () => {
      if (!actions.editable() || !app.history.length) return;
      const previous = app.history.pop();
      app.config = previous;
      app.view.map(app.config.map);
      actions.redraw();
    };
    $("#camera").onclick = () => {
      app.view.resetCamera();
      $("#camera-preset").value = "overview";
    };
    $("#help").onclick = actions.help;
    $("#workshop").onclick = actions.workshop;
    $("#campaign").onclick = actions.chooseMission;
    $("#sandbox").onclick = () => {
      if (app.mode === "battle") return actions.toast("请先返回布阵。");
      app.playMode = "sandbox";
      app.series = null;
      $("#bo3-enabled").checked = false;
      app.hotseatStage = null;
      actions.reset();
      app.history = [];
      app.missionIndex = null;
      app.config = defaultConfig(app.config.map);
      app.team = "red";
      app.view.map(app.config.map);
      actions.redraw();
    };
    $("#close-modal").onclick = () => $("#modal").close();
    $("#modal").addEventListener("click", (e) => {
      if (e.target === $("#modal")) {
        const r = e.target.getBoundingClientRect();
        if (
          e.clientX < r.left ||
          e.clientX > r.right ||
          e.clientY < r.top ||
          e.clientY > r.bottom
        )
          e.target.close();
      }
    });
    document.querySelectorAll("[data-speed]").forEach(
      (b) =>
        (b.onclick = () => {
          app.speed = Number(b.dataset.speed);
          document
            .querySelectorAll("[data-speed]")
            .forEach((x) => x.classList.toggle("active", x === b));
        }),
    );
    const touch = $("#touch-mode");
    const syncTouch = () => {
      touch.textContent =
        app.touchMode === "camera" ? "☝ 拖拽视角" : "⚑ 点选布阵";
      touch.setAttribute("aria-pressed", String(app.touchMode === "place"));
      app.view.controls.enabled = app.touchMode === "camera";
    };
    touch.onclick = () => {
      app.touchMode = app.touchMode === "camera" ? "place" : "camera";
      syncTouch();
    };
    $("#cancel-planning").onclick = () => app.planningController?.abort();
    bindBattlefield(app, actions);
    window.addEventListener("keydown", (e) => {
      const action = shortcutAction(
        e,
        document.activeElement,
        $("#modal").open,
      );
      if (!action) return;
      if (action === "pause") {
        e.preventDefault();
        if (app.mode === "deploy") actions.start();
        else if (["battle", "replay"].includes(app.mode)) $("#pause").click();
      } else if (action === "camera") {
        e.preventDefault();
        const select = $("#camera-preset");
        select.selectedIndex =
          (select.selectedIndex + 1) % select.options.length;
        select.dispatchEvent(new Event("change"));
      } else if (action === "undo") {
        e.preventDefault();
        $("#undo").click();
      } else if (action === "reset") actions.reset();
      else if (app.mode === "battle") {
        e.preventDefault();
        document.querySelector(`[data-order="${action}"]`)?.click();
      }
    });
    document.addEventListener("visibilitychange", () => {
      app.last = 0;
      app.accumulator = 0;
    });
  }
  return { initializeUI, bindEvents };
}
