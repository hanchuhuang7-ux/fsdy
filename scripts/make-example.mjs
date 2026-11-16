import fs from "node:fs/promises";
import { defaultConfig, formation } from "../src/data.js";
const config = defaultConfig("bridge");
config.red = formation("volley");
config.blue = formation("titans", "blue");
await fs.mkdir(new URL("../examples/", import.meta.url), { recursive: true });
await fs.writeFile(
  new URL("../examples/bridge-battle.json", import.meta.url),
  JSON.stringify(config, null, 2) + "\n",
);
const highland = defaultConfig("highland");
highland.red = formation("cavalry");
highland.blue = formation("siege", "blue");
highland.props = [
  { type: "spikes", team: "red", x: -3.5, z: 1.5 },
  { type: "banner", team: "red", x: -11, z: -6 },
  { type: "barrel", team: "blue", x: 3.5, z: 1.5 },
];
await fs.writeFile(
  new URL("../examples/highland-skirmish.json", import.meta.url),
  JSON.stringify(highland, null, 2) + "\n",
);
const types = ["militia", "shield", "spear", "archer", "bomber"];
const army = (team) =>
  Array.from({ length: 45 }, (_, i) => ({
    type: types[i % 5],
    x: (team === "red" ? -1 : 1) * (4 + Math.floor(i / 9) * 2.7),
    z: ((i % 9) - 4) * 2,
    stance: "advance",
  }));
const large = {
  ...defaultConfig(),
  budget: 10000,
  seed: 42,
  red: army("red"),
  blue: army("blue"),
};
await fs.writeFile(
  new URL("../examples/large-battle.json", import.meta.url),
  JSON.stringify(large, null, 2) + "\n",
);
