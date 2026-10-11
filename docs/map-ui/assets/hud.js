// The HUD: a full-bleed map with small floating pieces that appear when they have something to say.
'use strict';

const hud = { details: false, dqOpen: false, minimap: true, help: false, frailMin: false };

// ---------------------------------------------------------------- top strip
function renderClock() {
  const held = encActive() && speedAsked > 12;
  $('speeds').innerHTML = `<button class="${paused ? 'on' : ''}" data-s="p" title="Pause (Space)">❚❚</button>` + SPEEDS.map((s, i) =>
    `<button class="${!paused && speedAsked === s ? 'on' : ''} ${!paused && held && s > 12 && s <= speedAsked ? 'held' : ''}" data-s="${s}" title="Shift+${i + 1} · ${SPEED_LBL[i]} per second">${SPEED_LBL[i]}</button>`).join('');
  $('rate').innerHTML = paused ? '<b>PAUSED</b>' : held ? `<b>held 12×</b> by ${encActive()} actions` : encActive() ? `${SPEED_LBL[SPEEDS.indexOf(speedAsked)]} · actions cap 12×` : SPEED_LBL[SPEEDS.indexOf(speedAsked)];
}
$('speeds').addEventListener('click', e => { const b = e.target.closest('button'); if (!b) return; if (b.dataset.s === 'p') setPaused(!paused); else setSpeed(+b.dataset.s); });
const SCREENS = [['map', 'Map', 'F1'], ['fleet', 'Fleet', 'F2'], ['designs', 'Designs', 'F3'], ['yards', 'Yards', 'F4', 1], ['research', 'Research', 'F5'], ['intel', 'Intelligence', 'F6', 2], ['supply', 'Supply', 'F7'], ['admiralty', 'Admiralty', 'F8'], ['officers', 'Officers', 'F9'], ['reports', 'Reports', 'F10', 3]];
function renderTabs(cur = 'map') {
  $('screens').innerHTML = SCREENS.map(([id, n, k, b]) => `<div class="stab ${id === cur ? 'on' : ''}" data-id="${id}" title="${k}">${n}${b ? `<span class="bd">${b}</span>` : ''}</div>`).join('');
}
$('screens').addEventListener('click', e => { const t = e.target.closest('.stab'); if (!t) return; if (t.dataset.id === 'map') closeScreen(); else openScreen(t.dataset.id); });
function openScreenByKey(k) { const s = SCREENS.find(x => x[2] === k); if (!s) return; if (s[0] === 'map') closeScreen(); else openScreen(s[0]); }

// ---------------------------------------------------------------- lens + layers
function renderLens() {
  const L = [['ops', 'Ops'], ['intel', 'Intel'], ['supply', 'Supply'], ['yards', 'Yards']];
  const on = ['orders', 'ranges', 'fire', 'labels', 'night'].filter(k => lens[k]).length;
  $('lens').innerHTML = `<div class="seg glass">${L.map(([k, n]) => `<button data-l="${k}" class="${lens.mode === k ? 'on' : ''}">${n}</button>`).join('')}</div>` +
    `<div class="seg glass"><button data-layers="1" class="${$('layers').style.display === 'block' ? 'on' : ''}">Layers ${on}/5 ▾</button></div>`;
  const T = [['orders', 'All order lines', 'O'], ['ranges', 'Range rings of the selection', 'R'], ['fire', 'Gunfire lines', 'G'], ['labels', 'Labels', 'L'], ['night', 'Night side', 'N']];
  $('layers').innerHTML = T.map(([k, n, key]) => `<label data-t="${k}"><input type="checkbox" ${lens[k] ? 'checked' : ''} tabindex="-1"> ${n}<span class="kbd">${key}</span></label>`).join('') + '<div class="hint" style="padding:4px">Hold <span class="kbd">Tab</span> to see every order line once.</div>';
}
$('lens').addEventListener('click', e => { const b = e.target.closest('button'); if (!b) return; if (b.dataset.l) lens.mode = b.dataset.l; if (b.dataset.layers) $('layers').style.display = $('layers').style.display === 'block' ? 'none' : 'block'; renderLens(); });
$('layers').addEventListener('click', e => { const l = e.target.closest('label'); if (!l) return; e.preventDefault(); lens[l.dataset.t] = !lens[l.dataset.t]; landKey = ''; renderLens(); });

// ---------------------------------------------------------------- scale rail + band banner
const RAIL_H = 380, LM0 = Math.log(0.12), LM1 = Math.log(R / SMIN);
const railY = m => RAIL_H * (1 - (Math.log(m) - LM0) / (LM1 - LM0));
function renderRail() {
  let h = '<div class="tr"></div>';
  BANDS.forEach((b, i) => { h += `<div class="tk2" style="top:${railY(Math.min(b.hi, R / SMIN))}px"></div><div class="bd" data-b="${i}" style="top:${railY(Math.sqrt(b.lo * Math.min(b.hi, R / SMIN)))}px">${b.name}</div>`; });
  $('rail').innerHTML = h + '<div class="th" id="railTh"></div><div class="w" id="railW"></div>';
}
$('rail').addEventListener('click', e => {
  const b = e.target.closest('.bd'); if (b) { flyTo(cam.lat, cam.lon, BANDS[+b.dataset.b].mid); return; }
  const r = $('rail').getBoundingClientRect(); const y = (e.clientY - r.top) / SCALE; flyTo(cam.lat, cam.lon, Math.exp(LM0 + (1 - y / RAIL_H) * (LM1 - LM0)));
});
let lastBand = -1, bannerT = 0;
function bandBanner(bi) {
  if (bi === lastBand) return; const first = lastBand < 0; lastBand = bi; if (first) return;
  const el = $('banner'); el.innerHTML = `${BANDS[bi].name}<span>${fmtKm(viewWidthM())} across · ${['click selects a ship', 'click selects a ship', 'click selects a group', 'click selects a fleet', 'click selects a fleet'][bi]}</span>`;
  el.style.opacity = 1; clearTimeout(bannerT); bannerT = setTimeout(() => el.style.opacity = 0, 1400);
}

// ---------------------------------------------------------------- forces rail (left) and the full list (drawer)
let forcesItemsCache = [];
function railChip(it) {
  const u = it.k === 'unit' ? it.ref : null, isSel = sel && sel.ref === it.ref;
  const symCls = it.aff === 'H' ? 'ho' + (it.sus ? ' sus' : '') : it.aff === 'A' ? 'al' : it.aff === 'N' ? 'ne' : '';
  const act = it.us && it.us.some(x => x.enc);
  let rr;
  if (it.sec === 0) rr = (act ? '<span class="ac">◆ action</span><br>' : '') + (u && atSea(u) && u.kn ? `${fmtBrg(u.hdg)} ${u.kn} kn` : it.k === 'fleet' ? `${it.us.length} groups` : u ? u.status.toLowerCase() : '');
  else rr = `<span class="arw" style="transform:rotate(${Math.round(it.b)}deg)">↑</span> ${fmtKm(it.d)}${act ? '<br><span class="ac">◆ action</span>' : ''}`;
  return `<div class="fc glass ${isSel ? 'sel' : ''} ${it.sec ? 'near' : ''}" data-k="${it.k}" data-id="${it.k === 'fleet' ? esc(it.ref.name) : it.ref.id}">
    <span class="sym ${symCls}"></span><div class="nm"><b>${esc(it.name)}</b>${it.est ? pipsHTML(null, 'H', it.est) : pipsHTML(it.cp, it.aff)}</div><div class="rr">${rr}</div></div>`;
}
function renderFrail() {
  const { items, near, level } = forcesItems(); forcesItemsCache = items;
  const bi = bandIdx();
  const inV = items.filter(i => i.sec === 0).sort((a, b) => (b.us?.some(x => x.enc) ? 1 : 0) - (a.us?.some(x => x.enc) ? 1 : 0) || (b.cp?.t || 0) - (a.cp?.t || 0));
  const nr = items.filter(i => i.sec === 1).sort((a, b) => a.d - b.d);
  const nIn = Math.min(inV.length, nr.length ? 6 : 9), nNr = Math.min(nr.length, 4);
  let h = `<div class="fh"><b>${level === 'fleet' ? 'Fleets' : 'In view'}</b> ${inV.length}${bi <= 2 ? ` · <b>near</b> ${nr.length}` : ''}<span class="lnk" data-all="1">all forces <span class="kbd">U</span></span><span class="lnk" data-min="1" style="margin-left:6px">${hud.frailMin ? '+' : '–'}</span></div>`;
  if (!hud.frailMin) {
    h += inV.slice(0, nIn).map(railChip).join('');
    if (inV.length > nIn) h += `<div class="fh"><span class="lnk" data-all="1" style="margin-left:0">+${inV.length - nIn} more in view</span></div>`;
    if (nNr) h += nr.slice(0, nNr).map(railChip).join('');
  }
  $('frail').innerHTML = h;
  if ($('fdrawer').classList.contains('open')) renderForcesList();
}
function renderForcesList() {
  const items = forcesItemsCache, bi = bandIdx(), level = bi >= 3 ? 'fleet' : 'unit', near = nearRadius();
  $('fTabs').innerHTML = [['own', 'Own'], ['contacts', 'Contacts'], ['allied', 'Allied']].map(([k, n]) => `<span class="chip ${fTab === k ? 'on' : ''}" data-t="${k}">${n}</span>`).join('');
  $('fTitle').textContent = fTab === 'own' ? (level === 'fleet' ? 'Forces · fleets' : 'Forces · groups') : fTab === 'contacts' ? 'Contacts' : 'Allied & neutral';
  const s0 = items.filter(i => i.sec === 0).sort((a, b) => a.d - b.d), s1 = items.filter(i => i.sec === 1).sort((a, b) => a.d - b.d), s2 = items.filter(i => i.sec === 2);
  let h = `<div class="sec"><b>In view</b> · ${s0.length}<span class="rt">${BANDS[bi].name} · ${fmtKm(viewWidthM())} across</span></div>` + s0.map(rowHTML).join('');
  if (bi <= 2) h += `<div class="sec"><b>Near</b> · ${s1.length}<span class="rt">within ${fmtKm(near)}</span></div>` + s1.map(rowHTML).join('');
  const byTh = {}; for (const it of s2) (byTh[it.th] = byTh[it.th] || []).push(it);
  h += `<div class="sec"><b>Elsewhere</b> · ${s2.length}<span class="rt">by theatre</span></div>`;
  for (const th of ['Home waters', 'Atlantic', 'Mediterranean', 'Baltic', 'Indian Ocean', 'Pacific']) {
    const l = byTh[th]; if (!l) continue; const open = openTheatres[th];
    h += `<div class="sec tg" data-th="${th}"><span>${open ? '▾' : '▸'}</span><b style="text-transform:none;letter-spacing:0;font-size:11.5px">${th}</b><span class="rt">${l.length}</span></div>`;
    if (open) h += l.sort((a, b) => a.d - b.d).map(rowHTML).join('');
  }
  $('forces').innerHTML = h;
}
function findItem(el) { return forcesItemsCache.find(it => (it.k === 'fleet' ? it.ref.name : it.ref.id) === el.dataset.id && it.k === el.dataset.k); }
function goItem(it) {
  select(it.k, it.ref, false);
  if (it.sec > 0) { const p = it.p0; flyTo(p.lat, p.lon, it.k === 'fleet' ? Math.max(mpp(), 900) : it.us && !atSea(it.us[0]) ? Math.min(mpp(), 30) : mpp()); }
}
$('frail').addEventListener('click', e => {
  if (e.target.closest('[data-all]')) { toggleDrawer('fdrawer'); return; }
  if (e.target.closest('[data-min]')) { hud.frailMin = !hud.frailMin; renderFrail(); return; }
  const c = e.target.closest('.fc'); if (!c) return; const it = findItem(c); if (it) goItem(it);
});
$('frail').addEventListener('dblclick', e => { const c = e.target.closest('.fc'); if (!c) return; const it = findItem(c); if (it) flyTo(it.p0.lat, it.p0.lon, it.k === 'fleet' ? 1200 : mpp() * 0.5); });
$('frail').addEventListener('mouseover', e => { const c = e.target.closest('.fc'); hoverRow = c ? findItem(c) : null; });
$('frail').addEventListener('mouseleave', () => hoverRow = null);
$('fTabs').addEventListener('click', e => { const c = e.target.closest('.chip'); if (!c) return; fTab = c.dataset.t; forcesItemsCache = forcesItems().items; renderForcesList(); });
$('forces').addEventListener('click', e => {
  const t = e.target.closest('.sec.tg'); if (t) { openTheatres[t.dataset.th] = !openTheatres[t.dataset.th]; renderForcesList(); return; }
  const r = e.target.closest('.row'); if (!r) return; const it = findItem(r); if (it) goItem(it);
});
$('forces').addEventListener('mouseover', e => { const r = e.target.closest('.row'); hoverRow = r ? findItem(r) : null; });
function toggleDrawer(id) {
  const el = $(id), open = !el.classList.contains('open');
  document.querySelectorAll('.drawer').forEach(d => d.classList.remove('open'));
  if (open) { el.classList.add('open'); if (id === 'fdrawer') { forcesItemsCache = forcesItems().items; renderForcesList(); } else renderLog(); }
  $('frail').style.display = $('ticker').style.display = open ? 'none' : '';
}
document.addEventListener('click', e => { const c = e.target.closest('[data-close]'); if (c) toggleDrawer(c.dataset.close); });

// ---------------------------------------------------------------- minimap
const locCv = $('loc'), lc = locCv.getContext('2d');
const LP2 = d3.geoOrthographic().clipAngle(90).precision(1);
function renderLocator() {
  const g = bandIdx() === 4; $('locw').style.display = g ? 'none' : ''; // the globe is its own minimap
  $('ticker').style.bottom = g || !hud.minimap ? (g ? '14px' : '56px') : '212px';
  if (!hud.minimap || bandIdx() === 4) return;
  const W = 180, r = 88, cx = 90, cy = 90;
  lc.clearRect(0, 0, W, W);
  LP2.rotate([-cam.lon, -cam.lat]).scale(r).translate([cx, cy]);
  const path = d3.geoPath(LP2, lc);
  lc.fillStyle = 'rgba(16,38,58,.92)'; lc.beginPath(); lc.arc(cx, cy, r, 0, 7); lc.fill();
  lc.beginPath(); for (const p of L110) path(p.g); lc.fillStyle = '#323a32'; lc.fill();
  const [dec, slon] = sunPos(worldMs); lc.beginPath(); path(d3.geoCircle().center([slon + 180, -dec]).radius(90)()); lc.fillStyle = 'rgba(0,0,8,.35)'; lc.fill();
  lc.strokeStyle = 'rgba(150,172,188,.35)'; lc.lineWidth = 1; lc.beginPath(); lc.arc(cx, cy, r - .5, 0, 7); lc.stroke();
  const P2 = (la, lo) => LP2([lo, la]), visL = (la, lo) => dot(v3(la, lo), B.c) > 0;
  if (bandIdx() <= 3) {
    const pts = [], n = 8;
    for (let i = 0; i <= n; i++) pts.push([MAP.x0 + MAP.w * i / n, MAP.y0]);
    for (let i = 0; i <= n; i++) pts.push([MAP.x1, MAP.y0 + MAP.h * i / n]);
    for (let i = n; i >= 0; i--) pts.push([MAP.x0 + MAP.w * i / n, MAP.y1]);
    for (let i = n; i >= 0; i--) pts.push([MAP.x0, MAP.y0 + MAP.h * i / n]);
    lc.beginPath(); pts.forEach((p, i) => { const g = unproj(p[0], p[1]); if (!g) return; const q = P2(g[0], g[1]); if (i) lc.lineTo(q[0], q[1]); else lc.moveTo(q[0], q[1]); });
    lc.closePath(); lc.strokeStyle = '#D9B868'; lc.lineWidth = 1.5; lc.stroke(); lc.fillStyle = 'rgba(217,184,104,.14)'; lc.fill();
  }
  for (const u of units) if (u.own && atSea(u) && visL(u.lat, u.lon)) { const q = P2(u.lat, u.lon); lc.fillStyle = '#5B9BE0'; lc.fillRect(q[0] - 1, q[1] - 1, 2.5, 2.5); }
  for (const e of ENCOUNTERS) if (visL(e.lat, e.lon)) { const q = P2(e.lat, e.lon); lc.strokeStyle = '#E3A452'; lc.lineWidth = 1.5; lc.beginPath(); lc.arc(q[0], q[1], 4, 0, 7); lc.stroke(); }
}
locCv.addEventListener('click', e => { const r = locCv.getBoundingClientRect(); const x = (e.clientX - r.left) / SCALE, y = (e.clientY - r.top) / SCALE; const g = LP2.invert([x, y]); if (g && Math.hypot(x - 90, y - 90) < 88) flyTo(g[1], g[0], mpp()); });
$('locMin').addEventListener('click', () => toggleMinimap());
function toggleMinimap() { hud.minimap = !hud.minimap; $('locw').classList.toggle('min', !hud.minimap); $('locMin').textContent = hud.minimap ? '–' : '+'; $('ticker').style.bottom = hud.minimap ? '212px' : '56px'; }
BOOKMARKS.push({ n: 'Clydebank', f: () => [55.9010, -4.4003, 0.9] });
$('bm').innerHTML = BOOKMARKS.map((b, i) => `<span class="chip" data-i="${i}"><b>${i + 1}</b>${b.n}</span>`).join('');
$('bm').addEventListener('click', e => { const c = e.target.closest('.chip'); if (c) gotoBookmark(+c.dataset.i); });

// ---------------------------------------------------------------- actions (top centre) with a hover pop-over
function renderActs() {
  $('acts').innerHTML = ENCOUNTERS.map(e => `<div class="ach glass" data-e="${e.id}"><span class="dot"></span><b>${esc(e.name)}</b><span>${esc(e.phase.split(' · ')[0])}</span></div>`).join('');
}
let popEnc = null;
$('acts').addEventListener('mouseover', e => {
  const a = e.target.closest('.ach'); if (!a) return; popEnc = encById[a.dataset.e];
  const r = a.getBoundingClientRect(), ar = app.getBoundingClientRect();
  const pop = $('actPop'); pop.style.display = 'block'; pop.style.left = Math.max(10, (r.left - ar.left) / SCALE + (r.width / SCALE) / 2 - 165) + 'px';
  $('actTxt').innerHTML = `<div class="ln"><b>${esc(popEnc.name)}</b> · since ${popEnc.start} · ${esc(popEnc.phase)}</div><div class="ln"><span class="fr">■</span> ${esc(popEnc.own)}</div><div class="ln"><span class="ho">◆</span> ${esc(popEnc.foe)}</div><div class="ln">Losses: <b>${esc(popEnc.losses)}</b> · world held at ${popEnc.rate}×</div><div class="ln dim">click to go there</div>`;
  drawPop();
});
$('acts').addEventListener('mouseleave', () => { $('actPop').style.display = 'none'; popEnc = null; });
$('acts').addEventListener('click', e => { const a = e.target.closest('.ach'); if (!a) return; const en = encById[a.dataset.e]; select('enc', en, false); flyTo(en.lat, en.lon, en.id === 'E3' ? 9 : 26); });
function drawPop() {
  const e = popEnc; if (!e) return; const c = $('actCv').getContext('2d');
  const W = 312, H = 150, half = e.r * 1000 * 0.6, k = W / 2 / half;
  c.fillStyle = '#0d1a24'; c.fillRect(0, 0, W, H);
  c.strokeStyle = 'rgba(120,170,210,.1)'; c.beginPath(); for (let x = 0; x < W; x += 24) { c.moveTo(x, 0); c.lineTo(x, H); } for (let y = 0; y < H; y += 24) { c.moveTo(0, y); c.lineTo(W, y); } c.stroke();
  const pt = (la, lo) => [W / 2 + (lo - e.lon) * D2R * R * Math.cos(e.lat * D2R) * k, H / 2 - (la - e.lat) * D2R * R * k];
  for (const u of units) {
    if (u.enc !== e.id || (isEnemy(u) && !knownEnemy(u))) continue; c.fillStyle = affCol(u);
    for (const sh of u.ships) { const q = shipLL(sh), p = pt(q[0], q[1]); const s = sh.T.k === 'cap' ? 3.5 : sh.T.k === 'cru' ? 2.5 : 1.8; c.fillRect(p[0] - s / 2, p[1] - s / 2, s, s); }
  }
}

// ---------------------------------------------------------------- decisions (top right)
function renderDQ() {
  if (!decisions.length) { $('dq').innerHTML = ''; return; }
  const first = decisions[0];
  let h = `<div class="dq glass top" data-i="0"><div class="h"><span class="k ${first.k}"></span><b>${esc(first.title)}</b><span class="t">${first.t}</span></div><p>${esc(first.body)}</p><div class="bt">${first.acts.map((a, j) => `<button data-a="${j}">${esc(a)}</button>`).join('')}</div></div>`;
  if (decisions.length > 1) {
    if (hud.dqOpen) h += decisions.slice(1).map((d, i) => `<div class="dq glass mini" data-i="${i + 1}"><div class="h"><span class="k ${d.k}"></span><b style="font-weight:500">${esc(d.title)}</b><span class="t">${d.t}</span></div><p>${esc(d.body)}</p><div class="bt" style="display:none">${d.acts.map((a, j) => `<button data-a="${j}">${esc(a)}</button>`).join('')}</div></div>`).join('');
    h += `<div class="dqmore glass" data-more="1">${hud.dqOpen ? '▴ fewer' : `▾ ${decisions.length - 1} more decisions`}<span style="margin-left:auto"><span class="kbd">Q</span> next</span></div>`;
  }
  $('dq').innerHTML = h;
}
$('dq').addEventListener('click', e => {
  if (e.target.closest('[data-more]')) { hud.dqOpen = !hud.dqOpen; renderDQ(); return; }
  const d = e.target.closest('.dq'); if (!d) return; const i = +d.dataset.i, it = decisions[i];
  const b = e.target.closest('button');
  if (b) { const a = it.acts[+b.dataset.a]; addLog('cmd', `You: ${it.title.split(':')[0]} — ${a}`); if (a.startsWith('Open yards')) openScreen('yards'); else toast(`<b>${esc(a)}</b> · ${esc(it.title)}`); decisions.splice(i, 1); renderDQ(); return; }
  if (i > 0) { decisions.splice(i, 1); decisions.unshift(it); renderDQ(); }
  jumpRef(it.ref);
});
$('dq').addEventListener('mouseover', e => { const d = e.target.closest('.dq.mini'); document.querySelectorAll('.dq.mini').forEach(x => { const on = x === d; x.querySelector('p').style.display = on ? 'block' : ''; x.querySelector('.bt').style.display = on ? 'flex' : 'none'; }); });

// ---------------------------------------------------------------- ticker (recent events, fading) + log drawer
const tick = []; let logSeen = LOGS.length;
LOGS.slice(0, 3).reverse().forEach(l => tick.push({ l, t: performance.now() + 6000 }));
const _addLog = addLog;
addLog = function (c, m) { _addLog(c, m); tick.push({ l: LOGS[0], t: performance.now() }); if (tick.length > 5) tick.shift(); renderTicker(); };
function renderTicker() {
  const now = performance.now();
  let h = tick.map((x, i) => { const age = now - x.t, op = age > 14000 ? 0 : age > 10000 ? 1 - (age - 10000) / 4000 : 1; return `<div class="tk" data-i="${i}" style="opacity:${op.toFixed(2)}"><span class="t">${x.l.t}</span><span class="c ${x.l.c}">${x.l.c}</span><span>${esc(x.l.m)}</span></div>`; }).join('');
  h += `<div class="tkbar" data-log="1">Log <span class="kbd">J</span></div>`;
  $('ticker').innerHTML = h;
}
$('ticker').addEventListener('click', e => {
  if (e.target.closest('[data-log]')) { toggleDrawer('ldrawer'); return; }
  const r = e.target.closest('.tk'); if (!r) return; const l = tick[+r.dataset.i].l;
  const nm = Object.keys(shipsByName).find(n => l.m.startsWith(n) || l.m.includes(' ' + n + ' ') || l.m.includes(n + ':')); if (nm) jumpRef(['ship', nm]);
});

// ---------------------------------------------------------------- selection: a compact strip, details on demand (V)
function select(k, ref) {
  sel = k ? { k, ref } : null; refreshCard(true); renderFrail();
}
const sv = (lab, val, cls = '') => `<div class="sv"><i>${lab}</i><b class="${cls}">${val}</b></div>`;
const vb = (n, k, pri, dis) => `<button class="${pri ? 'pri' : ''}" ${dis ? `disabled title="${dis}"` : ''}>${n}<span class="k">${k}</span></button>`;
const symFor = u => `<span class="sym ${u.aff === 'H' ? 'ho' + (u.intel && u.intel.cert !== 'id' ? ' sus' : '') : u.ally ? 'al' : u.neutral ? 'ne' : ''}" style="width:20px;height:20px"></span>`;
function compactSel() {
  const k = sel.k, r = sel.ref;
  const idb = (sym, title, sub, chips = '') => `<div class="sid">${sym}<div class="tt"><b>${esc(title)}</b><span>${sub}</span>${chips ? `<div class="chs">${chips}</div>` : ''}</div></div>`;
  let id = '', st = '', vbs = '';
  if ((k === 'ship' || k === 'unit') && isEnemy(r.u || r)) {
    const u = r.u || r, sure = u.intel.cert === 'id';
    id = idb(symFor(u), sure ? (k === 'ship' ? r.name : u.name) : (u.intel.est || 'Unknown'), sure ? `${esc(k === 'ship' ? (r.cls || r.T.n) : u.fleet)} · identified` : 'suspect · type inferred', `<span class="st ${sure ? 'war' : 'port'}">${u.intel.src.toUpperCase()} · ${u.intel.age} MIN</span>`);
    st = sv('Strength', pipsHTML(u.comp, 'H', sure ? null : { cap: [0, u.comp.cap], cru: [0, u.comp.cru], lt: [0, u.comp.lt] })) + sv('Course', `${fmtBrg(u.hdg)} · ~${u.kn} kn`) + sv('Reported by', esc(u.intel.by));
    vbs = vb('Engage with…', 'E', 1) + vb('Shadow with…', 'W') + vb('Mark: hostile', 'M');
  } else if (k === 'ship') {
    const sh = r, u = sh.u, T = TURRETS[sh.cls];
    id = idb(symFor(u), sh.name, `${esc(sh.cls ? sh.cls + ' class' : sh.T.n)} · ${esc(u.short)}`, statusChip(u) + (u.own && atSea(u) ? ctlChip(u) : ''));
    st = sv('Flotation', `${bar(sh.flot)} ${Math.round(sh.flot * 100)}%`) + (sh.flood ? sv('Water', `${sh.flood} t${sh.list ? ` · ${sh.list}°` : ''}`, 'amb') : '') + (sh.fire ? sv('Fire', esc((sh.fireAt || 'amidships').split(' · ')[0]), 'amb') : '');
    if (atSea(u)) st += sv('Course', `${fmtBrg(u.hdg)} · ${u.kn} kn`);
    if (sh.tgt) { const a = shipLL(sh), t = shipLL(sh.tgt); st += sv('Target', `${esc(sh.tgt.name)} · ${(hav({ lat: a[0], lon: a[1] }, { lat: t[0], lon: t[1] }) / KYD).toFixed(1)} kyd`); }
    const tur = sh.turrets || (T ? T.slice(0, -1).map(t => [t, '', '', 0, sh.tgt ? 'FIRING' : 'TRAINED']) : null);
    if (tur) st += `<div class="sv"><i>Turrets</i><div class="tur">${tur.map(t => `<span class="${/OUT/.test(t[4]) ? 'out' : ''}" title="${t[0]}: ${t[4]}">${t[0]}</span>`).join('')}</div></div>`;
    if (u.status === 'BUILD' || u.status === 'FIT') st += sv('Building', `${bar(u.build.p, 'b')} ${Math.round(u.build.p * 100)}% · ${u.build.done}`);
    if (u.status === 'DOCK') st += sv('In dock', esc(u.task));
    vbs = u.own ? (atSea(u) ? vb('Haul out', 'H', 1) + vb('Target…', 'E') + vb('Damage ctl', 'K') : '') + vb('Design', 'F3') + vb('Register', 'F2') : '';
  } else if (k === 'unit') {
    const u = r;
    id = idb(symFor(u), u.name, `${esc(u.fleet)} · ${esc(u.cmdr || '')}`, statusChip(u) + (u.own && atSea(u) ? ctlChip(u) : ''));
    st = `<div class="sv"><i>${u.ships.length} ships</i><div class="minibars">${u.ships.slice(0, 22).map(s => `<i class="${s.flot < .6 ? 'r' : s.flot < .9 ? 'a' : ''}" style="height:${Math.round(6 + 14 * s.flot)}px" title="${esc(s.name)} ${Math.round(s.flot * 100)}%"></i>`).join('')}</div></div>`;
    if (u.status === 'BUILD' || u.status === 'FIT') { st += sv('Progress', `${bar(u.build.p, 'b')} ${Math.round(u.build.p * 100)}%`) + sv('Completes', u.build.done) + sv('Yard', esc(u.cmdr)); vbs = vb('Yard schedule', 'F4', 1) + vb('Design', 'F3'); }
    else if (!atSea(u)) { st += `<div class="sv task"><i>${u.port}</i><b>${esc(u.task)}</b></div>`; vbs = u.own ? vb('Sail…', 'S', 1) + vb('Yard', 'F4') : ''; }
    else {
      st += sv('Course', `${fmtBrg(u.hdg)} · ${u.kn} kn`);
      if (u.sent) st += sv('Sent', `${u.sent.hdg}° · in transit ${Math.max(0, Math.round(u.sent.transit))} s`, 'amb');
      if (u.wps.length) { const t = hav(u, u.wps[0]) / (u.kn * .5144 || 1); st += sv('Next', `ETA ${hhmm(worldMs + t * 1000)}`); }
      st += `<div class="sv task"><i>Task</i><b>${esc(u.task || '—')}</b></div>`;
      const noGuns = u.comp.mer > 0 && !u.comp.cap && !u.comp.cru;
      vbs = u.own ? vb('Move', 'RMB', 1) + vb('Engage', 'E', 0, noGuns && 'No armament') + vb('Screen', 'S') + vb('Turn together', 'T') + vb('Detach…', 'D') + vb('Doctrine', 'C') : '<span class="hint">Allied: not under your command</span>';
    }
  } else if (k === 'fleet' || k === 'cluster') {
    const us = k === 'fleet' ? r.units : r.items.map(i => i.u), cp = comp(us.flatMap(u => u.ships));
    id = idb(symFor(us[0]), k === 'fleet' ? r.name : `${us.length} groups`, `${us.reduce((a, u) => a + u.ships.length, 0)} ships · ${Math.round(cp.t)} kt`);
    st = sv('Strength', pipsHTML(cp, us[0].aff === 'H' ? 'H' : 'F')) + `<div class="sv" style="max-width:460px;white-space:normal"><i>Groups</i><div>${us.slice(0, 10).map(u => `<span class="gchip" data-unit="${u.id}"><span class="sym ${u.aff === 'H' ? 'ho' : ''}"></span>${esc(u.short)}</span>`).join('')}</div></div>`;
    vbs = vb('Zoom to fit', 'dbl', 1) + vb('Fleet doctrine', 'C');
  } else if (k === 'contact') {
    id = idb('<span class="sym ho sus" style="width:20px;height:20px"></span>', r.title, `${r.kind === 'df' ? 'radio direction-finding' : r.kind === 'ellipse' ? 'visual, unidentified' : r.kind === 'port' ? 'in harbour' : 'report'} · ${fmtAge(r.age)} old`);
    st = sv('Estimate', pipsHTML(null, 'H', r.est)) + `<div class="sv task"><i>Latest report</i><b>${esc(r.reports[0])}</b></div>`;
    vbs = vb('Investigate…', 'I', 1) + vb('Mark: hostile', 'M');
  } else if (k === 'port') {
    const us = units.filter(u => u.port === r.n);
    id = idb(`<span class="sym ${r.side === 'RN' ? '' : SIDES[r.side].aff === 'H' ? 'ho sus' : 'al'}" style="width:20px;height:20px"></span>`, r.n, `${esc(SIDES[r.side].name)}${r.docks ? ` · ${r.docks} docks` : ''}${r.yard ? ' · yard' : ''}`);
    st = sv('In port', String(us.filter(u => u.status === 'PORT').reduce((a, u) => a + u.ships.length, 0))) + sv('Docked', String(us.filter(u => u.status === 'DOCK').length)) + sv('Building', String(us.filter(u => u.status === 'BUILD' || u.status === 'FIT').length)) + sv('Coal · oil', `${Math.round(40 + hash(r.n) * 160)} · ${Math.round(8 + hash(r.n + 'o') * 60)} kt`);
    vbs = vb('Yard schedule', 'F4', 1) + vb('Zoom to berths', 'dbl');
  } else if (k === 'enc') {
    id = idb('<span class="st war" style="margin-right:2px">ACTION</span>', r.name, `since ${r.start} · held at ${r.rate}×`);
    st = sv('Phase', esc(r.phase)) + sv('Losses', esc(r.losses), 'amb');
    vbs = vb('Go to', 'dbl', 1) + vb('Pause on event', 'P') + vb('Replay', 'R');
  } else if (k === 'area') {
    id = idb('', r.a.label || r.a.n, 'sea area');
    st = sv('Own capital', `${r.own.cap}${r.al.cap ? ' +' + r.al.cap : ''}`) + sv('Enemy (est.)', `${r.en.cap[0]}–${r.en.cap[1]}`, 'bad');
  } else if (k === 'wreck') {
    id = idb('', r.name, `sunk ${r.at}`); st = `<div class="sv task"><i>Note</i><b>${esc(r.note)}</b></div>`;
  }
  return `${id}<div class="sst">${st}</div><div class="svb">${vbs}<button class="det" data-det="1">${hud.details ? 'Less' : 'Details'}<span class="k">V</span></button></div>`;
}
let cardKey = '', cardT = 0;
function refreshCard(force) {
  const el = $('sel');
  if (!sel) { el.classList.remove('on'); cardKey = ''; return; }
  const key = sel.k + (sel.ref.id || sel.ref.name || '') + hud.details;
  if (!force && key === cardKey && performance.now() - cardT < 1000) return;
  cardKey = key; cardT = performance.now();
  el.classList.add('on'); el.classList.toggle('det', hud.details);
  $('selRow').innerHTML = compactSel();
  if (hud.details) {
    let h = '', b = '';
    if (sel.k === 'ship') [h, b] = isEnemy(sel.ref.u) ? cardEnemyUnit(sel.ref.u, sel.ref) : cardShip(sel.ref);
    else if (sel.k === 'unit') [h, b] = isEnemy(sel.ref) ? cardEnemyUnit(sel.ref) : cardUnit(sel.ref);
    else if (sel.k === 'fleet') [h, b] = cardFleet(sel.ref.name, sel.ref.units);
    else if (sel.k === 'cluster') [h, b] = cardFleet(null, sel.ref.items.map(i => i.u));
    else if (sel.k === 'contact') [h, b] = cardContact(sel.ref);
    else if (sel.k === 'port') [h, b] = cardPort(sel.ref);
    else if (sel.k === 'enc') [h, b] = cardEnc(sel.ref);
    else if (sel.k === 'area') [h, b] = cardArea(sel.ref);
    else [h, b] = ['', '<div class="cc hint">Nothing more to show.</div>'];
    $('selDetail').innerHTML = `<div class="ph">${h}</div><div class="db">${b}</div>`;
  }
}
$('selRow').addEventListener('click', e => {
  if (e.target.closest('[data-det]')) { hud.details = !hud.details; refreshCard(true); return; }
  const g = e.target.closest('[data-unit]'); if (g) { const u = unitById[g.dataset.unit]; select('unit', u); flyTo(u.lat, u.lon, atSea(u) ? Math.min(mpp(), 400) : 3); return; }
  const b = e.target.closest('button'); if (!b) return; const t = b.textContent;
  if (/F3|Design/.test(t)) openScreen('designs'); else if (/F4|Yard/.test(t)) openScreen('yards'); else if (/F2|Register/.test(t)) openScreen('fleet');
  else if (/Go to|Zoom/.test(t) && sel) { const r = sel.ref; const la = r.lat ?? r.at?.[0] ?? r.items?.[0].u.lat ?? r.units?.[0].lat, lo = r.lon ?? r.at?.[1] ?? r.items?.[0].u.lon ?? r.units?.[0].lon; flyTo(la, lo, sel.k === 'port' ? 3 : sel.k === 'enc' ? 26 : mpp() / 4); }
  else toast(`<b>${esc(t.replace(/(RMB|dbl|[A-Z]\d*)$/, '').trim())}</b> · (mockup)`);
});

// ---------------------------------------------------------------- help
$('help').innerHTML = `<h3>Controls <span class="dim" style="float:right;letter-spacing:0;text-transform:none;font-weight:400">? or Esc to close</span></h3><div class="cols">
 <div><b>Camera</b><div class="r"><span class="kbd">Wheel</span>orbit to a single ship, continuous</div><div class="r"><span class="kbd">Drag</span>pan</div><div class="r"><span class="kbd">Home</span>back to the globe</div><div class="r"><span class="kbd">1–8</span>saved views (hover the minimap)</div><div class="r"><span class="kbd">F</span>fly to the selection</div><div class="r"><span class="kbd">M</span>minimap on/off</div></div>
 <div><b>Command</b><div class="r"><span class="kbd">Click</span>select at this zoom's level</div><div class="r"><span class="kbd">Dbl</span>a ship's whole group</div><div class="r"><span class="kbd">RMB</span>the order the cursor names</div><div class="r"><span class="kbd">R-drag</span>preview, release to give it</div><div class="r"><span class="kbd">Hold RMB</span>radial menu, flick to pick</div><div class="r"><span class="kbd">Shift</span>queue · <span class="kbd">Alt</span> this ship only</div><div class="r"><span class="kbd">Edge marker</span>click to go, right-drag to order it here</div></div>
 <div><b>Time and panels</b><div class="r"><span class="kbd">Space</span>pause · <span class="kbd">Shift+1–7</span> speed</div><div class="r"><span class="kbd">Q</span>next decision</div><div class="r"><span class="kbd">V</span>selection details</div><div class="r"><span class="kbd">U</span>all forces · <span class="kbd">J</span> log</div><div class="r"><span class="kbd">Tab</span>hold: every order line</div><div class="r"><span class="kbd">F2–F10</span>screens · <span class="kbd">Esc</span> back</div></div></div>`;
function toggleHelp() { hud.help = !hud.help; $('help').classList.toggle('on', hud.help); }
$('helpBtn').addEventListener('click', toggleHelp);
addEventListener('keydown', e => {
  if (e.ctrlKey || e.altKey || e.metaKey) return;
  const k = e.key.toLowerCase();
  if (k === 'v') { hud.details = !hud.details; refreshCard(true); }
  else if (k === 'u') toggleDrawer('fdrawer'); else if (k === 'j') toggleDrawer('ldrawer'); else if (k === 'm') toggleMinimap();
  else if (e.key === '?' || k === 'h') toggleHelp();
  else if (e.key === 'Escape') { if (hud.help) toggleHelp(); document.querySelectorAll('.drawer.open').forEach(d => toggleDrawer(d.id)); $('layers').style.display = 'none'; }
  else if (['o', 'r', 'l', 'n', 'g'].includes(k)) setTimeout(renderLens, 0);
});
document.addEventListener('mousedown', e => { if (!e.target.closest('#layers') && !e.target.closest('#lens')) { if ($('layers').style.display === 'block') { $('layers').style.display = 'none'; renderLens(); } } });

// edge markers keep clear of the HUD
window.pipInset = () => ({ x0: hud.frailMin || $('fdrawer').classList.contains('open') ? 30 : 270, y0: 120, x1: 1840, y1: sel ? (hud.details ? 700 : 990) : 1040 });

// ---------------------------------------------------------------- per frame
let lastSlow = 0, overSel = false, overRail = false;
$('sel').addEventListener('mouseenter', () => overSel = true); $('sel').addEventListener('mouseleave', () => overSel = false);
$('frail').addEventListener('mouseenter', () => overRail = true); $('frail').addEventListener('mouseleave', () => overRail = false);
function uiFrame(now, bi) {
  const d = new Date(worldMs);
  $('dTime').textContent = `${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}:${String(d.getUTCSeconds()).padStart(2, '0')} GMT`;
  const y = railY(mpp()); $('railTh').style.top = y + 'px'; $('railW').style.top = y + 'px'; $('railW').innerHTML = `${BANDS[bi].name.toUpperCase()}<br>${fmtKm(viewWidthM())}`;
  bandBanner(bi);
  const tip = $('tip'), cur = $('cur');
  hover = null;
  if (mouse.in && !drag?.moved && !radialOpen && !screenOpen()) {
    hover = hitTest(mouse.x, mouse.y);
    if (hover && !dragOrder) { tip.innerHTML = tipHTML(hover); tip.style.display = 'block'; tip.style.left = Math.min(mouse.x + 18, SW - 380) + 'px'; tip.style.top = Math.min(mouse.y + 14, SH - tip.offsetHeight - 8) + 'px'; }
    else tip.style.display = 'none';
    const u = ownCommandable() || (dragOrder && dragOrder.u);
    if (u) { const v = smartVerb(u, mouse.x, mouse.y); if (v) { cur.textContent = (dragOrder ? '▸ ' : '') + v.txt + (dragOrder && dragOrder.u.shift ? ' · queued' : ''); cur.className = v.en ? 'en' : ''; cur.style.display = 'block'; cur.style.left = (mouse.x + 16) + 'px'; cur.style.top = (mouse.y - 22) + 'px'; } }
    else cur.style.display = 'none';
  } else { tip.style.display = 'none'; cur.style.display = 'none'; }
  $('coords').textContent = mouse.geo && mouse.in ? fmtLL(mouse.geo[0], mouse.geo[1]) : fmtLL(cam.lat, cam.lon);
  if (hoverRow) {
    const p = hoverRow.p0, s = proj(p.lat, p.lon);
    ctx.save(); ctx.strokeStyle = '#D9B868'; ctx.lineWidth = 2;
    if (s.vis && inMap(s.x, s.y)) { ctx.beginPath(); ctx.arc(s.x, s.y, 20, 0, 7); ctx.stroke(); }
    else { const pip = pipList.find(g => g.us.includes(hoverRow.ref) || g.k === hoverRow.ref); if (pip) ctx.strokeRect(pip.box.x - 3, pip.box.y - 3, pip.box.w + 6, pip.box.h + 6); }
    ctx.restore();
  }
  if (now - lastSlow > 300) {
    lastSlow = now;
    if (!overRail || fly) renderFrail();
    renderLocator(); renderClock(); renderTicker(); drawPop();
    if (!overSel) refreshCard(false);
    document.querySelectorAll('.ach').forEach(a => { const e = encById[a.dataset.e]; a.classList.toggle('cur', bi <= 2 && hav({ lat: cam.lat, lon: cam.lon }, e) < e.r * 1500); });
  }
}

// ---------------------------------------------------------------- boot
renderTabs(); renderLens(); renderRail(); renderActs(); renderDQ(); renderClock(); renderTicker(); refreshCard(true);
(() => { const h = location.hash.slice(1); if (/^[1-8]$/.test(h)) setTimeout(() => gotoBookmark(+h - 1), 50); else if (SCREENS.some(s => s[0] === h) && h !== 'map') setTimeout(() => openScreen(h), 50); })();
