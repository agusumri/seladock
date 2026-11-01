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

const main = async () => {
  console.log("Bundling...");
  const serveUrl = await bundle({
    entryPoint: path.join(__dirname, "src/index.ts"),
    onProgress: (p) => process.stdout.write(`\rbundle ${p}%   `),
  });
  console.log("\nBundled.");

  const composition = await selectComposition({
    serveUrl,
    id: "SelaDock",
    chromiumOptions,
    browserExecutable,
  });
  console.log(`Rendering ${composition.durationInFrames} frames...`);

  const outPath = path.join(__dirname, "../docs/assets/media/seladock.mp4");
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
