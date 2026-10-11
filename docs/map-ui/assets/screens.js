// Screens pushed over the map. The map stays alive underneath; Esc or F1 pops back.
'use strict';
let curScreen = null;
const screenOpen = () => !!curScreen;
function closeScreen() { curScreen = null; $('screen').style.display = 'none'; renderTabs('map'); }
function worldStrip() {
  return `<div style="position:absolute;left:0;right:0;bottom:0;height:26px;background:#141a1f;border-top:1px solid var(--edge);display:flex;align-items:center;gap:14px;padding:0 10px;font-size:11px;color:var(--muted)">
    <span class="st war">3 ACTIONS</span><span>Horns Reef · Abrolhos · OQ-12</span><span class="amb">● ${decisions.length} decisions waiting</span>
    <span>world: ${paused ? 'paused' : 'running, held 12×'} — screens don't stop the clock; an auto-pause event brings a banner here</span>
    <span style="margin-left:auto"><span class="kbd">Esc</span> / <span class="kbd">F1</span> back to the map, exactly as you left it</span></div>`;
}
function openScreen(id) {
  curScreen = id; renderTabs(id);
  const el = $('screen'); el.style.display = 'block';
  const f = SCREEN_FN[id] || placeholder;
  el.innerHTML = f(id) + worldStrip();
  if (SCREEN_AFTER[id]) SCREEN_AFTER[id]();
}
const SCREEN_AFTER = {};
const sbar = (t, sub) => `<div class="sbar"><b>${t}</b><span>${sub}</span><span class="back"><span class="kbd">Esc</span> map</span></div>`;

// ---------------------------------------------------------------- F2 Fleet register
let regSel = 'Lion', regGroup = 'fleet';
function fleetScreen() {
  const own = units.filter(u => u.own);
  const groups = {};
  for (const u of own) for (const sh of u.ships) {
    const key = regGroup === 'fleet' ? u.fleet : regGroup === 'type' ? sh.T.n : regGroup === 'status' ? u.status : theatreOf(u.lat, u.lon);
    (groups[key] = groups[key] || []).push(sh);
  }
  const all = own.flatMap(u => u.ships);
  const cnt = comp(all);
  const where = u => atSea(u) ? (u.enc ? `<span class="amb">${encById[u.enc].name}</span>` : fmtLL(u.lat, u.lon)) : esc(u.port);
  let rows = '';
  for (const [g, list] of Object.entries(groups)) {
    const c = comp(list);
    rows += `<tr><td colspan="13" style="background:#151b20;color:var(--brass);font-weight:600;padding:3px 6px">${esc(g)} <span class="dim" style="font-weight:400">· ${list.length} ships · ${c.cap} cap · ${c.cru} cr · ${c.lt} light</span></td></tr>`;
    rows += list.map(sh => { const u = sh.u; const refit = u.status === 'DOCK' ? 'in hand' : `${['Jul', 'Sep', 'Nov', 'Jan', 'Mar'][Math.floor(hash(sh.name + 'r') * 5)]} ${hash(sh.name + 'r') > .6 ? 1917 : 1916}`;
      return `<tr class="hov ${regSel === sh.name ? 'sel' : ''}" data-reg="${esc(sh.name)}"><td style="color:var(--brass);cursor:pointer" data-go="${esc(sh.name)}" title="Show on map">⌖</td><td><b>${esc(sh.name)}</b></td><td class="dim">${esc(sh.cls || '—')}</td><td class="m">${sh.type}</td><td>${esc(u.short)}</td><td class="m">${where(u)}</td><td>${statusChip(u)}</td><td>${bar(sh.flot)}</td><td>${bar(sh.fuel, 'b')}</td><td>${bar(sh.ammo, 'b')}</td><td class="n">${sh.crew.toLocaleString('en')}</td><td class="m">${refit}</td><td class="dim">${sh.name === 'Lion' ? 'Capt Chatfield' : sh.T.k === 'cap' ? 'Capt ' + ['Chatfield', 'Brock', 'Prowse', 'Pelly', 'Green', 'Craig', 'Phillpotts', 'Woollcombe', 'Boyle', 'Ross'][Math.floor(hash(sh.name) * 10)] : sh.T.k === 'cru' ? 'Capt ' + ['Goodenough', 'Duff', 'Cochrane', 'Bonham'][Math.floor(hash(sh.name) * 4)] : 'Cdr —'}</td></tr>`; }).join('');
  }
  const s = shipsByName[regSel];
  const det = s ? `<div class="ph">${esc(s.name)}</div><div style="padding:8px"><img src="../designer-ui/assets/dreadnought.png" style="width:100%;${s.T.k === 'cap' ? '' : 'opacity:.25'}"><div class="kv" style="margin-top:6px">
    <span class="k">Class</span><span class="v">${esc(s.cls || s.T.n)}</span><span class="k">Group</span><span class="v">${esc(s.u.name)}</span><span class="k">Where</span><span class="v">${atSea(s.u) ? fmtLL(...shipLL(s)) : esc(s.u.port)}</span>
    <span class="k">Flotation</span><span class="v">${Math.round(s.flot * 100)}%</span><span class="k">Fuel · ammo</span><span class="v">${Math.round(s.fuel * 100)}% · ${Math.round(s.ammo * 100)}%</span><span class="k">Crew</span><span class="v">${s.crew.toLocaleString('en')} · efficiency 82%</span>
    <span class="k">Commissioned</span><span class="v">${1906 + Math.floor(hash(s.name) * 10)}</span><span class="k">Gunnery</span><span class="v">practice 3 wk ago · 71st percentile</span></div>
    <div class="verbs" style="margin-top:8px"><button class="pri" data-go="${esc(s.name)}">Show on map<span class="k">⌖</span></button><button onclick="openScreen('designs')">Open design<span class="k">F3</span></button><button>Order refit…<span class="k">R</span></button><button>Transfer to group…<span class="k">T</span></button><button>Reduce to reserve<span class="k">X</span></button><button>History<span class="k">H</span></button></div>
    <div class="hint" style="margin-top:8px">Every row is a ship at a real place. ⌖ pops this screen and flies the map to it with the ship selected.</div></div>` : '';
  const G = [['fleet', 'Fleet'], ['theatre', 'Theatre'], ['type', 'Type'], ['status', 'Status']];
  return sbar('FLEET REGISTER', `${all.length} ships · ${cnt.BB} BB · ${cnt.BC} BC · ${cnt.PB} old BB · ${cnt.CA + cnt.CL} cruisers · ${cnt.lt} light · ${Math.round(cnt.t)} kt`) +
    `<div class="scol" style="left:0;width:230px;border-right:1px solid var(--edge)"><div class="ph">Group by</div><div style="padding:6px;display:flex;gap:3px;flex-wrap:wrap">${G.map(([k, n]) => `<span class="chip ${regGroup === k ? 'on' : ''}" data-grp="${k}">${n}</span>`).join('')}</div>
      <div class="ph">Filter</div><div style="padding:6px;display:flex;flex-direction:column;gap:5px" class="hint">${['Battleships', 'Battlecruisers', 'Old battleships', 'Cruisers', 'Destroyers & leaders', 'Sloops', 'Auxiliaries', 'Merchant (convoy)'].map(t => `<label><input type="checkbox" checked> ${t}</label>`).join('')}
      <div style="margin-top:6px">Status: <span class="chip on">all</span> <span class="chip">at sea</span> <span class="chip">damaged</span> <span class="chip">refit due</span></div>
      <div style="margin-top:8px">Saved views: <span class="chip">Low fuel at sea</span> <span class="chip">Refit by Sep</span> <span class="chip">Fire control Mk IV</span></div></div></div>
    <div class="scol" style="left:231px;right:361px;overflow:auto"><table class="t" style="font-size:11.5px"><tr><th></th><th>Ship</th><th>Class</th><th>Type</th><th>Group</th><th>Where</th><th>Status</th><th>Flotation</th><th>Fuel</th><th>Ammo</th><th>Crew</th><th>Refit due</th><th>Captain</th></tr>${rows}</table></div>
    <div class="scol" style="right:0;width:360px;border-left:1px solid var(--edge)">${det}</div>`;
}
document.addEventListener('click', e => {
  if (!curScreen) return;
  const go = e.target.closest('[data-go]'); if (go) { const sh = shipsByName[go.dataset.go]; closeScreen(); select('ship', sh); const q = shipLL(sh); flyTo(q[0], q[1], atSea(sh.u) ? 14 : 1.2); return; }
  const r = e.target.closest('tr[data-reg]'); if (r) { regSel = r.dataset.reg; openScreen('fleet'); return; }
  const g = e.target.closest('[data-grp]'); if (g) { regGroup = g.dataset.grp; openScreen('fleet'); return; }
  const ds = e.target.closest('tr[data-des]'); if (ds) { desSel = +ds.dataset.des; openScreen('designs'); return; }
  const rs = e.target.closest('tr[data-res]'); if (rs) { resSel = +rs.dataset.res; openScreen('research'); return; }
});

// ---------------------------------------------------------------- F3 Designs
let desSel = 0;
const DESIGNS = [
  ['Admiral (1916)', 'BC', 1916, '36,300', 32, '8 × 15 in', 0, 1, 'Accepted', 'Hood laid down today'],
  ['Revenge', 'BB', 1913, '28,000', 21.5, '8 × 15 in', 2, 2, 'In service', ''],
  ['Queen Elizabeth', 'BB', 1912, '27,500', 24, '8 × 15 in', 4, 0, 'In service', ''],
  ['Renown', 'BC', 1915, '26,500', 31.5, '6 × 15 in', 0, 2, 'Building', ''],
  ['Courageous', 'LBC', 1915, '19,200', 32, '4 × 15 in', 0, 2, 'Building', ''],
  ['Iron Duke', 'BB', 1911, '25,000', 21, '10 × 13.5 in', 2, 0, 'In service', ''],
  ['Tiger', 'BC', 1911, '28,400', 28, '8 × 13.5 in', 1, 0, 'In service', ''],
  ['Lion', 'BC', 1909, '26,300', 27, '8 × 13.5 in', 2, 0, 'In service', 'magazine safety under review'],
  ['Indefatigable', 'BC', 1908, '18,500', 25, '8 × 12 in', 2, 0, 'In service', '1 lost 31 May'],
  ['Dreadnought', 'BB', 1905, '18,100', 21, '10 × 12 in', 1, 0, 'Obsolescent', ''],
  ['C-class cruiser', 'CL', 1914, '4,200', 29, '2 × 6 in, 8 × 4 in', 12, 4, 'In service', ''],
  ['M-class destroyer', 'DD', 1913, '1,000', 34, '3 × 4 in, 4 TT', 70, 14, 'In service', ''],
  ['Flower-class sloop', 'SL', 1915, '1,200', 16, '2 × 4.7 in', 30, 18, 'In service', ''],
  ['Fast BB study "G3-0"', 'BB', 1916, '41,000', 30, '9 × 16 in', 0, 0, 'Paper', 'needs 16 in gun (F5)'],
];
function designsScreen() {
  const d = DESIGNS[desSel];
  return sbar('DESIGNS', `${DESIGNS.length} designs · ${DESIGNS.filter(x => x[8] === 'Paper').length} on paper · the designer opens over this screen and returns here`) +
    `<div class="scol" style="left:0;width:640px;border-right:1px solid var(--edge);overflow:auto"><table class="t" style="font-size:11.5px"><tr><th>Class</th><th>Type</th><th>Year</th><th>Std t</th><th>kn</th><th>Main</th><th>Serv.</th><th>Bldg</th><th>State</th></tr>
    ${DESIGNS.map((x, i) => `<tr class="hov ${i === desSel ? 'sel' : ''}" data-des="${i}"><td><b>${x[0]}</b>${x[9] ? `<div class="dim" style="font-size:10px">${x[9]}</div>` : ''}</td><td class="m">${x[1]}</td><td class="m">${x[2]}</td><td class="n">${x[3]}</td><td class="n">${x[4]}</td><td class="m">${x[5]}</td><td class="n">${x[6]}</td><td class="n">${x[7]}</td><td class="m ${x[8] === 'Paper' ? 'gold' : x[8] === 'Obsolescent' ? 'dim' : ''}">${x[8]}</td></tr>`).join('')}</table>
    <div class="verbs" style="padding:8px;grid-template-columns:repeat(3,1fr)"><button class="pri">Open in designer<span class="k">Enter</span></button><button>New from this<span class="k">N</span></button><button>Compare (A/B)<span class="k">C</span></button><button>Lay down…<span class="k">F4</span></button><button>Refit design for…<span class="k">R</span></button><button>Blank design<span class="k">Ctrl+N</span></button></div></div>
    <div class="scol" style="left:641px;right:0;padding:10px"><div class="hint" style="margin-bottom:6px"><b>${d[0]}</b> · ${d[1]} · ${d[2]} · the designer as it stands in the game (docs/designer-ui), pushed on top of this screen:</div>
    <img src="../designer-ui/png/overview-game-dreadnought.png" style="width:100%;border:1px solid var(--edge);box-shadow:0 6px 24px rgba(0,0,0,.6)">
    <div class="hint" style="margin-top:8px">DesignContext from the world: year 1916, Royal Navy technology (F5), slip and dock limits from the yards (F4), treaty limits: none. On accept the design is submitted to the world, which compiles its ShipClass in the background; laying one down is a separate order on the Yards screen or from a yard on the map.</div></div>`;
}

// ---------------------------------------------------------------- F4 Yards
function yardsScreen() {
  const M0 = Date.UTC(1916, 5, 1), months = 20, pxm = 52, left = 330;
  const mx = t => left + (t - M0) / (30.44 * 864e5) * pxm;
  const D = (y, m, d = 1) => Date.UTC(y, m - 1, d);
  const Y = [
    ['John Brown, Clydebank', [['Slip 3', 'Repulse', D(1915, 1), D(1916, 1, 8), D(1916, 8, 18), 'BC'], ['Slip 4', 'Hood', D(1916, 5, 31), D(1918, 8, 22), D(1920, 1, 15), 'BC'], ['Slip 5', '— free —', 0, 0, 0], ['Basin', 'Repulse (fitting out)', D(1916, 1, 8), 0, D(1916, 8, 18), 'fit']]],
    ['Fairfield, Govan', [['Slip 1', 'Renown', D(1915, 1), D(1916, 3, 4), D(1916, 9, 20), 'BC'], ['Slip 2', '2 × M-class DD', D(1916, 2), D(1916, 9), D(1916, 12), 'DD']]],
    ['Beardmore, Dalmuir', [['Basin', 'Ramillies (fitting out)', D(1915, 9), D(1916, 9, 12), D(1917, 9, 1), 'fit'], ['Slip 2', 'Medusa (DD)', D(1916, 3), D(1916, 11), D(1917, 2), 'DD']]],
    ['Armstrong, Elswick', [['Slip A', 'Courageous', D(1915, 3), D(1916, 2, 5), D(1917, 1, 4), 'BC'], ['Slip B', 'Eagle (ex-Almirante Cochrane)', D(1913, 2), D(1918, 6), D(1924, 2), 'BB']]],
    ['Harland & Wolff, Belfast', [['Slip 2', 'Glorious', D(1915, 4), D(1916, 4, 20), D(1917, 1, 14), 'BC']]],
    ['Palmers, Jarrow', [['Basin', 'Resolution (fitting out)', D(1914, 1), D(1915, 1, 14), D(1916, 12, 30), 'fit']]],
    ['Rosyth Dockyard', [['No.1 Dock', 'Australia — collision repair', D(1916, 5, 3), 0, D(1916, 6, 11), 'rep'], ['No.2 Dock', '— free (Lion 1st in line?) —', 0, 0, 0], ['No.3 Dock', 'Blanche — boilers', D(1916, 5, 28), 0, D(1916, 6, 2), 'rep']]],
    ['Portsmouth Dockyard', [['No.15 Dock', 'Royal Oak refit (booked)', D(1916, 10, 1), 0, D(1916, 11, 20), 'rep'], ['Slip 5', 'K-class submarines ×2', D(1915, 11), D(1916, 10), D(1917, 1), 'DD']]],
  ];
  const col = { BC: '#5B9BE0', BB: '#5B9BE0', DD: '#7fa7cf', fit: '#a48fe0', rep: '#D9B868' };
  let rows = '', y = 30;
  for (const [yard, slips] of Y) {
    rows += `<div style="position:absolute;left:8px;top:${y}px;font-weight:600;color:var(--brass)">${yard}</div>`; y += 18;
    for (const [s, n, a, l, c, k] of slips) {
      rows += `<div style="position:absolute;left:18px;top:${y}px;width:300px;font-size:11.5px"><span class="mono dim">${s}</span> · ${n.startsWith('—') ? `<span class="dim">${n}</span>` : `<b style="color:var(--bright);font-weight:500">${n}</b>`}</div>`;
      if (a) {
        const x0 = Math.max(left, mx(a)), xl = l ? mx(l) : null, x1 = Math.min(left + months * pxm, mx(c));
        if (xl && xl > left) rows += `<div title="on the slip" style="position:absolute;left:${x0}px;top:${y + 2}px;width:${Math.max(0, Math.min(xl, x1) - x0)}px;height:12px;background:${col[k]};opacity:.85"></div>`;
        const fs = xl ? Math.max(left, xl) : x0;
        if (x1 > fs) rows += `<div title="${k === 'rep' ? 'in dock' : 'fitting out'}" style="position:absolute;left:${fs}px;top:${y + 2}px;width:${x1 - fs}px;height:12px;background:${k === 'rep' ? col.rep : col.fit};opacity:${k === 'rep' ? .8 : .55}"></div>`;
        if (mx(c) <= left + months * pxm) rows += `<div style="position:absolute;left:${mx(c) + 3}px;top:${y}px;font-size:10px" class="mono dim">${k === 'rep' ? 'out' : 'complete'} ${new Date(c).toUTCString().slice(8, 16)}</div>`;
        if (xl && xl > left && xl < left + months * pxm) rows += `<div style="position:absolute;left:${xl - 4}px;top:${y - 1}px;color:#fff;font-size:11px">▼</div>`;
      } else rows += `<div style="position:absolute;left:${left}px;top:${y + 2}px;width:${months * pxm}px;height:12px;border:1px dashed #3a4650"></div><div style="position:absolute;left:${left + 6}px;top:${y + 1}px;font-size:10.5px" class="gold">+ lay down / book…</div>`;
      y += 19;
    }
    y += 6;
  }
  let axis = '';
  for (let i = 0; i <= months; i++) { const d = new Date(Date.UTC(1916, 5 + i, 1)); axis += `<div style="position:absolute;left:${left + i * pxm}px;top:0;bottom:0;border-left:1px solid ${d.getUTCMonth() === 0 ? '#4a5764' : '#222a31'}"></div><div class="mono dim" style="position:absolute;left:${left + i * pxm + 3}px;top:8px;font-size:10px">${d.toUTCString().slice(8, 11)}${d.getUTCMonth() === 0 ? ' ' + d.getUTCFullYear() : ''}</div>`; }
  const today = mx(START_MS);
  return sbar('YARDS & DOCKS', '8 yards in this view · 5 capital ships building · 2 docks free · slip limits feed the designer') +
    `<div class="scol" style="left:0;right:380px;overflow:hidden"><div style="position:relative;height:1000px">${axis}<div style="position:absolute;left:${today}px;top:0;bottom:0;border-left:2px solid var(--amber)"></div><div class="mono amb" style="position:absolute;left:${today + 4}px;top:20px;font-size:10px">31 May</div>${rows}</div></div>
    <div class="scol" style="right:0;width:380px;border-left:1px solid var(--edge)"><div class="ph">John Brown · slip 4</div><div style="padding:8px"><div class="kv"><span class="k">Max length</span><span class="v">1,000 ft (305 m)</span><span class="k">Max beam</span><span class="v">104 ft (31.7 m)</span><span class="k">Cranes</span><span class="v">150 t gantry</span><span class="k">On the slip</span><span class="v">Hood · laid down today</span><span class="k">Launch</span><span class="v">Aug 1918 (est.)</span><span class="k">Labour</span><span class="v">3,100 / 3,400 · steel 92%</span></div>
    <div class="hint" style="margin-top:8px">The designer's "Limits" presets read these slips and docks: a design wider than every free dock says so in amber, never red.</div>
    <div class="verbs" style="margin-top:8px"><button class="pri">Lay down…<span class="k">L</span></button><button>Show on map<span class="k">⌖</span></button><button>Priority ▲<span class="k">+</span></button><button>Labour from…<span class="k">W</span></button></div>
    <div class="ph" style="margin:10px -8px 6px">Legend</div><div class="hint"><span style="display:inline-block;width:22px;height:9px;background:#5B9BE0"></span> on the slip · <span style="display:inline-block;width:22px;height:9px;background:#a48fe0;opacity:.6"></span> fitting out · <span style="display:inline-block;width:22px;height:9px;background:#D9B868"></span> in dock · ▼ launch</div></div></div>`;
}

// ---------------------------------------------------------------- F5 Research
let resSel = 1;
function researchScreen() {
  const y0 = 1904, y1 = 1922, L = 770, W = 700, xs = y => L + (y - y0) / (y1 - y0) * W;
  const r = RESEARCH[resSel];
  let axis = ''; for (let y = y0; y <= y1; y++) axis += `<div style="position:absolute;left:${xs(y)}px;top:0;bottom:0;border-left:1px solid ${y === 1916 ? 'transparent' : '#222a31'}"></div><div class="mono dim" style="position:absolute;left:${xs(y) + 2}px;top:4px;font-size:10px">${String(y).slice(2)}</div>`;
  const now = xs(1916.41);
  const rows = RESEARCH.map((f, i) => {
    const top = 24 + i * 44;
    const marks = f.items.map(([yr, n, s]) => `<div style="position:absolute;left:${xs(yr) - 5}px;top:${top + 6}px;width:10px;height:10px;border-radius:50%;${s === 2 ? 'background:#86C77E' : s === 1 ? 'background:conic-gradient(#D9B868 ' + Math.round(f.p * 360) + 'deg,#2a333b 0)' : 'border:1px solid #6c7882'}"></div><div style="position:absolute;left:${xs(yr) - 30}px;top:${top + 19}px;width:60px;text-align:center;font-size:9.5px;color:${s === 1 ? '#D9B868' : s === 2 ? '#9aa6af' : '#64707a'};line-height:1.05">${n}</div>`).join('');
    return `<div class="hov" data-resrow="${i}" style="position:absolute;left:0;right:0;top:${top}px;height:44px;border-bottom:1px solid #222a31;${i === resSel ? 'background:#2b2615' : ''}"></div>
      <div style="position:absolute;left:8px;top:${top + 5}px;width:140px;font-weight:600;color:var(--bright)">${f.f}</div>
      <div style="position:absolute;left:8px;top:${top + 22}px" class="mono"><span class="chip">◂</span> <span style="color:var(--bright)">${f.share}%</span> <span class="chip">▸</span></div>
      <div style="position:absolute;left:150px;top:${top + 5}px;width:330px;font-size:11.5px;color:var(--text)">${f.cur}</div>
      <div style="position:absolute;left:150px;top:${top + 24}px">${bar(f.p, 'b')} <span class="mono dim" style="font-size:10.5px">${Math.round(f.p * 100)}% · ETA ${f.eta}</span></div>
      <div style="position:absolute;left:490px;top:${top + 5}px;width:260px;font-size:10.5px;color:var(--muted);line-height:1.25">${f.aff}</div>${marks}`;
  }).join('');
  return sbar('RESEARCH', 'Admiralty & Director of Naval Construction · £1.94M a year · 9 fields · shares set the pace, projects set the order') +
    `<div class="scol" style="left:0;right:420px;overflow:hidden"><div style="position:relative;height:440px">
      <div style="position:absolute;left:8px;top:4px" class="mono dim">field · share</div><div style="position:absolute;left:150px;top:4px" class="mono dim">current project</div><div style="position:absolute;left:490px;top:4px" class="mono dim">where it shows</div>
      ${axis}<div style="position:absolute;left:${now}px;top:0;bottom:0;border-left:2px solid var(--amber)"></div>${rows}
</div>
      <div class="ph" style="margin-top:6px">Lessons from action <span class="rt">proposed by the Board after 31 May (accept to re-prioritise)</span></div>
      <div style="padding:6px 8px" class="hint">· <b>Magazine flash protection</b> — Indefatigable lost to a magazine explosion; Lion saved by flooding Q. <span class="gold">Proposed: new project in Armour, priority 1.</span><br>· <b>Shell quality</b> — 3 hits seen on Lützow without decisive effect. Supports "Green Boy" (Gunnery).<br>· <b>Night fighting</b> — searchlight control and star shell (Fire control), if the action runs into the night.</div></div>
    <div class="scol" style="right:0;width:420px;border-left:1px solid var(--edge)"><div class="ph">${r.f} · ${r.cur}</div><div style="padding:8px"><div class="kv"><span class="k">Progress</span><span class="v">${bar(r.p, 'b')} ${Math.round(r.p * 100)}%</span><span class="k">Spend</span><span class="v">£${Math.round(1940 * r.share / 100)}k a year (${r.share}%)</span><span class="k">ETA</span><span class="v">${r.eta} · ±3 months</span><span class="k">Unlocks</span><span class="v">${r.items.find(i => i[2] === 1)?.[1] || '—'}</span><span class="k">Shows in</span><span class="v" style="white-space:normal">${r.aff}</span></div>
      <div class="hint" style="margin-top:8px">What finishes here becomes a template in the designer's "Technology in this design" list with its year, and a "refit available" flag in the Fleet register for the ships it can be fitted to.</div>
      <div class="verbs" style="margin-top:8px"><button class="pri">Priority ▲<span class="k">+</span></button><button>Queue next…<span class="k">Q</span></button><button>Ships that benefit<span class="k">F2</span></button><button>Enemy progress est.<span class="k">F6</span></button></div></div></div>`;
}
SCREEN_AFTER.research = () => { document.querySelectorAll('[data-resrow]').forEach(el => el.addEventListener('click', () => { resSel = +el.dataset.resrow; openScreen('research'); })); };

// ---------------------------------------------------------------- F6 Intelligence
function intelScreen() {
  const rows = [
    ['Lützow', 'Derfflinger', 'BC', 'North Sea, in action', '16:42 today', 'visual', 'identified', 'firing; 3 hits seen'],
    ['Derfflinger', 'Derfflinger', 'BC', 'North Sea, in action', '16:42 today', 'visual', 'identified', ''], ['Seydlitz', 'Seydlitz', 'BC', 'North Sea, in action', '16:42 today', 'visual', 'identified', 'mine damage Apr (repaired?)'],
    ['Moltke', 'Moltke', 'BC', 'North Sea, in action', '16:42 today', 'visual', 'identified', ''], ['Von der Tann', 'Von der Tann', 'BC', 'North Sea, in action', '16:42 today', 'visual', 'identified', 'sank Indefatigable'],
    ['Friedrich der Grosse', 'Kaiser', 'BB', 'at sea? (DK fix)', '16:20 today', 'DF', 'probable', 'fleet flagship callsign DK'], ['König ×4', 'König', 'BB', 'at sea? (DK fix)', '14:10 today', 'Room 40', 'probable', 'III Squadron'],
    ['Kaiser ×5', 'Kaiser', 'BB', 'at sea?', '14:10 today', 'Room 40', 'possible', ''], ['Helgoland ×4, Nassau ×4', '', 'BB', 'at sea?', '14:10 today', 'Room 40', 'possible', 'I Squadron'],
    ['Deutschland ×6', 'Deutschland', 'PB', 'Jade? / at sea?', '29 May', 'agent', 'uncertain', 'II Squadron (old)'], ['Baden', 'Bayern', 'BB', 'Kiel, fitting out', '17 May', 'neutral merchant', 'uncertain', '15 in guns?'],
    ['Viribus Unitis ×4', 'Tegetthoff', 'BB', 'Pola', '29 May', 'seaplane', 'probable', 'Szent István completing'], ['Yavuz (Goeben)', 'Moltke', 'BC', 'Constantinople', '25 May', 'agent', 'probable', 'repairs to mine damage'],
    ['Möwe', 'raider', 'AMC', 'off Abrolhos, in action', '16:42 today', 'visual', 'identified', 'E2'], ['Raider?', '?', 'AMC?', 'Indian Ocean?', '22 May', "Lloyd's", 'rumour', 'Wairuna overdue'],
  ];
  return sbar('INTELLIGENCE', 'Enemy order of battle as you hold it — never the truth · Room 40 · agents · sightings') +
    `<div class="scol" style="left:0;right:420px;overflow:auto"><table class="t" style="font-size:11.5px"><tr><th>Ship(s)</th><th>Class</th><th>Type</th><th>Last held</th><th>When</th><th>Source</th><th>Confidence</th><th>Note</th></tr>
      ${rows.map(r => `<tr class="hov"><td><b>${r[0]}</b></td><td class="dim">${r[1]}</td><td class="m">${r[2]}</td><td>${r[3]}</td><td class="m">${r[4]}</td><td class="m">${r[5]}</td><td class="m ${r[6] === 'identified' ? 'ho' : r[6] === 'probable' ? 'amb' : 'dim'}">${r[6]}</td><td class="dim">${r[7]}</td></tr>`).join('')}</table></div>
    <div class="scol" style="right:0;width:420px;border-left:1px solid var(--edge)"><div class="ph">Room 40 · today</div><div style="padding:8px" class="hint">14:10 HSF believed at sea since 02:00 (callsign DK moved to Wilhelmshaven W/T — a known trick?)<br>16:20 DK fixed 55°55′N 6°40′E ±15 nm<br>16:38 Southampton's report of heavy ships to SSE fits the fix<br><br><b>On the map:</b> the Intel lens draws each of these as its shape of doubt — DF wedges, sighting ellipses, ageing port estimates — and the Contacts tab of the forces panel lists them by distance from the view.</div>
    <div class="ph">Enemy technology (estimates)</div><div style="padding:8px" class="hint">15 in guns: Baden class, ~1916–17 · director firing: unknown · stereoscopic rangefinders: yes (fast ranging seen today)</div></div>`;
}

function placeholder(id) {
  const P = {
    supply: ['SUPPLY', 'Coal, oil, shell and stores by base; colliers and oilers on the map', ['Bases: stocks, daily issue, days in hand (Scapa 31 d coal, 9 d oil)', 'Colliers and oilers are ships with positions and routes, shown on the map in the Supply lens', 'Ammunition by calibre and type: 13.5 in APC at 71% of establishment after today', 'Endurance rings for any selected group: Supply lens on the map']],
    admiralty: ['ADMIRALTY', 'Money, policy and politics', ['Navy estimates by vote (shipbuilding, personnel, works, research)', 'Board of Admiralty and the War Cabinet: pressure, requests, standing orders', 'Press and public: "Where was the Grand Fleet?" after a loss', 'Treaties and neutrals: blockade rules, US opinion']],
    officers: ['OFFICERS', 'Flag officers and captains: who commands which group', ['Beatty: aggressive · signals: poor (+20% misread chance on his flags)', 'Jellicoe: cautious · deployment: excellent', 'Evan-Thomas: follows the letter of orders (shows on the map as late turns)', 'Assign by dragging a name onto a group in the forces panel']],
    reports: ['REPORTS', 'Action reports, replays and monthly returns', ['Battle of Horns Reef (in progress): replay so far with the event log filters', 'Loss of Indefatigable: board of enquiry (draft)', 'Monthly return, May 1916: losses, gains, building', 'Every line links back to the map at that time and place']],
  }[id] || [id.toUpperCase(), '', []];
  return sbar(P[0], P[1]) + `<div class="scol" style="left:0;right:0;padding:24px 30px"><div class="hint" style="font-size:13px;line-height:1.6;max-width:900px">Not mocked yet. What lives here:<br>${P[2].map(x => '· ' + x).join('<br>')}</div></div>`;
}
const SCREEN_FN = { fleet: fleetScreen, designs: designsScreen, yards: yardsScreen, research: researchScreen, intel: intelScreen };
