import React from "react";
import {
  AbsoluteFill,
  interpolate,
  spring,
  useCurrentFrame,
  useVideoConfig,
  Easing,
} from "remotion";

/** Palette mirrors the app's Theme.xaml and the landing page. */
const COLORS = {
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

type App = { tip: string; c1: string; c2: string; running?: boolean; active?: boolean; glyph?: string };

const APPS: App[] = [
  { tip: "Files", c1: "#ffd56b", c2: "#f5a623" },
  { tip: "Chrome", c1: "#5ec6ff", c2: "#4c9bff", running: true },
  { tip: "Code", c1: "#67e8f9", c2: "#2b9fd8", running: true, active: true },
  { tip: "Mail", c1: "#a0e7a0", c2: "#3ddc97" },
  { tip: "Music", c1: "#ff9ad1", c2: "#e45bb0", running: true },
  { tip: "Notes", c1: "#c9b6ff", c2: "#8a7bff" },
];

const SYS = ["^", "\u{1F4F6}", "\u{1F509}", "\u{1F50B}"];

const ICON = 42; // rest icon size (px)
const GAP = 10;
const PLATE = Math.round(ICON * 1.22);

export const SelaDockScene: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps, width, height } = useVideoConfig();

  // Phase timeline (seconds -> frames)
  const t = (s: number) => s * fps;

  // 1) Dock slides up and fades in.
  const dockIn = spring({ frame: frame - t(0.4), fps, config: { damping: 18, mass: 0.7 } });
  const dockY = interpolate(dockIn, [0, 1], [120, 0]);

  // 2) Title fades in then out.
  const titleIn = interpolate(frame, [t(0.8), t(1.8)], [0, 1], { extrapolateRight: "clamp" });
  const titleOut = interpolate(frame, [t(3.4), t(4.2)], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const titleOpacity = Math.min(titleIn, titleOut);

  // 3) A cursor glides across the dock; icons magnify by proximity.
  // Compute dock geometry to place the cursor over successive icons.
  const dockLeftCount = 1 /*start*/ + APPS.length;
  const cellsForCursor = dockLeftCount; // cursor travels over start + apps
  const cursorStartX = width / 2 - 250;
  const cursorEndX = width / 2 + 60;
  const cursorProgress = interpolate(frame, [t(2.0), t(6.5)], [0, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.inOut(Easing.cubic),
  });
  const cursorX = interpolate(cursorProgress, [0, 1], [cursorStartX, cursorEndX]);
  const cursorY = interpolate(frame, [t(1.6), t(2.1)], [height, height - 70], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });

  // 4) Tray flyout opens near the end.
  const flyoutIn = spring({ frame: frame - t(7.6), fps, config: { damping: 16 } });
  const flyoutVisible = frame > t(7.3) && frame < t(11);

  // 5) Caption at the bottom.
  const capIn = interpolate(frame, [t(8.2), t(9)], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const capOut = interpolate(frame, [t(12.4), t(13.2)], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const capOpacity = Math.min(capIn, capOut);

  // 6) Dock gently slides down at the very end (auto-hide hint).
  const hide = interpolate(frame, [t(13), t(13.8)], [0, 70], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: Easing.in(Easing.cubic) });

  return (
    <AbsoluteFill
      style={{
        background: `radial-gradient(1100px 620px at 50% -120px, rgba(61,220,151,0.13), transparent 70%), linear-gradient(180deg, ${COLORS.bg0} 0%, ${COLORS.bg1} 55%, ${COLORS.bg0} 100%)`,
        fontFamily: "Segoe UI Variable Display, Segoe UI, system-ui, sans-serif",
        overflow: "hidden",
      }}
    >
      {/* faux desktop windows for depth */}
      <FauxWindows />

      {/* Title */}
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", paddingBottom: 160 }}>
        <div style={{ opacity: titleOpacity, textAlign: "center", transform: `translateY(${interpolate(titleOpacity, [0, 1], [16, 0])}px)` }}>
          <div style={{ color: COLORS.accent, letterSpacing: 4, fontSize: 15, fontWeight: 600, textTransform: "uppercase" }}>SelaDock</div>
          <div style={{ color: COLORS.text, fontSize: 56, fontWeight: 700, marginTop: 10 }}>A dock that replaces your taskbar</div>
          <div style={{ color: COLORS.sub, fontSize: 22, marginTop: 10 }}>Quiet, dark, and light on your PC.</div>
        </div>
      </AbsoluteFill>

      {/* Dock */}
      <AbsoluteFill style={{ justifyContent: "flex-end", alignItems: "center" }}>
        <div style={{ transform: `translateY(${dockY + hide}px)`, opacity: dockIn, marginBottom: 28, position: "relative" }}>
          {/* Tray flyout above the ^ button */}
          {flyoutVisible && <TrayFlyout progress={flyoutIn} />}
          <Dock frame={frame} fps={fps} cursorX={cursorX} />
        </div>
      </AbsoluteFill>

      {/* Caption */}
      <AbsoluteFill style={{ justifyContent: "flex-end", alignItems: "center", paddingBottom: 120 }}>
        <div style={{ opacity: capOpacity, color: COLORS.text, fontSize: 24, fontWeight: 600, background: "rgba(0,0,0,0.3)", padding: "10px 20px", borderRadius: 14, backdropFilter: "blur(6px)" }}>
          Pinned apps · running windows · tray · system info
        </div>
      </AbsoluteFill>

      {/* Cursor */}
      <Cursor x={cursorX} y={cursorY} visible={frame > t(1.7) && frame < t(7)} />
    </AbsoluteFill>
  );
};

const Dock: React.FC<{ frame: number; fps: number; cursorX: number }> = ({ cursorX }) => {
  const { width } = useVideoConfig();
  // Per-icon centers for magnification are approximated with a fixed step, since the dock row is
  // centered and the icons are uniform width.
  const step = ICON + GAP;
  const totalApps = APPS.length;
  // center of the apps block roughly at dock middle; compute each app center.
  const appsBlockWidth = totalApps * step;
  const appsStart = width / 2 - appsBlockWidth / 2;

  const magnify = (centerX: number) => {
    const d = Math.abs(cursorX - centerX);
    const m = Math.max(0, 1 - d / 120); // falloff within 120px
    return 1 + 0.28 * m;
  };

  return (
    <div
      style={{
        display: "flex",
        alignItems: "flex-end",
        gap: GAP,
        padding: "8px 12px",
        background: COLORS.dock,
        border: `1px solid ${COLORS.dockBorder}`,
        borderRadius: 20,
        boxShadow: "0 24px 60px -18px rgba(0,0,0,0.6)",
        backdropFilter: "blur(18px)",
      }}
    >
      <SysButton glyph={"\u229E"} />
      <Separator />
      {APPS.map((a, i) => {
        const centerX = appsStart + i * step + ICON / 2;
        const scale = magnify(centerX);
        return <AppIcon key={a.tip} app={a} scale={scale} />;
      })}
      <Separator />
      {SYS.map((g, i) => (
        <SysButton key={i} glyph={g} />
      ))}
      <Clock />
    </div>
  );
};

const AppIcon: React.FC<{ app: App; scale: number }> = ({ app, scale }) => {
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", width: PLATE, transform: `scale(${scale})`, transformOrigin: "bottom center" }}>
      <div
        style={{
          width: ICON,
          height: ICON,
          borderRadius: 11,
          background: `linear-gradient(145deg, ${app.c1}, ${app.c2})`,
          boxShadow: scale > 1.05 ? "0 6px 18px -4px rgba(0,0,0,0.5)" : "none",
        }}
      />
      <div
        style={{
          width: app.active ? 5 : 4,
          height: app.active ? 5 : 4,
          borderRadius: "50%",
          marginTop: 4,
          background: app.active ? COLORS.activeDot : app.running ? "rgba(255,255,255,0.9)" : "transparent",
        }}
      />
    </div>
  );
};

const SysButton: React.FC<{ glyph: string }> = ({ glyph }) => (
  <div
    style={{
      width: PLATE,
      height: PLATE,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      color: COLORS.text,
      fontSize: 20,
      borderRadius: 11,
    }}
  >
    {glyph}
  </div>
);

const Separator: React.FC = () => (
  <div style={{ width: 1, alignSelf: "stretch", margin: "6px 4px", background: "rgba(255,255,255,0.2)" }} />
);

const Clock: React.FC = () => (
  <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: "0 8px", justifyContent: "center" }}>
    <b style={{ color: COLORS.text, fontSize: 16, fontWeight: 600 }}>21:04</b>
    <i style={{ color: COLORS.sub, fontSize: 11, fontStyle: "normal" }}>Sun, 1 Nov</i>
  </div>
);

const TrayFlyout: React.FC<{ progress: number }> = ({ progress }) => {
  const apps = [
    { c1: "#ff9ad1", c2: "#e45bb0" },
    { c1: "#8af5c6", c2: "#3ddc97" },
    { c1: "#ffd56b", c2: "#f5a623" },
    { c1: "#9ad0ff", c2: "#4c9bff" },
  ];
  return (
    <div
      style={{
        position: "absolute",
        bottom: "110%",
        left: "58%",
        transform: `translateY(${interpolate(progress, [0, 1], [10, 0])}px)`,
        opacity: progress,
        display: "flex",
        gap: 6,
        padding: 8,
        background: "rgba(27,28,34,0.95)",
        border: "1px solid rgba(255,255,255,0.16)",
        borderRadius: 12,
        boxShadow: "0 18px 40px -12px rgba(0,0,0,0.6)",
      }}
    >
      {apps.map((a, i) => (
        <div key={i} style={{ width: 32, height: 32, borderRadius: 8, background: `linear-gradient(145deg, ${a.c1}, ${a.c2})` }} />
      ))}
    </div>
  );
};

const Cursor: React.FC<{ x: number; y: number; visible: boolean }> = ({ x, y, visible }) => {
  if (!visible) return null;
  return (
    <svg
      width={26}
      height={26}
      viewBox="0 0 24 24"
      style={{ position: "absolute", left: x, top: y, filter: "drop-shadow(0 2px 3px rgba(0,0,0,0.5))" }}
    >
      <path d="M5 3l14 8-6.2 1.6L10 19z" fill="#fff" stroke="#111" strokeWidth={1.4} strokeLinejoin="round" />
    </svg>
  );
};

const FauxWindows: React.FC = () => (
  <AbsoluteFill style={{ padding: 60, opacity: 0.5 }}>
    <div style={{ width: 520, height: 320, borderRadius: 14, background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.06)" }} />
  </AbsoluteFill>
);
