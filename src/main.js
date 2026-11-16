import "./style.css";
import "./mobile.css";
import "./expansion.css";
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
function editable() {
  if (mode !== "deploy") {
    toast("先点击「返回布阵」，再调整军队。");
    return false;
  }
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
    $("#red-count").textContent = config.red.length;
    $("#blue-count").textContent = config.blue.length;
  }
  $("#team-red").classList.toggle("active", team === "red");
  $("#team-blue").classList.toggle("active", team === "blue");
  $("#team-blue").disabled = missionIndex !== null;
  document.querySelectorAll(".unit-card").forEach((b) => {
    const definitions = catalog === "units" ? UNITS : PROPS,
      choice = catalog === "units" ? selected : selectedProp;
    b.classList.toggle("active", b.dataset.type === choice);
    b.disabled =
      mode !== "deploy" || available < definitions[b.dataset.type].cost;
  });
  for (const id of ["preset", "clear", "undo", "stance", "map"])
    $("#" + id).disabled =
      mode !== "deploy" ||
      (id === "map" && missionIndex !== null) ||
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
}
function redraw() {
  view.deployment(config);
  updatePanel();
  persist();
}
function reset() {
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
  redraw();
}
function changeMap(id) {
  if (!editable()) return;
  commit();
  config.map = id;
  config.budget = Math.max(2200, config.budget);
  config.props = [];
  config.red = formation("balanced");
  config.blue = formation("balanced", "blue");
  view.map(id);
  redraw();
}
function start() {
  if (mode === "battle" || mode === "replay" || mode === "ended") {
    reset();
    return;
  }
  if (!config.red.length || !config.blue.length) {
    toast("双方都需要至少一名士兵。");
    return;
  }
  try {
    validateConfig(config);
    battle = new Battle(config);
  } catch (e) {
    toast(e.message);
    return;
  }
  mode = "battle";
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
  const r = battle.result,
    winner =
      r.winner === "draw"
        ? "势均力敌"
        : r.winner === "red"
          ? "朱砂军获胜"
          : "靛蓝军获胜";
  const victory = missionIndex !== null && r.winner === "red";
  $("#red-count").textContent = r.red;
  $("#blue-count").textContent = r.blue;
  $("#battle-time").textContent = r.time.toFixed(1) + " 秒 / 90 秒";
  if (victory)
    try {
      localStorage.setItem("frontline-mission-" + missionIndex, "won");
    } catch {}
  $("#result").innerHTML =
    `<span class="eyebrow">${victory ? "TRIAL COMPLETED" : "THE DUST SETTLES"}</span><h2 class="${r.winner}">${winner}</h2><p>用时 ${r.time.toFixed(1)} 秒 · 剩余 ${r.red} : ${r.blue}<br>${r.reason === "timeout" ? "时间耗尽，按存活兵力价值判定。" : "最后一名敌人，已离开战场。"}</p><button id="watch-replay">▶ 战斗回放</button><button id="retry">调整阵型</button>${missionIndex !== null && missionIndex < MISSIONS.length - 1 ? '<button id="next-mission">下一试炼 →</button>' : ""}`;
  $("#result").classList.remove("hidden");
  $("#watch-replay").onclick = beginReplay;
  $("#retry").onclick = reset;
  if ($("#next-mission"))
    $("#next-mission").onclick = () => loadMission(missionIndex + 1);
}
function beginReplay() {
  if (!frames.length) {
    toast("先完成一场战斗，即可回放。");
    return;
  }
  mode = "replay";
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
  view.frame(frames[i]);
  $("#timeline").value = i;
  $("#replay-position").textContent = frames[i].time.toFixed(1) + " s";
  $("#battle-time").textContent = frames[i].time.toFixed(1) + " 秒";
  const counts = (t) =>
    frames[i].units.filter((u) => u.alive && u.team === t).length;
  $("#red-count").textContent = counts("red");
  $("#blue-count").textContent = counts("blue");
}
function saveJson(name, value) {
  const a = document.createElement("a"),
    url = URL.createObjectURL(
      new Blob([JSON.stringify(value, null, 2)], { type: "application/json" }),
    );
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
function workshop() {
  dialog(
    "策略与战场档案",
    `<p>把布阵文件交给 AI，让它研究兵种、坐标与姿态，写出自己的军队。这里保存完整对局配置；导入不会运行任何脚本。</p><div class="settings"><label for="seed">对局种子</label><input id="seed" type="number" min="0" max="4294967295" value="${config.seed}" ${mode !== "deploy" ? "disabled" : ""}><button class="action" id="apply-seed" ${mode !== "deploy" ? "disabled" : ""}>应用</button></div><button class="action" id="export-config">导出对局 JSON</button><button class="action" id="import-config">从文件导入</button><button class="action" id="save-local">保存当前阵型</button><input id="file-input" type="file" accept=".json,application/json" hidden><h3>编辑对局</h3><p>每方最多 ${MAX_UNITS} 人。x 为左右方向，z 为前后方向；红方 x ≤ -2.5，蓝方 x ≥ 2.5。姿态：advance / hold / flank。</p><textarea id="config-editor" spellcheck="false" aria-label="对局配置 JSON">${escape(JSON.stringify(config, null, 2))}</textarea><button class="action" id="apply-config" ${mode !== "deploy" ? "disabled" : ""}>载入编辑的对局</button><h3>复现与评测</h3><p>项目提供无界面的 Node.js 评测命令、策略示例和 AI 提示词。回放会记录当前对局的物理轨迹；相同引擎版本与种子可复现模拟。完整文档见项目 README。</p>`,
  );
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
    const n = Number($("#budget-setting").value);
    if (["red", "blue"].some((t) => teamCost(config, t) > n))
      return toast("当前兵力和道具超出这档军费，请先精简。");
    commit();
    config.budget = n;
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
    blue: formation(m.enemy, "blue"),
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
  if (!p) return false;
  if (team === "red" ? p.x > -2.5 : p.x < 2.5) return false;
  if (
    Math.abs(p.x) > 16.8 ||
    Math.abs(p.z) > 10.8 ||
    !groundAt(config.map, p.x, p.z)
  )
    return false;
  const radius =
    catalog === "units"
      ? 0.62 * UNITS[selected].scale
      : PROPS[selectedProp].radius;
  if (
    MAPS[config.map].walls.some(
      (w) =>
        Math.abs(p.x - w.x) < w.hx + radius &&
        Math.abs(p.z - w.z) < w.hz + radius,
    )
  )
    return false;
  if (
    [...MAPS[config.map].props, ...(config.props || [])].some(
      (q) =>
        !(catalog === "units" && ["spring", "spikes"].includes(q.type)) &&
        Math.hypot(p.x - q.x, p.z - q.z) < radius + PROPS[q.type].radius,
    )
  )
    return false;
  return !config[team].some(
    (u) =>
      Math.hypot(p.x - u.x, p.z - u.z) < 0.62 * UNITS[u.type].scale + radius,
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
    `<p>先排兵，再布置道具。骑兵需要冲锋距离，军医提供持续治疗，投石车可以越过低矮掩体。每个士兵都有实体碰撞；箭、炸弹、冲击波与重力会共同决定结果。</p><h3>如何操作</h3><ul><li>选兵种，左键点击本方半场放置。右键点击士兵移除，或按住 Shift 点击。</li><li>拖动旋转视角，滚轮缩放，右键拖动平移。触屏双指缩放。</li><li><span class="key">空格</span> 开战 / 暂停，<span class="key">R</span> 返回布阵，<span class="key">Ctrl Z</span> 撤销布阵。</li><li>自由沙盘可编辑双方；战术试炼只编辑朱砂军。</li></ul><h3>几条实用战术</h3><ul><li>盾卫面向敌军才能减伤。长枪在盾后也能攻击。</li><li>弓手会预测移动目标，但箭可能撞上友军。</li><li>炸弹 1.8 秒引爆，双方都会受到伤害与击飞。</li><li>危桥上的单位会尝试走桥；拥挤或冲击可能把它推下去。</li><li>交战上限 90 秒；超时按剩余血量折算的兵力价值决胜。</li></ul><h3>关于这个版本</h3><p>原创开源 Alpha 0.2。十种兵种、六张地图、五种可布置道具、关节布娃娃。近战使用距离与冷却判定，移动、碰撞、抛射与倒地由 Rapier 求解。玩法灵感来自《纸上谈兵》和全面战争式物理沙盘。</p>`,
  );
}

async function boot() {
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
    updatePanel();
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
  $("#team-blue").onclick = () => {
    if (!editable()) return;
    team = "blue";
    updatePanel();
  };
  $("#map").onchange = (e) => changeMap(e.target.value);
  $("#preset").onclick = choosePreset;
  $("#start").onclick = start;
  $("#pause").onclick = () => {
    paused = !paused;
    $("#pause").textContent = paused ? "▶ 继续" : "Ⅱ 暂停";
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
  $("#camera").onclick = () => view.resetCamera();
  $("#help").onclick = help;
  $("#workshop").onclick = workshop;
  $("#campaign").onclick = chooseMission;
  $("#sandbox").onclick = () => {
    if (mode === "battle") return toast("请先返回布阵。");
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
    down = { x: e.clientX, y: e.clientY, button: e.button };
  });
  canvas.addEventListener("pointermove", (e) => {
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
    }
  });
  canvas.addEventListener("pointerleave", () => view.hover(null, false));
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());
  canvas.addEventListener("pointerup", (e) => {
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
      if (Math.floor(state.time * 10) !== statusTick) {
        statusTick = Math.floor(state.time * 10);
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
