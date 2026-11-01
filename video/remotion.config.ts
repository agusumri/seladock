import { Config } from "@remotion/cli/config";

// Transparent-free, high-quality H.264 for the web. The landing page muxes this clip inline.
Config.setVideoImageFormat("jpeg");
Config.setOverwriteOutput(true);
// Keep the render footprint small so it completes reliably on constrained/sandboxed machines.
Config.setConcurrency(1);

