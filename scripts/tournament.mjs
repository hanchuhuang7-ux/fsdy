import fs from "node:fs/promises";
import path from "node:path";
import { runTournament } from "../src/tournament.js";
const args = process.argv.slice(2),
  options = {};
try {
  for (let i = 0; i < args.length; i += 2) {
    if (!["--config", "--out"].includes(args[i]) || !args[i + 1])
      throw Error(
        "用法：npm run tournament -- --config tournament.json --out ranking.json",
      );
    options[args[i].slice(2)] = args[i + 1];
  }
  const spec = options.config
    ? JSON.parse(await fs.readFile(options.config, "utf8"))
    : {
        entrants: ["guardian", "maneuver", "counter", "oracle"].map((ai) => ({
          name: ai,
          ai,
        })),
        maps: ["meadow"],
        seeds: [42],
        budget: 2200,
      };
  const baseDir = options.config
    ? path.dirname(path.resolve(options.config))
    : process.cwd();
  spec.entrants = await Promise.all(
    spec.entrants.map(async (entrant) => {
      if (!entrant.file && !entrant.favoriteFile) return entrant;
      const file = path.resolve(baseDir, entrant.file || entrant.favoriteFile),
        stat = await fs.stat(file);
      if (stat.size > 1024 * 1024) throw Error("阵型 / 收藏文件须小于 1 MB");
      const raw = JSON.parse(await fs.readFile(file, "utf8"));
      if (entrant.favoriteFile) {
        const slots = Array.isArray(raw) ? raw : raw.slots;
        if (
          !Number.isInteger(entrant.slot) ||
          entrant.slot < 0 ||
          entrant.slot > 4 ||
          !slots?.[entrant.slot]
        )
          throw Error("收藏槽位无效");
        return {
          ...slots[entrant.slot],
          name: entrant.name || slots[entrant.slot].name,
        };
      }
      const team = entrant.team || "red";
      return {
        name: entrant.name || path.basename(file),
        team,
        map: raw.map || entrant.map,
        units: Array.isArray(raw) ? raw : raw.units || raw[team],
        props: (raw.props || []).filter((p) => !p.team || p.team === team),
      };
    }),
  );
  const result = await runTournament(spec, {
    onProgress: (p) => {
      if (p.phase === "完成") console.error(p.completed + "/" + p.total);
    },
  });
  if (options.out)
    await fs.writeFile(options.out, JSON.stringify(result, null, 2) + "\n");
  console.log(
    JSON.stringify(
      { ranking: result.ranking, matches: result.matches.length },
      null,
      2,
    ),
  );
} catch (e) {
  console.error(e.message);
  process.exitCode = 1;
}
