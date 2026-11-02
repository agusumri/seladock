// Standalone render using the Remotion node API. More reliable in constrained/headless
// environments than the CLI: it reuses the pre-built bundle and sets explicit Chrome flags.
import { bundle } from "@remotion/bundler";
import { renderMedia, selectComposition } from "@remotion/renderer";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const chromiumOptions = { gl: "swiftshader" }; // software GL, no real GPU needed

// Use the system Chrome instead of Remotion's downloaded headless shell: in this environment the
// shell download fails to install, but a local Chrome renders frames just as well.
const browserExecutable = "C:/Program Files/Google/Chrome/Application/chrome.exe";

// `node render.mjs` renders the landscape clip; `node render.mjs reels` renders the vertical one.
const TARGETS = {
  landscape: { id: "SelaDock", out: "../docs/assets/media/seladock.mp4" },
  reels: { id: "SelaDockReels", out: "../docs/assets/media/seladock-reels.mp4" },
};

const targetName = process.argv[2] ?? "landscape";
const target = TARGETS[targetName];
if (!target) {
  console.error(`Unknown target "${targetName}". Use one of: ${Object.keys(TARGETS).join(", ")}`);
  process.exit(1);
}

const main = async () => {
  console.log("Bundling...");
  const serveUrl = await bundle({
    entryPoint: path.join(__dirname, "src/index.ts"),
    onProgress: (p) => process.stdout.write(`\rbundle ${p}%   `),
  });
  console.log("\nBundled.");

  const composition = await selectComposition({
    serveUrl,
    id: target.id,
    chromiumOptions,
    browserExecutable,
  });
  console.log(`Rendering ${composition.durationInFrames} frames...`);

  const outPath = path.join(__dirname, target.out);
  await renderMedia({
    composition,
    serveUrl,
    codec: "h264",
    outputLocation: outPath,
    concurrency: 1,
    chromiumOptions,
    browserExecutable,
    onProgress: ({ progress }) =>
      process.stdout.write(`\rrender ${Math.round(progress * 100)}%   `),
  });
  console.log(`\nDone: ${outPath}`);
};

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
