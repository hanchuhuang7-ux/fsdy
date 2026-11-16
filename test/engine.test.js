import test from "node:test";
import assert from "node:assert/strict";
import { Battle, initPhysics } from "../src/engine.js";
import {
  VERSION,
  UNITS,
  defaultConfig,
  formation,
  validateConfig,
} from "../src/data.js";

await initPhysics();
function complete(config) {
  const b = new Battle(config);
  try {
    while (!b.result) b.step();
    return { result: b.result, events: b.events, snapshot: b.snapshot() };
  } finally {
    b.dispose();
  }
}
function small() {
  return {
    version: VERSION,
    map: "meadow",
    seed: 42,
    budget: 1500,
    red: [{ type: "militia", x: -3, z: 0, stance: "advance" }],
    blue: [{ type: "militia", x: 3, z: 0, stance: "advance" }],
  };
}

test("same inputs reproduce results and physics transforms", () => {
  const c = defaultConfig();
  assert.deepEqual(complete(c), complete(c));
});
test("all three maps resolve a mixed army battle with finite physics", () => {
  for (const map of ["meadow", "bridge", "island"]) {
    const c = defaultConfig(map);
    c.red = formation("volley");
    c.blue = formation("titans", "blue");
    const r = complete(c);
    assert.ok(r.result.time <= 90.02);
    assert.ok(r.events.length > 0);
    for (const u of r.snapshot.units)
      if (u.t) assert.ok(u.t.every(Number.isFinite));
    assert.ok(["red", "blue", "draw"].includes(r.result.winner));
  }
});
test("fatal combat creates a six-body ragdoll linked by five joints", () => {
  const b = new Battle(small());
  try {
    while (!b.result) b.step();
    assert.ok(b.ragdolls.length > 0);
    assert.equal(b.ragdolls[0].parts.length, 6);
    assert.equal(b.world.impulseJoints.len(), b.ragdolls.length * 5);
    assert.equal(b.result.reason, "elimination");
  } finally {
    b.dispose();
  }
});
test("explosions damage their own army and impart physical velocity", () => {
  const c = small();
  c.red = [
    { type: "bomber", x: -4, z: 0, stance: "hold" },
    { type: "shield", x: -6, z: 0, stance: "hold" },
  ];
  const b = new Battle(c);
  try {
    const bomber = b.units[0],
      friend = b.units[1];
    b.shoot(bomber, b.units[2]);
    const bomb = b.projectiles[0];
    bomb.body.setTranslation({ x: -6, y: 1, z: 0.5 }, true);
    bomb.life = 0.0001;
    b.step();
    assert.ok(friend.hp < UNITS.shield.hp, "friendly fire must deal damage");
    assert.ok(
      Math.hypot(
        friend.body.linvel().x,
        friend.body.linvel().y,
        friend.body.linvel().z,
      ) > 0.1,
      "shockwave must move a rigid body",
    );
  } finally {
    b.dispose();
  }
});
test("a soldier falling into the chasm is eliminated", () => {
  const c = small();
  c.map = "bridge";
  const b = new Battle(c);
  try {
    b.units[0].body.setTranslation({ x: 0, y: -5, z: 5 }, true);
    b.step();
    assert.equal(b.units[0].alive, false);
    assert.equal(b.result.winner, "blue");
  } finally {
    b.dispose();
  }
});
test("shields reduce frontal damage but do not protect the rear", () => {
  const c = small();
  c.blue[0].type = "shield";
  const b = new Battle(c);
  try {
    const [a, s] = b.units;
    s.angle = -Math.PI / 2;
    b.hit(s, 50, a, 0);
    const front = UNITS.shield.hp - s.hp;
    s.angle = Math.PI / 2;
    const before = s.hp;
    b.hit(s, 50, a, 0);
    const rear = before - s.hp;
    assert.ok(front < rear);
    assert.equal(rear, 50);
  } finally {
    b.dispose();
  }
});
test("hold formations time out without hanging the simulation", () => {
  const c = small();
  c.red[0].stance = "hold";
  c.blue[0].stance = "hold";
  const r = complete(c);
  assert.equal(r.result.reason, "timeout");
  assert.equal(r.result.winner, "draw");
});
test("imports reject unknown types, illegal positions, budgets and excessive armies", () => {
  for (const change of [
    (c) => (c.red[0].type = "unknown"),
    (c) => (c.red[0].x = 4),
    (c) => (c.red[0].x = NaN),
    (c) => (c.seed = -1),
    (c) => (c.budget = 10001),
    (c) => (c.red = Array.from({ length: 46 }, () => c.red[0])),
  ]) {
    const c = small();
    change(c);
    assert.throws(() => validateConfig(c));
  }
  const c = small();
  c.red = [{ type: "giant", x: -5, z: 0, stance: "advance" }];
  c.budget = 100;
  assert.throws(() => validateConfig(c));
});
