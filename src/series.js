import { validateConfig, assertSafeData } from "./data.js";

export class BattleSeries {
  constructor(config) {
    this.initial = structuredClone(validateConfig(config));
    this.rounds = [];
    this.wins = { red: 0, blue: 0, draw: 0 };
  }
  get complete() {
    return this.wins.red >= 2 || this.wins.blue >= 2 || this.rounds.length >= 3;
  }
  get winner() {
    return !this.complete
      ? null
      : this.wins.red === this.wins.blue
        ? "draw"
        : this.wins.red > this.wins.blue
          ? "red"
          : "blue";
  }
  get last() {
    return this.rounds.at(-1) || null;
  }
  record(config, result) {
    assertSafeData(result);
    if (this.complete) throw Error("系列赛已结束");
    if (!result?.report || !["red", "blue", "draw"].includes(result.winner))
      throw Error("需要已完成的小局与公开战报");
    const normalized = validateConfig(config);
    if (
      normalized.map !== this.initial.map ||
      normalized.budget !== this.initial.budget ||
      normalized.commander.enabled !== this.initial.commander.enabled
    )
      throw Error("系列赛地图、军费与军令规则必须保持一致");
    if (this.rounds.some((r) => r.config.seed === normalized.seed))
      throw Error("该小局已经记分");
    this.rounds.push(structuredClone({ config: normalized, result }));
    this.wins[result.winner]++;
    return this.last;
  }
  nextConfig() {
    if (!this.last || this.complete) throw Error("没有待进行的下一局");
    return {
      ...structuredClone(this.last.config),
      seed: (this.initial.seed + this.rounds.length) >>> 0,
    };
  }
  export() {
    return structuredClone({
      format: "frontline-bo3",
      version: 1,
      initial: this.initial,
      rounds: this.rounds,
      wins: this.wins,
      winner: this.winner,
    });
  }
}
