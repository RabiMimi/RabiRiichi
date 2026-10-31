// Core internationalization (i18n) runtime for RabiRiichi Arena Web UI.
// Supports English (en), Simplified Chinese (zhs), Traditional Chinese (zht), and Japanese (ja).
// Strictly CSP 'self' clean: zero innerHTML, zero inline styles, zero localStorage.

import en from './en.js';
import zhs from './zhs.js';
import zht from './zht.js';
import ja from './ja.js';

export const SUPPORTED_LANGS = ['en', 'zhs', 'zht', 'ja'];

export const LANG_LABELS = {
  en: 'English',
  zhs: '简体中文',
  zht: '繁體中文',
  ja: '日本語',
};

export const BCP47_MAP = {
  en: 'en-US',
  zhs: 'zh-CN',
  zht: 'zh-TW',
  ja: 'ja-JP',
};

export const HTML_LANG_MAP = {
  en: 'en',
  zhs: 'zh-Hans',
  zht: 'zh-Hant',
  ja: 'ja',
};

const CATALOGS = { en, zhs, zht, ja };
const COOKIE_NAME = 'arena_lang';
const listeners = new Set();

export function normalizeLang(raw) {
  if (!raw || typeof raw !== 'string') return null;
  const s = raw.trim().toLowerCase();
  if (!s) return null;
  if (s === 'en' || s.startsWith('en-') || s.startsWith('en_')) return 'en';
  if (
    s === 'zht' ||
    s === 'zh-tw' ||
    s === 'zh_tw' ||
    s === 'zh-hk' ||
    s === 'zh_hk' ||
    s === 'zh-mo' ||
    s === 'zh_mo' ||
    s.includes('hant')
  ) {
    return 'zht';
  }
  if (s === 'zhs' || s === 'zh' || s.startsWith('zh-') || s.startsWith('zh_') || s.includes('hans')) {
    return 'zhs';
  }
  if (s === 'ja' || s === 'jp' || s.startsWith('ja-') || s.startsWith('ja_')) return 'ja';
  return null;
}

function readLangCookie() {
  if (typeof document === 'undefined' || !document.cookie) return null;
  const parts = document.cookie.split(';');
  for (const part of parts) {
    const eq = part.indexOf('=');
    if (eq <= 0) continue;
    const name = part.slice(0, eq).trim();
    if (name !== COOKIE_NAME) continue;
    try {
      return normalizeLang(decodeURIComponent(part.slice(eq + 1).trim()));
    } catch {
      return null;
    }
  }
  return null;
}

function writeLangCookie(lang) {
  if (typeof document === 'undefined') return;
  document.cookie = `${COOKIE_NAME}=${encodeURIComponent(lang)}; Path=/; Max-Age=31536000; SameSite=Lax`;
}

function detectInitialLang() {
  if (typeof window !== 'undefined' && window.location) {
    try {
      const params = new URLSearchParams(window.location.search);
      const fromUrl = normalizeLang(params.get('lang'));
      if (fromUrl) {
        writeLangCookie(fromUrl);
        return fromUrl;
      }
    } catch {
      // Ignore malformed query string
    }
  }

  const fromCookie = readLangCookie();
  if (fromCookie) return fromCookie;

  if (typeof navigator !== 'undefined') {
    const candidates = Array.isArray(navigator.languages) && navigator.languages.length > 0
      ? navigator.languages
      : [navigator.language];
    for (const cand of candidates) {
      const matched = normalizeLang(cand);
      if (matched) return matched;
    }
  }

  return 'en';
}

let currentLang = detectInitialLang();

export function getLang() {
  return currentLang;
}

export function getBcp47Locale() {
  return BCP47_MAP[currentLang] || 'en-US';
}

export function t(key, vars = null) {
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  let template = (cat.ui && cat.ui[key] !== undefined) ? cat.ui[key] : CATALOGS.en.ui[key];
  if (template === undefined || template === null) return key;
  let out = String(template);
  if (vars) {
    for (const [k, v] of Object.entries(vars)) {
      out = out.replaceAll(`{${k}}`, v === undefined || v === null ? '' : String(v));
    }
  }
  return out;
}

export function syncDocumentLocaleMeta() {
  if (typeof document === 'undefined') return;
  document.documentElement.lang = HTML_LANG_MAP[currentLang] || 'en';
  const isRedirect = document.body && document.body.classList.contains('redirect-body');
  document.title = t(isRedirect ? 'redirectTitle' : 'pageTitle');
}

export function applyStaticTranslations(root = document) {
  if (!root || typeof root.querySelectorAll !== 'function') return;
  syncDocumentLocaleMeta();

  root.querySelectorAll('[data-i18n]').forEach((node) => {
    const key = node.getAttribute('data-i18n');
    if (key) node.textContent = t(key);
  });
  root.querySelectorAll('[data-i18n-placeholder]').forEach((node) => {
    const key = node.getAttribute('data-i18n-placeholder');
    if (key) node.placeholder = t(key);
  });
  root.querySelectorAll('[data-i18n-title]').forEach((node) => {
    const key = node.getAttribute('data-i18n-title');
    if (key) node.title = t(key);
  });
  root.querySelectorAll('[data-i18n-aria-label]').forEach((node) => {
    const key = node.getAttribute('data-i18n-aria-label');
    if (key) node.setAttribute('aria-label', t(key));
  });
}

export function setLang(rawLang, { syncUrl = false } = {}) {
  const next = normalizeLang(rawLang) || 'en';
  const changed = next !== currentLang;
  currentLang = next;
  writeLangCookie(next);

  if (typeof window !== 'undefined' && window.history && window.location) {
    const url = new URL(window.location.href);
    if (syncUrl || url.searchParams.has('lang')) {
      url.searchParams.set('lang', next);
      window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
    }
  }

  applyStaticTranslations();
  if (changed) {
    for (const fn of listeners) {
      fn(currentLang);
    }
  }
  return currentLang;
}

export function onLangChange(fn) {
  if (typeof fn === 'function') listeners.add(fn);
  return () => listeners.delete(fn);
}

// =========================================================================
// Domain & Metadata Catalog Helpers
// =========================================================================

export function tStatus(rawStatus) {
  const s = String(rawStatus || 'Idle').trim();
  const norm = s.toLowerCase().replace(/[_\s-]+/g, '');
  const map = {
    idle: 'statusIdle',
    running: 'statusRunning',
    pausing: 'statusPausing',
    paused: 'statusPaused',
    finished: 'statusFinished',
    completed: 'statusFinished',
    stopped: 'statusStopped',
  };
  return map[norm] ? t(map[norm]) : s;
}

export function tOutcome(rawOutcome) {
  const s = String(rawOutcome || '').trim();
  const norm = s.toLowerCase().replace(/[_\s-]+/g, '');
  const map = {
    tsumo: 'outcomeTsumo',
    ron: 'outcomeRon',
    exhaustivedraw: 'outcomeExhaustiveDraw',
    abortivedraw: 'outcomeAbortiveDraw',
  };
  return map[norm] ? t(map[norm]) : s;
}

export function tRoundLabel(rawLabel) {
  if (!rawLabel) return t('inProgress');
  const s = String(rawLabel).trim();
  if (s === 'In Progress') return t('inProgress');
  // Match backend English round format: "East 1", "South 2 (H1)", etc.
  const m = /^(East|South|West|North)\s+(\d+)(?:\s*\(H(\d+)\))?$/i.exec(s);
  if (!m) return s;
  const windMap = {
    east: t('windEast'),
    south: t('windSouth'),
    west: t('windWest'),
    north: t('windNorth'),
  };
  const wind = windMap[m[1].toLowerCase()] || m[1];
  const num = m[2];
  const honba = m[3];
  return honba && Number(honba) > 0
    ? t('roundWithHonba', { wind, num, honba })
    : t('roundNoHonba', { wind, num });
}

export function tPresetId(presetId) {
  const id = String(presetId || 'Regular').trim();
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.presets && cat.presets[id]) || CATALOGS.en.presets[id];
  return entry ? entry.label : id;
}

export function tPreset(item) {
  if (!item) return { label: '', desc: '' };
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.presets && cat.presets[item.id]) || CATALOGS.en.presets[item.id];
  return {
    label: (entry && entry.label) || item.label || item.id,
    desc: (entry && entry.desc) || item.description || '',
  };
}

export function tProviderId(providerId) {
  const id = String(providerId || '').trim().toLowerCase();
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.providers && cat.providers[id]) || CATALOGS.en.providers[id];
  return entry ? entry.label : providerId;
}

export function tProvider(item) {
  if (!item) return { label: '', desc: '' };
  const id = String(item.id || '').toLowerCase();
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.providers && cat.providers[id]) || CATALOGS.en.providers[id];
  return {
    label: (entry && entry.label) || item.label || item.id,
    desc: (entry && entry.desc) || item.description || '',
  };
}

export function tBaseline(item) {
  if (!item) return { label: '', desc: '' };
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.baselines && cat.baselines[item.id]) || CATALOGS.en.baselines[item.id];
  return {
    label: (entry && entry.label) || item.label || item.id,
    desc: (entry && entry.desc) || item.description || '',
  };
}

export function tPolicyCategory(catItem) {
  if (!catItem) return '';
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.policies && cat.policies[catItem.key]) || CATALOGS.en.policies[catItem.key];
  return (entry && entry.label) || catItem.label || catItem.key;
}

export function tPolicyOption(catKey, optItem) {
  if (!optItem) return { label: '', desc: '' };
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const catEntry = (cat.policies && cat.policies[catKey]) || CATALOGS.en.policies[catKey];
  const optEntry = (catEntry && catEntry.options && catEntry.options[optItem.key]) ||
    (CATALOGS.en.policies[catKey] && CATALOGS.en.policies[catKey].options[optItem.key]);
  return {
    label: (optEntry && optEntry.label) || optItem.label || optItem.key,
    desc: (optEntry && optEntry.desc) || optItem.description || '',
  };
}

export function tYaku(item) {
  if (!item) return { name: '', han: '', desc: '' };
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.yakus && cat.yakus[item.name]) || CATALOGS.en.yakus[item.name];
  return {
    name: (entry && entry.name) || item.displayName || item.name,
    han: (entry && entry.han) || item.hanSummary || '',
    desc: (entry && entry.desc) || item.description || '',
  };
}

export function tYakuName(rawName, fallbackDisplay = '') {
  if (!rawName) return fallbackDisplay || '';
  const key = String(rawName).trim();
  const cat = CATALOGS[currentLang] || CATALOGS.en;
  const entry = (cat.yakus && cat.yakus[key]) || CATALOGS.en.yakus[key];
  if (entry && entry.name) return entry.name;
  // Handle "Dora 2", "Red Dora 1", "Ura Dora 1" style strings if engine emits count suffixes
  const countMatch = /^([A-Za-z]+)\s+(\d+)$/.exec(key);
  if (countMatch) {
    const baseEntry = (cat.yakus && cat.yakus[countMatch[1]]) || CATALOGS.en.yakus[countMatch[1]];
    if (baseEntry && baseEntry.name) return `${baseEntry.name} ${countMatch[2]}`;
  }
  return fallbackDisplay || key;
}
