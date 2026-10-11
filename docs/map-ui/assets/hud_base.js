// Shared pieces taken from ui.js (cards, forces items, tooltips); the HUD around them is below.
// Panels, cards, tooltips around the map.
'use strict';
const $ = id => document.getElementById(id);
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;');
const hhmm = ms => { const d = new Date(ms); return `${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}`; };
const SPEED_LBL = ['1×', '4×', '12×', '1m', '10m', '1h', '8h'];
let fTab = 'own', openTheatres = {};
function theatreOf(lat, lon) {
  if (lat > 48 && lat < 63 && lon > -14 && lon < 12) return 'Home waters';
  if (lat > 53 && lat < 67 && lon >= 12 && lon < 32) return 'Baltic';
  if (lat > 29 && lat < 47 && lon > -7 && lon < 42) return 'Mediterranean';
  if (lon > -100 && lon <= -7) return 'Atlantic';
  if (lon > 20 && lon < 100 && lat < 30) return 'Indian Ocean';
  return 'Pacific';
}
function pipsHTML(cp, aff = 'F', est) {
  const col = aff === 'H' ? 'var(--ho)' : aff === 'A' ? 'var(--al)' : aff === 'N' ? 'var(--ne)' : 'var(--fr)';
  let h = `<div class="pips">`;
  const capMax = est ? est.cap[1] : cp.cap, capMin = est ? est.cap[0] : cp.cap;
  for (let i = 0; i < Math.min(capMax, 34); i++) h += i < capMin ? `<i style="background:${col}"></i>` : `<i class="h" style="box-shadow:inset 0 0 0 1px ${col}"></i>`;
  const cru = est ? est.cru[1] : cp.cru; if (capMax && cru) h += '<i class="g"></i>';
  for (let i = 0; i < Math.min(cru, 24); i++) h += `<i class="c" style="background:${col}"></i>`;
  const lt = est ? est.lt[1] : cp.lt; if (lt) h += `<em style="width:${Math.min(50, lt * 1.2)}px;background:${col}"></em>`;
  const n = est ? `${capMin}–${capMax} cap?` : [cp.cap && `${cp.cap} cap`, cp.cru && `${cp.cru} cr`, cp.lt && `${cp.lt} lt`, cp.mer && `${cp.mer} mv`, cp.aux && `${cp.aux} aux`].filter(Boolean).join(' · ');
  return h + `<span class="n">${n}</span></div>`;
}
function statusChip(u) {
  if (u.enc && !isEnemy(u)) return '<span class="st war">ACTION</span>';
  const m = { SEA: ['sea', 'AT SEA'], PORT: ['port', 'PORT'], DOCK: ['dock', 'DOCK'], BUILD: ['build', 'BUILDING'], FIT: ['fit', 'FITTING OUT'] }[u.status];
  return `<span class="st ${m[0]}">${m[1]}</span>`;
}
function ctlChip(u) {
  if (u.sent) return '<span class="st tr">◷ SIGNAL</span>';
  const c = u.ctl || 'TASK'; return c === '—' ? '' : `<span class="st ${c === 'DIRECT' ? 'dir' : c === 'TASK' ? 'task' : 'doc'}">${c}</span>`;
}
function onScreen(u) { return u.ships.some(sh => { const q = shipLL(sh), p = proj(q[0], q[1]); return p.vis && inMap(p.x, p.y); }) || (() => { const p = proj(u.lat, u.lon); return p.vis && inMap(p.x, p.y); })(); }
function forcesItems() {
  const bi = bandIdx(), level = bi >= 3 ? 'fleet' : 'unit';
  const items = [];
  if (fTab === 'own') {
    if (level === 'fleet') for (const f of Object.values(fleets)) items.push({ k: 'fleet', ref: f, us: f.units, name: f.name, cp: f.comp, aff: 'F' });
    else for (const u of units) if (u.own) items.push({ k: 'unit', ref: u, us: [u], name: u.short, sub: u.fleet, cp: u.comp, aff: 'F' });
  } else if (fTab === 'contacts') {
    for (const u of units) if (knownEnemy(u)) items.push({ k: 'unit', ref: u, us: [u], name: u.intel.cert === 'id' ? u.name : (u.intel.est || 'Unknown'), sub: u.intel.cert === 'id' ? 'identified' : 'suspect', cp: u.comp, aff: 'H', sus: u.intel.cert !== 'id', age: u.intel.age });
    for (const k of contacts) { const pos = k.kind === 'df' ? { lat: unitById[k.truth].lat, lon: unitById[k.truth].lon } : k.kind === 'ellipse' ? (() => { const f = unitById[k.from], p = dest(f.lat, f.lon, k.brg, 23000); return { lat: p[0], lon: p[1] }; })() : { lat: k.at[0], lon: k.at[1] }; items.push({ k: 'contact', ref: k, pos, name: k.title, sub: k.kind === 'df' ? 'DF bearings' : k.kind === 'port' ? 'in port' : k.kind === 'ellipse' ? 'visual, unidentified' : 'report', est: k.est, aff: 'H', sus: true, age: k.age }); }
  } else {
    for (const u of units) if (!u.own && !isEnemy(u)) items.push({ k: 'unit', ref: u, us: [u], name: u.name, sub: SIDES[u.side].name, cp: u.comp, aff: u.neutral ? 'N' : 'A' });
  }
  const ctr = { lat: cam.lat, lon: cam.lon }, near = nearRadius();
  for (const it of items) {
    const ps = it.us ? it.us.map(u => ({ lat: u.lat, lon: u.lon })) : [it.pos];
    it.d = Math.min(...ps.map(p => hav(ctr, p))); const p0 = ps.reduce((a, p) => hav(ctr, p) < hav(ctr, a) ? p : a, ps[0]); it.b = brg(ctr, p0); it.p0 = p0;
    it.vis = it.us ? it.us.some(onScreen) : (() => { const p = proj(p0.lat, p0.lon); return p.vis && inMap(p.x, p.y); })();
    it.sec = it.vis ? 0 : it.d <= near && bi <= 2 ? 1 : 2;
    it.th = theatreOf(p0.lat, p0.lon);
  }
  return { items, near, level };
}
function rowHTML(it) {
  const u = it.k === 'unit' ? it.ref : null;
  const isSel = sel && sel.ref === it.ref;
  const symCls = it.aff === 'H' ? 'ho' + (it.sus ? ' sus' : '') : it.aff === 'A' ? 'al' : it.aff === 'N' ? 'ne' : '';
  let st = '';
  if (u) st = statusChip(u) + (u.own && atSea(u) ? ' ' + ctlChip(u) : '');
  else if (it.k === 'fleet') { const sea = it.us.filter(atSea).length, act = it.us.some(x => x.enc); st = act ? '<span class="st war">ACTION</span>' : sea ? `<span class="st sea">${sea} AT SEA</span>` : it.us.some(x => x.status === 'BUILD' || x.status === 'FIT') ? '<span class="st build">YARDS</span>' : '<span class="st port">PORT</span>'; }
  else st = `<span class="st port">${fmtAge(it.age)}</span>`;
  const rr = it.sec === 0 ? (u && u.kn && atSea(u) ? `<span class="mono">${fmtBrg(u.hdg)} ${u.kn} kn</span>` : it.k === 'fleet' ? `<span class="mono">${it.us.length} grp</span>` : '') : `<span class="mono">${fmtKm(it.d)} ${fmtBrg(it.b)}</span>`;
  return `<div class="row ${isSel ? 'sel' : ''}" data-k="${it.k}" data-id="${it.k === 'fleet' ? esc(it.ref.name) : it.k === 'contact' ? it.ref.id : it.ref.id}">
    <span class="sym ${symCls}"></span>
    <div class="nm"><b>${esc(it.name)}</b>${it.sub ? `<span class="s">${esc(it.sub)}</span>` : ''}${it.est ? pipsHTML(null, 'H', it.est) : pipsHTML(it.cp, it.aff, it.sus ? { cap: [0, it.cp.cap], cru: [0, it.cp.cru], lt: [0, it.cp.lt] } : null)}</div>
    <div class="rr">${st}<br>${rr}</div></div>`;
}
const BOOKMARKS = [
  { n: 'Horns Reef', f: () => [56.60, 5.88, 32] }, { n: 'North Sea', f: () => [56.5, 2.7, 640] }, { n: 'Home waters', f: () => [55.5, 1.0, 1700] }, { n: 'World', f: () => [30, 5, 16000] },
  { n: 'Lion', f: () => { const q = shipLL(shipsByName['Lion']); return [q[0], q[1], 0.42]; } }, { n: 'Rosyth dock', f: () => [56.0215, -3.4440, 0.9] }, { n: 'Abrolhos', f: () => [-18.1, -37.49, 18] }, { n: 'Clydebank', f: () => [55.9010, -4.4003, 0.9] },
];
function gotoBookmark(i) { const b = BOOKMARKS[i]; if (!b) return; const [la, lo, m] = b.f(); flyTo(la, lo, m); if (b.n === 'Lion') select('ship', shipsByName['Lion'], false); }
let decisions = DECISIONS.slice();
function jumpRef(ref) {
  const [k, id] = ref;
  if (k === 'ship') { const sh = shipsByName[id]; select('ship', sh, false); const q = shipLL(sh); flyTo(q[0], q[1], Math.min(mpp(), 12)); }
  if (k === 'unit') { const u = unitById[id]; select('unit', u, false); flyTo(u.lat, u.lon, atSea(u) ? (u.enc ? 22 : 160) : 2); }
  if (k === 'contact') { const c = contacts.find(x => x.id === id); select('contact', c, false); const f = unitById[c.from]; const q = dest(f.lat, f.lon, c.brg, 18000); flyTo(q[0], q[1], 40); }
}
$('dq').addEventListener('click', e => {
  const d = e.target.closest('.dq'); if (!d || d.dataset.i === undefined) return; const it = decisions[+d.dataset.i];
  const b = e.target.closest('button');
  if (b) { const a = it.acts[+b.dataset.a]; addLog('cmd', `You: ${it.title.split(':')[0]} — ${a}`); if (a.startsWith('Open yards')) openScreen('yards'); else toast(`<b>${esc(a)}</b> · ${esc(it.title)}`); decisions.splice(+d.dataset.i, 1); renderDQ(); return; }
  jumpRef(it.ref);
});
function nextDecision() { if (decisions[0]) jumpRef(decisions[0].ref); }
let logFilter = 'all';
function renderLog() {
  const F = [['all', 'All'], ['gun', 'Gun'], ['dmg', 'Dmg'], ['sig', 'Sig'], ['cmd', 'Cmd'], ['yrd', 'Yards']];
  $('logf').innerHTML = F.map(([k, n]) => `<span class="chip ${logFilter === k ? 'on' : ''}" data-f="${k}">${n}</span>`).join('');
  $('log').innerHTML = LOGS.filter(l => logFilter === 'all' || l.c === logFilter).slice(0, 40).map((l, i) => `<div class="lg" data-i="${i}"><span class="t">${l.t}</span><span class="c ${l.c}">${l.c}</span><span>${esc(l.m)}</span></div>`).join('');
}
const bar = (v, cls) => `<span class="bar"><i class="${cls || (v < .6 ? 'r' : v < .85 ? 'a' : '')}" style="width:${Math.round(v * 100)}%"></i></span>`;
function doctrineHTML(u) {
  const D = [['roe', 'ROE'], ['range', 'Engage at'], ['retreat', 'Retire if'], ['ammo', 'Ammunition'], ['torp', 'Torpedoes'], ['emcon', 'Emissions'], ['form', 'Formation']];
  return `<h4>Doctrine <span class="rt">from ${esc(u.fleet)} · <span class="gold">bold</span> = override</span></h4><div class="kv">` +
    D.map(([k, n]) => `<span class="k">${n}</span><span class="v ${u.over.includes(k) ? '' : 'inh'}">${u.over.includes(k) ? '<b>' + esc(u.doct[k]) + '</b>' : esc(u.doct[k])}</span>`).join('') + '</div>';
}
function verbsHTML(u) {
  const V = [['Move / course', 'RMB', 1], ['Engage', 'E'], ['Intercept', 'I'], ['Screen / escort', 'S'], ['Patrol area', 'P'], ['Turn together', 'T'], ['Turn in succession', 'Y'], ['Detach ships…', 'D'], ['Return to base', 'B'], ['Doctrine card…', 'C']];
  const noGuns = u.comp.mer > 0 && !u.comp.cap && !u.comp.cru;
  return '<div class="verbs">' + V.map(([n, k, p]) => `<button class="${p ? 'pri' : ''}" ${noGuns && (n === 'Engage' || n === 'Intercept') ? 'disabled title="No armament"' : ''}>${n}<span class="k">${k}</span></button>`).join('') + '</div>';
}
function ordersHTML(u) {
  let h = `<h4>Orders <span class="rt">${ctlChip(u)}</span></h4><div class="kv">`;
  h += `<span class="k">Task</span><span class="v" style="white-space:normal;color:var(--text);font-family:'Archivo Narrow';font-size:12px">${esc(u.task || '—')}</span>`;
  if (atSea(u)) h += `<span class="k">Executing</span><span class="v">course ${fmtBrg(u.hdg)} · ${u.kn} kn</span>`;
  if (u.sent) h += `<span class="k">Sent</span><span class="v amb">${u.sent.hdg}° at ${u.sent.at} · flags · in transit ${Math.max(0, Math.round(u.sent.transit))} s</span>`;
  let t = 0, a = unitPos(u);
  u.wps.forEach((w, i) => { t += hav(a, w) / (u.kn * .5144 || 1); a = w; h += `<span class="k">${i ? 'then' : 'Next'}</span><span class="v">${fmtLL(w.lat, w.lon)} · ETA ${hhmm(worldMs + t * 1000)}</span>`; });
  if (!u.wps.length && atSea(u)) h += `<span class="k">Waypoints</span><span class="v inh">none · right-click the map</span>`;
  return h + '</div>';
}
function cardNone() {
  const bi = bandIdx(), m = mpp();
  const ownIn = units.filter(u => u.own && onScreen(u)), enIn = units.filter(u => knownEnemy(u) && onScreen(u));
  const co = comp(ownIn.flatMap(u => u.ships)), ce = comp(enIn.flatMap(u => u.ships));
  const sa = sunAlt(cam.lat, cam.lon);
  let set = null; for (let s = 0; s < 86400; s += 300) { const t0 = worldMs + s * 1000; const [d, l] = sunPos(t0); const alt = Math.asin(clamp(dot(v3(cam.lat, cam.lon), v3(d, l)), -1, 1)) * R2D; if ((sa >= 0) !== (alt >= 0)) { set = t0; break; } }
  const h = `View · ${BANDS[bi].name} band · ${fmtKm(viewWidthM())} across · centre ${fmtLL(cam.lat, cam.lon)} <span class="rt">nothing selected — click anything on the map</span>`;
  const b = `<div class="cc" style="width:300px"><h4>Strength in view</h4><table class="t"><tr><th></th><th>Own</th><th>Known enemy</th></tr>
    <tr><td>Capital ships</td><td class="n fr">${co.cap}</td><td class="n ho">${ce.cap}</td></tr><tr><td>Cruisers</td><td class="n fr">${co.cru}</td><td class="n ho">${ce.cru}</td></tr>
    <tr><td>Light forces</td><td class="n fr">${co.lt}</td><td class="n ho">${ce.lt}</td></tr><tr><td>Displacement</td><td class="n fr">${Math.round(co.t)} kt</td><td class="n ho">${Math.round(ce.t)} kt</td></tr></table>
    <div class="hint" style="margin-top:4px">Plus unidentified: <span class="ho">heavy ships? 6–16</span> (smoke, SSE) and <span class="ho">DK fix</span> (DF).</div></div>
    <div class="cc" style="width:270px"><h4>Conditions at centre</h4><div class="kv">
    <span class="k">Sun</span><span class="v">${sa >= 0 ? '+' : ''}${sa.toFixed(0)}° · ${sa >= 0 ? 'sets' : 'rises'} ${set ? hhmm(set) : '—'} GMT</span>
    <span class="k">Visibility</span><span class="v">${sa > 0 ? '10 nm; haze 6 nm to the E' : 'night: 1–2 nm'}</span><span class="k">Wind · sea</span><span class="v">NW 3 · state 2</span>
    <span class="k">Horizon</span><span class="v">foretop 30 m: 20 km</span><span class="k">Signals</span><span class="v">flags 40 s · lamp 1 min · W/T 3 min</span></div></div>
    <div class="cc" style="flex:1"><h4>Controls</h4><div class="hint" style="column-count:2;column-gap:18px">
    <b>Click</b> select at this zoom's level · <b>double-click</b> a ship for its group<br><b>Right-click</b> the smart order (cursor names it) · <b>right-drag</b> shape it<br><b>Hold right</b> radial menu, flick to pick<br><b>Shift</b> queue · <b>Alt</b> this ship only · <b>Ctrl</b> preview<br><b>Tab</b> (hold) all order lines · <b>F</b> fly to selection<br>
    <b>Wheel</b> orbit → ship, continuous · <b>Home</b> globe<br><b>1–7</b> camera views · <b>Shift+1–7</b> speed · <b>Space</b> pause<br><b>Q</b> next decision · <b>F1–F10</b> screens, <b>Esc</b> back<br><b>Edge markers</b> own groups near the view: click to go, right-drag from one to order it here</div></div>`;
  return [h, b];
}
function cardUnit(u) {
  const h = `<span class="sym" style="margin-right:2px"></span>${esc(u.name)} <span class="dim" style="text-transform:none;letter-spacing:0">· ${esc(u.fleet)} · ${esc(u.cmdr || '')}</span> ${statusChip(u)} ${u.own && atSea(u) ? ctlChip(u) : ''}<span class="rt">${u.ships.length} ships · ${Math.round(u.comp.t)} kt · ${atSea(u) ? fmtLL(u.lat, u.lon) : u.port}</span>`;
  const rows = u.ships.slice(0, 9).map(sh => `<tr class="hov ${sel && sel.ref === sh ? 'sel' : ''}" data-ship="${esc(sh.name)}"><td>${esc(sh.name)}</td><td class="dim">${esc(sh.cls || sh.T.n)}</td><td>${bar(sh.flot)}</td><td class="n">${sh.flood ? sh.flood + ' t' : '—'}</td><td class="m">${sh.tgt ? '→ ' + esc(sh.tgt.name) : sh.fire ? '<span class="amb">fire</span>' : '—'}</td><td>${bar(sh.ammo, 'b')}</td></tr>`).join('');
  const more = u.ships.length > 9 ? `<tr><td colspan="6" class="dim">+ ${u.ships.length - 9} more · click to expand</td></tr>` : '';
  if (!u.own) return [h, `<div class="cc" style="width:100%"><table class="t"><tr><th>Ship</th><th>Class</th><th>Condition</th><th>Water</th><th>Target</th><th>Ammo</th></tr>${rows}${more}</table><div class="hint">Allied: not under your command. Request co-operation through the Admiralty (F8).</div></div>`];
  let b = `<div class="cc" style="width:430px"><h4>Ships <span class="rt">click a row for its card</span></h4><table class="t" id="shipTbl"><tr><th>Ship</th><th>Class</th><th>Flotation</th><th>Water</th><th>Target</th><th>Ammo</th></tr>${rows}${more}</table></div>`;
  if (u.status === 'BUILD' || u.status === 'FIT') {
    b += `<div class="cc" style="flex:1"><h4>Construction</h4><div class="kv"><span class="k">Yard</span><span class="v">${esc(u.cmdr)}</span><span class="k">Stage</span><span class="v">${esc(u.build.stage)}</span><span class="k">Progress</span><span class="v">${bar(u.build.p, 'b')} ${Math.round(u.build.p * 100)}%</span><span class="k">Completes</span><span class="v">${u.build.done}</span><span class="k">Design</span><span class="v">${esc(CLASSES[u.ships[0].name] || u.ships[0].name)} · open in Designs (F3)</span></div><div class="verbs" style="margin-top:6px"><button class="pri">Yard schedule (F4)<span class="k">F4</span></button><button>Accelerate (+£0.4M)<span class="k">A</span></button><button>Open design<span class="k">F3</span></button><button>Suspend work<span class="k">X</span></button></div></div>`;
    return [h, b];
  }
  b += `<div class="cc" style="width:290px">${ordersHTML(u)}</div><div class="cc" style="width:290px">${doctrineHTML(u)}</div><div class="cc" style="flex:1"><h4>Order <span class="rt">or radial (hold RMB)</span></h4>${verbsHTML(u)}</div>`;
  return [h, b];
}
function cardShip(sh) {
  const u = sh.u, T = TURRETS[sh.cls], isLion = sh.name === 'Lion';
  const h = `<span class="sym" style="margin-right:2px"></span>${esc(sh.name)} <span class="dim" style="text-transform:none;letter-spacing:0">· ${esc(sh.cls ? sh.cls + ' class' : '')} ${esc(sh.T.n.toLowerCase())} · ${esc(u.short)}${isLion ? ' · flag V-Adm Beatty · Capt Chatfield' : ''}</span> ${statusChip(u)} ${u.own && atSea(u) ? ctlChip(u) : ''}<span class="rt">${atSea(u) ? `course ${fmtBrg(u.hdg)} · ${u.kn} kn · ` : ''}${fmtLL(...shipLL(sh))}</span>`;
  const turrets = sh.turrets || (T ? T.slice(0, -1).map((t, i) => [t, sh.tgt ? fmtBrg((brg({ lat: shipLL(sh)[0], lon: shipLL(sh)[1] }, { lat: shipLL(sh.tgt)[0], lon: shipLL(sh.tgt)[1] }))) : 'fore & aft', sh.tgt ? sh.tgt.name : '—', Math.round(80 + hash(sh.name + t) * 30), sh.tgt ? 'FIRING' : 'TRAINED']) : null);
  const gun = T ? T.slice(-1)[0] : sh.type === 'CL' ? '6 in ×9 (single)' : sh.type === 'CA' ? '9.2 in ×6, 7.5 in ×4' : sh.type === 'DD' || sh.type === 'DL' ? '4 in ×3 · TT 21 in ×4' : sh.type === 'SL' ? '4.7 in ×2 · DC ×4' : sh.type === 'MV' ? 'unarmed' : '—';
  const sprite = (sh.T.k === 'cap') ? `<div style="position:relative;height:56px;margin:2px 0 4px;background:#0f1d28"><img src="../designer-ui/assets/dreadnought.png" style="position:absolute;left:4px;top:5px;width:272px;height:47px;${isEnemy(u) ? 'filter:grayscale(.7)' : ''}">${isLion ? '<span style="position:absolute;left:150px;top:14px;width:22px;height:22px;border-radius:50%;background:radial-gradient(rgba(224,106,90,.85),rgba(224,106,90,0))"></span><span style="position:absolute;left:94px;top:8px;width:16px;height:16px;border-radius:50%;background:radial-gradient(rgba(227,164,82,.8),rgba(227,164,82,0))"></span><span style="position:absolute;left:60px;top:30px;width:14px;height:14px;border-radius:50%;background:radial-gradient(rgba(227,164,82,.8),rgba(227,164,82,0))"></span><span class="mono" style="position:absolute;left:176px;top:2px;font-size:9.5px;color:#ffb3a9">Q · mag flooded</span>' : ''}</div>` : '';
  let b = `<div class="cc" style="width:300px"><h4>Condition <span class="rt">${sh.hits} hits</span></h4>${sprite}<div class="kv">
    <span class="k">Flotation</span><span class="v">${bar(sh.flot)} ${Math.round(sh.flot * 100)}%${sh.flood ? ` · ${sh.flood} t aboard` : ''}${sh.list ? ` · list ${sh.list}°` : ''}</span>
    <span class="k">Structure</span><span class="v">${bar(sh.str)} ${Math.round(sh.str * 100)}%</span>
    <span class="k">Fires</span><span class="v ${sh.fire ? 'amb' : ''}">${sh.fire ? esc(sh.fireAt || 'amidships') : 'none'}</span>
    <span class="k">Speed</span><span class="v">${u.kn} kn of ${sh.T.k === 'cap' ? 27.5 : 29} · fuel ${Math.round(sh.fuel * 100)}%</span>
    <span class="k">Crew</span><span class="v">${sh.crew.toLocaleString('en')}${sh.crewLoss ? ` <span class="bad">−${sh.crewLoss}</span>` : ''}</span></div></div>`;
  b += `<div class="cc" style="width:330px"><h4>Armament <span class="rt">${esc(gun)}</span></h4>`;
  if (turrets) b += `<table class="t"><tr><th>Mount</th><th>Brg</th><th>Target</th><th>Rnds</th><th>State</th></tr>${turrets.map(t => `<tr><td class="m">${t[0]}</td><td class="m">${t[1]}</td><td>${esc(t[2])}</td><td class="n">${t[3]}</td><td class="m ${/OUT/.test(t[4]) ? 'bad' : t[4] === 'FIRING' ? 'amb' : 'dim'}">${t[4]}</td></tr>`).join('')}
    <tr><td class="m">Sec</td><td class="m">—</td><td class="dim">held</td><td class="n">${isLion ? 1340 : 1200}</td><td class="m dim">ROE: heavy only</td></tr></table>`;
  else b += `<div class="kv"><span class="k">Guns</span><span class="v">${esc(gun)}</span><span class="k">State</span><span class="v">${sh.tgt ? 'engaging ' + esc(sh.tgt.name) : 'cleared away, not engaged'}</span><span class="k">Ammunition</span><span class="v">${bar(sh.ammo, 'b')} ${Math.round(sh.ammo * 100)}%</span></div>`;
  b += '</div>';
  if (sh.tgt) {
    const a = shipLL(sh), t = shipLL(sh.tgt), r = hav({ lat: a[0], lon: a[1] }, { lat: t[0], lon: t[1] });
    b += `<div class="cc" style="width:300px"><h4>Fire control <span class="rt">director · fore</span></h4><div class="kv">
      <span class="k">Target</span><span class="v">${esc(sh.tgt.name)} · identified (visual)</span>
      <span class="k">Range</span><span class="v">${Math.round(r / KYD * 1000).toLocaleString('en')} yd · RF ${Math.round(r / KYD * 1000 - 250).toLocaleString('en')} ±250</span>
      <span class="k">Rate</span><span class="v">−150 yd/min · bearing drift 0.2°/min</span>
      <span class="k">Plot</span><span class="v">Dreyer table: 6 fixes, good</span>
      <span class="k">Salvos</span><span class="v">${isLion ? 38 : 20 + Math.round(hash(sh.name) * 20)} · straddles ${isLion ? 6 : 3} · hits seen ${isLion ? 3 : 1}</span>
      <span class="k">Est. hit rate</span><span class="v">${isLion ? '2.1' : (1 + hash(sh.name) * 2).toFixed(1)}% · spotting 60% (smoke)</span></div></div>`;
  } else b += `<div class="cc" style="width:300px"><h4>Fire control</h4><div class="hint">No target. ${u.status === 'DOCK' ? 'In dock: guns not manned.' : u.status === 'BUILD' || u.status === 'FIT' ? 'Not in commission.' : 'Lookouts and rangefinders closed up.'}</div></div>`;
  b += `<div class="cc" style="flex:1"><h4>This ship <span class="rt"><span class="kbd">Alt</span>+order = this ship only</span></h4><div class="verbs">
    <button class="pri">Haul out of line<span class="k">H</span></button><button>Change target…<span class="k">E</span></button><button>Damage control ▸<span class="k">K</span></button><button>Ship's-eye plot<span class="k">V</span></button>
    <button>Open design<span class="k">F3</span></button><button>In fleet register<span class="k">F2</span></button><button>Captain's report<span class="k">I</span></button><button>Select group<span class="k">dbl</span></button></div>
    <div class="hint" style="margin-top:5px">${u.own && atSea(u) ? `Group task: ${esc(u.task)}` : esc(u.task || '')}</div></div>`;
  return [h, b];
}
function cardEnemyUnit(u, sh) {
  const it = u.intel, id = it.cert === 'id';
  const h = `<span class="sym ho ${id ? '' : 'sus'}" style="margin-right:2px"></span>${id ? esc(sh ? sh.name : u.name) : esc(it.est || 'Unknown')} <span class="dim" style="text-transform:none;letter-spacing:0">· ${id ? 'identified' : 'suspect · type inferred'} · ${esc(it.src)} · ${it.age} min</span><span class="rt">contact · ${fmtLL(u.lat, u.lon)}</span>`;
  const ships = u.ships.map(s => `<tr><td>${id ? esc(s.name) : '?'}</td><td class="dim">${id ? esc(s.cls || s.T.n) : esc(s.T.n) + '?'}</td><td class="m">${id ? (s.tgt ? '→ ' + esc(s.tgt.name) : '—') : '—'}</td><td class="m">${id ? (s.hits ? s.hits + ' hits seen' : '—') : '—'}</td></tr>`).join('');
  const b = `<div class="cc" style="width:420px"><h4>What we hold</h4><table class="t"><tr><th>Ship</th><th>Class</th><th>Firing at</th><th>Damage seen</th></tr>${ships}</table></div>
    <div class="cc" style="width:360px"><h4>Report trail</h4><div class="hint">${esc(it.by)} · ${esc(it.src)} · last ${it.age} min ago<br>${id ? 'Class confirmed by silhouette (tripod masts, 2 funnels)' : 'Frame dashed: type inferred, not confirmed'}<br>Course ${fmtBrg(u.hdg)} · ~${u.kn} kn (${id ? '6 fixes, firm' : '2 fixes, fan'})</div></div>
    <div class="cc" style="flex:1"><h4>Judgement</h4><div class="verbs"><button class="pri">Engage with…<span class="k">E</span></button><button>Shadow with…<span class="k">W</span></button><button>Mark: assumed hostile<span class="k">M</span></button><button>Merge / split track<span class="k">J</span></button></div><div class="hint" style="margin-top:5px">Your mark is drawn apart from what the sensors confirm.</div></div>`;
  return [h, b];
}
function cardContact(k) {
  const h = `<span class="sym ho sus" style="margin-right:2px"></span>${esc(k.title)} <span class="dim" style="text-transform:none;letter-spacing:0">· ${k.kind === 'df' ? 'radio direction-finding' : k.kind === 'ellipse' ? 'visual, unidentified' : k.kind === 'port' ? 'in harbour' : 'report'} · ${fmtAge(k.age)} old</span><span class="rt">${k.area}</span>`;
  const e = k.est;
  const b = `<div class="cc" style="width:300px"><h4>Estimate</h4><table class="t"><tr><th></th><th>min</th><th>max</th></tr><tr><td>Capital</td><td class="n">${e.cap[0]}</td><td class="n">${e.cap[1]}</td></tr><tr><td>Cruisers</td><td class="n">${e.cru[0]}</td><td class="n">${e.cru[1]}</td></tr><tr><td>Light</td><td class="n">${e.lt[0]}</td><td class="n">${e.lt[1]}</td></tr></table>${pipsHTML(null, 'H', e)}</div>
    <div class="cc" style="width:520px"><h4>Report trail <span class="rt">newest first</span></h4><div class="hint">${k.reports.map(r => '· ' + esc(r)).join('<br>')}</div><div class="hint" style="margin-top:6px">${k.kind === 'df' ? 'Two shore bearings cross: a fix, but only of the transmitter. What it is comes from the callsign.' : k.kind === 'ellipse' ? 'A lookout knows the bearing well and the range poorly: the long axis lies along the line of sight.' : 'Ages fade the symbol; nothing here is confirmed until seen again.'}</div></div>
    <div class="cc" style="flex:1"><h4>Act on it</h4><div class="verbs"><button class="pri">Investigate with…<span class="k">I</span></button><button>Mark: assumed HSF<span class="k">M</span></button><button>Air recce (Engadine)<span class="k">A</span></button><button>Ask Admiralty<span class="k">F6</span></button></div></div>`;
  return [h, b];
}
function cardFleet(name, us) {
  const cp = comp(us.flatMap(u => u.ships));
  const h = `<span class="sym" style="margin-right:2px"></span>${esc(name || us.length + ' groups')} <span class="dim" style="text-transform:none;letter-spacing:0">· ${us.length} groups · ${us.reduce((a, u) => a + u.ships.length, 0)} ships · ${Math.round(cp.t)} kt</span><span class="rt">${cp.BB} BB · ${cp.BC} BC · ${cp.PB} old BB · ${cp.CA + cp.CL} cruisers · ${cp.lt} light</span>`;
  const rows = us.map(u => `<tr class="hov" data-unit="${u.id}"><td><b>${esc(u.short)}</b></td><td>${pipsHTML(u.comp, u.aff === 'H' ? 'H' : u.ally ? 'A' : 'F')}</td><td>${statusChip(u)}</td><td class="m">${atSea(u) ? fmtBrg(u.hdg) + ' ' + u.kn + ' kn' : esc(u.port || '')}</td><td class="dim" style="white-space:normal">${esc(u.task || '')}</td></tr>`).join('');
  const b = `<div class="cc" style="width:860px;overflow:auto"><table class="t"><tr><th>Group</th><th>Strength</th><th>Status</th><th>Course / port</th><th>Task</th></tr>${rows}</table></div>
    <div class="cc" style="flex:1"><h4>Fleet</h4><div class="verbs"><button class="pri">Zoom to fit<span class="k">dbl</span></button><button>Fleet doctrine…<span class="k">C</span></button><button>Concentrate at…<span class="k">RMB</span></button><button>Order of battle<span class="k">F2</span></button></div><div class="hint" style="margin-top:5px">At this zoom a click picks a fleet; zoom in to pick its groups, closer still for ships.</div></div>`;
  return [h, b];
}
function cardPort(p) {
  const us = units.filter(u => u.port === p.n);
  const h = `<span class="sym ${p.side === 'RN' ? '' : SIDES[p.side].aff === 'H' ? 'ho sus' : 'al'}" style="margin-right:2px"></span>${esc(p.n)} <span class="dim" style="text-transform:none;letter-spacing:0">· ${esc(SIDES[p.side].name)}${p.docks ? ` · ${p.docks} dry docks` : ''}${p.yard ? ' · shipyard' : ''}</span><span class="rt">${fmtLL(p.at[0], p.at[1])}</span>`;
  const intel = contacts.find(k => k.kind === 'port' && k.title.startsWith(p.n.split(' ')[0]));
  if (intel) return [h, cardContact(intel)[1]];
  const rows = us.map(u => u.ships.map(sh => `<tr class="hov" data-ship="${esc(sh.name)}"><td>${esc(sh.name)}</td><td class="dim">${esc(sh.T.n)}</td><td>${statusChip(u)}</td><td class="dim">${esc(u.task)}</td></tr>`).join('')).join('');
  const b = `<div class="cc" style="width:700px;overflow:hidden"><h4>In port <span class="rt">${us.reduce((a, u) => a + u.ships.length, 0)} ships</span></h4><table class="t"><tr><th>Ship</th><th>Type</th><th>State</th><th>Work / task</th></tr>${rows || '<tr><td class="dim">none</td></tr>'}</table></div>
    <div class="cc" style="width:260px"><h4>Stocks</h4><div class="kv"><span class="k">Coal</span><span class="v">${Math.round(40 + hash(p.n) * 160)} kt</span><span class="k">Fuel oil</span><span class="v">${Math.round(8 + hash(p.n + 'o') * 60)} kt</span><span class="k">Shell 12–15 in</span><span class="v">${Math.round(600 + hash(p.n + 's') * 2400)} rds</span><span class="k">Dry docks</span><span class="v">${p.docks ? `${us.filter(u => u.status === 'DOCK').length}/${p.docks} in use` : '—'}</span><span class="k">Repair</span><span class="v">${p.big ? 'major' : 'minor'} · 1,800 men</span></div></div>
    <div class="cc" style="flex:1"><h4>Port</h4><div class="verbs"><button class="pri">Yard schedule<span class="k">F4</span></button><button>Sail all ready…<span class="k">S</span></button><button>Stores & coaling<span class="k">F7</span></button><button>Zoom to berths<span class="k">dbl</span></button></div></div>`;
  return [h, b];
}
function cardEnc(e) {
  const us = units.filter(u => u.enc === e.id), own = us.filter(u => u.own), en = us.filter(knownEnemy);
  const h = `<span class="st war" style="margin-right:6px">ACTION</span>${esc(e.name)} <span class="dim" style="text-transform:none;letter-spacing:0">· ${esc(e.phase)} · since ${e.start}</span><span class="rt">tactical tier · held at ${e.rate}× · ${fmtLL(e.lat, e.lon)}</span>`;
  const b = `<div class="cc" style="width:420px"><h4>Sides</h4><table class="t"><tr><th></th><th>Groups</th><th>Strength</th></tr><tr><td class="fr">Own</td><td>${own.map(u => esc(u.short)).join(', ')}</td><td>${pipsHTML(comp(own.flatMap(u => u.ships)))}</td></tr><tr><td class="ho">Enemy</td><td>${en.map(u => esc(u.intel.cert === 'id' ? u.short : u.intel.est)).join(', ') || '—'}</td><td>${pipsHTML(comp(en.flatMap(u => u.ships)), 'H')}</td></tr></table><div class="hint" style="margin-top:4px">Losses: ${esc(e.losses)}</div></div>
    <div class="cc" style="width:380px"><h4>Auto-pause rules <span class="rt">this action</span></h4><div class="hint">✔ new contact identified · ✔ ship below 50% flotation · ✔ torpedoes in the water near capital ships · ✘ every hit · ✔ order completed</div><div class="hint" style="margin-top:6px">World speed is held at 12× while any action is open; groups on rails elsewhere move at the same clock.</div></div>
    <div class="cc" style="flex:1"><h4>Action</h4><div class="verbs"><button class="pri">Go to (tactical)<span class="k">dbl</span></button><button>Pause on next event<span class="k">P</span></button><button>Replay so far<span class="k">R</span></button><button>Break off…<span class="k">X</span></button></div></div>`;
  return [h, b];
}
function cardArea(r) {
  const { a, own, al, en } = r;
  const h = `Sea area · ${esc(a.label || a.n)} <span class="rt">own and allied counts are exact; enemy is intelligence</span>`;
  const b = `<div class="cc" style="width:520px"><table class="t"><tr><th></th><th>Own</th><th>Allied</th><th>Enemy (est.)</th></tr><tr><td>Capital</td><td class="n fr">${own.cap}</td><td class="n">${al.cap}</td><td class="n ho">${en.cap[0]}–${en.cap[1]}</td></tr><tr><td>Cruisers</td><td class="n fr">${own.cru}</td><td class="n">${al.cru}</td><td class="n ho">${en.cru[0]}–${en.cru[1]}</td></tr><tr><td>Light</td><td class="n fr">${own.lt}</td><td class="n">${al.lt}</td><td class="n ho">${en.lt[0]}–${en.lt[1]}</td></tr></table></div><div class="cc" style="flex:1"><div class="hint">Zoom in to see where those ships are; nothing here is abstracted: each count is ships at real positions inside the area's box.</div></div>`;
  return [h, b];
}
$('selDetail').addEventListener('click', e => {
  const r = e.target.closest('tr[data-ship]'); if (r) { const sh = shipsByName[r.dataset.ship]; select('ship', sh); if (!onScreen(sh.u)) { const q = shipLL(sh); flyTo(q[0], q[1], Math.min(mpp(), 20)); } return; }
  const r2 = e.target.closest('tr[data-unit]'); if (r2) { const u = unitById[r2.dataset.unit]; select('unit', u); flyTo(u.lat, u.lon, atSea(u) ? Math.min(mpp(), 400) : 3); return; }
  const b = e.target.closest('button'); if (!b) return; const t = b.textContent;
  if (/F3|design/i.test(t)) openScreen('designs'); else if (/F4|Yard/.test(t)) openScreen('yards'); else if (/F2|register|Order of battle/.test(t)) openScreen('fleet');
  else if (/Go to|Zoom/.test(t) && sel) { const r = sel.ref; const la = r.lat ?? r.at?.[0] ?? r.items?.[0].u.lat ?? r.units?.[0].lat, lo = r.lon ?? r.at?.[1] ?? r.items?.[0].u.lon ?? r.units?.[0].lon; flyTo(la, lo, sel.k === 'port' ? 3 : 24); }
  else toast(`<b>${esc(t.replace(/[A-Z0-9]+$/, '').trim())}</b> · (mockup)`);
});

function tipHTML(h) {
  const r = h.ref;
  if (h.k === 'ship') { const u = r.u; if (isEnemy(u)) return `<div class="tt ho">${u.intel.cert === 'id' ? esc(r.name) + ' · ' + esc(r.cls || '') : esc(u.intel.est)}</div><div class="kv"><span class="k">Certainty</span><span class="v">${u.intel.cert === 'id' ? 'identified (solid)' : 'suspect (dashed)'}</span><span class="k">Course</span><span class="v">${fmtBrg(u.hdg)} · ~${u.kn} kn</span></div><div class="src">${esc(u.intel.src)} · ${esc(u.intel.by)} · ${u.intel.age} min</div>`;
    return `<div class="tt">${esc(r.name)} <span class="dim">· ${esc(r.cls || r.T.n)}</span></div><div class="kv"><span class="k">Group</span><span class="v">${esc(u.short)} (${esc(u.fleet)})</span><span class="k">Flotation</span><span class="v">${Math.round(r.flot * 100)}%${r.flood ? ' · ' + r.flood + ' t water' : ''}</span>${r.tgt ? `<span class="k">Firing at</span><span class="v">${esc(r.tgt.name)}</span>` : ''}${atSea(u) ? `<span class="k">Course</span><span class="v">${fmtBrg(u.hdg)} · ${u.kn} kn</span>` : `<span class="k">State</span><span class="v">${esc(u.task)}</span>`}</div>`; }
  if (h.k === 'unit') { const u = r; if (isEnemy(u)) return `<div class="tt ho">${esc(u.intel.cert === 'id' ? u.name : u.intel.est)}</div><div class="src">${esc(u.intel.src)} · ${esc(u.intel.by)} · ${u.intel.age} min</div>`;
    return `<div class="tt">${esc(u.name)}</div><div class="kv"><span class="k">Fleet</span><span class="v">${esc(u.fleet)}</span><span class="k">Ships</span><span class="v">${u.ships.length} · ${Math.round(u.comp.t)} kt</span>${atSea(u) ? `<span class="k">Course</span><span class="v">${fmtBrg(u.hdg)} · ${u.kn} kn</span>` : ''}<span class="k">Task</span><span class="v">${esc(u.task || '—')}</span></div>${u.sent ? '<div class="src amb">◷ order in transit: sent ' + u.sent.hdg + '°, executing ' + Math.round(u.hdg) + '°</div>' : ''}`; }
  if (h.k === 'cluster') { const us = r.items.map(i => i.u); return `<div class="tt">${us.length} groups here</div><div class="hint">${us.map(u => esc(u.short)).join(' · ')}</div><div class="src">click: list · double-click: zoom in to split</div>`; }
  if (h.k === 'contact') return `<div class="tt ho">${esc(r.title)}</div><div class="src">${r.reports.map(esc).join('<br>')}</div>`;
  if (h.k === 'port') { const us = units.filter(u => u.port === r.n); return `<div class="tt">${esc(r.n)}</div><div class="hint">${us.length ? us.map(u => `${esc(u.short)} · ${u.ships.length} (${u.status.toLowerCase()})`).join('<br>') : 'no ships of yours'}</div>`; }
  if (h.k === 'enc') return `<div class="tt amb">Action · ${esc(r.name)}</div><div class="hint">${esc(r.phase)}<br>${esc(r.own)}<br>${esc(r.foe)}</div><div class="src">double-click to go in</div>`;
  if (h.k === 'pip' && r.k) return `<div class="tt ho">${esc(r.k.title)} · ${fmtKm(r.d)} off screen</div><div class="src">${r.k.reports.map(esc).join('<br>')}</div>`;
  if (h.k === 'pip') { const us = r.us; return `<div class="tt">${esc(us.length > 1 ? us[0].fleet : us[0].name)} · ${fmtKm(r.d)} off screen</div><div class="hint">${us.map(u => `${esc(u.short)}: ${u.ships.length} ships, ${fmtBrg(u.hdg)} ${u.kn} kn`).join('<br>')}</div><div class="src">click: go · ${us[0].own ? 'right-drag: order it to a point in this view' : 'known enemy beyond the view'}</div>`; }
  if (h.k === 'area') return `<div class="tt">${esc(r.a.label || r.a.n)}</div><div class="hint">own ${r.own.cap} capital (+${r.al.cap} allied) · enemy ${r.en.cap[0]}–${r.en.cap[1]} est.</div>`;
  if (h.k === 'wreck') return `<div class="tt amb">${esc(r.name)}</div><div class="hint">${esc(r.note)}</div>`;
  if (h.k === 'wp') return `<div class="tt">Waypoint ${h.ref.i + 1}</div><div class="hint">drag to move · right-click: delete / insert / speed</div>`;
  return '';
}
