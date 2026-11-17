import {
  replayPackage,
  validateReplay,
  createReplayBattle,
} from "../replay.js";

import {
  VERSION,
  PROPS,
  MAPS,
  MAX_UNITS,
  MAX_PROPS,
  cost,
  teamCost,
  validateConfig,
  combatPower,
} from "../data.js";
import { $, clone, escape } from "./dom.js";

export function createWorkshop(app, actions) {
  function toast(message) {
    if ($("#modal").open) {
      $("#modal-feedback").textContent = message;
      return;
    }
    $("#toast").textContent = message;
    $("#toast").classList.add("show");
    clearTimeout(app.toastTimer);
    app.toastTimer = setTimeout(
      () => $("#toast").classList.remove("show"),
      3200,
    );
  }
  function dialog(title, body) {
    $("#modal-feedback").textContent = "";
    $("#modal-title").textContent = title;
    $("#modal-body").innerHTML = body;
    $("#modal").showModal();
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
      actions.saveJson(
        `frontline-${kind}-replay.json`,
        app.replayEngineVersion === VERSION
          ? replayPackage(
              app.replayConfig || app.config,
              app.frames,
              app.lastResult,
              kind,
            )
          : validateReplay({
              format: "frontline-replay",
              version: app.replayEngineVersion === "0.4.0" ? 2 : 1,
              orderLog:
                app.lastResult?.orders ||
                app.frames.at(-1)?.commander?.log ||
                [],
              engineVersion: app.replayEngineVersion,
              kind,
              config: app.replayConfig || app.config,
              frames: kind === "trajectory" ? app.frames : undefined,
              result: kind === "trajectory" ? app.lastResult : undefined,
            }),
        true,
      );
      actions.toast("回放包已导出。");
    } catch (e) {
      actions.toast("回放导出失败：" + e.message);
    }
  }
  async function loadReplay(raw) {
    const pkg = validateReplay(raw);
    let loadedFrames = pkg.frames,
      result = pkg.result;
    if (pkg.kind === "seed") {
      actions.toast("正在按种子重演…");
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
    app.battle?.dispose();
    app.battle = null;
    app.series = null;
    app.audio.reset();
    app.replayEngineVersion = pkg.engineVersion;
    app.config = pkg.config;
    app.replayConfig = clone(app.config);
    app.lastResult = result;
    app.frames = loadedFrames;
    app.playMode = "sandbox";
    app.hotseatStage = null;
    app.missionIndex = null;
    app.selection = [];
    app.editTool = "place";
    app.team = "red";
    app.view.controls.enabled = true;
    app.initialPower = combatPower(loadedFrames[0].units);
    app.view.map(app.config.map);
    app.mode = "ended";
    actions.updatePanel();
    $("#modal").close();
    $("#start").innerHTML = "返回布阵 <span>↶</span>";
    actions.beginReplay();
  }
  function workshop() {
    if (app.playMode === "hotseat" && app.hotseatStage !== "revealed")
      return actions.toast("盲选封存期间，策略档案与导出已关闭。");
    actions.dialog(
      "策略与战场档案",
      `<p>把布阵文件交给 AI，让它研究兵种、坐标与姿态，写出自己的军队。这里保存完整对局配置；导入不会运行任何脚本。</p><div class="settings"><label for="seed">对局种子</label><input id="seed" type="number" min="0" max="4294967295" value="${app.config.seed}" ${app.mode !== "deploy" ? "disabled" : ""}><button class="action" id="apply-seed" ${app.mode !== "deploy" ? "disabled" : ""}>应用</button></div><button class="action" id="export-config">导出对局 JSON</button><button class="action" id="import-config">从文件导入</button><button class="action" id="save-local">保存当前阵型</button><input id="file-input" type="file" accept=".json,application/json" hidden><h3>编辑对局</h3><p>每方最多 ${MAX_UNITS} 人。x 为左右方向，z 为前后方向；红方 x ≤ -2.5，蓝方 x ≥ 2.5。姿态：advance / hold / flank / hunt；省略时默认 advance。可选统帅军令配置 commander: {enabled:true,aiTeams:[&quot;blue&quot;]}。</p><textarea id="config-editor" spellcheck="false" aria-label="对局配置 JSON">${escape(JSON.stringify(app.config, null, 2))}</textarea><button class="action" id="apply-config" ${app.mode !== "deploy" ? "disabled" : ""}>载入编辑的对局</button><h3>复现与评测</h3><p>项目提供无界面的 Node.js 评测命令、策略示例和 AI 提示词。回放会记录当前对局的物理轨迹；相同引擎版本与种子可复现模拟。完整文档见项目 README。</p>`,
    );
    const arenaButton = document.createElement("button");
    arenaButton.className = "action";
    arenaButton.id = "open-tournament";
    arenaButton.textContent = "演武擂台 / 循环赛排行 ↗";
    arenaButton.onclick = async () => {
      const { openTournament } = await import("./arena.js");
      openTournament(app, actions);
    };
    $("#modal-body").prepend(arenaButton);
    const replayTools = document.createElement("div");
    replayTools.innerHTML = `<h3>回放档案</h3><button class="action" id="import-replay">载入回放包 / 种子包</button><input id="replay-file" type="file" accept=".json,application/json" hidden><p>完整回放保存 10 Hz 轨迹；种子包使用同一版本引擎重新推演。回放文件最多 64 MB。</p>`;
    $("#modal-body").prepend(replayTools);
    $("#import-replay").onclick = () => {
      if (app.mode === "battle") return actions.toast("请先返回布阵。");
      $("#replay-file").click();
    };
    $("#replay-file").onchange = async (e) => {
      const f = e.target.files[0];
      if (!f) return;
      if (f.size > 64 * 1024 * 1024)
        return actions.toast("回放文件须小于 64 MB。");
      $("#import-replay").disabled = true;
      try {
        await actions.loadReplay(JSON.parse(await f.text()));
      } catch (error) {
        actions.toast("载入失败：" + error.message);
      } finally {
        if ($("#import-replay")) $("#import-replay").disabled = false;
      }
    };
    $("#export-config").onclick = () =>
      actions.saveJson("frontline-battle.json", app.config);
    const settings = document.createElement("div");
    settings.className = "settings";
    settings.innerHTML = `<label for="budget-setting">每方军费</label><select id="budget-setting" ${app.mode !== "deploy" || app.missionIndex !== null ? "disabled" : ""}>${[
      ...new Set([1500, 2200, 4000, 8000, 10000, app.config.budget]),
    ]
      .sort((a, b) => a - b)
      .map(
        (n) =>
          `<option value="${n}" ${n === app.config.budget ? "selected" : ""}>${n.toLocaleString()}</option>`,
      )
      .join(
        "",
      )}</select><button class="action" id="apply-budget" ${app.mode !== "deploy" || app.missionIndex !== null ? "disabled" : ""}>应用军费</button>`;
    $("#modal-body").prepend(settings);
    $("#apply-budget").onclick = async () => {
      if (app.series?.rounds.length)
        return actions.toast("系列赛内军费固定；重新开始系列赛后可修改。");
      const n = Number($("#budget-setting").value);
      if (["red", "blue"].some((t) => teamCost(app.config, t) > n))
        return actions.toast("当前兵力和道具超出这档军费，请先精简。");
      actions.commit();
      app.config.budget = n;
      await actions.refreshAI(false);
      actions.redraw();
      actions.updatePanel();
      actions.persist();
      $("#config-editor").value = JSON.stringify(app.config, null, 2);
      actions.toast("军费已更新。");
    };
    $("#save-local").onclick = () => {
      actions.persist();
      actions.toast("阵型已保存在此浏览器。");
    };
    $("#apply-seed").onclick = () => {
      if (app.series?.rounds.length)
        return actions.toast("系列赛种子按小局递增；重新开始后可修改。");
      const seed = Number($("#seed").value);
      if (!Number.isInteger(seed) || seed < 0 || seed > 0xffffffff)
        return actions.toast("请输入有效的 32 位整数种子。");
      actions.commit();
      app.config.seed = seed;
      actions.persist();
      actions.toast("已应用种子 " + seed);
    };
    const apply = (value) => {
      if (!actions.editable()) return;
      try {
        const normalized = validateConfig(value);
        app.history = [];
        app.missionIndex = null;
        app.config = clone(normalized);
        app.series = null;
        $("#bo3-enabled").checked = false;
        app.playMode = "sandbox";
        app.hotseatStage = null;
        app.selection = [];
        app.editTool = "place";
        app.team = "red";
        app.view.map(app.config.map);
        actions.redraw();
        $("#modal").close();
        actions.toast("对局已载入。");
      } catch (e) {
        actions.toast("导入失败：" + e.message);
      }
    };
    $("#apply-config").onclick = () => {
      try {
        apply(JSON.parse($("#config-editor").value));
      } catch (e) {
        actions.toast("JSON 格式有误：" + e.message);
      }
    };
    $("#import-config").onclick = () => {
      if (actions.editable()) $("#file-input").click();
    };
    $("#file-input").onchange = async (e) => {
      const file = e.target.files[0];
      if (!file) return;
      if (file.size > 1024 * 1024) return actions.toast("文件须小于 1 MB。");
      try {
        apply(JSON.parse(await file.text()));
      } catch {
        actions.toast("无法读取 JSON 文件。");
      }
    };
  }
  function terrainInfo() {
    actions.dialog(
      "地形与道具",
      `<h3>${MAPS[app.config.map].name}</h3><p>${MAPS[app.config.map].description}<br>${MAPS[app.config.map].hint}</p><p>切换左侧「道具」可在本方放置，每方最多 ${MAX_PROPS} 件，与兵种共用军费。右键点击或 Shift + 点击移除自己布置的道具。地图自带的中立道具无法在布阵时移除。</p><div class="field-notes">${Object.values(
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
    actions.dialog(
      "战场手册",
      `<p>先排兵，再布置道具。骑兵需要冲锋距离，军医提供持续治疗，投石车可以越过低矮掩体。每个士兵都有实体碰撞；箭、炸弹、冲击波与重力会共同决定结果。</p><h3>如何操作</h3><ul><li>选兵种，左键点击本方半场放置。右键点击士兵移除，或按住 Shift 点击。</li><li>拖动旋转视角，滚轮缩放，右键拖动平移。触屏双指缩放；使用「拖拽视角 / 点选布阵」开关，拖动视角时不放兵。</li><li><span class="key">空格</span> 开战 / 暂停，<span class="key">R</span> 返回布阵，<span class="key">Ctrl / Cmd Z</span> 撤销布阵；1 / 2 / 3 选择军令，Tab 循环视角。</li><li>自由沙盘可编辑双方；玩家 vs AI 编辑红方；自动演武由双方 AI 布阵。热座按红方 → 封存交接 → 蓝方 → 揭晓开战操作。</li><li>混编整队可选方阵、横排、楔形并镜像追加。框选模式拖动地面矩形或逐个点击；选中同兵种后可移动、删除、改姿态。取消选择后恢复视角旋转。</li><li>悬停部队或道具查看地面射程圈；高地加成按最近公开敌军的地面高度计算。骑兵箭头表示开场冲锋方向。</li></ul><h3>几条实用战术</h3><ul><li>盾卫面向敌军才能减伤。长枪在盾后也能攻击。</li><li>弓手会预测移动目标，但箭可能撞上友军。</li><li>炸弹 1.8 秒引爆，双方都会受到伤害与击飞。</li><li>危桥上的单位会尝试走桥；拥挤或冲击可能把它推下去。</li><li>交战上限 90 秒；超时按剩余血量折算的兵力价值决胜。</li></ul><h3>关于这个版本</h3><p>原创开源 Alpha 0.5。公开侦察 AI、Bo3 复盘调阵、统帅军令、合成音效、实时战力与收藏夹。十种兵种、六张地图、五种可布置道具。近战使用距离与冷却判定，移动、碰撞、抛射与倒地由 Rapier 求解。玩法灵感来自《纸上谈兵》和全面战争式物理沙盘。</p>`,
    );
  }
  return {
    toast,
    dialog,
    saveJson,
    exportReplay,
    loadReplay,
    workshop,
    terrainInfo,
    help,
  };
}
