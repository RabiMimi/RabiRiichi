// Visual Configuration Studio for RabiRiichi Arena.
// Covers 100% of ArenaConfig: Models & Baselines, Game Format & Points,
// Visual 38-Tile & Wildcard Deck Builder, Rule Policies, Standard + Koyaku Yaku Picker,
// Prompts & Fair-Play Controls, and Run / Elo / Replay URL / Security settings.

import {
  clear,
  el,
  createTileFace,
  formatInt,
  providerBadgeClass,
  showToast,
  openDialogModal,
  closeDialogModal,
  confirmAction,
} from './dom.js';
import {
  LANG_LABELS,
  t,
  tPreset,
  tProvider,
  tProviderId,
  tBaseline,
  tPolicyCategory,
  tPolicyOption,
  tYaku,
} from './i18n/index.js';

function getSectionTabs() {
  return [
    { id: 'all', label: t('csTabAll') },
    { id: 'models', label: t('csTabModels') },
    { id: 'format', label: t('csTabFormat') },
    { id: 'tiles', label: t('csTabTiles') },
    { id: 'policies', label: t('csTabPolicies') },
    { id: 'yakus', label: t('csTabYakus') },
    { id: 'prompts', label: t('csTabPrompts') },
    { id: 'system', label: t('csTabSystem') },
  ];
}

function getSuitGroups() {
  return [
    { label: t('csSuitManzu'), codes: ['1m', '2m', '3m', '4m', '5m', 'r5m', '6m', '7m', '8m', '9m'] },
    { label: t('csSuitPinzu'), codes: ['1p', '2p', '3p', '4p', '5p', 'r5p', '6p', '7p', '8p', '9p'] },
    { label: t('csSuitSouzu'), codes: ['1s', '2s', '3s', '4s', '5s', 'r5s', '6s', '7s', '8s', '9s'] },
    { label: t('csSuitHonors'), codes: ['1z', '2z', '3z', '4z', '5z', '6z', '7z'] },
    { label: t('csSuitWildcard'), codes: ['*'] },
  ];
}

export function cloneConfigDraft(cfg, metadata) {
  const src = cfg || {};
  const rules = src.gameRules || {};
  const ts = rules.tileSet || {};
  const prompts = src.prompts || {};
  const decision = src.decision || {};
  const run = src.run || {};
  const rating = src.rating || {};
  const defaultYakus = (metadata && metadata.defaultAllowedYakus) || [];

  return {
    workspaceDir: src.workspaceDir || './workspace',
    adminPassword: '',
    clientUrl: src.clientUrl || 'http://localhost:5173',
    wsUrl: src.wsUrl || '',
    exposeReasoningPublicly: src.exposeReasoningPublicly !== false,
    models: Array.isArray(src.models)
      ? src.models.map((m) => ({
          id: m.id || '',
          displayName: m.displayName || '',
          provider: m.provider || 'openai',
          model: m.model || '',
          baseUrl: m.baseUrl || '',
          apiKey: '',
          hasApiKey: Boolean(m.hasApiKey),
          clearApiKey: false,
          thinkingLevel: m.thinkingLevel || 'minimal',
          temperature: m.temperature ?? null,
          maxOutputTokens: m.maxOutputTokens ?? null,
          promptLanguage: m.promptLanguage || '',
          variant: m.variant || 'balanced',
          frozenElo: m.frozenElo ?? null,
          enabled: m.enabled !== false,
          notes: m.notes || '',
        }))
      : [],
    gameRules: {
      playerCount: Number(rules.playerCount ?? 4),
      totalRound: Number(rules.totalRound ?? 1),
      minHan: Number(rules.minHan ?? 1),
      initialPoints: Number(rules.initialPoints ?? 25000),
      finishPoints: Number(rules.finishPoints ?? 30000),
      lowerPoints: Number(rules.lowerPoints ?? 0),
      upperPoints: Number(rules.upperPoints ?? 1000000),
      riichiPoints: Number(rules.riichiPoints ?? 1000),
      honbaPoints: Number(rules.honbaPoints ?? 300),
      ryuukyokuPoints0: Number(rules.ryuukyokuPoints0 ?? 1000),
      ryuukyokuPoints1: Number(rules.ryuukyokuPoints1 ?? 1500),
      pointsDeductionPolicy: Number(rules.pointsDeductionPolicy ?? 0),
      renchanPolicy: Number(rules.renchanPolicy ?? 3),
      endGamePolicy: Number(rules.endGamePolicy ?? 29),
      kuikaePolicy: Number(rules.kuikaePolicy ?? 3),
      riichiPolicy: Number(rules.riichiPolicy ?? 7),
      doraOption: Number(rules.doraOption ?? 15),
      agariOption: Number(rules.agariOption ?? 7),
      scoringOption: Number(rules.scoringOption ?? 14),
      ryuukyokuTrigger: Number(rules.ryuukyokuTrigger ?? 31),
      tileSet: {
        preset: ts.preset || 'Regular',
        extraWildcards: Number(ts.extraWildcards ?? 0),
        customCounts: ts.customCounts ? { ...ts.customCounts } : {},
      },
      allowedYakus: Array.isArray(rules.allowedYakus) ? [...rules.allowedYakus] : [...defaultYakus],
    },
    prompts: {
      language: prompts.language || 'en',
      toolContextMode: prompts.toolContextMode || 'standard',
      askLlmForCalls: prompts.askLlmForCalls !== false,
      enableTableTalk: Boolean(prompts.enableTableTalk),
      maxHistoryTurns: Number(prompts.maxHistoryTurns ?? 24),
      revealOpponentIdentity: Boolean(prompts.revealOpponentIdentity),
      customSystemAppendix: prompts.customSystemAppendix || '',
    },
    decision: {
      timeoutSeconds: Number(decision.timeoutSeconds ?? 60),
      maxRetries: Number(decision.maxRetries ?? 2),
      maxOutputTokens: Number(decision.maxOutputTokens ?? 1024),
    },
    run: {
      scheduleMode: run.scheduleMode || 'swiss',
      swissRounds: Number(run.swissRounds ?? 4),
      targetMatches: Number(run.targetMatches ?? 8),
      matchConcurrency: Number(run.matchConcurrency ?? 2),
    },
    rating: {
      initialElo: Number(rating.initialElo ?? 1500),
      kFactor: Number(rating.kFactor ?? 24),
      scale: Number(rating.scale ?? 400),
    },
  };
}

export class ConfigStudioController {
  constructor({ rootEl, getMetadata, isAuthenticated, onRequestLogin, apiSaveConfig, apiTestModel, onDirtyChange, onSaved }) {
    this.rootEl = rootEl;
    this.getMetadata = getMetadata;
    this.isAuthenticated = isAuthenticated;
    this.onRequestLogin = onRequestLogin;
    this.apiSaveConfig = apiSaveConfig;
    this.apiTestModel = apiTestModel;
    this.onDirtyChange = onDirtyChange;
    this.onSaved = onSaved;

    this.serverSnapshot = null;
    this.draft = null;
    this.dirty = false;
    this.activeSection = 'all';
    this.yakuSearch = '';
    this.yakuGroup = 'all';
    this.testResults = new Map();
    this.saveErrors = [];

    this.wireImportModal();
  }

  wireImportModal() {
    const closeBtn = document.getElementById('import-json-close-btn');
    const cancelBtn = document.getElementById('import-json-cancel-btn');
    const applyBtn = document.getElementById('import-json-apply-btn');
    const fileInput = document.getElementById('import-json-file');
    const area = document.getElementById('import-json-textarea');

    if (closeBtn) closeBtn.addEventListener('click', () => closeDialogModal('import-json-dialog'));
    if (cancelBtn) cancelBtn.addEventListener('click', () => closeDialogModal('import-json-dialog'));
    if (fileInput) {
      fileInput.addEventListener('change', async () => {
        const f = fileInput.files && fileInput.files[0];
        if (!f || !area) return;
        const txt = await f.text();
        area.value = txt;
      });
    }
    if (applyBtn) {
      applyBtn.addEventListener('click', () => {
        if (!area) return;
        try {
          const parsed = JSON.parse(area.value);
          this.draft = cloneConfigDraft(parsed, this.getMetadata());
          this.markDirty(true);
          closeDialogModal('import-json-dialog');
          this.render();
          showToast(t('toastJsonImported'), 'success');
        } catch (err) {
          showToast(t('toastInvalidJson', { error: err.message || err }), 'error');
        }
      });
    }
  }

  loadFromServer(configDto) {
    this.serverSnapshot = cloneConfigDraft(configDto, this.getMetadata());
    this.draft = cloneConfigDraft(configDto, this.getMetadata());
    this.saveErrors = [];
    this.markDirty(false);
    this.render();
  }

  focusSection(sectionId) {
    this.activeSection = sectionId || 'all';
    this.render();
  }

  markDirty(flag) {
    this.dirty = Boolean(flag);
    if (typeof this.onDirtyChange === 'function') {
      this.onDirtyChange(this.dirty);
    }
    const statusText = document.getElementById('studio-dirty-indicator');
    if (statusText) {
      statusText.textContent = this.dirty ? t('csDirtyUnsaved') : t('csDirtySaved');
      statusText.className = this.dirty ? 'badge badge-warn' : 'badge badge-success';
    }
    const saveBtn = document.getElementById('studio-save-btn');
    if (saveBtn) saveBtn.disabled = !this.dirty;
    const revertBtn = document.getElementById('studio-revert-btn');
    if (revertBtn) revertBtn.disabled = !this.dirty;
  }

  effectiveTileCounts() {
    const meta = this.getMetadata() || {};
    const ts = this.draft.gameRules.tileSet;
    const codes = meta.canonicalTileCodes || [];
    const out = {};
    if (ts.preset === 'Custom') {
      for (const code of codes) {
        out[code] = Math.max(0, Number(ts.customCounts[code] ?? 0));
      }
      out['*'] = Math.max(0, Number(out['*'] || 0) + Number(ts.extraWildcards || 0));
      return out;
    }
    const presetMap = (meta.presetTileCounts && meta.presetTileCounts[ts.preset]) || {};
    for (const code of codes) {
      out[code] = Number(presetMap[code] ?? 0);
    }
    out['*'] = Math.max(0, Number(out['*'] || 0) + Number(ts.extraWildcards || 0));
    return out;
  }

  forkPresetIntoCustom() {
    const meta = this.getMetadata() || {};
    const ts = this.draft.gameRules.tileSet;
    if (ts.preset === 'Custom') return;
    const counts = this.effectiveTileCounts();
    const nextCustom = {};
    for (const code of meta.canonicalTileCodes || Object.keys(counts)) {
      if (counts[code] > 0) nextCustom[code] = counts[code];
    }
    ts.preset = 'Custom';
    ts.extraWildcards = 0;
    ts.customCounts = nextCustom;
    this.markDirty(true);
  }

  exportJson() {
    if (!this.draft) return;
    const redactedOut = JSON.parse(JSON.stringify(this.draft));
    delete redactedOut.adminPassword;
    for (const m of redactedOut.models || []) {
      delete m.apiKey;
      delete m.hasApiKey;
      delete m.clearApiKey;
    }
    const blob = new Blob([JSON.stringify(redactedOut, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = el('a', { href: url, download: 'arena.config.export.json' });
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  async save() {
    if (!this.draft) return;
    this.saveErrors = [];
    const res = await this.apiSaveConfig(this.draft);
    if (!res || !res.ok) {
      this.saveErrors = (res && res.errors) || [t('csSaveFailedDefault')];
      this.render();
      showToast(this.saveErrors[0], 'error');
      return;
    }
    this.markDirty(false);
    showToast(t('csSavedToast'), 'success');
    await this.onSaved();
  }

  render() {
    clear(this.rootEl);

    if (!this.isAuthenticated()) {
      this.rootEl.appendChild(
        el('div', { className: 'card' }, [
          el('div', { className: 'empty-state' }, [
            el('h3', { text: t('csAuthRequiredTitle') }),
            el('p', {
              className: 'muted-text',
              text: t('csAuthRequiredDesc'),
            }),
            el(
              'button',
              {
                type: 'button',
                className: 'btn btn-primary',
                text: t('csUnlockBtn'),
                on: { click: () => this.onRequestLogin() },
              }
            ),
          ]),
        ])
      );
      return;
    }

    if (!this.draft) {
      this.rootEl.appendChild(el('div', { className: 'card empty-state', text: t('csLoading') }));
      return;
    }

    // Sticky Studio Action Bar
    const topbar = el('div', { className: 'studio-topbar' }, [
      el('div', { className: 'badge-cluster' }, [
        el('span', {
          id: 'studio-dirty-indicator',
          className: this.dirty ? 'badge badge-warn' : 'badge badge-success',
          text: this.dirty ? t('csDirtyUnsaved') : t('csDirtySaved'),
        }),
        el('span', {
          className: 'badge',
          text: t('csContenderCount', {
            enabled: this.draft.models.filter((m) => m.enabled).length,
            total: this.draft.models.length,
          }),
        }),
      ]),
      el('div', { className: 'action-toolbar' }, [
        el('button', {
          type: 'button',
          className: 'btn btn-ghost btn-sm',
          text: t('csImportBtn'),
          on: {
            click: () => {
              const area = document.getElementById('import-json-textarea');
              if (area) area.value = '';
              openDialogModal('import-json-dialog');
            },
          },
        }),
        el('button', {
          type: 'button',
          className: 'btn btn-ghost btn-sm',
          text: t('csExportBtn'),
          on: { click: () => this.exportJson() },
        }),
        el('button', {
          id: 'studio-revert-btn',
          type: 'button',
          className: 'btn btn-ghost btn-sm',
          text: t('csRevertBtn'),
          disabled: !this.dirty,
          on: {
            click: async () => {
              const ok = await confirmAction({
                title: t('csRevertConfirmTitle'),
                message: t('csRevertConfirmMsg'),
                confirmLabel: t('csRevertConfirmBtn'),
                danger: true,
              });
              if (!ok) return;
              this.draft = cloneConfigDraft(this.serverSnapshot, this.getMetadata());
              this.saveErrors = [];
              this.markDirty(false);
              this.render();
            },
          },
        }),
        el('button', {
          id: 'studio-save-btn',
          type: 'button',
          className: 'btn btn-primary btn-sm',
          text: t('csSaveBtn'),
          disabled: !this.dirty,
          on: { click: () => this.save() },
        }),
      ]),
    ]);
    this.rootEl.appendChild(topbar);

    if (this.saveErrors.length > 0) {
      this.rootEl.appendChild(
        el(
          'div',
          { className: 'inline-alert inline-alert-danger studio-section' },
          [
            el('strong', { text: t('csValidationErrorsHeader') }),
            el(
              'ul',
              {},
              this.saveErrors.map((err) => el('li', { text: err }))
            ),
          ]
        )
      );
    }

    // Section Filter Subnav
    const subnav = el(
      'div',
      { className: 'studio-subnav' },
      getSectionTabs().map((sec) =>
        el('button', {
          type: 'button',
          className: `subnav-pill${this.activeSection === sec.id ? ' active' : ''}`,
          text: sec.label,
          on: {
            click: () => {
              this.activeSection = sec.id;
              this.render();
            },
          },
        })
      )
    );
    this.rootEl.appendChild(subnav);

    const show = (id) => this.activeSection === 'all' || this.activeSection === id;

    if (show('models')) this.rootEl.appendChild(this.renderModelsSection());
    if (show('format')) this.rootEl.appendChild(this.renderFormatPointsSection());
    if (show('tiles')) this.rootEl.appendChild(this.renderTileSetSection());
    if (show('policies')) this.rootEl.appendChild(this.renderPoliciesSection());
    if (show('yakus')) this.rootEl.appendChild(this.renderYakusSection());
    if (show('prompts')) this.rootEl.appendChild(this.renderPromptsSection());
    if (show('system')) this.rootEl.appendChild(this.renderSystemSection());
  }

  renderModelsSection() {
    const meta = this.getMetadata() || {};
    const providers = meta.providers || [];
    const baselineVariants = meta.baselineVariants || [];
    const globalPromptLang = this.draft.prompts.language || 'en';
    const inheritLabel = t('csPromptLangInherit', { lang: LANG_LABELS[globalPromptLang] || 'English' });

    const addLlm = () => {
      const idx = this.draft.models.length + 1;
      this.draft.models.push({
        id: `llm-model-${idx}`,
        displayName: t('csNewLlmName', { idx }),
        provider: 'openai',
        model: 'gpt-5-mini',
        baseUrl: 'https://api.openai.com',
        apiKey: '',
        hasApiKey: false,
        clearApiKey: false,
        thinkingLevel: 'minimal',
        temperature: null,
        maxOutputTokens: null,
        promptLanguage: '',
        variant: 'balanced',
        frozenElo: null,
        enabled: true,
        notes: '',
      });
      this.markDirty(true);
      this.render();
    };

    const addBaseline = (variant = 'balanced') => {
      const vMeta = baselineVariants.find((v) => v.id === variant) || {
        id: variant,
        label: `Baseline (${variant})`,
        suggestedFrozenElo: 1500,
      };
      const locBase = tBaseline(vMeta);
      let candidateId = `baseline-${vMeta.id.replace(/_/g, '-')}`;
      let suffix = 2;
      while (this.draft.models.some((m) => m.id === candidateId)) {
        candidateId = `baseline-${vMeta.id.replace(/_/g, '-')}-${suffix++}`;
      }
      this.draft.models.push({
        id: candidateId,
        displayName: locBase.label || vMeta.label,
        provider: 'baseline',
        model: '',
        baseUrl: '',
        apiKey: '',
        hasApiKey: false,
        clearApiKey: false,
        thinkingLevel: 'minimal',
        temperature: null,
        maxOutputTokens: null,
        promptLanguage: '',
        variant: vMeta.id,
        frozenElo: vMeta.id === 'balanced' ? 1500 : null,
        enabled: true,
        notes: locBase.desc || vMeta.description || '',
      });
      this.markDirty(true);
      this.render();
    };

    const addFullBaselineSuite = () => {
      let added = 0;
      for (const v of baselineVariants) {
        const expectedId = `baseline-${v.id.replace(/_/g, '-')}`;
        if (!this.draft.models.some((m) => m.provider === 'baseline' && m.variant === v.id)) {
          const locBase = tBaseline(v);
          this.draft.models.push({
            id: this.draft.models.some((m) => m.id === expectedId) ? `${expectedId}-anchor` : expectedId,
            displayName: locBase.label || v.label,
            provider: 'baseline',
            model: '',
            baseUrl: '',
            apiKey: '',
            hasApiKey: false,
            clearApiKey: false,
            thinkingLevel: 'minimal',
            temperature: null,
            maxOutputTokens: null,
            promptLanguage: '',
            variant: v.id,
            frozenElo: v.id === 'balanced' ? 1500 : null,
            enabled: true,
            notes: locBase.desc || v.description || '',
          });
          added++;
        }
      }
      if (added > 0) {
        this.markDirty(true);
        this.render();
        showToast(t('csAddedBaselinesToast', { count: added }), 'success');
      } else {
        showToast(t('csAllBaselinesExistToast'), 'info');
      }
    };

    const thinkingLabels = {
      minimal: t('csThinkMinimal'),
      low: t('csThinkLow'),
      medium: t('csThinkMedium'),
      high: t('csThinkHigh'),
    };

    const cards = this.draft.models.map((m, idx) => {
      const isBaseline = String(m.provider || '').toLowerCase() === 'baseline';
      const provMeta = providers.find((p) => p.id === m.provider) || providers[0] || {};
      const testState = this.testResults.get(idx);

      return el(
        'div',
        { className: `model-editor-card${m.enabled ? '' : ' disabled-model'}` },
        [
          el('div', { className: 'model-card-header' }, [
            el('div', { className: 'badge-cluster' }, [
              el('label', { className: 'checkbox-pill' }, [
                el('input', {
                  type: 'checkbox',
                  checked: m.enabled,
                  on: {
                    change: (e) => {
                      m.enabled = e.target.checked;
                      this.markDirty(true);
                      this.render();
                    },
                  },
                }),
                el('span', { text: m.enabled ? t('csEnabled') : t('csDisabled') }),
              ]),
              el('span', { className: providerBadgeClass(m.provider), text: tProviderId(m.provider) }),
              m.hasApiKey && !m.clearApiKey
                ? el('span', { className: 'badge badge-success', text: t('csApiKeyStored') })
                : null,
              m.frozenElo !== null && m.frozenElo !== undefined && m.frozenElo !== ''
                ? el('span', { className: 'badge badge-warn', text: t('csAnchorEloBadge', { elo: m.frozenElo }) })
                : null,
            ]),
            el('div', { className: 'action-toolbar' }, [
              el('button', {
                type: 'button',
                className: 'btn btn-secondary btn-xs',
                text: testState && testState.loading ? t('csTestingBtn') : t('csTestConnBtn'),
                disabled: Boolean(testState && testState.loading),
                on: {
                  click: async () => {
                    this.testResults.set(idx, { loading: true });
                    this.render();
                    const res = await this.apiTestModel(m);
                    this.testResults.set(idx, { loading: false, ...res });
                    this.render();
                  },
                },
              }),
              el('button', {
                type: 'button',
                className: 'btn btn-ghost btn-xs',
                text: t('csDuplicateBtn'),
                on: {
                  click: () => {
                    const copy = {
                      ...m,
                      id: `${m.id}-copy`,
                      displayName: t('csCopySuffix', { name: m.displayName }),
                      apiKey: '',
                      hasApiKey: false,
                    };
                    this.draft.models.splice(idx + 1, 0, copy);
                    this.markDirty(true);
                    this.render();
                  },
                },
              }),
              el('button', {
                type: 'button',
                className: 'btn btn-danger btn-xs',
                text: t('csRemoveBtn'),
                on: {
                  click: () => {
                    this.draft.models.splice(idx, 1);
                    this.markDirty(true);
                    this.render();
                  },
                },
              }),
            ]),
          ]),

          el('div', { className: 'form-grid-4' }, [
            el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldUniqueId') }),
              el('input', {
                type: 'text',
                className: 'input-text mono',
                value: m.id,
                placeholder: 'e.g. gpt-5-mini',
                on: {
                  input: (e) => {
                    m.id = e.target.value.trim();
                    this.markDirty(true);
                  },
                },
              }),
            ]),
            el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldDisplayName') }),
              el('input', {
                type: 'text',
                className: 'input-text',
                value: m.displayName,
                placeholder: t('csPlaceholderDisplayName'),
                on: {
                  input: (e) => {
                    m.displayName = e.target.value;
                    this.markDirty(true);
                  },
                },
              }),
            ]),
            el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldProvider') }),
              el(
                'select',
                {
                  className: 'input-select',
                  on: {
                    change: (e) => {
                      const nextProv = e.target.value;
                      m.provider = nextProv;
                      const nextMeta = providers.find((p) => p.id === nextProv);
                      if (nextMeta && nextProv !== 'baseline') {
                        m.baseUrl = nextMeta.defaultBaseUrl || '';
                        if (!m.model) m.model = nextMeta.placeholderModel || '';
                      }
                      this.markDirty(true);
                      this.render();
                    },
                  },
                },
                providers.map((p) =>
                  el('option', {
                    value: p.id,
                    text: tProvider(p).label,
                    attrs: m.provider === p.id ? { selected: 'selected' } : {},
                  })
                )
              ),
            ]),

            isBaseline
              ? el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldBaselineProfile') }),
                  el(
                    'select',
                    {
                      className: 'input-select',
                      on: {
                        change: (e) => {
                          m.variant = e.target.value;
                          this.markDirty(true);
                        },
                      },
                    },
                    baselineVariants.map((bv) =>
                      el('option', {
                        value: bv.id,
                        text: tBaseline(bv).label,
                        attrs: m.variant === bv.id ? { selected: 'selected' } : {},
                      })
                    )
                  ),
                ])
              : el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldModelId') }),
                  el('input', {
                    type: 'text',
                    className: 'input-text mono',
                    value: m.model,
                    placeholder: provMeta.placeholderModel || 'model-name',
                    on: {
                      input: (e) => {
                        m.model = e.target.value.trim();
                        this.markDirty(true);
                      },
                    },
                  }),
                ]),
          ]),

          !isBaseline
            ? el('div', { className: 'form-grid-4' }, [
                el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldBaseUrl') }),
                  el('input', {
                    type: 'url',
                    className: 'input-text mono',
                    value: m.baseUrl,
                    placeholder: provMeta.defaultBaseUrl || 'https://api.openai.com',
                    on: {
                      input: (e) => {
                        m.baseUrl = e.target.value.trim();
                        this.markDirty(true);
                      },
                    },
                  }),
                ]),
                el('div', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldApiKey') }),
                  el('input', {
                    type: 'password',
                    className: 'input-text mono',
                    value: m.apiKey,
                    placeholder: m.hasApiKey && !m.clearApiKey ? t('csPlaceholderKeyStored') : t('csPlaceholderKeyPaste'),
                    attrs: { autocomplete: 'new-password' },
                    on: {
                      input: (e) => {
                        m.apiKey = e.target.value;
                        if (m.apiKey) m.clearApiKey = false;
                        this.markDirty(true);
                      },
                    },
                  }),
                  m.hasApiKey
                    ? el('label', { className: 'field-hint' }, [
                        el('input', {
                          type: 'checkbox',
                          checked: Boolean(m.clearApiKey),
                          on: {
                            change: (e) => {
                              m.clearApiKey = e.target.checked;
                              this.markDirty(true);
                              this.render();
                            },
                          },
                        }),
                        t('csClearKeyCheckbox'),
                      ])
                    : null,
                ]),
                el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldThinking') }),
                  el(
                    'select',
                    {
                      className: 'input-select',
                      on: {
                        change: (e) => {
                          m.thinkingLevel = e.target.value;
                          this.markDirty(true);
                        },
                      },
                    },
                    ['minimal', 'low', 'medium', 'high'].map((lvl) =>
                      el('option', {
                        value: lvl,
                        text: thinkingLabels[lvl] || lvl,
                        attrs: m.thinkingLevel === lvl ? { selected: 'selected' } : {},
                      })
                    )
                  ),
                ]),
                el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldTemp') }),
                  el('input', {
                    type: 'number',
                    className: 'input-text',
                    min: 0,
                    max: 2,
                    step: '0.05',
                    value: m.temperature ?? '',
                    placeholder: t('csPlaceholderTemp'),
                    on: {
                      input: (e) => {
                        m.temperature = e.target.value === '' ? null : Number(e.target.value);
                        this.markDirty(true);
                      },
                    },
                  }),
                ]),
              ])
            : null,

          el('div', { className: isBaseline ? 'form-grid-2' : 'form-grid-4' }, [
            !isBaseline
              ? el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldMaxTokens') }),
                  el('input', {
                    type: 'number',
                    className: 'input-text',
                    min: 64,
                    max: 32768,
                    value: m.maxOutputTokens ?? '',
                    placeholder: t('csPlaceholderMaxTokens', { tokens: this.draft.decision.maxOutputTokens }),
                    on: {
                      input: (e) => {
                        m.maxOutputTokens = e.target.value === '' ? null : Number(e.target.value);
                        this.markDirty(true);
                      },
                    },
                  }),
                ])
              : null,
            !isBaseline
              ? el('label', { className: 'field-group' }, [
                  el('span', { className: 'field-label', text: t('csFieldPromptLangOverride') }),
                  el(
                    'select',
                    {
                      className: 'input-select',
                      on: {
                        change: (e) => {
                          m.promptLanguage = e.target.value;
                          this.markDirty(true);
                        },
                      },
                    },
                    [
                      { v: '', label: inheritLabel },
                      { v: 'en', label: t('csPromptLangEn') },
                      { v: 'zhs', label: t('csPromptLangZhs') },
                      { v: 'zht', label: t('csPromptLangZht') },
                      { v: 'ja', label: t('csPromptLangJa') },
                    ].map((opt) =>
                      el('option', {
                        value: opt.v,
                        text: opt.label,
                        attrs: (m.promptLanguage || '') === opt.v ? { selected: 'selected' } : {},
                      })
                    )
                  ),
                ])
              : null,
            el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldFrozenElo') }),
              el('input', {
                type: 'number',
                className: 'input-text',
                min: 100,
                max: 4000,
                step: '10',
                value: m.frozenElo ?? '',
                placeholder: t('csPlaceholderFrozenElo'),
                on: {
                  input: (e) => {
                    m.frozenElo = e.target.value === '' ? null : Number(e.target.value);
                    this.markDirty(true);
                  },
                },
              }),
            ]),
            el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldNotes') }),
              el('input', {
                type: 'text',
                className: 'input-text',
                value: m.notes || '',
                placeholder: t('csPlaceholderNotes'),
                on: {
                  input: (e) => {
                    m.notes = e.target.value;
                    this.markDirty(true);
                  },
                },
              }),
            ]),
          ]),

          testState && !testState.loading
            ? el(
                'div',
                { className: `inline-alert ${testState.ok ? 'inline-alert-success' : 'inline-alert-danger'}` },
                [testState.message || (testState.ok ? t('csConnOk') : t('csConnFailed'))]
              )
            : null,
        ]
      );
    });

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec1Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec1Sub'),
          }),
        ]),
        el('div', { className: 'action-toolbar' }, [
          el('button', { type: 'button', className: 'btn btn-primary btn-sm', text: t('csAddLlmBtn'), on: { click: addLlm } }),
          el('button', {
            type: 'button',
            className: 'btn btn-secondary btn-sm',
            text: t('csAddBaselineBtn'),
            on: { click: () => addBaseline('balanced') },
          }),
          el('button', {
            type: 'button',
            className: 'btn btn-ghost btn-sm',
            text: t('csAddAllBaselinesBtn'),
            on: { click: addFullBaselineSuite },
          }),
        ]),
      ]),
      cards.length > 0
        ? el('div', { className: 'models-roster-list' }, cards)
        : el('div', { className: 'empty-state', text: t('csEmptyModels') }),
    ]);
  }

  renderFormatPointsSection() {
    const r = this.draft.gameRules;
    const numInput = (label, key, min, max, step = 100) =>
      el('label', { className: 'field-group' }, [
        el('span', { className: 'field-label', text: label }),
        el('input', {
          type: 'number',
          className: 'input-text tabular',
          min,
          max,
          step,
          value: r[key],
          on: {
            input: (e) => {
              r[key] = Number(e.target.value);
              this.markDirty(true);
            },
          },
        }),
      ]);

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec2Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec2Sub'),
          }),
        ]),
      ]),
      el('div', { className: 'form-grid-3' }, [
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldPlayersPerTable') }),
          el(
            'select',
            {
              className: 'input-select',
              on: {
                change: (e) => {
                  r.playerCount = Number(e.target.value);
                  this.markDirty(true);
                  this.render();
                },
              },
            },
            [
              { v: 2, label: t('csPlayers2') },
              { v: 3, label: t('csPlayers3') },
              { v: 4, label: t('csPlayers4') },
            ].map((o) =>
              el('option', { value: o.v, text: o.label, attrs: r.playerCount === o.v ? { selected: 'selected' } : {} })
            )
          ),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldMatchLength') }),
          el(
            'select',
            {
              className: 'input-select',
              on: {
                change: (e) => {
                  r.totalRound = Number(e.target.value);
                  this.markDirty(true);
                },
              },
            },
            [
              { v: 1, label: t('csLengthEast') },
              { v: 2, label: t('csLengthHanchan') },
            ].map((o) =>
              el('option', { value: o.v, text: o.label, attrs: r.totalRound === o.v ? { selected: 'selected' } : {} })
            )
          ),
        ]),
        numInput(t('csFieldMinHan'), 'minHan', 1, 13, 1),
      ]),
      el('div', { className: 'form-grid-4 deck-suit-row' }, [
        numInput(t('csFieldInitPts'), 'initialPoints', 1000, 500000, 1000),
        numInput(t('csFieldFinishPts'), 'finishPoints', 1000, 500000, 1000),
        numInput(t('csFieldLowerPts'), 'lowerPoints', -500000, 500000, 1000),
        numInput(t('csFieldUpperPts'), 'upperPoints', 10000, 100000000, 10000),
        numInput(t('csFieldRiichiPts'), 'riichiPoints', 0, 50000, 100),
        numInput(t('csFieldHonbaPts'), 'honbaPoints', 0, 30000, 100),
        numInput(t('csFieldNoten0'), 'ryuukyokuPoints0', 0, 50000, 100),
        numInput(t('csFieldNoten1'), 'ryuukyokuPoints1', 0, 50000, 100),
      ]),
    ]);
  }

  renderTileSetSection() {
    const meta = this.getMetadata() || {};
    const presets = meta.tilePresets || [];
    const ts = this.draft.gameRules.tileSet;
    const counts = this.effectiveTileCounts();
    const totalTiles = Object.values(counts).reduce((a, b) => a + b, 0);
    const wildcardCount = counts['*'] || 0;
    const minRequired = this.draft.gameRules.playerCount * 13 + 15;
    const wallOk = totalTiles >= minRequired;

    const presetCards = presets.map((p) => {
      const locP = tPreset(p);
      return el(
        'button',
        {
          type: 'button',
          className: `preset-card${ts.preset === p.id ? ' selected' : ''}`,
          on: {
            click: () => {
              if (p.id === 'Custom' && ts.preset !== 'Custom') {
                this.forkPresetIntoCustom();
              } else {
                ts.preset = p.id;
              }
              this.markDirty(true);
              this.render();
            },
          },
        },
        [
          el('div', { className: 'preset-card-title' }, [
            el('span', { text: locP.label }),
            p.defaultWildcards > 0 ? el('span', { className: 'badge badge-wildcard', text: `★ +${p.defaultWildcards}` }) : null,
          ]),
          el('div', { className: 'preset-card-desc', text: locP.desc }),
        ]
      );
    });

    const adjustTile = (code, delta) => {
      if (ts.preset !== 'Custom') {
        this.forkPresetIntoCustom();
      }
      const cur = Number(ts.customCounts[code] ?? 0);
      const next = Math.max(0, Math.min(136, cur + delta));
      if (next === 0) delete ts.customCounts[code];
      else ts.customCounts[code] = next;
      this.markDirty(true);
      this.render();
    };

    const suitRows = getSuitGroups().map((grp) =>
      el('div', { className: 'deck-suit-row' }, [
        el('div', { className: 'deck-suit-label', text: grp.label }),
        el(
          'div',
          { className: 'deck-tiles-grid' },
          grp.codes.map((code) => {
            const cnt = counts[code] || 0;
            return el(
              'div',
              { className: `deck-tile-stepper${cnt === 0 ? ' zero-tile' : ''}` },
              [
                createTileFace(code),
                el('div', { className: 'stepper-controls' }, [
                  el('button', {
                    type: 'button',
                    className: 'stepper-btn',
                    text: '−',
                    disabled: cnt <= 0,
                    attrs: { 'aria-label': t('csDecreaseTileAria', { code }) },
                    on: { click: () => adjustTile(code, -1) },
                  }),
                  el('span', { className: 'stepper-count', text: cnt }),
                  el('button', {
                    type: 'button',
                    className: 'stepper-btn',
                    text: '+',
                    attrs: { 'aria-label': t('csIncreaseTileAria', { code }) },
                    on: { click: () => adjustTile(code, 1) },
                  }),
                ]),
              ]
            );
          })
        ),
      ])
    );

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec3Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec3Sub'),
          }),
        ]),
        el('div', { className: 'badge-cluster' }, [
          el('span', {
            className: wallOk ? 'badge badge-success' : 'badge badge-danger',
            text: t('csTotalDeckBadge', { total: formatInt(totalTiles), min: minRequired }),
          }),
          el('span', {
            className: wildcardCount > 0 ? 'badge badge-wildcard' : 'badge',
            text: t('csWildcardsBadge', { count: wildcardCount }),
          }),
        ]),
      ]),

      el('div', { className: 'preset-cards-grid' }, presetCards),

      el('div', { className: 'form-grid-3' }, [
        ts.preset !== 'Custom'
          ? el('label', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csFieldExtraWildcards') }),
              el('input', {
                type: 'number',
                className: 'input-text tabular',
                min: 0,
                max: 64,
                value: ts.extraWildcards,
                on: {
                  input: (e) => {
                    ts.extraWildcards = Math.max(0, Math.min(64, Number(e.target.value) || 0));
                    this.markDirty(true);
                    this.render();
                  },
                },
              }),
            ])
          : el('div', { className: 'field-group' }, [
              el('span', { className: 'field-label', text: t('csCustomDeckActive') }),
              el('span', {
                className: 'field-hint',
                text: t('csCustomDeckHint'),
              }),
            ]),
        el('div', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldQuickFork') }),
          el('button', {
            type: 'button',
            className: 'btn btn-secondary btn-sm',
            text: ts.preset === 'Custom' ? t('csBtnResetCustomDeck') : t('csBtnCloneToCustom'),
            on: {
              click: () => {
                if (ts.preset === 'Custom') {
                  ts.preset = 'Regular';
                  ts.extraWildcards = 0;
                  this.forkPresetIntoCustom();
                } else {
                  this.forkPresetIntoCustom();
                }
                this.render();
              },
            },
          }),
        ]),
      ]),

      ...suitRows,
    ]);
  }

  renderPoliciesSection() {
    const meta = this.getMetadata() || {};
    const categories = meta.policyCategories || [];
    const rules = this.draft.gameRules;

    const cards = categories.map((cat) => {
      const curVal = Number(rules[cat.key] ?? cat.defaultValue);
      return el('div', { className: 'policy-card' }, [
        el('div', { className: 'match-player-top' }, [
          el('strong', { text: tPolicyCategory(cat) }),
          el('button', {
            type: 'button',
            className: 'btn btn-ghost btn-xs',
            text: t('csPolicyDefaultBtn'),
            on: {
              click: () => {
                rules[cat.key] = cat.defaultValue;
                this.markDirty(true);
                this.render();
              },
            },
          }),
        ]),
        el(
          'div',
          { className: 'policy-options-list' },
          (cat.options || []).map((opt) => {
            const isChecked = cat.isEnum ? curVal === opt.value : (curVal & opt.value) === opt.value;
            const locOpt = tPolicyOption(cat.key, opt);
            return el('label', { className: 'policy-opt-label' }, [
              el('input', {
                type: cat.isEnum ? 'radio' : 'checkbox',
                name: `policy-${cat.key}`,
                checked: isChecked,
                on: {
                  change: (e) => {
                    if (cat.isEnum) {
                      rules[cat.key] = opt.value;
                    } else if (e.target.checked) {
                      rules[cat.key] = curVal | opt.value;
                    } else {
                      rules[cat.key] = curVal & ~opt.value;
                    }
                    this.markDirty(true);
                    this.render();
                  },
                },
              }),
              el('div', {}, [
                el('div', { text: locOpt.label }),
                el('div', { className: 'field-hint', text: locOpt.desc }),
              ]),
            ]);
          })
        ),
      ]);
    });

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec4Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec4Sub'),
          }),
        ]),
      ]),
      el('div', { className: 'policies-grid' }, cards),
    ]);
  }

  renderYakusSection() {
    const meta = this.getMetadata() || {};
    const yakus = meta.yakus || [];
    const allowedSet = new Set(this.draft.gameRules.allowedYakus || []);

    const q = this.yakuSearch.trim().toLowerCase();
    const filtered = yakus.filter((y) => {
      if (this.yakuGroup !== 'all' && y.group !== this.yakuGroup) return false;
      if (!q) return true;
      const locY = tYaku(y);
      return (
        y.name.toLowerCase().includes(q) ||
        y.displayName.toLowerCase().includes(q) ||
        y.description.toLowerCase().includes(q) ||
        locY.name.toLowerCase().includes(q) ||
        locY.desc.toLowerCase().includes(q)
      );
    });

    const syncSet = () => {
      this.draft.gameRules.allowedYakus = yakus.filter((y) => allowedSet.has(y.name)).map((y) => y.name);
      this.markDirty(true);
      this.render();
    };

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header toolbar-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec5Title', { count: yakus.length }) }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec5Sub'),
          }),
        ]),
        el('span', {
          className: 'badge badge-success',
          text: t('csYakuEnabledBadge', { enabled: allowedSet.size, total: yakus.length }),
        }),
      ]),

      el('div', { className: 'action-toolbar studio-section' }, [
        el('input', {
          type: 'search',
          className: 'input-text input-sm',
          placeholder: t('csYakuSearchPlaceholder'),
          value: this.yakuSearch,
          on: {
            input: (e) => {
              this.yakuSearch = e.target.value;
              this.render();
            },
          },
        }),
        el(
          'select',
          {
            className: 'input-select input-sm',
            on: {
              change: (e) => {
                this.yakuGroup = e.target.value;
                this.render();
              },
            },
          },
          [
            { id: 'all', label: t('csYakuGroupAll', { count: yakus.length }) },
            { id: '1han', label: t('csYakuGroup1Han') },
            { id: '2han', label: t('csYakuGroup2Han') },
            { id: '3han', label: t('csYakuGroup3Han') },
            { id: '6han', label: t('csYakuGroup6Han') },
            { id: 'yakuman', label: t('csYakuGroupYakuman') },
            { id: 'koyaku', label: t('csYakuGroupKoyaku') },
            { id: 'other', label: t('csYakuGroupOther') },
          ].map((g) =>
            el('option', { value: g.id, text: g.label, attrs: this.yakuGroup === g.id ? { selected: 'selected' } : {} })
          )
        ),
        el('button', {
          type: 'button',
          className: 'btn btn-ghost btn-xs',
          text: t('csBtnStdYaku'),
          on: {
            click: () => {
              allowedSet.clear();
              for (const n of meta.defaultAllowedYakus || []) allowedSet.add(n);
              syncSet();
            },
          },
        }),
        el('button', {
          type: 'button',
          className: 'btn btn-secondary btn-xs',
          text: t('csBtnAllKoyaku'),
          on: {
            click: () => {
              for (const y of yakus) allowedSet.add(y.name);
              syncSet();
            },
          },
        }),
        el('button', {
          type: 'button',
          className: 'btn btn-ghost btn-xs',
          text: t('csBtnYakumanOnly'),
          on: {
            click: () => {
              allowedSet.clear();
              for (const y of yakus) {
                if (y.group === 'yakuman' || y.hanSummary.toLowerCase().includes('yakuman')) {
                  allowedSet.add(y.name);
                }
              }
              syncSet();
            },
          },
        }),
      ]),

      el(
        'div',
        { className: 'yaku-grid' },
        filtered.map((y) => {
          const checked = allowedSet.has(y.name);
          const isKoyaku = y.group === 'koyaku';
          const locY = tYaku(y);
          return el(
            'label',
            { className: `yaku-item${checked ? ' checked' : ''}${isKoyaku ? ' koyaku-item' : ''}` },
            [
              el('input', {
                type: 'checkbox',
                checked,
                on: {
                  change: (e) => {
                    if (e.target.checked) allowedSet.add(y.name);
                    else allowedSet.delete(y.name);
                    syncSet();
                  },
                },
              }),
              el('div', {}, [
                el('div', { className: 'badge-cluster' }, [
                  el('strong', { text: locY.name }),
                  el('span', {
                    className: isKoyaku ? 'badge badge-koyaku' : 'badge',
                    text: isKoyaku ? t('csKoyakuPrefix', { han: locY.han }) : locY.han,
                  }),
                ]),
                el('div', { className: 'field-hint', text: locY.desc }),
              ]),
            ]
          );
        })
      ),
    ]);
  }

  renderPromptsSection() {
    const p = this.draft.prompts;
    const d = this.draft.decision;

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec6Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec6Sub'),
          }),
        ]),
      ]),

      el('div', { className: 'form-grid-3' }, [
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldPromptLanguage') }),
          el(
            'select',
            {
              className: 'input-select',
              on: {
                change: (e) => {
                  p.language = e.target.value;
                  this.markDirty(true);
                  this.render();
                },
              },
            },
            [
              { v: 'en', label: t('csPromptLangEn') },
              { v: 'zhs', label: t('csPromptLangZhs') },
              { v: 'zht', label: t('csPromptLangZht') },
              { v: 'ja', label: t('csPromptLangJa') },
            ].map((o) =>
              el('option', { value: o.v, text: o.label, attrs: (p.language || 'en') === o.v ? { selected: 'selected' } : {} })
            )
          ),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldToolContext') }),
          el(
            'select',
            {
              className: 'input-select',
              on: {
                change: (e) => {
                  p.toolContextMode = e.target.value;
                  this.markDirty(true);
                },
              },
            },
            [
              { v: 'minimal', label: t('csToolMinimal') },
              { v: 'standard', label: t('csToolStandard') },
              { v: 'extended', label: t('csToolExtended') },
            ].map((o) =>
              el('option', { value: o.v, text: o.label, attrs: p.toolContextMode === o.v ? { selected: 'selected' } : {} })
            )
          ),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldHistoryTurns') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 0,
            max: 100,
            value: p.maxHistoryTurns,
            on: {
              input: (e) => {
                p.maxHistoryTurns = Math.max(0, Math.min(100, Number(e.target.value) || 0));
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldTimeoutSec') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 5,
            max: 600,
            value: d.timeoutSeconds,
            on: {
              input: (e) => {
                d.timeoutSeconds = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldMaxRetries') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 0,
            max: 5,
            value: d.maxRetries,
            on: {
              input: (e) => {
                d.maxRetries = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldDefaultMaxTokens') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 64,
            max: 32768,
            value: d.maxOutputTokens,
            on: {
              input: (e) => {
                d.maxOutputTokens = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
      ]),

      el('div', { className: 'action-toolbar deck-suit-row' }, [
        el('label', { className: 'checkbox-pill' }, [
          el('input', {
            type: 'checkbox',
            checked: p.askLlmForCalls,
            on: {
              change: (e) => {
                p.askLlmForCalls = e.target.checked;
                this.markDirty(true);
              },
            },
          }),
          el('span', { text: t('csCheckAskCalls') }),
        ]),
        el('label', { className: 'checkbox-pill' }, [
          el('input', {
            type: 'checkbox',
            checked: p.enableTableTalk,
            on: {
              change: (e) => {
                p.enableTableTalk = e.target.checked;
                this.markDirty(true);
              },
            },
          }),
          el('span', { text: t('csCheckTableTalk') }),
        ]),
        el('label', { className: 'checkbox-pill' }, [
          el('input', {
            type: 'checkbox',
            checked: p.revealOpponentIdentity,
            on: {
              change: (e) => {
                p.revealOpponentIdentity = e.target.checked;
                this.markDirty(true);
              },
            },
          }),
          el('span', { text: t('csCheckRevealOpponent') }),
        ]),
      ]),

      el('label', { className: 'field-group deck-suit-row' }, [
        el('span', { className: 'field-label', text: t('csFieldCustomAppendix') }),
        el('textarea', {
          rows: 4,
          className: 'input-textarea code-textarea',
          value: p.customSystemAppendix,
          placeholder: t('csPlaceholderAppendix'),
          on: {
            input: (e) => {
              p.customSystemAppendix = e.target.value;
              this.markDirty(true);
            },
          },
        }),
      ]),
    ]);
  }

  renderSystemSection() {
    const run = this.draft.run;
    const rating = this.draft.rating;

    return el('div', { className: 'card studio-section' }, [
      el('div', { className: 'card-header' }, [
        el('div', {}, [
          el('h2', { className: 'card-title', text: t('csSec7Title') }),
          el('p', {
            className: 'card-subtitle',
            text: t('csSec7Sub'),
          }),
        ]),
      ]),

      el('div', { className: 'form-grid-4' }, [
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldDefSchedule') }),
          el(
            'select',
            {
              className: 'input-select',
              on: {
                change: (e) => {
                  run.scheduleMode = e.target.value;
                  this.markDirty(true);
                },
              },
            },
            [
              { v: 'swiss', label: t('csSchedSwiss') },
              { v: 'balanced', label: t('csSchedBalanced') },
            ].map((o) =>
              el('option', { value: o.v, text: o.label, attrs: run.scheduleMode === o.v ? { selected: 'selected' } : {} })
            )
          ),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldDefSwissRounds') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 1,
            max: 500,
            value: run.swissRounds,
            on: {
              input: (e) => {
                run.swissRounds = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldDefBalancedMatches') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 1,
            max: 10000,
            value: run.targetMatches,
            on: {
              input: (e) => {
                run.targetMatches = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldMatchConcurrency') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 1,
            max: 16,
            value: run.matchConcurrency,
            on: {
              input: (e) => {
                run.matchConcurrency = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
      ]),

      el('div', { className: 'form-grid-3 deck-suit-row' }, [
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldInitElo') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 100,
            max: 4000,
            value: rating.initialElo,
            on: {
              input: (e) => {
                rating.initialElo = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldKFactor') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 1,
            max: 200,
            value: rating.kFactor,
            on: {
              input: (e) => {
                rating.kFactor = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldEloScale') }),
          el('input', {
            type: 'number',
            className: 'input-text tabular',
            min: 50,
            max: 2000,
            value: rating.scale,
            on: {
              input: (e) => {
                rating.scale = Number(e.target.value);
                this.markDirty(true);
              },
            },
          }),
        ]),
      ]),

      el('div', { className: 'form-grid-3 deck-suit-row' }, [
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldClientUrl') }),
          el('input', {
            type: 'url',
            className: 'input-text mono',
            value: this.draft.clientUrl,
            placeholder: 'http://localhost:5173',
            on: {
              input: (e) => {
                this.draft.clientUrl = e.target.value.trim();
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldWsUrl') }),
          el('input', {
            type: 'text',
            className: 'input-text mono',
            value: this.draft.wsUrl,
            placeholder: t('csPlaceholderWsUrl'),
            on: {
              input: (e) => {
                this.draft.wsUrl = e.target.value.trim();
                this.markDirty(true);
              },
            },
          }),
        ]),
        el('label', { className: 'field-group' }, [
          el('span', { className: 'field-label', text: t('csFieldAdminPwd') }),
          el('input', {
            type: 'password',
            className: 'input-text mono',
            value: this.draft.adminPassword,
            placeholder: t('csPlaceholderAdminPwd'),
            attrs: { autocomplete: 'new-password' },
            on: {
              input: (e) => {
                this.draft.adminPassword = e.target.value;
                this.markDirty(true);
              },
            },
          }),
        ]),
      ]),

      el('div', { className: 'action-toolbar deck-suit-row' }, [
        el('label', { className: 'checkbox-pill' }, [
          el('input', {
            type: 'checkbox',
            checked: this.draft.exposeReasoningPublicly,
            on: {
              change: (e) => {
                this.draft.exposeReasoningPublicly = e.target.checked;
                this.markDirty(true);
              },
            },
          }),
          el('span', { text: t('csCheckExposeReasoning') }),
        ]),
      ]),
    ]);
  }
}
