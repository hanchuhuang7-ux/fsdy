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
