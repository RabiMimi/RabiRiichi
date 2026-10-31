// Safe DOM & SVG construction utilities for RabiRiichi Arena.
// Strictly avoids innerHTML, outerHTML, insertAdjacentHTML, document.write,
// inline styles, and browser blocking dialogs.

import { getBcp47Locale, t } from './i18n/index.js';

const SVG_NS = 'http://www.w3.org/2000/svg';

export function el(tag, opts = {}, children = []) {
  const node = document.createElement(tag);
  if (opts.className) node.className = opts.className;
  if (opts.id) node.id = opts.id;
  if (opts.text !== undefined && opts.text !== null) node.textContent = String(opts.text);
  if (opts.title) node.title = String(opts.title);
  if (opts.type) node.type = opts.type;
  if (opts.name) node.name = opts.name;
  if (opts.value !== undefined && opts.value !== null) node.value = String(opts.value);
  if (opts.placeholder !== undefined) node.placeholder = String(opts.placeholder);
  if (opts.checked !== undefined) node.checked = Boolean(opts.checked);
  if (opts.disabled !== undefined) node.disabled = Boolean(opts.disabled);
  if (opts.readOnly !== undefined) node.readOnly = Boolean(opts.readOnly);
  if (opts.min !== undefined) node.min = String(opts.min);
  if (opts.max !== undefined) node.max = String(opts.max);
  if (opts.step !== undefined) node.step = String(opts.step);
  if (opts.rows !== undefined) node.rows = Number(opts.rows);
  if (opts.htmlFor) node.htmlFor = opts.htmlFor;
  if (opts.href) node.href = opts.href;
  if (opts.target) node.target = opts.target;
  if (opts.rel) node.rel = opts.rel;
  if (opts.download !== undefined) node.download = String(opts.download);
  if (opts.attrs) {
    for (const [k, v] of Object.entries(opts.attrs)) {
      if (v !== undefined && v !== null && k.toLowerCase() !== 'style') {
        node.setAttribute(k, String(v));
      }
    }
  }
  if (opts.dataset) {
    for (const [k, v] of Object.entries(opts.dataset)) {
      if (v !== undefined && v !== null) node.dataset[k] = String(v);
    }
  }
  if (opts.on) {
    for (const [evt, fn] of Object.entries(opts.on)) {
      if (typeof fn === 'function') node.addEventListener(evt, fn);
    }
  }
  appendChildren(node, children);
  return node;
}

export function svgEl(tag, attrs = {}, children = []) {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v !== undefined && v !== null && k.toLowerCase() !== 'style') {
      node.setAttribute(k, String(v));
    }
  }
  appendChildren(node, children);
  return node;
}

export function appendChildren(parent, children) {
  if (!children) return parent;
  const list = Array.isArray(children) ? children : [children];
  for (const c of list) {
    if (c === null || c === undefined || c === false) continue;
    if (Array.isArray(c)) {
      appendChildren(parent, c);
    } else if (c instanceof Node) {
      parent.appendChild(c);
    } else {
      parent.appendChild(document.createTextNode(String(c)));
    }
  }
  return parent;
}

export function clear(node) {
  if (node) node.replaceChildren();
  return node;
}

export function safeExternalHref(rawUrl) {
  if (!rawUrl || typeof rawUrl !== 'string') return null;
  try {
    const parsed = new URL(rawUrl, window.location.origin);
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
      return parsed.href;
    }
  } catch {
    // Reject malformed URLs
  }
  return null;
}

export function formatInt(val) {
  if (val === null || val === undefined || Number.isNaN(Number(val))) return '0';
  return new Intl.NumberFormat(getBcp47Locale()).format(Math.round(Number(val)));
}

export function formatSigned(val, digits = 0) {
  const n = Number(val);
  if (!Number.isFinite(n)) return '0';
  const locale = getBcp47Locale();
  const abs =
    digits > 0
      ? new Intl.NumberFormat(locale, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(Math.abs(n))
      : new Intl.NumberFormat(locale).format(Math.round(Math.abs(n)));
  if (n > 0) return `+${abs}`;
  if (n < 0) return `-${abs}`;
  return digits > 0
    ? new Intl.NumberFormat(locale, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(0)
    : '0';
}

export function formatPct(rate, digits = 1) {
  const n = Number(rate);
  const locale = getBcp47Locale();
  const safeVal = Number.isFinite(n) ? n : 0;
  return new Intl.NumberFormat(locale, {
    style: 'percent',
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(safeVal);
}

export function formatCompactTokens(tokens) {
  const n = Number(tokens);
  if (!Number.isFinite(n) || n <= 0) return '0';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(2)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
  return String(Math.round(n));
}

export function formatDuration(ms) {
  const n = Number(ms);
  if (!Number.isFinite(n) || n <= 0) return '—';
  if (n < 1000) return t('durationMs', { n: Math.round(n) });
  const totalSec = Math.round(n / 1000);
  const mins = Math.floor(totalSec / 60);
  const secs = totalSec % 60;
  if (mins === 0) return t('durationSec', { s: secs });
  const hrs = Math.floor(mins / 60);
  if (hrs > 0) return t('durationHrMin', { h: hrs, m: mins % 60 });
  return t('durationMinSec', { m: mins, s: secs });
}

export function formatTimestamp(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  return new Intl.DateTimeFormat(getBcp47Locale(), {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(d);
}

export function providerBadgeClass(provider) {
  const p = String(provider || '').toLowerCase();
  if (p === 'openai') return 'badge provider-openai';
  if (p === 'gemini') return 'badge provider-gemini';
  if (p === 'anthropic') return 'badge provider-anthropic';
  if (p === 'grok' || p === 'xai') return 'badge provider-grok';
  if (p === 'baseline') return 'badge provider-baseline';
  return 'badge';
}

export function providerColorHex(provider, idx = 0) {
  const palette = [
    '#60a5fa',
    '#f472b6',
    '#34d399',
    '#fbbf24',
    '#a78bfa',
    '#fb7185',
    '#2dd4bf',
    '#f97316',
    '#38bdf8',
    '#e879f9',
  ];
  const p = String(provider || '').toLowerCase();
  if (idx === 0 && p === 'openai') return '#34d399';
  if (idx === 0 && p === 'gemini') return '#60a5fa';
  if (idx === 0 && p === 'anthropic') return '#fb923c';
  if (idx === 0 && p === 'grok') return '#e879f9';
  return palette[idx % palette.length];
}

export function statusBadgeClass(status) {
  const s = String(status || '').toLowerCase();
  if (s === 'running') return 'status-pill status-running';
  if (s === 'pausing' || s === 'paused') return 'status-pill status-paused';
  if (s === 'finished') return 'status-pill status-finished';
  if (s === 'stopped') return 'status-pill status-stopped';
  return 'status-pill status-idle';
}

const HONOR_LABEL_KEYS = {
  '1z': 'tileHonor1z',
  '2z': 'tileHonor2z',
  '3z': 'tileHonor3z',
  '4z': 'tileHonor4z',
  '5z': 'tileHonor5z',
  '6z': 'tileHonor6z',
  '7z': 'tileHonor7z',
  '*': 'tileWild',
};

const HONOR_GLYPHS = {
  '1z': '東',
  '2z': '南',
  '3z': '西',
  '4z': '北',
  '5z': '白',
  '6z': '發',
  '7z': '中',
  '*': '★',
};

export function createTileFace(code, compact = false) {
  const c = String(code || '').trim();
  let suitClass = 'tile-honor';
  let topText = c;
  let subText = '';
  if (c === '*') {
    suitClass = 'tile-wildcard';
    topText = '★ *';
    subText = t('tileWild');
  } else if (c.endsWith('m')) {
    suitClass = c.startsWith('r') ? 'tile-man tile-aka' : 'tile-man';
    topText = c.startsWith('r') ? '0m' : c;
    subText = c.startsWith('r') ? t('tileRed5Man') : t('tileManSuffix', { n: c[0] });
  } else if (c.endsWith('p')) {
    suitClass = c.startsWith('r') ? 'tile-pin tile-aka' : 'tile-pin';
    topText = c.startsWith('r') ? '0p' : c;
    subText = c.startsWith('r') ? t('tileRed5Pin') : t('tilePinSuffix', { n: c[0] });
  } else if (c.endsWith('s')) {
    suitClass = c.startsWith('r') ? 'tile-sou tile-aka' : 'tile-sou';
    topText = c.startsWith('r') ? '0s' : c;
    subText = c.startsWith('r') ? t('tileRed5Sou') : t('tileSouSuffix', { n: c[0] });
  } else if (HONOR_GLYPHS[c]) {
    if (c === '6z') suitClass = 'tile-honor tile-hatsu';
    else if (c === '7z') suitClass = 'tile-honor tile-chun';
    topText = `${HONOR_GLYPHS[c]} ${c}`;
    subText = HONOR_LABEL_KEYS[c] ? t(HONOR_LABEL_KEYS[c]) : '';
  }

  return el(
    'span',
    { className: `mahjong-tile ${suitClass}${compact ? ' mahjong-tile-sm' : ''}`, title: `${c}${subText ? ` (${subText})` : ''}` },
    compact
      ? [el('span', { className: 'tile-top', text: c === '*' ? '★*' : c })]
      : [
          el('span', { className: 'tile-top', text: topText }),
          el('span', { className: 'tile-sub', text: subText }),
        ]
  );
}

export function showToast(message, kind = 'info', durationMs = 4200) {
  const container = document.getElementById('toast-container');
  if (!container) return;
  const item = el('div', { className: `toast toast-${kind}`, attrs: { role: 'status' } }, [
    el('span', { className: 'toast-msg', text: message }),
    el('button', {
      type: 'button',
      className: 'toast-close',
      text: '×',
      attrs: { 'aria-label': t('dismissToastAria') },
      on: { click: () => item.remove() },
    }),
  ]);
  container.appendChild(item);
  window.setTimeout(() => {
    if (item.isConnected) item.remove();
  }, durationMs);
}

export function openDialogModal(dialogId) {
  const dlg = document.getElementById(dialogId);
  if (!dlg) return;
  if (typeof dlg.showModal === 'function') {
    if (!dlg.open) dlg.showModal();
  } else {
    dlg.setAttribute('open', 'open');
  }
}

export function closeDialogModal(dialogId) {
  const dlg = document.getElementById(dialogId);
  if (!dlg) return;
  if (typeof dlg.close === 'function' && dlg.open) {
    dlg.close();
  } else {
    dlg.removeAttribute('open');
  }
}

export function confirmAction({ title, message, confirmLabel = null, danger = false }) {
  return new Promise((resolve) => {
    const dlg = document.getElementById('confirm-dialog');
    const titleNode = document.getElementById('confirm-dialog-title');
    const msgNode = document.getElementById('confirm-dialog-message');
    const okBtn = document.getElementById('confirm-dialog-ok');
    const cancelBtn = document.getElementById('confirm-dialog-cancel');
    if (!dlg || !titleNode || !msgNode || !okBtn || !cancelBtn) {
      resolve(false);
      return;
    }

    titleNode.textContent = title || t('confirmDefaultTitle');
    msgNode.textContent = message || '';
    okBtn.textContent = confirmLabel || t('confirmBtn');
    cancelBtn.textContent = t('cancelBtn');
    okBtn.className = danger ? 'btn btn-danger' : 'btn btn-primary';

    let settled = false;
    const finish = (val) => {
      if (settled) return;
      settled = true;
      okBtn.removeEventListener('click', onOk);
      cancelBtn.removeEventListener('click', onCancel);
      dlg.removeEventListener('close', onClose);
      closeDialogModal('confirm-dialog');
      resolve(val);
    };
    const onOk = () => finish(true);
    const onCancel = () => finish(false);
    const onClose = () => finish(false);

    okBtn.addEventListener('click', onOk);
    cancelBtn.addEventListener('click', onCancel);
    dlg.addEventListener('close', onClose);
    openDialogModal('confirm-dialog');
  });
}
