import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { MAPS, UNITS, PROPS, heightAt } from "./data.js";
import { ActorBatches } from "./batches.js";

const PALETTE = {
  red: 0xbf6650,
  blue: 0x527f9e,
  skin: 0xefd8ae,
  metal: 0x798777,
  dark: 0x485b4c,
  wood: 0x8b7050,
};
export class BattlefieldView {
  constructor(container) {
    this.container = container;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0xdfe5d9);
    this.scene.fog = new THREE.Fog(0xdfe5d9, 55, 115);
    this.renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: false,
      powerPreference: "high-performance",
    });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.setClearColor(0xdfe5d9);
    container.append(this.renderer.domElement);
    this.camera = new THREE.PerspectiveCamera(37, 1, 0.1, 180);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.09;
    this.controls.maxPolarAngle = Math.PI * 0.47;
    this.controls.minDistance = 13;
    this.controls.maxDistance = 75;
    this.controls.mouseButtons = {
      LEFT: THREE.MOUSE.ROTATE,
      MIDDLE: THREE.MOUSE.DOLLY,
      RIGHT: THREE.MOUSE.PAN,
    };
    this.scene.add(new THREE.HemisphereLight(0xffffef, 0x8b9e89, 2.5));
    const sun = new THREE.DirectionalLight(0xfff6df, 3.2);
    sun.position.set(-18, 32, 18);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, {
      left: -26,
      right: 26,
      top: 26,
      bottom: -26,
      near: 1,
      far: 90,
    });
    sun.shadow.bias = -0.0008;
    sun.shadow.normalBias = 0.035;
    this.scene.add(sun);
    this.terrain = new THREE.Group();
    this.actors = new THREE.Group();
    this.overlay = new THREE.Group();
    this.scene.add(this.terrain, this.actors, this.overlay);
    this.actorBatches = new ActorBatches(this.actors);
    this.scene.add(this.actorBatches.group);
    this.propModels = new Map();
    this.groundMeshes = [];
    this.sun = sun;
    this.metrics = { fps: 60, calls: 0, meshes: 0, batches: 0 };
    this.lastRenderTime = 0;
    this.models = new Map();
    this.dolls = new Map();
    this.shots = new Map();
    this.bursts = new Map();
    this.flags = [];
    this.grid = [];
    this.ray = new THREE.Raycaster();
    this.pointer = new THREE.Vector2();
    this.plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    this.geometryCache = new Map();
    this.materialCache = new Map();
    this.ghost = this.mesh(
      new THREE.RingGeometry(0.4, 0.48, 32),
      this.mat(0xffffff, {
        transparent: true,
        opacity: 0.7,
        side: THREE.DoubleSide,
      }),
    );
    this.ghost.rotation.x = -Math.PI / 2;
    this.ghost.visible = false;
    this.overlay.add(this.ghost);
    this.resetCamera();
    this.observer = new ResizeObserver(() => this.resize());
    this.observer.observe(container);
    this.resize();
    this.setQuality("balanced");
  }
  geo(key, create) {
    if (!this.geometryCache.has(key)) this.geometryCache.set(key, create());
    return this.geometryCache.get(key);
  }
  setQuality(quality) {
    this.quality = quality;
    this.renderer.setPixelRatio(
      Math.min(
        devicePixelRatio,
        quality === "low" ? 1 : quality === "high" ? 2 : 1.35,
      ),
    );
    this.renderer.shadowMap.enabled = quality !== "low";
    const size = quality === "high" ? 2048 : 1024;
    this.sun.shadow.mapSize.set(size, size);
    if (this.sun.shadow.map) {
      this.sun.shadow.map.dispose();
      this.sun.shadow.map = null;
    }
    this.resize();
  }
  mat(color, options = {}) {
    const key = color + JSON.stringify(options);
    if (!this.materialCache.has(key))
      this.materialCache.set(
        key,
        new THREE.MeshStandardMaterial({ color, roughness: 0.93, ...options }),
      );
    return this.materialCache.get(key);
  }
  box(x, y, z) {
    const key = `b${x},${y},${z}`;
    if (!this.geometryCache.has(key))
      this.geometryCache.set(key, new THREE.BoxGeometry(x, y, z));
    return this.geometryCache.get(key);
  }
  ball(r) {
    const key = `s${r}`;
    if (!this.geometryCache.has(key))
      this.geometryCache.set(key, new THREE.SphereGeometry(r, 10, 8));
    return this.geometryCache.get(key);
  }
  mesh(geometry, material) {
    const m = new THREE.Mesh(geometry, material);
    m.castShadow = true;
    m.receiveShadow = true;
    return m;
  }
  addBox(parent, size, pos, color) {
    const m = this.mesh(this.box(...size), this.mat(color));
    m.position.set(...pos);
    parent.add(m);
    return m;
  }
  resize() {
    const w = this.container.clientWidth,
      h = this.container.clientHeight;
    if (!w || !h) return;
    this.renderer.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }
  resetCamera() {
    this.camera.position.set(25, 30, 35);
    this.controls.target.set(0, 0, 0);
    this.controls.update();
  }
  map(id) {
    this.mapId = id;
    this.terrain.traverse((object) => {
      if (
        object.geometry &&
        !this.geometryCache.has(object.userData.cacheKey) &&
        !Array.from(this.geometryCache.values()).includes(object.geometry)
      )
        object.geometry.dispose();
    });
    this.terrain.clear();
    this.groundMeshes = [];
    this.overlay.remove(...this.grid);
    this.grid = [];
    this.flags = [];
    this.clearActors();
    const map = MAPS[id];
    // Floating, layered terrain is also the exact collision footprint used by Rapier.
    for (const p of map.platforms) {
      const ground = this.addBox(
        this.terrain,
        [p.hx * 2, p.hy * 2, p.hz * 2],
        [p.x, p.y, p.z],
        p.bridge ? 0x9b825e : 0xa79f80,
      );
      ground.rotation.z = p.angle || 0;
      this.groundMeshes.push(ground);
      const cap = this.addBox(
        this.terrain,
        [p.hx * 2 + 0.02, 0.14, p.hz * 2 + 0.02],
        [p.x, p.y + p.hy - 0.07, p.z],
        p.bridge ? 0xbca37b : map.color,
      );
      if (p.angle) {
        cap.position.set(
          p.x - Math.sin(p.angle) * (p.hy - 0.07),
          p.y + Math.cos(p.angle) * (p.hy - 0.07),
          p.z,
        );
        cap.rotation.z = p.angle;
      }
      if (p.bridge)
        for (let x = -2.8; x <= 2.8; x += 0.43)
          this.addBox(this.terrain, [0.36, 0.08, 3.5], [x, 0.02, 0], 0xc9b38d);
      else if (!p.ramp)
        this.addBox(
          this.terrain,
          [p.hx * 1.96, 0.35, p.hz * 1.94],
          [p.x, p.y - p.hy - 0.12, p.z],
          0x8d8d74,
        );
    }
    for (const p of map.walls) {
      this.addBox(
        this.terrain,
        [p.hx * 2, p.hy * 2, p.hz * 2],
        [p.x, p.hy, p.z],
        0x9a9c87,
      );
      for (let z = p.z - p.hz + 0.3; z < p.z + p.hz; z += 1.1)
        this.addBox(
          this.terrain,
          [p.hx * 2 + 0.16, 0.18, 0.8],
          [p.x, p.hy * 2 + 0.09, z],
          0xb8baa0,
        );
    }
    for (const zone of map.zones) {
      const floor = heightAt(id, zone.x, zone.z),
        mat = this.mat(zone.type === "mud" ? 0x697b50 : 0xbcdadd, {
          transparent: true,
          opacity: zone.type === "mud" ? 0.82 : 0.72,
          roughness: 0.32,
        });
      const patch = this.mesh(
        this.geo(
          "zone" + zone.hx + "," + zone.hz,
          () => new THREE.PlaneGeometry(zone.hx * 2, zone.hz * 2),
        ),
        mat,
      );
      patch.rotation.x = -Math.PI / 2;
      patch.position.set(zone.x, floor + 0.015, zone.z);
      patch.castShadow = false;
      this.terrain.add(patch);
      for (let i = 0; i < 10; i++) {
        const x = zone.x + ((i % 5) - 2) * zone.hx * 0.32,
          z = zone.z + (Math.floor(i / 5) - 0.5) * zone.hz;
        const spot = this.mesh(
          this.geo("wetSpot", () => new THREE.CircleGeometry(0.45, 12)),
          this.mat(zone.type === "mud" ? 0x8b9a66 : 0xe1f1e8, {
            transparent: true,
            opacity: 0.45,
            side: THREE.DoubleSide,
          }),
        );
        spot.rotation.x = -Math.PI / 2;
        spot.position.set(x, heightAt(id, x, z) + 0.03, z);
        spot.castShadow = false;
        this.terrain.add(spot);
      }
    }
    const water = this.mesh(
      new THREE.PlaneGeometry(180, 180),
      this.mat(0xc3d8d0, { roughness: 0.5 }),
    );
    water.rotation.x = -Math.PI / 2;
    water.position.y = -3.5;
    water.receiveShadow = true;
    this.terrain.add(water);
    for (const rock of map.rocks) {
      const m = this.mesh(
        new THREE.DodecahedronGeometry(rock.r, 0),
        this.mat(0x899382),
      );
      m.position.set(rock.x, rock.r * 0.22, rock.z);
      m.rotation.set(0.4, 0.7, 0.2);
      this.terrain.add(m);
    }
    let seed = 31;
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };
    for (let i = 0; i < 90; i++) {
      const x = (random() - 0.5) * 34,
        z = (random() - 0.5) * 22;
      if (
        !map.platforms.some(
          (p) =>
            !p.bridge &&
            Math.abs(x - p.x) < p.hx - 0.5 &&
            Math.abs(z - p.z) < p.hz - 0.5,
        )
      )
        continue;
      if (Math.abs(x) < 14 && Math.abs(z) < 8) continue;
      const h = 0.12 + random() * 0.25;
      const grass = this.mesh(
        new THREE.ConeGeometry(0.1, h, 3),
        this.mat(random() > 0.5 ? 0x8caa74 : 0xa2b984),
      );
      grass.position.set(x, heightAt(id, x, z) + h / 2, z);
      this.terrain.add(grass);
    }
    for (const [x, z] of [
      [-16.5, -10.6],
      [16.2, 10.4],
      [-16.5, 9.8],
      [16.4, -10],
    ]) {
      if (id === "island") continue;
      this.addBox(this.terrain, [0.17, 1.3, 0.17], [x, 0.65, z], 0x8d795a);
      const tree = this.mesh(
        new THREE.IcosahedronGeometry(0.95, 0),
        this.mat(0x809466),
      );
      tree.scale.y = 1.2;
      tree.position.set(x, 1.8, z);
      this.terrain.add(tree);
    }
    for (const team of ["red", "blue"]) {
      const sign = team === "red" ? -1 : 1,
        x = sign * (id === "island" ? 13.7 : 16.4);
      this.addBox(
        this.terrain,
        [0.06, 3.1, 0.06],
        [x, 1.55, -(id === "island" ? 8.5 : 10.3)],
        0x6e7b62,
      );
      const flag = this.mesh(
        new THREE.PlaneGeometry(1.25, 0.7, 10, 3),
        this.mat(PALETTE[team], { side: THREE.DoubleSide }),
      );
      flag.position.set(x + 0.6, 2.65, -(id === "island" ? 8.5 : 10.3));
      this.terrain.add(flag);
      this.flags.push(flag);
      const pts = [];
      const maxX = id === "island" ? 14 : 17,
        maxZ = id === "island" ? 9 : 11;
      for (let a = 3; a <= maxX; a++)
        for (let z = -maxZ; z < maxZ; z++) {
          const h1 = heightAt(id, sign * a, z),
            h2 = heightAt(id, sign * a, z + 1);
          if (Number.isFinite(h1) && Number.isFinite(h2))
            pts.push(
              new THREE.Vector3(sign * a, h1 + 0.035, z),
              new THREE.Vector3(sign * a, h2 + 0.035, z + 1),
            );
        }
      for (let z = -maxZ; z <= maxZ; z++)
        for (let a = 3; a < maxX; a++) {
          const h1 = heightAt(id, sign * a, z),
            h2 = heightAt(id, sign * (a + 1), z);
          if (Number.isFinite(h1) && Number.isFinite(h2))
            pts.push(
              new THREE.Vector3(sign * a, h1 + 0.035, z),
              new THREE.Vector3(sign * (a + 1), h2 + 0.035, z),
            );
        }
      const grid = new THREE.LineSegments(
        new THREE.BufferGeometry().setFromPoints(pts),
        new THREE.LineBasicMaterial({
          color: PALETTE[team],
          transparent: true,
          opacity: 0.12,
        }),
      );
      this.overlay.add(grid);
      this.grid.push(grid);
    }
    const center = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(0, 0.04, -11),
        new THREE.Vector3(0, 0.04, 11),
      ]),
      new THREE.LineDashedMaterial({
        color: 0xe9ead8,
        dashSize: 0.3,
        gapSize: 0.25,
        transparent: true,
        opacity: 0.8,
      }),
    );
    center.computeLineDistances();
    if (id !== "bridge") this.terrain.add(center);
  }
  clearActors() {
    this.actors.clear();
    this.models.clear();
    this.dolls.clear();
    this.shots.clear();
    this.bursts.clear();
    this.propModels.clear();
  }
  soldier(type, team) {
    const group = new THREE.Group(),
      s = UNITS[type].scale,
      color = PALETTE[team];
    group.scale.setScalar(s);
    const torso = this.addBox(group, [0.47, 0.63, 0.32], [0, 0, 0], color);
    const belt = this.addBox(
      group,
      [0.49, 0.08, 0.34],
      [0, -0.2, 0],
      PALETTE.dark,
    );
    const head = this.mesh(this.ball(0.22), this.mat(PALETTE.skin));
    head.position.y = 0.53;
    group.add(head);
    if (["spear", "shield", "giant"].includes(type)) {
      const helmet = this.mesh(
        this.geo(
          "helmet",
          () =>
            new THREE.SphereGeometry(
              0.23,
              10,
              6,
              0,
              Math.PI * 2,
              0,
              Math.PI * 0.6,
            ),
        ),
        this.mat(PALETTE.metal),
      );
      helmet.position.y = 0.57;
      group.add(helmet);
      this.addBox(group, [0.07, 0.2, 0.26], [0, 0.83, 0], color);
    } else if (type === "archer") {
      const hat = this.mesh(
        this.geo("hat", () => new THREE.ConeGeometry(0.31, 0.2, 8)),
        this.mat(color),
      );
      hat.position.y = 0.76;
      group.add(hat);
    }
    const armL = this.addBox(group, [0.2, 0.6, 0.21], [-0.36, -0.02, 0], color),
      armR = this.addBox(group, [0.2, 0.6, 0.21], [0.36, -0.02, 0], color);
    const legL = this.addBox(
        group,
        [0.23, 0.6, 0.26],
        [-0.15, -0.57, 0],
        PALETTE.dark,
      ),
      legR = this.addBox(
        group,
        [0.23, 0.6, 0.26],
        [0.15, -0.57, 0],
        PALETTE.dark,
      );
    const weapon = new THREE.Group();
    group.add(weapon);
    if (type === "shield") {
      const shield = this.mesh(
        this.geo(
          "shield",
          () => new THREE.CylinderGeometry(0.43, 0.43, 0.1, 6),
        ),
        this.mat(0xdec79a),
      );
      shield.rotation.x = Math.PI / 2;
      shield.position.set(-0.23, 0.02, 0.36);
      weapon.add(shield);
      const boss = this.mesh(this.ball(0.11), this.mat(PALETTE.metal));
      boss.position.set(-0.23, 0.02, 0.44);
      weapon.add(boss);
      this.addBox(
        weapon,
        [0.09, 0.09, 0.8],
        [0.38, -0.05, 0.45],
        PALETTE.metal,
      );
    } else if (type === "spear") {
      this.addBox(
        weapon,
        [0.055, 0.055, 2.4],
        [0.36, 0.12, 0.65],
        PALETTE.wood,
      );
      const tip = this.mesh(
        this.geo("spearTip", () => new THREE.ConeGeometry(0.11, 0.4, 4)),
        this.mat(0xd0d2bb),
      );
      tip.rotation.x = Math.PI / 2;
      tip.position.set(0.36, 0.12, 2);
      weapon.add(tip);
    } else if (type === "archer") {
      const bow = this.mesh(
        this.geo(
          "bow",
          () => new THREE.TorusGeometry(0.42, 0.025, 3, 12, Math.PI),
        ),
        this.mat(0x886a45),
      );
      bow.rotation.z = -Math.PI / 2;
      bow.scale.y = 1.3;
      bow.position.set(0.37, 0.12, 0.3);
      weapon.add(bow);
      this.addBox(weapon, [0.4, 0.5, 0.22], [0, 0, -0.32], 0x96835f);
    } else if (type === "bomber") {
      const bomb = this.mesh(this.ball(0.23), this.mat(0x414d43));
      bomb.position.set(0.4, 0.1, 0.23);
      weapon.add(bomb);
      this.addBox(weapon, [0.05, 0.15, 0.05], [0.4, 0.4, 0.23], 0xd4a454);
      this.addBox(weapon, [0.49, 0.1, 0.35], [0, 0.7, 0], 0x6d795d);
    } else if (type === "giant") {
      this.addBox(weapon, [0.09, 1.4, 0.09], [0.4, 0.1, 0.25], PALETTE.wood);
      this.addBox(weapon, [0.75, 0.38, 0.4], [0.4, 0.8, 0.25], 0x7d857a);
    } else if (type === "crossbow") {
      this.addBox(weapon, [0.85, 0.09, 0.1], [0.25, 0.12, 0.45], PALETTE.wood);
      this.addBox(weapon, [0.1, 0.12, 0.75], [0.25, 0.12, 0.45], PALETTE.metal);
      this.addBox(group, [0.5, 0.14, 0.36], [0, 0.73, 0], PALETTE.dark);
    } else if (type === "medic") {
      this.addBox(group, [0.54, 0.65, 0.35], [0, 0, 0], 0xe2e5d4);
      this.addBox(group, [0.27, 0.08, 0.025], [0, 0.1, 0.19], color);
      this.addBox(group, [0.08, 0.27, 0.03], [0, 0.1, 0.2], color);
      this.addBox(weapon, [0.28, 0.37, 0.25], [0.4, -0.12, 0.08], 0xd3d1b8);
      this.addBox(group, [0.5, 0.12, 0.36], [0, 0.73, 0], 0xd5dfc3);
    } else if (type === "cavalry") {
      for (const part of [...group.children])
        if (part !== weapon) part.position.y += 0.5;
      const horse = this.addBox(
        group,
        [0.6, 0.48, 1.15],
        [0, -0.05, 0],
        0x8f7858,
      );
      this.addBox(group, [0.3, 0.4, 0.35], [0, 0.28, 0.55], 0xa38a65);
      this.addBox(group, [0.18, 0.26, 0.17], [-0.13, 0.55, 0.55], 0x6a604a);
      this.addBox(group, [0.18, 0.26, 0.17], [0.13, 0.55, 0.55], 0x6a604a);
      for (const x of [-0.22, 0.22])
        for (const z of [-0.4, 0.4])
          this.addBox(group, [0.13, 0.6, 0.14], [x, -0.53, z], 0x615b48);
      this.addBox(weapon, [0.07, 0.07, 1.9], [0.35, 0.64, 0.8], PALETTE.metal);
      this.addBox(group, [0.65, 0.13, 0.5], [0, 0.25, -0.05], color);
      group.userData.horse = horse;
    } else if (type === "catapult") {
      this.addBox(group, [0.9, 0.14, 1.25], [0, -0.4, 0], 0x927552);
      const wheelGeo = this.geo(
        "wheel",
        () => new THREE.CylinderGeometry(0.28, 0.28, 0.12, 10),
      );
      for (const x of [-0.55, 0.55])
        for (const z of [-0.4, 0.4]) {
          const wheel = this.mesh(wheelGeo, this.mat(0x645b43));
          wheel.rotation.z = Math.PI / 2;
          wheel.position.set(x, -0.48, z);
          group.add(wheel);
        }
      this.addBox(weapon, [0.12, 1.4, 0.12], [0, 0.05, 0.12], 0x856847);
      this.addBox(weapon, [0.5, 0.16, 0.45], [0, 0.8, 0.12], 0xb4a07a);
      this.addBox(group, [0.12, 0.9, 0.12], [-0.4, 0, 0.1], PALETTE.wood);
      this.addBox(group, [0.12, 0.9, 0.12], [0.4, 0, 0.1], PALETTE.wood);
    } else
      this.addBox(weapon, [0.11, 0.15, 0.85], [0.36, -0.02, 0.45], 0xa8956d);
    const ring = this.mesh(
      this.geo("unitRing", () => new THREE.RingGeometry(0.32, 0.37, 24)),
      this.mat(color, {
        transparent: true,
        opacity: 0.55,
        side: THREE.DoubleSide,
      }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = -0.77;
    group.add(ring);
    const health = new THREE.Group();
    const bg = this.mesh(this.box(0.65, 0.055, 0.015), this.mat(0x687360));
    const fill = this.mesh(this.box(0.65, 0.06, 0.02), this.mat(0xdde7b7));
    health.add(bg, fill);
    health.position.y = 1.05;
    health.visible = false;
    group.add(health);
    group.userData = {
      armL,
      armR,
      legL,
      legR,
      weapon,
      health,
      fill,
      ring,
      type,
      team,
    };
    return group;
  }
  deployment(config) {
    this.clearActors();
    for (const team of ["red", "blue"])
      config[team].forEach((u, i) => {
        const m = this.soldier(u.type, team);
        m.position.set(
          u.x,
          heightAt(config.map, u.x, u.z) + 0.8 * UNITS[u.type].scale,
          u.z,
        );
        m.rotation.y = team === "red" ? Math.PI / 2 : -Math.PI / 2;
        this.actors.add(m);
        this.models.set(`${team}-${i}`, m);
      });
    this.grid.forEach((g) => (g.visible = true));
    this.updateProps(
      [...MAPS[config.map].props, ...(config.props || [])].map((p, i) => ({
        ...p,
        id: "deploy-prop-" + i,
        floor: heightAt(config.map, p.x, p.z),
        team: p.team || "neutral",
        alive: true,
      })),
    );
  }
  propModel(type, team = "neutral") {
    const root = new THREE.Group(),
      wood = 0x99794f,
      color = PALETTE[team] || 0x859f63;
    const ring = (radius, shade, opacity = 0.35) => {
      const m = this.mesh(
        this.geo(
          "propRing" + radius,
          () => new THREE.RingGeometry(radius - 0.055, radius, 32),
        ),
        this.mat(shade, { transparent: true, opacity, side: THREE.DoubleSide }),
      );
      m.rotation.x = -Math.PI / 2;
      m.position.y = 0.04;
      m.castShadow = false;
      root.add(m);
      return m;
    };
    if (type === "barrel") {
      const barrel = this.mesh(
        this.geo(
          "barrel",
          () => new THREE.CylinderGeometry(0.4, 0.43, 1.1, 10),
        ),
        this.mat(wood),
      );
      barrel.position.y = 0.55;
      root.add(barrel);
      for (const y of [0.2, 0.82]) {
        const band = this.mesh(
          this.geo(
            "barrelBand",
            () => new THREE.TorusGeometry(0.425, 0.045, 4, 10),
          ),
          this.mat(0x626654),
        );
        band.rotation.x = Math.PI / 2;
        band.position.y = y;
        root.add(band);
      }
      for (const angle of [-0.65, 0.65]) {
        const danger = this.addBox(
          root,
          [0.1, 0.52, 0.03],
          [0, 0.56, 0.43],
          0xc76c43,
        );
        danger.rotation.z = angle;
      }
      this.addBox(root, [0.06, 0.18, 0.06], [0, 1.17, 0], 0xe5c268);
      ring(0.68, 0xbe734b);
    } else if (type === "barricade") {
      for (const z of [-0.8, 0, 0.8])
        for (const a of [-0.55, 0.55]) {
          const beam = this.addBox(
            root,
            [0.16, 1.45, 0.17],
            [0, 0.67, z],
            wood,
          );
          beam.rotation.z = a;
        }
      this.addBox(root, [0.18, 0.14, 2.6], [0.14, 0.8, 0], 0x6d6e55);
      ring(1.3, color, 0.2);
    } else if (type === "spikes") {
      this.addBox(root, [1.7, 0.1, 1.7], [0, 0.05, 0], 0x7d7658);
      for (let x = -1; x <= 1; x++)
        for (let z = -1; z <= 1; z++) {
          const spike = this.mesh(
            this.geo("spike", () => new THREE.ConeGeometry(0.13, 0.55, 4)),
            this.mat(0xc0bea1),
          );
          spike.position.set(x * 0.5, 0.35, z * 0.5);
          root.add(spike);
        }
      ring(1.15, 0xc18b54);
    } else if (type === "spring") {
      this.addBox(root, [1.6, 0.1, 1.6], [0, 0.05, 0], 0x657c6b);
      for (const y of [0.13, 0.23, 0.33]) {
        const coil = this.mesh(
          this.geo("coil", () => new THREE.TorusGeometry(0.4, 0.035, 4, 12)),
          this.mat(0xd0d3ba),
        );
        coil.rotation.x = Math.PI / 2;
        coil.position.y = y;
        root.add(coil);
      }
      this.addBox(root, [1.4, 0.1, 1.4], [0, 0.43, 0], 0x82a69d);
      this.addBox(root, [0.1, 0.02, 0.7], [0, 0.5, 0], 0xe8edcd);
      for (const a of [-0.7, 0.7]) {
        const arrow = this.addBox(
          root,
          [0.1, 0.02, 0.43],
          [a > 0 ? 0.13 : -0.13, 0.5, 0.22],
          0xe8edcd,
        );
        arrow.rotation.y = a;
      }
      ring(1.02, 0x91bebb);
    } else if (type === "banner") {
      this.addBox(root, [0.08, 2.2, 0.08], [0, 1.1, 0], wood);
      this.addBox(root, [1, 0.6, 0.04], [0.45, 1.8, 0], color);
      this.addBox(root, [0.38, 0.09, 0.06], [0.45, 1.8, 0.03], 0xf0edda);
      this.addBox(root, [0.09, 0.38, 0.06], [0.45, 1.8, 0.03], 0xf0edda);
      ring(3.2, 0xa1bd7a, 0.23);
      ring(0.45, color);
    }
    root.userData = { type };
    return root;
  }
  updateProps(props) {
    const ids = new Set(props.filter((p) => p.alive).map((p) => p.id));
    for (const [id, m] of this.propModels)
      if (!ids.has(id)) {
        this.actors.remove(m);
        this.propModels.delete(id);
      }
    for (const p of props)
      if (p.alive) {
        let m = this.propModels.get(p.id);
        if (!m) {
          m = this.propModel(p.type, p.team);
          this.actors.add(m);
          this.propModels.set(p.id, m);
        }
        m.position.set(p.x, p.floor, p.z);
        m.scale.setScalar(p.active && p.type === "barrel" ? 1.04 : 1);
      }
  }
  setTransform(object, t) {
    object.position.set(t[0], t[1], t[2]);
    object.quaternion.set(t[3], t[4], t[5], t[6]);
  }
  frame(state) {
    this.grid.forEach((g) => (g.visible = false));
    this.ghost.visible = false;
    this.updateProps(state.props || []);
    for (const [key, m] of this.models)
      if (typeof key === "string") {
        this.actors.remove(m);
        this.models.delete(key);
      }
    for (const u of state.units) {
      let m = this.models.get(u.id);
      if (!u.alive) {
        if (m) {
          this.actors.remove(m);
          this.models.delete(u.id);
        }
        continue;
      }
      if (!m) {
        m = this.soldier(u.type, u.team);
        this.actors.add(m);
        this.models.set(u.id, m);
      }
      this.setTransform(m, u.t);
      m.rotation.y = u.angle;
      const a = m.userData,
        walk = Math.sin(state.time * 11 + u.id) * 0.35;
      a.legL.rotation.x = walk;
      a.legR.rotation.x = -walk;
      a.armL.rotation.x = -walk * 0.5;
      a.armR.rotation.x = walk * 0.5 - u.attack * 0.8;
      a.weapon.rotation.x = -u.attack * 0.7;
      a.health.visible = u.hp < UNITS[u.type].hp;
      a.fill.scale.x = Math.max(0.01, u.hp / UNITS[u.type].hp);
      a.health.quaternion
        .copy(this.camera.quaternion)
        .premultiply(m.quaternion.clone().invert());
    }
    const dollIds = new Set(state.ragdolls.map((d) => d.id));
    for (const [id, m] of this.dolls)
      if (!dollIds.has(id)) {
        this.actors.remove(m);
        this.dolls.delete(id);
      }
    for (const d of state.ragdolls) {
      let root = this.dolls.get(d.id);
      if (!root) {
        root = new THREE.Group();
        const s = UNITS[d.type].scale;
        for (const p of d.parts) {
          const head = p.name === "head",
            torso = p.name === "torso",
            arm = p.name.startsWith("arm");
          const size = head
            ? [0.4, 0.4, 0.4]
            : torso
              ? [0.46, 0.64, 0.32]
              : arm
                ? [0.22, 0.6, 0.22]
                : [0.24, 0.6, 0.26];
          const material = this.mat(
            head ? PALETTE.skin : torso || arm ? PALETTE[d.team] : PALETTE.dark,
          );
          const mesh = this.mesh(
            head ? this.ball(0.2 * s) : this.box(...size.map((v) => v * s)),
            material,
          );
          mesh.name = p.name;
          root.add(mesh);
        }
        this.actors.add(root);
        this.dolls.set(d.id, root);
      }
      for (const p of d.parts)
        this.setTransform(root.getObjectByName(p.name), p.t);
    }
    const shotIds = new Set(state.projectiles.map((p) => p.id));
    for (const [id, m] of this.shots)
      if (!shotIds.has(id)) {
        this.actors.remove(m);
        this.shots.delete(id);
      }
    for (const p of state.projectiles) {
      let m = this.shots.get(p.id);
      if (!m) {
        m = this.mesh(
          p.type === "bomb"
            ? this.ball(0.18)
            : p.type === "stone"
              ? this.ball(0.28)
              : this.box(0.045, p.type === "bolt" ? 0.45 : 0.65, 0.045),
          this.mat(
            p.type === "bomb"
              ? 0x424e42
              : p.type === "stone"
                ? 0x939782
                : 0x715b3a,
          ),
        );
        this.actors.add(m);
        this.shots.set(p.id, m);
      }
      this.setTransform(m, p.t);
      if (p.type === "arrow" || p.type === "bolt") {
        const direction = new THREE.Vector3(p.v.x, p.v.y, p.v.z).normalize();
        m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction);
      }
    }
    const effectIds = new Set(state.effects.map((e) => e.id));
    for (const [id, m] of this.bursts)
      if (!effectIds.has(id)) {
        this.actors.remove(m);
        this.bursts.delete(id);
      }
    for (const e of state.effects) {
      let m = this.bursts.get(e.id);
      if (!m) {
        m = this.mesh(
          this.ball(1),
          new THREE.MeshBasicMaterial({
            color:
              e.type === "heal"
                ? 0xaaca8b
                : e.type === "spring"
                  ? 0xa4d3cd
                  : e.type === "explosion"
                    ? 0xf5cb70
                    : 0xe8d6a1,
            transparent: true,
            opacity: 0.5,
            wireframe: e.type === "slam",
          }),
        );
        m.material.userData.transient = true;
        m.castShadow = false;
        this.actors.add(m);
        this.bursts.set(e.id, m);
      }
      const progress = 1 - e.life / e.maxLife;
      m.position.set(e.x, e.y, e.z);
      m.scale.setScalar(
        0.2 +
          progress *
            (e.type === "heal" ? 0.7 : e.type === "explosion" ? 3.6 : 2),
      );
      m.material.opacity = (1 - progress) * 0.65;
    }
  }
  pick(clientX, clientY) {
    const r = this.renderer.domElement.getBoundingClientRect();
    this.pointer.set(
      ((clientX - r.left) / r.width) * 2 - 1,
      (-(clientY - r.top) / r.height) * 2 + 1,
    );
    this.ray.setFromCamera(this.pointer, this.camera);
    const hits = this.ray.intersectObjects(this.groundMeshes, false);
    if (hits.length) return hits[0].point;
    const point = new THREE.Vector3();
    return this.ray.ray.intersectPlane(this.plane, point);
  }
  hover(point, valid, radius = 0.48) {
    this.ghost.visible = !!point;
    if (point) {
      this.ghost.scale.setScalar(radius / 0.48);
      const floor = heightAt(this.mapId, point.x, point.z);
      this.ghost.position.set(
        point.x,
        Number.isFinite(floor) ? floor + 0.08 : 0.08,
        point.z,
      );
      this.ghost.material.color.setHex(valid ? 0xf5f4d5 : 0xba624e);
    }
  }
  render(time) {
    const delta = this.lastRenderTime ? time - this.lastRenderTime : 1 / 60;
    this.lastRenderTime = time;
    if (delta > 0 && delta < 0.5)
      this.metrics.fps =
        this.metrics.fps * 0.95 + Math.min(144, 1 / delta) * 0.05;
    for (const flag of this.flags) {
      const pos = flag.geometry.attributes.position;
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i);
        pos.setZ(i, Math.sin(x * 4 + time * 3) * (x + 0.65) * 0.08);
      }
      pos.needsUpdate = true;
    }
    this.controls.update();
    this.actorBatches.sync();
    this.renderer.render(this.scene, this.camera);
    this.metrics.calls = this.renderer.info.render.calls;
    this.metrics.meshes = this.actorBatches.meshCount;
    this.metrics.batches = this.actorBatches.batchCount;
    this.container.dataset.drawCalls = this.metrics.calls;
    this.container.dataset.actorMeshes = this.metrics.meshes;
    this.container.dataset.actorBatches = this.metrics.batches;
  }
}
