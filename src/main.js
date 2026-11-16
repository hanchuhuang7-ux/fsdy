import "./style.css";
import "./mobile.css";
import "./expansion.css";
import "./tactics.css";
import "./command.css";
import { BattleAudio } from "./audio.js";
import { BattleSeries } from "./series.js";
import { saveFavorite, loadFavorite, readFavorites } from "./favorites.js";
import { AI_STYLES, withAI, aiDuel } from "./tactics.js";
import {
  squad,
  placeSquad,
  moveSquad,
  hotseatView,
  nextHotseat,
} from "./deployment.js";
import { replayPackage, validateReplay, createReplayBattle } from "./replay.js";
import { BattlefieldView } from "./scene.js";
import { initPhysics, Battle } from "./engine.js";
import {
  VERSION,
  UNITS,
  PROPS,
  MAPS,
  MISSIONS,
  MAX_UNITS,
  MAX_PROPS,
  DT,
  formation,
  cost,
  teamCost,
  groundAt,
  validateConfig,
  defaultConfig,
  canPlace,
  missionArmy,
  STANCES,
  ORDERS,
  combatPower,
} from "./data.js";

const $ = (selector) => document.querySelector(selector);
let config = defaultConfig(),
  team = "red",
  selected = "spear",
  selectedProp = "barrel",
  catalog = "units",
  view,
  battle = null,
  mode = "deploy",
  speed = 1,
  paused = false,
  accumulator = 0,
  last = 0,
  history = [],
  frames = [],
  replayTime = 0,
  missionIndex = null,
  toastTimer;
let statusTick = -1,
  liveState = null;
let playMode = "sandbox",
  hotseatStage = null,
  selection = [],
  editTool = "place",
  lastResult = null,
  replayConfig = null;
const audio = new BattleAudio();
let series = null,
  roundRecorded = false,
  seriesPreparing = false,
  pendingOrder = null,
  favorites = Array(5).fill(null),
  initialPower = { red: 1, blue: 1 },
  replayEngineVersion = VERSION;
let orderPoint = null;
const storageKey = "frontline-lab-v1";
const clone = (value) => structuredClone(value);
const escape = (s) =>
  String(s).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
function toast(message) {
  if ($("#modal").open) {
    $("#modal-feedback").textContent = message;
    return;
  }
  $("#toast").textContent = message;
  $("#toast").classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => $("#toast").classList.remove("show"), 3200);
}
function persist() {
  if (playMode === "hotseat") return;
  try {
    localStorage.setItem(storageKey, JSON.stringify(config));
  } catch {}
}
function commit() {
  history.push(clone(config));
  if (history.length > 40) history.shift();
}
function dialog(title, body) {
  $("#modal-feedback").textContent = "";
  $("#modal-title").textContent = title;
  $("#modal-body").innerHTML = body;
  $("#modal").showModal();
}
function editable(allowAI = false) {
  if (mode !== "deploy") {
    toast("先点击「返回布阵」，再调整军队。");
    return false;
  }
  if (playMode === "ai-ai" && !allowAI) {
    toast("自动演武由 AI 布阵；切换对战方式即可手动编辑。");
    return false;
  }
  if (playMode === "hotseat" && !["red", "blue"].includes(hotseatStage))
    return false;
  return true;
}
function budget() {
  return missionIndex === null ? config.budget : MISSIONS[missionIndex].budget;
}
function renderCatalog() {
  const definitions = catalog === "units" ? UNITS : PROPS,
    choice = catalog === "units" ? selected : selectedProp;
  $("#unit-cards").innerHTML = Object.entries(definitions)
    .map(
      ([id, d]) =>
        `<button class="unit-card ${id === choice ? "active" : ""}" data-type="${id}" title="${d.description}"><span class="glyph">${d.glyph}</span><span><strong>${d.name}</strong><small>${d.subtitle}</small></span><b>${d.cost} ◇</b></button>`,
    )
    .join("");
  document.querySelectorAll("[data-type]").forEach(
    (b) =>
      (b.onclick = () => {
        if (catalog === "units") selected = b.dataset.type;
        else selectedProp = b.dataset.type;
        document.body.classList.remove("compact-catalog-open");
        const toggle = $(".compact-panel-toggle");
        if (toggle) toggle.textContent = "⚑ 兵种 / 布阵";
        updatePanel();
      }),
  );
  $("#catalog-units").classList.toggle("active", catalog === "units");
  $("#catalog-props").classList.toggle("active", catalog === "props");
  $("#catalog-hint").textContent =
    catalog === "units" ? "点选后在本方场地放置" : "道具与兵种共用军费";
}
function updatePanel() {
  const available = budget() - teamCost(config, team);
  $("#money").innerHTML =
    available.toLocaleString() +
    `<small> / ${budget().toLocaleString()}</small>`;
  $("#budget-fill").style.width =
    `${Math.max(0, (available / budget()) * 100)}%`;
  if (mode === "deploy") {
    for (const t of ["red", "blue"])
      $("#" + t + "-count").textContent =
        playMode === "hotseat" &&
        hotseatStage !== "revealed" &&
        hotseatStage !== t
          ? "?"
          : config[t].length;
  }
  $("#team-red").classList.toggle("active", team === "red");
  $("#team-blue").classList.toggle("active", team === "blue");
  $("#team-red").disabled = playMode === "hotseat" && hotseatStage !== "red";
  $("#team-blue").disabled =
    missionIndex !== null ||
    playMode === "human-ai" ||
    (playMode === "hotseat" && hotseatStage !== "blue");
  document.querySelectorAll(".unit-card").forEach((b) => {
    const definitions = catalog === "units" ? UNITS : PROPS,
      choice = catalog === "units" ? selected : selectedProp;
    b.classList.toggle("active", b.dataset.type === choice);
    b.disabled =
      mode !== "deploy" ||
      playMode === "ai-ai" ||
      available < definitions[b.dataset.type].cost;
  });
  for (const id of ["preset", "clear", "undo", "stance", "map"])
    $("#" + id).disabled =
      mode !== "deploy" ||
      (playMode === "ai-ai" && id !== "map") ||
      (id === "map" && (missionIndex !== null || playMode === "hotseat")) ||
      (id === "stance" && catalog === "props");
  $("#unit-detail").textContent =
    catalog === "units"
      ? UNITS[selected].description
      : PROPS[selectedProp].description;
  $("#prop-count").textContent =
    `道具 ${(config.props || []).filter((p) => p.team === team).length} / ${MAX_PROPS}`;
  $("#map").value = config.map;
  $("#map-description").textContent = MAPS[config.map].description;
  $("#terrain-info").textContent = MAPS[config.map].hint + " ↗";
  $("#sandbox").classList.toggle("active", missionIndex === null);
  $("#campaign").classList.toggle("active", missionIndex !== null);
  $("#mission").classList.toggle("hidden", missionIndex === null);
  if (missionIndex !== null) {
    const m = MISSIONS[missionIndex];
    $("#mission").innerHTML =
      `<strong>${m.name}</strong><p>${m.description}</p>`;
  }
  $("#bo3-enabled").disabled =
    mode !== "deploy" || missionIndex !== null || !!series?.rounds.length;
  $("#commander-enabled").disabled =
    mode !== "deploy" || !!series?.rounds.length;
  $("#commander-enabled").checked = config.commander?.enabled || false;
  if (series?.rounds.length) {
    $("#map").disabled = true;
    $("#play-mode").disabled = true;
  }
  $("#series-status").textContent = series
    ? `Bo3 · 红 ${series.wins.red} : ${series.wins.blue} 蓝 · ${series.complete ? "已结束" : "第 " + (series.rounds.length + 1) + " 局"}`
    : $("#bo3-enabled").checked
      ? "Bo3 · 等待第 1 局"
      : "单局推演";
  $("#restart-series").classList.toggle("hidden", !series);
  $("#round-label").classList.toggle("hidden", !series);
  $("#round-label").textContent = $("#series-status").textContent;
  $("#play-mode").value = playMode;
  $("#play-mode").disabled = mode !== "deploy" || missionIndex !== null;
  $(".ai-settings").classList.toggle(
    "hidden",
    !["human-ai", "ai-ai"].includes(playMode),
  );
  $("#red-ai").disabled = playMode !== "ai-ai" || mode !== "deploy";
  for (const id of ["blue-ai", "generate-ai"])
    $("#" + id).disabled = mode !== "deploy";
  $("#workshop").disabled =
    playMode === "hotseat" && hotseatStage !== "revealed";
  $("#mode-description").textContent = {
    sandbox: "编辑双方，试验你的战术。",
    "human-ai": "你指挥红方。开战前蓝方 AI 依据公开编队完成反制。",
    "ai-ai": "双方各侦察一次公开草案，再同时定阵；点击开战自动演武。",
    hotseat:
      hotseatStage === "red"
        ? "红方秘密布阵，完成后封存并交接。"
        : hotseatStage === "blue"
          ? "蓝方秘密布阵；红方兵力、道具与数量已隐藏。"
          : "封存编队，等待揭晓。",
  }[playMode];
  for (const id of [
    "squad-builder",
    "select-tool",
    "select-type",
    "move-selection",
    "delete-selection",
    "apply-stance",
    "clear-selection",
  ])
    $("#" + id).disabled = mode !== "deploy" || playMode === "ai-ai";
  $("#selection-info").textContent = selection.length
    ? `已选 ${selection.length} 人 · ${editTool === "move" ? "点击目标位置移动" : "可移动、删除或改姿态"}`
    : editTool === "select"
      ? "拖动框选本方部队；点击可逐个选择"
      : "未选中部队";
  $("#select-tool").classList.toggle("active", editTool === "select");
  if (mode === "deploy" && playMode === "hotseat")
    $("#start").innerHTML =
      hotseatStage === "red"
        ? "封存红方 →"
        : hotseatStage === "blue"
          ? "揭晓并开战 ↗"
          : "等待交接";
  if (series?.rounds.length) {
    $("#map").disabled = true;
    $("#play-mode").disabled = true;
  }
  $("#favorites").disabled = mode !== "deploy" || playMode === "ai-ai";
  if ($(".compact-panel-toggle"))
    $(".compact-panel-toggle").disabled = mode !== "deploy";
}
function redraw() {
  const visible =
    playMode === "hotseat" ? hotseatView(config, hotseatStage) : config;
  view.deployment(visible);
  selection = selection.filter((i) => config[team][i]);
  view.selection(team, selection);
  view.controls.enabled = editTool !== "select";
  updatePower(
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
  updatePanel();
  persist();
}
function reset() {
  if (series && !series.complete && roundRecorded && !seriesPreparing)
    return prepareNextRound();
  battle?.dispose();
  battle = null;
  liveState = null;
  statusTick = -1;
  mode = "deploy";
  paused = false;
  accumulator = 0;
  $("#result").classList.add("hidden");
  $("#pause").classList.add("hidden");
  $("#start").innerHTML = "开 战 <span>↗</span>";
  $("#status").textContent = "布阵阶段";
  $("#battle-time").textContent = "准备好后，见证混乱。";
  $("#status-dot").style.background = "#a3ae8a";
  $(".replay-bar")?.remove();
  frames = [];
  pendingOrder = null;
  audio.reset();
  roundRecorded = false;
  replayEngineVersion = VERSION;
  if (series?.complete && !seriesPreparing) series = null;
  lastResult = null;
  replayConfig = null;
  selection = [];
  editTool = "place";
  if (playMode === "hotseat") {
    config = { ...config, red: [], blue: [], props: [] };
    hotseatStage = "red";
    team = "red";
    history = [];
  }
  $("#hotseat-cover").classList.add("hidden");
  $("#preview-label").classList.add("hidden");
  redraw();
  updateCommandUI();
}
function changeMap(id) {
  if (!editable(true)) return;
  commit();
  config.map = id;
  config.budget = Math.max(2200, config.budget);
  config.props = [];
  config.red = formation("balanced");
  config.blue = formation("balanced", "blue");
  selection = [];
  refreshAI(false);
  view.map(id);
  redraw();
}
function refreshAI(notify = true) {
  if (mode !== "deploy" || missionIndex !== null) return;
  if (playMode === "human-ai")
    config = withAI(
      config,
      "blue",
      $("#blue-ai").value,
      config.red,
      series?.last,
    );
  if (playMode === "ai-ai")
    config = aiDuel(
      config,
      $("#red-ai").value,
      $("#blue-ai").value,
      series?.last,
    );
  if (notify) {
    redraw();
    toast("AI 已按当前地图、军费与公开编队重新布阵。");
  }
}
function updatePower(units, deploy = false) {
  const values = combatPower(units);
  if (deploy) initialPower = { ...values };
  for (const t of ["red", "blue"]) {
    const hidden =
      mode === "deploy" &&
      playMode === "hotseat" &&
      hotseatStage !== "revealed" &&
      hotseatStage !== t;
    $("#" + t + "-power-value").textContent = hidden
      ? "?"
      : Math.round(values[t]).toLocaleString();
    $("#" + t + "-power-fill").style.width =
      (hidden
        ? 0
        : Math.min(100, (values[t] / Math.max(1, initialPower[t])) * 100)) +
      "%";
  }
}
function updateCommandUI(state = null) {
  const enabled = (replayConfig || config).commander?.enabled;
  $("#command-dock").classList.toggle(
    "hidden",
    !enabled || !["battle", "replay"].includes(mode),
  );
  if (!enabled) return;
  const own = state?.commander || battle?.orderState,
    tick = state?.tick ?? battle?.tick ?? 0;
  if (!own) return;
  const t = $("#command-team").value,
    ai = (replayConfig || config).commander.aiTeams.includes(t),
    cool = Math.max(0, (own.readyAt[t] - tick) / 60);
  $("#command-status").textContent =
    `${ai ? "AI · " : ""}${own.points[t]} 点 · ${cool > 0 ? cool.toFixed(1) + "s 冷却" : "可下令"}${pendingOrder ? " · 点地面指定区域" : ""}`;
  $("#command-team").disabled =
    playMode === "human-ai" || missionIndex !== null || mode === "replay";
  for (const b of document.querySelectorAll("[data-order]")) {
    b.disabled =
      mode !== "battle" || ai || own.points[t] < 1 || cool > 0 || paused;
    b.classList.toggle("active", pendingOrder === b.dataset.order);
  }
}
function prepareNextRound() {
  if (!series || series.complete) return;
  const next = series.nextConfig();
  seriesPreparing = true;
  reset();
  seriesPreparing = false;
  config = next;
  history = [];
  team = "red";
  if (playMode === "hotseat") {
    hotseatStage = "red";
    config.red = [];
    config.blue = [];
    config.props = [];
  } else refreshAI(false);
  view.map(config.map);
  redraw();
  toast("上一局公开阵型和战报已保留；可微调后开始下一局。");
}
function favoriteDialog() {
  if (!editable()) return;
  dialog(
    "常用阵型收藏夹",
    `<p>保存当前阵营的士兵和道具，共五槽。载入时检查当前地图与军费；载入到另一阵营会镜像 x 坐标。试炼仍遵守玩家军费。</p>${favorites.map((s, i) => `<div class="favorite-slot"><span><strong>槽 ${i + 1} · ${s ? escape(s.name) : "空"}</strong><br>${s ? escape(MAPS[s.map].name) + " · " + s.units.length + " 人" : ""}</span><button data-load-favorite="${i}" ${s ? "" : "disabled"}>载入</button><button data-save-favorite="${i}">保存本方</button></div>`).join("")}<label>收藏名称 <input id="favorite-name" maxlength="32" value="我的阵型"></label>`,
  );
  document.querySelectorAll("[data-save-favorite]").forEach(
    (b) =>
      (b.onclick = () => {
        try {
          favorites = saveFavorite(
            favorites,
            Number(b.dataset.saveFavorite),
            $("#favorite-name").value,
            config,
            team,
          );
          localStorage.setItem(
            "frontline-favorites-v1",
            JSON.stringify(favorites),
          );
          favoriteDialog();
          toast("阵型已存入收藏槽。");
        } catch (e) {
          toast(e.message);
        }
      }),
  );
  document.querySelectorAll("[data-load-favorite]").forEach(
    (b) =>
      (b.onclick = () => {
        try {
          const next = loadFavorite(
            config,
            team,
            favorites[Number(b.dataset.loadFavorite)],
            budget(),
          );
          commit();
          config = next;
          selection = [];
          editTool = "place";
          redraw();
          $("#modal").close();
          audio.play("place");
          toast("收藏阵型已载入。");
        } catch (e) {
          toast(e.message);
        }
      }),
  );
}
function changePlayMode(value) {
  if (mode !== "deploy" || missionIndex !== null) return;
  playMode = value;
  series = null;
  hotseatStage = value === "hotseat" ? "red" : null;
  team = "red";
  selection = [];
  history = [];
  editTool = "place";
  if (value === "hotseat") config = { ...config, red: [], blue: [], props: [] };
  else if (!config.red.length || !config.blue.length)
    config = defaultConfig(config.map);
  refreshAI(false);
  redraw();
}
function buildSquad() {
  if (!editable()) return;
  dialog(
    "混编整队与镜像排阵",
    `<p>各兵种按列表顺序排入队形。锚点是队伍前沿中心；后排向己方纵深延伸。整队通过全部间距与军费检查后才放置。</p><div class="squad-roster">${Object.entries(
      UNITS,
    )
      .map(
        ([id, d]) =>
          `<label>${d.name} · ${d.cost}<input aria-label="${d.name}人数" data-squad="${id}" type="number" min="0" max="60" value="${id === selected ? 3 : 0}"></label>`,
      )
      .join(
        "",
      )}</div><div class="squad-form"><label>队形 <select id="squad-shape"><option value="square">方阵</option><option value="line">横排</option><option value="wedge">楔形阵</option></select></label><label>锚点 x <input id="squad-x" type="number" step="0.5" value="${team === "red" ? -6 : 6}"></label><label>锚点 z <input id="squad-z" type="number" step="0.5" value="0"></label><label><input id="squad-mirror" type="checkbox"> 沿 z=0 镜像追加</label></div><p>新单位使用左侧当前姿态；镜像队伍请把锚点移向一侧，避免与原队重叠。</p><button class="action" id="place-squad">整队放置</button>`,
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
        team,
        mirror: $("#squad-mirror").checked,
      });
      const next = placeSquad(config, team, additions);
      if (teamCost(next, team) > budget()) throw Error("超出试炼军费");
      commit();
      config = next;
      selection = [];
      editTool = "place";
      redraw();
      $("#modal").close();
      audio.play("place");
      toast(`已放置 ${additions.length} 名士兵。`);
    } catch (e) {
      toast(e.message);
    }
  };
}
function contributionsHTML(report) {
  if (!report) return "";
  const n = (v) => Math.round(v).toLocaleString();
  const header =
    "<tr><th>兵种 / 单位</th><th>存活/出战</th><th>伤亡</th><th>伤害</th><th>承伤</th><th>治疗</th><th>击杀</th><th>坠崖</th><th>误伤</th></tr>";
  const row = (name, r) =>
    `<tr><td>${name}</td><td>${r.survived ?? (r.alive ? 1 : 0)}/${r.initial ?? 1}</td><td>${r.lost ?? (r.alive ? 0 : 1)}</td><td>${n(r.damageDealt)}</td><td>${n(r.damageTaken)}</td><td>${n(r.healing)}</td><td>${r.kills}</td><td>${r.falls}</td><td>${n(r.friendlyFire)}</td></tr>`;
  let body = "";
  for (const t of ["red", "blue"]) {
    const s = report.teams[t];
    body += `<tr><td colspan="9" class="team-title">${t === "red" ? "朱砂军" : "靛蓝军"} · 剩余战力 ${n(s.remainingValue)}</td></tr>`;
    body += row("全军（含己方道具）", s);
    body += Object.entries(s.byType)
      .sort(
        (a, b) =>
          b[1].damageDealt + b[1].healing - a[1].damageDealt - a[1].healing,
      )
      .map(([type, r]) => row(UNITS[type].name, r))
      .join("");
  }
  const mvp = report.mvp;
  const rank = [...report.units]
    .sort(
      (a, b) =>
        b.damageDealt -
          b.friendlyFire +
          b.healing -
          (a.damageDealt - a.friendlyFire + a.healing) || a.id - b.id,
    )
    .slice(0, 8);
  return `${mvp ? `<div class="mvp">本场 MVP · ${mvp.team === "red" ? "红" : "蓝"}方 ${UNITS[mvp.type].name} #${mvp.id} · ${n(mvp.damageDealt)} 伤害 / ${n(mvp.healing)} 治疗 / ${mvp.kills} 击杀</div>` : ""}<div class="contribution-scroll"><table class="contribution-table">${header}${body}</table><details><summary>单位贡献榜 · 前八名</summary><table class="contribution-table">${header}${rank.map((u) => row(`${u.team === "red" ? "红" : "蓝"} ${UNITS[u.type].name} #${u.id}`, u)).join("")}</table></details></div>`;
}
function start() {
  if (mode === "battle" || mode === "replay" || mode === "ended") {
    reset();
    return;
  }
  if (playMode === "hotseat") {
    try {
      hotseatStage = nextHotseat(hotseatStage, config);
      selection = [];
      history = [];
      editTool = "place";
      if (hotseatStage === "handoff") {
        team = "blue";
        redraw();
        $("#hotseat-cover").classList.remove("hidden");
        return;
      }
    } catch (e) {
      toast(e.message);
      return;
    }
  }
  if (playMode === "human-ai")
    config = withAI(
      config,
      "blue",
      $("#blue-ai").value,
      config.red,
      series?.last,
    );
  if (playMode === "ai-ai")
    config = aiDuel(
      config,
      $("#red-ai").value,
      $("#blue-ai").value,
      series?.last,
    );
  if (!config.red.length || !config.blue.length) {
    toast("双方都需要至少一名士兵。");
    return;
  }
  try {
    config.commander = {
      enabled: $("#commander-enabled").checked,
      aiTeams:
        playMode === "ai-ai"
          ? ["red", "blue"]
          : playMode === "human-ai" || missionIndex !== null
            ? ["blue"]
            : [],
    };
    validateConfig(config);
    battle = new Battle(config);
  } catch (e) {
    toast(e.message);
    return;
  }
  mode = "battle";
  if (playMode === "human-ai" || missionIndex !== null)
    $("#command-team").value = "red";
  replayEngineVersion = VERSION;
  if ($("#bo3-enabled").checked && !series) series = new BattleSeries(config);
  roundRecorded = false;
  pendingOrder = null;
  audio.reset();
  audio.play("start");
  initialPower = combatPower(battle.snapshot().units);
  config = validateConfig(config);
  replayConfig = clone(config);
  lastResult = null;
  selection = [];
  view.controls.enabled = true;
  $("#preview-label").classList.add("hidden");
  frames = [battle.snapshot()];
  accumulator = 0;
  paused = false;
  $("#pause").classList.remove("hidden");
  $("#pause").textContent = "Ⅱ 暂停";
  $("#start").innerHTML = "返回布阵 <span>↶</span>";
  $("#status").textContent = "交战中";
  $("#status-dot").style.background = "#bc7959";
  view.clearActors();
  updatePanel();
}
function finish() {
  mode = "ended";
  $("#pause").classList.add("hidden");
  $("#status").textContent = "战斗结束";
  $("#status-dot").style.background = "#728e5f";
  const r = battle?.result || lastResult,
    winner =
      r.winner === "draw"
        ? "势均力敌"
        : r.winner === "red"
          ? "朱砂军获胜"
          : "靛蓝军获胜";
  const victory = missionIndex !== null && r.winner === "red";
  lastResult = r;
  if (series && !roundRecorded && battle?.result) {
    series.record(replayConfig, r);
    roundRecorded = true;
  }
  $("#red-count").textContent = r.red;
  $("#blue-count").textContent = r.blue;
  $("#battle-time").textContent = r.time.toFixed(1) + " 秒 / 90 秒";
  if (victory)
    try {
      localStorage.setItem("frontline-mission-" + missionIndex, "won");
    } catch {}
  $("#result").innerHTML =
    `<span class="eyebrow">${victory ? "TRIAL COMPLETED" : "THE DUST SETTLES"}</span><h2 class="${r.winner}">${winner}</h2><p>用时 ${r.time.toFixed(1)} 秒 · 剩余 ${r.red} : ${r.blue}<br>${r.reason === "timeout" ? "时间耗尽，按存活兵力价值判定。" : "最后一名敌人，已离开战场。"}</p>${contributionsHTML(r.report)}<div class="result-actions"><button id="watch-replay">▶ 战斗回放</button><button id="export-replay">导出完整回放</button><button id="export-seed">导出种子重演包</button><button id="retry">调整阵型</button>${missionIndex !== null && missionIndex < MISSIONS.length - 1 ? '<button id="next-mission">下一试炼 →</button>' : ""}</div>`;
  $("#result").classList.remove("hidden");
  if (series) {
    const note = document.createElement("div");
    note.className = "series-note";
    note.textContent = `Bo3 红 ${series.wins.red} : ${series.wins.blue} 蓝${series.complete ? " · 系列赛结束" : " · 可复盘并调整下一局"}`;
    $("#result").prepend(note);
    if (!series.complete) {
      const next = document.createElement("button");
      next.className = "series-next";
      next.textContent = "复盘调阵 → 第 " + (series.rounds.length + 1) + " 局";
      next.onclick = prepareNextRound;
      $(".result-actions").prepend(next);
    }
    const save = document.createElement("button");
    save.textContent = "导出 Bo3 档案";
    save.onclick = () => saveJson("frontline-bo3.json", series.export());
    $(".result-actions").append(save);
  }
  $("#watch-replay").onclick = beginReplay;
  $("#retry").onclick = reset;
  $("#export-replay").onclick = () => exportReplay("trajectory");
  $("#export-seed").onclick = () => exportReplay("seed");
  if ($("#next-mission"))
    $("#next-mission").onclick = () => loadMission(missionIndex + 1);
  updateCommandUI();
  updatePanel();
}
function beginReplay() {
  if (!frames.length) {
    toast("先完成一场战斗，即可回放。");
    return;
  }
  mode = "replay";
  audio.reset();
  $(".replay-bar")?.remove();
  replayTime = 0;
  paused = false;
  view.clearActors();
  $("#result").classList.add("hidden");
  $("#pause").classList.remove("hidden");
  $("#pause").textContent = "Ⅱ 暂停";
  $("#status").textContent = "战斗回放";
  const bar = document.createElement("div");
  bar.className = "replay-bar";
  bar.innerHTML = `<span>REPLAY</span><input id="timeline" type="range" min="0" max="${frames.length - 1}" value="0" aria-label="回放进度"><span id="replay-position">0.0 s</span><button id="replay-result">查看战果</button>`;
  $(".arena").append(bar);
  $("#timeline").oninput = (e) => {
    replayTime = frames[Number(e.target.value)].time;
    paused = true;
    audio.cursor = frames[Number(e.target.value)].cues?.at(-1)?.id || 0;
    view.feedbackCursor = audio.cursor;
    $("#pause").textContent = "▶ 继续";
    showReplay();
  };
  $("#replay-result").onclick = () => {
    bar.remove();
    finish();
  };
  showReplay();
}
function showReplay() {
  const i =
    replayTime >= frames.at(-1).time
      ? frames.length - 1
      : Math.min(frames.length - 1, Math.floor(replayTime * 10));
  view.frame(frames[i], paused);
  updatePower(frames[i].units);
  updateCommandUI(frames[i]);
  audio.consume(frames[i].cues, !paused);
  $("#timeline").value = i;
  $("#replay-position").textContent = frames[i].time.toFixed(1) + " s";
  $("#battle-time").textContent = frames[i].time.toFixed(1) + " 秒";
  const counts = (t) =>
    frames[i].units.filter((u) => u.alive && u.team === t).length;
  $("#red-count").textContent = counts("red");
  $("#blue-count").textContent = counts("blue");
}
function saveJson(name, value, compact = false) {
  const a = document.createElement("a"),
    url = URL.createObjectURL(
      new Blob([JSON.stringify(value, null, compact ? 0 : 2)], {
        type: "application/json",
      }),
    );
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
function exportReplay(kind) {
  try {
    saveJson(
      `frontline-${kind}-replay.json`,
      replayEngineVersion === VERSION
        ? replayPackage(replayConfig || config, frames, lastResult, kind)
        : validateReplay({
            format: "frontline-replay",
            version: 1,
            engineVersion: replayEngineVersion,
            kind,
            config: replayConfig || config,
            frames: kind === "trajectory" ? frames : undefined,
            result: kind === "trajectory" ? lastResult : undefined,
          }),
      true,
    );
    toast("回放包已导出。");
  } catch (e) {
    toast("回放导出失败：" + e.message);
  }
}
async function loadReplay(raw) {
  const pkg = validateReplay(raw);
  let loadedFrames = pkg.frames,
    result = pkg.result;
  if (pkg.kind === "seed") {
    toast("正在按种子重演…");
    const simulator = await createReplayBattle(pkg);
    loadedFrames = [simulator.snapshot()];
    try {
      while (!simulator.result) {
        simulator.step();
        if (simulator.tick % 6 === 0) loadedFrames.push(simulator.snapshot());
        if (simulator.tick % 600 === 0)
          await new Promise((resolve) => setTimeout(resolve, 0));
      }
      if (simulator.tick % 6 !== 0) loadedFrames.push(simulator.snapshot());
      result = simulator.result;
    } finally {
      simulator.dispose();
    }
    if (pkg.engineVersion !== VERSION) {
      const migrated = validateReplay({
        ...pkg,
        kind: "trajectory",
        frames: loadedFrames,
        result,
      });
      loadedFrames = migrated.frames;
      result = migrated.result;
    }
  }
  battle?.dispose();
  battle = null;
  series = null;
  audio.reset();
  replayEngineVersion = pkg.engineVersion;
  config = pkg.config;
  replayConfig = clone(config);
  lastResult = result;
  frames = loadedFrames;
  playMode = "sandbox";
  hotseatStage = null;
  missionIndex = null;
  selection = [];
  editTool = "place";
  team = "red";
  view.controls.enabled = true;
  initialPower = combatPower(loadedFrames[0].units);
  view.map(config.map);
  mode = "ended";
  updatePanel();
  $("#modal").close();
  $("#start").innerHTML = "返回布阵 <span>↶</span>";
  beginReplay();
}
function workshop() {
  if (playMode === "hotseat" && hotseatStage !== "revealed")
    return toast("盲选封存期间，策略档案与导出已关闭。");
  dialog(
    "策略与战场档案",
    `<p>把布阵文件交给 AI，让它研究兵种、坐标与姿态，写出自己的军队。这里保存完整对局配置；导入不会运行任何脚本。</p><div class="settings"><label for="seed">对局种子</label><input id="seed" type="number" min="0" max="4294967295" value="${config.seed}" ${mode !== "deploy" ? "disabled" : ""}><button class="action" id="apply-seed" ${mode !== "deploy" ? "disabled" : ""}>应用</button></div><button class="action" id="export-config">导出对局 JSON</button><button class="action" id="import-config">从文件导入</button><button class="action" id="save-local">保存当前阵型</button><input id="file-input" type="file" accept=".json,application/json" hidden><h3>编辑对局</h3><p>每方最多 ${MAX_UNITS} 人。x 为左右方向，z 为前后方向；红方 x ≤ -2.5，蓝方 x ≥ 2.5。姿态：advance / hold / flank / hunt；省略时默认 advance。可选统帅军令配置 commander: {enabled:true,aiTeams:[&quot;blue&quot;]}。</p><textarea id="config-editor" spellcheck="false" aria-label="对局配置 JSON">${escape(JSON.stringify(config, null, 2))}</textarea><button class="action" id="apply-config" ${mode !== "deploy" ? "disabled" : ""}>载入编辑的对局</button><h3>复现与评测</h3><p>项目提供无界面的 Node.js 评测命令、策略示例和 AI 提示词。回放会记录当前对局的物理轨迹；相同引擎版本与种子可复现模拟。完整文档见项目 README。</p>`,
  );
  const replayTools = document.createElement("div");
  replayTools.innerHTML = `<h3>回放档案</h3><button class="action" id="import-replay">载入回放包 / 种子包</button><input id="replay-file" type="file" accept=".json,application/json" hidden><p>完整回放保存 10 Hz 轨迹；种子包使用同一版本引擎重新推演。回放文件最多 64 MB。</p>`;
  $("#modal-body").prepend(replayTools);
  $("#import-replay").onclick = () => {
    if (mode === "battle") return toast("请先返回布阵。");
    $("#replay-file").click();
  };
  $("#replay-file").onchange = async (e) => {
    const f = e.target.files[0];
    if (!f) return;
    if (f.size > 64 * 1024 * 1024) return toast("回放文件须小于 64 MB。");
    $("#import-replay").disabled = true;
    try {
      await loadReplay(JSON.parse(await f.text()));
    } catch (error) {
      toast("载入失败：" + error.message);
    } finally {
      if ($("#import-replay")) $("#import-replay").disabled = false;
    }
  };
  $("#export-config").onclick = () => saveJson("frontline-battle.json", config);
  const settings = document.createElement("div");
  settings.className = "settings";
  settings.innerHTML = `<label for="budget-setting">每方军费</label><select id="budget-setting" ${mode !== "deploy" || missionIndex !== null ? "disabled" : ""}>${[
    ...new Set([1500, 2200, 4000, 8000, 10000, config.budget]),
  ]
    .sort((a, b) => a - b)
    .map(
      (n) =>
        `<option value="${n}" ${n === config.budget ? "selected" : ""}>${n.toLocaleString()}</option>`,
    )
    .join(
      "",
    )}</select><button class="action" id="apply-budget" ${mode !== "deploy" || missionIndex !== null ? "disabled" : ""}>应用军费</button>`;
  $("#modal-body").prepend(settings);
  $("#apply-budget").onclick = () => {
    if (series?.rounds.length)
      return toast("系列赛内军费固定；重新开始系列赛后可修改。");
    const n = Number($("#budget-setting").value);
    if (["red", "blue"].some((t) => teamCost(config, t) > n))
      return toast("当前兵力和道具超出这档军费，请先精简。");
    commit();
    config.budget = n;
    refreshAI(false);
    redraw();
    updatePanel();
    persist();
    $("#config-editor").value = JSON.stringify(config, null, 2);
    toast("军费已更新。");
  };
  $("#save-local").onclick = () => {
    persist();
    toast("阵型已保存在此浏览器。");
  };
  $("#apply-seed").onclick = () => {
    if (series?.rounds.length)
      return toast("系列赛种子按小局递增；重新开始后可修改。");
    const seed = Number($("#seed").value);
    if (!Number.isInteger(seed) || seed < 0 || seed > 0xffffffff)
      return toast("请输入有效的 32 位整数种子。");
    commit();
    config.seed = seed;
    persist();
    toast("已应用种子 " + seed);
  };
  const apply = (value) => {
    if (!editable()) return;
    try {
      const normalized = validateConfig(value);
      history = [];
      missionIndex = null;
      config = clone(normalized);
      series = null;
      $("#bo3-enabled").checked = false;
      playMode = "sandbox";
      hotseatStage = null;
      selection = [];
      editTool = "place";
      team = "red";
      view.map(config.map);
      redraw();
      $("#modal").close();
      toast("对局已载入。");
    } catch (e) {
      toast("导入失败：" + e.message);
    }
  };
  $("#apply-config").onclick = () => {
    try {
      apply(JSON.parse($("#config-editor").value));
    } catch (e) {
      toast("JSON 格式有误：" + e.message);
    }
  };
  $("#import-config").onclick = () => {
    if (editable()) $("#file-input").click();
  };
  $("#file-input").onchange = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    if (file.size > 1024 * 1024) return toast("文件须小于 1 MB。");
    try {
      apply(JSON.parse(await file.text()));
    } catch {
      toast("无法读取 JSON 文件。");
    }
  };
}
function choosePreset() {
  if (!editable()) return;
  dialog(
    "选择阵型",
    `<p>为${team === "red" ? "朱砂军" : "靛蓝军"}招募一支预设军队。军费不足时会按顺序精简。</p>${[
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
        commit();
        config[team] = [];
        for (const u of formation(b.dataset.preset, team))
          if (teamCost(config, team) + UNITS[u.type].cost <= budget())
            config[team].push(u);
        redraw();
        $("#modal").close();
        toast("阵型已就位。");
      }),
  );
}
function loadMission(index) {
  series = null;
  $("#bo3-enabled").checked = false;
  playMode = "sandbox";
  hotseatStage = null;
  reset();
  history = [];
  missionIndex = index;
  const m = MISSIONS[index];
  config = {
    version: VERSION,
    map: m.map,
    seed: 20261116,
    budget: Math.max(m.budget, cost(formation(m.enemy, "blue"))),
    red: [],
    blue: missionArmy(m),
    props: [],
  };
  team = "red";
  view.map(m.map);
  redraw();
  $("#modal").close();
  toast("布置朱砂军，完成试炼。");
}
function chooseMission() {
  if (mode === "battle") return toast("请先返回布阵。");
  dialog(
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
    .forEach((b) => (b.onclick = () => loadMission(Number(b.dataset.mission))));
}
function validPlacement(p) {
  return (
    !!p &&
    canPlace(
      config,
      team,
      { type: catalog === "units" ? selected : selectedProp, x: p.x, z: p.z },
      catalog === "units" ? "unit" : "prop",
    )
  );
}
function terrainInfo() {
  dialog(
    "地形与道具",
    `<h3>${MAPS[config.map].name}</h3><p>${MAPS[config.map].description}<br>${MAPS[config.map].hint}</p><p>切换左侧「道具」可在本方放置，每方最多 ${MAX_PROPS} 件，与兵种共用军费。右键点击或 Shift + 点击移除自己布置的道具。地图自带的中立道具无法在布阵时移除。</p><div class="field-notes">${Object.values(
      PROPS,
    )
      .map(
        (p) =>
          `<article><h3>${p.glyph} ${p.name} · ${p.cost} ◇</h3><p>${p.description}</p></article>`,
      )
      .join(
        "",
      )}</div><h3>利用地形</h3><p>废墟墙可以挡住低弧度弹药；投石车的高抛弹道更容易越过。高台比敌方高出一米时，远程射程增加 18%。沼泽内速度降为 45%，冰面上的横向控制更弱。冲击波和弹射都可能让单位跌落。</p>`,
  );
}
function help() {
  dialog(
    "战场手册",
    `<p>先排兵，再布置道具。骑兵需要冲锋距离，军医提供持续治疗，投石车可以越过低矮掩体。每个士兵都有实体碰撞；箭、炸弹、冲击波与重力会共同决定结果。</p><h3>如何操作</h3><ul><li>选兵种，左键点击本方半场放置。右键点击士兵移除，或按住 Shift 点击。</li><li>拖动旋转视角，滚轮缩放，右键拖动平移。触屏双指缩放。</li><li><span class="key">空格</span> 开战 / 暂停，<span class="key">R</span> 返回布阵，<span class="key">Ctrl Z</span> 撤销布阵。</li><li>自由沙盘可编辑双方；玩家 vs AI 编辑红方；自动演武由双方 AI 布阵。热座按红方 → 封存交接 → 蓝方 → 揭晓开战操作。</li><li>混编整队可选方阵、横排、楔形并镜像追加。框选模式拖动地面矩形或逐个点击；选中同兵种后可移动、删除、改姿态。取消选择后恢复视角旋转。</li><li>悬停部队或道具查看地面射程圈；高地加成按最近公开敌军的地面高度计算。骑兵箭头表示开场冲锋方向。</li></ul><h3>几条实用战术</h3><ul><li>盾卫面向敌军才能减伤。长枪在盾后也能攻击。</li><li>弓手会预测移动目标，但箭可能撞上友军。</li><li>炸弹 1.8 秒引爆，双方都会受到伤害与击飞。</li><li>危桥上的单位会尝试走桥；拥挤或冲击可能把它推下去。</li><li>交战上限 90 秒；超时按剩余血量折算的兵力价值决胜。</li></ul><h3>关于这个版本</h3><p>原创开源 Alpha 0.4。公开侦察 AI、Bo3 复盘调阵、统帅军令、合成音效、实时战力与收藏夹。十种兵种、六张地图、五种可布置道具。近战使用距离与冷却判定，移动、碰撞、抛射与倒地由 Rapier 求解。玩法灵感来自《纸上谈兵》和全面战争式物理沙盘。</p>`,
  );
}

async function boot() {
  try {
    favorites = readFavorites(
      JSON.parse(localStorage.getItem("frontline-favorites-v1") || "[]"),
    );
  } catch {}
  try {
    const p = JSON.parse(localStorage.getItem("frontline-feedback") || "{}");
    if (p.volume !== undefined) audio.setVolume(p.volume);
    audio.setMuted(p.muted);
    $("#camera-shake").checked = p.shake !== false;
  } catch {}
  $("#volume").value = Math.round(audio.volume * 100);
  const saveFeedback = () => {
    try {
      localStorage.setItem(
        "frontline-feedback",
        JSON.stringify({
          volume: audio.volume,
          muted: audio.muted,
          shake: $("#camera-shake").checked,
        }),
      );
    } catch {}
  };
  const syncAudio = () => {
    $("#audio-toggle").textContent = audio.muted ? "♫×" : "♫";
    $("#audio-toggle").setAttribute("aria-pressed", String(audio.muted));
    $("#audio-toggle").title = audio.muted ? "开启声音" : "静音";
  };
  syncAudio();
  document.addEventListener(
    "pointerdown",
    () => {
      if (!audio.context || audio.context.state === "suspended") audio.unlock();
    },
    { capture: true },
  );
  $("#audio-toggle").onclick = () => {
    audio.setMuted(!audio.muted);
    syncAudio();
    saveFeedback();
  };
  $("#volume").oninput = (e) => {
    audio.setVolume(Number(e.target.value) / 100);
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
  renderCatalog();
  $("#map").innerHTML = Object.entries(MAPS)
    .map(([id, m]) => `<option value="${id}">${m.name}</option>`)
    .join("");
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey));
    if (stored) config = validateConfig(stored);
  } catch {}
  try {
    view = new BattlefieldView($("#viewport"));
    view.shakeEnabled = $("#camera-shake").checked;
    let quality = "balanced";
    try {
      quality = localStorage.getItem("frontline-quality") || "balanced";
    } catch {}
    if (!["low", "balanced", "high"].includes(quality)) quality = "balanced";
    view.setQuality(quality);
    $("#quality").value = quality;
    view.map(config.map);
    redraw();
    await initPhysics();
    $("#loading").classList.add("hidden");
  } catch (e) {
    $("#loading").innerHTML =
      `<h2>战场加载失败</h2><p>${escape(e.message)}</p><p>请使用支持 WebGL 2 的浏览器，并开启硬件加速。</p>`;
    console.error(e);
    return;
  }
  $("#team-red").onclick = () => {
    if (!editable()) return;
    team = "red";
    selection = [];
    redraw();
  };
  $("#catalog-units").onclick = () => {
    catalog = "units";
    renderCatalog();
    updatePanel();
  };
  $("#catalog-props").onclick = () => {
    catalog = "props";
    renderCatalog();
    updatePanel();
  };
  $("#quality").onchange = (e) => {
    view.setQuality(e.target.value);
    try {
      localStorage.setItem("frontline-quality", e.target.value);
    } catch {}
  };
  $("#terrain-info").onclick = terrainInfo;
  $("#camera-shake").onchange = (e) => {
    view.shakeEnabled = e.target.checked;
    saveFeedback();
  };
  $("#camera-preset").onchange = (e) => view.cameraPreset(e.target.value);
  $("#viewport").addEventListener("camera-manual", () => {
    $("#camera-preset").value = "overview";
  });
  $("#favorites").onclick = favoriteDialog;
  $("#commander-enabled").onchange = (e) => {
    config.commander = { enabled: e.target.checked, aiTeams: [] };
    persist();
  };
  $("#bo3-enabled").onchange = () => {
    series = null;
    updatePanel();
  };
  $("#restart-series").onclick = () => {
    if (mode === "battle") return toast("请先返回布阵。");
    const initial = series?.initial;
    series = null;
    reset();
    if (initial) config = clone(initial);
    redraw();
  };
  $("#command-team").onchange = () => {
    pendingOrder = null;
    updateCommandUI();
  };
  document.querySelectorAll("[data-order]").forEach(
    (b) =>
      (b.onclick = () => {
        pendingOrder = b.dataset.order;
        updateCommandUI();
        toast("点选战场地面，指定" + ORDERS[pendingOrder].name + "区域。");
      }),
  );
  $("#cancel-order").onclick = () => {
    pendingOrder = null;
    view.preview(config, null);
    updateCommandUI();
  };
  const compact = document.createElement("button");
  compact.className = "compact-panel-toggle";
  compact.textContent = "⚑ 兵种 / 布阵";
  compact.onclick = () => {
    const open = document.body.classList.toggle("compact-catalog-open");
    compact.textContent = open ? "收起兵种 ×" : "⚑ 兵种 / 布阵";
    if (open) {
      catalog = "units";
      renderCatalog();
      updatePanel();
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };
  document.body.append(compact);
  $("#team-blue").onclick = () => {
    if (!editable()) return;
    team = "blue";
    selection = [];
    redraw();
  };
  $("#map").onchange = (e) => changeMap(e.target.value);
  $("#play-mode").onchange = (e) => changePlayMode(e.target.value);
  $("#generate-ai").onclick = () => {
    commit();
    refreshAI();
  };
  for (const id of ["red-ai", "blue-ai"])
    $("#" + id).onchange = () => refreshAI();
  $("#hotseat-ready").onclick = () => {
    hotseatStage = nextHotseat(hotseatStage, config);
    team = "blue";
    history = [];
    $("#hotseat-cover").classList.add("hidden");
    redraw();
  };
  $("#squad-builder").onclick = buildSquad;
  $("#select-tool").onclick = () => {
    if (!editable()) return;
    editTool = editTool === "select" ? "place" : "select";
    redraw();
  };
  $("#select-type").onclick = () => {
    if (!editable()) return;
    selection = config[team].flatMap((u, i) =>
      u.type === selected ? [i] : [],
    );
    editTool = "select";
    redraw();
  };
  $("#clear-selection").onclick = () => {
    selection = [];
    editTool = "place";
    redraw();
  };
  $("#move-selection").onclick = () => {
    if (!editable()) return;
    if (!selection.length) return toast("请先框选或选中同兵种部队。");
    editTool = "move";
    view.controls.enabled = true;
    updatePanel();
    toast("点击本方地面，选中部队中心将移动到该处。");
  };
  $("#delete-selection").onclick = () => {
    if (!editable() || !selection.length) return;
    commit();
    config[team] = config[team].filter((u, i) => !selection.includes(i));
    selection = [];
    redraw();
  };
  $("#apply-stance").onclick = () => {
    if (!editable() || !selection.length) return toast("请先选中部队。");
    commit();
    for (const i of selection) config[team][i].stance = $("#stance").value;
    redraw();
    toast("选中部队姿态已更新。");
  };
  $("#preset").onclick = choosePreset;
  $("#start").onclick = start;
  $("#pause").onclick = () => {
    paused = !paused;
    $("#pause").textContent = paused ? "▶ 继续" : "Ⅱ 暂停";
    if (mode === "battle") view.frame(battle.snapshot(), paused);
    updateCommandUI();
  };
  $("#clear").onclick = () => {
    if (!editable()) return;
    commit();
    config[team] = [];
    config.props = (config.props || []).filter((p) => p.team !== team);
    redraw();
  };
  $("#undo").onclick = () => {
    if (!editable() || !history.length) return;
    const previous = history.pop();
    config = previous;
    view.map(config.map);
    redraw();
  };
  $("#camera").onclick = () => {
    view.resetCamera();
    $("#camera-preset").value = "overview";
  };
  $("#help").onclick = help;
  $("#workshop").onclick = workshop;
  $("#campaign").onclick = chooseMission;
  $("#sandbox").onclick = () => {
    if (mode === "battle") return toast("请先返回布阵。");
    playMode = "sandbox";
    series = null;
    $("#bo3-enabled").checked = false;
    hotseatStage = null;
    reset();
    history = [];
    missionIndex = null;
    config = defaultConfig(config.map);
    team = "red";
    view.map(config.map);
    redraw();
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
        speed = Number(b.dataset.speed);
        document
          .querySelectorAll("[data-speed]")
          .forEach((x) => x.classList.toggle("active", x === b));
      }),
  );
  const canvas = view.renderer.domElement;
  let down = null;
  canvas.addEventListener("pointerdown", (e) => {
    down = {
      x: e.clientX,
      y: e.clientY,
      button: e.button,
      point: view.pick(e.clientX, e.clientY),
    };
  });
  canvas.addEventListener("pointermove", (e) => {
    if (mode === "battle" && pendingOrder) {
      const p = view.pick(e.clientX, e.clientY);
      orderPoint = p ? { x: p.x, z: p.z } : null;
      view.preview(
        config,
        p ? { type: pendingOrder, x: p.x, z: p.z } : null,
        "order",
        $("#command-team").value,
      );
      return;
    }
    if (mode === "battle" && paused) {
      const p = view.pick(e.clientX, e.clientY),
        state = battle.snapshot(),
        u = p
          ? state.units.find(
              (u) => u.alive && Math.hypot(u.t[0] - p.x, u.t[2] - p.z) < 0.9,
            )
          : null;
      const data = view.preview(
        config,
        u ? { type: u.type, x: u.t[0], z: u.t[2] } : null,
        "unit",
        u?.team || "red",
      );
      $("#preview-label").classList.toggle("hidden", !u);
      if (u)
        $("#preview-label").textContent =
          `${UNITS[u.type].name} · ${STANCES[u.stance]} · 射程 ${data.radius.toFixed(1)} 米`;
      return;
    }
    if (mode === "deploy") {
      const p = view.pick(e.clientX, e.clientY);
      if (p) {
        p.x = Math.round(p.x * 2) / 2;
        p.z = Math.round(p.z * 2) / 2;
      }
      view.hover(
        p,
        validPlacement(p),
        catalog === "props"
          ? PROPS[selectedProp].radius
          : 0.62 * UNITS[selected].scale,
      );
      const visible =
        playMode === "hotseat" ? hotseatView(config, hotseatStage) : config;
      let item = null,
        kind = "unit",
        itemTeam = team;
      if (p) {
        for (const t of ["red", "blue"]) {
          const found = visible[t].find(
            (u) => Math.hypot(u.x - p.x, u.z - p.z) < 0.85,
          );
          if (found) {
            item = found;
            itemTeam = t;
            break;
          }
        }
        if (!item) {
          const prop = [...MAPS[config.map].props, ...visible.props].find(
            (q) =>
              Math.hypot(q.x - p.x, q.z - p.z) < PROPS[q.type].radius + 0.25,
          );
          if (prop) {
            item = prop;
            kind = "prop";
            itemTeam = prop.team || team;
          }
        }
        if (!item && editTool === "place") {
          item = {
            ...p,
            type: catalog === "units" ? selected : selectedProp,
            stance: $("#stance").value,
          };
          kind = catalog === "units" ? "unit" : "prop";
        }
      }
      const data = view.preview(visible, item, kind, itemTeam);
      if (editTool === "select" && down?.button === 0)
        view.selectionBox(down.point, p);
      $("#preview-label").classList.toggle("hidden", !data);
      if (data)
        $("#preview-label").textContent =
          `${kind === "unit" ? UNITS[item.type].name + " · " + STANCES[item.stance || "advance"] : PROPS[item.type].name} · ${kind === "unit" ? "攻击射程" : "作用半径"} ${data.radius.toFixed(1)} 米${data.bonus ? " · 对最近敌军高地 +18%" : ""}${data.charge ? " · 箭头为开场冲锋方向" : ""}`;
    }
  });
  canvas.addEventListener("pointerleave", () => {
    view.hover(null, false);
    view.preview(config, null);
    $("#preview-label").classList.add("hidden");
  });
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());
  canvas.addEventListener("pointerup", (e) => {
    if (
      mode === "battle" &&
      pendingOrder &&
      down?.button === 0 &&
      !paused &&
      Math.hypot(down.x - e.clientX, down.y - e.clientY) < 6
    ) {
      const p = view.pick(e.clientX, e.clientY);
      if (!p) return;
      const result = battle.issueOrder($("#command-team").value, {
        type: pendingOrder,
        x: p.x,
        z: p.z,
      });
      if (result.ok) {
        pendingOrder = null;
        audio.play("order");
        toast("军令已下达。");
      } else toast(result.reason);
      updateCommandUI();
      return;
    }
    if (
      mode === "deploy" &&
      down &&
      editTool === "select" &&
      down.button === 0
    ) {
      const p = view.pick(e.clientX, e.clientY),
        a = down.point;
      if (!p || !a || !editable()) return;
      if (Math.hypot(down.x - e.clientX, down.y - e.clientY) > 6)
        selection = config[team].flatMap((u, i) =>
          u.x >= Math.min(a.x, p.x) &&
          u.x <= Math.max(a.x, p.x) &&
          u.z >= Math.min(a.z, p.z) &&
          u.z <= Math.max(a.z, p.z)
            ? [i]
            : [],
        );
      else {
        const i = config[team].findIndex(
          (u) => Math.hypot(u.x - p.x, u.z - p.z) < 0.9,
        );
        if (i >= 0)
          selection = selection.includes(i)
            ? selection.filter((n) => n !== i)
            : [...selection, i];
      }
      redraw();
      down = null;
      return;
    }
    if (
      !down ||
      Math.hypot(down.x - e.clientX, down.y - e.clientY) > 6 ||
      mode !== "deploy"
    )
      return;
    const p = view.pick(e.clientX, e.clientY);
    if (!p) return;
    p.x = Math.round(p.x * 2) / 2;
    p.z = Math.round(p.z * 2) / 2;
    if (
      playMode === "ai-ai" ||
      (playMode === "hotseat" && hotseatStage !== team)
    )
      return;
    if (editTool === "move" && down.button === 0) {
      try {
        const center = selection.reduce(
          (s, i) => ({
            x: s.x + config[team][i].x / selection.length,
            z: s.z + config[team][i].z / selection.length,
          }),
          { x: 0, z: 0 },
        );
        const next = moveSquad(
            config,
            team,
            selection,
            p.x - center.x,
            p.z - center.z,
          ),
          n = selection.length;
        commit();
        config = next;
        selection = Array.from(
          { length: n },
          (_, i) => config[team].length - n + i,
        );
        editTool = "select";
        redraw();
      } catch (error) {
        toast(error.message);
      }
      return;
    }
    if (down.button === 2 || e.shiftKey) {
      let nearest = -1,
        distance = 1.1;
      config[team].forEach((u, i) => {
        const d = Math.hypot(u.x - p.x, u.z - p.z);
        if (d < distance) {
          distance = d;
          nearest = i;
        }
      });
      if (nearest >= 0) {
        commit();
        config[team].splice(nearest, 1);
        redraw();
      } else {
        const index = (config.props || []).findIndex(
          (q) =>
            q.team === team &&
            Math.hypot(q.x - p.x, q.z - p.z) < PROPS[q.type].radius + 0.3,
        );
        if (index >= 0) {
          commit();
          config.props.splice(index, 1);
          redraw();
        }
      }
      return;
    }
    if (down.button !== 0) return;
    if (catalog === "props") {
      if (
        (config.props || []).filter((q) => q.team === team).length >= MAX_PROPS
      )
        return toast("每方最多 " + MAX_PROPS + " 件道具。");
      if (teamCost(config, team) + PROPS[selectedProp].cost > budget())
        return toast("军费不足。");
      if (!validPlacement(p))
        return toast("请在本方空地放置，并为道具留出空间。");
      commit();
      config.props ||= [];
      config.props.push({ type: selectedProp, team, x: p.x, z: p.z });
      redraw();
      return;
    }
    if (config[team].length >= MAX_UNITS)
      return toast("每方最多 " + MAX_UNITS + " 人。");
    if (teamCost(config, team) + UNITS[selected].cost > budget())
      return toast("军费不足，试试更便宜的兵种。");
    if (!validPlacement(p))
      return toast("请放在本方空地上，并留出士兵之间的间距。");
    commit();
    config[team].push({
      type: selected,
      x: p.x,
      z: p.z,
      stance: $("#stance").value,
    });
    audio.play("place");
    redraw();
  });
  window.addEventListener("keydown", (e) => {
    if (
      $("#modal").open ||
      ["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement.tagName)
    )
      return;
    if (e.code === "Space") {
      e.preventDefault();
      if (mode === "deploy") start();
      else if (mode === "battle" || mode === "replay") $("#pause").click();
    }
    if (e.key.toLowerCase() === "r") reset();
    if (e.ctrlKey && e.key.toLowerCase() === "z") {
      e.preventDefault();
      $("#undo").click();
    }
  });
  document.addEventListener("visibilitychange", () => {
    last = 0;
    accumulator = 0;
  });
  function animate(now) {
    requestAnimationFrame(animate);
    const delta = last ? Math.min((now - last) / 1000, 0.1) : 0;
    last = now;
    if (mode === "battle" && !paused) {
      accumulator += delta * speed;
      let steps = 0;
      while (accumulator >= DT && steps < 24 && !battle.result) {
        battle.step();
        accumulator -= DT;
        steps++;
        if (battle.tick % 6 === 0) frames.push(battle.snapshot());
      }
      const state = battle.snapshot((liveState ||= {}));
      view.frame(state);
      if (pendingOrder && orderPoint)
        view.preview(
          config,
          { ...orderPoint, type: pendingOrder },
          "order",
          $("#command-team").value,
        );
      audio.consume(state.cues);
      if (Math.floor(state.time * 10) !== statusTick) {
        statusTick = Math.floor(state.time * 10);
        updatePower(state.units);
        updateCommandUI(state);
        $("#battle-time").textContent = state.time.toFixed(1) + " 秒 / 90 秒";
        $("#red-count").textContent = state.units.filter(
          (u) => u.alive && u.team === "red",
        ).length;
        $("#blue-count").textContent = state.units.filter(
          (u) => u.alive && u.team === "blue",
        ).length;
      }
      if (battle.result) {
        if (battle.tick % 6 !== 0) frames.push(battle.snapshot());
        finish();
      }
    } else if (mode === "replay") {
      if (!paused)
        replayTime = Math.min(frames.at(-1).time, replayTime + delta * speed);
      showReplay();
      if (replayTime >= frames.at(-1).time) {
        paused = true;
        $("#pause").textContent = "▶ 继续";
      }
    }
    view.render(now / 1000);
    if (Math.floor(now / 500) !== Math.floor((now - delta * 1000) / 500))
      $("#performance").textContent = `${Math.round(view.metrics.fps)} FPS`;
  }
  requestAnimationFrame(animate);
}
boot();
