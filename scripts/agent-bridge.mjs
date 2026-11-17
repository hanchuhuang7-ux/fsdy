import { spawn } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Battle, initPhysics } from "../src/engine.js";
import { validateConfig, defaultConfig } from "../src/data.js";
import { BattleSeries } from "../src/series.js";
import { replayPackage } from "../src/replay.js";
import {
  observeBattle,
  acceptAgentCommand,
  OBSERVATION_INTERVAL,
} from "../src/agent-protocol.js";

/** Local trusted programs only. The game protocol grants orders, never arbitrary simulation mutations. */
export class StdioAgent {
  constructor(command, { cwd = process.cwd() } = {}) {
    if (
      !Array.isArray(command) ||
      !command.length ||
      command.some((x) => typeof x !== "string" || !x)
    )
      throw Error("Bot 命令须为非空字符串数组");
    this.child = spawn(command[0], command.slice(1), {
      cwd,
      shell: false,
      windowsHide: true,
      stdio: ["pipe", "pipe", "pipe"],
    });
    this.buffer = "";
    this.pending = null;
    this.closed = false;
    this.stderr = "";
    this.child.stdout.setEncoding("utf8");
    this.child.stdout.on("data", (s) => this.consume(s));
    this.child.stderr.on("data", (s) => {
      this.stderr = (this.stderr + s).slice(-2048);
    });
    this.child.stdin.on("error", () => this.fail("pipe"));
    this.child.on("error", () => this.fail("spawn"));
    this.child.on("exit", () => this.fail("exit"));
  }
  consume(s) {
    this.buffer += s;
    if (Buffer.byteLength(this.buffer) > 16384) {
      this.fail("oversize");
      this.child.kill();
      return;
    }
    let end;
    while ((end = this.buffer.indexOf("\n")) >= 0) {
      const line = this.buffer.slice(0, end);
      this.buffer = this.buffer.slice(end + 1);
      let data;
      try {
        data = JSON.parse(line);
      } catch {
        if (this.pending) this.resolve({ status: "invalid-json" });
        continue;
      }
      if (this.pending && data.requestId === this.pending.id)
        this.resolve({ status: "response", response: data });
    }
  }
  resolve(value) {
    const p = this.pending;
    if (!p) return;
    this.pending = null;
    clearTimeout(p.timer);
    p.resolve(value);
  }
  fail(reason) {
    this.closed = true;
    this.resolve({ status: reason });
  }
  request(observation, timeoutMs = 1000) {
    if (this.closed) return Promise.resolve({ status: "closed" });
    if (this.pending) throw Error("每个 Bot 只能有一个待处理观察");
    return new Promise((resolve) => {
      const timer = setTimeout(
        () => this.resolve({ status: "timeout" }),
        timeoutMs,
      );
      this.pending = { id: observation.requestId, resolve, timer };
      this.child.stdin.write(JSON.stringify(observation) + "\n");
    });
  }
  async close() {
    this.resolve({ status: "closed" });
    if (this.child.exitCode !== null || this.child.killed) return;
    this.child.stdin.end();
    this.child.kill();
    await new Promise((resolve) => {
      const timer = setTimeout(resolve, 500);
      this.child.once("exit", () => {
        clearTimeout(timer);
        resolve();
      });
    });
  }
}
export async function runAgentSeries({
  config = defaultConfig(),
  commands,
  timeoutMs = 1000,
  outDir = null,
  bo3 = true,
} = {}) {
  if (!Number.isInteger(timeoutMs) || timeoutMs < 5 || timeoutMs > 10000)
    throw Error("timeoutMs 须为 5–10000");
  await initPhysics();
  let c = validateConfig({
    ...config,
    commander: { enabled: true, aiTeams: [] },
  });
  const series = new BattleSeries(c),
    replays = [],
    diagnostics = [];
  const agents = {};
  try {
    for (const t of ["red", "blue"]) agents[t] = new StdioAgent(commands[t]);
    for (let round = 1; round <= (bo3 ? 3 : 1); round++) {
      if (round > 1) c = series.nextConfig();
      const b = new Battle(c),
        frames = [b.snapshot()];
      try {
        while (!b.result) {
          if (b.tick % OBSERVATION_INTERVAL === 0) {
            const teams = ["red", "blue"],
              requests = teams.map((t) =>
                observeBattle(b, t, `${round}:${b.tick}:${t}`, round),
              );
            const replies = await Promise.all(
              teams.map((t, i) => agents[t].request(requests[i], timeoutMs)),
            );
            // Simultaneous public observation, then stable red/blue application at the same integer tick.
            for (let i = 0; i < teams.length; i++) {
              const reply = replies[i],
                accepted =
                  reply.status === "response"
                    ? acceptAgentCommand(
                        b,
                        teams[i],
                        requests[i].requestId,
                        reply.response,
                      )
                    : { ok: false, reason: reply.status };
              diagnostics.push({
                round,
                tick: b.tick,
                team: teams[i],
                status: reply.status,
                ...accepted,
              });
            }
          }
          b.step();
          if (b.tick % 6 === 0) frames.push(b.snapshot());
        }
        if (b.tick % 6 !== 0) frames.push(b.snapshot());
        const replay = replayPackage(c, frames, b.result);
        replays.push(replay);
        series.record(c, b.result);
      } finally {
        b.dispose();
      }
      if (series.complete) break;
    }
  } finally {
    await Promise.all(Object.values(agents).map((a) => a.close()));
  }
  const report = {
    schema: "frontline-agent-series@1",
    series: series.export(),
    diagnostics,
  };
  if (outDir) {
    await fs.mkdir(outDir, { recursive: true });
    for (let i = 0; i < replays.length; i++)
      await fs.writeFile(
        path.join(outDir, `round-${i + 1}.replay.json`),
        JSON.stringify(replays[i]),
      );
    await fs.writeFile(
      path.join(outDir, "series.json"),
      JSON.stringify(report, null, 2),
    );
  }
  return { report, replays };
}
if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  try {
    const args = process.argv.slice(2),
      opts = {};
    for (let i = 0; i < args.length; i += 2) {
      if (
        ![
          "--red-bot",
          "--blue-bot",
          "--config",
          "--out-dir",
          "--timeout-ms",
        ].includes(args[i]) ||
        !args[i + 1]
      )
        throw Error("参数无效；Bot 命令使用 JSON 数组");
      opts[args[i].slice(2)] = args[i + 1];
    }
    const example = [
        process.execPath,
        fileURLToPath(new URL("./bots/minimal.mjs", import.meta.url)),
      ],
      config = opts.config
        ? JSON.parse(await fs.readFile(opts.config, "utf8"))
        : defaultConfig("ruins");
    const { report } = await runAgentSeries({
      config,
      commands: {
        red: opts["red-bot"] ? JSON.parse(opts["red-bot"]) : example,
        blue: opts["blue-bot"] ? JSON.parse(opts["blue-bot"]) : example,
      },
      timeoutMs: Number(opts["timeout-ms"] || 1000),
      outDir: opts["out-dir"] || "agent-results",
    });
    console.log(
      JSON.stringify(
        {
          rounds: report.series.rounds.length,
          wins: report.series.wins,
          winner: report.series.winner,
          acceptedOrders: report.diagnostics.filter((r) => r.entry).length,
          timeouts: report.diagnostics.filter((r) => r.status === "timeout")
            .length,
        },
        null,
        2,
      ),
    );
  } catch (e) {
    console.error(e.message);
    process.exitCode = 1;
  }
}
