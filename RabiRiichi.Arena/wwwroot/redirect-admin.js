import { applyStaticTranslations } from './i18n/index.js';

applyStaticTranslations();
window.location.replace('/' + (window.location.search || '') + '#config');
