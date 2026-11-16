import RAPIER from "@dimforge/rapier3d-compat";
import { UNITS, MAPS, DT, MAX_TIME, validateConfig } from "./data.js";

let ready;
export function initPhysics() {
  return (ready ||= RAPIER.init());
}
const vec = (x = 0, y = 0, z = 0) => ({ x, y, z });
const horizontal = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);

/** No renderer, wall clock or browser dependency: the same simulation runs in Node. */
export class Battle {
  constructor(config) {
    validateConfig(config);
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
    this.colliderUnits = new Map();
    this.colliderProjectiles = new Map();
    this.tick = 0;
    this.nextId = 1;
    this.result = null;
    for (const p of MAPS[config.map].platforms)
      this.world.createCollider(
        RAPIER.ColliderDesc.cuboid(p.hx, p.hy, p.hz)
          .setTranslation(p.x, p.y, p.z)
          .setFriction(0.9),
      );
    for (const p of MAPS[config.map].rocks)
      this.world.createCollider(
        RAPIER.ColliderDesc.ball(p.r).setTranslation(p.x, p.r * 0.2, p.z),
      );
    for (const team of ["red", "blue"])
      for (const u of config[team]) this.spawn(team, u);
  }
  random() {
    this.rng = (Math.imul(this.rng, 1664525) + 1013904223) >>> 0;
    return this.rng / 4294967296;
  }
  spawn(team, u) {
    const d = UNITS[u.type],
      s = d.scale;
    const body = this.world.createRigidBody(
      RAPIER.RigidBodyDesc.dynamic()
        .setTranslation(u.x, 0.8 * s, u.z)
        .lockRotations()
        .setLinearDamping(0.6)
        .setCcdEnabled(true),
    );
    const collider = this.world.createCollider(
      RAPIER.ColliderDesc.capsule(0.35 * s, 0.27 * s)
        .setMass(d.mass)
        .setFriction(0.2)
        .setRestitution(0.05),
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
    };
    this.units.push(unit);
    this.colliderUnits.set(collider.handle, unit);
    return unit;
  }
  target(unit) {
    const p = unit.body.translation();
    let best = null,
      score = Infinity;
    for (const e of this.units)
      if (e.alive && e.team !== unit.team) {
        let s = horizontal(p, e.body.translation());
        if (unit.stance === "flank" && UNITS[e.type].ranged) s *= 0.65;
        if (s < score) {
          best = e;
          score = s;
        }
      }
    return best;
  }
  command(u) {
    const d = UNITS[u.type],
      p = u.body.translation();
    if (p.y < -4 || Math.abs(p.x) > 23 || Math.abs(p.z) > 19) {
      this.kill(u, vec());
      return;
    }
    u.cooldown -= DT;
    u.attack = Math.max(0, u.attack - DT * 3);
    u.stun = Math.max(0, u.stun - DT);
    const target = this.target(u);
    if (!target) return;
    const tp = target.body.translation(),
      dist = horizontal(p, tp);
    const dx = tp.x - p.x,
      dz = tp.z - p.z;
    u.angle = Math.atan2(dx, dz);
    const min = UNITS[target.type].scale * 0.25;
    let mx = dx / (dist || 1),
      mz = dz / (dist || 1),
      speed = d.speed;
    const firing = d.ranged && dist < d.range;
    if (d.ranged && dist < 4 && u.stance !== "hold") {
      mx *= -1;
      mz *= -1;
      speed *= 0.7;
    } else if (
      (d.ranged && firing) ||
      (!d.ranged && dist < d.range + min - 0.15) ||
      u.stance === "hold"
    )
      speed = 0;
    // A waypoint brings troops to the bridge before they try to cross the chasm.
    if (
      this.config.map === "bridge" &&
      Math.abs(p.x) > 3.5 &&
      p.x * tp.x < 0 &&
      Math.abs(p.z) > 1.1
    ) {
      const wx = (p.x < 0 ? -4.2 : 4.2) - p.x,
        wz = -p.z,
        wl = Math.hypot(wx, wz) || 1;
      mx = wx / wl;
      mz = wz / wl;
      if (u.stance !== "hold") speed = d.speed;
    } else if (
      u.stance === "flank" &&
      !d.ranged &&
      dist > 6 &&
      this.config.map !== "bridge"
    ) {
      const flank = Math.sign(u.z || 1) * 5;
      mz = (tp.z + flank - p.z) / (dist + 3);
      const norm = Math.hypot(mx, mz) || 1;
      mx /= norm;
      mz /= norm;
    }
    // Soft separation lets physics resolve the remaining contact pressure.
    if (speed > 0)
      for (const friend of this.units)
        if (friend !== u && friend.alive && friend.team === u.team) {
          const fp = friend.body.translation(),
            dd = horizontal(p, fp),
            radius = 0.65 * (d.scale + UNITS[friend.type].scale);
          if (dd < radius && dd > 0.01) {
            mx += ((p.x - fp.x) / dd) * (radius - dd) * 0.7;
            mz += ((p.z - fp.z) / dd) * (radius - dd) * 0.7;
          }
        }
    const v = u.body.linvel(),
      norm = Math.max(1, Math.hypot(mx, mz)),
      response = u.stun > 0 ? 0.015 : 0.16;
    // Do not steer while airborne: explosions retain their momentum.
    if (p.y < d.scale * 1.1)
      u.body.applyImpulse(
        vec(
          ((mx / norm) * speed - v.x) * d.mass * response,
          0,
          ((mz / norm) * speed - v.z) * d.mass * response,
        ),
        true,
      );
    if (u.cooldown <= 0 && p.y < d.scale * 1.4 && dist < d.range + min) {
      u.cooldown = d.cooldown;
      u.attack = 1;
      if (d.ranged) this.shoot(u, target);
      else if (u.type === "giant") {
        for (const e of this.units)
          if (
            e.alive &&
            e.team !== u.team &&
            horizontal(p, e.body.translation()) < d.range + min
          )
            this.hit(e, d.damage, u, d.knock);
        this.effect("slam", p, 1.1);
      } else this.hit(target, d.damage, u, d.knock);
    }
  }
  hit(target, damage, attacker, knock, origin = null) {
    if (!target.alive) return;
    const p = target.body.translation(),
      from = origin || attacker.body.translation();
    const dx = p.x - from.x,
      dz = p.z - from.z,
      dist = Math.hypot(dx, dz) || 1;
    // Shields only protect the facing hemisphere.
    if (
      target.type === "shield" &&
      (Math.sin(target.angle) * -dx + Math.cos(target.angle) * -dz) / dist >
        0.25
    )
      damage *= 0.4;
    target.hp -= damage;
    target.stun = 0.25;
    const impulse = vec((dx / dist) * knock, knock * 0.32, (dz / dist) * knock);
    target.body.applyImpulse(impulse, true);
    if (target.hp <= 0) {
      attacker && attacker.kills++;
      this.kill(target, impulse);
    }
  }
  shoot(u, target) {
    const d = UNITS[u.type],
      p = u.body.translation(),
      t = target.body.translation();
    const dx = t.x - p.x,
      dz = t.z - p.z,
      dist = Math.hypot(dx, dz) || 1;
    const bomb = u.type === "bomber",
      flight = Math.max(0.35, dist / (bomb ? 10 : 19));
    const spawn = vec(
      p.x + (dx / dist) * 0.7,
      p.y + 0.5 * d.scale,
      p.z + (dz / dist) * 0.7,
    );
    const spread = (this.random() - 0.5) * (bomb ? 0.9 : 0.55),
      tv = target.body.linvel();
    const velocity = vec(
      (t.x + tv.x * flight * 0.6 - spawn.x + spread) / flight,
      (t.y + 0.15 - spawn.y + 9 * flight * flight) / flight,
      (t.z + tv.z * flight * 0.6 - spawn.z + spread) / flight,
    );
    const body = this.world.createRigidBody(
      RAPIER.RigidBodyDesc.dynamic()
        .setTranslation(spawn.x, spawn.y, spawn.z)
        .setLinvel(velocity.x, velocity.y, velocity.z)
        .setCcdEnabled(true),
    );
    const collider = this.world.createCollider(
      RAPIER.ColliderDesc.ball(bomb ? 0.18 : 0.09)
        .setMass(bomb ? 1.5 : 0.15)
        .setRestitution(bomb ? 0.35 : 0)
        .setActiveEvents(RAPIER.ActiveEvents.COLLISION_EVENTS),
      body,
    );
    const projectile = {
      id: this.nextId++,
      type: bomb ? "bomb" : "arrow",
      body,
      collider,
      owner: u,
      life: bomb ? 1.8 : 5,
      removed: false,
    };
    this.projectiles.push(projectile);
    this.colliderProjectiles.set(collider.handle, projectile);
  }
  explode(projectile) {
    const p = projectile.body.translation();
    this.effect("explosion", p, 0.7);
    for (const e of this.units)
      if (e.alive) {
        const ep = e.body.translation(),
          dist = Math.hypot(ep.x - p.x, ep.y - p.y, ep.z - p.z);
        if (dist < 3.6)
          this.hit(
            e,
            UNITS.bomber.damage * (1 - dist / 4.5),
            projectile.owner,
            UNITS.bomber.knock * (1 - dist / 4.5),
            p,
          );
      }
    // Corpses also react to explosions.
    for (const doll of this.ragdolls)
      for (const part of doll.parts) {
        const q = part.body.translation(),
          dx = q.x - p.x,
          dz = q.z - p.z,
          dist = Math.hypot(dx, dz);
        if (dist < 4) part.body.applyImpulse(vec(dx * 1.5, 5, dz * 1.5), true);
      }
    this.removeProjectile(projectile);
  }
  removeProjectile(p) {
    if (p.removed) return;
    p.removed = true;
    this.colliderProjectiles.delete(p.collider.handle);
    this.world.removeRigidBody(p.body);
  }
  kill(u, impulse) {
    if (!u.alive) return;
    u.alive = false;
    u.hp = 0;
    const p = u.body.translation(),
      v = u.body.linvel(),
      s = UNITS[u.type].scale;
    this.colliderUnits.delete(u.collider.handle);
    this.world.removeRigidBody(u.body);
    const specifications = [
      { name: "torso", o: vec(0, 0, 0), h: vec(0.23, 0.32, 0.16) },
      { name: "head", o: vec(0, 0.53, 0), h: vec(0.2, 0.2, 0.2) },
      { name: "armL", o: vec(-0.4, -0.02, 0), h: vec(0.11, 0.3, 0.11) },
      { name: "armR", o: vec(0.4, -0.02, 0), h: vec(0.11, 0.3, 0.11) },
      { name: "legL", o: vec(-0.16, -0.57, 0), h: vec(0.12, 0.3, 0.13) },
      { name: "legR", o: vec(0.16, -0.57, 0), h: vec(0.12, 0.3, 0.13) },
    ];
    const parts = specifications.map((q) => {
      const body = this.world.createRigidBody(
        RAPIER.RigidBodyDesc.dynamic()
          .setTranslation(p.x + q.o.x * s, p.y + q.o.y * s, p.z + q.o.z * s)
          .setLinvel(v.x, v.y, v.z)
          .setAngularDamping(0.8)
          .setLinearDamping(0.2),
      );
      this.world.createCollider(
        RAPIER.ColliderDesc.cuboid(q.h.x * s, q.h.y * s, q.h.z * s)
          .setMass(q.name === "torso" ? 2 : 0.5)
          .setFriction(0.8),
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
    this.ragdolls.push({ id: u.id, team: u.team, type: u.type, parts, age: 0 });
    this.events.push({
      tick: this.tick,
      type: "death",
      team: u.team,
      unit: u.type,
    });
  }
  effect(type, p, life) {
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
    for (const u of this.units) if (u.alive) this.command(u);
    this.world.step(this.queue);
    this.queue.drainCollisionEvents((a, b, started) => {
      if (!started) return;
      const projectile =
        this.colliderProjectiles.get(a) || this.colliderProjectiles.get(b);
      if (!projectile || projectile.removed || projectile.type === "bomb")
        return;
      const other = projectile.collider.handle === a ? b : a,
        unit = this.colliderUnits.get(other);
      if (unit === projectile.owner) return;
      if (unit)
        this.hit(
          unit,
          UNITS.archer.damage,
          projectile.owner,
          UNITS.archer.knock,
          projectile.body.translation(),
        );
      this.removeProjectile(projectile);
    });
    for (const p of this.projectiles)
      if (!p.removed) {
        p.life -= DT;
        if (p.life <= 0 || p.body.translation().y < -5) {
          if (p.type === "bomb" && p.life <= 0) this.explode(p);
          else this.removeProjectile(p);
        }
      }
    this.projectiles = this.projectiles.filter((p) => !p.removed);
    for (const e of this.effects) e.life -= DT;
    this.effects = this.effects.filter((e) => e.life > 0);
    for (const d of this.ragdolls) d.age += DT;
    // Bound physics load during larger battles; settled corpses disappear after 18 s.
    for (const d of this.ragdolls)
      if (d.age > 18 && !d.removed) {
        for (const p of d.parts) this.world.removeRigidBody(p.body);
        d.removed = true;
      }
    this.ragdolls = this.ragdolls.filter((d) => !d.removed);
    this.tick++;
    const red = this.units.filter((u) => u.alive && u.team === "red"),
      blue = this.units.filter((u) => u.alive && u.team === "blue");
    if (!red.length || !blue.length || this.time >= MAX_TIME) {
      const score = (army) =>
        army.reduce((sum, u) => sum + (u.hp / u.maxHp) * UNITS[u.type].cost, 0);
      let winner =
        !red.length && !blue.length
          ? "draw"
          : !red.length
            ? "blue"
            : !blue.length
              ? "red"
              : Math.abs(score(red) - score(blue)) < 1
                ? "draw"
                : score(red) > score(blue)
                  ? "red"
                  : "blue";
      this.result = {
        winner,
        time: this.time,
        red: red.length,
        blue: blue.length,
        reason: this.time >= MAX_TIME ? "timeout" : "elimination",
      };
    }
  }
  get time() {
    return this.tick * DT;
  }
  snapshot() {
    const transform = (body) => {
      const p = body.translation(),
        q = body.rotation();
      return [p.x, p.y, p.z, q.x, q.y, q.z, q.w];
    };
    return {
      time: this.time,
      result: this.result,
      units: this.units.map((u) => ({
        id: u.id,
        type: u.type,
        team: u.team,
        hp: u.hp,
        alive: u.alive,
        angle: u.angle,
        attack: u.attack,
        t: u.alive ? transform(u.body) : null,
      })),
      projectiles: this.projectiles.map((p) => ({
        id: p.id,
        type: p.type,
        t: transform(p.body),
        v: p.body.linvel(),
      })),
      ragdolls: this.ragdolls.map((d) => ({
        id: d.id,
        team: d.team,
        type: d.type,
        parts: d.parts.map((p) => ({ name: p.name, t: transform(p.body) })),
      })),
      effects: this.effects.map((e) => ({ ...e })),
    };
  }
  dispose() {
    this.queue.free();
    this.world.free();
  }
}
