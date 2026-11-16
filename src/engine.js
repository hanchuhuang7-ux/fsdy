import RAPIER from "@dimforge/rapier3d-compat";
import {
  UNITS,
  PROPS,
  MAPS,
  DT,
  MAX_TIME,
  heightAt,
  terrainAt,
  validateConfig,
  ORDERS,
  ORDER_POINTS,
  ORDER_COOLDOWN,
  unitPower,
  validateOrderLog,
} from "./data.js";
let ready;
export function initPhysics() {
  return (ready ||= RAPIER.init());
}
const vec = (x = 0, y = 0, z = 0) => ({ x, y, z });
const distance = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const groups = (member, filter) => ((member << 16) | filter) >>> 0;
const GROUP = {
  ground: groups(1, 65535),
  unit: groups(2, 1 | 2 | 4 | 16),
  shot: groups(4, 1 | 2 | 16),
  corpse: groups(8, 1 | 16),
  prop: groups(16, 1 | 2 | 4 | 8 | 16),
};
const transform = (body, out = []) => {
  const p = body.translation(),
    q = body.rotation();
  out[0] = p.x;
  out[1] = p.y;
  out[2] = p.z;
  out[3] = q.x;
  out[4] = q.y;
  out[5] = q.z;
  out[6] = q.w;
  return out;
};
function mapInto(source, out = [], copy) {
  out.length = source.length;
  for (let i = 0; i < source.length; i++) out[i] = copy(source[i], out[i]);
  return out;
}
export const MAX_RAGDOLLS = 24;
const contributions = () => ({
  damageDealt: 0,
  damageTaken: 0,
  healing: 0,
  kills: 0,
  falls: 0,
  fallKills: 0,
  friendlyFire: 0,
  friendlyKills: 0,
});
const rounded = (n) => Math.round(n * 1000) / 1000;

/** Fixed-step simulation shared by the browser and CLI. No wall-clock inputs. */
export class Battle {
  constructor(input) {
    const config = validateConfig(input);
    this.config = structuredClone(config);
    this.rng = config.seed >>> 0;
    this.world = new RAPIER.World(vec(0, -18, 0));
    this.world.timestep = DT;
    this.queue = new RAPIER.EventQueue(true);
    this.units = [];
    this.projectiles = [];
    this.ragdolls = [];
    this.effects = [];
    this.events = [];
    this.props = [];
    this.colliderUnits = new Map();
    this.colliderProjectiles = new Map();
    this.colliderProps = new Map();
    this.grid = new Map();
    this.armies = { red: [], blue: [] };
    this.tick = 0;
    this.nextId = 1;
    this.result = null;
    this.orderState = {
      points: { red: ORDER_POINTS, blue: ORDER_POINTS },
      readyAt: { red: 0, blue: 0 },
      active: [],
      log: [],
    };
    this.cues = [];
    this.cueTimes = new Map();
    this.nextCueId = 1;
    this.stats = { targetChecks: 0, neighborChecks: 0 };
    for (const p of MAPS[config.map].platforms) {
      const a = p.angle || 0;
      this.world.createCollider(
        RAPIER.ColliderDesc.cuboid(p.hx, p.hy, p.hz)
          .setTranslation(p.x, p.y, p.z)
          .setRotation({ x: 0, y: 0, z: Math.sin(a / 2), w: Math.cos(a / 2) })
          .setFriction(0.8)
          .setCollisionGroups(GROUP.ground),
      );
    }
    for (const p of MAPS[config.map].rocks)
      this.world.createCollider(
        RAPIER.ColliderDesc.ball(p.r)
          .setTranslation(p.x, p.r * 0.2, p.z)
          .setCollisionGroups(GROUP.ground),
      );
    for (const p of MAPS[config.map].walls)
      this.world.createCollider(
        RAPIER.ColliderDesc.cuboid(p.hx, p.hy, p.hz)
          .setTranslation(p.x, p.hy, p.z)
          .setCollisionGroups(GROUP.ground),
      );
    for (const team of ["red", "blue"])
      for (const u of config[team]) this.spawn(team, u);
    for (const p of [...MAPS[config.map].props, ...config.props])
      this.spawnProp(p);
    this.cachePositions();
  }
  random() {
    this.rng = (Math.imul(this.rng, 1664525) + 1013904223) >>> 0;
    return this.rng / 4294967296;
  }
  cue(type, p, strength = 1) {
    if (this.tick - (this.cueTimes.get(type) ?? -100) < 8) return;
    this.cueTimes.set(type, this.tick);
    if (this.cues.length >= 24) this.cues.shift();
    this.cues.push({
      id: this.nextCueId++,
      tick: this.tick,
      type,
      x: p.x,
      y: p.y || 0,
      z: p.z,
      strength,
    });
  }
  issueOrder(team, order) {
    const reject = (reason) => ({ ok: false, reason });
    if (!this.config.commander.enabled) return reject("军令模式未开启");
    if (this.result) return reject("本局已经结束");
    if (
      !["red", "blue"].includes(team) ||
      !order ||
      !Object.hasOwn(ORDERS, order.type)
    )
      return reject("军令或阵营无效");
    if (
      !Number.isFinite(order.x) ||
      !Number.isFinite(order.z) ||
      Math.abs(order.x) > 18 ||
      Math.abs(order.z) > 12 ||
      !Number.isFinite(heightAt(this.config.map, order.x, order.z))
    )
      return reject("请选择战场地面");
    const d = ORDERS[order.type],
      state = this.orderState;
    if (state.points[team] < d.cost) return reject("军令点数不足");
    if (this.tick < state.readyAt[team]) return reject("军令正在冷却");
    // UI orders and tick-scheduled replay orders must sample the same current transforms.
    this.cachePositions();
    const entry = {
      id: state.log.length + 1,
      team,
      type: order.type,
      x: order.x,
      z: order.z,
      tick: this.tick,
    };
    state.points[team] -= d.cost;
    state.readyAt[team] = this.tick + ORDER_COOLDOWN;
    state.log.push(entry);
    const active = { ...entry, endTick: this.tick + d.duration };
    state.active.push(active);
    this.nearby(order, d.radius, (u) => {
      if (u.team !== team) return;
      if (order.type === "inspire") u.inspireUntil = active.endTick;
      else if (order.type === "fortify") {
        if (["shield", "spear", "giant", "militia"].includes(u.type))
          u.fortifyUntil = active.endTick;
      } else u.focus = { x: order.x, z: order.z, endTick: active.endTick };
      // Order feedback is bounded by the existing 41-effect budget.
      if (u.id % 3 === 0) this.effect("order", u.position, 0.7);
    });
    this.cue("order", {
      x: order.x,
      y: heightAt(this.config.map, order.x, order.z) + 0.2,
      z: order.z,
    });
    return { ok: true, entry: { ...entry } };
  }
  useOrderLog(log) {
    if (this.tick !== 0 || this.orderState.log.length)
      throw Error("军令重演须从初始状态开始");
    this.replayOrders = validateOrderLog(log, this.config);
    this.replayOrderIndex = 0;
  }
  tickOrders() {
    this.orderState.active = this.orderState.active.filter(
      (o) => o.endTick > this.tick,
    );
    if (this.replayOrders) {
      while (
        this.replayOrderIndex < this.replayOrders.length &&
        this.replayOrders[this.replayOrderIndex].tick === this.tick
      ) {
        const o = this.replayOrders[this.replayOrderIndex++],
          r = this.issueOrder(o.team, o);
        if (!r.ok) throw Error("军令日志无法重演：" + r.reason);
      }
      return;
    }
    if (this.tick % 60 !== 0) return;
    for (const team of this.config.commander.aiTeams) {
      if (
        this.orderState.points[team] <= 0 ||
        this.tick < this.orderState.readyAt[team]
      )
        continue;
      const own = this.armies[team].filter((u) => u.alive),
        front = own.reduce(
          (best, u) =>
            !best ||
            (team === "red"
              ? u.position.x > best.position.x
              : u.position.x < best.position.x)
              ? u
              : best,
          null,
        );
      if (!front) continue;
      const enemies = this.armies[team === "red" ? "blue" : "red"].filter(
        (u) => u.alive && distance(front.position, u.position) < 8,
      );
      const type =
        front.hp / front.maxHp < 0.65
          ? "fortify"
          : enemies.length >= 3
            ? "focus"
            : "inspire";
      const p =
        type === "focus"
          ? enemies.reduce(
              (s, u) => ({
                x: s.x + u.position.x / enemies.length,
                z: s.z + u.position.z / enemies.length,
              }),
              { x: 0, z: 0 },
            )
          : front.position;
      this.issueOrder(team, { type, x: p.x, z: p.z });
    }
  }
  spawn(team, u) {
    const d = UNITS[u.type],
      s = d.scale,
      floor = heightAt(this.config.map, u.x, u.z);
    const body = this.world.createRigidBody(
      RAPIER.RigidBodyDesc.dynamic()
        .setTranslation(u.x, floor + 0.8 * s, u.z)
        .lockRotations()
        .setLinearDamping(0.6),
    );
    const collider = this.world.createCollider(
      RAPIER.ColliderDesc.capsule(0.35 * s, 0.27 * s)
        .setMass(d.mass)
        .setFriction(0.2)
        .setRestitution(0.05)
        .setCollisionGroups(GROUP.unit),
      body,
    );
    const unit = {
      ...u,
      team,
      id: this.nextId++,
      hp: d.hp,
      maxHp: d.hp,
      body,
      collider,
      alive: true,
      angle: team === "red" ? Math.PI / 2 : -Math.PI / 2,
      cooldown: this.random() * 0.6,
      attack: 0,
      stun: 0,
      kills: 0,
      contribution: contributions(),
      anchor: { x: u.x, z: u.z },
      progress: { x: u.x, z: u.z, blocked: 0, escapeUntil: 0, escapes: 0 },
      charge: 0,
      target: null,
      inspireUntil: 0,
      fortifyUntil: 0,
      focus: null,
      position: vec(u.x, floor + 0.8 * s, u.z),
      floor,
    };
    this.units.push(unit);
    this.armies[team].push(unit);
    this.colliderUnits.set(collider.handle, unit);
    return unit;
  }
  spawnProp(input) {
    const d = PROPS[input.type],
      floor = heightAt(this.config.map, input.x, input.z);
    const p = {
      ...input,
      team: input.team || "neutral",
      id: this.nextId++,
      hp: d.hp,
      alive: true,
      floor,
      cooldown: 0,
      fuse: -1,
      kills: 0,
      contribution: contributions(),
    };
    let desc;
    if (p.type === "barrel")
      desc = RAPIER.ColliderDesc.cylinder(0.55, 0.42).setTranslation(
        p.x,
        floor + 0.55,
        p.z,
      );
    else if (p.type === "barricade")
      desc = RAPIER.ColliderDesc.cuboid(0.38, 0.7, 1.2).setTranslation(
        p.x,
        floor + 0.7,
        p.z,
      );
    else if (p.type === "banner")
      desc = RAPIER.ColliderDesc.cylinder(0.9, 0.13).setTranslation(
        p.x,
        floor + 0.9,
        p.z,
      );
    else
      desc = RAPIER.ColliderDesc.cuboid(0.85, 0.08, 0.85)
        .setTranslation(p.x, floor + 0.08, p.z)
        .setSensor(true);
    p.collider = this.world.createCollider(desc.setCollisionGroups(GROUP.prop));
    this.colliderProps.set(p.collider.handle, p);
    this.props.push(p);
    return p;
  }
  cachePositions() {
    for (const bucket of this.grid.values()) bucket.length = 0;
    for (const u of this.units)
      if (u.alive) {
        u.position = u.body.translation();
        const x = Math.floor(u.position.x / 3),
          z = Math.floor(u.position.z / 3),
          key = (x + 64) * 128 + z + 64;
        let bucket = this.grid.get(key);
        if (!bucket) {
          bucket = [];
          this.grid.set(key, bucket);
        }
        bucket.push(u);
        u.floor = heightAt(this.config.map, u.position.x, u.position.z);
      }
  }
  nearby(p, r, visit) {
    for (let x = Math.floor((p.x - r) / 3); x <= Math.floor((p.x + r) / 3); x++)
      for (
        let z = Math.floor((p.z - r) / 3);
        z <= Math.floor((p.z + r) / 3);
        z++
      ) {
        const bucket = this.grid.get((x + 64) * 128 + z + 64);
        if (bucket)
          for (const u of bucket)
            if (u.alive) {
              this.stats.neighborChecks++;
              if (
                (p.x - u.position.x) ** 2 + (p.z - u.position.z) ** 2 <=
                r * r
              )
                visit(u);
            }
      }
  }
  target(unit) {
    const p = unit.position;
    let best = null,
      score = Infinity;
    for (const e of this.armies[unit.team === "red" ? "blue" : "red"])
      if (e.alive) {
        this.stats.targetChecks++;
        let s = (p.x - e.position.x) ** 2 + (p.z - e.position.z) ** 2;
        if (
          unit.focus &&
          unit.focus.endTick > this.tick &&
          Math.hypot(e.position.x - unit.focus.x, e.position.z - unit.focus.z) <
            4
        )
          s *= 0.08;
        if (unit.stance === "flank" && UNITS[e.type].ranged) s *= 0.42;
        if (unit.stance === "hunt")
          s *= UNITS[e.type].ranged
            ? 0.12 * (160 / UNITS[e.type].cost)
            : e.type === "medic"
              ? 0.2
              : 1.5;
        if (s < score) {
          best = e;
          score = s;
        }
      }
    return best;
  }
  rangeFor(u, target) {
    return (
      UNITS[u.type].range *
      (UNITS[u.type].ranged && u.floor > target.floor + 1 ? 1.18 : 1)
    );
  }
  heal(target, amount, source = null) {
    if (target.alive) {
      const restored = Math.min(target.maxHp - target.hp, amount);
      target.hp += restored;
      if (source) source.contribution.healing += restored;
      if (restored > 0 && (this.tick + target.id) % 12 === 0)
        this.cue("heal", target.position);
    }
  }
  medic(u) {
    let ally = null,
      ratio = 1;
    this.nearby(u.position, 6, (e) => {
      if (e.team === u.team && e.hp / e.maxHp < ratio) {
        ratio = e.hp / e.maxHp;
        ally = e;
      }
    });
    if (ally) {
      this.heal(ally, UNITS.medic.heal * 0.1, u);
      if ((this.tick + u.id) % 30 === 0)
        this.effect("heal", ally.position, 0.5);
    }
  }
  command(u) {
    const d = UNITS[u.type],
      p = u.position;
    if (p.y < -4 || Math.abs(p.x) > 23 || Math.abs(p.z) > 19) {
      this.kill(u, vec(), "fall");
      return;
    }
    const inspired = u.inspireUntil > this.tick;
    u.cooldown -= DT * (inspired ? 1.15 : 1);
    u.attack = Math.max(0, u.attack - DT * 3);
    u.stun = Math.max(0, u.stun - DT);
    const ai = (this.tick + u.id) % 6 === 0;
    if (ai || !u.target?.alive) u.target = this.target(u);
    if (u.type === "medic" && ai) this.medic(u);
    const target = u.target;
    if (!target) return;
    const tp = target.position,
      dist = distance(p, tp),
      dx = tp.x - p.x,
      dz = tp.z - p.z;
    u.angle = Math.atan2(dx, dz);
    const min = UNITS[target.type].scale * 0.25,
      range = this.rangeFor(u, target);
    let mx = dx / (dist || 1),
      mz = dz / (dist || 1),
      speed = d.speed;
    let firing = d.ranged && dist < range;
    // Low crossbow trajectories need an opening through the tall ruin walls.
    if (
      u.type === "crossbow" &&
      p.x * tp.x < 0 &&
      this.config.map === "ruins"
    ) {
      const z = p.z + (tp.z - p.z) * (-p.x / (tp.x - p.x));
      if (Math.abs(z) > 2.1 && Math.abs(z) < 9.1) firing = false;
    }
    if ((d.ranged || u.type === "medic") && dist < 4 && u.stance !== "hold") {
      mx *= -1;
      mz *= -1;
      speed *= 0.7;
    } else if (
      firing ||
      (!d.ranged && dist < range + min - 0.15) ||
      u.stance === "hold"
    )
      speed = 0;
    if (u.type === "medic" && dist > 5 && dist < 9) speed = 0;
    if (
      u.stance === "hold" &&
      !firing &&
      dist > range + min &&
      distance(p, u.anchor) > 1.4
    ) {
      const home = distance(p, u.anchor) || 1;
      mx = (u.anchor.x - p.x) / home;
      mz = (u.anchor.z - p.z) / home;
      speed = d.speed * 0.7;
    }
    const passage =
      MAPS[this.config.map].passage ||
      (u.stance === "flank" && this.config.map === "marsh"
        ? { approach: 5.3, lanes: [-9.6, 9.6] }
        : null);
    if (
      passage &&
      u.stance !== "hold" &&
      !firing &&
      dist > range + min - 0.15
    ) {
      if (!u.route && p.x * tp.x < 0) {
        const lanes = passage.lanes,
          lane =
            u.stance === "flank" && lanes.length > 1
              ? u.z >= 0
                ? Math.max(...lanes)
                : Math.min(...lanes)
              : lanes[0];
        u.route = {
          sign: tp.x >= 0 ? 1 : -1,
          lane,
          stage: Math.abs(p.z - lane) > 1 ? "entry" : "cross",
        };
      }
      if (u.route) {
        const route = u.route,
          entry = { x: -route.sign * passage.approach, z: route.lane };
        if (route.stage === "entry" && distance(p, entry) < 0.85)
          route.stage = "cross";
        if (
          route.stage === "cross" &&
          route.sign * p.x > passage.approach + 0.5
        )
          u.route = null;
        else {
          const x =
              route.stage === "entry"
                ? entry.x
                : route.sign * (passage.approach + 0.8),
            wx = x - p.x,
            wz = route.lane - p.z,
            l = Math.hypot(wx, wz) || 1;
          mx = wx / l;
          mz = wz / l;
          speed = d.speed;
        }
      }
    } else if (u.stance === "flank" && !d.ranged && dist > 6 && !passage) {
      mz = (tp.z + Math.sign(u.z || 1) * 5 - p.z) / (dist + 3);
      const n = Math.hypot(mx, mz) || 1;
      mx /= n;
      mz /= n;
    }
    const terrain = terrainAt(this.config.map, p.x, p.z);
    if (inspired) speed *= 1.12;
    if (speed > 0 && u.stance !== "hold") {
      if ((this.tick + u.id) % 30 === 0) this.checkProgress(u, mx, mz);
      if (u.progress.escapeUntil > this.tick) {
        const escape = this.escapeDirection(u, mx, mz);
        if (escape) {
          mx = escape.x;
          mz = escape.z;
        }
      }
    }
    if (terrain === "mud") speed *= 0.45;
    if (speed > 0)
      this.nearby(p, 3, (e) => {
        if (e !== u && e.team === u.team) {
          const dd = distance(p, e.position),
            radius = 0.65 * (d.scale + UNITS[e.type].scale);
          if (dd < radius && dd > 0.01) {
            mx += ((p.x - e.position.x) / dd) * (radius - dd) * 0.7;
            mz += ((p.z - e.position.z) / dd) * (radius - dd) * 0.7;
          }
        }
      });
    const velocity = u.body.linvel(),
      norm = Math.max(1, Math.hypot(mx, mz)),
      response = u.stun > 0 ? 0.015 : terrain === "ice" ? 0.035 : 0.16;
    const grounded = Number.isFinite(u.floor) && p.y < u.floor + d.scale * 1.12;
    if (grounded)
      u.body.applyImpulse(
        vec(
          ((mx / norm) * speed - velocity.x) * d.mass * response,
          0,
          ((mz / norm) * speed - velocity.z) * d.mass * response,
        ),
        true,
      );
    if (u.type === "cavalry")
      u.charge = Math.min(
        1.5,
        Math.max(
          0,
          u.charge +
            (Math.hypot(velocity.x, velocity.z) > 3.8 && terrain !== "mud"
              ? DT
              : -DT * 0.5),
        ),
      );
    if (u.cooldown <= 0 && grounded) {
      if (!d.ranged) {
        const obstruction = this.props.find(
          (q) =>
            q.alive &&
            ((q.type === "barricade" && q.team !== u.team) ||
              (q.type === "banner" &&
                q.team !== "neutral" &&
                q.team !== u.team)) &&
            Math.abs(p.y - q.floor) < 2.8 &&
            distance(p, q) < range + PROPS[q.type].radius,
        );
        if (obstruction) {
          this.damageProp(obstruction, d.damage, u);
          u.cooldown = d.cooldown;
          u.attack = 1;
          return;
        }
      }
      if (dist < range + min && Math.abs(p.y - tp.y) < (d.ranged ? 20 : 2.2)) {
        u.cooldown = d.cooldown;
        u.attack = 1;
        if (d.ranged) this.shoot(u, target);
        else if (u.type === "giant") {
          this.nearby(p, range + min, (e) => {
            if (e.team !== u.team) this.hit(e, d.damage, u, d.knock);
          });
          this.effect("slam", p, 0.6);
        } else {
          const charge = u.type === "cavalry" && u.charge > 0.55,
            brace =
              u.type === "spear" &&
              target.type === "cavalry" &&
              Math.hypot(target.body.linvel().x, target.body.linvel().z) > 3;
          this.hit(
            target,
            d.damage * (charge ? 2.5 : brace ? 1.8 : 1),
            u,
            d.knock * (charge ? 3.5 : brace ? 1.4 : 1),
          );
          if (charge) {
            u.charge = 0;
            this.effect("charge", p, 0.45);
          }
        }
      }
    }
  }
  checkProgress(u, mx, mz) {
    const p = u.position,
      q = u.progress;
    if (Math.hypot(p.x - q.x, p.z - q.z) < 0.18 && u.stun <= 0) q.blocked++;
    else q.blocked = 0;
    q.x = p.x;
    q.z = p.z;
    if (q.blocked >= 3) {
      q.escapeUntil = this.tick + 90;
      q.blocked = 0;
      q.escapes++;
      // Stable per-unit parity; no random draws or wall clock affect escape decisions.
      q.side = (u.id % 2 ? 1 : -1) * (q.escapes % 2 ? 1 : -1);
      const obstacles = [
        ...MAPS[this.config.map].walls,
        ...MAPS[this.config.map].rocks.map((r) => ({ ...r, hx: r.r, hz: r.r })),
        ...this.props
          .filter((p) => p.alive && p.type === "barricade")
          .map((p) => ({ ...p, hx: 0.38, hz: 1.2 })),
      ];
      const obstacle = obstacles.find(
        (o) =>
          Math.abs(p.x - o.x) < o.hx + 1.3 && Math.abs(p.z - o.z) < o.hz + 1.3,
      );
      if (obstacle) {
        const margin = 0.6 * UNITS[u.type].scale + 0.35;
        const lanes = [
          obstacle.z + obstacle.hz + margin,
          obstacle.z - obstacle.hz - margin,
        ].sort(
          (a, b) => Math.abs(a - p.z) - Math.abs(b - p.z) || q.side * (a - b),
        );
        const lane = lanes.find(
          (z) =>
            Number.isFinite(heightAt(this.config.map, p.x, z)) &&
            Number.isFinite(heightAt(this.config.map, obstacle.x, z)),
        );
        if (lane !== undefined) {
          const sign = mx >= 0 ? 1 : -1;
          q.detour = {
            entryX: obstacle.x - sign * (obstacle.hx + margin),
            exitX: obstacle.x + sign * (obstacle.hx + margin),
            z: lane,
            stage: 0,
          };
          q.escapeUntil = this.tick + 300;
        }
      }
    }
  }
  escapeDirection(u, mx, mz) {
    const p = u.position,
      radius = 0.3 * UNITS[u.type].scale;
    const detour = u.progress.detour;
    if (detour) {
      let x = detour.stage === 0 ? detour.entryX : detour.exitX,
        z = detour.z;
      if (Math.hypot(x - p.x, z - p.z) < 0.45) {
        if (detour.stage === 0) {
          detour.stage = 1;
          x = detour.exitX;
        } else {
          u.progress.detour = null;
          u.progress.escapeUntil = 0;
          return null;
        }
      }
      const n = Math.hypot(x - p.x, z - p.z) || 1;
      return { x: (x - p.x) / n, z: (z - p.z) / n };
    }
    const clear = (x, z) =>
      Number.isFinite(heightAt(this.config.map, x, z)) &&
      !MAPS[this.config.map].walls.some(
        (w) =>
          Math.abs(x - w.x) < w.hx + radius &&
          Math.abs(z - w.z) < w.hz + radius,
      ) &&
      !MAPS[this.config.map].rocks.some(
        (r) => Math.hypot(x - r.x, z - r.z) < r.r + radius,
      ) &&
      !this.props.some(
        (q) =>
          q.alive &&
          q.type === "barricade" &&
          Math.abs(x - q.x) < 0.4 + radius &&
          Math.abs(z - q.z) < 1.2 + radius,
      );
    const side = u.progress.side || 1;
    for (const angle of [
      (side * Math.PI) / 3,
      (side * Math.PI) / 2,
      (-side * Math.PI) / 3,
      (-side * Math.PI) / 2,
      Math.PI,
    ]) {
      const x = mx * Math.cos(angle) - mz * Math.sin(angle),
        z = mx * Math.sin(angle) + mz * Math.cos(angle);
      if (
        clear(p.x + x * 0.85, p.z + z * 0.85) &&
        clear(p.x + x * 0.45, p.z + z * 0.45)
      )
        return { x, z };
    }
    return null;
  }
  hit(target, damage, attacker, knock, origin = null, pierce = 0) {
    if (!target.alive) return;
    const p = target.body.translation(),
      from = origin || attacker?.position || p,
      dx = p.x - from.x,
      dz = p.z - from.z,
      dist = Math.hypot(dx, dz) || 1;
    let blocked = false;
    if (
      target.type === "shield" &&
      (Math.sin(target.angle) * -dx + Math.cos(target.angle) * -dz) / dist >
        0.25
    ) {
      damage *= 0.4 + 0.6 * pierce;
      blocked = true;
    }
    if (
      target.fortifyUntil > this.tick &&
      ["shield", "spear", "giant", "militia"].includes(target.type)
    ) {
      damage *= 0.75;
      knock *= 0.5;
    }
    this.cue(blocked ? "block" : "melee", p);
    if ((this.tick + target.id) % 6 === 0)
      this.effect(blocked ? "block" : "hit", p, 0.18);
    const actual = Math.min(target.hp, Math.max(0, damage));
    target.contribution.damageTaken += actual;
    if (attacker) {
      attacker.contribution.damageDealt += actual;
      if (attacker.team === target.team)
        attacker.contribution.friendlyFire += actual;
      target.lastHitSource = attacker;
      target.lastHitTick = this.tick;
    }
    target.hp -= damage;
    target.stun = 0.25;
    const impulse = vec((dx / dist) * knock, knock * 0.32, (dz / dist) * knock);
    target.body.applyImpulse(impulse, true);
    if (target.hp <= 0) {
      this.kill(target, impulse, "combat", attacker);
    }
  }
  shoot(u, target) {
    this.cue(
      u.type === "archer"
        ? "bow"
        : u.type === "crossbow"
          ? "crossbow"
          : u.type === "catapult"
            ? "catapult"
            : "bomb",
      u.position,
    );
    const d = UNITS[u.type],
      p = u.body.translation(),
      t = target.body.translation(),
      dx = t.x - p.x,
      dz = t.z - p.z,
      dist = Math.hypot(dx, dz) || 1;
    const type =
      u.type === "bomber"
        ? "bomb"
        : u.type === "catapult"
          ? "stone"
          : u.type === "crossbow"
            ? "bolt"
            : "arrow";
    const bomb = type === "bomb",
      stone = type === "stone",
      bolt = type === "bolt";
    const flight = Math.max(
        0.22,
        dist / (bomb ? 10 : stone ? 12 : bolt ? 28 : 19),
      ),
      gravity = bolt ? 0.2 : 1;
    const spawn = vec(
      p.x + (dx / dist) * (stone ? 0.9 : 0.7),
      p.y + 0.5 * d.scale,
      p.z + (dz / dist) * (stone ? 0.9 : 0.7),
    );
    const spread =
        (this.random() - 0.5) * (bomb ? 0.9 : stone ? 1.1 : bolt ? 0.2 : 0.55),
      tv = target.body.linvel();
    const velocity = vec(
      (t.x + tv.x * flight * 0.6 - spawn.x + spread) / flight,
      (t.y + 0.15 - spawn.y + 9 * gravity * flight * flight) / flight,
      (t.z + tv.z * flight * 0.6 - spawn.z + spread) / flight,
    );
    const body = this.world.createRigidBody(
      RAPIER.RigidBodyDesc.dynamic()
        .setTranslation(spawn.x, spawn.y, spawn.z)
        .setLinvel(velocity.x, velocity.y, velocity.z)
        .setGravityScale(gravity)
        .setCcdEnabled(true),
    );
    const collider = this.world.createCollider(
      RAPIER.ColliderDesc.ball(bomb ? 0.18 : stone ? 0.28 : 0.09)
        .setMass(bomb ? 1.5 : stone ? 4 : 0.15)
        .setRestitution(bomb ? 0.35 : 0)
        .setCollisionGroups(GROUP.shot)
        .setActiveEvents(RAPIER.ActiveEvents.COLLISION_EVENTS),
      body,
    );
    const shot = {
      id: this.nextId++,
      type,
      body,
      collider,
      owner: u,
      life: bomb ? 1.8 : 6,
      damage: d.damage,
      knock: d.knock,
      pierce: bolt ? 0.6 : 0,
      removed: false,
    };
    this.projectiles.push(shot);
    this.colliderProjectiles.set(collider.handle, shot);
  }
  blast(origin, radius, damage, knock, owner = null) {
    const p = { ...origin };
    this.effect("explosion", p, 0.65);
    // Explosions use current rigid-body transforms, including airborne targets.
    for (const e of this.units)
      if (e.alive) {
        const q = e.body.translation(),
          dist = Math.hypot(q.x - p.x, q.y - p.y, q.z - p.z);
        if (dist < radius)
          this.hit(
            e,
            damage * (1 - dist / (radius * 1.3)),
            owner,
            knock * (1 - dist / (radius * 1.3)),
            p,
          );
      }
    for (const q of this.props)
      if (
        q.alive &&
        Math.hypot(q.x - p.x, q.floor + 0.5 - p.y, q.z - p.z) < radius
      )
        this.damageProp(q, damage * 0.9, owner);
    for (const d of this.ragdolls)
      for (const part of d.parts) {
        const q = part.body.translation(),
          dx = q.x - p.x,
          dz = q.z - p.z;
        if (Math.hypot(dx, dz) < radius)
          part.body.applyImpulse(vec(dx, 4, dz), true);
      }
  }
  explode(projectile) {
    if (projectile.removed) return;
    const p = projectile.body.translation(),
      stone = projectile.type === "stone";
    this.removeProjectile(projectile);
    this.blast(
      p,
      stone ? 2.1 : 3.6,
      projectile.damage,
      projectile.knock,
      projectile.owner,
    );
  }
  removeProjectile(p) {
    if (p.removed) return;
    p.removed = true;
    this.colliderProjectiles.delete(p.collider.handle);
    this.world.removeRigidBody(p.body);
  }
  damageProp(p, damage, source = null) {
    if (!p.alive) return;
    const actual = Math.min(p.hp, Math.max(0, damage));
    p.contribution.damageTaken += actual;
    if (source) {
      source.contribution.damageDealt += actual;
      if (source.team === p.team) source.contribution.friendlyFire += actual;
    }
    p.hp -= damage;
    if (p.hp > 0) return;
    p.alive = false;
    this.colliderProps.delete(p.collider.handle);
    this.world.removeCollider(p.collider, true);
    this.events.push({ tick: this.tick, type: "prop-destroyed", prop: p.type });
    if (p.type === "barrel")
      this.blast(vec(p.x, p.floor + 0.45, p.z), 4.2, 95, 28, p);
    else this.effect("slam", vec(p.x, p.floor + 0.4, p.z), 0.6);
  }
  tickProps() {
    for (const p of this.props)
      if (p.alive) {
        p.cooldown = Math.max(0, p.cooldown - 0.1);
        if (p.type === "barrel") {
          if (p.fuse < 0)
            this.nearby(p, 1.5, (e) => {
              if (
                (p.team === "neutral" || e.team !== p.team) &&
                Math.abs(e.position.y - p.floor) < 2.3
              )
                p.fuse = 0.7;
            });
          if (p.fuse >= 0) {
            p.fuse -= 0.1;
            if (p.fuse <= 0) this.damageProp(p, 9999);
          }
        } else if (p.type === "banner")
          this.nearby(p, 3.2, (e) => {
            if (
              (p.team === "neutral" || e.team === p.team) &&
              Math.abs(e.position.y - p.floor) < 3
            )
              this.heal(e, 1, p);
          });
        else if (p.type === "spikes" && p.cooldown === 0) {
          let triggered = false;
          this.nearby(p, 1.15, (e) => {
            if (
              (p.team === "neutral" || e.team !== p.team) &&
              e.position.y < p.floor + 1.7
            ) {
              this.hit(e, 14, p, 0.2, vec(p.x, p.floor, p.z));
              triggered = true;
            }
          });
          if (triggered) {
            p.cooldown = 0.6;
            this.effect("spikes", vec(p.x, p.floor + 0.1, p.z), 0.4);
          }
        } else if (p.type === "spring" && p.cooldown === 0) {
          let triggered = false;
          this.nearby(p, 0.95, (e) => {
            if (triggered || e.position.y > p.floor + 1.5) return;
            const sign =
                (p.team === "neutral" ? e.team : p.team) === "red" ? 1 : -1,
              mass = UNITS[e.type].mass;
            e.body.applyImpulse(vec(sign * mass * 7, mass * 8, 0), true);
            e.stun = 0.7;
            p.cooldown = 1.5;
            triggered = true;
            this.effect("spring", vec(p.x, p.floor, p.z), 0.5);
          });
        }
      }
  }
  retireDoll(d) {
    for (const p of d.parts) this.world.removeRigidBody(p.body);
    d.removed = true;
  }
  kill(u, impulse, reason = "combat", source = null) {
    if (!u.alive) return;
    if (reason === "fall") {
      u.contribution.falls++;
      if (this.tick - u.lastHitTick <= 180) source = u.lastHitSource;
    }
    if (source) {
      if (source.team === u.team) source.contribution.friendlyKills++;
      else {
        source.kills++;
        source.contribution.kills++;
        if (reason === "fall") source.contribution.fallKills++;
      }
    }
    u.alive = false;
    u.hp = 0;
    const p = u.body.translation(),
      v = u.body.linvel(),
      s = UNITS[u.type].scale;
    this.colliderUnits.delete(u.collider.handle);
    this.world.removeRigidBody(u.body);
    if (this.ragdolls.length >= MAX_RAGDOLLS) {
      this.retireDoll(this.ragdolls.shift());
    }
    const specs = [
      { name: "torso", o: vec(), h: vec(0.23, 0.32, 0.16) },
      { name: "head", o: vec(0, 0.53, 0), h: vec(0.2, 0.2, 0.2) },
      { name: "armL", o: vec(-0.4, -0.02, 0), h: vec(0.11, 0.3, 0.11) },
      { name: "armR", o: vec(0.4, -0.02, 0), h: vec(0.11, 0.3, 0.11) },
      { name: "legL", o: vec(-0.16, -0.57, 0), h: vec(0.12, 0.3, 0.13) },
      { name: "legR", o: vec(0.16, -0.57, 0), h: vec(0.12, 0.3, 0.13) },
    ];
    const parts = specs.map((q) => {
      const body = this.world.createRigidBody(
        RAPIER.RigidBodyDesc.dynamic()
          .setTranslation(p.x + q.o.x * s, p.y + q.o.y * s, p.z + q.o.z * s)
          .setLinvel(v.x, v.y, v.z)
          .setAngularDamping(0.8)
          .setLinearDamping(0.25),
      );
      this.world.createCollider(
        RAPIER.ColliderDesc.cuboid(q.h.x * s, q.h.y * s, q.h.z * s)
          .setMass(q.name === "torso" ? 2 : 0.5)
          .setFriction(0.8)
          .setCollisionGroups(GROUP.corpse),
        body,
      );
      body.applyImpulse(
        vec(impulse.x * 0.07, (1 + this.random()) * 0.5, impulse.z * 0.07),
        true,
      );
      return { name: q.name, body };
    });
    const anchors = [
      null,
      [vec(0, 0.32 * s, 0), vec(0, -0.2 * s, 0)],
      [vec(-0.25 * s, 0.2 * s, 0), vec(0.12 * s, 0.2 * s, 0)],
      [vec(0.25 * s, 0.2 * s, 0), vec(-0.12 * s, 0.2 * s, 0)],
      [vec(-0.16 * s, -0.3 * s, 0), vec(0, 0.27 * s, 0)],
      [vec(0.16 * s, -0.3 * s, 0), vec(0, 0.27 * s, 0)],
    ];
    for (let i = 1; i < parts.length; i++) {
      const joint = this.world.createImpulseJoint(
        RAPIER.JointData.spherical(...anchors[i]),
        parts[0].body,
        parts[i].body,
        true,
      );
      joint.setContactsEnabled(false);
    }
    this.ragdolls.push({
      id: u.id,
      team: u.team,
      type: u.type,
      parts,
      age: 0,
      wreck: ["cavalry", "catapult"].includes(u.type)
        ? [p.x, p.y - 0.5, p.z, u.angle]
        : null,
    });
    this.events.push({
      tick: this.tick,
      type: "death",
      team: u.team,
      unit: u.type,
      reason,
      source: source?.id || null,
    });
  }
  effect(type, p, life) {
    if (["explosion", "slam", "charge"].includes(type)) this.cue(type, p);
    if (this.effects.length > 40) this.effects.shift();
    this.effects.push({
      id: this.nextId++,
      type,
      x: p.x,
      y: p.y,
      z: p.z,
      life,
      maxLife: life,
    });
  }
  step() {
    if (this.result) return;
    this.cachePositions();
    if (this.config.commander.enabled) this.tickOrders();
    if (this.tick % 6 === 0) this.tickProps();
    for (const u of this.units) if (u.alive) this.command(u);
    this.world.step(this.queue);
    this.queue.drainCollisionEvents((a, b, started) => {
      if (!started) return;
      const shot =
        this.colliderProjectiles.get(a) || this.colliderProjectiles.get(b);
      if (!shot || shot.removed || shot.type === "bomb") return;
      const other = shot.collider.handle === a ? b : a,
        unit = this.colliderUnits.get(other),
        prop = this.colliderProps.get(other);
      if (unit === shot.owner) return;
      if (shot.type === "stone") {
        this.explode(shot);
        return;
      }
      if (unit)
        this.hit(
          unit,
          shot.damage,
          shot.owner,
          shot.knock,
          shot.body.translation(),
          shot.pierce,
        );
      if (prop) this.damageProp(prop, shot.damage, shot.owner);
      this.removeProjectile(shot);
    });
    for (const p of this.projectiles)
      if (!p.removed) {
        p.life -= DT;
        if (p.body.translation().y < -5) this.removeProjectile(p);
        else if (p.life <= 0) {
          if (p.type === "bomb" || p.type === "stone") this.explode(p);
          else this.removeProjectile(p);
        }
      }
    this.projectiles = this.projectiles.filter((p) => !p.removed);
    for (const e of this.effects) e.life -= DT;
    this.effects = this.effects.filter((e) => e.life > 0);
    for (const d of this.ragdolls) {
      d.age += DT;
      if (d.age > 12 && !d.removed) this.retireDoll(d);
    }
    this.ragdolls = this.ragdolls.filter((d) => !d.removed);
    this.tick++;
    let red = 0,
      blue = 0,
      redScore = 0,
      blueScore = 0;
    for (const u of this.units)
      if (u.alive) {
        if (u.team === "red") {
          red++;
          redScore += unitPower(u);
        } else {
          blue++;
          blueScore += unitPower(u);
        }
      }
    if (!red || !blue || this.time >= MAX_TIME)
      this.result = {
        winner:
          !red && !blue
            ? "draw"
            : !red
              ? "blue"
              : !blue
                ? "red"
                : Math.abs(redScore - blueScore) < 1
                  ? "draw"
                  : redScore > blueScore
                    ? "red"
                    : "blue",
        time: this.time,
        red,
        blue,
        reason: this.time >= MAX_TIME ? "timeout" : "elimination",
      };
    if (this.result) {
      this.result.remainingValue = {
        red: rounded(redScore),
        blue: rounded(blueScore),
      };
      this.result.report = this.report();
      this.result.orders = structuredClone(this.orderState.log);
    }
  }
  report() {
    const units = this.units.map((u) => ({
      id: u.id,
      team: u.team,
      type: u.type,
      alive: u.alive,
      hp: rounded(u.hp),
      spawn: { x: u.x, z: u.z },
      ...Object.fromEntries(
        Object.entries(u.contribution).map(([k, v]) => [k, rounded(v)]),
      ),
    }));
    const props = this.props.map((p) => ({
      id: p.id,
      team: p.team,
      type: p.type,
      alive: p.alive,
      ...Object.fromEntries(
        Object.entries(p.contribution).map(([k, v]) => [k, rounded(v)]),
      ),
    }));
    const teams = {};
    for (const team of ["red", "blue"]) {
      const summary = {
        initial: 0,
        survived: 0,
        lost: 0,
        remainingValue: 0,
        ...contributions(),
        byType: {},
      };
      for (const u of units.filter((u) => u.team === team)) {
        const row = (summary.byType[u.type] ||= {
          initial: 0,
          survived: 0,
          lost: 0,
          ...contributions(),
        });
        for (const out of [row, summary]) {
          out.initial++;
          out[u.alive ? "survived" : "lost"]++;
          for (const key of Object.keys(contributions())) out[key] += u[key];
        }
        summary.remainingValue +=
          (u.hp / UNITS[u.type].hp) * UNITS[u.type].cost;
      }
      for (const p of props.filter((p) => p.team === team))
        for (const key of Object.keys(contributions())) summary[key] += p[key];
      for (const key of Object.keys(contributions()))
        summary[key] = rounded(summary[key]);
      summary.remainingValue = rounded(summary.remainingValue);
      teams[team] = summary;
    }
    const score = (u) =>
      u.damageDealt -
      2 * u.friendlyFire +
      0.75 * u.healing +
      30 * u.kills +
      20 * u.fallKills;
    const ranked = [...units].sort(
      (a, b) => score(b) - score(a) || a.id - b.id,
    );
    return {
      teams,
      units,
      props,
      mvp: ranked.length
        ? { ...ranked[0], score: rounded(score(ranked[0])) }
        : null,
    };
  }
  get time() {
    return this.tick * DT;
  }
  snapshot(state = {}) {
    state.time = this.time;
    state.result = this.result;
    state.tick = this.tick;
    state.commander = structuredClone(this.orderState);
    state.cues = mapInto(this.cues, state.cues, (cue, out = {}) =>
      Object.assign(out, cue),
    );
    state.units = mapInto(this.units, state.units, (u, out = {}) =>
      Object.assign(out, {
        id: u.id,
        type: u.type,
        team: u.team,
        hp: u.hp,
        alive: u.alive,
        angle: u.angle,
        attack: u.attack,
        charge: u.charge,
        stance: u.stance,
        inspired: u.inspireUntil > this.tick,
        fortified: u.fortifyUntil > this.tick,
        t: u.alive ? transform(u.body, out.t || []) : null,
      }),
    );
    state.projectiles = mapInto(
      this.projectiles,
      state.projectiles,
      (p, out = {}) =>
        Object.assign(out, {
          id: p.id,
          type: p.type,
          t: transform(p.body, out.t),
          v: Object.assign(out.v || {}, p.body.linvel()),
        }),
    );
    state.ragdolls = mapInto(this.ragdolls, state.ragdolls, (d, out = {}) =>
      Object.assign(out, {
        id: d.id,
        team: d.team,
        type: d.type,
        age: d.age,
        wreck: d.wreck ? [...d.wreck] : null,
        parts: mapInto(d.parts, out.parts, (p, part = {}) =>
          Object.assign(part, { name: p.name, t: transform(p.body, part.t) }),
        ),
      }),
    );
    state.effects = mapInto(this.effects, state.effects, (e, out = {}) =>
      Object.assign(out, e),
    );
    state.props = mapInto(this.props, state.props, (p, out = {}) =>
      Object.assign(out, {
        id: p.id,
        type: p.type,
        x: p.x,
        z: p.z,
        floor: p.floor,
        team: p.team,
        hp: Math.max(0, p.hp),
        alive: p.alive,
        active: p.fuse >= 0 || p.cooldown > 0,
      }),
    );
    return state;
  }
  dispose() {
    this.queue.free();
    this.world.free();
  }
}
