/* dc-lite: a minimal stand-in for the Design canvas runtime, so the mockup boards open as plain HTML.
   Supports what the boards use: {{path}} holes, <sc-for list as>, <sc-if value>, <helmet>, on* handlers,
   and a DCLogic class with props/state/setState. Re-renders are patched into the live DOM (a small morph),
   so elements persist between states and CSS transitions run. */
(function () {
  class DCLogic {
    constructor(props) { this.props = props || {}; this.state = {}; }
    setState(patch) { Object.assign(this.state, typeof patch === 'function' ? patch(this.state) : patch); this._render && this._render(); }
    forceUpdate() { this._render && this._render(); }
  }
  window.DCLogic = DCLogic;

  const get = (scope, path) => {
    path = path.trim();
    if (path === 'true') return true;
    if (path === 'false') return false;
    if (/^-?\d+(\.\d+)?$/.test(path)) return Number(path);
    if (/^'.*'$|^".*"$/.test(path)) return path.slice(1, -1);
    return path.split('.').reduce((o, k) => (o == null ? undefined : o[k]), scope);
  };
  const HOLE = /\{\{\s*([^}]+?)\s*\}\}/g;
  const whole = (s) => { const m = /^\s*\{\{\s*([^}]+?)\s*\}\}\s*$/.exec(s); return m ? m[1] : null; };
  const interp = (s, scope) => s.replace(HOLE, (_, p) => { const v = get(scope, p); return v == null ? '' : String(v); });

  function bind(el, type) {
    el.__bound = el.__bound || {};
    if (el.__bound[type]) return;
    el.__bound[type] = true;
    el.addEventListener(type, (e) => { const fn = el.__h && el.__h[type]; if (fn) { e.preventDefault(); e.stopPropagation(); fn(e); } });
  }

  function renderNodes(src, scope, out) {
    for (const n of Array.from(src.childNodes)) {
      if (n.nodeType === 3) { out.appendChild(document.createTextNode(interp(n.nodeValue, scope))); continue; }
      if (n.nodeType !== 1) continue;
      const tag = n.localName;
      if (tag === 'helmet') continue;
      if (tag === 'sc-for') {
        const list = get(scope, whole(n.getAttribute('list')) || '') || [];
        const as = n.getAttribute('as') || 'item';
        list.forEach((item, i) => renderNodes(n, Object.assign(Object.create(scope), { [as]: item, $index: i }), out));
        continue;
      }
      if (tag === 'sc-if') {
        if (get(scope, whole(n.getAttribute('value')) || '')) renderNodes(n, scope, out);
        continue;
      }
      const el = n.namespaceURI === 'http://www.w3.org/2000/svg'
        ? document.createElementNS(n.namespaceURI, n.localName) : document.createElement(n.localName);
      el.__h = {};
      for (const a of Array.from(n.attributes)) {
        if (a.name.startsWith('hint-')) continue;
        const w = whole(a.value);
        if (a.name.startsWith('on') && w) {
          const fn = get(scope, w);
          if (typeof fn === 'function') el.__h[a.name.slice(2)] = fn;
          continue;
        }
        el.setAttribute(a.name, interp(a.value, scope));
      }
      renderNodes(n, scope, el);
      out.appendChild(el);
    }
  }

  // patch `live` to look like `next`, keeping nodes where tag and position match
  function morph(live, next) {
    const a = Array.from(live.childNodes), b = Array.from(next.childNodes);
    for (let i = 0; i < b.length; i++) {
      const o = a[i], n = b[i];
      if (!o) { adopt(n); live.appendChild(n); continue; }
      if (o.nodeType !== n.nodeType || (o.nodeType === 1 && (o.localName !== n.localName || o.namespaceURI !== n.namespaceURI))) {
        adopt(n); live.replaceChild(n, o); continue;
      }
      if (o.nodeType === 3) { if (o.nodeValue !== n.nodeValue) o.nodeValue = n.nodeValue; continue; }
      for (const at of Array.from(o.attributes)) if (!n.hasAttribute(at.name)) o.removeAttribute(at.name);
      for (const at of Array.from(n.attributes)) if (o.getAttribute(at.name) !== at.value) o.setAttribute(at.name, at.value);
      o.__h = n.__h || {};
      for (const t in o.__h) bind(o, t);
      morph(o, n);
    }
    for (let i = a.length - 1; i >= b.length; i--) live.removeChild(a[i]);
  }
  function adopt(n) {
    if (n.nodeType !== 1) return;
    for (const t in (n.__h || {})) bind(n, t);
    for (const c of Array.from(n.childNodes)) adopt(c);
  }

  window.addEventListener('DOMContentLoaded', () => {
    const tpl = document.querySelector('x-dc');
    const helmet = tpl.querySelector('helmet');
    if (helmet) for (const c of Array.from(helmet.childNodes)) document.head.appendChild(c.cloneNode(true));
    const code = document.querySelector('script[data-dc-script]').textContent;
    const Component = new Function('DCLogic', code + '\n;return Component;')(DCLogic);
    const inst = new Component({});
    const mount = document.createElement('div');
    tpl.replaceWith(mount);
    inst._render = () => { const next = document.createElement('div'); renderNodes(tpl, inst.renderVals(), next); morph(mount, next); };
    inst._render();
    if (inst.componentDidMount) inst.componentDidMount();
  });
})();
