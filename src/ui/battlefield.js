import {
  UNITS,
  PROPS,
  MAPS,
  STANCES,
  MAX_UNITS,
  MAX_PROPS,
  teamCost,
} from "../data.js";
import { hotseatView, moveSquad } from "../deployment.js";
import { $ } from "./dom.js";
export function bindBattlefield(app, actions) {
  const canvas = app.view.renderer.domElement;
  let down = null;
  canvas.addEventListener("pointerdown", (e) => {
    down = {
      x: e.clientX,
      y: e.clientY,
      button: e.button,
      pointerId: e.pointerId,
      pointerType: e.pointerType,
      touchMode: app.touchMode,
      point: app.view.pick(e.clientX, e.clientY),
    };
  });
  canvas.addEventListener("pointermove", (e) => {
    if (app.mode === "battle" && app.pendingOrder) {
      const p = app.view.pick(e.clientX, e.clientY);
      app.orderPoint = p ? { x: p.x, z: p.z } : null;
      app.view.preview(
        app.config,
        p ? { type: app.pendingOrder, x: p.x, z: p.z } : null,
        "order",
        $("#command-team").value,
      );
      return;
    }
    if (app.mode === "battle" && app.paused) {
      const p = app.view.pick(e.clientX, e.clientY),
        state = app.battle.snapshot(),
        u = p
          ? state.units.find(
              (u) => u.alive && Math.hypot(u.t[0] - p.x, u.t[2] - p.z) < 0.9,
            )
          : null;
      const data = app.view.preview(
        app.config,
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
    if (app.mode === "deploy") {
      const p = app.view.pick(e.clientX, e.clientY);
      if (p) {
        p.x = Math.round(p.x * 2) / 2;
        p.z = Math.round(p.z * 2) / 2;
      }
      app.view.hover(
        p,
        actions.validPlacement(p),
        app.catalog === "props"
          ? PROPS[app.selectedProp].radius
          : 0.62 * UNITS[app.selected].scale,
      );
      const visible =
        app.playMode === "hotseat"
          ? hotseatView(app.config, app.hotseatStage)
          : app.config;
      let item = null,
        kind = "unit",
        itemTeam = app.team;
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
          const prop = [...MAPS[app.config.map].props, ...visible.props].find(
            (q) =>
              Math.hypot(q.x - p.x, q.z - p.z) < PROPS[q.type].radius + 0.25,
          );
          if (prop) {
            item = prop;
            kind = "prop";
            itemTeam = prop.team || app.team;
          }
        }
        if (!item && app.editTool === "place") {
          item = {
            ...p,
            type: app.catalog === "units" ? app.selected : app.selectedProp,
            stance: $("#stance").value,
          };
          kind = app.catalog === "units" ? "unit" : "prop";
        }
      }
      const data = app.view.preview(visible, item, kind, itemTeam);
      if (app.editTool === "select" && down?.button === 0)
        app.view.selectionBox(down.point, p);
      $("#preview-label").classList.toggle("hidden", !data);
      if (data)
        $("#preview-label").textContent =
          `${kind === "unit" ? UNITS[item.type].name + " · " + STANCES[item.stance || "advance"] : PROPS[item.type].name} · ${kind === "unit" ? "攻击射程" : "作用半径"} ${data.radius.toFixed(1)} 米${data.bonus ? " · 对最近敌军高地 +18%" : ""}${data.charge ? " · 箭头为开场冲锋方向" : ""}`;
    }
  });
  canvas.addEventListener("pointerleave", () => {
    app.view.hover(null, false);
    app.view.preview(app.config, null);
    $("#preview-label").classList.add("hidden");
  });
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());
  canvas.addEventListener("pointercancel", () => {
    down = null;
  });
  canvas.addEventListener("pointerup", (e) => {
    if (
      (e.pointerType === "touch" || matchMedia("(max-width:600px)").matches) &&
      (app.touchMode !== "place" || down?.pointerId !== e.pointerId)
    ) {
      down = null;
      return;
    }
    if (
      app.mode === "battle" &&
      app.pendingOrder &&
      down?.button === 0 &&
      !app.paused &&
      Math.hypot(down.x - e.clientX, down.y - e.clientY) < 6
    ) {
      const p = app.view.pick(e.clientX, e.clientY);
      if (!p) return;
      const result = app.battle.issueOrder($("#command-team").value, {
        type: app.pendingOrder,
        x: p.x,
        z: p.z,
      });
      if (result.ok) {
        app.pendingOrder = null;
        app.audio.play("order");
        actions.toast("军令已下达。");
      } else actions.toast(result.reason);
      actions.updateCommandUI();
      return;
    }
    if (
      app.mode === "deploy" &&
      down &&
      app.editTool === "select" &&
      down.button === 0
    ) {
      const p = app.view.pick(e.clientX, e.clientY),
        a = down.point;
      if (!p || !a || !actions.editable()) return;
      if (Math.hypot(down.x - e.clientX, down.y - e.clientY) > 6)
        app.selection = app.config[app.team].flatMap((u, i) =>
          u.x >= Math.min(a.x, p.x) &&
          u.x <= Math.max(a.x, p.x) &&
          u.z >= Math.min(a.z, p.z) &&
          u.z <= Math.max(a.z, p.z)
            ? [i]
            : [],
        );
      else {
        const i = app.config[app.team].findIndex(
          (u) => Math.hypot(u.x - p.x, u.z - p.z) < 0.9,
        );
        if (i >= 0)
          app.selection = app.selection.includes(i)
            ? app.selection.filter((n) => n !== i)
            : [...app.selection, i];
      }
      actions.redraw();
      down = null;
      return;
    }
    if (
      !down ||
      Math.hypot(down.x - e.clientX, down.y - e.clientY) > 6 ||
      app.mode !== "deploy"
    )
      return;
    const p = app.view.pick(e.clientX, e.clientY);
    if (!p) return;
    p.x = Math.round(p.x * 2) / 2;
    p.z = Math.round(p.z * 2) / 2;
    if (
      app.playMode === "ai-ai" ||
      (app.playMode === "hotseat" && app.hotseatStage !== app.team)
    )
      return;
    if (app.editTool === "move" && down.button === 0) {
      try {
        const center = app.selection.reduce(
          (s, i) => ({
            x: s.x + app.config[app.team][i].x / app.selection.length,
            z: s.z + app.config[app.team][i].z / app.selection.length,
          }),
          { x: 0, z: 0 },
        );
        const next = moveSquad(
            app.config,
            app.team,
            app.selection,
            p.x - center.x,
            p.z - center.z,
          ),
          n = app.selection.length;
        actions.commit();
        app.config = next;
        app.selection = Array.from(
          { length: n },
          (_, i) => app.config[app.team].length - n + i,
        );
        app.editTool = "select";
        actions.redraw();
      } catch (error) {
        actions.toast(error.message);
      }
      return;
    }
    if (down.button === 2 || e.shiftKey) {
      let nearest = -1,
        distance = 1.1;
      app.config[app.team].forEach((u, i) => {
        const d = Math.hypot(u.x - p.x, u.z - p.z);
        if (d < distance) {
          distance = d;
          nearest = i;
        }
      });
      if (nearest >= 0) {
        actions.commit();
        app.config[app.team].splice(nearest, 1);
        actions.redraw();
      } else {
        const index = (app.config.props || []).findIndex(
          (q) =>
            q.team === app.team &&
            Math.hypot(q.x - p.x, q.z - p.z) < PROPS[q.type].radius + 0.3,
        );
        if (index >= 0) {
          actions.commit();
          app.config.props.splice(index, 1);
          actions.redraw();
        }
      }
      return;
    }
    if (down.button !== 0) return;
    if (app.catalog === "props") {
      if (
        (app.config.props || []).filter((q) => q.team === app.team).length >=
        MAX_PROPS
      )
        return actions.toast("每方最多 " + MAX_PROPS + " 件道具。");
      if (
        teamCost(app.config, app.team) + PROPS[app.selectedProp].cost >
        actions.budget()
      )
        return actions.toast("军费不足。");
      if (!actions.validPlacement(p))
        return actions.toast("请在本方空地放置，并为道具留出空间。");
      actions.commit();
      app.config.props ||= [];
      app.config.props.push({
        type: app.selectedProp,
        team: app.team,
        x: p.x,
        z: p.z,
      });
      actions.redraw();
      return;
    }
    if (app.config[app.team].length >= MAX_UNITS)
      return actions.toast("每方最多 " + MAX_UNITS + " 人。");
    if (
      teamCost(app.config, app.team) + UNITS[app.selected].cost >
      actions.budget()
    )
      return actions.toast("军费不足，试试更便宜的兵种。");
    if (!actions.validPlacement(p))
      return actions.toast("请放在本方空地上，并留出士兵之间的间距。");
    actions.commit();
    app.config[app.team].push({
      type: app.selected,
      x: p.x,
      z: p.z,
      stance: $("#stance").value,
    });
    app.audio.play("place");
    actions.redraw();
  });
}
