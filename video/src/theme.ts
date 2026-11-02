/** Shared look for both compositions. Palette mirrors the app's Theme.xaml and the landing page. */
export const COLORS = {
  bg0: "#0E1116",
  bg1: "#1B2230",
  dock: "rgba(20,21,26,0.86)",
  dockBorder: "rgba(255,255,255,0.14)",
  highlight: "rgba(255,255,255,0.10)",
  accent: "#3DDC97",
  text: "#F5F5F7",
  sub: "rgba(255,255,255,0.6)",
  activeDot: "#6CA8FF",
};

export const FONT = "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif";

export type App = { tip: string; c1: string; c2: string; running?: boolean; active?: boolean; glyph?: string };

export const APPS: App[] = [
  { tip: "Files", c1: "#ffd56b", c2: "#f5a623" },
  { tip: "Chrome", c1: "#5ec6ff", c2: "#4c9bff", running: true },
  { tip: "Code", c1: "#67e8f9", c2: "#2b9fd8", running: true, active: true },
  { tip: "Mail", c1: "#a0e7a0", c2: "#3ddc97" },
  { tip: "Music", c1: "#ff9ad1", c2: "#e45bb0", running: true },
  { tip: "Notes", c1: "#c9b6ff", c2: "#8a7bff" },
];
