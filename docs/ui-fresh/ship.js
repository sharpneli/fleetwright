// Ink drawings of ships from a short spec, for the mockups only. Metres; stern at x = 0, bow to the right.
// profile(spec, opts) and plan(spec, opts) return an <svg> string that fills its box.
(function () {
  const SPECS = {
    bismarck: { L: 234, B: 32, fb: 7.5, d: 10.4,
      turrets: [{ x: 38, s: 0, id: 'Y' }, { x: 53, s: 1, id: 'X' }, { x: 176, s: 1, id: 'B' }, { x: 191, s: 0, id: 'A' }],
      tr: 6.2, gun: 17, funnels: [{ x: 121, w: 13, h: 16 }],
      blocks: [{ a: 66, b: 166, h: 5 }, { a: 78, b: 104, h: 9 }, { a: 128, b: 160, h: 10 }, { a: 146, b: 158, h: 21 }, { a: 84, b: 93, h: 14 }],
      masts: [{ x: 152, h: 40 }, { x: 100, h: 34 }],
      sec: [82, 104, 126, 148], secTwin: true,
      belt: { a: 34, b: 196, top: 2.6, bot: 2.2, t: 320, c: 4 }, upper: { a: 34, b: 196, t: 145, c: 2 },
      ends: [{ a: 4, b: 34, t: 80, c: 1 }, { a: 196, b: 228, t: 60, c: 1 }], deck: { t: 80 } },
    borodino: { L: 121, B: 23.2, fb: 6.8, d: 8.2, tumble: true,
      turrets: [{ x: 22, s: 0, id: 'Aft' }, { x: 96, s: 0, id: 'Fore' }], tr: 4.6, gun: 10,
      funnels: [{ x: 52, w: 5.5, h: 15 }, { x: 66, w: 5.5, h: 15 }],
      blocks: [{ a: 32, b: 88, h: 3.2 }, { a: 80, b: 89, h: 8 }, { a: 30, b: 37, h: 6 }],
      masts: [{ x: 84, h: 30 }, { x: 34, h: 30 }], sec: [36, 59, 82], secTwin: true,
      belt: { a: 4, b: 117, top: .6, bot: 1.5, t: 194, c: 3 }, upper: { a: 18, b: 100, t: 152, c: 2 }, ends: [], deck: { t: 51 } },
    dreadnought: { L: 160, B: 25, fb: 6.5, d: 8.1,
      turrets: [{ x: 16, s: 0, id: 'Y' }, { x: 46, s: 0, id: 'X' }, { x: 132, s: 0, id: 'A' }],
      wings: [{ x: 96, id: 'P' }, { x: 96, id: 'Q' }], tr: 5.2, gun: 13,
      funnels: [{ x: 108, w: 7, h: 15 }, { x: 68, w: 5, h: 15 }],
      blocks: [{ a: 60, b: 122, h: 3.4 }, { a: 110, b: 121, h: 9 }, { a: 62, b: 72, h: 5 }],
      masts: [{ x: 100, h: 34 }, { x: 60, h: 24 }], sec: [],
      belt: { a: 10, b: 140, top: .7, bot: 1.6, t: 279, c: 4 }, upper: { a: 30, b: 136, t: 203, c: 3 },
      ends: [{ a: 140, b: 158, t: 152, c: 2 }, { a: 2, b: 10, t: 102, c: 1 }], deck: { t: 76 } },
  };
  const A = ['var(--a1)', 'var(--a2)', 'var(--a3)', 'var(--a4)', 'var(--a5)'];
  const r = v => Math.round(v * 100) / 100;

  function profile(key, o = {}) {
    const s = SPECS[key], L = s.L, pad = o.pad ?? 8, top = o.top ?? 44, ink = 'var(--ink)';
    const sw = o.sw ?? .32; let g = '';
    const deckY = x => -s.fb - Math.max(0, (x - L * .62) / (L * .38)) ** 2 * 2.2 - Math.max(0, (L * .12 - x) / (L * .12)) ** 2 * .4;
    // underwater body, then the hull above water
    const hull = `M0,${r(deckY(0))} L${L},${r(deckY(L))} C${L - 1},${-2} ${L - 3},${s.d * .5} ${L - 9},${s.d} L${L * .09},${s.d} C${L * .035},${s.d} ${L * .03},${1} ${L*.012},${-.5} L${-1},${-2.5} Z`;
    g += `<path d="${hull}" fill="var(--paper)" stroke="${ink}" stroke-width="${sw}"/>`;
    if (o.frames !== false) for (let x = L * .1; x < L * .95; x += L / 16) g += `<line x1="${r(x)}" x2="${r(x)}" y1="${r(deckY(x))}" y2="${s.d}" stroke="${ink}" stroke-width=".1" opacity=".35"/>`;
    if (o.armour !== false) {
      const b = s.belt;
      for (const e of s.ends) g += `<rect x="${e.a}" y="${-b.top}" width="${e.b - e.a}" height="${b.top + b.bot - .4}" fill="${A[e.c]}" stroke="${ink}" stroke-width=".15"/>`;
      if (s.upper) g += `<rect x="${s.upper.a}" y="${r(-s.fb + .9)}" width="${s.upper.b - s.upper.a}" height="${r(s.fb - .9 - b.top)}" fill="${A[s.upper.c]}" stroke="${ink}" stroke-width=".15"/>`;
      g += `<rect x="${b.a}" y="${-b.top}" width="${b.b - b.a}" height="${b.top + b.bot}" fill="${A[b.c]}" stroke="${ink}" stroke-width=".2"/>`;
    }
    // upperworks
    for (const k of s.blocks) g += `<rect x="${k.a}" y="${r(-s.fb - k.h)}" width="${k.b - k.a}" height="${k.h}" fill="var(--paper)" stroke="${ink}" stroke-width="${sw * .7}"/>`;
    for (const f of s.funnels) g += `<path d="M${f.x - f.w / 2},${-s.fb - s.blocks[0].h} L${f.x - f.w / 2 + .5},${-s.fb - f.h - 3} h${f.w - 1} L${f.x + f.w / 2},${-s.fb - s.blocks[0].h}Z" fill="var(--paper2)" stroke="${ink}" stroke-width="${sw * .7}"/>`;
    for (const m of s.masts) g += `<line x1="${m.x}" x2="${m.x}" y1="${-s.fb}" y2="${-s.fb - m.h}" stroke="${ink}" stroke-width=".35"/><line x1="${m.x - 3}" x2="${m.x + 3}" y1="${-s.fb - m.h * .8}" y2="${-s.fb - m.h * .8}" stroke="${ink}" stroke-width=".25"/>`;
    for (const x of s.sec) g += `<rect x="${x - 2.4}" y="${-s.fb - (s.blocks[0].h) - 2.2}" width="4.8" height="2.2" fill="var(--paper)" stroke="${ink}" stroke-width=".2"/><line x1="${x}" x2="${x + 6}" y1="${-s.fb - s.blocks[0].h - 1.3}" y2="${-s.fb - s.blocks[0].h - 1.6}" stroke="${ink}" stroke-width=".25"/>`;
    const tur = (t, wing) => {
      const fwd = t.x > L / 2 ? 1 : -1, y0 = deckY(t.x) - (t.s ? 3.4 : 0), w = s.tr * 2, h = 3;
      let q = '';
      if (o.armour !== false) q += `<rect x="${t.x - s.tr * .8}" y="${r(y0)}" width="${s.tr * 1.6}" height="${r(-y0 - s.fb + .9)}" fill="${A[Math.max(1, s.belt.c - 1)]}" stroke="${ink}" stroke-width=".15"/>`;
      else if (t.s) q += `<rect x="${t.x - s.tr * .8}" y="${r(y0)}" width="${s.tr * 1.6}" height="3.4" fill="var(--paper)" stroke="${ink}" stroke-width=".2"/>`;
      q += `<line x1="${t.x + fwd * s.tr * .7}" x2="${t.x + fwd * (s.tr + s.gun)}" y1="${r(y0 - h * .55)}" y2="${r(y0 - h * .55 - .8)}" stroke="${ink}" stroke-width=".55"/>`;
      q += `<path d="M${t.x - w / 2},${r(y0)} l${fwd > 0 ? 0 : .9},${-h} h${w - .9} l${fwd > 0 ? .9 : 0},${h}Z" fill="${o.armour !== false ? A[4] : 'var(--paper2)'}" stroke="${ink}" stroke-width="${sw * .7}" ${wing ? 'opacity=".9"' : ''}/>`;
      return q;
    };
    for (const t of s.turrets) g += tur(t);
    if (s.wings) g += tur({ x: s.wings[0].x, s: 0 }, true);
    // waterline, drawn right across the sheet
    g += `<line x1="${-pad}" x2="${L + pad}" y1="0" y2="0" stroke="var(--water)" stroke-width=".3"/>`;
    return `<svg viewBox="${-pad} ${-top} ${L + pad * 2} ${top + s.d + 3}" preserveAspectRatio="xMidYMid meet" width="100%" height="100%">${g}${o.extra ? o.extra(s, deckY) : ''}</svg>`;
  }

  function plan(key, o = {}) {
    const s = SPECS[key], L = s.L, hb = s.B / 2, pad = o.pad ?? 8, ink = 'var(--ink)', sw = o.sw ?? .32;
    let g = '';
    if (o.arcs) for (const t of s.turrets) {
      const fwd = t.x > L / 2 ? 1 : -1, R = o.arcR ?? 60, a = 145 * Math.PI / 180;
      const x1 = t.x + fwd * Math.cos(a) * R, y1 = Math.sin(a) * R;
      g += `<path d="M${t.x},0 L${r(x1)},${r(-y1)} A${R},${R} 0 1 ${fwd > 0 ? 1 : 0} ${r(x1)},${r(y1)}Z" fill="var(--ink)" fill-opacity=".03" stroke="${ink}" stroke-width=".15" stroke-dasharray="1.2 1.2"/>`;
    }
    const side = k => `C${L * .86},${k * hb * .55} ${L * .7},${k * hb} ${L * .5},${k * hb} C${L * .25},${k * hb} ${L * .06},${k * hb * .92} `;
    g += `<path d="M${L},0 ${side(-1)}0,${-hb * .45} L0,${hb * .45} C${L * .06},${hb * .92} ${L * .25},${hb} ${L * .5},${hb} C${L * .7},${hb} ${L * .86},${hb * .55} ${L},0Z" fill="var(--paper)" stroke="${ink}" stroke-width="${sw}"/>`;
    if (o.armour) { const b = s.belt; g += `<rect x="${b.a}" y="${-hb * .86}" width="${b.b - b.a}" height="${hb * 1.72}" fill="${A[1]}" opacity=".5" stroke="${ink}" stroke-width=".15" stroke-dasharray="1 1"/>`; }
    const k0 = s.blocks[0]; g += `<rect x="${k0.a}" y="${-hb * .72}" width="${k0.b - k0.a}" height="${hb * 1.44}" rx="3" fill="var(--paper2)" stroke="${ink}" stroke-width="${sw * .7}"/>`;
    for (const k of s.blocks.slice(1)) g += `<rect x="${k.a}" y="${-hb * .38}" width="${k.b - k.a}" height="${hb * .76}" fill="var(--paper)" stroke="${ink}" stroke-width=".2"/>`;
    for (const f of s.funnels) g += `<ellipse cx="${f.x}" cy="0" rx="${f.w / 2}" ry="${f.w / 3}" fill="${ink}"/>`;
    for (const x of s.sec) for (const k of [-1, 1]) g += `<circle cx="${x}" cy="${k * hb * .78}" r="2.3" fill="var(--paper)" stroke="${ink}" stroke-width=".22"/><line x1="${x}" x2="${x + (x > L / 2 ? 5 : -5)}" y1="${k * hb * .78}" y2="${k * hb * .86}" stroke="${ink}" stroke-width=".28"/>`;
    const tur = (t, y) => {
      const fwd = t.x > L / 2 ? 1 : -1; let q = `<circle cx="${t.x}" cy="${y}" r="${s.tr}" fill="var(--paper)" stroke="${ink}" stroke-width="${sw}"/>`;
      for (const k of [-1, 1]) q += `<line x1="${t.x + fwd * s.tr * .5}" x2="${t.x + fwd * (s.tr + s.gun)}" y1="${y + k * 1.4}" y2="${y + k * 1.4}" stroke="${ink}" stroke-width=".55"/>`;
      if (o.ids) q += `<text x="${t.x - fwd * 0}" y="${y + 1.5}" font-size="4" text-anchor="middle" fill="${ink}" font-weight="600">${t.id[0]}</text>`;
      return q;
    };
    for (const t of s.turrets) g += tur(t, 0);
    if (s.wings) { g += tur(s.wings[0], -hb * .62); g += tur(s.wings[1], hb * .62); }
    const vh = o.vh ?? hb + 6;
    return `<svg viewBox="${-pad} ${-vh} ${L + pad * 2} ${vh * 2}" preserveAspectRatio="xMidYMid meet" width="100%" height="100%">${g}${o.extra ? o.extra(s) : ''}</svg>`;
  }

  // a ship seen from far above, for a map: a filled sliver, bow along +x
  function mark(len = 30, fill = 'var(--friend)') {
    return `<path d="M${len / 2},0 C${len * .3},${-len * .08} ${-len * .3},${-len * .09} ${-len / 2},${-len * .05} L${-len / 2},${len * .05} C${-len * .3},${len * .09} ${len * .3},${len * .08} ${len / 2},0Z" fill="${fill}"/>`;
  }
  window.Ship = { SPECS, profile, plan, mark };

  // every mockup: N swaps the day and night skins
  addEventListener('keydown', e => { if (e.key === 'n' || e.key === 'N') toggleSkin(); });
  window.toggleSkin = () => { const d = document.documentElement; d.dataset.skin = d.dataset.skin === 'night' ? 'day' : 'night'; };
  if (location.hash === '#night') document.documentElement.dataset.skin = 'night';
})();
