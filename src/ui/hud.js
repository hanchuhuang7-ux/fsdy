import { highlightFrameIndex, highlightText } from "../highlights.js";

import { UNITS, MISSIONS, combatPower } from "../data.js";
import { $ } from "./dom.js";

export function createHud(app, actions) {
  function updatePower(units, deploy = false) {
    const values = combatPower(units);
    if (deploy) app.initialPower = { ...values };
    for (const t of ["red", "blue"]) {
      const hidden =
        app.mode === "deploy" &&
        app.playMode === "hotseat" &&
        app.hotseatStage !== "revealed" &&
        app.hotseatStage !== t;
      $("#" + t + "-power-value").textContent = hidden
        ? "?"
        : Math.round(values[t]).toLocaleString();
      $("#" + t + "-power-fill").style.width =
        (hidden
          ? 0
          : Math.min(
              100,
              (values[t] / Math.max(1, app.initialPower[t])) * 100,
            )) + "%";
    }
  }
  function updateCommandUI(state = null) {
    const enabled = (app.replayConfig || app.config).commander?.enabled;
    $("#command-dock").classList.toggle(
      "hidden",
      !enabled || !["battle", "replay"].includes(app.mode),
    );
    if (!enabled) return;
    const own = state?.commander || app.battle?.orderState,
      tick = state?.tick ?? app.battle?.tick ?? 0;
    if (!own) return;
    const t = $("#command-team").value,
      ai = (app.replayConfig || app.config).commander.aiTeams.includes(t),
      cool = Math.max(0, (own.readyAt[t] - tick) / 60);
    $("#command-status").textContent =
      `${ai ? "AI · " : ""}${own.points[t]} 点 · ${cool > 0 ? cool.toFixed(1) + "s 冷却" : "可下令"}${app.pendingOrder ? " · 点地面指定区域" : ""}`;
    $("#command-team").disabled =
      app.playMode === "human-ai" ||
      app.missionIndex !== null ||
      app.mode === "replay";
    for (const b of document.querySelectorAll("[data-order]")) {
      b.disabled =
        app.mode !== "battle" ||
        ai ||
        own.points[t] < 1 ||
        cool > 0 ||
        app.paused;
      b.classList.toggle("active", app.pendingOrder === b.dataset.order);
    }
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
  function finish() {
    app.mode = "ended";
    $("#pause").classList.add("hidden");
    $("#status").textContent = "战斗结束";
    $("#status-dot").style.background = "#728e5f";
    const r = app.battle?.result || app.lastResult,
      winner =
        r.winner === "draw"
          ? "势均力敌"
          : r.winner === "red"
            ? "朱砂军获胜"
            : "靛蓝军获胜";
    const victory = app.missionIndex !== null && r.winner === "red";
    app.lastResult = r;
    renderHighlights(r.highlights || [], Math.round(r.time * 60));
    if (app.series && !app.roundRecorded && app.battle?.result) {
      app.series.record(app.replayConfig, r);
      app.roundRecorded = true;
    }
    $("#red-count").textContent = r.red;
    $("#blue-count").textContent = r.blue;
    $("#battle-time").textContent = r.time.toFixed(1) + " 秒 / 90 秒";
    if (victory)
      try {
        localStorage.setItem("frontline-mission-" + app.missionIndex, "won");
      } catch {}
    $("#result").innerHTML =
      `<span class="eyebrow">${victory ? "TRIAL COMPLETED" : "THE DUST SETTLES"}</span><h2 class="${r.winner}">${winner}</h2><p>用时 ${r.time.toFixed(1)} 秒 · 剩余 ${r.red} : ${r.blue}<br>${r.reason === "timeout" ? "时间耗尽，按存活兵力价值判定。" : "最后一名敌人，已离开战场。"}</p>${actions.contributionsHTML(r.report)}<div class="result-actions"><button id="watch-replay">▶ 战斗回放</button><button id="export-replay">导出完整回放</button><button id="export-seed">导出种子重演包</button><button id="retry">调整阵型</button>${app.missionIndex !== null && app.missionIndex < MISSIONS.length - 1 ? '<button id="next-mission">下一试炼 →</button>' : ""}</div>`;
    $("#result").classList.remove("hidden");
    if (app.series) {
      const note = document.createElement("div");
      note.className = "series-note";
      note.textContent = `Bo3 红 ${app.series.wins.red} : ${app.series.wins.blue} 蓝${app.series.complete ? " · 系列赛结束" : " · 可复盘并调整下一局"}`;
      $("#result").prepend(note);
      if (!app.series.complete) {
        const next = document.createElement("button");
        next.className = "series-next";
        next.textContent =
          "复盘调阵 → 第 " + (app.series.rounds.length + 1) + " 局";
        next.onclick = actions.prepareNextRound;
        $(".result-actions").prepend(next);
      }
      const save = document.createElement("button");
      save.textContent = "导出 Bo3 档案";
      save.onclick = () =>
        actions.saveJson("frontline-bo3.json", app.series.export());
      $(".result-actions").append(save);
    }
    $("#watch-replay").onclick = actions.beginReplay;
    $("#retry").onclick = actions.reset;
    $("#export-replay").onclick = () => actions.exportReplay("trajectory");
    $("#export-seed").onclick = () => actions.exportReplay("seed");
    if ($("#next-mission"))
      $("#next-mission").onclick = () =>
        actions.loadMission(app.missionIndex + 1);
    actions.updateCommandUI();
    actions.updatePanel();
  }
  function renderHighlights(events = [], tick = 5400) {
    const feed = $("#battle-feed-list");
    feed.replaceChildren(
      ...events
        .filter((e) => e.tick <= tick)
        .slice(-5)
        .reverse()
        .map((e) => {
          const row = document.createElement("li");
          row.textContent = highlightText(e);
          return row;
        }),
    );
  }
  function beginReplay() {
    if (!app.frames.length) {
      actions.toast("先完成一场战斗，即可回放。");
      return;
    }
    app.mode = "replay";
    app.audio.reset();
    $(".replay-bar")?.remove();
    app.replayTime = 0;
    app.paused = false;
    app.view.clearActors();
    $("#result").classList.add("hidden");
    $("#pause").classList.remove("hidden");
    $("#pause").textContent = "Ⅱ 暂停";
    $("#status").textContent = "战斗回放";
    const bar = document.createElement("div");
    bar.className = "replay-bar";
    bar.innerHTML = `<span>REPLAY</span><div class="timeline-track"><input id="timeline" type="range" min="0" max="${app.frames.length - 1}" value="0" aria-label="回放进度"></div><span id="replay-position">0.0 s</span><button id="replay-result">查看战果</button>`;
    $(".arena").append(bar);
    const marks = document.createElement("div");
    marks.className = "highlight-marks";
    marks.setAttribute("aria-label", "回放高光");
    const groups = new Map();
    for (const event of app.lastResult?.highlights || []) {
      const list = groups.get(event.tick) || [];
      list.push(event);
      groups.set(event.tick, list);
    }
    for (const [tick, events] of groups) {
      const button = document.createElement("button");
      button.title = events.map(highlightText).join(" / ");
      button.setAttribute("aria-label", button.title);
      button.style.left =
        (100 * tick) / Math.max(1, Math.round(app.frames.at(-1).time * 60)) +
        "%";
      button.onclick = () => {
        $("#timeline").value = highlightFrameIndex(app.frames, tick);
        $("#timeline").dispatchEvent(new Event("input"));
      };
      marks.append(button);
    }
    bar.querySelector(".timeline-track").append(marks);
    $("#timeline").oninput = (e) => {
      app.replayTime = app.frames[Number(e.target.value)].time;
      app.paused = true;
      app.audio.cursor =
        app.frames[Number(e.target.value)].cues?.at(-1)?.id || 0;
      app.view.feedbackCursor = app.audio.cursor;
      $("#pause").textContent = "▶ 继续";
      actions.showReplay();
    };
    $("#replay-result").onclick = () => {
      bar.remove();
      actions.finish();
    };
    actions.showReplay();
  }
  function showReplay() {
    const i =
      app.replayTime >= app.frames.at(-1).time
        ? app.frames.length - 1
        : Math.min(app.frames.length - 1, Math.floor(app.replayTime * 10));
    app.view.frame(app.frames[i], app.paused);
    renderHighlights(
      app.lastResult?.highlights || [],
      Math.round(app.frames[i].time * 60),
    );
    actions.updatePower(app.frames[i].units);
    actions.updateCommandUI(app.frames[i]);
    app.audio.consume(app.frames[i].cues, !app.paused);
    $("#timeline").value = i;
    $("#replay-position").textContent = app.frames[i].time.toFixed(1) + " s";
    $("#battle-time").textContent = app.frames[i].time.toFixed(1) + " 秒";
    const counts = (t) =>
      app.frames[i].units.filter((u) => u.alive && u.team === t).length;
    $("#red-count").textContent = counts("red");
    $("#blue-count").textContent = counts("blue");
  }
  return {
    renderHighlights,
    updatePower,
    updateCommandUI,
    contributionsHTML,
    finish,
    beginReplay,
    showReplay,
  };
}
