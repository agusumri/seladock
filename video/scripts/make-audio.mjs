// Synthesizes the SelaDock Reels soundtrack and UI sound effects from scratch (no samples, no
// third-party audio), so every sound is ours to use. Deterministic: seeded noise, fixed params.
// Output: 44.1 kHz, stereo, 16-bit PCM WAV in public/audio/.
//
//   node scripts/make-audio.mjs

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.join(__dirname, "../public/audio");
const SR = 44100;
const TAU = Math.PI * 2;

// ---- helpers -------------------------------------------------------------------------------

/** Small seeded PRNG (mulberry32) so noise is identical on every run. */
const rng = (seed) => () => {
  seed |= 0;
  seed = (seed + 0x6d2b79f5) | 0;
  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};

const stereo = (seconds) => {
  const n = Math.round(seconds * SR);
  return { L: new Float64Array(n), R: new Float64Array(n), n };
};

const midi = (m) => 440 * 2 ** ((m - 69) / 12);
const dB = (db) => 10 ** (db / 20);
const clamp01 = (x) => Math.min(1, Math.max(0, x));
/** Equal-power pan, p in [-1, 1]. */
const pan = (p) => [Math.cos(((p + 1) * Math.PI) / 4), Math.sin(((p + 1) * Math.PI) / 4)];

/** Remove DC with a one-pole high-pass (~20 Hz). */
const dcBlock = (x) => {
  const a = Math.exp((-TAU * 20) / SR);
  let px = 0;
  let py = 0;
  for (let i = 0; i < x.length; i++) {
    const y = x[i] - px + a * py;
    px = x[i];
    py = y;
    x[i] = y;
  }
};

/** Short linear fades at both ends so no file starts or stops on a non-zero sample. */
const edgeFade = (buf, inMs = 2, outMs = 8) => {
  const fi = Math.round((inMs / 1000) * SR);
  const fo = Math.round((outMs / 1000) * SR);
  for (const ch of [buf.L, buf.R]) {
    for (let i = 0; i < fi && i < ch.length; i++) ch[i] *= i / fi;
    for (let i = 0; i < fo && i < ch.length; i++) ch[ch.length - 1 - i] *= i / fo;
  }
};

const normalize = (buf, peakDb) => {
  let peak = 0;
  for (const ch of [buf.L, buf.R]) for (const v of ch) peak = Math.max(peak, Math.abs(v));
  const g = peak > 0 ? dB(peakDb) / peak : 0;
  for (const ch of [buf.L, buf.R]) for (let i = 0; i < ch.length; i++) ch[i] *= g;
};

const finish = (buf, peakDb) => {
  dcBlock(buf.L);
  dcBlock(buf.R);
  normalize(buf, peakDb);
  edgeFade(buf);
  return buf;
};

const writeWav = (file, buf) => {
  const { L, R, n } = buf;
  const data = Buffer.alloc(n * 4);
  for (let i = 0; i < n; i++) {
    const l = Math.round(Math.max(-1, Math.min(1, L[i])) * 32767);
    const r = Math.round(Math.max(-1, Math.min(1, R[i])) * 32767);
    data.writeInt16LE(l, i * 4);
    data.writeInt16LE(r, i * 4 + 2);
  }
  const header = Buffer.alloc(44);
  header.write("RIFF", 0);
  header.writeUInt32LE(36 + data.length, 4);
  header.write("WAVE", 8);
  header.write("fmt ", 12);
  header.writeUInt32LE(16, 16); // PCM chunk size
  header.writeUInt16LE(1, 20); // PCM
  header.writeUInt16LE(2, 22); // channels
  header.writeUInt32LE(SR, 24);
  header.writeUInt32LE(SR * 4, 28); // byte rate
  header.writeUInt16LE(4, 32); // block align
  header.writeUInt16LE(16, 34); // bits
  header.write("data", 36);
  header.writeUInt32LE(data.length, 40);
  fs.writeFileSync(path.join(OUT_DIR, file), Buffer.concat([header, data]));
  console.log(`wrote ${file} (${(n / SR).toFixed(3)} s)`);
};

/** Chamberlin state-variable filter; returns a per-sample function (input, cutoffHz, q) -> band/low. */
const svf = () => {
  let low = 0;
  let band = 0;
  return (x, fc, q = 0.7) => {
    const f = 2 * Math.sin((Math.PI * Math.min(fc, SR / 6)) / SR);
    low += f * band;
    const high = x - low - (1 / q) * band;
    band += f * high;
    return { low, band, high };
  };
};

// ---- music ---------------------------------------------------------------------------------
// 18 s at ~107 BPM: 8 chords of 4 beats (2.25 s each), Am - F - C - G twice.

const makeMusic = () => {
  const DUR = 18;
  const buf = stereo(DUR);
  const BEAT = 2.25 / 4;
  const CHORD = 2.25;
  // MIDI notes: pad voicing (mid register) and bass root.
  const CHORDS = [
    { pad: [57, 60, 64, 67], root: 45 }, // Am7
    { pad: [53, 57, 60, 64], root: 41 }, // Fmaj7
    { pad: [55, 60, 64, 67], root: 48 }, // C (add G on top)
    { pad: [55, 59, 62, 67], root: 43 }, // G
  ];
  const chordAt = (k) => CHORDS[k % CHORDS.length];

  const padL = new Float64Array(buf.n);
  const padR = new Float64Array(buf.n);
  const dry = { L: new Float64Array(buf.n), R: new Float64Array(buf.n) };

  // Pad: each voice = two detuned soft "saw" stacks (first 5 harmonics, 1/k^1.6 rolloff),
  // with slow attack/release that overlaps the next chord so changes are seamless.
  const ATT = 0.7;
  const REL = 1.2;
  for (let k = 0; k < 8; k++) {
    const { pad: notes } = chordAt(k);
    const t0 = k * CHORD;
    const t1 = t0 + CHORD;
    const s0 = Math.max(0, Math.floor((t0 - 0.05) * SR));
    const s1 = Math.min(buf.n, Math.ceil((t1 + REL) * SR));
    notes.forEach((m, vi) => {
      const f0 = midi(m);
      const [pl, pr] = pan(-0.5 + (vi / (notes.length - 1)) * 1.0);
      const det = [0.9965, 1.0035];
      for (let i = s0; i < s1; i++) {
        const t = i / SR;
        const lt = t - t0;
        let env = clamp01(lt / ATT);
        env = env * env * (3 - 2 * env); // smoothstep attack
        if (t > t1) env *= Math.cos(clamp01((t - t1) / REL) * (Math.PI / 2)) ** 2;
        if (env <= 0) continue;
        let a = 0;
        let b = 0;
        for (let h = 1; h <= 5; h++) {
          const amp = 1 / h ** 1.6;
          a += amp * Math.sin(TAU * f0 * det[0] * h * t + vi);
          b += amp * Math.sin(TAU * f0 * det[1] * h * t + vi * 1.7);
        }
        const v = 0.05 * env;
        padL[i] += v * (a * pl + b * 0.6 * pr);
        padR[i] += v * (b * pr + a * 0.6 * pl);
      }
    });
  }
  // Darken the pad with a slow-breathing low-pass.
  {
    const fl = svf();
    const fr = svf();
    for (let i = 0; i < buf.n; i++) {
      const t = i / SR;
      const fc = 1300 + 500 * Math.sin((TAU * t) / 9);
      dry.L[i] += fl(padL[i], fc, 0.6).low;
      dry.R[i] += fr(padR[i], fc, 0.6).low;
    }
  }

  // Bass: sine root plus a little 2nd harmonic, soft swell on beats 1 and 3.
  for (let k = 0; k < 8; k++) {
    const f0 = midi(chordAt(k).root);
    for (const beat of [0, 2]) {
      const t0 = k * CHORD + beat * BEAT;
      const len = BEAT * 2;
      const s0 = Math.floor(t0 * SR);
      const s1 = Math.min(buf.n, Math.floor((t0 + len + 0.15) * SR));
      for (let i = s0; i < s1; i++) {
        const lt = i / SR - t0;
        const env = clamp01(lt / 0.03) * Math.exp(-lt * 2.2) * clamp01((len + 0.15 - lt) / 0.15);
        const v = 0.16 * env * (Math.sin(TAU * f0 * lt) + 0.18 * Math.sin(TAU * 2 * f0 * lt));
        dry.L[i] += v;
        dry.R[i] += v;
      }
    }
  }

  // Soft kick: sine pitch drop on beats 1 and 3, from bar 2 on.
  for (let b = 4; b < 32; b += 2) {
    const t0 = b * BEAT;
    const s0 = Math.floor(t0 * SR);
    const s1 = Math.min(buf.n, Math.floor((t0 + 0.32) * SR));
    let ph = 0;
    for (let i = s0; i < s1; i++) {
      const lt = i / SR - t0;
      const fr = 44 + 70 * Math.exp(-lt * 28);
      ph += (TAU * fr) / SR;
      const env = clamp01(lt / 0.004) * Math.exp(-lt * 11);
      const v = 0.22 * env * Math.sin(ph);
      dry.L[i] += v;
      dry.R[i] += v;
    }
  }

  // Soft shaker on the off-beats (filtered seeded noise), very low.
  {
    const rand = rng(7);
    const f = svf();
    for (let b = 8; b < 32; b++) {
      const t0 = (b + 0.5) * BEAT;
      const s0 = Math.floor(t0 * SR);
      const s1 = Math.min(buf.n, Math.floor((t0 + 0.09) * SR));
      const [pl, pr] = pan(b % 2 ? 0.35 : -0.35);
      for (let i = s0; i < s1; i++) {
        const lt = i / SR - t0;
        const env = clamp01(lt / 0.006) * Math.exp(-lt * 45);
        const v = 0.035 * env * f(rand() * 2 - 1, 6500, 1.2).band;
        dry.L[i] += v * pl;
        dry.R[i] += v * pr;
      }
    }
  }

  // Arpeggio: eighth notes through the chord an octave up, sine + soft 2nd harmonic,
  // enters at bar 3 (4.5 s), ping-pong panned. Sent to the echo below.
  const arpL = new Float64Array(buf.n);
  const arpR = new Float64Array(buf.n);
  const PATTERN = [0, 1, 2, 3, 2, 1, 2, 3];
  for (let k = 2; k < 8; k++) {
    const { pad } = chordAt(k);
    for (let e = 0; e < 8; e++) {
      const m = pad[PATTERN[e]] + 12;
      const f0 = midi(m);
      const t0 = k * CHORD + e * (BEAT / 2);
      const s0 = Math.floor(t0 * SR);
      const s1 = Math.min(buf.n, Math.floor((t0 + 0.6) * SR));
      const [pl, pr] = pan(e % 2 ? 0.45 : -0.45);
      const vel = e % 4 === 0 ? 1 : 0.7;
      for (let i = s0; i < s1; i++) {
        const lt = i / SR - t0;
        const env = clamp01(lt / 0.008) * Math.exp(-lt * 7) * clamp01((0.6 - lt) / 0.05);
        const v = 0.05 * vel * env * (Math.sin(TAU * f0 * lt) + 0.25 * Math.sin(TAU * 2 * f0 * lt) * Math.exp(-lt * 10));
        arpL[i] += v * pl;
        arpR[i] += v * pr;
      }
    }
  }

  // Stereo echo (dotted-eighth-ish, cross-fed) for the arp and a little of the pad.
  {
    const dl = Math.round(BEAT * 0.75 * SR);
    const dr = Math.round(BEAT * 1.0 * SR);
    const fb = 0.38;
    const lp = { l: 0, r: 0 };
    const bufL = new Float64Array(buf.n);
    const bufR = new Float64Array(buf.n);
    for (let i = 0; i < buf.n; i++) {
      const inL = arpL[i] + 0.15 * dry.L[i];
      const inR = arpR[i] + 0.15 * dry.R[i];
      const eL = i >= dl ? bufR[i - dl] : 0; // cross-feed: ping-pong
      const eR = i >= dr ? bufL[i - dr] : 0;
      lp.l += 0.35 * (eL - lp.l);
      lp.r += 0.35 * (eR - lp.r);
      bufL[i] = inL + fb * lp.l;
      bufR[i] = inR + fb * lp.r;
      buf.L[i] = dry.L[i] + arpL[i] + 0.5 * lp.l;
      buf.R[i] = dry.R[i] + arpR[i] + 0.5 * lp.r;
    }
  }

  // Master fades: 1 s in, 1.5 s out (equal-power curves).
  for (let i = 0; i < buf.n; i++) {
    const t = i / SR;
    const fin = Math.sin(clamp01(t / 1.0) * (Math.PI / 2));
    const fout = Math.sin(clamp01((DUR - t) / 1.5) * (Math.PI / 2));
    const g = fin * fin * fout * fout;
    buf.L[i] *= g;
    buf.R[i] *= g;
  }
  return finish(buf, -6);
};

// ---- sound effects -------------------------------------------------------------------------

/** Soft UI tap: a short damped tone with a whisper of filtered noise. */
const makeClick = () => {
  const buf = stereo(0.06);
  const rand = rng(11);
  const f = svf();
  for (let i = 0; i < buf.n; i++) {
    const t = i / SR;
    const env = clamp01(t / 0.0015) * Math.exp(-t * 90);
    const tone = Math.sin(TAU * 1700 * t) * 0.6 + Math.sin(TAU * 900 * t) * 0.4;
    const noise = f(rand() * 2 - 1, 3200, 1.5).band * Math.exp(-t * 300);
    const v = env * (tone + 0.8 * noise);
    buf.L[i] = v;
    buf.R[i] = v;
  }
  return finish(buf, -14);
};

/** Soft swoosh: band-passed noise whose center sweeps up then down, panned left to right. */
const makeWhoosh = () => {
  const DUR = 0.4;
  const buf = stereo(DUR);
  const rand = rng(23);
  const f1 = svf();
  const f2 = svf();
  for (let i = 0; i < buf.n; i++) {
    const t = i / SR;
    const p = t / DUR;
    const env = Math.sin(Math.PI * p ** 0.8) ** 2; // skewed bell
    const fc = 350 + 2100 * Math.sin(Math.PI * clamp01(p * 1.1)) ** 1.5;
    const x = rand() * 2 - 1;
    const v = env * f2(f1(x, fc, 0.9).band, fc, 0.9).band;
    const [pl, pr] = pan(-0.6 + 1.2 * p);
    buf.L[i] = v * pl;
    buf.R[i] = v * pr;
  }
  return finish(buf, -14);
};

/** Soft rising blip for tooltips and the flyout. */
const makePop = () => {
  const DUR = 0.12;
  const buf = stereo(DUR);
  let ph = 0;
  for (let i = 0; i < buf.n; i++) {
    const t = i / SR;
    const fr = 520 + 640 * (1 - Math.exp(-t * 40));
    ph += (TAU * fr) / SR;
    const env = clamp01(t / 0.004) * Math.exp(-t * 32);
    const v = env * (Math.sin(ph) + 0.12 * Math.sin(2 * ph));
    buf.L[i] = v;
    buf.R[i] = v;
  }
  return finish(buf, -18);
};

/** Gentle bell: inharmonic partials on A5 with separate decays, slight stereo spread. */
const makeChime = () => {
  const DUR = 1.2;
  const buf = stereo(DUR);
  const f0 = midi(81); // A5, in key with the Am-F-C-G bed
  const PARTIALS = [
    { r: 1, a: 1, d: 3.2 },
    { r: 2.0, a: 0.45, d: 4.5 },
    { r: 2.76, a: 0.25, d: 6.5 },
    { r: 4.07, a: 0.12, d: 9 },
    { r: 0.5, a: 0.2, d: 2.6 },
  ];
  for (let i = 0; i < buf.n; i++) {
    const t = i / SR;
    const att = clamp01(t / 0.006);
    const tail = clamp01((DUR - t) / 0.25);
    let l = 0;
    let r = 0;
    for (const p of PARTIALS) {
      const e = p.a * Math.exp(-t * p.d);
      l += e * Math.sin(TAU * f0 * p.r * 0.999 * t);
      r += e * Math.sin(TAU * f0 * p.r * 1.001 * t + 0.4);
    }
    buf.L[i] = att * tail * l;
    buf.R[i] = att * tail * r;
  }
  return finish(buf, -10);
};

// ---- main ----------------------------------------------------------------------------------

fs.mkdirSync(OUT_DIR, { recursive: true });
writeWav("music.wav", makeMusic());
writeWav("click.wav", makeClick());
writeWav("whoosh.wav", makeWhoosh());
writeWav("pop.wav", makePop());
writeWav("chime.wav", makeChime());
