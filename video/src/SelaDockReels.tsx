import React from "react";
import {
  AbsoluteFill,
  Easing,
  Img,
  interpolate,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { APPS, COLORS, FONT } from "./theme";

/*
 * Vertical (1080x1920) cut for Reels / TikTok / Shorts.
 * Key text lives between y=250 and y=1500, clear of the platform overlays.
 * The dock sits in the lower-middle, its bottom edge above y=1500.
 */

const W = 1080;

// ---- Dock geometry (absolute, so the cursor can target exact items) ------------------------
const ICON = 52;
const PLATE = 64;
const GAP = 8;
const PAD_X = 14;
const PAD_Y = 10;
const SEP_W = 9;
const CLOCK_W = 110;

type Item =
  | { kind: "start"; w: number; tip: string }
  | { kind: "sep"; w: number }
  | { kind: "app"; w: number; app: (typeof APPS)[number] }
  | { kind: "sys"; w: number; icon: SysIcon; tip: string }
  | { kind: "clock"; w: number; tip: string };

type SysIcon = "caret" | "wifi" | "volume" | "battery";

const ITEMS_RAW: Item[] = [
  { kind: "start", w: PLATE, tip: "Start" },
  { kind: "sep", w: SEP_W },
  ...APPS.map((app) => ({ kind: "app" as const, w: PLATE, app })),
  { kind: "sep", w: SEP_W },
  { kind: "sys", w: PLATE, icon: "caret", tip: "Tray" },
  { kind: "sys", w: PLATE, icon: "wifi", tip: "Network" },
  { kind: "sys", w: PLATE, icon: "volume", tip: "Volume" },
  { kind: "sys", w: PLATE, icon: "battery", tip: "Battery" },
  { kind: "clock", w: CLOCK_W, tip: "Calendar" },
];

const ITEMS = (() => {
  let x = PAD_X;
  return ITEMS_RAW.map((it) => {
    const placed = { ...it, x };
    x += it.w + GAP;
    return placed;
  });
})();

const DOCK_W = ITEMS[ITEMS.length - 1].x + CLOCK_W + PAD_X;
const DOCK_H = PAD_Y * 2 + PLATE;
const DOCK_LEFT = Math.round((W - DOCK_W) / 2);
const DOCK_TOP = 1352; // bottom edge at 1436, above the 1500 safe line
const ITEM_BOTTOM = DOCK_TOP + PAD_Y + PLATE; // icons scale from here
const ICON_Y = DOCK_TOP + PAD_Y + ICON / 2;
const STRIP_TOP = DOCK_TOP - 16; // maximized windows stop here

const cx = (i: number) => DOCK_LEFT + ITEMS[i].x + ITEMS[i].w / 2;
const idxOf = (pred: (it: Item) => boolean) => ITEMS.findIndex(pred);
const START_I = idxOf((it) => it.kind === "start");
const FIRST_APP_I = idxOf((it) => it.kind === "app");
const LAST_APP_I = FIRST_APP_I + APPS.length - 1;
const sysI = (icon: SysIcon) => idxOf((it) => it.kind === "sys" && it.icon === icon);
const CLOCK_I = idxOf((it) => it.kind === "clock");

// ---- Tray flyout geometry ------------------------------------------------------------------
const FLY_ICON = 60;
const FLY_GAP = 12;
const FLY_PAD = 14;
const FLY_APPS = [
  { c1: "#ff9ad1", c2: "#e45bb0" },
  { c1: "#8af5c6", c2: "#3ddc97" },
  { c1: "#ffd56b", c2: "#f5a623" },
  { c1: "#9ad0ff", c2: "#4c9bff" },
];
const FLY_W = FLY_PAD * 2 + FLY_APPS.length * FLY_ICON + (FLY_APPS.length - 1) * FLY_GAP;
const FLY_H = FLY_PAD * 2 + FLY_ICON;
const FLY_LEFT = Math.round(Math.min(W - 40 - FLY_W, cx(sysI("caret")) - FLY_W / 2));
const FLY_TOP = DOCK_TOP - 22 - FLY_H;
const FLY_PICK = 1; // the app brought forward
const flyCx = (i: number) => FLY_LEFT + FLY_PAD + i * (FLY_ICON + FLY_GAP) + FLY_ICON / 2;
const FLY_CY = FLY_TOP + FLY_PAD + FLY_ICON / 2;

// ---- Windows -------------------------------------------------------------------------------
type Rect = { x: number; y: number; w: number; h: number };
const RESTORED: Rect = { x: 110, y: 820, w: 860, h: 420 };
const MAXED: Rect = { x: 0, y: 0, w: W, h: STRIP_TOP };
const FORWARD: Rect = { x: 150, y: 820, w: 780, h: 400 };
const TITLE_H = 64;
const MAX_BTN = { x: RESTORED.x + RESTORED.w - 110, y: RESTORED.y + TITLE_H / 2 };

// ---- Timeline (seconds) --------------------------------------------------------------------
const T = {
  taskbarOut: 1.1,
  dockIn: 1.35,
  maxClick: 6.6,
  windowOut: 8.9,
  trayClick: 9.7,
  pickClick: 10.6,
  forwardOut: 12.2,
  endCard: 15.0,
};
const CLICKS = [T.maxClick, T.trayClick, T.pickClick];

type Key = [t: number, x: number, y: number];
const CURSOR_KEYS: Key[] = [
  [2.3, cx(FIRST_APP_I) + 40, 1640],
  [2.9, cx(FIRST_APP_I), ICON_Y],
  [5.2, cx(LAST_APP_I), ICON_Y],
  [5.6, cx(LAST_APP_I), ICON_Y],
  [6.4, MAX_BTN.x, MAX_BTN.y],
  [6.75, MAX_BTN.x, MAX_BTN.y],
  [7.4, 760, 1150],
  [8.9, 760, 1150],
  [9.5, cx(sysI("caret")), ICON_Y],
  [9.85, cx(sysI("caret")), ICON_Y],
  [10.4, flyCx(FLY_PICK), FLY_CY],
  [10.75, flyCx(FLY_PICK), FLY_CY],
  [11.5, 700, 1270],
  [12.3, 700, 1270],
  [12.7, cx(START_I), ICON_Y],
  [12.95, cx(START_I), ICON_Y],
  [13.35, cx(sysI("wifi")), ICON_Y],
  [13.6, cx(sysI("wifi")), ICON_Y],
  [13.8, cx(sysI("volume")), ICON_Y],
  [14.05, cx(sysI("volume")), ICON_Y],
  [14.25, cx(sysI("battery")), ICON_Y],
  [14.5, cx(sysI("battery")), ICON_Y],
  [14.7, cx(CLOCK_I), ICON_Y],
  [15.0, cx(CLOCK_I), ICON_Y],
  [15.5, cx(CLOCK_I) + 60, 1700],
];
const CURSOR_ON: [number, number] = [2.3, 15.5];

const ease = Easing.inOut(Easing.cubic);
const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

const cursorAt = (s: number) => {
  const k = CURSOR_KEYS;
  if (s <= k[0][0]) return { x: k[0][1], y: k[0][2] };
  for (let i = 1; i < k.length; i++) {
    if (s <= k[i][0]) {
      const [t0, x0, y0] = k[i - 1];
      const [t1, x1, y1] = k[i];
      const p = ease((s - t0) / (t1 - t0));
      return { x: x0 + (x1 - x0) * p, y: y0 + (y1 - y0) * p };
    }
  }
  const last = k[k.length - 1];
  return { x: last[1], y: last[2] };
};

const lerpRect = (a: Rect, b: Rect, p: number): Rect => ({
  x: a.x + (b.x - a.x) * p,
  y: a.y + (b.y - a.y) * p,
  w: a.w + (b.w - a.w) * p,
  h: a.h + (b.h - a.h) * p,
});

// ============================================================================================

export const SelaDockReels: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = frame / fps;
  const f = (sec: number) => sec * fps;

  const cursor = cursorAt(s);
  const cursorVisible = s >= CURSOR_ON[0] && s <= CURSOR_ON[1];

  // Hook: a plain taskbar drops away and the dock rises in its place.
  const taskbarOut = interpolate(frame, [f(T.taskbarOut), f(T.taskbarOut + 0.5)], [0, 1], {
    ...clamp,
    easing: Easing.in(Easing.cubic),
  });
  const dockIn = spring({ frame: frame - f(T.dockIn), fps, config: { damping: 16, mass: 0.7 } });

  // Hover only counts while the pointer is over the dock band.
  const overDock = cursorVisible && Math.abs(cursor.y - ICON_Y) < 70;

  // Beat 3: window maximizes and stops above the dock.
  const winIn = interpolate(frame, [f(5.5), f(5.9)], [0, 1], clamp);
  const maxP = spring({ frame: frame - f(T.maxClick + 0.05), fps, config: { damping: 20, mass: 0.8 } });
  const winOut = interpolate(frame, [f(T.windowOut), f(T.windowOut + 0.4)], [0, 1], clamp);
  const winRect = lerpRect(RESTORED, MAXED, maxP);
  const edgeLine = Math.min(
    interpolate(frame, [f(7.2), f(7.6)], [0, 1], clamp),
    1 - winOut,
  );

  // Beat 4: tray flyout opens, one app comes forward.
  const flyIn = spring({ frame: frame - f(T.trayClick + 0.05), fps, config: { damping: 16 } });
  const flyOut = interpolate(frame, [f(T.pickClick + 0.15), f(T.pickClick + 0.4)], [0, 1], clamp);
  const flyP = s > T.trayClick ? Math.min(flyIn, 1 - flyOut) : 0;
  const fwdIn = spring({ frame: frame - f(T.pickClick + 0.15), fps, config: { damping: 18, mass: 0.8 } });
  const fwdOut = interpolate(frame, [f(T.forwardOut), f(T.forwardOut + 0.4)], [0, 1], clamp);

  // End card.
  const endIn = spring({ frame: frame - f(T.endCard), fps, config: { damping: 18, mass: 0.8 } });

  return (
    <AbsoluteFill
      style={{
        background: `radial-gradient(1200px 900px at 50% -160px, rgba(61,220,151,0.15), transparent 70%), linear-gradient(180deg, ${COLORS.bg0} 0%, ${COLORS.bg1} 55%, ${COLORS.bg0} 100%)`,
        fontFamily: FONT,
        overflow: "hidden",
      }}
    >
      {/* soft glow behind the dock */}
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          top: DOCK_TOP - 260,
          height: 600,
          background: "radial-gradient(520px 200px at 50% 50%, rgba(61,220,151,0.10), transparent 70%)",
          opacity: dockIn,
        }}
      />

      {/* Beat 3 window (Code, the active app) */}
      {s > 5.4 && s < T.windowOut + 0.5 && (
        <FauxWindow
          rect={winRect}
          radius={interpolate(maxP, [0, 1], [18, 0])}
          tint="#67e8f9"
          opacity={winIn * (1 - winOut)}
          scale={1 - 0.04 * winOut}
          pressMax={s > T.maxClick - 0.05 && s < T.maxClick + 0.15}
        />
      )}
      {/* "stops here" edge where the window meets the reserved strip */}
      {edgeLine > 0 && (
        <div
          style={{
            position: "absolute",
            left: 0,
            width: W,
            top: STRIP_TOP - 2,
            height: 4,
            background: COLORS.accent,
            opacity: edgeLine,
            boxShadow: `0 0 24px 4px rgba(61,220,151,0.55)`,
            transform: `scaleX(${interpolate(edgeLine, [0, 1], [0.2, 1])})`,
          }}
        />
      )}

      {/* Beat 4 window brought forward from the tray */}
      {s > T.pickClick + 0.1 && s < T.forwardOut + 0.5 && (
        <FauxWindow
          rect={FORWARD}
          radius={18}
          tint={FLY_APPS[FLY_PICK].c2}
          opacity={Math.min(1, fwdIn * 1.5) * (1 - fwdOut)}
          scale={interpolate(fwdIn, [0, 1], [0.15, 1]) - 0.04 * fwdOut}
          origin={`${flyCx(FLY_PICK) - FORWARD.x}px ${FLY_CY - FORWARD.y}px`}
        />
      )}

      {/* Headlines, one per beat */}
      <Headline from={-0.3} to={2.3} eyebrow="SelaDock · for Windows" lines={["A dock that", "replaces your", "taskbar,", "quietly."]} accent={3} size={116} />
      <Headline from={2.45} to={5.5} eyebrow="Hover" lines={["Hover,", "and it lifts."]} sub="Icons magnify under the pointer." />
      <Headline from={5.6} to={8.9} eyebrow="Reserved space" lines={["Your apps stop", "above the dock."]} sub="Nothing you type hides behind it." />
      <Headline from={9.0} to={12.3} eyebrow="Tray flyout" lines={["Tray apps,", "one click away."]} sub="The ^ button opens them." />
      <Headline from={12.4} to={15.0} eyebrow="System" lines={["Everything the", "taskbar gave you."]} sub="Start, network, volume, battery, and the time." />

      {/* Hook: plain taskbar that the dock replaces */}
      {taskbarOut < 1 && <PlainTaskbar out={taskbarOut} />}

      {/* Tray flyout */}
      {flyP > 0.001 && <TrayFlyout progress={flyP} cursor={cursor} />}

      {/* Dock */}
      <div
        style={{
          position: "absolute",
          left: DOCK_LEFT,
          top: DOCK_TOP,
          width: DOCK_W,
          height: DOCK_H,
          transform: `translateY(${interpolate(dockIn, [0, 1], [180, 0])}px)`,
          opacity: Math.min(1, dockIn * 1.4),
        }}
      >
        <Dock cursorX={cursor.x} hover={overDock} />
      </div>

      {/* Tooltip above the hovered item */}
      {overDock && <Tooltip cursorX={cursor.x} />}

      {/* End card */}
      {s > T.endCard - 0.1 && <EndCard p={endIn} />}

      {/* Cursor + click ripples */}
      {cursorVisible && <Cursor x={cursor.x} y={cursor.y} s={s} />}
    </AbsoluteFill>
  );
};

// ---- Headline ------------------------------------------------------------------------------

const Headline: React.FC<{
  from: number;
  to: number;
  eyebrow: string;
  lines: string[];
  accent?: number;
  sub?: string;
  size?: number;
}> = ({ from, to, eyebrow, lines, accent, sub, size = 104 }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const start = from * fps;
  const end = to * fps;
  if (frame < start - 2 || frame > end) return null;

  const outP = interpolate(frame, [end - 8, end], [0, 1], { ...clamp, easing: Easing.in(Easing.cubic) });
  const rows = [eyebrow, ...lines, ...(sub ? [sub] : [])];
  const enter = (i: number) =>
    interpolate(frame, [start + i * 4, start + i * 4 + 12], [0, 1], { ...clamp, easing: Easing.out(Easing.cubic) });
  const style = (i: number): React.CSSProperties => {
    const p = enter(i);
    return {
      opacity: p * (1 - outP),
      transform: `translateY(${(1 - p) * 48 - outP * 28}px)`,
    };
  };

  return (
    <div style={{ position: "absolute", left: 84, right: 84, top: 330 }}>
      <div
        style={{
          ...style(0),
          color: COLORS.accent,
          fontSize: 30,
          fontWeight: 600,
          letterSpacing: 6,
          textTransform: "uppercase",
          marginBottom: 22,
        }}
      >
        {eyebrow}
      </div>
      {lines.map((line, i) => (
        <div
          key={line}
          style={{
            ...style(i + 1),
            color: i === accent ? COLORS.accent : COLORS.text,
            fontSize: size,
            fontWeight: 700,
            lineHeight: 1.06,
            letterSpacing: -2,
            textShadow: "0 4px 30px rgba(0,0,0,0.45)",
          }}
        >
          {line}
        </div>
      ))}
      {sub && (
        <div style={{ ...style(rows.length - 1), color: "rgba(255,255,255,0.72)", fontSize: 42, fontWeight: 500, marginTop: 30, lineHeight: 1.3 }}>
          {sub}
        </div>
      )}
    </div>
  );
};

// ---- Dock ----------------------------------------------------------------------------------

const proximity = (cursorX: number, i: number, reach: number) =>
  Math.max(0, 1 - Math.abs(cursorX - cx(i)) / reach);

const Dock: React.FC<{ cursorX: number; hover: boolean }> = ({ cursorX, hover }) => (
  <div
    style={{
      position: "absolute",
      inset: 0,
      background: COLORS.dock,
      border: `1.5px solid ${COLORS.dockBorder}`,
      borderRadius: 26,
      boxShadow: "0 30px 70px -18px rgba(0,0,0,0.7)",
      backdropFilter: "blur(18px)",
    }}
  >
    {ITEMS.map((it, i) => {
      const hot = hover ? proximity(cursorX, i, 34) : 0;
      const base: React.CSSProperties = { position: "absolute", left: it.x, top: PAD_Y, width: it.w, height: PLATE };
      switch (it.kind) {
        case "sep":
          return <div key={i} style={{ ...base, top: PAD_Y + 8, height: PLATE - 16, display: "flex", justifyContent: "center" }}><div style={{ width: 1.5, background: "rgba(255,255,255,0.2)" }} /></div>;
        case "app": {
          const scale = 1 + (hover ? 0.28 * proximity(cursorX, i, 150) : 0);
          return <AppIcon key={i} style={base} app={it.app} scale={scale} />;
        }
        case "start":
          return (
            <div key={i} style={{ ...base, display: "flex", alignItems: "center", justifyContent: "center" }}>
              <div
                style={{
                  width: ICON,
                  height: ICON,
                  borderRadius: 14,
                  background: "linear-gradient(145deg, #2a2f3a, #171b22)",
                  border: "1px solid rgba(255,255,255,0.08)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  transform: `scale(${1 + 0.1 * hot})`,
                  boxShadow: hot > 0.3 ? "0 0 0 3px rgba(108,168,255,0.35)" : "none",
                }}
              >
                <WinLogo />
              </div>
            </div>
          );
        case "sys":
          return (
            <div key={i} style={{ ...base, display: "flex", alignItems: "center", justifyContent: "center", borderRadius: 14, background: `rgba(255,255,255,${0.12 * hot})` }}>
              <SysGlyph icon={it.icon} />
            </div>
          );
        case "clock":
          return (
            <div key={i} style={{ ...base, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", borderRadius: 14, background: `rgba(255,255,255,${0.12 * hot})` }}>
              <b style={{ color: COLORS.text, fontSize: 26, fontWeight: 600, lineHeight: 1.1 }}>21:04</b>
              <i style={{ color: COLORS.sub, fontSize: 16, fontStyle: "normal" }}>Sun, 1 Nov</i>
            </div>
          );
      }
    })}
  </div>
);

const AppIcon: React.FC<{ app: (typeof APPS)[number]; scale: number; style: React.CSSProperties }> = ({ app, scale, style }) => (
  <div style={{ ...style, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "flex-end", transform: `scale(${scale})`, transformOrigin: "bottom center" }}>
    <div
      style={{
        width: ICON,
        height: ICON,
        borderRadius: 14,
        background: `linear-gradient(145deg, ${app.c1}, ${app.c2})`,
        boxShadow: scale > 1.05 ? "0 10px 24px -6px rgba(0,0,0,0.55)" : "none",
      }}
    />
    <div
      style={{
        width: app.active ? 8 : 6,
        height: app.active ? 8 : 6,
        borderRadius: "50%",
        marginTop: 4,
        marginBottom: app.active ? 0 : 1,
        background: app.active ? COLORS.activeDot : app.running ? "rgba(255,255,255,0.9)" : "transparent",
      }}
    />
  </div>
);

const Tooltip: React.FC<{ cursorX: number }> = ({ cursorX }) => {
  // Nearest hoverable item.
  let best = -1;
  let bestD = Infinity;
  ITEMS.forEach((it, i) => {
    if (it.kind === "sep") return;
    const d = Math.abs(cursorX - cx(i));
    if (d < bestD) {
      bestD = d;
      best = i;
    }
  });
  const it = ITEMS[best];
  if (!it || it.kind === "sep") return null;
  const o = Math.max(0, 1 - bestD / 26);
  if (o <= 0) return null;
  const label = it.kind === "app" ? it.app.tip : it.tip;
  const lift = it.kind === "app" ? PLATE * 1.28 : PLATE;
  return (
    <div
      style={{
        position: "absolute",
        left: cx(best),
        top: ITEM_BOTTOM - lift - 20,
        transform: `translate(-50%, -100%) translateY(${(1 - o) * 8}px)`,
        opacity: o,
        padding: "10px 22px",
        background: "rgba(27,28,34,0.96)",
        border: "1px solid rgba(255,255,255,0.16)",
        borderRadius: 14,
        color: COLORS.text,
        fontSize: 28,
        fontWeight: 600,
        whiteSpace: "nowrap",
        boxShadow: "0 14px 30px -10px rgba(0,0,0,0.6)",
      }}
    >
      {label}
    </div>
  );
};

const TrayFlyout: React.FC<{ progress: number; cursor: { x: number; y: number } }> = ({ progress, cursor }) => (
  <div
    style={{
      position: "absolute",
      left: FLY_LEFT,
      top: FLY_TOP,
      width: FLY_W,
      height: FLY_H,
      transform: `translateY(${interpolate(progress, [0, 1], [16, 0])}px) scale(${interpolate(progress, [0, 1], [0.94, 1])})`,
      transformOrigin: `${cx(sysI("caret")) - FLY_LEFT}px 100%`,
      opacity: progress,
      background: "rgba(27,28,34,0.96)",
      border: "1.5px solid rgba(255,255,255,0.16)",
      borderRadius: 20,
      boxShadow: "0 24px 50px -12px rgba(0,0,0,0.7)",
    }}
  >
    {FLY_APPS.map((a, i) => {
      const hot = Math.abs(cursor.x - flyCx(i)) < FLY_ICON / 2 && Math.abs(cursor.y - FLY_CY) < FLY_ICON / 2;
      return (
        <div
          key={i}
          style={{
            position: "absolute",
            left: FLY_PAD + i * (FLY_ICON + FLY_GAP),
            top: FLY_PAD,
            width: FLY_ICON,
            height: FLY_ICON,
            borderRadius: 14,
            background: `linear-gradient(145deg, ${a.c1}, ${a.c2})`,
            transform: `scale(${hot ? 1.1 : 1})`,
            boxShadow: hot ? "0 0 0 4px rgba(255,255,255,0.25)" : "none",
          }}
        />
      );
    })}
  </div>
);

// ---- Desktop pieces ------------------------------------------------------------------------

const FauxWindow: React.FC<{
  rect: Rect;
  radius: number;
  tint: string;
  opacity: number;
  scale: number;
  origin?: string;
  pressMax?: boolean;
}> = ({ rect, radius, tint, opacity, scale, origin = "50% 50%", pressMax }) => {
  const lines = [0.62, 0.4, 0.78, 0.5, 0.7, 0.3, 0.56, 0.66, 0.36, 0.74, 0.48, 0.6, 0.42, 0.7, 0.52, 0.34, 0.64, 0.46, 0.72, 0.38];
  const visibleLines = Math.max(0, Math.floor((rect.h - TITLE_H - 48) / 54));
  return (
    <div
      style={{
        position: "absolute",
        left: rect.x,
        top: rect.y,
        width: rect.w,
        height: rect.h,
        borderRadius: radius,
        overflow: "hidden",
        background: "#131821",
        border: "1.5px solid rgba(255,255,255,0.10)",
        boxShadow: "0 40px 90px -30px rgba(0,0,0,0.8)",
        opacity,
        transform: `scale(${scale})`,
        transformOrigin: origin,
      }}
    >
      <div style={{ height: TITLE_H, display: "flex", alignItems: "center", padding: "0 26px", background: "#1a2030", borderBottom: "1px solid rgba(255,255,255,0.06)" }}>
        <div style={{ width: 26, height: 26, borderRadius: 7, background: tint }} />
        <div style={{ width: 180, height: 12, borderRadius: 6, marginLeft: 18, background: "rgba(255,255,255,0.16)" }} />
        <div style={{ flex: 1 }} />
        <WinButton kind="min" />
        <WinButton kind="max" pressed={pressMax} />
        <WinButton kind="close" />
      </div>
      <div style={{ padding: "34px 40px" }}>
        {lines.slice(0, visibleLines).map((wPct, i) => (
          <div key={i} style={{ display: "flex", alignItems: "center", gap: 18, height: 54 }}>
            <div style={{ width: 28, height: 10, borderRadius: 5, background: "rgba(255,255,255,0.07)" }} />
            <div style={{ width: `${wPct * 70}%`, height: 14, borderRadius: 7, background: i % 3 === 0 ? `${tint}33` : "rgba(255,255,255,0.08)" }} />
          </div>
        ))}
      </div>
    </div>
  );
};

const WinButton: React.FC<{ kind: "min" | "max" | "close"; pressed?: boolean }> = ({ kind, pressed }) => (
  <div style={{ width: 60, height: 44, display: "flex", alignItems: "center", justifyContent: "center", borderRadius: 8, background: pressed ? "rgba(255,255,255,0.14)" : "transparent" }}>
    <svg width={22} height={22} viewBox="0 0 22 22" fill="none" stroke="rgba(255,255,255,0.75)" strokeWidth={2}>
      {kind === "min" && <path d="M4 11h14" />}
      {kind === "max" && <rect x={4} y={4} width={14} height={14} rx={2} />}
      {kind === "close" && <path d="M5 5l12 12M17 5L5 17" />}
    </svg>
  </div>
);

const PlainTaskbar: React.FC<{ out: number }> = ({ out }) => (
  <div
    style={{
      position: "absolute",
      left: 0,
      width: W,
      top: DOCK_TOP - 6,
      height: 96,
      background: "#202226",
      borderTop: "1px solid rgba(255,255,255,0.08)",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      gap: 18,
      transform: `translateY(${out * 180}px)`,
      opacity: 1 - out,
    }}
  >
    <WinLogo size={30} />
    {APPS.slice(0, 5).map((a) => (
      <div key={a.tip} style={{ width: 40, height: 40, borderRadius: 6, background: a.c2, opacity: 0.55 }} />
    ))}
    <div style={{ position: "absolute", right: 30, color: "rgba(255,255,255,0.7)", fontSize: 22, textAlign: "right", lineHeight: 1.2 }}>
      21:04
      <br />
      01/11/2026
    </div>
  </div>
);

const EndCard: React.FC<{ p: number }> = ({ p }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const row = (i: number) => {
    const q = interpolate(frame, [fps * T.endCard + 4 + i * 4, fps * T.endCard + 16 + i * 4], [0, 1], {
      ...clamp,
      easing: Easing.out(Easing.cubic),
    });
    return { opacity: q, transform: `translateY(${(1 - q) * 36}px)` };
  };
  return (
    <div style={{ position: "absolute", left: 0, right: 0, top: 380, display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center" }}>
      <Img
        src={staticFile("logo.svg")}
        style={{
          width: 220,
          height: 220,
          opacity: Math.min(1, p * 1.5),
          transform: `scale(${interpolate(p, [0, 1], [0.6, 1])})`,
          filter: "drop-shadow(0 24px 50px rgba(61,220,151,0.25))",
        }}
      />
      <div style={{ ...row(0), color: COLORS.text, fontSize: 132, fontWeight: 700, letterSpacing: -3, marginTop: 36 }}>SelaDock</div>
      <div style={{ ...row(1), color: "rgba(255,255,255,0.72)", fontSize: 44, fontWeight: 500, marginTop: 8 }}>Free and open source</div>
      <div style={{ ...row(2), color: "rgba(255,255,255,0.72)", fontSize: 44, fontWeight: 500, marginTop: 6 }}>Windows 10 and 11</div>
      <div
        style={{
          ...row(3),
          marginTop: 56,
          padding: "22px 40px",
          borderRadius: 999,
          border: `2px solid ${COLORS.accent}`,
          background: "rgba(61,220,151,0.12)",
          color: COLORS.accent,
          fontSize: 46,
          fontWeight: 600,
        }}
      >
        agusumri.github.io/seladock
      </div>
    </div>
  );
};

// ---- Small glyphs --------------------------------------------------------------------------

const WinLogo: React.FC<{ size?: number }> = ({ size = 24 }) => {
  const q = size * 0.46;
  return (
    <div style={{ width: size, height: size, position: "relative" }}>
      {[[0, 0], [1, 0], [0, 1], [1, 1]].map(([x, y]) => (
        <div key={`${x}${y}`} style={{ position: "absolute", width: q, height: q, left: x ? size - q : 0, top: y ? size - q : 0, background: "#6cb0ff", borderRadius: 1 }} />
      ))}
    </div>
  );
};

const SysGlyph: React.FC<{ icon: SysIcon }> = ({ icon }) => (
  <svg width={30} height={30} viewBox="0 0 24 24" fill="none" stroke={COLORS.text} strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
    {icon === "caret" && <path d="M6 15l6-6 6 6" />}
    {icon === "wifi" && (
      <>
        <path d="M2.5 9a14 14 0 0 1 19 0" />
        <path d="M5.5 12.5a9.5 9.5 0 0 1 13 0" />
        <path d="M8.7 16a5 5 0 0 1 6.6 0" />
        <circle cx={12} cy={19.2} r={1.1} fill={COLORS.text} />
      </>
    )}
    {icon === "volume" && (
      <>
        <path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z" />
        <path d="M15.5 9a4 4 0 0 1 0 6" />
        <path d="M18 6.5a7.5 7.5 0 0 1 0 11" />
      </>
    )}
    {icon === "battery" && (
      <>
        <rect x={2.5} y={7} width={17} height={10} rx={2.2} />
        <path d="M21.5 10.5v3" />
        <rect x={5} y={9.5} width={10} height={5} rx={1} fill={COLORS.text} stroke="none" />
      </>
    )}
  </svg>
);

const Cursor: React.FC<{ x: number; y: number; s: number }> = ({ x, y, s }) => {
  const SIZE = 52;
  // tip of the path is at (5,3) in a 24 viewBox
  const tipX = (5 / 24) * SIZE;
  const tipY = (3 / 24) * SIZE;
  const pressed = CLICKS.some((c) => s >= c && s < c + 0.12);
  return (
    <>
      {CLICKS.map((c) => {
        const k = (s - c) / 0.45;
        if (k < 0 || k > 1) return null;
        const r = 14 + 46 * Easing.out(Easing.cubic)(k);
        return (
          <div
            key={c}
            style={{
              position: "absolute",
              left: x - r,
              top: y - r,
              width: r * 2,
              height: r * 2,
              borderRadius: "50%",
              border: `3px solid ${COLORS.accent}`,
              opacity: 0.8 * (1 - k),
            }}
          />
        );
      })}
      <svg
        width={SIZE}
        height={SIZE}
        viewBox="0 0 24 24"
        style={{
          position: "absolute",
          left: x - tipX,
          top: y - tipY,
          transform: `scale(${pressed ? 0.88 : 1})`,
          transformOrigin: `${tipX}px ${tipY}px`,
          filter: "drop-shadow(0 3px 5px rgba(0,0,0,0.55))",
        }}
      >
        <path d="M5 3l14 8-6.2 1.6L10 19z" fill="#fff" stroke="#111" strokeWidth={1.2} strokeLinejoin="round" />
      </svg>
    </>
  );
};
