import "./style.css";
import "./mobile.css";
import "./expansion.css";
import "./tactics.css";
import "./command.css";
import "./arena.css";
import { BattleAudio } from "./audio.js";
import { BattleSeries } from "./series.js";

import { withAIAsync, aiDuelAsync } from "./tactics.js";
import { hotseatView, nextHotseat } from "./deployment.js";

import { BattlefieldView } from "./scene.js";
import { initPhysics, Battle } from "./engine.js";
import {
  VERSION,
  UNITS,
  MISSIONS,
  DT,
  formation,
  cost,
  validateConfig,
  defaultConfig,
  canPlace,
  missionArmy,
  combatPower,
} from "./data.js";
import { $, clone, escape, storageKey } from "./ui/dom.js";
import { createWorkshop } from "./ui/workshop.js";
import { createHud } from "./ui/hud.js";
import { createDeploy } from "./ui/deploy.js";
import { createEvents } from "./ui/events.js";

const app = {
  config: defaultConfig(),
  team: "red",
  selected: "spear",
  selectedProp: "barrel",
  catalog: "units",
  view: undefined,
  battle: null,
  mode: "deploy",
  speed: 1,
  paused: false,
  accumulator: 0,
  last: 0,
  history: [],
  frames: [],
  replayTime: 0,
  missionIndex: null,
  toastTimer: undefined,
  statusTick: -1,
  liveState: null,
  playMode: "sandbox",
  hotseatStage: null,
  selection: [],
  editTool: "place",
  lastResult: null,
  replayConfig: null,
  audio: new BattleAudio(),
  series: null,
  roundRecorded: false,
  seriesPreparing: false,
  pendingOrder: null,
  favorites: Array(5).fill(null),
  initialPower: { red: 1, blue: 1 },
  replayEngineVersion: VERSION,
  orderPoint: null,
  planning: false,
  planningController: null,
  touchMode: "camera",
};
const actions = {};
Object.assign(actions, createWorkshop(app, actions));
Object.assign(actions, createHud(app, actions));
Object.assign(actions, createDeploy(app, actions));
Object.assign(actions, createEvents(app, actions));
Object.assign(actions, {
  persist,
  commit,
  editable,
  redraw,
  reset,
  changeMap,
  refreshAI,
  prepareNextRound,
  changePlayMode,
  start,
  loadMission,
  validPlacement,
});
function persist() {
  if (app.playMode === "hotseat") return;
  try {
    localStorage.setItem(storageKey, JSON.stringify(app.config));
  } catch {}
}
function commit() {
  app.history.push(clone(app.config));
  if (app.history.length > 40) app.history.shift();
}
function editable(allowAI = false) {
  if (app.planning) {
    actions.toast("推演中，请稍候或取消推演。");
    return false;
  }
  if (app.mode !== "deploy") {
    actions.toast("先点击「返回布阵」，再调整军队。");
    return false;
  }
  if (app.playMode === "ai-ai" && !allowAI) {
    actions.toast("自动演武由 AI 布阵；切换对战方式即可手动编辑。");
    return false;
  }
  if (app.playMode === "hotseat" && !["red", "blue"].includes(app.hotseatStage))
    return false;
  return true;
}
function redraw() {
  const visible =
    app.playMode === "hotseat"
      ? hotseatView(app.config, app.hotseatStage)
      : app.config;
  app.view.deployment(visible);
  app.selection = app.selection.filter((i) => app.config[app.team][i]);
  app.view.selection(app.team, app.selection);
  app.view.controls.enabled =
    app.editTool !== "select" &&
    !(
      matchMedia("(max-width:600px), (pointer:coarse)").matches &&
      app.touchMode === "place"
    );
  actions.updatePower(
    visible["red"]
      .map((u) => ({ ...u, team: "red", alive: true, hp: UNITS[u.type].hp }))
      .concat(
        visible["blue"].map((u) => ({
          ...u,
          team: "blue",
          alive: true,
          hp: UNITS[u.type].hp,
        })),
      ),
    true,
  );
  actions.updatePanel();
  actions.persist();
}
function reset() {
  app.planningController?.abort();
  if (
    app.series &&
    !app.series.complete &&
    app.roundRecorded &&
    !app.seriesPreparing
  )
    return actions.prepareNextRound();
  app.battle?.dispose();
  app.battle = null;
  app.liveState = null;
  app.statusTick = -1;
  app.mode = "deploy";
  app.paused = false;
  app.accumulator = 0;
  $("#result").classList.add("hidden");
  $("#pause").classList.add("hidden");
  $("#start").innerHTML = "开 战 <span>↗</span>";
  $("#status").textContent = "布阵阶段";
  $("#battle-time").textContent = "准备好后，见证混乱。";
  $("#status-dot").style.background = "#a3ae8a";
  $(".replay-bar")?.remove();
  app.frames = [];
  app.pendingOrder = null;
  app.audio.reset();
  app.roundRecorded = false;
  app.replayEngineVersion = VERSION;
  if (app.series?.complete && !app.seriesPreparing) app.series = null;
  app.lastResult = null;
  app.replayConfig = null;
  app.selection = [];
  app.editTool = "place";
  if (app.playMode === "hotseat") {
    app.config = { ...app.config, red: [], blue: [], props: [] };
    app.hotseatStage = "red";
    app.team = "red";
    app.history = [];
  }
  $("#hotseat-cover").classList.add("hidden");
  $("#preview-label").classList.add("hidden");
  actions.redraw();
  actions.updateCommandUI();
}
async function changeMap(id) {
  if (!actions.editable(true)) return;
  actions.commit();
  app.config.map = id;
  app.config.budget = Math.max(2200, app.config.budget);
  app.config.props = [];
  app.config.red = formation("balanced");
  app.config.blue = formation("balanced", "blue");
  app.selection = [];
  await actions.refreshAI(false);
  app.view.map(id);
  actions.redraw();
}
async function refreshAI(notify = true) {
  if (
    app.mode !== "deploy" ||
    app.missionIndex !== null ||
    !["human-ai", "ai-ai"].includes(app.playMode)
  )
    return;
  app.planningController?.abort();
  const controller = new AbortController();
  app.planningController = controller;
  const input = clone(app.config),
    signature = JSON.stringify(input),
    playMode = app.playMode;
  app.planning = true;
  $("#planning-status").hidden = false;
  $("#start").disabled = true;
  $("#generate-ai").disabled = true;
  try {
    const control = {
      signal: controller.signal,
      onProgress: (p) => {
        $("#planning-status").textContent =
          `算策推演 · ${p.phase} · ${Math.round((100 * p.done) / p.total)}%`;
      },
    };
    const next =
      playMode === "human-ai"
        ? await withAIAsync(
            input,
            "blue",
            $("#blue-ai").value,
            input.red,
            app.series?.last,
            control,
          )
        : await aiDuelAsync(
            input,
            $("#red-ai").value,
            $("#blue-ai").value,
            app.series?.last,
            control,
          );
    if (
      controller.signal.aborted ||
      app.mode !== "deploy" ||
      app.playMode !== playMode ||
      JSON.stringify(app.config) !== signature
    )
      throw Error("布阵已改变，请重新推演。");
    app.config = next;
    if (notify) {
      actions.redraw();
      actions.toast("AI 已按公开编队完成推演。");
    }
    return true;
  } catch (e) {
    if (!controller.signal.aborted) actions.toast(e.message);
    return false;
  } finally {
    if (app.planningController === controller) {
      app.planning = false;
      $("#planning-status").hidden = true;
      $("#start").disabled = false;
      $("#generate-ai").disabled = false;
    }
  }
}
async function prepareNextRound() {
  if (!app.series || app.series.complete) return;
  const next = app.series.nextConfig();
  app.seriesPreparing = true;
  actions.reset();
  app.seriesPreparing = false;
  app.config = next;
  app.history = [];
  app.team = "red";
  if (app.playMode === "hotseat") {
    app.hotseatStage = "red";
    app.config.red = [];
    app.config.blue = [];
    app.config.props = [];
  } else await actions.refreshAI(false);
  app.view.map(app.config.map);
  actions.redraw();
  actions.toast("上一局公开阵型和战报已保留；可微调后开始下一局。");
}
async function changePlayMode(value) {
  if (app.mode !== "deploy" || app.missionIndex !== null) return;
  app.playMode = value;
  app.series = null;
  app.hotseatStage = value === "hotseat" ? "red" : null;
  app.team = "red";
  app.selection = [];
  app.history = [];
  app.editTool = "place";
  if (value === "hotseat")
    app.config = { ...app.config, red: [], blue: [], props: [] };
  else if (!app.config.red.length || !app.config.blue.length)
    app.config = defaultConfig(app.config.map);
  await actions.refreshAI(false);
  actions.redraw();
}
async function start() {
  if (app.planning) return;
  if (app.mode === "battle" || app.mode === "replay" || app.mode === "ended") {
    actions.reset();
    return;
  }
  if (app.playMode === "hotseat") {
    try {
      app.hotseatStage = nextHotseat(app.hotseatStage, app.config);
      app.selection = [];
      app.history = [];
      app.editTool = "place";
      if (app.hotseatStage === "handoff") {
        app.team = "blue";
        actions.redraw();
        $("#hotseat-cover").classList.remove("hidden");
        return;
      }
    } catch (e) {
      actions.toast(e.message);
      return;
    }
  }
  if (["human-ai", "ai-ai"].includes(app.playMode) && !(await refreshAI(false)))
    return;
  if (!app.config.red.length || !app.config.blue.length) {
    actions.toast("双方都需要至少一名士兵。");
    return;
  }
  try {
    app.config.commander = {
      enabled: $("#commander-enabled").checked,
      aiTeams:
        app.playMode === "ai-ai"
          ? ["red", "blue"]
          : app.playMode === "human-ai" || app.missionIndex !== null
            ? ["blue"]
            : [],
    };
    validateConfig(app.config);
    app.battle = new Battle(app.config);
  } catch (e) {
    actions.toast(e.message);
    return;
  }
  app.mode = "battle";
  if (app.playMode === "human-ai" || app.missionIndex !== null)
    $("#command-team").value = "red";
  app.replayEngineVersion = VERSION;
  if ($("#bo3-enabled").checked && !app.series)
    app.series = new BattleSeries(app.config);
  app.roundRecorded = false;
  app.pendingOrder = null;
  app.audio.reset();
  app.audio.play("start");
  app.initialPower = combatPower(app.battle.snapshot().units);
  app.config = validateConfig(app.config);
  app.replayConfig = clone(app.config);
  app.lastResult = null;
  app.selection = [];
  app.view.controls.enabled = true;
  $("#preview-label").classList.add("hidden");
  app.frames = [app.battle.snapshot()];
  app.accumulator = 0;
  app.paused = false;
  $("#pause").classList.remove("hidden");
  $("#pause").textContent = "Ⅱ 暂停";
  $("#start").innerHTML = "返回布阵 <span>↶</span>";
  $("#status").textContent = "交战中";
  $("#status-dot").style.background = "#bc7959";
  app.view.clearActors();
  actions.updatePanel();
}
function loadMission(index) {
  app.series = null;
  $("#bo3-enabled").checked = false;
  app.playMode = "sandbox";
  app.hotseatStage = null;
  actions.reset();
  app.history = [];
  app.missionIndex = index;
  const m = MISSIONS[index];
  app.config = {
    version: VERSION,
    map: m.map,
    seed: 20261116,
    budget: Math.max(m.budget, cost(formation(m.enemy, "blue"))),
    red: [],
    blue: missionArmy(m),
    props: [],
  };
  app.team = "red";
  app.view.map(m.map);
  actions.redraw();
  $("#modal").close();
  actions.toast("布置朱砂军，完成试炼。");
}
function validPlacement(p) {
  return (
    !!p &&
    canPlace(
      app.config,
      app.team,
      {
        type: app.catalog === "units" ? app.selected : app.selectedProp,
        x: p.x,
        z: p.z,
      },
      app.catalog === "units" ? "unit" : "prop",
    )
  );
}
async function boot() {
  Object.assign(actions, actions.initializeUI());
  try {
    app.view = new BattlefieldView($("#viewport"));
    app.view.shakeEnabled = $("#camera-shake").checked;
    let quality = "balanced";
    try {
      quality = localStorage.getItem("frontline-quality") || "balanced";
    } catch {}
    if (!["low", "balanced", "high"].includes(quality)) quality = "balanced";
    app.view.setQuality(quality);
    $("#quality").value = quality;
    app.view.map(app.config.map);
    actions.redraw();
    await initPhysics();
    $("#loading").classList.add("hidden");
  } catch (e) {
    $("#loading").innerHTML =
      `<h2>战场加载失败</h2><p>${escape(e.message)}</p><p>请使用支持 WebGL 2 的浏览器，并开启硬件加速。</p>`;
    console.error(e);
    return;
  }

  actions.bindEvents();
  function animate(now) {
    requestAnimationFrame(animate);
    const delta = app.last ? Math.min((now - app.last) / 1000, 0.1) : 0;
    app.last = now;
    if (app.mode === "battle" && !app.paused) {
      app.accumulator += delta * app.speed;
      let steps = 0;
      while (app.accumulator >= DT && steps < 24 && !app.battle.result) {
        app.battle.step();
        app.accumulator -= DT;
        steps++;
        if (app.battle.tick % 6 === 0) app.frames.push(app.battle.snapshot());
      }
      const state = app.battle.snapshot((app.liveState ||= {}));
      app.view.frame(state);
      if (app.pendingOrder && app.orderPoint)
        app.view.preview(
          app.config,
          { ...app.orderPoint, type: app.pendingOrder },
          "order",
          $("#command-team").value,
        );
      app.audio.consume(state.cues);
      if (Math.floor(state.time * 10) !== app.statusTick) {
        app.statusTick = Math.floor(state.time * 10);
        actions.updatePower(state.units);
        actions.updateCommandUI(state);
        actions.renderHighlights(app.battle.highlights, app.battle.tick);
        $("#battle-time").textContent = state.time.toFixed(1) + " 秒 / 90 秒";
        $("#red-count").textContent = state.units.filter(
          (u) => u.alive && u.team === "red",
        ).length;
        $("#blue-count").textContent = state.units.filter(
          (u) => u.alive && u.team === "blue",
        ).length;
      }
      if (app.battle.result) {
        if (app.battle.tick % 6 !== 0) app.frames.push(app.battle.snapshot());
        actions.finish();
      }
    } else if (app.mode === "replay") {
      if (!app.paused)
        app.replayTime = Math.min(
          app.frames.at(-1).time,
          app.replayTime + delta * app.speed,
        );
      actions.showReplay();
      if (app.replayTime >= app.frames.at(-1).time) {
        app.paused = true;
        $("#pause").textContent = "▶ 继续";
      }
    }
    app.view.render(now / 1000);
    if (Math.floor(now / 500) !== Math.floor((now - delta * 1000) / 500))
      $("#performance").textContent = `${Math.round(app.view.metrics.fps)} FPS`;
  }
  requestAnimationFrame(animate);
}
boot();
