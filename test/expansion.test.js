import test from "node:test";
import assert from "node:assert/strict";
import { Group, Mesh, BoxGeometry, MeshBasicMaterial, Matrix4 } from "three";
import { Battle, initPhysics, MAX_RAGDOLLS } from "../src/engine.js";
import {
  VERSION,
  UNITS,
  MAPS,
  MISSIONS,
  MAX_UNITS,
  formation,
  cost,
  defaultConfig,
  heightAt,
  teamCost,
  validateConfig,
} from "../src/data.js";
import { ActorBatches } from "../src/batches.js";
await initPhysics();
const unit = (type, x, z = 0) => ({ type, x, z, stance: "hold" });
const fixture = (map = "meadow") => ({
  version: VERSION,
  map,
  seed: 42,
  budget: 10000,
  red: [unit("militia", -5)],
  blue: [unit("militia", 15)],
  props: [],
});
function run(b, ticks) {
  for (let i = 0; i < ticks && !b.result; i++) b.step();
}

test("all six battlefields settle mixed new armies with finite transforms", () => {
  assert.equal(Object.keys(UNITS).length, 10);
  assert.equal(Object.keys(MAPS).length, 6);
  for (const map of Object.keys(MAPS)) {
    const c = defaultConfig(map);
    c.blue = formation("siege", "blue");
    const b = new Battle(c);
    try {
      run(b, 5401);
      assert.ok(b.result, map + " must terminate");
      for (const u of b.snapshot().units)
        if (u.t) assert.ok(u.t.every(Number.isFinite));
    } finally {
      b.dispose();
    }
  }
});
test("medics heal living allies without resurrecting them", () => {
  const c = fixture();
  c.red = [unit("medic", -6), unit("militia", -5, 1)];
  const b = new Battle(c);
  try {
    b.units[1].hp = 20;
    run(b, 120);
    assert.ok(b.units[1].hp > 40);
    b.kill(b.units[1], { x: 0, y: 0, z: 0 });
    run(b, 60);
    assert.equal(b.units[1].alive, false);
  } finally {
    b.dispose();
  }
});
test("a cavalry run produces a charged impact", () => {
  const c = fixture();
  c.red = [{ ...unit("cavalry", -9), stance: "advance" }];
  c.blue = [unit("giant", 8)];
  const b = new Battle(c);
  try {
    let charged = false;
    for (let i = 0; i < 400 && !b.result; i++) {
      b.step();
      charged ||= b.effects.some((e) => e.type === "charge");
    }
    assert.ok(charged);
  } finally {
    b.dispose();
  }
});
test("a barricade physically blocks and absorbs crossbow bolts", () => {
  const c = fixture();
  c.red = [unit("crossbow", -9)];
  c.blue = [unit("militia", 3)];
  c.props = [{ type: "barricade", team: "red", x: -3.5, z: 0 }];
  const b = new Battle(c);
  try {
    run(b, 200);
    assert.equal(b.units[1].hp, UNITS.militia.hp);
    assert.ok(b.props[0].hp < 240);
  } finally {
    b.dispose();
  }
});
test("explosive barrels chain and damage the owning army", () => {
  const c = fixture();
  c.props = [
    { type: "barrel", team: "red", x: -4, z: 0 },
    { type: "barrel", team: "red", x: -6, z: 0 },
  ];
  const b = new Battle(c);
  try {
    b.damageProp(b.props[0], 999);
    assert.ok(b.props.every((p) => !p.alive));
    assert.ok(b.units[0].hp < 80);
    assert.equal(b.events.filter((e) => e.type === "prop-destroyed").length, 2);
  } finally {
    b.dispose();
  }
});
test("spikes hurt enemies while leaving the owning team unharmed", () => {
  const c = fixture();
  c.props = [{ type: "spikes", team: "red", x: -3, z: 0 }];
  const b = new Battle(c);
  try {
    for (const [i, u] of b.units.entries()) {
      u.body.setTranslation({ x: -3, y: 0.7, z: i * 0.5 }, true);
      u.cooldown = 999;
    }
    b.step();
    assert.equal(b.units[0].hp, 80);
    assert.ok(b.units[1].hp < 80);
  } finally {
    b.dispose();
  }
});
test("spring pads launch rigid bodies and respect a trigger cooldown", () => {
  const c = fixture();
  c.props = [{ type: "spring", team: "red", x: -5, z: 0 }];
  const b = new Battle(c);
  try {
    b.step();
    assert.ok(b.units[0].body.linvel().y > 5);
    assert.ok(b.props[0].cooldown > 1);
    assert.ok(b.units[0].body.linvel().x > 4);
  } finally {
    b.dispose();
  }
});
test("neutral supply banners heal both armies", () => {
  const c = fixture("marsh");
  const b = new Battle(c);
  try {
    for (const [i, u] of b.units.entries()) {
      u.hp = 30;
      u.cooldown = 999;
      u.body.setTranslation({ x: i ? 1 : -1, y: 0.8, z: 9.4 }, true);
    }
    run(b, 30);
    assert.ok(b.units.every((u) => u.hp > 30));
  } finally {
    b.dispose();
  }
});
test("mud slows advance relative to the same open-ground formation", () => {
  const positions = [];
  for (const map of ["meadow", "marsh"]) {
    const c = fixture(map);
    c.red[0].stance = "advance";
    const b = new Battle(c);
    try {
      run(b, 120);
      positions.push(b.units[0].body.translation().x);
    } finally {
      b.dispose();
    }
  }
  assert.ok(positions[1] < positions[0] - 1);
});
test("ramps support units and elevated archers reach otherwise unreachable targets", () => {
  assert.ok(Math.abs(heightAt("highland", 0, 0) - 2.2) < 0.001);
  assert.ok(heightAt("highland", -5.5, 0) > 1);
  const shots = [];
  for (const map of ["meadow", "highland"]) {
    const c = fixture(map);
    c.red = [unit("archer", -2.5)];
    c.blue = [unit("militia", 16)];
    const b = new Battle(c);
    try {
      run(b, 60);
      shots.push(b.projectiles.length);
      if (map === "highland") assert.ok(b.units[0].body.translation().y > 2);
    } finally {
      b.dispose();
    }
  }
  assert.equal(shots[0], 0);
  assert.ok(shots[1] > 0);
});
test("corpses remain bounded during a mass casualty event", () => {
  const c = fixture();
  c.red = Array.from({ length: 35 }, (_, i) =>
    unit("militia", -4 - Math.floor(i / 9) * 3, ((i % 9) - 4) * 2),
  );
  const b = new Battle(c);
  try {
    for (const u of b.units.slice(0, 35)) b.kill(u, { x: 0, y: 0, z: 0 });
    assert.equal(b.ragdolls.length, MAX_RAGDOLLS);
    assert.equal(b.world.impulseJoints.len(), MAX_RAGDOLLS * 5);
  } finally {
    b.dispose();
  }
});
test("render snapshot reuse cannot mutate recorded replay frames", () => {
  const b = new Battle(defaultConfig());
  try {
    const recorded = b.snapshot(),
      json = JSON.stringify(recorded),
      live = b.snapshot({});
    run(b, 20);
    assert.equal(b.snapshot(live), live);
    assert.equal(JSON.stringify(recorded), json);
    assert.notEqual(live.time, recorded.time);
  } finally {
    b.dispose();
  }
});
test("instancing batches shared geometry and skips invisible children", () => {
  const source = new Group(),
    g = new BoxGeometry(1, 1, 1),
    m = new MeshBasicMaterial();
  for (let i = 0; i < 20; i++) {
    const mesh = new Mesh(g, m);
    mesh.position.x = i;
    source.add(mesh);
  }
  source.children[0].visible = false;
  const batches = new ActorBatches(source);
  batches.sync();
  assert.equal(batches.meshCount, 19);
  assert.equal(batches.batchCount, 1);
  const first = [...batches.batches.values()][0],
    matrix = new Matrix4();
  first.getMatrixAt(0, matrix);
  assert.equal(matrix.elements[12], 1);
  batches.dispose();
  g.dispose();
  m.dispose();
});
test("legacy formations migrate and props share the army budget", () => {
  const c = fixture();
  c.version = "0.1.0";
  delete c.props;
  const normalized = validateConfig(c);
  assert.equal(normalized.version, VERSION);
  assert.deepEqual(normalized.props, []);
  c.version = VERSION;
  c.budget = 100;
  c.props = [{ type: "banner", team: "red", x: -9, z: 0 }];
  assert.equal(teamCost(c, "red"), 140);
  assert.throws(() => validateConfig(c), /军费/);
  const tooMany = fixture();
  tooMany.red = Array.from({ length: MAX_UNITS + 1 }, () =>
    unit("militia", -6),
  );
  assert.throws(() => validateConfig(tooMany), /最多/);
});
test("inherited object keys are rejected as game identifiers", () => {
  for (const kind of ["unit", "prop", "map"]) {
    const c = fixture();
    if (kind === "unit") c.red[0].type = "constructor";
    else if (kind === "prop")
      c.props = [{ type: "__proto__", team: "red", x: -8, z: 0 }];
    else c.map = "constructor";
    assert.throws(() => validateConfig(c));
  }
});
test("flanking cavalry follow the outer opening past ruin walls", () => {
  const c = fixture("ruins");
  c.red = [{ ...unit("cavalry", -10, 6), stance: "flank" }];
  c.blue = [unit("giant", 15, 0)];
  const b = new Battle(c);
  try {
    let crossed = false,
      outer = false;
    for (let i = 0; i < 900 && !b.result; i++) {
      b.step();
      const p = b.units[0].body.translation();
      outer ||= p.z > 9.5;
      crossed ||= p.x > 1;
      if (crossed) break;
    }
    assert.ok(outer);
    assert.ok(crossed);
  } finally {
    b.dispose();
  }
});
test("ice preserves more horizontal momentum than open ground", () => {
  const velocities = [];
  for (const map of ["meadow", "highland"]) {
    const c = fixture(map);
    c.red = [unit("militia", -2.5)];
    const b = new Battle(c);
    try {
      b.units[0].body.setLinvel({ x: 5, y: 0, z: 0 }, true);
      b.step();
      velocities.push(b.units[0].body.linvel().x);
    } finally {
      b.dispose();
    }
  }
  assert.ok(velocities[1] > velocities[0] + 0.2);
});
test("all six trials have a winning legal reference formation", () => {
  const references = [
    "balanced",
    "volley",
    "volley",
    "siege",
    "volley",
    "siege",
  ];
  for (const [index, m] of MISSIONS.entries()) {
    const red = [];
    for (const u of formation(references[index]))
      if (cost(red) + UNITS[u.type].cost <= m.budget) red.push(u);
    const blue = formation(m.enemy, "blue");
    const b = new Battle({
      version: VERSION,
      map: m.map,
      budget: Math.max(m.budget, cost(blue)),
      seed: 20261116,
      red,
      blue,
      props: [],
    });
    try {
      run(b, 5401);
      assert.equal(b.result?.winner, "red", m.name);
    } finally {
      b.dispose();
    }
  }
});
