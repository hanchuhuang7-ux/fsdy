import { defineConfig } from "vite";
import fs from "node:fs";
import path from "node:path";

// Emit the exact pinned compatibility WASM as an asset instead of expanding it into base64 JS.
// Node tests and historical simulators keep importing the unchanged installed dependency.
function externalRapierWasm() {
  return {
    name: "frontline-rapier-wasm",
    apply: "build",
    transform(code, id) {
      if (
        !id
          .replaceAll("\\", "/")
          .endsWith("/@dimforge/rapier3d-compat/dist/rapier.mjs")
      )
        return;
      const expression =
        /\w+\.toByteArray\("(AGFzbQ[A-Za-z0-9+/=]+)"\)\.buffer/;
      const match = code.match(expression);
      if (!match) throw Error("Pinned Rapier init wrapper changed");
      const binary = fs.readFileSync(
        path.join(path.dirname(id), "rapier_wasm3d_bg.wasm"),
      );
      if (!binary.equals(Buffer.from(match[1], "base64")))
        throw Error("Rapier binary differs from its embedded payload");
      const ref = this.emitFile({
        type: "asset",
        name: "rapier-0.21.0.wasm",
        source: binary,
      });
      return {
        code: code.replace(
          expression,
          `(yield (yield fetch(import.meta.ROLLUP_FILE_URL_${ref})).arrayBuffer())`,
        ),
        map: null,
      };
    },
  };
}
export default defineConfig({
  base: "./",
  plugins: [externalRapierWasm()],
  build: { target: "es2022" },
});
