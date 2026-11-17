import readline from "node:readline";
const input = readline.createInterface({
  input: process.stdin,
  crlfDelay: Infinity,
});
for await (const line of input) {
  try {
    const o = JSON.parse(line),
      own = o.units.filter((u) => u.team === o.team),
      front = own.sort(
        (a, b) => (o.team === "red" ? b.x - a.x : a.x - b.x) || a.id - b.id,
      )[0];
    const ready =
      front &&
      o.commander.enabled &&
      o.commander.points[o.team] > 0 &&
      o.tick >= o.commander.readyAt[o.team];
    console.log(
      JSON.stringify({
        schema: "frontline-agent@1",
        kind: "command",
        requestId: o.requestId,
        team: o.team,
        order: ready
          ? {
              type: front.hp / front.maxHp < 0.7 ? "fortify" : "inspire",
              x: front.x,
              z: front.z,
            }
          : null,
      }),
    );
  } catch {
    /* stdout is reserved for complete JSON responses. */
  }
}
