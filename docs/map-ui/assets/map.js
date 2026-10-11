// Map core: camera on the sphere, world state, rendering by zoom band, input.
'use strict';
const R = 6371000, D2R = Math.PI / 180, R2D = 180 / Math.PI;
const SW = 1920, SH = 1080, TOP = 46, LP = 320, RP = 360, CARDH = 236;
const MAP = window.LAYOUT ? Object.assign({}, window.LAYOUT) : { x0: LP, y0: TOP, x1: SW - RP, y1: SH - CARDH };
MAP.cx = (MAP.x0 + MAP.x1) / 2; MAP.cy = (MAP.y0 + MAP.y1) / 2; MAP.w = MAP.x1 - MAP.x0; MAP.h = MAP.y1 - MAP.y0;
const SMIN = 300, SMAX = R / 0.12;
const NM = 1852, KYD = 914.4;

const cv = document.getElementById('map'), ctx = cv.getContext('2d');
const app = document.getElementById('app');
let SCALE = 1;
function fit() { SCALE = Math.min(innerWidth / SW, innerHeight / SH); app.style.transform = `scale(${SCALE})`; }
addEventListener('resize', fit); fit();

// ---------------------------------------------------------------- maths
const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
const lerp = (a, b, t) => a + (b - a) * t;
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
function v3(lat, lon) { const a = lat * D2R, b = lon * D2R, c = Math.cos(a); return [c * Math.cos(b), c * Math.sin(b), Math.sin(a)]; }
function ll(v) { const n = Math.hypot(v[0], v[1], v[2]); return [Math.asin(clamp(v[2] / n, -1, 1)) * R2D, Math.atan2(v[1], v[0]) * R2D]; }
function hav(a, b) { // metres
  const dφ = (b.lat - a.lat) * D2R, dλ = (b.lon - a.lon) * D2R;
  const h = Math.sin(dφ / 2) ** 2 + Math.cos(a.lat * D2R) * Math.cos(b.lat * D2R) * Math.sin(dλ / 2) ** 2;
  return 2 * R * Math.asin(Math.min(1, Math.sqrt(h)));
}
function brg(a, b) {
  const φ1 = a.lat * D2R, φ2 = b.lat * D2R, dλ = (b.lon - a.lon) * D2R;
  const y = Math.sin(dλ) * Math.cos(φ2), x = Math.cos(φ1) * Math.sin(φ2) - Math.sin(φ1) * Math.cos(φ2) * Math.cos(dλ);
  return (Math.atan2(y, x) * R2D + 360) % 360;
}
function dest(lat, lon, hdg, m) {
  const δ = m / R, θ = hdg * D2R, φ1 = lat * D2R, λ1 = lon * D2R;
  const φ2 = Math.asin(Math.sin(φ1) * Math.cos(δ) + Math.cos(φ1) * Math.sin(δ) * Math.cos(θ));
  const λ2 = λ1 + Math.atan2(Math.sin(θ) * Math.sin(δ) * Math.cos(φ1), Math.cos(δ) - Math.sin(φ1) * Math.sin(φ2));
  return [φ2 * R2D, ((λ2 * R2D + 540) % 360) - 180];
}
function offLL(lat, lon, hdg, f, s) { // forward/starboard metres -> lat/lon (local, exact enough inside a few km)
  const h = hdg * D2R, n = f * Math.cos(h) - s * Math.sin(h), e = f * Math.sin(h) + s * Math.cos(h);
  return [lat + n / R * R2D, lon + e / (R * Math.cos(lat * D2R)) * R2D];
}
function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return (h >>> 0) / 4294967296; }
const fmtKm = m => m >= 100000 ? Math.round(m / 1000) + ' km' : m >= 10000 ? (m / 1000).toFixed(0) + ' km' : m >= 1000 ? (m / 1000).toFixed(1) + ' km' : Math.round(m) + ' m';
const fmtBrg = b => String(Math.round(b) % 360).padStart(3, '0') + '°';
function fmtDur(s) { s = Math.max(0, s); const h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60); if (h >= 48) return Math.round(h / 24) + ' d'; return h ? `${h}h${String(m).padStart(2, '0')}` : `${m} min`; }
function fmtLL(lat, lon) {
  const f = (v, p, n) => { const a = Math.abs(v), d = Math.floor(a), m = Math.floor((a - d) * 60); return `${d}°${String(m).padStart(2, '0')}′${v >= 0 ? p : n}`; };
  return f(lat, 'N', 'S') + ' ' + f(lon, 'E', 'W');
}

// ---------------------------------------------------------------- camera
const cam = { lat: 56.60, lon: 5.88, s: R / 32 };
const B = { c: [1, 0, 0], e: [0, 1, 0], n: [0, 0, 1] };
function basis() {
  const la = cam.lat * D2R, lo = cam.lon * D2R;
  B.c = v3(cam.lat, cam.lon); B.e = [-Math.sin(lo), Math.cos(lo), 0];
  B.n = [-Math.sin(la) * Math.cos(lo), -Math.sin(la) * Math.sin(lo), Math.cos(la)];
}
basis();
function projV(p) { return { x: MAP.cx + cam.s * dot(p, B.e), y: MAP.cy - cam.s * dot(p, B.n), vis: dot(p, B.c) > 0 }; }
function proj(lat, lon) { return projV(v3(lat, lon)); }
function unproj(x, y) {
  const a = (x - MAP.cx) / cam.s, b = -(y - MAP.cy) / cam.s, q = a * a + b * b;
  if (q > 1) return null;
  const z = Math.sqrt(1 - q);
  return ll([a * B.e[0] + b * B.n[0] + z * B.c[0], a * B.e[1] + b * B.n[1] + z * B.c[1], a * B.e[2] + b * B.n[2] + z * B.c[2]]);
}
function panPx(dx, dy) {
  const k = 1 / cam.s, c = B.c;
  const nc = [c[0] - dx * k * B.e[0] + dy * k * B.n[0], c[1] - dx * k * B.e[1] + dy * k * B.n[1], c[2] - dx * k * B.e[2] + dy * k * B.n[2]];
  [cam.lat, cam.lon] = ll(nc); cam.lat = clamp(cam.lat, -82, 82); basis();
}
function zoomAt(mx, my, f) {
  const g = unproj(mx, my);
  cam.s = clamp(cam.s * f, SMIN, SMAX); basis();
  if (!g) return;
  for (let i = 0; i < 4; i++) { const p = proj(g[0], g[1]); panPx(mx - p.x, my - p.y); }
}
const mpp = () => R / cam.s;
const BANDS = [
  { id: 'ship', name: 'Ship', lo: 0.12, hi: 3, mid: 0.6 },
  { id: 'tac', name: 'Tactical', lo: 3, hi: 40, mid: 14 },
  { id: 'ops', name: 'Operations', lo: 40, hi: 800, mid: 220 },
  { id: 'thr', name: 'Theatre', lo: 800, hi: 5000, mid: 2000 },
  { id: 'globe', name: 'Globe', lo: 5000, hi: R / SMIN, mid: 14000 },
];
function bandIdx(m = mpp()) { for (let i = 0; i < BANDS.length; i++) if (m < BANDS[i].hi) return i; return 4; }
const band = () => BANDS[bandIdx()].id;
// 1 when mpp <= a, 0 when mpp >= b, log-interpolated between
function fade(a, b, m = mpp()) { if (m <= a) return 1; if (m >= b) return 0; return 1 - Math.log(m / a) / Math.log(b / a); }
function viewRadius() { const d = Math.hypot(MAP.w + 2 * LP, MAP.h + CARDH) / 2 / cam.s; return d >= 1 ? Math.PI / 2 : Math.asin(d); }
function viewWidthM() { return MAP.w * mpp(); }
function nearRadius() { return clamp(viewWidthM() * 3, 100000, 1500000); }

let fly = null;
function flyTo(lat, lon, m, dur) {
  const a = v3(cam.lat, cam.lon), b = v3(lat, lon), ang = Math.acos(clamp(dot(a, b), -1, 1));
  const ls0 = Math.log(cam.s), ls1 = Math.log(clamp(R / m, SMIN, SMAX));
  const fitS = Math.log(clamp(MAP.h * 0.6 / Math.max(ang, 1e-9), SMIN, SMAX));
  const mid = Math.min(fitS, ls0, ls1);
  fly = { t0: performance.now(), dur: dur || clamp(600 + 260 * Math.abs(ls1 - ls0) + 900 * Math.min(1, ang * 4), 600, 2200), a, b, ang, ls0, ls1, mid };
}
function stepFly(now) {
  if (!fly) return false;
  let t = clamp((now - fly.t0) / fly.dur, 0, 1); const e = t < .5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
  let v;
  if (fly.ang < 1e-9) v = fly.b; else { const s = Math.sin(fly.ang); const k0 = Math.sin((1 - e) * fly.ang) / s, k1 = Math.sin(e * fly.ang) / s; v = [fly.a[0] * k0 + fly.b[0] * k1, fly.a[1] * k0 + fly.b[1] * k1, fly.a[2] * k0 + fly.b[2] * k1]; }
  [cam.lat, cam.lon] = ll(v);
  const lin = lerp(fly.ls0, fly.ls1, e);
  const bump = Math.max(0, Math.min(fly.ls0, fly.ls1) - fly.mid);
  cam.s = Math.exp(lin - bump * Math.sin(Math.PI * e));
  basis();
  if (t >= 1) fly = null;
  return true;
}

// ---------------------------------------------------------------- world
let worldMs = START_MS, speedAsked = 1, paused = false;
const SPEEDS = [1, 4, 12, 60, 600, 3600, 30000];
const relLL = ([n, e]) => [BATTLE_O[0] + n * 1000 / R * R2D, BATTLE_O[1] + e * 1000 / (R * Math.cos(BATTLE_O[0] * D2R)) * R2D];

const units = [], shipsByName = {}, unitById = {};
const TURRETS = {
  'Lion': ['A', 'B', 'Q', 'X', '2×13.5 in'], 'Queen Mary': ['A', 'B', 'Q', 'X', '2×13.5 in'], 'Tiger': ['A', 'B', 'Q', 'X', '2×13.5 in'],
  'Indefatigable': ['A', 'P', 'Q', 'X', '2×12 in'], 'Invincible': ['A', 'P', 'Q', 'X', '2×12 in'], 'Queen Elizabeth': ['A', 'B', 'X', 'Y', '2×15 in'], 'Revenge': ['A', 'B', 'X', 'Y', '2×15 in'],
  'Iron Duke': ['A', 'B', 'Q', 'X', 'Y', '2×13.5 in'], 'King George V': ['A', 'B', 'Q', 'X', 'Y', '2×13.5 in'], 'Orion': ['A', 'B', 'Q', 'X', 'Y', '2×13.5 in'],
  'Derfflinger': ['A', 'B', 'C', 'D', '2×30.5 cm'], 'Seydlitz': ['A', 'B', 'C', 'D', 'E', '2×28 cm'], 'Moltke': ['A', 'B', 'C', 'D', 'E', '2×28 cm'], 'Von der Tann': ['A', 'B', 'C', 'D', '2×28 cm'],
};
const MAINRANGE = { BB: 21000, BC: 21800, PB: 12500, CA: 14000, CL: 13000, DL: 9000, DD: 8500, SS: 4000, SL: 6000, AV: 5000, MON: 18000, AUX: 8000, MV: 0 };
const DOCTRINE = {
  base: { roe: 'Weapons free', range: '16–18 kyd', retreat: 'Flotation < 40%', ammo: 'APC v capital · CPC v cruisers', emcon: 'W/T: enemy reports only', torp: 'Free > 8 kyd', form: 'Line ahead, 2.5 cables' },
  bcs1: { range: '14–16 kyd' }, fl13: { torp: 'Attack: fire at 7 kyd', form: 'Line ahead, 1 cable' }, lcs2: { roe: 'Report; avoid action', emcon: 'W/T: free (scouting)' },
  bs5: { range: '17–19 kyd' }, oq12: { roe: 'Escort: depth charge on contact', form: 'Convoy 4 cols, 5 cables' }, satl: { roe: 'Weapons free (raider)' },
};

function mkShip(type, name, u, i) {
  const T = TYPES[type] || TYPES.AUX, r = hash(name), r2 = hash(name + 'x');
  const engaged = !!u.enc && u.side !== 'MER';
  const sh = {
    name, type, T, u, i, cls: CLASSES[name] || null,
    flot: engaged ? 0.8 + 0.2 * r : 0.97 + 0.03 * r, str: engaged ? 0.7 + 0.3 * r2 : 1, fire: engaged && r > 0.82 ? 1 : 0, flood: engaged ? Math.round(r * 160) : 0,
    ammo: engaged ? 0.7 + 0.25 * r2 : 0.95 + 0.05 * r, fuel: 0.45 + 0.5 * hash(name + 'f'), crew: Math.round((T.k === 'cap' ? 1050 : T.k === 'cru' ? 420 : 90) * (0.92 + 0.08 * r)),
    hits: engaged ? Math.floor(r2 * (T.k === 'cap' ? 6 : 2)) : 0, kn: 0,
  };
  return sh;
}
function formOffsets(u) {
  const n = u.ships.length, o = [];
  switch (u.form) {
    case 'line': { let f = 0; for (let i = 0; i < n; i++) { o.push([f, 0]); f -= u.ships[i].T.L * 2.4 + 140; } break; }
    case 'cols': { const per = 4, nc = Math.ceil(n / per); for (let i = 0; i < n; i++) { const c = Math.floor(i / per), j = i % per; o.push([-j * 560, (c - (nc - 1) / 2) * 1800]); } break; }
    case 'scout': for (let i = 0; i < n; i++) o.push([0, (i - (n - 1) / 2) * 5200]); break;
    case 'screen': for (let i = 0; i < n; i++) { const a = (n > 1 ? i / (n - 1) - .5 : 0) * Math.PI * 0.95; o.push([Math.cos(a) * 2400 + 600, Math.sin(a) * 2400]); } break;
    case 'port': for (let i = 0; i < n; i++) o.push([-Math.floor(i / 4) * 420, (i % 4) * 330]); break;
    case 'convoy': {
      let m = 0, e = 0;
      for (let i = 0; i < n; i++) {
        if (u.ships[i].type === 'SL') { o.push([900 - e * 1900, e ? -1900 : 1900 * 0 - 1500]); e++; }
        else { o.push([-Math.floor(m / 4) * 750, (m % 4 - 1.5) * 900]); m++; }
      }
      break;
    }
    default: for (let i = 0; i < n; i++) o.push([0, i * 160]);
  }
  return o;
}
for (const d of UNITS) {
  const u = Object.assign({}, d);
  const pos = d.at || relLL(d.rel);
  u.lat = pos[0]; u.lon = pos[1]; u.hdg = d.hdg || 0; u.kn = d.kn || 0;
  u.own = d.side === 'RN'; u.aff = SIDES[d.side].aff; u.ally = !!SIDES[d.side].ally; u.neutral = d.side === 'US';
  u.status = d.status || 'SEA';
  u.ships = d.ships.split(';').map((s, i) => { const k = s.indexOf(' '); return mkShip(s.slice(0, k), s.slice(k + 1), u, i); });
  u.off = formOffsets(u);
  u.wps = (d.wps || []).map(w => { const p = w.at || relLL(w.rel); return { lat: p[0], lon: p[1] }; });
  u.loop = d.id === 'dov';
  u.intel = d.intel || null;
  u.sent = d.sent ? Object.assign({}, d.sent) : null;
  u.doct = Object.assign({}, DOCTRINE.base, DOCTRINE[d.id] || {});
  u.over = Object.keys(DOCTRINE[d.id] || {});
  units.push(u); unitById[u.id] = u;
  for (const s of u.ships) shipsByName[s.name] = s;
}
// the hero ship's state, hand-set
Object.assign(shipsByName['Lion'], { flot: 0.86, str: 0.72, fire: 1, flood: 320, list: 1.2, crew: 1092, crewLoss: 99, hits: 12, ammo: 0.71, fuel: 0.58,
  fireAt: 'Q gunhouse (out) · boat deck', turrets: [['A', '092°', 'Lützow', 98, 'FIRING'], ['B', '093°', 'Lützow', 96, 'FIRING'], ['Q', '—', '—', 0, 'OUT · magazine flooded'], ['X', '095°', 'Lützow', 101, 'FIRING']] });
Object.assign(shipsByName['Queen Mary'], { flot: 0.91, str: 0.83, hits: 4, fire: 0 });
Object.assign(shipsByName['Clan Ross'], { flot: 0.38, str: 0.6, flood: 3100, list: 7, fire: 0, hits: 1 });
const isEnemy = u => u.aff === 'H';
const knownEnemy = u => isEnemy(u) && u.intel && u.intel.cert !== 'none';
const atSea = u => u.status === 'SEA';
const unitPos = u => ({ lat: u.lat, lon: u.lon });
function shipLL(sh) { const u = sh.u, o = u.off[sh.i]; return offLL(u.lat, u.lon, u.hdg, o[0], o[1]); }
function comp(list) { // composition over ships
  const c = { cap: 0, cru: 0, lt: 0, aux: 0, mer: 0, t: 0, BB: 0, BC: 0, PB: 0, CA: 0, CL: 0, DD: 0, SS: 0 };
  for (const s of list) { c[s.T.k]++; c.t += s.T.kt; if (c[s.type] !== undefined) c[s.type]++; if (s.type === 'DL') c.DD++; }
  return c;
}
units.forEach(u => { u.comp = comp(u.ships); });
const fleets = {};
for (const u of units) { if (!u.own) continue; (fleets[u.fleet] = fleets[u.fleet] || { name: u.fleet, units: [] }).units.push(u); }
Object.values(fleets).forEach(f => { f.comp = comp(f.units.flatMap(u => u.ships)); });

const TARGETS = [['Lion', 'Lützow'], ['Princess Royal', 'Derfflinger'], ['Queen Mary', 'Seydlitz'], ['Tiger', 'Moltke'], ['New Zealand', 'Von der Tann'],
  ['Barham', 'Von der Tann'], ['Valiant', 'Moltke'], ['Lützow', 'Lion'], ['Derfflinger', 'Queen Mary'], ['Seydlitz', 'Queen Mary'], ['Moltke', 'Tiger'], ['Von der Tann', 'New Zealand'],
  ['Kent', 'Möwe'], ['Glasgow', 'Möwe'], ['Möwe', 'Glasgow']];
for (const [a, b] of TARGETS) { shipsByName[a].tgt = shipsByName[b]; }
const encById = {}; ENCOUNTERS.forEach(e => { e.lat = e.at[0]; e.lon = e.at[1]; encById[e.id] = e; });
const wrecks = WRECKS.map(w => { const p = relLL(w.rel); return Object.assign({ lat: p[0], lon: p[1] }, w); });
const contacts = CONTACTS.map(c => Object.assign({}, c));

const LOGS = LOG.map(l => ({ t: l[0], c: l[1], m: l[2] }));
function addLog(c, m) { const d = new Date(worldMs); LOGS.unshift({ t: `${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}`, c, m, nu: 1 }); if (window.renderLog) renderLog(); }

function encActive() { return ENCOUNTERS.length; }
function achieved() { if (paused) return 0; return encActive() ? Math.min(speedAsked, 12) : speedAsked; }

function stepWorld(dt) { // dt game seconds
  if (dt <= 0) return;
  for (const u of units) {
    if (!atSea(u) || !u.kn) continue;
    if (u.sent) { u.sent.transit -= dt; if (u.sent.transit <= 0) { u.hdg = u.sent.hdg; addLog('sig', `${u.short}: signal taken in, turning to ${u.sent.hdg}°`); u.sent = null; if (window.refreshCard) refreshCard(true); } }
    let m = u.kn * 0.5144 * dt;
    while (u.wps.length && m > 0) {
      const w = u.wps[0], d = hav(u, w);
      u.hdg = brg(u, w);
      if (d <= m) { u.lat = w.lat; u.lon = w.lon; m -= d; const p = u.wps.shift(); if (u.loop) u.wps.push(p); else if (!u.wps.length) { addLog('cmd', `${u.short}: arrived at waypoint`); } }
      else break;
    }
    if (m > 0) { const p = dest(u.lat, u.lon, u.hdg, m); u.lat = p[0]; u.lon = p[1]; }
  }
}

// ---------------------------------------------------------------- land
function decodeLayer(polys, q) {
  return polys.map(poly => {
    const coords = poly.map(r => { const out = []; let x = 0, y = 0; for (let i = 0; i < r.length; i += 2) { x += r[i]; y += r[i + 1]; out.push([x * q, y * q]); } return out; });
    let sx = 0, sy = 0, sz = 0; const ring = coords[0];
    for (const [lo, la] of ring) { const v = v3(la, lo); sx += v[0]; sy += v[1]; sz += v[2]; }
    const n = Math.hypot(sx, sy, sz); const c = n > 1e-9 ? [sx / n, sy / n, sz / n] : [0, 0, -1];
    let mr = 0; for (const [lo, la] of ring) mr = Math.max(mr, Math.acos(clamp(dot(v3(la, lo), c), -1, 1)));
    if (n < ring.length * 0.2) mr = Math.PI; // wraps a pole: never cull
    return { g: { type: 'Polygon', coordinates: coords }, c, r: mr };
  });
}
const L110 = decodeLayer(LAND.l110, LAND.q110), L50 = decodeLayer(LAND.l50, LAND.q50);
let L10 = null;
const inBox = (lat, lon, b, m = 0) => lon > b[0] + m && lon < b[2] - m && lat > b[1] + m && lat < b[3] - m;
function landLayer() {
  const m = mpp();
  if (m > 2600) return L110;
  if (m > 330) return L50;
  if (inBox(cam.lat, cam.lon, LAND.box, 2)) { if (!L10) L10 = decodeLayer(LAND.l10, LAND.q10); return L10; }
  return L50;
}
function viewBox(vr) { // lon/lat box around the view, with a margin
  const d = vr * R2D * 1.3 + 0.05, cl = Math.cos(Math.min(85, Math.abs(cam.lat) + d) * D2R);
  return [cam.lon - d / cl, cam.lat - d, cam.lon + d / cl, cam.lat + d];
}
function clipRing(pts, b) { // Sutherland-Hodgman against a lon/lat rectangle (fine for small boxes)
  const E = [[p => p[0] >= b[0], (a, c) => ix(a, c, 0, b[0])], [p => p[0] <= b[2], (a, c) => ix(a, c, 0, b[2])], [p => p[1] >= b[1], (a, c) => ix(a, c, 1, b[1])], [p => p[1] <= b[3], (a, c) => ix(a, c, 1, b[3])]];
  function ix(a, c, k, v) { const t = (v - a[k]) / (c[k] - a[k]); return [a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t]; }
  let q = pts.slice(0, -1);
  for (const [inside, inter] of E) { const r = []; for (let i = 0; i < q.length; i++) { const cur = q[i], prev = q[(i + q.length - 1) % q.length], ci = inside(cur), pi = inside(prev); if (ci) { if (!pi) r.push(inter(prev, cur)); r.push(cur); } else if (pi) r.push(inter(prev, cur)); } q = r; if (!q.length) return null; }
  if (q.length < 3) return null; q.push(q[0]); return q;
}
function clipPoly(g, b) {
  const rings = []; for (let i = 0; i < g.coordinates.length; i++) { const r = clipRing(g.coordinates[i], b); if (r) rings.push(r); else if (i === 0) return null; }
  return { type: 'Polygon', coordinates: rings };
}
const P = d3.geoOrthographic().clipAngle(90).precision(0.6);
const landCv = document.createElement('canvas'); landCv.width = SW; landCv.height = SH; const lctx = landCv.getContext('2d');
let landKey = '';
function sunPos(ms) {
  const d = new Date(ms), y0 = Date.UTC(d.getUTCFullYear(), 0, 0), doy = (ms - y0) / 864e5;
  const dec = 23.44 * Math.sin(2 * Math.PI * (284 + doy) / 365), h = (ms % 864e5) / 36e5;
  return [dec, ((12 - h) * 15 + 540) % 360 - 180];
}
function sunAlt(lat, lon) { const [a, b] = sunPos(worldMs); return Math.asin(clamp(dot(v3(lat, lon), v3(a, b)), -1, 1)) * R2D; }
function niceStep(deg) { const S = [30, 15, 10, 5, 2, 1, .5, 1 / 4, 1 / 6, 1 / 12, 1 / 30, 1 / 60, 1 / 120]; for (const s of S) if (s <= deg) return s; return 1 / 120; }
let gratStep = 10;
function drawBase() {
  const m = mpp(), nightKey = Math.floor(worldMs / 300000);
  const key = [cam.lat.toFixed(7), cam.lon.toFixed(7), cam.s.toPrecision(7), m > 500 ? nightKey : 0, lens.night].join('|');
  if (key === landKey) return;
  landKey = key;
  const c = lctx;
  c.setTransform(1, 0, 0, 1, 0, 0);
  c.fillStyle = '#06090c'; c.fillRect(0, 0, SW, SH);
  P.rotate([-cam.lon, -cam.lat]).scale(cam.s).translate([MAP.cx, MAP.cy]).clipExtent([[-4, -4], [SW + 4, SH + 4]]);
  const path = d3.geoPath(P, c);
  // sea
  if (cam.s < 2600) {
    const g = c.createRadialGradient(MAP.cx - cam.s * .3, MAP.cy - cam.s * .35, cam.s * .1, MAP.cx, MAP.cy, cam.s);
    g.addColorStop(0, '#173041'); g.addColorStop(1, '#0c1a25');
    // atmosphere
    const a = c.createRadialGradient(MAP.cx, MAP.cy, cam.s * .98, MAP.cx, MAP.cy, cam.s * 1.09);
    a.addColorStop(0, 'rgba(90,155,224,.32)'); a.addColorStop(1, 'rgba(90,155,224,0)');
    c.fillStyle = a; c.beginPath(); c.arc(MAP.cx, MAP.cy, cam.s * 1.1, 0, 7); c.fill();
    c.fillStyle = g; c.beginPath(); c.arc(MAP.cx, MAP.cy, cam.s, 0, 7); c.fill();
  } else {
    c.fillStyle = m < 40 ? '#12293a' : '#11263a'; c.fillRect(0, 0, SW, SH);
  }
  // land
  const Lr = landLayer(), vr = viewRadius() + 0.02;
  c.beginPath();
  // zoomed in: no adaptive resampling (the data is denser than the screen), and cut each ring to the view's lon/lat box
  P.precision(m < 3000 ? 0 : 0.6);
  const box = vr < 0.6 ? viewBox(vr) : null;
  for (const p of Lr) {
    if (p.r < Math.PI && Math.acos(clamp(dot(p.c, B.c), -1, 1)) - p.r > vr) continue;
    if (box) { const g = clipPoly(p.g, box); if (g) path(g); } else path(p.g);
  }
  c.fillStyle = m < 40 ? '#2c3128' : '#272c27'; c.fill('evenodd');
  c.strokeStyle = m < 40 ? '#6d7a66' : '#56634f'; c.lineWidth = m < 40 ? 1.4 : 0.9; c.stroke();
  // graticule
  const st = niceStep(m * 260 / 111320); gratStep = st;
  const rDeg = Math.min(vr, viewRadius() * 1.25 + 1e-4) * R2D;
  const la0 = Math.max(-85, cam.lat - rDeg), la1 = Math.min(85, cam.lat + rDeg);
  const lw = rDeg / Math.max(0.05, Math.cos(Math.min(85, Math.abs(cam.lat) + rDeg) * D2R));
  const fullLon = lw > 170;
  const lo0 = fullLon ? -180 : cam.lon - lw, lo1 = fullLon ? 180 : cam.lon + lw;
  const lines = [];
  const sub = rDeg < 3 ? Math.max(st, rDeg) : st / 4; // short lines need no intermediate points
  for (let lo = Math.ceil(lo0 / st) * st; lo <= lo1 + 1e-9; lo += st) { const l = []; for (let la = Math.floor(la0 / sub) * sub; la <= la1 + sub; la += sub) l.push([lo, clamp(la, -89.9, 89.9)]); lines.push(l); }
  for (let la = Math.ceil(la0 / st) * st; la <= la1 + 1e-9; la += st) { const l = []; for (let lo = lo0; lo <= lo1 + sub; lo += sub) l.push([Math.min(lo, lo1), la]); lines.push(l); }
  c.beginPath(); path({ type: 'MultiLineString', coordinates: lines });
  c.strokeStyle = m > 500 ? 'rgba(120,170,210,.13)' : 'rgba(120,170,210,.11)'; c.lineWidth = 1; c.stroke();
  // night
  if (lens.night && m > 300) {
    const [dec, slon] = sunPos(worldMs), anti = [slon + 180, -dec];
    for (const [r, a] of [[90, .24], [87, .1], [84, .07]]) { c.beginPath(); path(d3.geoCircle().center(anti).radius(r)()); c.fillStyle = `rgba(2,6,14,${a})`; c.fill(); }
  }
}

// ---------------------------------------------------------------- render state
const lens = { mode: 'ops', orders: false, ranges: true, fire: true, night: true, labels: true };
let hits = [], hover = null, sel = null, hoverRow = null;
let mouse = { x: -1, y: -1, geo: null, in: false };
let tabHeld = false;
const COL = { F: '#5B9BE0', A: '#9DBFE6', H: '#E2685C', N: '#6FC08A', U: '#E5C454', brass: '#D9B868', amber: '#E3A452' };
function affCol(u) { return u.aff === 'H' ? COL.H : u.aff === 'N' ? COL.N : u.ally ? COL.A : COL.F; }
const sprite = new Image(); sprite.src = '../designer-ui/assets/dreadnought.png';

let occ = [];
function claimBox(x, y, w, h, force) { if (!force) for (const r of occ) if (x < r.x + r.w && r.x < x + w && y < r.y + r.h && r.y < y + h) return false; occ.push({ x, y, w, h }); return true; }
function textW(t, font) { ctx.save(); ctx.font = font; const w = ctx.measureText(t).width; ctx.restore(); return w; }
function pipsW(cp, est) { const cap = est ? est.cap[1] : cp.cap, cru = est ? est.cru[1] : cp.cru, lt = est ? est.lt[1] : cp.lt; return Math.min(cap, 40) * 4 + (cap && cru ? 3 : 0) + Math.min(cru, 30) * 3 + (lt ? 3 + Math.min(60, lt * 1.2) : 0); }
// a symbol's two-line label (name + strength pips): right of the symbol, else left, else dropped (hover still has it)
function symLabel(c, x, y, name, cp, col, est, opt = {}) {
  const font = opt.font || '600 12px "Archivo Narrow"', w = Math.max(textW(name, font), opt.text2 ? textW(opt.text2, '10px "IBM Plex Mono"') : pipsW(cp, est)) + 4, h = 25;
  const cands = opt.left ? [[x - 14 - w, y], [x + 14, y]] : [[x + 14, y], [x - 14 - w, y]];
  cands.push([x - w / 2, y - 24], [x - w / 2, y + 26]);
  const second = (lx, ly) => { if (opt.text2) label(c, lx, ly + 12, opt.text2, { font: '10px "IBM Plex Mono"', col: '#9fb0bc' }); else pips(c, lx, ly + 3, cp, col, est); };
  for (const [lx, ly] of cands) if (claimBox(lx, ly - 13, w, h)) { label(c, lx, ly - 2, name, { font, col: opt.tcol || col }); second(lx, ly); return true; }
  if (opt.force) { label(c, cands[0][0], y - 2, name, { font, col: opt.tcol || col }); second(cands[0][0], y); }
  return false;
}
function inMap(x, y, m = 0) { return x > MAP.x0 + m && x < MAP.x1 - m && y > MAP.y0 + m && y < MAP.y1 - m; }

function symbol(c, x, y, r, u, opt = {}) {
  const col = opt.col || affCol(u);
  c.save(); c.lineWidth = opt.lw || 1.8; c.strokeStyle = col;
  if (opt.dash) c.setLineDash([3, 2]);
  c.fillStyle = opt.fill || (u.aff === 'H' ? 'rgba(70,24,20,.9)' : u.aff === 'N' ? 'rgba(20,50,30,.9)' : 'rgba(16,36,56,.92)');
  c.beginPath();
  if (u.aff === 'H') { c.moveTo(x, y - r * 1.2); c.lineTo(x + r * 1.2, y); c.lineTo(x, y + r * 1.2); c.lineTo(x - r * 1.2, y); c.closePath(); }
  else if (u.aff === 'N') c.rect(x - r, y - r, 2 * r, 2 * r);
  else c.arc(x, y, r, 0, 7);
  if (!opt.dash) c.fill(); c.stroke();
  c.setLineDash([]);
  if (opt.txt !== undefined) { c.fillStyle = '#EAF0F4'; c.font = `600 ${opt.fs || 10}px "IBM Plex Mono"`; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(opt.txt, x, y + 0.5); }
  c.restore();
}
function pips(c, x, y, cp, col, est) { // capital pips, cruiser pips, light-forces bar
  c.save(); c.fillStyle = col; let px = x;
  const capMax = est ? est.cap[1] : cp.cap, capMin = est ? est.cap[0] : cp.cap;
  for (let i = 0; i < Math.min(capMax, 40); i++) { if (i < capMin) c.fillRect(px, y, 3, 8); else { c.strokeStyle = col; c.lineWidth = 1; c.strokeRect(px + .5, y + .5, 2, 7); } px += 4; }
  if (capMax > 40) { c.font = '9px "IBM Plex Mono"'; c.fillText('+', px, y + 7); px += 6; }
  const cru = est ? est.cru[1] : cp.cru; if (capMax && cru) px += 3;
  c.globalAlpha = .85; for (let i = 0; i < Math.min(cru, 30); i++) { c.fillRect(px, y + 2, 2, 6); px += 3; }
  const lt = est ? est.lt[1] : cp.lt; if (lt) { px += 3; c.globalAlpha = .65; c.fillRect(px, y + 6, Math.min(60, lt * 1.2), 2); px += Math.min(60, lt * 1.2); }
  c.restore(); return px - x;
}
function label(c, x, y, txt, opt = {}) {
  c.save(); c.font = opt.font || '600 12px "Archivo Narrow"'; c.textBaseline = 'alphabetic'; c.textAlign = opt.align || 'left';
  if (opt.bg) { const w = c.measureText(txt).width; const lx = opt.align === 'center' ? x - w / 2 : x; c.fillStyle = opt.bg; c.fillRect(lx - 3, y - 11, w + 6, 14); }
  if (!opt.bg) { c.lineWidth = 3; c.strokeStyle = 'rgba(8,12,16,.85)'; c.strokeText(txt, x, y); }
  c.fillStyle = opt.col || '#EAF0F4'; c.fillText(txt, x, y); c.restore();
}
function geoLine(c, a, b, n = 24) { // great circle polyline, returns false if any point hidden
  const va = v3(a.lat, a.lon), vb = v3(b.lat, b.lon), ang = Math.acos(clamp(dot(va, vb), -1, 1));
  let started = false;
  for (let i = 0; i <= n; i++) {
    const t = i / n; let v;
    if (ang < 1e-9) v = va; else { const s = Math.sin(ang), k0 = Math.sin((1 - t) * ang) / s, k1 = Math.sin(t * ang) / s; v = [va[0] * k0 + vb[0] * k1, va[1] * k0 + vb[1] * k1, va[2] * k0 + vb[2] * k1]; }
    const p = projV(v); if (!p.vis) { started = false; continue; }
    if (!started) { c.moveTo(p.x, p.y); started = true; } else c.lineTo(p.x, p.y);
  }
}
function geoCircle(c, lat, lon, rM, n = 72) {
  let started = false;
  for (let i = 0; i <= n; i++) { const p0 = dest(lat, lon, i / n * 360, rM), p = proj(p0[0], p0[1]); if (!p.vis) { started = false; continue; } if (!started) { c.moveTo(p.x, p.y); started = true; } else c.lineTo(p.x, p.y); }
}
function shipAngle(lat, lon, hdg) { // screen angle of a heading (handles globe tilt)
  const a = proj(lat, lon), p = dest(lat, lon, hdg, 2000 * Math.max(1, mpp())), b = proj(p[0], p[1]);
  return Math.atan2(b.y - a.y, b.x - a.x);
}

// ---------------------------------------------------------------- per-frame drawing
function drawHull(c, x, y, ang, L, Bm, col, opt = {}) {
  c.save(); c.translate(x, y); c.rotate(ang);
  const l = L / 2, b = Bm / 2;
  c.beginPath(); c.moveTo(l, 0); c.quadraticCurveTo(l * .55, -b * 1.05, l * .1, -b); c.lineTo(-l * .85, -b * .92); c.quadraticCurveTo(-l, -b * .7, -l, 0); c.quadraticCurveTo(-l, b * .7, -l * .85, b * .92); c.lineTo(l * .1, b); c.quadraticCurveTo(l * .55, b * 1.05, l, 0); c.closePath();
  if (opt.dash) { c.setLineDash([3, 2]); c.strokeStyle = col; c.lineWidth = 1.2; c.stroke(); }
  else { c.fillStyle = opt.fill || col; c.fill(); c.strokeStyle = opt.stroke || 'rgba(0,0,0,.55)'; c.lineWidth = 1; c.stroke(); }
  c.restore();
}
function drawVectorShip(c, x, y, ang, Lpx, sh, alpha) { // detailed light-ship look for the ship band
  const t = sh.type, Bpx = Lpx * (t === 'MV' ? 0.14 : t === 'SS' ? 0.09 : t === 'CA' ? 0.14 : 0.105);
  c.save(); c.globalAlpha = alpha; c.translate(x, y); c.rotate(ang);
  const l = Lpx / 2, b = Bpx / 2;
  c.beginPath(); c.moveTo(l, 0); c.quadraticCurveTo(l * .5, -b * 1.05, l * .05, -b); c.lineTo(-l * .9, -b * .9); c.quadraticCurveTo(-l * 1.01, 0, -l * .9, b * .9); c.lineTo(l * .05, b); c.quadraticCurveTo(l * .5, b * 1.05, l, 0); c.closePath();
  const enemy = sh.u.aff === 'H';
  c.fillStyle = t === 'MV' ? '#2b2522' : t === 'SS' ? '#25292c' : enemy ? '#6f7377' : '#7b8188'; c.fill();
  c.strokeStyle = '#1b1e21'; c.lineWidth = Math.max(1, Lpx / 300); c.stroke();
  if (t !== 'SS') {
    c.fillStyle = t === 'MV' ? '#6b5a48' : '#a59b88'; c.beginPath(); c.moveTo(l * .82, 0); c.quadraticCurveTo(l * .45, -b * .82, 0, -b * .8); c.lineTo(-l * .82, -b * .74); c.lineTo(-l * .82, b * .74); c.lineTo(0, b * .8); c.quadraticCurveTo(l * .45, b * .82, l * .82, 0); c.fill();
    c.fillStyle = t === 'MV' ? '#d8d2c6' : '#8d939a'; c.fillRect(-l * .18, -b * .55, l * .62, b * 1.1);
    const nf = t === 'MV' ? 1 : t === 'CA' ? 4 : t === 'CL' ? 3 : t === 'DD' || t === 'DL' ? 3 : 1;
    c.fillStyle = t === 'MV' ? '#2a2a2a' : '#2f3337';
    for (let i = 0; i < nf; i++) { const fx = l * 0.3 - i * (l * 0.25); c.beginPath(); c.ellipse(fx, 0, l * 0.06, b * 0.42, 0, 0, 7); c.fill(); }
    if (t !== 'MV') { c.fillStyle = '#5d646b'; for (const gx of [0.72, -0.72]) { c.beginPath(); c.arc(l * gx, 0, b * 0.38, 0, 7); c.fill(); c.fillRect(gx > 0 ? l * gx : l * gx - b * 0.9, -b * 0.06, b * 0.9, b * 0.12); } }
  }
  c.restore();
}
function drawWake(c, x, y, ang, Lpx, kn, mppv) {
  if (!kn) return;
  const len = kn * 26 / mppv;
  c.save(); c.translate(x, y); c.rotate(ang); // ship frame: bow +x
  if (mppv < 4) {
    const Bp = Lpx * 0.16, k = Math.tan(19.5 * D2R), x0 = Lpx * 0.42, L2 = len + Lpx;
    for (const sg of [-1, 1]) { // Kelvin arms from the bow shoulders
      const g = c.createLinearGradient(x0, 0, x0 - L2, 0); g.addColorStop(0, 'rgba(225,238,246,.32)'); g.addColorStop(.6, 'rgba(225,238,246,.1)'); g.addColorStop(1, 'rgba(225,238,246,0)');
      c.strokeStyle = g; c.lineWidth = Math.max(1.5, Lpx * 0.01); c.beginPath(); c.moveTo(x0, sg * Bp * 0.45); c.lineTo(x0 - L2, sg * (Bp * 0.45 + L2 * k)); c.stroke();
    }
    const g2 = c.createLinearGradient(-Lpx * 0.5, 0, -Lpx * 0.5 - len * 0.8, 0); g2.addColorStop(0, 'rgba(210,228,240,.28)'); g2.addColorStop(1, 'rgba(210,228,240,0)');
    c.fillStyle = g2; c.beginPath(); c.moveTo(-Lpx * 0.45, -Bp * 0.45); c.lineTo(-Lpx * 0.5 - len * 0.8, -Bp * 1.6); c.lineTo(-Lpx * 0.5 - len * 0.8, Bp * 1.6); c.lineTo(-Lpx * 0.45, Bp * 0.45); c.closePath(); c.fill();
    c.strokeStyle = 'rgba(225,238,246,.35)'; c.lineWidth = 1.5; c.beginPath(); c.ellipse(Lpx * 0.44, 0, Lpx * 0.07, Bp * 0.58, 0, -1.3, 1.3); c.stroke(); // bow wave
  } else {
    const g = c.createLinearGradient(-Lpx / 2, 0, -Lpx / 2 - len, 0); g.addColorStop(0, 'rgba(200,225,240,.35)'); g.addColorStop(1, 'rgba(200,225,240,0)');
    c.strokeStyle = g; c.lineWidth = 1.5; c.beginPath(); c.moveTo(-Lpx / 2, 0); c.lineTo(-Lpx / 2 - len, 0); c.stroke();
  }
  c.restore();
}
function softBlob(c, x, y, rx, ry, rot, col, a = 0.28) {
  c.save(); c.translate(x, y); c.rotate(rot); c.scale(1, ry / rx);
  const g = c.createRadialGradient(0, 0, 0, 0, 0, rx); g.addColorStop(0, col.replace('A', a)); g.addColorStop(0.6, col.replace('A', a * 0.55)); g.addColorStop(1, col.replace('A', 0));
  c.fillStyle = g; c.beginPath(); c.arc(0, 0, rx, 0, 7); c.fill(); c.restore();
}

function drawContacts(c, bi) {
  const m = mpp(), focus = lens.mode === 'intel';
  for (const k of contacts) {
    if (k.kind === 'df' && bi >= 1 && bi <= 3) { const wedges = bi >= 2;
      const tr = unitById[k.truth]; const fix = { lat: tr.lat + k.err[0] * 0.09, lon: tr.lon + k.err[1] * 0.09 };
      if (wedges) for (const st of k.stations) {
        const o = { lat: st.at[0], lon: st.at[1] }, b0 = brg(o, fix);
        c.beginPath();
        const far = hav(o, fix) + 140000; const pA = dest(o.lat, o.lon, b0 - k.width / 2, far), pB = dest(o.lat, o.lon, b0 + k.width / 2, far);
        const so = proj(o.lat, o.lon), sa = proj(pA[0], pA[1]), sb = proj(pB[0], pB[1]);
        if (so.vis && sa.vis && sb.vis) {
          const g = c.createLinearGradient(so.x, so.y, (sa.x + sb.x) / 2, (sa.y + sb.y) / 2); g.addColorStop(0, 'rgba(226,104,92,.02)'); g.addColorStop(.7, `rgba(226,104,92,${focus ? .14 : .07})`); g.addColorStop(1, 'rgba(226,104,92,0)');
          c.moveTo(so.x, so.y); c.lineTo(sa.x, sa.y); c.lineTo(sb.x, sb.y); c.closePath(); c.fillStyle = g; c.fill();
          c.setLineDash([2, 4]); c.strokeStyle = 'rgba(226,104,92,.35)'; c.lineWidth = 1; c.beginPath(); c.moveTo(so.x, so.y); c.lineTo((sa.x + sb.x) / 2, (sa.y + sb.y) / 2); c.stroke(); c.setLineDash([]);
          if (bi >= 2 && inMap(so.x, so.y)) label(c, so.x + 6, so.y - 6, `${st.name} ${fmtBrg(b0)}`, { font: '10px "IBM Plex Mono"', col: '#e79a91' });
        }
      }
      const p = proj(fix.lat, fix.lon);
      if (p.vis) {
        const rr = Math.max(10, 28000 / m);
        softBlob(c, p.x, p.y, rr, rr * 0.8, 0.4, 'rgba(226,104,92,A)', focus ? .4 : .28);
        symbol(c, p.x, p.y, 6, { aff: 'H' }, { dash: 1, txt: '' }); label(c, p.x, p.y + 4, '?', { font: '700 10px "IBM Plex Mono"', align: 'center', col: '#ffb3a9' });
        hits.push({ x: p.x, y: p.y, r: Math.max(12, rr * .5), k: 'contact', ref: k });
        if (bi >= 2 || focus) cLabel(c, p.x, p.y, [`DK · DF fix ±15 nm · ${k.age} min`]);
      }
    }
    if (k.kind === 'ellipse' && bi <= 2) {
      const from = unitById[k.from]; const mid = (k.rng[0] + k.rng[1]) / 2 * 1000; const ce = dest(from.lat, from.lon, k.brg, mid);
      const p = proj(ce[0], ce[1]), o = proj(from.lat, from.lon); if (!p.vis) continue;
      const rx = (k.rng[1] - k.rng[0]) * 500 / m, ry = Math.max(6, 2600 / m), rot = shipAngle(ce[0], ce[1], k.brg);
      softBlob(c, p.x, p.y, Math.max(rx, 8), Math.max(ry, 5), rot, 'rgba(226,104,92,A)', focus ? .42 : .3);
      c.setLineDash([1, 4]); c.strokeStyle = 'rgba(229,196,84,.45)'; c.lineWidth = 1; c.beginPath(); c.moveTo(o.x, o.y); c.lineTo(p.x, p.y); c.stroke(); c.setLineDash([]);
      symbol(c, p.x, p.y, 7, { aff: 'H' }, { dash: 1 }); label(c, p.x, p.y + 4, '?', { font: '700 11px "IBM Plex Mono"', align: 'center', col: '#ffb3a9' });
      // fan of possible tracks
      c.strokeStyle = 'rgba(226,104,92,.22)'; c.lineWidth = 1;
      for (const dh of [-40, -15, 10, 35]) { const q = dest(ce[0], ce[1], 330 + dh, 20000), s = proj(q[0], q[1]); c.beginPath(); c.moveTo(p.x, p.y); c.lineTo(s.x, s.y); c.stroke(); }
      hits.push({ x: p.x, y: p.y, r: 14, k: 'contact', ref: k });
      cLabel(c, p.x, p.y, ['HEAVY SHIPS? 6–16 · smoke', `visual · Southampton · ${k.age} min · range ±7 km`]);
    }
    if (k.kind === 'blob' && bi >= 2) {
      const p = proj(k.at[0], k.at[1]); if (!p.vis) continue;
      const rr = Math.max(14, k.r * 1000 / m), fadeA = clamp(1 - k.age / 20000, .35, 1);
      softBlob(c, p.x, p.y, rr, rr, 0, 'rgba(226,104,92,A)', (focus ? .3 : .2) * fadeA);
      c.globalAlpha = fadeA; symbol(c, p.x, p.y, 6, { aff: 'H' }, { dash: 1 }); c.globalAlpha = 1;
      hits.push({ x: p.x, y: p.y, r: 12, k: 'contact', ref: k });
      if (bi <= 3 || focus) cLabel(c, p.x, p.y, [`${k.title} · ${fmtAge(k.age)}`]);
    }
  }
}
function cLabel(c, x, y, lines) {
  const f1 = lines.length > 1 ? '600 11px "Archivo Narrow"' : '10.5px "IBM Plex Mono"', f2 = '10px "IBM Plex Mono"';
  const w = Math.max(...lines.map((t, i) => textW(t, i ? f2 : f1))) + 4, h = lines.length * 13 + 2;
  for (const [lx, ly] of [[x + 12, y - 8], [x - 12 - w, y - 8], [x - w / 2, y + 12], [x - w / 2, y - 14 - h]]) if (claimBox(lx, ly - 4, w, h)) {
    lines.forEach((t, i) => label(c, lx, ly + 8 + i * 13, t, { font: i ? f2 : f1, col: i ? '#c98c84' : '#f3b0a7' })); return; }
}
function fmtAge(min) { return min < 90 ? `${min} min` : min < 2880 ? `${Math.round(min / 60)} h` : `${Math.round(min / 1440)} d`; }

function drawPorts(c, bi) {
  const drawn = [];
  for (const p of PORTS) {
    const s = proj(p.at[0], p.at[1]); if (!s.vis || !inMap(s.x, s.y, -40)) continue;
    const here = units.filter(u => u.port === p.n);
    const own = here.filter(u => u.own);
    const docked = own.filter(u => u.status === 'DOCK').length, building = own.filter(u => u.status === 'BUILD' || u.status === 'FIT').reduce((a, u) => a + u.ships.length, 0);
    const ownShips = own.reduce((a, u) => a + u.ships.length, 0) - building;
    const intel = contacts.find(k => k.kind === 'port' && k.title.startsWith(p.n.split(' ')[0]));
    const allyShips = here.filter(u => !u.own).reduce((a, u) => a + u.ships.length, 0);
    if (bi === 4 && !ownShips && !intel && !allyShips && !building) continue;
    if (bi === 3 && !p.big && !ownShips && !building && !intel && !allyShips && !(lens.mode === 'yards' && p.yard)) continue;
    // merge with a port already drawn close by (the Clyde yards, say)
    const near = drawn.find(d => Math.hypot(d.x - s.x, d.y - s.y) < (bi >= 3 ? 16 : bi === 2 ? 16 : 6));
    if (near) { near.extra.push(p.n); near.own += ownShips; near.bld += building; continue; }
    drawn.push({ p, x: s.x, y: s.y, own: ownShips, bld: building, docked, intel, allyShips, extra: [] });
  }
  for (const d of drawn) {
    const p = d.p, s = d;
    const col = p.side === 'RN' ? '#9fc3ec' : SIDES[p.side].aff === 'H' ? '#f0a79d' : SIDES[p.side].aff === 'N' ? '#9ad7ac' : '#bcd3ec';
    c.save(); c.fillStyle = col; c.strokeStyle = '#0a0d10'; c.lineWidth = 1;
    if (mpp() < 8 && (p.docks || p.yard)) {} else if (SIDES[p.side].aff === 'H') { c.beginPath(); c.moveTo(s.x, s.y - 5); c.lineTo(s.x + 5, s.y); c.lineTo(s.x, s.y + 5); c.lineTo(s.x - 5, s.y); c.closePath(); c.fill(); c.stroke(); }
    else { c.beginPath(); c.rect(s.x - 3.5, s.y - 3.5, 7, 7); c.fill(); c.stroke(); }
    c.restore();
    hits.push({ x: s.x, y: s.y, r: 9, k: 'port', ref: p });
    if (bi === 4) continue; // globe: marks only, the sea-area cards carry the numbers
    const name = d.extra.length ? (p.grp ? p.grp : p.n + ' +' + d.extra.length) : p.n;
    const font = (bi <= 1 ? '600 12px' : '500 11px') + ' "Archivo Narrow"';
    const parts = [name]; if (d.own) parts.push(`⚓${d.own}`); if (d.docked) parts.push(`▣${d.docked} dock`); if (d.bld) parts.push(`⚒${d.bld}`); if (d.allyShips) parts.push(`⚓${d.allyShips}`); if (d.intel) parts.push(`~${d.intel.est.cap[0]}–${d.intel.est.cap[1]} cap · ${fmtAge(d.intel.age)}`);
    const tw = textW(name, font) + 6 + parts.slice(1).reduce((a, t) => a + textW(t, '10.5px "IBM Plex Mono"') + 8, 0);
    let lx = s.x + 8, ly = s.y + 4;
    if (mpp() < 8 && (p.docks || p.yard)) { const q = offLL(p.at[0], p.at[1], 0, 240, -440), qp = proj(q[0], q[1]); lx = qp.x + 8; ly = qp.y + 14; c.save(); c.fillStyle = col; c.fillRect(qp.x - 3, qp.y + 6, 6, 6); c.restore(); }
    if (!claimBox(lx, ly - 11, tw, 14)) { lx = s.x - 8 - tw; if (!claimBox(lx, ly - 11, tw, 14)) { if (!d.own && !d.intel) continue; lx = s.x + 8; } }
    label(c, lx, ly, name, { font, col });
    c.save(); c.font = font; lx += c.measureText(name).width + 6; c.restore();
    const put = (t, col2, bg) => { label(c, lx, ly, t, { font: '10.5px "IBM Plex Mono"', col: col2, bg }); c.save(); c.font = '10.5px "IBM Plex Mono"'; lx += c.measureText(t).width + 8; c.restore(); };
    if (d.own) put(`⚓${d.own}`, '#cfe0f1', 'rgba(16,36,56,.85)');
    if (d.docked) put(`▣${d.docked} dock`, '#e8cf91', 'rgba(58,48,23,.85)');
    if (d.bld) put(`⚒${d.bld}`, '#c6b7ea', 'rgba(43,36,64,.85)');
    if (d.allyShips) put(`⚓${d.allyShips}`, '#bcd3ec', 'rgba(16,36,56,.6)');
    if (d.intel) { c.save(); c.globalAlpha = clamp(1 - d.intel.age / 30000, .5, 1); put(`~${d.intel.est.cap[0]}–${d.intel.est.cap[1]} cap · ${fmtAge(d.intel.age)}`, '#ffd1ca', 'rgba(70,24,20,.85)'); c.restore(); }
    if (lens.mode === 'supply' && p.side === 'RN' && p.big) label(c, s.x + 8, s.y + 17, `coal ${Math.round(40 + hash(p.n) * 160)} kt · oil ${Math.round(8 + hash(p.n + 'o') * 60)} kt`, { font: '10px "IBM Plex Mono"', col: '#9fd3a0' });
  }
}

function clusterItems(bi) {
  const items = [];
  for (const u of units) {
    if (!atSea(u)) continue;
    if (isEnemy(u) && !knownEnemy(u)) continue;
    const p = proj(u.lat, u.lon); if (!p.vis) continue;
    items.push({ u, x: p.x, y: p.y, key: u.own ? 'own' : u.aff === 'H' ? 'en' : u.neutral ? 'ne' : 'al', w: u.comp.t });
  }
  items.sort((a, b) => b.w - a.w);
  const rc = bi === 4 ? 30 : 22, cl = [];
  for (const it of items) {
    let best = null;
    for (const k of cl) if (k.key === it.key && Math.hypot(k.x - it.x, k.y - it.y) < rc) { best = k; break; }
    if (best) best.items.push(it); else cl.push({ key: it.key, x: it.x, y: it.y, items: [it] });
  }
  return cl;
}

function drawUnitsFar(c, bi) { // globe + theatre: clustered strength markers
  const cl = clusterItems(bi);
  for (const k of cl) claimBox(k.x - 10, k.y - 10, 20, 20, true);
  cl.sort((a, b) => (b.key === 'own') - (a.key === 'own') || comp(b.items.flatMap(i => i.u.ships)).t - comp(a.items.flatMap(i => i.u.ships)).t);
  for (const k of cl) {
    const us = k.items.map(i => i.u), u0 = us[0];
    const ships = us.flatMap(u => u.ships), cp = comp(ships);
    const enemyEst = u0.aff === 'H';
    const fleetsIn = [...new Set(us.map(u => u.fleet))];
    const FS = { 'Battlecruiser Fleet': 'BCF', 'Grand Fleet': 'Grand Fleet', 'Hochseeflotte': 'HSF' };
    const name = us.length === 1 ? (bi === 4 ? u0.short : u0.name) : fleetsIn.length === 1 ? fleetsIn[0] : fleetsIn.length === 2 ? fleetsIn.map(f => FS[f] || f).join(' + ') : `${FS[fleetsIn[0]] || fleetsIn[0]} +${fleetsIn.length - 1} fleets`;
    const isSel = sel && ((sel.k === 'unit' && us.includes(sel.ref)) || (sel.k === 'fleet' && us.some(u => u.fleet === sel.ref.name)) || (sel.k === 'cluster' && sel.ref.items.some(i => us.includes(i.u))));
    const engaged = us.some(u => u.enc);
    if (lens.orders || isSel || tabHeld) for (const u of us) if (u.own && u.wps.length) {
      c.save(); c.strokeStyle = isSel ? 'rgba(217,184,104,.85)' : 'rgba(91,155,224,.65)'; c.setLineDash([4, 3]); c.lineWidth = 1.2; c.beginPath();
      let a = unitPos(u); for (const w of u.wps) { geoLine(c, a, w, 32); a = w; } c.stroke(); c.restore();
    }
    if (bi === 3) for (const u of us) if (u.kn) {
      const q = dest(u.lat, u.lon, u.hdg, u.kn * 0.5144 * 3600 * 6), a = proj(u.lat, u.lon), b = proj(q[0], q[1]);
      c.save(); c.strokeStyle = affCol(u); c.globalAlpha = .7; c.lineWidth = 1.2; c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke(); c.restore();
    }
    const r = 8;
    if (isSel) { c.save(); c.strokeStyle = COL.brass; c.lineWidth = 2; c.beginPath(); c.arc(k.x, k.y, r + 5, 0, 7); c.stroke(); c.restore(); }
    const capTxt = enemyEst ? '' : cp.cap ? String(cp.cap) : cp.cru ? 'C' : cp.mer ? 'M' : cp.lt ? 'D' : 'A';
    symbol(c, k.x, k.y, r, u0, { txt: capTxt, dash: u0.intel && u0.intel.cert === 'sus', lw: u0.ally ? 1.3 : 1.8, fs: capTxt.length > 1 ? 9 : 10 });
    if (enemyEst) label(c, k.x, k.y + 4, '!', { font: '700 10px "IBM Plex Mono"', align: 'center', col: '#ffd1ca' });
    if (engaged) { c.save(); c.fillStyle = COL.amber; c.beginPath(); c.arc(k.x + r * .8, k.y - r * .8, 3, 0, 7); c.fill(); c.restore(); }
    const important = isSel || engaged || cp.cap >= 3;
    const t2 = bi === 4 ? [cp.cap && `${cp.cap} cap`, cp.cru && `${cp.cru} cr`, cp.lt && `${cp.lt} lt`, cp.mer && `${cp.mer} mv`].filter(Boolean).join(' · ') : null;
    if (lens.labels && (bi === 3 || important)) symLabel(c, k.x, k.y, name, cp, affCol(u0), null, { left: enemyEst, tcol: u0.own ? '#EAF0F4' : affCol(u0), font: `600 ${bi === 4 ? 11 : 12}px "Archivo Narrow"`, force: isSel || (u0.own && cp.cap >= 8), text2: t2 });
    hits.push({ x: k.x, y: k.y, r: 12, k: us.length === 1 ? 'unit' : 'cluster', ref: us.length === 1 ? u0 : k });
  }
}

function drawSeaAreas(c) {
  const cards = [];
  for (const a of SEA_AREAS) {
    const p = proj(a.at[0], a.at[1]); if (!p.vis || !inMap(p.x, p.y, -60)) continue;
    const inA = u => u.lat > a.box[0] && u.lat < a.box[2] && u.lon > a.box[1] && u.lon < a.box[3];
    const own = comp(units.filter(u => u.own && inA(u) && u.status !== 'BUILD' && u.status !== 'FIT').flatMap(u => u.ships));
    const al = comp(units.filter(u => u.ally && u.side !== 'MER' && inA(u)).flatMap(u => u.ships));
    const en = { cap: [0, 0], cru: [0, 0], lt: [0, 0] };
    for (const u of units) if (knownEnemy(u) && inA(u)) { const cp = u.comp; for (const k of ['cap', 'cru', 'lt']) { en[k][0] += cp[k]; en[k][1] += cp[k]; } }
    for (const k of contacts) if (!k.sameAs && (k.area === a.n || (k.area === 'Home waters' && a.n === 'Home waters W'))) { for (const t of ['cap', 'cru', 'lt']) { en[t][0] += k.est[t][0]; en[t][1] += k.est[t][1]; } }
    if (!own.cap && !own.cru && !al.cap && !en.cap[1] && !en.cru[1] && !own.lt) continue;
    const o = a.off || [0, 0]; cards.push({ a, ax: p.x, ay: p.y, x: p.x - 105 + o[0], y: p.y - 22 + o[1], w: 210, h: 44, own, al, en });
  }
  for (let it = 0; it < 30; it++) { let moved = false; // push overlapping cards apart vertically
    for (let i = 0; i < cards.length; i++) for (let j = i + 1; j < cards.length; j++) {
      const A = cards[i], Bc = cards[j];
      if (A.x < Bc.x + Bc.w + 4 && Bc.x < A.x + A.w + 4 && A.y < Bc.y + Bc.h + 4 && Bc.y < A.y + A.h + 4) {
        const dy = (A.y + A.h / 2 <= Bc.y + Bc.h / 2 ? -1 : 1) * 6; A.y += dy; Bc.y -= dy; moved = true;
      }
    }
    if (!moved) break; }
  for (const K of cards) {
    const { a, own, al, en, x, y, w, h } = K;
    c.save();
    if (Math.hypot(K.ax - (x + w / 2), K.ay - (y + h / 2)) > 30) { c.strokeStyle = 'rgba(217,184,104,.5)'; c.beginPath(); c.moveTo(K.ax, K.ay); c.lineTo(clamp(K.ax, x, x + w), clamp(K.ay, y, y + h)); c.stroke(); c.fillStyle = '#D9B868'; c.fillRect(K.ax - 2, K.ay - 2, 4, 4); }
    claimBox(x, y, w, h, true);
    c.fillStyle = 'rgba(14,20,26,.9)'; c.strokeStyle = 'rgba(90,110,125,.7)'; c.lineWidth = 1; c.fillRect(x, y, w, h); c.strokeRect(x + .5, y + .5, w - 1, h - 1);
    c.font = '600 10.5px "Archivo Narrow"'; c.fillStyle = '#D9B868'; c.textAlign = 'left'; c.fillText((a.short || a.label || a.n).toUpperCase(), x + 6, y + 12);
    c.font = '9px "IBM Plex Mono"'; c.fillStyle = '#7F8B95'; c.textAlign = 'right'; c.fillText('capital: own | enemy', x + w - 6, y + 12);
    const mx = x + w / 2, by = y + 17, k = 2.2, maxw = w / 2 - 26;
    c.fillStyle = COL.F; const ow = Math.min(maxw, own.cap * k); c.fillRect(mx - ow, by, ow, 9);
    c.fillStyle = 'rgba(157,191,230,.5)'; const aw = Math.min(maxw - ow, al.cap * k); c.fillRect(mx - ow - aw, by, aw, 9);
    c.fillStyle = COL.H; const e0 = Math.min(maxw, en.cap[0] * k), e1 = Math.min(maxw, en.cap[1] * k); c.fillRect(mx, by, e0, 9);
    if (e1 > e0) { c.strokeStyle = COL.H; c.lineWidth = 1; c.strokeRect(mx + e0 + .5, by + .5, e1 - e0 - 1, 8); for (let hx = mx + e0; hx < mx + e1; hx += 3) { c.beginPath(); c.moveTo(hx, by + 9); c.lineTo(Math.min(hx + 3, mx + e1), by); c.stroke(); } }
    c.fillStyle = '#c9d3db'; c.fillRect(mx - .5, by - 2, 1, 13);
    c.font = '500 11px "IBM Plex Mono"'; c.textAlign = 'right'; c.fillStyle = '#cfe0f1'; c.fillText(String(own.cap) + (al.cap ? `+${al.cap}` : ''), x + 24, by + 9);
    c.textAlign = 'left'; c.fillStyle = '#f3b0a7'; c.fillText(en.cap[0] === en.cap[1] ? String(en.cap[0]) : `${en.cap[0]}–${en.cap[1]}`, x + w - 24, by + 9);
    c.font = '9.5px "IBM Plex Mono"'; c.fillStyle = '#8b98a2'; c.textAlign = 'left'; c.fillText(`${own.cru} cr · ${own.lt} lt`, x + 6, y + h - 5);
    c.textAlign = 'right'; c.fillStyle = '#b98a84'; c.fillText(`${en.cru[0]}–${en.cru[1]} cr · ${en.lt[0]}–${en.lt[1]} lt`, x + w - 6, y + h - 5);
    c.restore();
    hits.push({ x: x + w / 2, y: y + h / 2, r: 0, box: { x, y, w, h }, k: 'area', ref: { a, own, al, en } });
  }
}

function drawUnitsOps(c) { // operations band: one symbol per group (stacked when they overlap), ships as ticks, leaders
  const m = mpp(), vis = [];
  for (const u of units) {
    if (isEnemy(u) && !knownEnemy(u)) continue;
    const p = proj(u.lat, u.lon); if (!p.vis || !inMap(p.x, p.y, -200)) continue;
    const col = affCol(u), isSel = sel && sel.k === 'unit' && sel.ref === u || sel && sel.k === 'ship' && sel.ref.u === u;
    c.save(); c.strokeStyle = col; c.lineWidth = 2; c.globalAlpha = .9;
    for (const sh of u.ships) {
      const q = shipLL(sh), s = proj(q[0], q[1]); const a = shipAngle(q[0], q[1], u.hdg), L = Math.max(sh.T.k === 'cap' ? 5 : 3, sh.T.L / m);
      c.beginPath(); c.moveTo(s.x - Math.cos(a) * L / 2, s.y - Math.sin(a) * L / 2); c.lineTo(s.x + Math.cos(a) * L / 2, s.y + Math.sin(a) * L / 2); c.stroke();
    }
    c.restore();
    if (u.kn && atSea(u)) {
      const q = dest(u.lat, u.lon, u.hdg, u.kn * 0.5144 * 1800), b = proj(q[0], q[1]);
      c.save(); c.strokeStyle = col; c.globalAlpha = .7; c.lineWidth = 1.2; c.beginPath(); c.moveTo(p.x, p.y); c.lineTo(b.x, b.y); c.stroke();
      for (const f of [1 / 3, 2 / 3]) { const tx = lerp(p.x, b.x, f), ty = lerp(p.y, b.y, f), a = Math.atan2(b.y - p.y, b.x - p.x) + Math.PI / 2; c.beginPath(); c.moveTo(tx - Math.cos(a) * 3, ty - Math.sin(a) * 3); c.lineTo(tx + Math.cos(a) * 3, ty + Math.sin(a) * 3); c.stroke(); }
      c.restore();
    }
    drawOrders(c, u, isSel);
    if (atSea(u) || m < 120) vis.push({ u, x: p.x, y: p.y, isSel, key: u.own ? 'own' : u.aff === 'H' ? 'en' : u.neutral ? 'ne' : 'al' });
  }
  vis.sort((a, b) => b.u.comp.t - a.u.comp.t);
  const cl = [];
  for (const v of vis) { const k = cl.find(k => k.key === v.key && Math.hypot(k.x - v.x, k.y - v.y) < 26); if (k) k.items.push(v); else cl.push({ key: v.key, x: v.x, y: v.y, items: [v] }); }
  for (const k of cl) claimBox(k.x - 10, k.y - 10, 20, 20, true);
  for (const k of cl) {
    const us = k.items.map(i => i.u), u = us[0], col = affCol(u), cp = comp(us.flatMap(x => x.ships));
    const unsure = isEnemy(u) && us.some(x => x.intel.cert !== 'id');
    const isSel = k.items.some(i => i.isSel) || (sel && sel.k === 'cluster' && sel.ref.items && sel.ref.items.some(i => us.includes(i.u)));
    if (isSel) { c.save(); c.strokeStyle = COL.brass; c.lineWidth = 2; c.beginPath(); c.arc(k.x, k.y, 15, 0, 7); c.stroke(); c.restore(); }
    if (us.length > 1) { c.save(); c.strokeStyle = col; c.globalAlpha = .6; c.lineWidth = 1.2; c.beginPath(); if (u.aff === 'H') { c.moveTo(k.x + 3, k.y - 13); c.lineTo(k.x + 16, k.y - 1); } else c.arc(k.x + 3, k.y - 3, 10, -1.9, 0.4); c.stroke(); c.restore(); }
    const txt = cp.cap ? String(cp.cap) : cp.mer ? 'M' : cp.cru ? 'C' : cp.lt ? 'D' : 'A';
    symbol(c, k.x, k.y, 8, u, { txt: unsure && !cp.cap ? '?' : txt, dash: unsure && us.length === 1, lw: u.ally ? 1.3 : 1.8, fs: txt.length > 1 ? 9 : 10 });
    if (us.length === 1) modifiers(c, k.x, k.y, u);
    else if (us.some(x => x.enc) && !isEnemy(u)) { const worst = Math.min(...us.flatMap(x => x.ships).map(s => s.flot)); c.save(); c.fillStyle = worst < .6 ? COL.H : worst < .9 ? COL.amber : '#86C77E'; c.fillRect(k.x - 12, k.y + 6, 5, 5); c.restore(); }
    if (lens.labels) {
      const fl = [...new Set(us.map(x => x.fleet))];
      const nm = us.length === 1 ? (unsure ? (u.intel.est || 'unknown') : u.short) : isEnemy(u) ? `${u.intel.cert === 'id' ? u.short : u.intel.est} +${us.length - 1}` : fl.length === 1 ? `${fl[0]} · ${us.length} groups` : `${us.length} groups`;
      symLabel(c, k.x, k.y, nm, cp, col, unsure ? { cap: [us.filter(x => x.intel.cert === 'id').reduce((a, x) => a + x.comp.cap, 0), cp.cap], cru: [0, cp.cru], lt: [0, cp.lt] } : null, { left: isEnemy(u), tcol: u.own ? '#EAF0F4' : col, force: isSel });
    }
    hits.push({ x: k.x, y: k.y, r: 14, k: us.length === 1 ? 'unit' : 'cluster', ref: us.length === 1 ? u : { items: k.items.map(i => ({ u: i.u })), x: k.x, y: k.y } });
  }
}
function modifiers(c, x, y, u) { // fixed slots: damage BL, override TR, order-in-transit BR
  const worst = Math.min(...u.ships.map(s => s.flot));
  if (u.enc && !isEnemy(u)) { c.save(); c.fillStyle = worst < .6 ? COL.H : worst < .9 ? COL.amber : '#86C77E'; c.fillRect(x - 12, y + 6, 5, 5); c.restore(); }
  if (u.over && u.over.length && u.own) { c.save(); c.fillStyle = COL.brass; c.font = '700 9px "IBM Plex Mono"'; c.fillText('±', x + 7, y - 6); c.restore(); }
  if (u.sent) { c.save(); c.fillStyle = COL.amber; c.font = '700 10px "IBM Plex Mono"'; c.fillText('◷', x + 7, y + 13); c.restore(); }
}
function drawOrders(c, u, isSel) {
  if (!u.own || !(lens.orders || isSel || tabHeld)) return;
  const m = mpp();
  if (u.wps.length) {
    c.save(); c.strokeStyle = isSel ? COL.brass : 'rgba(91,155,224,.85)'; c.lineWidth = isSel ? 1.8 : 1.3;
    let a = unitPos(u), t = 0;
    u.wps.forEach((w, i) => {
      c.setLineDash(i ? [3, 4] : []); c.beginPath(); geoLine(c, a, w, 16); c.stroke();
      t += hav(a, w) / (u.kn * 0.5144 || 1); a = w;
      const s = proj(w.lat, w.lon);
      if (s.vis) {
        c.setLineDash([]); c.beginPath(); c.arc(s.x, s.y, 4, 0, 7); c.stroke();
        if (m < 600) { const d = new Date(worldMs + t * 1000); label(c, s.x + 7, s.y - 5, `ETA ${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}`, { font: '10.5px "IBM Plex Mono"', col: isSel ? '#D9B868' : '#9fc3ec' }); }
        hits.push({ x: s.x, y: s.y, r: 7, k: 'wp', ref: { u, i } });
      }
    });
    c.restore();
  }
  if (u.sent) { // the order sent (ghost) vs what's executed (the leader)
    const q = dest(u.lat, u.lon, u.sent.hdg, u.kn * 0.5144 * 1500), a = proj(u.lat, u.lon), b = proj(q[0], q[1]);
    c.save(); c.setLineDash([6, 4]); c.strokeStyle = 'rgba(227,164,82,.9)'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke(); c.restore();
    label(c, b.x + 6, b.y + 4, `sent ${u.sent.hdg}° ${u.sent.at} · in transit ${Math.max(0, Math.round(u.sent.transit))} s`, { font: '10.5px "IBM Plex Mono"', col: '#E3A452' });
  }
}

function salvoSplashes(c, sh, m) { // deterministic in world time: pauses freeze them
  if (!sh.tgt || sh.tgt.sunk) return;
  const per = sh.T.k === 'cap' ? 34 : 14, tS = worldMs / 1000, ph = hash(sh.name) * per;
  const k0 = Math.floor((tS - ph) / per);
  const a = shipLL(sh), b = shipLL(sh.tgt);
  const pa = proj(a[0], a[1]), pb = proj(b[0], b[1]);
  if (lens.fire) {
    c.save(); c.strokeStyle = sh.u.aff === 'H' ? 'rgba(226,104,92,.22)' : 'rgba(91,155,224,.22)'; c.lineWidth = 1; c.setLineDash([2, 4]);
    c.beginPath(); c.moveTo(pa.x, pa.y); c.lineTo(pb.x, pb.y); c.stroke(); c.restore();
  }
  for (let k = k0; k > k0 - 2; k--) {
    const tf = k * per + ph, age = tS - tf, flight = 22;
    if (age >= 0 && age < 1.2) { const g = c.createRadialGradient(pa.x, pa.y, 0, pa.x, pa.y, Math.max(6, 120 / m)); g.addColorStop(0, `rgba(255,190,110,${.8 * (1 - age / 1.2)})`); g.addColorStop(1, 'rgba(255,190,110,0)'); c.fillStyle = g; c.beginPath(); c.arc(pa.x, pa.y, Math.max(6, 120 / m), 0, 7); c.fill(); }
    const sa = age - flight; if (sa < 0 || sa > 9) continue;
    const brgT = brg({ lat: a[0], lon: a[1] }, { lat: b[0], lon: b[1] });
    const n = sh.T.k === 'cap' ? 4 : 2;
    const hit = hash(sh.name + k) > 0.86;
    for (let i = 0; i < n; i++) {
      const r1 = hash(sh.name + k + 'r' + i) - .5, r2 = hash(sh.name + k + 'c' + i) - .5;
      const q = offLL(b[0], b[1], brgT, r1 * 380 + 60, r2 * 90), s = proj(q[0], q[1]);
      const rad = Math.max(1.5, (12 + sa * 3) / m), al = (1 - sa / 9) * 0.85;
      c.fillStyle = `rgba(235,245,250,${al})`; c.beginPath(); c.arc(s.x, s.y, rad, 0, 7); c.fill();
    }
    if (hit && sa < 3) { const g = c.createRadialGradient(pb.x, pb.y, 0, pb.x, pb.y, Math.max(8, 60 / m)); g.addColorStop(0, `rgba(255,150,60,${.9 * (1 - sa / 3)})`); g.addColorStop(1, 'rgba(255,120,40,0)'); c.fillStyle = g; c.beginPath(); c.arc(pb.x, pb.y, Math.max(8, 60 / m), 0, 7); c.fill(); }
  }
}

function drawShipsNear(c, bi) { // tactical + ship bands
  const m = mpp(), aSprite = fade(1.2, 3.2), aHull = 1;
  const drawList = [];
  for (const u of units) {
    if (isEnemy(u) && !knownEnemy(u)) continue;
    const pu = proj(u.lat, u.lon); if (!pu.vis) continue;
    if (Math.hypot(pu.x - MAP.cx, pu.y - MAP.cy) > 3000) continue;
    for (const sh of u.ships) { const q = shipLL(sh), s = proj(q[0], q[1]); if (!inMap(s.x, s.y, -300)) continue; drawList.push({ sh, q, s }); }
  }
  // dockyard ground under docks and slips (until the Earth model has the real coast)
  if (m < 8) for (const p of PORTS) if (p.docks || p.yard) {
    const s0 = proj(p.at[0], p.at[1]); if (!inMap(s0.x, s0.y, -900)) continue;
    const corners = [[-450, -260], [450, -260], [450, 260], [-450, 260]].map(([e, n]) => { const q = offLL(p.at[0], p.at[1], 0, n, e); return proj(q[0], q[1]); });
    c.save(); c.fillStyle = '#2e322b'; c.strokeStyle = '#8a8d80'; c.lineWidth = 1.2; c.beginPath(); corners.forEach((q, i) => i ? c.lineTo(q.x, q.y) : c.moveTo(q.x, q.y)); c.closePath(); c.fill(); c.stroke(); c.restore();
    for (let i = 0; i < Math.min(p.docks || 0, 3); i++) { // the empty docks too: a dock is a place, not a number
      const q = offLL(p.at[0], p.at[1], 0, -i * 75, 0), dp = proj(q[0], q[1]), a = shipAngle(q[0], q[1], 90), L = 260 / m, W = 42 / m;
      c.save(); c.translate(dp.x, dp.y); c.rotate(a); c.fillStyle = i ? '#1b3346' : '#4a4d4f'; c.strokeStyle = '#9a9d92'; c.lineWidth = 1; c.fillRect(-L / 2, -W / 2, L, W); c.strokeRect(-L / 2, -W / 2, L, W); c.restore();
      if (m < 3) label(c, dp.x - L / 2 - 4, dp.y + 4, `No.${i + 1} Dock${i ? ' · free' : ''}`, { font: '10.5px "IBM Plex Mono"', col: i ? '#9fc3ec' : '#e8cf91', align: 'right' });
    }
  }
  // dock and slip furniture
  for (const u of units) if (u.form === 'slip' && m < 30) {
    const p = proj(u.lat, u.lon); if (!inMap(p.x, p.y, -200)) continue;
    const a = shipAngle(u.lat, u.lon, u.hdg), L = (u.ships[0].T.L + 40) / m, W = 42 / m;
    c.save(); c.translate(p.x, p.y); c.rotate(a);
    c.fillStyle = u.form === 'dock' ? '#4a4d4f' : '#3b3a36'; c.strokeStyle = '#8a8d8f'; c.lineWidth = 1;
    c.fillRect(-L / 2, -W / 2, L, W); c.strokeRect(-L / 2, -W / 2, L, W);
    if (u.form === 'slip') { c.strokeStyle = 'rgba(200,190,160,.35)'; for (let i = -2; i <= 2; i++) { c.beginPath(); c.moveTo(-L / 2, i * W / 6); c.lineTo(L / 2, i * W / 6); c.stroke(); } }
    c.restore();
  }
  // wakes first
  for (const d of drawList) {
    const u = d.sh.u; if (!atSea(u) || d.sh.sunk) continue;
    const a = shipAngle(d.q[0], d.q[1], u.hdg); d.a = a; drawWake(c, d.s.x, d.s.y, a, d.sh.T.L / m, u.kn, m);
  }
  // selected-unit range rings
  if (lens.ranges && sel && (sel.k === 'unit' || sel.k === 'ship') && bi <= 1) {
    const u = sel.k === 'unit' ? sel.ref : sel.ref.u, sh = sel.k === 'ship' ? sel.ref : u.ships[0];
    const q = shipLL(sh), rM = MAINRANGE[sh.type] || 0;
    if (rM) {
      c.save(); c.strokeStyle = 'rgba(217,184,104,.55)'; c.setLineDash([5, 4]); c.lineWidth = 1.2; c.beginPath(); geoCircle(c, q[0], q[1], rM); c.stroke();
      const lp = dest(q[0], q[1], 45, rM), s = proj(lp[0], lp[1]); c.setLineDash([]);
      label(c, s.x + 4, s.y, `${TURRETS[sh.cls] ? TURRETS[sh.cls].slice(-1)[0].replace('2×', '') : 'main'} · ${(rM / KYD).toFixed(1)} kyd`, { font: '10.5px "IBM Plex Mono"', col: '#D9B868' });
      if (sh.T.k === 'cap') { c.strokeStyle = 'rgba(217,184,104,.3)'; c.setLineDash([2, 4]); c.beginPath(); geoCircle(c, q[0], q[1], 9000); c.stroke(); const lp2 = dest(q[0], q[1], 45, 9000), s2 = proj(lp2[0], lp2[1]); c.setLineDash([]); label(c, s2.x + 4, s2.y, '4 in · 9.8 kyd', { font: '10px "IBM Plex Mono"', col: '#b39a5e' }); }
      c.restore();
    }
  }
  // fire
  if (bi <= 1) for (const d of drawList) if (d.sh.tgt && d.sh.u.enc) salvoSplashes(c, d.sh, m);
  // hulls / sprites
  for (const d of drawList) {
    const { sh, s } = d, u = sh.u, col = affCol(u), a = d.a !== undefined ? d.a : shipAngle(d.q[0], d.q[1], u.hdg);
    const Lr = sh.T.L / m;
    const minL = sh.T.k === 'cap' ? 20 : sh.T.k === 'cru' ? 14 : sh.type === 'MV' ? 12 : 9;
    const isSel = sel && ((sel.k === 'ship' && sel.ref === sh) || (sel.k === 'unit' && sel.ref === u));
    const susp = isEnemy(u) && u.intel.cert === 'sus';
    const building = u.status === 'BUILD';
    if (isSel) { c.save(); c.strokeStyle = COL.brass; c.lineWidth = Lr > 60 ? 1.2 : 2; c.globalAlpha = Lr > 60 ? .55 : .9; c.setLineDash(Lr > 60 ? [6, 6] : []); c.beginPath(); c.arc(s.x, s.y, Math.max(Lr * 0.58, 9), 0, 7); c.stroke(); c.restore(); }
    if (aSprite > 0 && !susp) {
      if (building) {
        c.save(); c.translate(s.x, s.y); c.rotate(a); c.globalAlpha = aSprite; c.setLineDash([4, 3]); c.strokeStyle = '#c6b7ea'; c.lineWidth = 1.5;
        drawHull(c, 0, 0, 0, Lr, Lr * 0.15, '#c6b7ea', { dash: 1 });
        const p = u.build.p; c.fillStyle = 'rgba(198,183,234,.35)'; c.fillRect(-Lr / 2, -Lr * 0.06, Lr * p, Lr * 0.12);
        c.restore();
      } else if (sh.T.k === 'cap' && sprite.complete) {
        c.save(); c.translate(s.x, s.y); c.rotate(a); c.globalAlpha = aSprite;
        if (isEnemy(u)) c.filter = 'grayscale(0.7) brightness(0.85)';
        const Bp = Lr * sprite.height / sprite.width; c.drawImage(sprite, -Lr / 2, -Bp / 2, Lr, Bp); c.filter = 'none';
        if (sh.fire) { const g = c.createRadialGradient(-Lr * .05, 0, 0, -Lr * .05, 0, Lr * .1); g.addColorStop(0, 'rgba(255,140,40,.8)'); g.addColorStop(1, 'rgba(255,90,20,0)'); c.fillStyle = g; c.beginPath(); c.arc(-Lr * .05, 0, Lr * .1, 0, 7); c.fill(); }
        c.restore();
      } else drawVectorShip(c, s.x, s.y, a, Lr, sh, aSprite);
    }
    if (aSprite < 1) {
      c.save(); c.globalAlpha = (1 - aSprite) * aHull;
      const L = Math.max(minL, Lr), W = Math.max(sh.T.k === 'cap' ? 5.5 : sh.T.k === 'cru' ? 4 : 3, L * 0.17);
      if (building) drawHull(c, s.x, s.y, a, L, W, '#c6b7ea', { dash: 1 });
      else if (susp) drawHull(c, s.x, s.y, a, L, W, col, { dash: 1 });
      else { drawHull(c, s.x, s.y, a, L + 3, W + 3, '#05080b', { fill: 'rgba(5,8,11,.85)', stroke: 'rgba(0,0,0,0)' }); drawHull(c, s.x, s.y, a, L, W, col, { fill: u.aff === 'H' ? '#e0695d' : u.ally ? '#a9c6e8' : sh.type === 'MV' ? '#8fa9c4' : '#6aa8ec', stroke: isSel ? '#D9B868' : 'rgba(255,255,255,.35)' }); }
      if (sh.fire) { c.fillStyle = '#ff9a3c'; c.beginPath(); c.arc(s.x, s.y, 2.2, 0, 7); c.fill(); }
      c.restore();
    }
    // per-ship damage pip at tactical when engaged
    if (u.enc && !isEnemy(u) && bi === 1 && sh.flot < 0.92) { c.save(); c.fillStyle = sh.flot < .6 ? COL.H : COL.amber; c.fillRect(s.x - 2, s.y + Math.max(6, Lr * .2) + 2, 4, 4); c.restore(); }
    // ship labels
    const showName = isSel || (hover && hover.ref === sh) || (m < 9 && (sh.T.k === 'cap' || sh.T.k === 'cru' || m < 3 || sh.type === 'MV')) || (sh.i === 0 && bi === 1);
    if (showName && lens.labels) {
      const nm = susp ? `${u.intel.est || '?'}` : isEnemy(u) ? `${sh.name}` : sh.name;
      const off = Lr > 80 ? Lr * 0.12 + 16 : Math.max(10, Math.min(Lr * 0.18, 40) + 8);
      label(c, s.x + 6, s.y - off, nm, { font: `${sh.i === 0 ? 600 : 500} ${m < 3 ? 13 : 11.5}px "Archivo Narrow"`, col: isEnemy(u) ? '#f3b0a7' : '#e3ecf3' });
      if (sh.i === 0 && bi === 1 && !isSel && !isEnemy(u)) label(c, s.x + 6, s.y - off - 13, u.short, { font: '10px "IBM Plex Mono"', col: '#9fc3ec' });
      if (u.status === 'BUILD' || u.status === 'FIT') label(c, s.x + 6, s.y + off + 8, `${u.build.stage} · ${Math.round(u.build.p * 100)}% · ${u.build.done}`, { font: '10.5px "IBM Plex Mono"', col: '#c6b7ea' });
      if (u.status === 'DOCK') label(c, s.x + 6, s.y + off + 8, u.task, { font: '10.5px "IBM Plex Mono"', col: '#e8cf91' });
    }
    hits.push({ x: s.x, y: s.y, r: Math.max(8, Math.min(Lr * 0.5, 60)), k: 'ship', ref: sh });
  }
  // unit leaders at tactical + orders
  if (bi === 1) for (const u of units) {
    if (!atSea(u) || (isEnemy(u) && !knownEnemy(u))) continue;
    const q = shipLL(u.ships[0]), p = proj(q[0], q[1]); if (!inMap(p.x, p.y, -100)) continue;
    const isSel = sel && (sel.k === 'unit' && sel.ref === u || sel.k === 'ship' && sel.ref.u === u);
    for (const sh of (isEnemy(u) || isSel ? u.ships : [u.ships[0]])) {
      const qq = shipLL(sh), a = proj(qq[0], qq[1]), e = offLL(qq[0], qq[1], u.hdg, u.kn * 0.5144 * 180, 0), b = proj(e[0], e[1]);
      c.save(); c.strokeStyle = affCol(u); c.globalAlpha = .7; c.lineWidth = 1.1; c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke(); c.restore();
    }
    drawOrders(c, u, isSel);
    if (u.sent) { label(c, p.x + 8, p.y + 26, '◷ signal in transit', { font: '10.5px "IBM Plex Mono"', col: '#E3A452' }); }
  }
  for (const w of wrecks) {
    const p = proj(w.lat, w.lon); if (!inMap(p.x, p.y)) continue;
    c.save(); c.strokeStyle = '#E3A452'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(p.x - 5, p.y - 5); c.lineTo(p.x + 5, p.y + 5); c.moveTo(p.x + 5, p.y - 5); c.lineTo(p.x - 5, p.y + 5); c.stroke();
    for (let i = 0; i < 9; i++) { c.fillStyle = 'rgba(150,140,120,.6)'; c.fillRect(p.x + (hash(w.name + i) - .5) * 26, p.y + (hash(w.name + 'y' + i) - .5) * 26, 2, 2); }
    c.restore();
    label(c, p.x + 8, p.y + 4, `${w.name} · sunk ${w.at}`, { font: '10.5px "IBM Plex Mono"', col: '#E3A452' });
    hits.push({ x: p.x, y: p.y, r: 8, k: 'wreck', ref: w });
  }
}

function drawEncounters(c, bi) {
  const t = performance.now() / 1000;
  for (const e of ENCOUNTERS) {
    const p = proj(e.lat, e.lon); if (!p.vis || !inMap(p.x, p.y, -100)) continue;
    if (bi >= 2) {
      const rr = Math.max(16, e.r * 1000 / mpp());
      c.save(); c.strokeStyle = COL.amber; c.globalAlpha = .9; c.lineWidth = 1.5; c.setLineDash(bi === 2 ? [4, 4] : []);
      c.beginPath(); c.arc(p.x, p.y, rr, 0, 7); c.stroke(); c.setLineDash([]);
      const k = (t % 1.6) / 1.6; c.globalAlpha = .6 * (1 - k); c.beginPath(); c.arc(p.x, p.y, rr + 4 + k * 14, 0, 7); c.stroke(); c.restore();
      const t1 = `ACTION · ${e.name}`, t2 = `${e.start}–now · ${e.phase.split(' · ')[0]} · ${e.rate}×`;
      const w = Math.max(textW(t1, '700 11.5px "Archivo Narrow"'), bi === 4 ? 0 : textW(t2, '10px "IBM Plex Mono"')) + 6;
      let lx = p.x - w / 2, ly = p.y - rr - 10 - (bi === 4 ? 0 : 13);
      claimBox(lx - 3, ly - 12, w, bi === 4 ? 16 : 29, true);
      label(c, lx, ly, t1, { font: '700 11.5px "Archivo Narrow"', col: '#1a1100', bg: '#E3A452' });
      if (bi !== 4) label(c, lx, ly + 13, t2, { font: '10px "IBM Plex Mono"', col: '#E3A452' });
      hits.push({ x: p.x, y: p.y, r: Math.max(rr, 14), k: 'enc', ref: e });
    } else {
      c.save(); c.strokeStyle = 'rgba(227,164,82,.35)'; c.setLineDash([2, 6]); c.lineWidth = 1; c.beginPath(); geoCircle(c, e.lat, e.lon, e.r * 1000, 120); c.stroke(); c.restore();
    }
  }
}

function drawSupply(c, bi) {
  if (lens.mode !== 'supply' || !sel) return;
  const u = sel.k === 'unit' ? sel.ref : sel.k === 'ship' ? sel.ref.u : null; if (!u || !u.own) return;
  const fuel = Math.min(...u.ships.map(s => s.fuel)), endur = fuel * (u.comp.cap ? 5600 : 3200) * NM * (u.kn > 18 ? 0.55 : 1);
  c.save(); c.strokeStyle = 'rgba(134,199,126,.7)'; c.setLineDash([6, 4]); c.lineWidth = 1.5; c.beginPath(); geoCircle(c, u.lat, u.lon, endur, 180); c.stroke();
  c.strokeStyle = 'rgba(227,164,82,.7)'; c.beginPath(); geoCircle(c, u.lat, u.lon, endur / 2, 180); c.stroke(); c.restore();
  const p1 = dest(u.lat, u.lon, 200, endur), s1 = proj(p1[0], p1[1]); if (s1.vis) label(c, s1.x, s1.y, `fuel endurance at ${u.kn} kn · ${Math.round(endur / NM)} nm`, { font: '10.5px "IBM Plex Mono"', col: '#9fd3a0' });
  const p2 = dest(u.lat, u.lon, 200, endur / 2), s2 = proj(p2[0], p2[1]); if (s2.vis) label(c, s2.x, s2.y, 'radius of action (return)', { font: '10.5px "IBM Plex Mono"', col: '#E3A452' });
}

function drawOceanLabels(c, bi) {
  if (bi < 2) return;
  for (const [t, la, lo, lv] of OCEAN_LABELS) {
    if ((bi === 4 && lv > 0) || (bi === 3 && lv > 1) || (bi === 2 && lv < 1)) continue;
    const p = proj(la, lo); if (!p.vis || !inMap(p.x, p.y)) continue;
    c.save(); c.font = `italic ${bi === 4 ? 12 : 14}px "Archivo Narrow"`; c.fillStyle = 'rgba(140,175,200,.38)'; c.textAlign = 'center'; c.letterSpacing = '3px'; c.fillText(t, p.x, p.y); c.restore();
  }
}

// edge pips: own forces (and known enemies) near the view but off screen
let pipList = [];
function drawEdgePips(c, bi) {
  pipList = [];
  if (bi > 2) return;
  const vw = viewWidthM(), near = nearRadius(), ctr = { lat: cam.lat, lon: cam.lon };
  const inset = window.pipInset ? window.pipInset() : { x0: MAP.x0 + 26, y0: MAP.y0 + 46, x1: MAP.x1 - 70, y1: MAP.y1 - 34 };
  const groups = {};
  const angOf = (p, pos) => p.vis ? Math.atan2(p.y - MAP.cy, p.x - MAP.cx) : (brg(ctr, pos) - 90) * D2R;
  for (const u of units) {
    if (!(u.own || knownEnemy(u)) || !atSea(u)) continue;
    const d = hav(ctr, u); if (d > near) continue;
    const pts = u.ships.map(sh => { const q = shipLL(sh); return proj(q[0], q[1]); });
    if (pts.some(p => p.vis && inMap(p.x, p.y))) continue;
    const ang = angOf(proj(u.lat, u.lon), u);
    const key = (u.own ? u.fleet : 'en' + u.fleet) + Math.round(ang / 0.3);
    (groups[key] = groups[key] || { us: [], ang, d, own: u.own }).us.push(u);
    groups[key].d = Math.min(groups[key].d, d);
  }
  for (const k of contacts) { // unidentified contacts beyond the view
    let pos = null;
    if (k.kind === 'ellipse') { const f = unitById[k.from], q = dest(f.lat, f.lon, k.brg, (k.rng[0] + k.rng[1]) * 500); pos = { lat: q[0], lon: q[1] }; }
    if (k.kind === 'df') { const tr = unitById[k.truth]; pos = { lat: tr.lat + k.err[0] * 0.09, lon: tr.lon + k.err[1] * 0.09 }; }
    if (!pos) continue;
    const d = hav(ctr, pos); if (d > near) continue; const p = proj(pos.lat, pos.lon); if (p.vis && inMap(p.x, p.y)) continue;
    groups['c' + k.id] = { us: [], k, pos, ang: angOf(p, pos), d, own: false };
  }
  const list = Object.values(groups).sort((a, b) => a.d - b.d).slice(0, bi === 0 ? 8 : 12);
  for (const g of list) {
    const dx = Math.cos(g.ang), dy = Math.sin(g.ang);
    const tx = dx > 0 ? (inset.x1 - MAP.cx) / dx : dx < 0 ? (inset.x0 - MAP.cx) / dx : 1e9, ty = dy > 0 ? (inset.y1 - MAP.cy) / dy : dy < 0 ? (inset.y0 - MAP.cy) / dy : 1e9;
    const t = Math.min(tx, ty); g.x = MAP.cx + dx * t; g.y = MAP.cy + dy * t; g.edge = t === ty ? (dy < 0 ? 'T' : 'B') : (dx < 0 ? 'L' : 'R');
    const u0 = g.us[0];
    if (g.k) { g.t1 = `${g.k.kind === 'df' ? 'DK? (DF fix)' : 'Heavy ships?'} · ${g.k.est.cap[0]}–${g.k.est.cap[1]} cap`; g.t2 = `${fmtKm(g.d)} ${fmtBrg(brg(ctr, g.pos))} · ${g.k.age} min old`; g.col = COL.H; }
    else {
      const cp = comp(g.us.flatMap(u => u.ships));
      const capS = cp.cap ? (cp.BB && cp.BC ? `${cp.BB} BB ${cp.BC} BC` : `${cp.cap} ${cp.BB ? 'BB' : 'BC'}`) : cp.cru ? `${cp.cru} cr` : cp.lt ? `${cp.lt} lt` : cp.mer ? `${cp.mer} mv` : `${cp.aux} aux`;
      g.t1 = `${g.us.length > 1 ? u0.fleet : u0.short} · ${capS}`; g.t2 = `${fmtKm(g.d)} ${fmtBrg(brg(ctr, u0))}${u0.kn ? ' · ' + u0.kn + ' kn ' + fmtBrg(u0.hdg) : ''}`; g.col = affCol(u0);
    }
    c.save(); c.font = '600 11.5px "Archivo Narrow"'; const w1 = c.measureText(g.t1).width; c.font = '10px "IBM Plex Mono"'; const w2 = c.measureText(g.t2).width; c.restore();
    g.w = Math.max(w1, w2) + 12; g.h = 30;
    // box placed inward from the chevron
    if (g.edge === 'T') { g.bx = g.x - g.w / 2; g.by = g.y + 10; } else if (g.edge === 'B') { g.bx = g.x - g.w / 2; g.by = g.y - 40; }
    else if (g.edge === 'L') { g.bx = g.x + 14; g.by = g.y - 15; } else { g.bx = g.x - 14 - g.w; g.by = g.y - 15; }
  }
  for (const E of ['T', 'B', 'L', 'R']) { // sweep apart along each edge
    const l = list.filter(g => g.edge === E);
    if (E === 'T' || E === 'B') { l.sort((a, b) => a.bx - b.bx); for (let i = 1; i < l.length; i++) l[i].bx = Math.max(l[i].bx, l[i - 1].bx + l[i - 1].w + 6); const over = l.length ? l[l.length - 1].bx + l[l.length - 1].w - (MAP.x1 - 60) : 0; if (over > 0) l.forEach(g => g.bx -= over); }
    else { l.sort((a, b) => a.by - b.by); for (let i = 1; i < l.length; i++) l[i].by = Math.max(l[i].by, l[i - 1].by + 36); }
  }
  for (const g of list) {
    const isHover = hover && hover.k === 'pip' && hover.ref === g, col = g.col, own = g.own;
    c.save(); c.translate(g.x, g.y); c.rotate(g.ang);
    c.fillStyle = col; c.globalAlpha = isHover ? 1 : .9; c.beginPath(); c.moveTo(12, 0); c.lineTo(0, -7); c.lineTo(3, 0); c.lineTo(0, 7); c.closePath(); c.fill(); c.restore();
    const x = g.bx, y = g.by, w = g.w;
    c.save(); c.strokeStyle = 'rgba(160,170,180,.25)'; c.beginPath(); c.moveTo(g.x, g.y); c.lineTo(clamp(g.x, x, x + w), clamp(g.y, y, y + 30)); c.stroke();
    c.fillStyle = isHover ? 'rgba(42,36,18,.95)' : 'rgba(14,20,26,.9)'; c.strokeStyle = isHover ? COL.brass : col; c.lineWidth = 1; c.fillRect(x, y, w, 30); c.strokeRect(x + .5, y + .5, w - 1, 29);
    c.fillStyle = col; c.fillRect(x, y, 2, 30); c.restore();
    label(c, x + 6, y + 13, g.t1, { font: '600 11.5px "Archivo Narrow"', col: own ? '#EAF0F4' : '#f3b0a7' });
    label(c, x + 6, y + 25, g.t2, { font: '10px "IBM Plex Mono"', col: '#9fb0bc' });
    g.box = { x, y, w, h: 30 };
    pipList.push(g);
    hits.push({ x: x + w / 2, y: y + 15, r: 0, box: g.box, k: 'pip', ref: g });
  }
}

function drawScaleBar() {
  const sb = document.getElementById('scalebar'), c = sb.getContext('2d'); c.clearRect(0, 0, 200, 26);
  const m = mpp(); const target = 140 * m; const nice = [1, 2, 5]; let v = 1;
  const useNm = m > 3; const unit = useNm ? NM : 1;
  const raw = target / unit; let p = Math.pow(10, Math.floor(Math.log10(raw))); for (const n of nice) if (n * p <= raw) v = n * p;
  const px = v * unit / m;
  c.fillStyle = 'rgba(14,18,21,.75)'; c.fillRect(0, 0, px + 64, 24);
  c.strokeStyle = '#c9d3db'; c.lineWidth = 1; c.beginPath(); c.moveTo(6, 16); c.lineTo(6 + px, 16); c.moveTo(6, 11); c.lineTo(6, 20); c.moveTo(6 + px, 11); c.lineTo(6 + px, 20); c.moveTo(6 + px / 2, 13); c.lineTo(6 + px / 2, 19); c.stroke();
  c.font = '10px "IBM Plex Mono"'; c.fillStyle = '#c9d3db'; c.fillText(useNm ? `${v} nm` : `${v} m`, px + 10, 19);
  c.fillStyle = '#7F8B95'; c.fillText(useNm ? (v * NM / KYD >= 10 ? `${Math.round(v * NM / KYD)} kyd` : `${(v * NM / 1000).toFixed(1)} km`) : '', px + 10, 9);
}

let lastFrame = performance.now(), frameN = 0;
function frame(now) {
  const dtReal = Math.min(0.1, (now - lastFrame) / 1000); lastFrame = now; frameN++;
  const dtGame = dtReal * achieved();
  worldMs += dtGame * 1000; stepWorld(dtGame);
  stepFly(now);
  const bi = bandIdx();
  hits = []; occ = [];
  drawBase();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.drawImage(landCv, 0, 0);
  if (bi <= 2) { const alt = sunAlt(cam.lat, cam.lon); if (alt < 0 && lens.night) { ctx.fillStyle = `rgba(2,6,14,${clamp(-alt / 12, 0, 1) * .45})`; ctx.fillRect(0, 0, SW, SH); } }
  drawOceanLabels(ctx, bi);
  drawSupply(ctx, bi);
  drawContacts(ctx, bi);
  drawEncounters(ctx, bi);
  if (bi === 4) drawSeaAreas(ctx);
  if (bi >= 3) drawUnitsFar(ctx, bi);
  else if (bi === 2) drawUnitsOps(ctx);
  else drawShipsNear(ctx, bi);
  drawPorts(ctx, bi);
  drawEdgePips(ctx, bi);
  drawDragOrder(ctx);
  if (frameN % 2 === 0) drawScaleBar();
  if (window.uiFrame) uiFrame(now, bi);
  requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- input
function toLocal(e) { const r = app.getBoundingClientRect(); return { x: (e.clientX - r.left) / SCALE, y: (e.clientY - r.top) / SCALE }; }
function hitTest(x, y) {
  let best = null, bd = 1e9;
  for (const h of hits) {
    if (h.box) { if (x >= h.box.x && x <= h.box.x + h.box.w && y >= h.box.y && y <= h.box.y + h.box.h) return h; continue; }
    const d = Math.hypot(h.x - x, h.y - y); const pr = { pip: 0, ship: 1, unit: 2, wp: 2, cluster: 3, contact: 4, wreck: 4, port: 5, enc: 7, area: 8 }[h.k] || 6;
    if (d <= h.r && d + pr * 4 < bd) { bd = d + pr * 4; best = h; }
  }
  return best;
}
let drag = null, rdown = null, dragOrder = null;
function ownCommandable() {
  if (!sel) return null;
  if (sel.k === 'unit' && sel.ref.own && atSea(sel.ref)) return sel.ref;
  if (sel.k === 'ship' && sel.ref.u.own && atSea(sel.ref.u)) return sel.ref.u;
  return null;
}
function smartVerb(u, x, y) {
  const h = hitTest(x, y); const g = unproj(x, y); if (!g) return null;
  const tgt = { lat: g[0], lon: g[1] };
  if (h && (h.k === 'ship' || h.k === 'unit') && (h.ref.u || h.ref).aff === 'H') { const t = h.ref.u || h.ref; return { v: 'ENGAGE', en: 1, txt: `ENGAGE ${h.ref.name || t.short} · ${fmtKm(hav(u, h.k === 'ship' ? { lat: shipLL(h.ref)[0], lon: shipLL(h.ref)[1] } : t))}` }; }
  if (h && h.k === 'contact') return { v: 'INVESTIGATE', en: 1, txt: `INVESTIGATE ${h.ref.title}` };
  if (h && (h.k === 'unit' || h.k === 'ship') && (h.ref.u || h.ref).own && (h.ref.u || h.ref) !== u) { const t = h.ref.u || h.ref; return { v: 'JOIN', txt: t.comp.cap > u.comp.cap ? `SCREEN ${t.short}` : `JOIN ${t.short}` }; }
  if (h && h.k === 'port' && h.ref.side === 'RN') { const d = hav(u, { lat: h.ref.at[0], lon: h.ref.at[1] }); return { v: 'RTB', txt: `RETURN TO ${h.ref.n} · ETA ${fmtDur(d / (u.kn * .5144 || 5))}` }; }
  const d = hav(u, tgt); return { v: 'MOVE', tgt, txt: `MOVE ${fmtBrg(brg(u, tgt))} · ${fmtKm(d)} · ETA ${fmtDur(d / (u.kn * 0.5144 || 5))}` };
}
function drawDragOrder(c) {
  if (!dragOrder || !dragOrder.moved) return;
  const u = dragOrder.u, g = unproj(mouse.x, mouse.y); if (!g) return;
  const a = u.shift && u.wps.length ? u.wps[u.wps.length - 1] : unitPos(u);
  c.save(); c.strokeStyle = COL.brass; c.lineWidth = 2; c.setLineDash([8, 5]); c.beginPath(); geoLine(c, a, { lat: g[0], lon: g[1] }, 24); c.stroke(); c.setLineDash([]);
  c.beginPath(); c.arc(mouse.x, mouse.y, 6, 0, 7); c.stroke(); c.restore();
}
function issueMove(u, tgt, queue) {
  if (!queue) u.wps = [];
  u.wps.push({ lat: tgt.lat, lon: tgt.lon });
  if (u.sent) u.sent = null;
  u.ctl = 'DIRECT';
  addLog('cmd', `You: ${u.short} ${queue ? 'queue' : 'move'} → ${fmtLL(tgt.lat, tgt.lon)} (${u.kn} kn)`);
  toast(`<b>${u.short}</b> · ${queue ? 'queued' : 'ordered'}: course ${fmtBrg(brg(u, u.wps[0]))}, ${u.kn} kn · signal by flags, ~${u.id === 'bcs1' ? 0 : 40} s`);
  if (window.refreshCard) refreshCard(true);
}
cv.addEventListener('contextmenu', e => e.preventDefault());
app.addEventListener('contextmenu', e => { if (e.target === cv) e.preventDefault(); });
cv.addEventListener('mousedown', e => {
  const p = toLocal(e); mouse.x = p.x; mouse.y = p.y;
  if (e.button === 0) { drag = { x: p.x, y: p.y, moved: false }; }
  if (e.button === 2) {
    const h = hitTest(p.x, p.y);
    let u = ownCommandable();
    if (h && h.k === 'pip' && h.ref.own && h.ref.us.length) { u = h.ref.us[0]; select('unit', u, false); }
    if (!u) return;
    u.shift = e.shiftKey;
    dragOrder = { u, x: p.x, y: p.y, moved: false, t: performance.now() };
    rdown = setTimeout(() => { if (dragOrder && !dragOrder.moved) { openRadial(p.x, p.y, u); dragOrder = null; } }, 300);
  }
});
addEventListener('mousemove', e => {
  const p = toLocal(e); mouse.x = p.x; mouse.y = p.y; mouse.in = e.target === cv;
  mouse.geo = unproj(p.x, p.y);
  if (drag) { const dx = p.x - drag.x, dy = p.y - drag.y; if (drag.moved || Math.hypot(dx, dy) > 3) { drag.moved = true; panPx(dx, dy); drag.x = p.x; drag.y = p.y; fly = null; } }
  if (dragOrder && Math.hypot(p.x - dragOrder.x, p.y - dragOrder.y) > 6) { dragOrder.moved = true; clearTimeout(rdown); }
  if (radialOpen) radialHover(p.x, p.y);
});
addEventListener('mouseup', e => {
  const p = toLocal(e);
  if (e.button === 0 && drag) {
    if (!drag.moved && e.target === cv) {
      const h = hitTest(p.x, p.y);
      if (!h) { select(null); }
      else if (h.k === 'pip') { if (h.ref.k) { select('contact', h.ref.k, false); flyTo(h.ref.pos.lat, h.ref.pos.lon, mpp()); } else { const u = h.ref.us[0]; select('unit', u, false); flyTo(u.lat, u.lon, mpp()); } }
      else if (h.k === 'wp') select('unit', h.ref.u);
      else select(h.k, h.ref);
    }
    drag = null;
  }
  if (e.button === 2) {
    clearTimeout(rdown);
    if (radialOpen) { closeRadial(true); return; }
    if (dragOrder) {
      const u = dragOrder.u, v = smartVerb(u, p.x, p.y);
      if (v) {
        if (v.v === 'MOVE') issueMove(u, v.tgt, e.shiftKey);
        else { addLog('cmd', `You: ${u.short} — ${v.txt.toLowerCase()}`); toast(`<b>${u.short}</b> · ${v.txt}`); }
      }
      dragOrder = null;
    }
  }
});
cv.addEventListener('dblclick', e => {
  const p = toLocal(e), h = hitTest(p.x, p.y), bi = bandIdx();
  if (h && h.k === 'ship') { select('unit', h.ref.u); return; }
  if (h && (h.k === 'unit' || h.k === 'cluster' || h.k === 'enc')) { const r = h.ref, lat = r.lat !== undefined ? r.lat : r.items[0].u.lat, lon = r.lon !== undefined ? r.lon : r.items[0].u.lon; flyTo(lat, lon, BANDS[Math.max(0, bi - 1)].mid); return; }
  const g = unproj(p.x, p.y); if (g) flyTo(g[0], g[1], BANDS[Math.max(0, bi - 1)].mid);
});
cv.addEventListener('wheel', e => { e.preventDefault(); const p = toLocal(e); fly = null; zoomAt(p.x, p.y, Math.exp(-e.deltaY * 0.0016)); }, { passive: false });
addEventListener('keydown', e => {
  if (e.key === ' ') { e.preventDefault(); setPaused(!paused); }
  else if (e.key === 'Tab') { e.preventDefault(); tabHeld = true; }
  else if (e.key === 'Escape') { if (window.screenOpen && screenOpen()) closeScreen(); else if (radialOpen) closeRadial(false); else select(null); }
  else if (/^F([1-9]|10)$/.test(e.key)) { e.preventDefault(); if (window.openScreenByKey) openScreenByKey(e.key); }
  else if (e.key === 'Home') flyTo(cam.lat, cam.lon, 14000);
  else if (e.key >= '1' && e.key <= '7' && !e.ctrlKey && !e.altKey) { const i = +e.key - 1; if (e.shiftKey) setSpeed(SPEEDS[i]); else if (window.gotoBookmark) gotoBookmark(i); }
  else if (e.key === 'f' && sel) { const r = sel.ref, u = r.u || r; const q = r.u ? shipLL(r) : [u.lat, u.lon]; flyTo(q[0], q[1], mpp()); }
  else if (e.key === 'q' && window.nextDecision) nextDecision();
  else if (e.key === 'o') { lens.orders = !lens.orders; renderLens(); } else if (e.key === 'r') { lens.ranges = !lens.ranges; renderLens(); } else if (e.key === 'l') { lens.labels = !lens.labels; renderLens(); } else if (e.key === 'n') { lens.night = !lens.night; landKey = ''; renderLens(); } else if (e.key === 'g') { lens.fire = !lens.fire; renderLens(); }
  else if (e.key === '+' || e.key === '=') zoomAt(MAP.cx, MAP.cy, 1.5); else if (e.key === '-') zoomAt(MAP.cx, MAP.cy, 1 / 1.5);
});
addEventListener('keyup', e => { if (e.key === 'Tab') tabHeld = false; });

function setPaused(p) { paused = p; if (window.renderClock) renderClock(); }
function setSpeed(s) { speedAsked = s; paused = false; if (window.renderClock) renderClock(); }

// radial marking menu
let radialOpen = null;
const RADIAL = [
  { v: 'Engage', k: 'E', a: -90 }, { v: 'Intercept', k: 'I', a: -45 }, { v: 'Screen / escort', k: 'S', a: 0 }, { v: 'Patrol area', k: 'P', a: 45 },
  { v: 'Hold / stop', k: 'H', a: 90 }, { v: 'Return to base', k: 'B', a: 135 }, { v: 'Shadow', k: 'W', a: 180 }, { v: 'Formation…', k: 'D', a: -135 },
];
function openRadial(x, y, u) {
  const el = document.getElementById('radial'); el.style.left = x + 'px'; el.style.top = y + 'px'; el.style.display = 'block';
  const noGuns = u.comp.mer > 0 && !u.comp.cap && !u.comp.cru;
  el.innerHTML = '<div class="hub"></div>' + RADIAL.map((r, i) => {
    const off = (noGuns && (r.v === 'Engage' || r.v === 'Intercept')) || (r.v === 'Shadow' && u.comp.cap > 2);
    const rr = 92; return `<div class="it ${off ? 'off' : ''}" data-i="${i}" style="left:${Math.cos(r.a * D2R) * rr}px;top:${Math.sin(r.a * D2R) * rr}px" title="${off ? (noGuns ? 'No armament' : 'Heavy ships do not shadow; use a light cruiser') : ''}">${r.v}<span class="k">${r.k}</span></div>`;
  }).join('') + `<div class="cap">${u.short} · flick to choose · release outside to cancel</div>`;
  radialOpen = { x, y, u, i: -1 };
}
function radialHover(x, y) {
  const dx = x - radialOpen.x, dy = y - radialOpen.y; let i = -1;
  if (Math.hypot(dx, dy) > 24) { const a = Math.atan2(dy, dx) * R2D; let best = 1e9; RADIAL.forEach((r, j) => { let d = Math.abs(((a - r.a) + 540) % 360 - 180); if (d < best) { best = d; i = j; } }); }
  radialOpen.i = i;
  document.querySelectorAll('#radial .it').forEach((el, j) => el.classList.toggle('hl', j === i));
}
function closeRadial(commit) {
  const el = document.getElementById('radial');
  if (commit && radialOpen && radialOpen.i >= 0) {
    const it = el.querySelector(`.it[data-i="${radialOpen.i}"]`);
    if (it && !it.classList.contains('off')) { const v = RADIAL[radialOpen.i].v; addLog('cmd', `You: ${radialOpen.u.short} — ${v.toLowerCase()} (aim with the cursor)`); toast(`<b>${radialOpen.u.short}</b> · ${v}: click the area or target`); }
  }
  el.style.display = 'none'; radialOpen = null;
}
let toastT = 0;
function toast(html) { const el = document.getElementById('toast'); el.innerHTML = html; el.style.display = 'block'; clearTimeout(toastT); toastT = setTimeout(() => el.style.display = 'none', 3200); }

requestAnimationFrame(frame);
