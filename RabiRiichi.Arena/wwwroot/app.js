// Main frontend application for RabiRiichi Arena.
// Zero innerHTML, zero inline styles, zero localStorage secrets, strict CSP 'self'.

import {
  clear,
  el,
  safeExternalHref,
  formatInt,
  formatSigned,
  formatPct,
  formatCompactTokens,
  formatDuration,
  formatTimestamp,
  providerBadgeClass,
  statusBadgeClass,
  showToast,
  openDialogModal,
  closeDialogModal,
  confirmAction,
} from './dom.js';

import {
  renderProgressRingSvg,
  renderPlacementMiniBarSvg,
  renderEloTrajectoryChart,
  renderPlacementStackedChart,
  renderPlaystyleScatterChart,
  renderHeadToHeadMatrix,
  renderYakuBreakdownChart,
} from './charts.js';

import { ConfigStudioController } from './config-studio.js';

import {
  getLang,
  setLang,
  onLangChange,
  applyStaticTranslations,
  getBcp47Locale,
  t,
  tStatus,
  tOutcome,
  tRoundLabel,
  tPresetId,
  tProviderId,
  tYakuName,
} from './i18n/index.js';

const state = {
  activeTab: 'overview',
  scope: 'current', // 'current' | 'all' | specific runId
  metadata: null,
  runs: [],
  nextStatus: null,
  analytics: null,
  matchesPage: { page: 1, pageSize: 15, totalCount: 0, items: [] },

  // Admin session (held strictly in JS memory; auth cookie is HttpOnly)
  adminAuthenticated: false,
  csrfToken: '',
  defaultPasswordWarning: false,
  adminStatus: null,
  adminConfig: null,

  // UI filters & sort
  scoreboardSearch: '',
  scoreboardProvider: 'all',
  scoreboardHideBaselines: false,
  scoreboardSortKey: 'elo',
  scoreboardSortDir: 'desc',

  eloHiddenModels: new Set(),
  scatterMode: 'agari_vs_dealin',
  yakuModelFilter: 'all',

  matchesSearch: '',
  matchesModelFilter: '',
  pollTimer: null,
};

let configStudio = null;

async function apiFetch(url, options = {}) {
  const method = (options.method || 'GET').toUpperCase();
  const headers = { ...(options.headers || {}) };
  if (options.body !== undefined && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }
  if (method !== 'GET' && method !== 'HEAD' && state.csrfToken) {
    headers['X-Arena-Csrf'] = state.csrfToken;
  }

  const res = await fetch(url, {
    ...options,
    method,
    headers,
    credentials: 'same-origin',
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  const contentType = res.headers.get('content-type') || '';
  const data = contentType.includes('application/json') ? await res.json().catch(() => null) : null;
  if (res.status === 401 && url.startsWith('/api/admin/') && !url.endsWith('/login')) {
    state.adminAuthenticated = false;
    state.csrfToken = '';
    updateAuthHeaderUI();
  }
  return { ok: res.ok, status: res.status, data };
}

function updateAuthHeaderUI() {
  const authBtn = document.getElementById('admin-auth-btn');
  if (authBtn) {
    authBtn.textContent = state.adminAuthenticated ? t('lockAdmin') : t('unlockAdmin');
    authBtn.className = state.adminAuthenticated ? 'btn btn-ghost btn-sm' : 'btn btn-secondary btn-sm';
  }

  const warnBanner = document.getElementById('default-password-banner');
  if (warnBanner) {
    warnBanner.classList.toggle('hidden', !state.defaultPasswordWarning);
  }

  const resetBtn = document.getElementById('reset-usage-btn');
  if (resetBtn) {
    resetBtn.classList.toggle('hidden', !state.adminAuthenticated);
  }
}

function switchTab(tabId) {
  const validTabs = ['overview', 'scoreboard', 'analytics', 'matches', 'config'];
  const target = validTabs.includes(tabId) ? tabId : 'overview';
  state.activeTab = target;

  document.querySelectorAll('.tab-btn').forEach((btn) => {
    btn.classList.toggle('active', btn.dataset.tab === target);
  });
  validTabs.forEach((id) => {
    const sec = document.getElementById(`view-${id}`);
    if (sec) sec.classList.toggle('hidden', id !== target);
  });

  if (window.location.hash !== `#${target}`) {
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}#${target}`);
  }

  if (target === 'analytics') renderAnalyticsView();
  if (target === 'scoreboard') renderScoreboardView();
  if (target === 'matches') renderMatchesView();
  if (target === 'config' && configStudio) configStudio.render();
}

async function checkAdminSession() {
  const res = await apiFetch('/api/admin/session');
  if (res.ok && res.data) {
    state.adminAuthenticated = Boolean(res.data.authenticated);
    state.csrfToken = res.data.csrfToken || '';
    state.defaultPasswordWarning = Boolean(res.data.defaultPasswordWarning);
  }
  updateAuthHeaderUI();
  if (state.adminAuthenticated) {
    await Promise.all([loadAdminStatus(), loadAdminConfig()]);
  }
}

async function loadAdminStatus() {
  if (!state.adminAuthenticated) return;
  const res = await apiFetch('/api/admin/status');
  if (res.ok && res.data) {
    state.adminStatus = res.data;
    if (Array.isArray(res.data.runs)) {
      state.runs = res.data.runs;
      syncScopeDropdown();
    }
  }
}

async function loadAdminConfig() {
  if (!state.adminAuthenticated) return;
  const res = await apiFetch('/api/admin/config');
  if (res.ok && res.data) {
    state.adminConfig = res.data;
    if (configStudio && !configStudio.dirty) {
      configStudio.loadFromServer(res.data);
    }
  }
}

async function refreshAllData(silent = false) {
  try {
    const scopeParam = encodeURIComponent(state.scope || 'current');
    const matchRunParam =
      state.scope === 'all' ? 'all' : state.scope === 'current' ? '' : state.scope;

    const matchQuery = new URLSearchParams({
      page: String(state.matchesPage.page || 1),
      pageSize: String(state.matchesPage.pageSize || 15),
    });
    if (matchRunParam) matchQuery.set('runId', matchRunParam);
    if (state.matchesModelFilter) matchQuery.set('modelId', state.matchesModelFilter);
    if (state.matchesSearch.trim()) matchQuery.set('q', state.matchesSearch.trim());

    const statusQuery =
      state.scope !== 'current' && state.scope !== 'all'
        ? `?runId=${encodeURIComponent(state.scope)}`
        : '';

    const requests = [
      state.metadata ? Promise.resolve({ ok: true, data: state.metadata }) : apiFetch('/api/arena/metadata'),
      apiFetch('/api/arena/runs'),
      apiFetch(`/api/arena/status${statusQuery}`),
      apiFetch(`/api/arena/analytics?scope=${scopeParam}`),
      apiFetch(`/api/arena/matches?${matchQuery.toString()}`),
    ];
    if (state.adminAuthenticated) {
      requests.push(apiFetch('/api/admin/status'));
    }

    const [metaRes, runsRes, statusRes, analyticsRes, matchesRes, adminStatusRes] = await Promise.all(requests);

    if (metaRes.ok && metaRes.data) state.metadata = metaRes.data;
    if (runsRes.ok && Array.isArray(runsRes.data)) state.runs = runsRes.data;
    if (statusRes.ok && statusRes.data) state.nextStatus = statusRes.data;
    if (analyticsRes.ok && analyticsRes.data) state.analytics = analyticsRes.data;
    if (matchesRes.ok && matchesRes.data) state.matchesPage = matchesRes.data;
    if (adminStatusRes && adminStatusRes.ok && adminStatusRes.data) {
      state.adminStatus = adminStatusRes.data;
    }

    syncScopeDropdown();
    syncScoreboardProviderFilter();
    syncModelFilterDropdowns();
    renderGlobalBadges();
    renderOverviewView();
    if (state.activeTab === 'scoreboard') renderScoreboardView();
    if (state.activeTab === 'analytics') renderAnalyticsView();
    if (state.activeTab === 'matches') renderMatchesView();

    schedulePolling();
  } catch (err) {
    if (!silent) showToast(t('toastRefreshFailed', { error: err.message || err }), 'error');
  }
}

function schedulePolling() {
  if (state.pollTimer) window.clearTimeout(state.pollTimer);
  const st = (state.nextStatus && state.nextStatus.status) || 'Idle';
  const fast = st === 'Running' || st === 'Pausing';
  state.pollTimer = window.setTimeout(() => {
    void refreshAllData(true);
  }, fast ? 2500 : 10000);
}

function syncScopeDropdown() {
  const sel = document.getElementById('scope-select');
  if (!sel) return;
  const prev = state.scope;
  clear(sel);
  sel.appendChild(el('option', { value: 'current', text: t('scopeCurrent') }));
  sel.appendChild(el('option', { value: 'all', text: t('scopeAll') }));
  for (const r of state.runs || []) {
    const activeTag = r.active ? ' ★' : '';
    const lbl = r.label ? `${r.label} (${r.runId})` : r.runId;
    sel.appendChild(
      el('option', {
        value: r.runId,
        text: t('scopeRunOption', {
          label: lbl,
          activeTag,
          matches: r.completedMatches,
          status: tStatus(r.status),
        }),
      })
    );
  }
  const hasPrev = prev === 'current' || prev === 'all' || (state.runs || []).some((r) => r.runId === prev);
  state.scope = hasPrev ? prev : 'current';
  sel.value = state.scope;
}

function syncScoreboardProviderFilter() {
  const sel = document.getElementById('scoreboard-provider-filter');
  if (!sel) return;
  const cur = state.scoreboardProvider || 'all';
  clear(sel);
  sel.appendChild(el('option', { value: 'all', text: t('provFilterAll') }));
  for (const provId of ['openai', 'gemini', 'anthropic', 'grok']) {
    sel.appendChild(el('option', { value: provId, text: tProviderId(provId) }));
  }
  sel.appendChild(el('option', { value: 'baseline', text: t('provFilterBaselines') }));
  sel.value = cur;
}

function syncModelFilterDropdowns() {
  const leaderboard = (state.analytics && state.analytics.leaderboard) || [];
  const yakuSel = document.getElementById('yaku-model-filter');
  if (yakuSel) {
    const cur = state.yakuModelFilter;
    clear(yakuSel);
    yakuSel.appendChild(el('option', { value: 'all', text: t('filterAllModels') }));
    for (const r of leaderboard) {
      yakuSel.appendChild(el('option', { value: r.modelId, text: r.displayName || r.modelId }));
    }
    yakuSel.value = leaderboard.some((r) => r.modelId === cur) ? cur : 'all';
    state.yakuModelFilter = yakuSel.value;
  }

  const matchSel = document.getElementById('matches-model-filter');
  if (matchSel) {
    const cur = state.matchesModelFilter;
    clear(matchSel);
    matchSel.appendChild(el('option', { value: '', text: t('filterAllModels') }));
    for (const r of leaderboard) {
      matchSel.appendChild(el('option', { value: r.modelId, text: r.displayName || r.modelId }));
    }
    matchSel.value = leaderboard.some((r) => r.modelId === cur) ? cur : '';
    state.matchesModelFilter = matchSel.value;
  }
}

function renderGlobalBadges() {
  const st = state.nextStatus || {};
  const pill = document.getElementById('global-status-pill');
  if (pill) {
    const rawStatus = st.status || 'Idle';
    pill.className = statusBadgeClass(rawStatus);
    pill.textContent = tStatus(rawStatus);
  }

  const liveCount = Array.isArray(st.activeMatches) ? st.activeMatches.length : 0;
  const liveBadge = document.getElementById('nav-badge-live');
  if (liveBadge) {
    liveBadge.textContent = t('badgeLiveCount', { count: liveCount });
    liveBadge.classList.toggle('hidden', liveCount <= 0);
  }

  const modelsBadge = document.getElementById('nav-badge-models');
  if (modelsBadge && state.analytics && Array.isArray(state.analytics.leaderboard)) {
    modelsBadge.textContent = String(state.analytics.leaderboard.length);
  }

  const matchesBadge = document.getElementById('nav-badge-matches');
  if (matchesBadge && state.analytics && state.analytics.summary) {
    matchesBadge.textContent = formatInt(state.analytics.summary.totalMatches || 0);
  }
}

function formatScheduleModeUpper(mode) {
  const m = String(mode || 'swiss').toLowerCase();
  if (m === 'swiss') return t('modeSwissUpper');
  if (m === 'balanced') return t('modeBalancedUpper');
  if (m === 'custom') return t('modeCustomUpper');
  return String(mode).toUpperCase();
}

function formatScopeSummaryName(rawScope) {
  if (!rawScope || rawScope === 'current') return t('scopeCurrent');
  if (rawScope === 'all') return t('scopeAll');
  return rawScope;
}

// =========================================================================
// TAB 1: OVERVIEW & FULL ARENA RUN CONTROL
// =========================================================================

function renderOverviewView() {
  const st = state.nextStatus || {};
  const activeRun = (state.runs || []).find((r) => r.runId === st.runId) || (state.runs || [])[0] || null;
  const statusStr = st.status || 'Idle';

  const sub = document.getElementById('hero-run-subtitle');
  if (sub) {
    if (st.runId) {
      sub.textContent = t('heroSubtitleActive', {
        runId: st.runId,
        labelPart: st.label ? ` — "${st.label}"` : '',
        mode: formatScheduleModeUpper(st.scheduleMode),
        workers: st.matchConcurrency || 1,
      });
    } else {
      sub.textContent = t('heroSubtitleEmpty');
    }
  }

  // Ruleset badges
  const rulesCluster = document.getElementById('hero-rules-badges');
  if (rulesCluster) {
    clear(rulesCluster);
    if (activeRun) {
      rulesCluster.appendChild(
        el('span', { className: 'badge', text: t('rulePlayersBadge', { count: activeRun.playerCount || 4 }) })
      );
      rulesCluster.appendChild(
        el('span', { className: 'badge', text: activeRun.totalRound === 2 ? t('ruleHanchan') : t('ruleEastOnly') })
      );
      rulesCluster.appendChild(el('span', { className: 'badge', text: tPresetId(activeRun.tileSetPreset || 'Regular') }));
      if (activeRun.hasWildcards) {
        rulesCluster.appendChild(el('span', { className: 'badge badge-wildcard', text: t('ruleWildcardsActive') }));
      }
    }
  }

  // Progress Ring
  const ringSlot = document.getElementById('hero-progress-ring');
  if (ringSlot) {
    clear(ringSlot);
    let fraction = 0;
    let mainLabel = `${st.completedMatches || 0}`;
    let subLabel = t('ringMatchesDone');
    if (st.scheduleMode === 'swiss' && st.totalSwissRounds > 0) {
      const doneRounds = Math.max(0, (st.currentSwissRound || 1) - 1);
      const roundFrac =
        st.totalTablesInRound > 0 ? (st.completedTablesInRound || 0) / st.totalTablesInRound : 0;
      fraction = statusStr === 'Finished' ? 1 : (doneRounds + roundFrac) / st.totalSwissRounds;
      mainLabel = t('ringSwissRound', {
        cur: Math.min(st.currentSwissRound || 0, st.totalSwissRounds),
        total: st.totalSwissRounds,
      });
      subLabel = t('ringMatchesCount', { count: st.completedMatches || 0 });
    } else if ((st.targetMatches || 0) > 0) {
      fraction = Math.min(1, (st.completedMatches || 0) / st.targetMatches);
      mainLabel = `${st.completedMatches || 0}/${st.targetMatches}`;
      subLabel =
        st.queuedCustomTables > 0
          ? t('ringCustomQueued', { count: st.queuedCustomTables })
          : t('ringTargetMatches');
    }
    ringSlot.appendChild(renderProgressRingSvg(fraction, mainLabel, subLabel));
  }

  // Mini KPIs
  const kpiSlot = document.getElementById('hero-kpis');
  if (kpiSlot) {
    clear(kpiSlot);
    const items = [
      { label: t('kpiRunStatus'), value: tStatus(statusStr), sub: st.runId || t('kpiReady') },
      {
        label: t('kpiCompletedMatches'),
        value: formatInt(st.completedMatches || 0),
        sub: t('kpiConcurrency', { count: st.matchConcurrency || 1 }),
      },
      {
        label: st.scheduleMode === 'swiss' ? t('kpiSwissProgress') : t('kpiTargetQueue'),
        value:
          st.scheduleMode === 'swiss'
            ? t('kpiRoundOf', { cur: st.currentSwissRound || 0, total: st.totalSwissRounds || 0 })
            : `${st.completedMatches || 0} / ${st.targetMatches || 0}`,
        sub:
          st.scheduleMode === 'swiss'
            ? t('kpiTablesInRound', { done: st.completedTablesInRound || 0, total: st.totalTablesInRound || 0 })
            : t('kpiCustomQueue', { count: st.queuedCustomTables || 0 }),
      },
      {
        label: t('kpiActiveTables'),
        value: String((st.activeMatches || []).length),
        sub: t('kpiLiveParallel'),
      },
    ];
    for (const item of items) {
      kpiSlot.appendChild(
        el('div', { className: 'kpi-box' }, [
          el('div', { className: 'kpi-label', text: item.label }),
          el('div', { className: 'kpi-value', text: item.value }),
          el('div', { className: 'kpi-sub', text: item.sub }),
        ])
      );
    }
  }

  // Error banner
  const errBanner = document.getElementById('hero-error-banner');
  if (errBanner) {
    if (st.lastError) {
      errBanner.textContent = t('lastSchedulerAlert', { error: st.lastError });
      errBanner.classList.remove('hidden');
    } else {
      errBanner.classList.add('hidden');
    }
  }

  // Full Run Action Toolbar
  const actionBar = document.getElementById('hero-action-bar');
  if (actionBar) {
    clear(actionBar);
    if (!state.adminAuthenticated) {
      actionBar.appendChild(
        el('button', {
          type: 'button',
          className: 'btn btn-primary',
          text: t('btnUnlockToControl'),
          on: { click: () => openLoginModal() },
        })
      );
    } else {
      const isRunning = statusStr === 'Running';
      const isPausing = statusStr === 'Pausing';

      actionBar.appendChild(
        el('button', {
          type: 'button',
          className: 'btn btn-primary',
          text: t('btnStartNewRun'),
          on: { click: () => openNewRunModal() },
        })
      );

      actionBar.appendChild(
        el('button', {
          type: 'button',
          className: 'btn btn-secondary',
          text: t('btnStartAppendBatch'),
          on: { click: () => openBatchModal() },
        })
      );

      if (!isRunning && !isPausing) {
        actionBar.appendChild(
          el('button', {
            type: 'button',
            className: 'btn btn-success',
            text: st.runId ? t('btnResumeRun') : t('btnQuickStartRun'),
            on: { click: () => triggerRunAction('/api/admin/run/start', t('toastRunStarted')) },
          })
        );
      }

      if (isRunning) {
        actionBar.appendChild(
          el('button', {
            type: 'button',
            className: 'btn btn-warn',
            text: t('btnPauseGraceful'),
            on: { click: () => triggerRunAction('/api/admin/run/pause', t('toastPauseRequested')) },
          })
        );
      }

      if (isRunning || isPausing) {
        actionBar.appendChild(
          el('button', {
            type: 'button',
            className: 'btn btn-danger',
            text: t('btnStopImmediate'),
            on: {
              click: async () => {
                const ok = await confirmAction({
                  title: t('confirmStopTitle'),
                  message: t('confirmStopMsg'),
                  confirmLabel: t('btnStopImmediate'),
                  danger: true,
                });
                if (ok) await triggerRunAction('/api/admin/run/stop', t('toastRunStopped'));
              },
            },
          })
        );
      }
    }
  }

  // Next Queued Match Card
  const nextSlot = document.getElementById('next-match-slot');
  if (nextSlot) {
    clear(nextSlot);
    if (st.nextMatch && Array.isArray(st.nextMatch.seats) && st.nextMatch.seats.length > 0) {
      nextSlot.appendChild(
        el('div', { className: 'badge-cluster' }, [
          el('span', {
            className: 'badge badge-success',
            text: st.nextMatch.label || t('nextRoundFallback', { round: st.nextMatch.swissRound }),
          }),
          st.nextMatch.padded ? el('span', { className: 'badge badge-warn', text: t('nextBaselinePadded') }) : null,
        ])
      );
      const seatRows = el(
        'div',
        { className: 'next-seat-list deck-suit-row' },
        st.nextMatch.seats.map((s) =>
          el('div', { className: 'next-seat-row' }, [
            el('span', { className: 'muted-text mono', text: t('seatNumber', { num: s.seat + 1 }) }),
            el('strong', { text: s.displayName || s.modelId }),
          ])
        )
      );
      nextSlot.appendChild(seatRows);
    } else {
      nextSlot.appendChild(
        el('div', {
          className: 'empty-state',
          text: statusStr === 'Finished' ? t('nextFinishedEmpty') : t('nextPendingEmpty'),
        })
      );
    }
  }

  // Live Active Tables Grid
  const activeMatches = st.activeMatches || [];
  const countBadge = document.getElementById('active-tables-count');
  if (countBadge) {
    countBadge.textContent = t('activeCountBadge', { count: activeMatches.length });
    countBadge.className = activeMatches.length > 0 ? 'badge badge-success' : 'badge';
  }

  const liveGrid = document.getElementById('active-tables-grid');
  if (liveGrid) {
    clear(liveGrid);
    if (activeMatches.length === 0) {
      liveGrid.appendChild(
        el('div', {
          className: 'empty-state',
          text: statusStr === 'Running' ? t('livePreparing') : t('liveEmpty'),
        })
      );
    } else {
      for (const m of activeMatches) {
        liveGrid.appendChild(
          el('div', { className: 'live-table-card' }, [
            el('div', { className: 'live-table-top' }, [
              el('strong', { className: 'mono', text: m.gameId }),
              el('div', { className: 'badge-cluster' }, [
                el('span', { className: 'badge badge-success', text: tRoundLabel(m.roundLabel) }),
                el('span', { className: 'badge', text: t('liveWallBadge', { count: m.wallRemaining }) }),
                el('span', { className: 'badge', text: t('liveTurnBadge', { count: m.turnCount }) }),
              ]),
            ]),
            el(
              'div',
              { className: 'live-seats-list' },
              (m.seats || []).map((s) =>
                el('div', { className: 'live-seat-chip' }, [
                  el('span', { className: 'live-seat-name', text: s.displayName || s.modelId }),
                  el('div', { className: 'badge-cluster' }, [
                    s.inRiichi ? el('span', { className: 'riichi-dot', text: t('riichiBadge') }) : null,
                    el('span', { className: 'mono tabular', text: formatInt(s.points) }),
                  ]),
                ])
              )
            ),
          ])
        );
      }
    }
  }

  renderRunsHistoryTable();
  renderUsageTelemetryTable();
}

function renderRunsHistoryTable() {
  const slot = document.getElementById('runs-history-slot');
  if (!slot) return;
  clear(slot);

  if (!state.runs || state.runs.length === 0) {
    slot.appendChild(el('div', { className: 'empty-state', text: t('runHistoryEmpty') }));
    return;
  }

  const rows = state.runs.map((r) =>
    el('tr', {}, [
      el('td', {}, [
        el('div', { className: 'badge-cluster' }, [
          el('strong', { className: 'mono', text: r.runId }),
          r.active ? el('span', { className: 'badge badge-success', text: t('badgeActive') }) : null,
        ]),
        r.label ? el('div', { className: 'muted-text', text: r.label }) : null,
      ]),
      el('td', {}, [el('span', { className: statusBadgeClass(r.status), text: tStatus(r.status) })]),
      el('td', {}, [
        el('div', { className: 'badge-cluster' }, [
          el('span', {
            className: 'badge',
            text: t('rulesetShort', { players: r.playerCount || 4, preset: tPresetId(r.tileSetPreset || 'Regular') }),
          }),
          r.hasWildcards ? el('span', { className: 'badge badge-wildcard', text: '★' }) : null,
        ]),
      ]),
      el('td', { className: 'num' }, [formatInt(r.completedMatches || 0)]),
      el('td', {}, [
        el('div', { className: 'action-toolbar' }, [
          el('button', {
            type: 'button',
            className: 'btn btn-ghost btn-xs',
            text: state.scope === r.runId ? t('btnViewing') : t('btnView'),
            disabled: state.scope === r.runId,
            on: {
              click: () => {
                state.scope = r.runId;
                state.matchesPage.page = 1;
                void refreshAllData();
              },
            },
          }),
          state.adminAuthenticated && !r.active
            ? el('button', {
                type: 'button',
                className: 'btn btn-secondary btn-xs',
                text: t('btnSetActive'),
                on: {
                  click: async () => {
                    const res = await apiFetch(`/api/admin/runs/${encodeURIComponent(r.runId)}/activate`, {
                      method: 'POST',
                    });
                    if (res.ok) {
                      showToast(t('toastRunActivated', { runId: r.runId }), 'success');
                      await refreshAllData();
                    } else {
                      showToast((res.data && res.data.error) || t('toastActivateFailed'), 'error');
                    }
                  },
                },
              })
            : null,
          state.adminAuthenticated
            ? el('button', {
                type: 'button',
                className: 'btn btn-danger btn-xs',
                text: t('btnDelete'),
                on: {
                  click: async () => {
                    const ok = await confirmAction({
                      title: t('confirmDeleteRunTitle'),
                      message: t('confirmDeleteRunMsg', { runId: r.runId }),
                      confirmLabel: t('confirmDeleteRunBtn'),
                      danger: true,
                    });
                    if (!ok) return;
                    const res = await apiFetch(`/api/admin/runs/${encodeURIComponent(r.runId)}`, {
                      method: 'DELETE',
                    });
                    if (res.ok) {
                      if (state.scope === r.runId) state.scope = 'current';
                      showToast(t('toastRunDeleted', { runId: r.runId }), 'success');
                      await refreshAllData();
                    } else {
                      showToast((res.data && res.data.error) || t('toastDeleteRunFailed'), 'error');
                    }
                  },
                },
              })
            : null,
        ]),
      ]),
    ])
  );

  slot.appendChild(
    el('table', { className: 'data-table' }, [
      el('thead', {}, [
        el('tr', {}, [
          el('th', { text: t('colRun') }),
          el('th', { text: t('colStatus') }),
          el('th', { text: t('colRuleset') }),
          el('th', { className: 'num', text: t('colMatches') }),
          el('th', { text: t('colActions') }),
        ]),
      ]),
      el('tbody', {}, rows),
    ])
  );
}

function renderUsageTelemetryTable() {
  const slot = document.getElementById('usage-telemetry-slot');
  if (!slot) return;
  clear(slot);

  if (state.adminAuthenticated && state.adminStatus && Array.isArray(state.adminStatus.models)) {
    const models = state.adminStatus.models;
    if (models.length === 0) {
      slot.appendChild(el('div', { className: 'empty-state', text: t('telemetryEmptyAdmin') }));
      return;
    }
    slot.appendChild(
      el('table', { className: 'data-table' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', { text: t('colModel') }),
            el('th', { className: 'num', text: t('colCalls') }),
            el('th', { className: 'num', text: t('colErrors') }),
            el('th', { className: 'num', text: t('colRetriesPen') }),
            el('th', { className: 'num', text: t('colTokens') }),
            el('th', { className: 'num', text: t('colAvgLatency') }),
          ]),
        ]),
        el(
          'tbody',
          {},
          models.map((m) =>
            el('tr', {}, [
              el('td', {}, [
                el('strong', { text: m.displayName || m.modelId }),
                el('div', { className: 'muted-text', text: tProviderId(m.provider) }),
              ]),
              el('td', { className: 'num', text: formatInt(m.requests) }),
              el('td', {
                className: 'num',
                title: t('errorBreakdownTitle', {
                  timeout: m.timeoutErrors,
                  net: m.networkErrors,
                  parse: m.invalidResponseErrors,
                  rate: m.rateLimitedErrors,
                  auth: m.authErrors,
                }),
                text: formatInt(m.failures),
              }),
              el('td', { className: 'num', text: `${formatInt(m.retries)} / ${formatInt(m.penalties)}` }),
              el('td', { className: 'num', text: formatCompactTokens(m.totalTokens) }),
              el('td', { className: 'num', text: m.avgLatencyMs > 0 ? t('durationMs', { n: Math.round(m.avgLatencyMs) }) : '—' }),
            ])
          )
        ),
      ])
    );
    return;
  }

  // Public fallback summary from analytics leaderboard
  const lb = (state.analytics && state.analytics.leaderboard) || [];
  const llmRows = lb.filter((r) => !r.isBaseline && r.decisions > 0);
  if (llmRows.length === 0) {
    slot.appendChild(
      el('div', {
        className: 'empty-state',
        text: t('telemetryEmptyPublic'),
      })
    );
    return;
  }

  slot.appendChild(
    el('table', { className: 'data-table' }, [
      el('thead', {}, [
        el('tr', {}, [
          el('th', { text: t('colModel') }),
          el('th', { className: 'num', text: t('colDecisions') }),
          el('th', { className: 'num', text: t('colFirstTryValid') }),
          el('th', { className: 'num', text: t('colPenalties') }),
          el('th', { className: 'num', text: t('colTokensPerGame') }),
          el('th', { className: 'num', text: t('colAvgLatency') }),
        ]),
      ]),
      el(
        'tbody',
        {},
        llmRows.map((r) =>
          el('tr', {}, [
            el('td', {}, [el('strong', { text: r.displayName || r.modelId })]),
            el('td', { className: 'num', text: formatInt(r.decisions) }),
            el('td', { className: 'num', text: formatPct(r.validFirstTryRate) }),
            el('td', { className: 'num', text: formatInt(r.penalties) }),
            el('td', { className: 'num', text: formatCompactTokens(r.tokensPerGame) }),
            el('td', { className: 'num', text: r.avgLatencyMs > 0 ? t('durationMs', { n: Math.round(r.avgLatencyMs) }) : '—' }),
          ])
        )
      ),
    ])
  );
}

async function triggerRunAction(endpoint, toastMsg) {
  const res = await apiFetch(endpoint, { method: 'POST' });
  if (res.ok) {
    showToast(toastMsg, 'success');
    state.scope = 'current';
    await refreshAllData();
  } else {
    showToast((res.data && res.data.error) || t('toastRunActionFailed'), 'error');
  }
}

// =========================================================================
// RUN & BATCH MODALS (NEW RUN + START/APPEND BATCH)
// =========================================================================

function getEnabledRosterModels() {
  if (state.adminConfig && Array.isArray(state.adminConfig.models)) {
    return state.adminConfig.models.filter((m) => m.enabled !== false);
  }
  return ((state.analytics && state.analytics.leaderboard) || []).map((r) => ({
    id: r.modelId,
    displayName: r.displayName,
    provider: r.provider,
  }));
}

function getConfiguredPlayerCount() {
  if (state.adminConfig && state.adminConfig.gameRules) {
    return Number(state.adminConfig.gameRules.playerCount || 4);
  }
  return 4;
}

function openNewRunModal() {
  const body = document.getElementById('new-run-body');
  const form = document.getElementById('new-run-form');
  if (!body || !form) return;
  clear(body);

  const cfgRun = (state.adminConfig && state.adminConfig.run) || {};
  const roster = getEnabledRosterModels();
  const playerCount = getConfiguredPlayerCount();

  const labelInput = el('input', {
    type: 'text',
    className: 'input-text',
    placeholder: t('newRunLabelPlaceholder'),
  });
  const modeSelect = el('select', { className: 'input-select' }, [
    el('option', { value: 'swiss', text: t('modeOptSwiss'), attrs: cfgRun.scheduleMode === 'swiss' ? { selected: 'selected' } : {} }),
    el('option', { value: 'balanced', text: t('modeOptBalanced'), attrs: cfgRun.scheduleMode === 'balanced' ? { selected: 'selected' } : {} }),
    el('option', { value: 'custom', text: t('modeOptCustom') }),
  ]);
  const countInput = el('input', {
    type: 'number',
    className: 'input-text tabular',
    min: 1,
    max: 500,
    value: cfgRun.scheduleMode === 'balanced' ? cfgRun.targetMatches || 8 : cfgRun.swissRounds || 4,
  });
  const concInput = el('input', {
    type: 'number',
    className: 'input-text tabular',
    min: 1,
    max: 16,
    value: cfgRun.matchConcurrency || 2,
  });

  const countLabel = el('span', {
    className: 'field-label',
    text: cfgRun.scheduleMode === 'balanced' ? t('fieldTargetMatches') : t('fieldSwissRounds'),
  });

  const customBox = el('div', { className: 'hidden' });
  const seatSelects = [];
  for (let s = 0; s < playerCount; s++) {
    const sel = el(
      'select',
      { className: 'input-select' },
      roster.map((m, idx) =>
        el('option', {
          value: m.id,
          text: `${m.displayName || m.id} (${tProviderId(m.provider)})`,
          attrs: idx % Math.max(1, roster.length) === s % Math.max(1, roster.length) ? { selected: 'selected' } : {},
        })
      )
    );
    seatSelects.push(sel);
    customBox.appendChild(
      el('label', { className: 'field-group' }, [
        el('span', { className: 'field-label', text: t('fieldCustomSeat', { num: s + 1 }) }),
        sel,
      ])
    );
  }

  modeSelect.addEventListener('change', () => {
    const mode = modeSelect.value;
    if (mode === 'swiss') {
      countLabel.textContent = t('fieldNumSwissRounds');
      countInput.value = String(cfgRun.swissRounds || 4);
      customBox.classList.add('hidden');
    } else if (mode === 'balanced') {
      countLabel.textContent = t('fieldTargetBalanced');
      countInput.value = String(cfgRun.targetMatches || 8);
      customBox.classList.add('hidden');
    } else {
      countLabel.textContent = t('fieldRepeatLineup');
      countInput.value = '2';
      customBox.classList.remove('hidden');
    }
  });

  body.appendChild(
    el('label', { className: 'field-group' }, [
      el('span', { className: 'field-label', text: t('newRunLabelField') }),
      labelInput,
    ])
  );
  body.appendChild(
    el('div', { className: 'form-grid-3' }, [
      el('label', { className: 'field-group' }, [el('span', { className: 'field-label', text: t('scheduleModeField') }), modeSelect]),
      el('label', { className: 'field-group' }, [countLabel, countInput]),
      el('label', { className: 'field-group' }, [
        el('span', { className: 'field-label', text: t('fieldParallelConc') }),
        concInput,
      ]),
    ])
  );
  body.appendChild(customBox);

  form.onsubmit = async (e) => {
    e.preventDefault();
    const mode = modeSelect.value;
    const n = Math.max(1, Number(countInput.value) || 1);
    const payload = {
      label: labelInput.value.trim(),
      scheduleMode: mode,
      matchConcurrency: Math.max(1, Math.min(16, Number(concInput.value) || 1)),
    };
    if (mode === 'swiss') payload.swissRounds = n;
    else if (mode === 'balanced') payload.targetMatches = n;
    else {
      const lineup = seatSelects.map((s) => s.value);
      payload.customTables = Array.from({ length: n }, () => [...lineup]);
    }

    const res = await apiFetch('/api/admin/run/new', { method: 'POST', body: payload });
    if (res.ok) {
      closeDialogModal('new-run-dialog');
      state.scope = 'current';
      showToast(t('toastNewRunStarted'), 'success');
      await refreshAllData();
    } else {
      showToast((res.data && res.data.error) || t('toastNewRunFailed'), 'error');
    }
  };

  openDialogModal('new-run-dialog');
}

function openBatchModal() {
  const body = document.getElementById('batch-body');
  const form = document.getElementById('batch-form');
  if (!body || !form) return;
  clear(body);

  const roster = getEnabledRosterModels();
  const playerCount = getConfiguredPlayerCount();

  const modeSelect = el('select', { className: 'input-select' }, [
    el('option', { value: 'balanced_matches', text: t('batchOptBalanced') }),
    el('option', { value: 'swiss_rounds', text: t('batchOptSwiss') }),
    el('option', { value: 'custom_lineup', text: t('batchOptCustom') }),
  ]);
  const countInput = el('input', {
    type: 'number',
    className: 'input-text tabular',
    min: 1,
    max: 500,
    value: 4,
  });
  const labelInput = el('input', {
    type: 'text',
    className: 'input-text',
    placeholder: t('batchLabelPlaceholder'),
  });

  const customSeatsWrap = el('div', { className: 'form-grid-2 hidden' });
  const seatSelects = [];
  for (let s = 0; s < playerCount; s++) {
    const sel = el(
      'select',
      { className: 'input-select' },
      roster.map((m, idx) =>
        el('option', {
          value: m.id,
          text: `${m.displayName || m.id} (${tProviderId(m.provider)})`,
          attrs: idx % Math.max(1, roster.length) === s % Math.max(1, roster.length) ? { selected: 'selected' } : {},
        })
      )
    );
    seatSelects.push(sel);
    customSeatsWrap.appendChild(
      el('label', { className: 'field-group' }, [
        el('span', { className: 'field-label', text: t('batchSeatContender', { num: s + 1 }) }),
        sel,
      ])
    );
  }

  modeSelect.addEventListener('change', () => {
    customSeatsWrap.classList.toggle('hidden', modeSelect.value !== 'custom_lineup');
  });

  body.appendChild(
    el('div', { className: 'form-grid-2' }, [
      el('label', { className: 'field-group' }, [el('span', { className: 'field-label', text: t('batchTypeField') }), modeSelect]),
      el('label', { className: 'field-group' }, [el('span', { className: 'field-label', text: t('batchCountField') }), countInput]),
    ])
  );
  body.appendChild(
    el('label', { className: 'field-group' }, [
      el('span', { className: 'field-label', text: t('batchLabelField') }),
      labelInput,
    ])
  );
  body.appendChild(customSeatsWrap);

  form.onsubmit = async (e) => {
    e.preventDefault();
    const payload = {
      mode: modeSelect.value,
      count: Math.max(1, Number(countInput.value) || 1),
      label: labelInput.value.trim(),
      seatModelIds: modeSelect.value === 'custom_lineup' ? seatSelects.map((s) => s.value) : [],
    };
    const res = await apiFetch('/api/admin/run/batch', { method: 'POST', body: payload });
    if (res.ok) {
      closeDialogModal('batch-dialog');
      state.scope = 'current';
      showToast(t('toastBatchQueued'), 'success');
      await refreshAllData();
    } else {
      const errs = res.data && res.data.errors;
      showToast((Array.isArray(errs) && errs[0]) || t('toastBatchFailed'), 'error');
    }
  };

  openDialogModal('batch-dialog');
}

// =========================================================================
// TAB 2: INTERACTIVE SCOREBOARD & MODEL DEEP-DIVE
// =========================================================================

function getScoreboardColumns() {
  return [
    { key: 'rank', label: t('sbColRank'), num: false },
    { key: 'displayName', label: t('sbColContender'), num: false },
    { key: 'elo', label: t('sbColElo'), num: true },
    { key: 'games', label: t('sbColGames'), num: true },
    { key: 'winRate', label: t('sbColWinRate'), num: true },
    { key: 'top2Rate', label: t('sbColTop2Rate'), num: true },
    { key: 'avgPlacement', label: t('sbColAvgPlace'), num: true },
    { key: 'placements', label: t('sbColPlacements'), num: false, nosort: true },
    { key: 'agariRate', label: t('sbColAgariRate'), num: true },
    { key: 'dealInRate', label: t('sbColDealInRate'), num: true },
    { key: 'riichiRate', label: t('sbColRiichiRate'), num: true },
    { key: 'openRate', label: t('sbColOpenRate'), num: true },
    { key: 'avgWinPoints', label: t('sbColAvgWinPts'), num: true },
    { key: 'wildcardWins', label: t('sbColWildWins'), num: true },
    { key: 'validFirstTryRate', label: t('sbColFirstTry'), num: true },
    { key: 'avgLatencyMs', label: t('sbColLatency'), num: true },
  ];
}

function renderScoreboardView() {
  const slot = document.getElementById('scoreboard-table-slot');
  if (!slot) return;
  clear(slot);

  const rawRows = (state.analytics && state.analytics.leaderboard) || [];
  const q = state.scoreboardSearch.trim().toLowerCase();

  const filtered = rawRows.filter((r) => {
    if (state.scoreboardHideBaselines && r.isBaseline) return false;
    if (state.scoreboardProvider !== 'all' && String(r.provider || '').toLowerCase() !== state.scoreboardProvider) {
      return false;
    }
    if (!q) return true;
    return (
      String(r.displayName || '').toLowerCase().includes(q) ||
      String(r.modelId || '').toLowerCase().includes(q) ||
      String(r.variant || '').toLowerCase().includes(q)
    );
  });

  if (filtered.length === 0) {
    slot.appendChild(el('div', { className: 'empty-state', text: t('scoreboardEmpty') }));
    return;
  }

  const key = state.scoreboardSortKey;
  const mul = state.scoreboardSortDir === 'asc' ? 1 : -1;
  const locale = getBcp47Locale();
  filtered.sort((a, b) => {
    const va = a[key];
    const vb = b[key];
    if (typeof va === 'string' || typeof vb === 'string') {
      return String(va || '').localeCompare(String(vb || ''), locale) * mul;
    }
    return ((Number(va) || 0) - (Number(vb) || 0)) * mul;
  });

  const columns = getScoreboardColumns();
  const thead = el('thead', {}, [
    el(
      'tr',
      {},
      columns.map((col) => {
        const active = !col.nosort && state.scoreboardSortKey === col.key;
        const arrow = active ? (state.scoreboardSortDir === 'asc' ? ' ▲' : ' ▼') : '';
        return el('th', {
          className: `${col.num ? 'num ' : ''}${col.nosort ? '' : 'sortable'}`.trim(),
          text: `${col.label}${arrow}`,
          on: col.nosort
            ? {}
            : {
                click: () => {
                  if (state.scoreboardSortKey === col.key) {
                    state.scoreboardSortDir = state.scoreboardSortDir === 'asc' ? 'desc' : 'asc';
                  } else {
                    state.scoreboardSortKey = col.key;
                    state.scoreboardSortDir = col.key === 'avgPlacement' || col.key === 'dealInRate' || col.key === 'rank' ? 'asc' : 'desc';
                  }
                  renderScoreboardView();
                },
              },
        });
      })
    ),
  ]);

  const tbody = el(
    'tbody',
    {},
    filtered.map((r) => {
      const rankClass = r.rank <= 3 ? `rank-medal rank-${r.rank}` : 'rank-medal';
      return el(
        'tr',
        {
          className: 'clickable-row',
          on: { click: () => openModelDeepDive(r) },
        },
        [
          el('td', {}, [el('span', { className: rankClass, text: r.rank })]),
          el('td', {}, [
            el('div', { className: 'badge-cluster' }, [
              el('strong', { text: r.displayName || r.modelId }),
              el('span', {
                className: providerBadgeClass(r.provider),
                text: r.isBaseline ? `${tProviderId('baseline')}:${r.variant}` : tProviderId(r.provider),
              }),
              r.isFrozen ? el('span', { className: 'badge badge-warn', text: t('badgeAnchor') }) : null,
            ]),
          ]),
          el('td', { className: 'num' }, [
            el('strong', { text: String(Math.round(r.elo)) }),
            r.eloCi95 > 0 ? el('span', { className: 'muted-text', text: ` ±${Math.round(r.eloCi95)}` }) : null,
          ]),
          el('td', { className: 'num', text: formatInt(r.games) }),
          el('td', { className: 'num', text: r.games > 0 ? formatPct(r.winRate) : '—' }),
          el('td', { className: 'num', text: r.games > 0 ? formatPct(r.top2Rate) : '—' }),
          el('td', { className: 'num', text: r.games > 0 ? r.avgPlacement.toFixed(2) : '—' }),
          el('td', {}, [renderPlacementMiniBarSvg(r)]),
          el('td', { className: 'num', text: r.roundsPlayed > 0 ? formatPct(r.agariRate) : '—' }),
          el('td', { className: 'num', text: r.roundsPlayed > 0 ? formatPct(r.dealInRate) : '—' }),
          el('td', { className: 'num', text: r.roundsPlayed > 0 ? formatPct(r.riichiRate) : '—' }),
          el('td', { className: 'num', text: r.roundsPlayed > 0 ? formatPct(r.openRate) : '—' }),
          el('td', { className: 'num', text: r.agariCount > 0 ? formatInt(r.avgWinPoints) : '—' }),
          el('td', { className: 'num', text: formatInt(r.wildcardWins || 0) }),
          el('td', { className: 'num', text: r.isBaseline ? formatPct(1, 0) : r.decisions > 0 ? formatPct(r.validFirstTryRate) : '—' }),
          el('td', { className: 'num', text: r.avgLatencyMs > 0 ? t('durationMs', { n: Math.round(r.avgLatencyMs) }) : '—' }),
        ]
      );
    })
  );

  slot.appendChild(el('table', { className: 'data-table' }, [thead, tbody]));
}

function openModelDeepDive(row) {
  const titleEl = document.getElementById('model-detail-title');
  const bodyEl = document.getElementById('model-detail-body');
  if (!titleEl || !bodyEl) return;

  titleEl.textContent = t('deepDiveTitle', { name: row.displayName || row.modelId });
  clear(bodyEl);

  const metrics = [
    { label: t('ddEloRating'), value: `${Math.round(row.elo)}${row.eloCi95 > 0 ? ` ±${Math.round(row.eloCi95)}` : ''}` },
    { label: t('ddGamesRounds'), value: t('ddGamesRoundsVal', { games: formatInt(row.games), rounds: formatInt(row.roundsPlayed) }) },
    { label: t('ddAvgPlacement'), value: row.games > 0 ? row.avgPlacement.toFixed(2) : '—' },
    { label: t('ddFourthAvoid'), value: row.games > 0 ? formatPct(row.fourthAvoidRate) : '—' },
    { label: t('ddAgariTsumo'), value: t('ddAgariTsumoVal', { agari: formatPct(row.agariRate), tsumo: formatPct(row.tsumoShare) }) },
    { label: t('ddDealInLoss'), value: t('ddDealInLossVal', { rate: formatPct(row.dealInRate), pts: formatInt(row.avgDealInPoints) }) },
    { label: t('ddRiichiOpen'), value: `${formatPct(row.riichiRate)} / ${formatPct(row.openRate)}` },
    {
      label: t('ddAvgWinVal'),
      value: row.agariCount > 0 ? t('ddAvgWinValFmt', { pts: formatInt(row.avgWinPoints), han: row.avgWinHan.toFixed(2) }) : '—',
    },
    { label: t('ddWildcardWins'), value: formatInt(row.wildcardWins || 0) },
    { label: t('ddKansNuki'), value: `${formatInt(row.kanCount)} / ${formatInt(row.nukiCount)}` },
    {
      label: t('ddFirstTryPen'),
      value: `${row.isBaseline ? formatPct(1, 0) : formatPct(row.validFirstTryRate)} / ${formatInt(row.penalties)}`,
    },
    { label: t('ddTokensGame'), value: row.isBaseline ? t('ddZeroLocal') : formatCompactTokens(row.tokensPerGame) },
  ];

  bodyEl.appendChild(
    el(
      'div',
      { className: 'kpi-mini-grid' },
      metrics.map((m) =>
        el('div', { className: 'kpi-box' }, [
          el('div', { className: 'kpi-label', text: m.label }),
          el('div', { className: 'kpi-value', text: m.value }),
        ])
      )
    )
  );

  // Top Yaku for this model
  const yakuList = ((state.analytics && state.analytics.yakuBreakdown) || [])
    .map((y) => ({ ...y, modelCount: (y.byModel && y.byModel[row.modelId]) || 0 }))
    .filter((y) => y.modelCount > 0)
    .sort((a, b) => b.modelCount - a.modelCount)
    .slice(0, 12);

  bodyEl.appendChild(el('h4', { className: 'card-title', text: t('ddSignatureYaku') }));
  if (yakuList.length === 0) {
    bodyEl.appendChild(el('div', { className: 'muted-text', text: t('ddNoWinsYet') }));
  } else {
    bodyEl.appendChild(
      el(
        'div',
        { className: 'badge-cluster' },
        yakuList.map((y) =>
          el('span', {
            className: y.isKoyaku ? 'badge badge-koyaku' : y.group === 'yakuman' ? 'badge badge-wildcard' : 'badge',
            text: `${tYakuName(y.name, y.displayName)}: ${formatInt(y.modelCount)}`,
          })
        )
      )
    );
  }

  // Head-to-head matchups
  const h2h = ((state.analytics && state.analytics.headToHead) || []).filter(
    (c) => c.modelA === row.modelId && c.sharedMatches > 0
  );
  const nameMap = new Map(
    ((state.analytics && state.analytics.leaderboard) || []).map((r) => [r.modelId, r.displayName || r.modelId])
  );

  bodyEl.appendChild(el('h4', { className: 'card-title', text: t('ddPairwiseH2H') }));
  if (h2h.length === 0) {
    bodyEl.appendChild(el('div', { className: 'muted-text', text: t('ddNoSharedMatches') }));
  } else {
    bodyEl.appendChild(
      el('table', { className: 'data-table' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', { text: t('ddColOpponent') }),
            el('th', { className: 'num', text: t('ddColSharedGames') }),
            el('th', { className: 'num', text: t('ddColRecord') }),
            el('th', { className: 'num', text: t('ddColFinishAhead') }),
            el('th', { className: 'num', text: t('ddColAvgPtDiff') }),
          ]),
        ]),
        el(
          'tbody',
          {},
          h2h.map((c) =>
            el('tr', {}, [
              el('td', { text: nameMap.get(c.modelB) || c.modelB }),
              el('td', { className: 'num', text: formatInt(c.sharedMatches) }),
              el('td', { className: 'num', text: `${c.aBeatB}-${c.bBeatA}-${c.ties}` }),
              el('td', { className: 'num', text: formatPct(c.winRateA) }),
              el('td', {
                className: `num ${c.avgPointDiffA >= 0 ? 'delta-pos' : 'delta-neg'}`,
                text: formatSigned(c.avgPointDiffA, 0),
              }),
            ])
          )
        ),
      ])
    );
  }

  bodyEl.appendChild(
    el('div', { className: 'action-toolbar' }, [
      el('button', {
        type: 'button',
        className: 'btn btn-secondary btn-sm',
        text: t('ddFilterMatchesBtn'),
        on: {
          click: () => {
            closeDialogModal('model-detail-dialog');
            state.matchesModelFilter = row.modelId;
            state.matchesPage.page = 1;
            switchTab('matches');
            void refreshAllData();
          },
        },
      }),
    ])
  );

  openDialogModal('model-detail-dialog');
}

// =========================================================================
// TAB 3: ANALYTICS & VISUALIZATIONS
// =========================================================================

function renderAnalyticsView() {
  const analytics = state.analytics;
  if (!analytics) return;

  const s = analytics.summary || {};
  const strip = document.getElementById('analytics-kpi-strip');
  if (strip) {
    clear(strip);
    const kpis = [
      {
        label: t('anKpiMatches'),
        value: formatInt(s.totalMatches || 0),
        sub: t('anKpiHandsSub', { count: formatInt(s.totalRounds || 0) }),
      },
      {
        label: t('anKpiWildActivity'),
        value: formatInt(s.wildcardWins || 0),
        sub: t('anKpiWildMatchesSub', { count: formatInt(s.wildcardMatches || 0) }),
      },
      {
        label: t('anKpiDraws'),
        value: t('anKpiExhaustiveVal', { count: formatInt(s.exhaustiveDraws || 0) }),
        sub: t('anKpiAbortiveSub', { count: formatInt(s.abortedDraws || 0) }),
      },
      {
        label: t('anKpiDecisions'),
        value: formatInt(s.totalDecisions || 0),
        sub: t('anKpiValidSub', { rate: formatPct(s.validFirstTryRate || 0) }),
      },
      {
        label: t('anKpiTokens'),
        value: formatCompactTokens(s.totalTokens || 0),
        sub: t('anKpiPenaltiesSub', { count: formatInt(s.totalPenalties || 0) }),
      },
      {
        label: t('anKpiDuration'),
        value: formatDuration(s.avgMatchDurationMs || 0),
        sub: t('anKpiScopeSub', { scope: formatScopeSummaryName(s.scope) }),
      },
    ];
    for (const k of kpis) {
      strip.appendChild(
        el('div', { className: 'card kpi-box' }, [
          el('div', { className: 'kpi-label', text: k.label }),
          el('div', { className: 'kpi-value', text: k.value }),
          el('div', { className: 'kpi-sub', text: k.sub }),
        ])
      );
    }
  }

  renderEloTrajectoryChart(
    document.getElementById('chart-elo-trajectory'),
    analytics.eloSeries || [],
    state.eloHiddenModels,
    (modelId) => {
      if (state.eloHiddenModels.has(modelId)) state.eloHiddenModels.delete(modelId);
      else state.eloHiddenModels.add(modelId);
      renderAnalyticsView();
    }
  );

  renderPlacementStackedChart(document.getElementById('chart-placements'), analytics.leaderboard || []);

  renderPlaystyleScatterChart(
    document.getElementById('chart-playstyle-scatter'),
    analytics.leaderboard || [],
    state.scatterMode
  );

  renderHeadToHeadMatrix(
    document.getElementById('chart-h2h-matrix'),
    analytics.leaderboard || [],
    analytics.headToHead || []
  );

  renderYakuBreakdownChart(
    document.getElementById('chart-yaku-breakdown'),
    analytics.yakuBreakdown || [],
    state.yakuModelFilter
  );
}

// =========================================================================
// TAB 4: MATCHES, 3D REPLAY LINKS & REASONING INSPECTOR
// =========================================================================

function renderMatchesView() {
  const listSlot = document.getElementById('matches-list-slot');
  const pagSlot = document.getElementById('matches-pagination-slot');
  if (!listSlot || !pagSlot) return;
  clear(listSlot);
  clear(pagSlot);

  const items = (state.matchesPage && state.matchesPage.items) || [];
  if (items.length === 0) {
    listSlot.appendChild(
      el('div', { className: 'empty-state', text: t('matchesEmpty') })
    );
    return;
  }

  for (const m of items) {
    const sortedPlayers = [...(m.players || [])].sort((a, b) => (a.placement || 9) - (b.placement || 9));
    const safeMainReplay = safeExternalHref(m.replayLink);
    const safeDownload = safeExternalHref(m.replayDownloadUrl);

    const seatButtons = (m.seatReplayLinks || []).map((rawUrl, idx) => {
      const safeSeatUrl = safeExternalHref(rawUrl);
      if (!safeSeatUrl) return null;
      return el('a', {
        href: safeSeatUrl,
        target: '_blank',
        rel: 'noopener noreferrer',
        className: 'btn btn-ghost btn-xs',
        title: t('seatPerspectiveTitle', { seat: idx + 1 }),
        text: t('seatShort', { seat: idx + 1 }),
      });
    });

    listSlot.appendChild(
      el('article', { className: 'match-card' }, [
        el('div', { className: 'match-card-header' }, [
          el('div', { className: 'match-meta-row' }, [
            el('strong', { className: 'mono', text: m.matchId }),
            m.batchLabel ? el('span', { className: 'badge', text: m.batchLabel }) : null,
            el('span', {
              className: 'badge',
              text: t('matchFormatBadge', {
                players: m.playerCount || 4,
                length: m.totalRound === 2 ? t('lengthHanchanShort') : t('lengthEastShort'),
                preset: tPresetId(m.tileSetPreset || 'Regular'),
              }),
            }),
            m.hasWildcards ? el('span', { className: 'badge badge-wildcard', text: t('badgeWildcards') }) : null,
            el('span', { className: 'badge', text: t('badgeHandsCount', { count: m.roundsPlayed || 0 }) }),
          ]),
          el('span', {
            className: 'muted-text',
            text: `${formatTimestamp(m.finishedAt)} • ${formatDuration(m.durationMs)}`,
          }),
        ]),

        el(
          'div',
          { className: 'match-seats-grid' },
          sortedPlayers.map((p) =>
            el('div', { className: `match-player-tile${p.placement === 1 ? ' winner-tile' : ''}` }, [
              el('div', { className: 'match-player-top' }, [
                el('span', {
                  className: p.placement <= 3 ? `rank-medal rank-${p.placement}` : 'rank-medal',
                  text: `#${p.placement}`,
                }),
                el('strong', { text: p.displayName || p.modelId }),
                el('span', { className: 'muted-text mono', text: t('seatShort', { seat: p.seat + 1 }) }),
              ]),
              el('div', { className: 'match-player-top tabular' }, [
                el('span', { text: t('ptsValue', { pts: formatInt(p.finalPoints) }) }),
                el('span', {
                  className: p.eloDelta >= 0 ? 'delta-pos' : 'delta-neg',
                  text: `${formatSigned(p.eloDelta, 1)} Elo`,
                }),
              ]),
              el('div', {
                className: 'field-hint',
                text: t('matchSeatStats', {
                  agari: p.agariCount || 0,
                  dealin: p.dealInCount || 0,
                  penPart: p.penaltyCount ? t('matchSeatPenPart', { pen: p.penaltyCount }) : '',
                }),
              }),
            ])
          )
        ),

        el('div', { className: 'match-actions-row' }, [
          el('div', { className: 'seat-links-group' }, [
            safeMainReplay
              ? el('a', {
                  href: safeMainReplay,
                  target: '_blank',
                  rel: 'noopener noreferrer',
                  className: 'btn btn-primary btn-sm',
                  text: t('btnWatch3dReplay'),
                })
              : null,
            seatButtons.some(Boolean) ? el('span', { className: 'muted-text', text: t('seatViewLabel') }) : null,
            ...seatButtons,
            safeDownload
              ? el('a', {
                  href: safeDownload,
                  className: 'btn btn-ghost btn-xs',
                  text: t('btnDownloadPb'),
                })
              : null,
          ]),
          el('button', {
            type: 'button',
            className: 'btn btn-secondary btn-sm',
            text: t('btnInspectMatch'),
            on: { click: () => openMatchInspector(m.matchId, m.runId) },
          }),
        ]),
      ])
    );
  }

  // Pagination
  const totalPages = Math.max(1, Math.ceil((state.matchesPage.totalCount || 0) / (state.matchesPage.pageSize || 15)));
  const curPage = state.matchesPage.page || 1;
  pagSlot.appendChild(
    el('span', {
      className: 'muted-text',
      text: t('paginationSummary', {
        cur: curPage,
        total: totalPages,
        count: formatInt(state.matchesPage.totalCount || 0),
      }),
    })
  );
  pagSlot.appendChild(
    el('div', { className: 'action-toolbar' }, [
      el('button', {
        type: 'button',
        className: 'btn btn-ghost btn-sm',
        text: t('btnPrevPage'),
        disabled: curPage <= 1,
        on: {
          click: () => {
            state.matchesPage.page = Math.max(1, curPage - 1);
            void refreshAllData();
          },
        },
      }),
      el('button', {
        type: 'button',
        className: 'btn btn-ghost btn-sm',
        text: t('btnNextPage'),
        disabled: curPage >= totalPages,
        on: {
          click: () => {
            state.matchesPage.page = Math.min(totalPages, curPage + 1);
            void refreshAllData();
          },
        },
      }),
    ])
  );
}

async function openMatchInspector(matchId, runId) {
  const titleEl = document.getElementById('match-detail-title');
  const subEl = document.getElementById('match-detail-subtitle');
  const bodyEl = document.getElementById('match-detail-body');
  if (!titleEl || !subEl || !bodyEl) return;

  titleEl.textContent = t('matchInspectorTitle', { matchId });
  subEl.textContent = t('matchInspectorLoading');
  clear(bodyEl);
  openDialogModal('match-detail-dialog');

  const query = runId ? `?runId=${encodeURIComponent(runId)}` : '';
  const res = await apiFetch(`/api/arena/matches/${encodeURIComponent(matchId)}${query}`);
  if (!res.ok || !res.data) {
    bodyEl.appendChild(el('div', { className: 'inline-alert inline-alert-danger', text: t('matchInspectorLoadFailed') }));
    return;
  }

  const d = res.data;
  subEl.textContent = t('matchInspectorSubtitle', {
    players: d.playerCount,
    preset: tPresetId(d.tileSetPreset || 'Regular'),
    wildPart: d.hasWildcards ? t('matchInspectorWildPart', { count: d.wildcardCount }) : '',
    hands: d.roundsPlayed,
    seed: d.seed,
  });

  const safeReplay = safeExternalHref(d.replayLink);
  const topToolbar = el('div', { className: 'action-toolbar' }, [
    safeReplay
      ? el('a', {
          href: safeReplay,
          target: '_blank',
          rel: 'noopener noreferrer',
          className: 'btn btn-primary btn-sm',
          text: t('btnOpen3dWeb'),
        })
      : null,
    ...(d.seatReplayLinks || []).map((u, idx) => {
      const safeU = safeExternalHref(u);
      return safeU
        ? el('a', {
            href: safeU,
            target: '_blank',
            rel: 'noopener noreferrer',
            className: 'btn btn-ghost btn-xs',
            text: t('btnSeat3dView', { seat: idx + 1 }),
          })
        : null;
    }),
  ]);
  bodyEl.appendChild(topToolbar);

  // Per-Seat Summary Table
  const playersBySeat = new Map((d.players || []).map((p) => [p.seat, p]));
  bodyEl.appendChild(el('h4', { className: 'card-title', text: t('miStandingsTitle') }));
  bodyEl.appendChild(
    el('div', { className: 'table-scroll' }, [
      el('table', { className: 'data-table' }, [
        el('thead', {}, [
          el('tr', {}, [
            el('th', { text: t('miColPlace') }),
            el('th', { text: t('miColSeat') }),
            el('th', { text: t('miColContender') }),
            el('th', { className: 'num', text: t('miColFinalPts') }),
            el('th', { className: 'num', text: t('miColEloChange') }),
            el('th', { className: 'num', text: t('miColAgariTR') }),
            el('th', { className: 'num', text: t('miColDealIn') }),
            el('th', { className: 'num', text: t('miColRiichiCalls') }),
            el('th', { className: 'num', text: t('miColWildWins') }),
            el('th', { className: 'num', text: t('miColDecPen') }),
            el('th', { className: 'num', text: t('miColTokens') }),
          ]),
        ]),
        el(
          'tbody',
          {},
          [...(d.players || [])]
            .sort((a, b) => a.placement - b.placement)
            .map((p) =>
              el('tr', {}, [
                el('td', { text: `#${p.placement}` }),
                el('td', { className: 'mono', text: t('seatNumber', { num: p.seat + 1 }) }),
                el('td', {}, [el('strong', { text: p.displayName || p.modelId })]),
                el('td', { className: 'num', text: formatInt(p.finalPoints) }),
                el('td', {
                  className: `num ${p.eloDelta >= 0 ? 'delta-pos' : 'delta-neg'}`,
                  text: `${Math.round(p.eloBefore)} → ${Math.round(p.eloAfter)} (${formatSigned(p.eloDelta, 1)})`,
                }),
                el('td', { className: 'num', text: `${p.agariCount} (${p.tsumoCount}/${p.ronCount})` }),
                el('td', { className: 'num', text: `${p.dealInCount} (${formatInt(p.dealInPoints)})` }),
                el('td', { className: 'num', text: `${p.riichiCount} / ${p.callCount}` }),
                el('td', { className: 'num', text: String(p.wildcardWinCount || 0) }),
                el('td', { className: 'num', text: `${p.decisionsCount} / ${p.penaltyCount}` }),
                el('td', {
                  className: 'num',
                  text: formatCompactTokens((p.promptTokens || 0) + (p.completionTokens || 0) + (p.reasoningTokens || 0)),
                }),
              ])
            )
        ),
      ]),
    ])
  );

  // Round-by-Round Breakdown
  if (Array.isArray(d.rounds) && d.rounds.length > 0) {
    bodyEl.appendChild(el('h4', { className: 'card-title', text: t('miHandLogTitle') }));
    bodyEl.appendChild(
      el('div', { className: 'table-scroll' }, [
        el('table', { className: 'data-table' }, [
          el('thead', {}, [
            el('tr', {}, [
              el('th', { text: t('miColHand') }),
              el('th', { text: t('miColOutcome') }),
              el('th', { text: t('miColWinnerTarget') }),
              el('th', { text: t('miColValueYaku') }),
              el('th', { text: t('miColScoreDeltas') }),
            ]),
          ]),
          el(
            'tbody',
            {},
            d.rounds.map((r) => {
              const winners = (r.winnerSeats || [])
                .map((s) => (playersBySeat.get(s) ? playersBySeat.get(s).displayName : t('seatNumber', { num: s + 1 })))
                .join(', ');
              const loser =
                r.loserSeat !== null && r.loserSeat !== undefined
                  ? playersBySeat.get(r.loserSeat)?.displayName || t('seatNumber', { num: r.loserSeat + 1 })
                  : null;
              const yakuJoined = (r.yaku || []).map((yn) => tYakuName(yn)).join(', ');
              const tenpaiSeatsText =
                (r.tenpaiSeats || []).map((s) => t('seatShort', { seat: s + 1 })).join(', ') || t('miNone');
              return el('tr', {}, [
                el('td', { className: 'mono', text: r.label ? tRoundLabel(r.label) : `#${r.roundIndex + 1}` }),
                el('td', {}, [
                  el('span', { className: 'badge', text: tOutcome(r.outcome) }),
                  r.usedWildcard ? el('span', { className: 'badge badge-wildcard', text: t('badgeWildShort') }) : null,
                ]),
                el('td', {
                  text: winners
                    ? `${winners}${loser ? ` ← ${loser}` : t('miTsumoSuffix')}`
                    : r.abortReason || t('miTenpaiList', { seats: tenpaiSeatsText }),
                }),
                el('td', {
                  text:
                    r.pointsWon > 0
                      ? r.yakuman > 0
                        ? t('miYakumanValue', { pts: formatInt(r.pointsWon), count: r.yakuman, yaku: yakuJoined })
                        : t('miHanFuValue', { pts: formatInt(r.pointsWon), han: r.han, fu: r.fu, yaku: yakuJoined })
                      : '—',
                }),
                el('td', {
                  className: 'mono muted-text',
                  text: (r.scoreDeltas || []).map((v, i) => `${t('seatShort', { seat: i + 1 })}:${formatSigned(v, 0)}`).join('  '),
                }),
              ]);
            })
          ),
        ]),
      ])
    );
  }

  // Per-Seat LLM Reasoning & Prompt Transcript Inspector
  bodyEl.appendChild(el('h4', { className: 'card-title', text: t('miTranscriptTitle') }));
  const seatTabsBar = el('div', { className: 'studio-subnav' });
  const transcriptContainer = el('div', {});
  bodyEl.appendChild(seatTabsBar);
  bodyEl.appendChild(transcriptContainer);

  const loadSeatTranscript = async (seat) => {
    seatTabsBar.querySelectorAll('button').forEach((b) => {
      b.classList.toggle('active', Number(b.dataset.seat) === seat);
    });
    clear(transcriptContainer);
    transcriptContainer.appendChild(el('div', { className: 'muted-text', text: t('miLoadingSeat', { seat: seat + 1 }) }));

    const rRes = await apiFetch(
      `/api/arena/matches/${encodeURIComponent(d.matchId)}/reasoning/${seat}${query}`
    );
    clear(transcriptContainer);

    if (rRes.status === 401) {
      transcriptContainer.appendChild(
        el('div', { className: 'empty-state' }, [
          el('p', { text: t('miAuthRequiredMsg') }),
          el('button', { type: 'button', className: 'btn btn-primary btn-sm', text: t('unlockAdmin'), on: { click: () => openLoginModal() } }),
        ])
      );
      return;
    }

    if (!rRes.ok || !rRes.data) {
      transcriptContainer.appendChild(
        el('div', {
          className: 'empty-state',
          text: t('miNoReasoningMsg'),
        })
      );
      return;
    }

    const { meta, turns } = rRes.data;
    if (meta && meta.systemPrompt) {
      transcriptContainer.appendChild(
        el('details', { className: 'turn-card' }, [
          el('summary', {}, [
            el('strong', {
              text: t('miSystemPromptSummary', {
                name: meta.displayName || meta.modelId,
                provider: meta.provider,
                model: meta.model,
                thinking: meta.thinkingLevel,
              }),
            }),
          ]),
          el('pre', { className: 'pre-block', text: meta.systemPrompt }),
        ])
      );
    }

    if (!Array.isArray(turns) || turns.length === 0) {
      transcriptContainer.appendChild(el('div', { className: 'empty-state', text: t('miZeroTurnsMsg') }));
      return;
    }

    for (const turn of turns) {
      transcriptContainer.appendChild(
        el('div', { className: `turn-card${turn.penalized ? ' penalized-turn' : ''}` }, [
          el('div', { className: 'turn-header' }, [
            el('div', { className: 'badge-cluster' }, [
              el('strong', { className: 'mono', text: t('miTurnNum', { seq: turn.turnSeq }) }),
              turn.roundLabel ? el('span', { className: 'badge', text: tRoundLabel(turn.roundLabel) }) : null,
              turn.parsedAction ? el('span', { className: 'badge badge-success', text: turn.parsedAction }) : null,
              turn.penalized
                ? el('span', { className: 'badge badge-danger', text: t('miFallbackPenalty', { tries: turn.attempts }) })
                : el('span', { className: 'badge', text: t('miTriesCount', { tries: turn.attempts }) }),
            ]),
            el('span', {
              className: 'muted-text mono',
              text: `${t('durationMs', { n: turn.latencyMs })} • in:${turn.promptTokens} out:${turn.completionTokens}${turn.reasoningTokens ? ` think:${turn.reasoningTokens}` : ''}`,
            }),
          ]),
          turn.rationale ? el('blockquote', { className: 'rationale-quote', text: turn.rationale }) : null,
          turn.error ? el('div', { className: 'inline-alert inline-alert-danger', text: t('miErrorPrefix', { error: turn.error }) }) : null,
          turn.thinkingText
            ? el('details', {}, [
                el('summary', { className: 'muted-text', text: t('miInternalThought') }),
                el('pre', { className: 'pre-block', text: turn.thinkingText }),
              ])
            : null,
          turn.promptDelta
            ? el('details', {}, [
                el('summary', { className: 'muted-text', text: t('miTurnPrompt') }),
                el('pre', { className: 'pre-block', text: turn.promptDelta }),
              ])
            : null,
          turn.rawResponse
            ? el('details', {}, [
                el('summary', { className: 'muted-text', text: t('miRawCompletion') }),
                el('pre', { className: 'pre-block', text: turn.rawResponse }),
              ])
            : null,
        ])
      );
    }
  };

  const orderedPlayers = [...(d.players || [])].sort((a, b) => a.seat - b.seat);
  orderedPlayers.forEach((p) => {
    const hasReasoning = (d.reasoningSeats || []).includes(p.seat);
    seatTabsBar.appendChild(
      el('button', {
        type: 'button',
        className: 'subnav-pill',
        dataset: { seat: p.seat },
        text: t('miSeatTabLabel', {
          seat: p.seat + 1,
          name: p.displayName || p.modelId,
          suffix: hasReasoning ? ' 💬' : t('miBaselineSuffix'),
        }),
        on: { click: () => loadSeatTranscript(p.seat) },
      })
    );
  });

  const firstSeat =
    (d.reasoningSeats && d.reasoningSeats.length > 0 ? d.reasoningSeats[0] : orderedPlayers[0]?.seat) ?? 0;
  await loadSeatTranscript(firstSeat);
}

// =========================================================================
// ADMIN AUTH LOGIN / LOGOUT
// =========================================================================

function openLoginModal() {
  const errBox = document.getElementById('login-error-box');
  const pwdInput = document.getElementById('login-password-input');
  if (errBox) errBox.classList.add('hidden');
  if (pwdInput) pwdInput.value = '';
  openDialogModal('login-dialog');
  if (pwdInput) pwdInput.focus();
}

function wireEvents() {
  // Language selector
  const langSel = document.getElementById('lang-select');
  if (langSel) {
    langSel.value = getLang();
    langSel.addEventListener('change', () => {
      setLang(langSel.value, { syncUrl: true });
    });
  }

  onLangChange((nextLang) => {
    if (langSel && langSel.value !== nextLang) {
      langSel.value = nextLang;
    }
    updateAuthHeaderUI();
    syncScopeDropdown();
    syncScoreboardProviderFilter();
    syncModelFilterDropdowns();
    renderGlobalBadges();
    renderOverviewView();
    if (state.activeTab === 'scoreboard') renderScoreboardView();
    if (state.activeTab === 'analytics') renderAnalyticsView();
    if (state.activeTab === 'matches') renderMatchesView();
    if (configStudio) configStudio.render();
  });

  // Navigation tabs
  document.querySelectorAll('.tab-btn').forEach((btn) => {
    btn.addEventListener('click', () => switchTab(btn.dataset.tab));
  });

  // Scope selector
  const scopeSel = document.getElementById('scope-select');
  if (scopeSel) {
    scopeSel.addEventListener('change', () => {
      state.scope = scopeSel.value;
      state.matchesPage.page = 1;
      void refreshAllData();
    });
  }

  // Refresh button
  const refreshBtn = document.getElementById('refresh-now-btn');
  if (refreshBtn) {
    refreshBtn.addEventListener('click', () => void refreshAllData(false));
  }

  // Default password banner shortcut
  const jumpSecBtn = document.getElementById('jump-security-btn');
  if (jumpSecBtn) {
    jumpSecBtn.addEventListener('click', () => {
      switchTab('config');
      if (!state.adminAuthenticated) openLoginModal();
      else if (configStudio) configStudio.focusSection('system');
    });
  }

  // Admin Unlock / Lock button
  const authBtn = document.getElementById('admin-auth-btn');
  if (authBtn) {
    authBtn.addEventListener('click', async () => {
      if (state.adminAuthenticated) {
        await apiFetch('/api/admin/logout', { method: 'POST' });
        state.adminAuthenticated = false;
        state.csrfToken = '';
        state.adminStatus = null;
        updateAuthHeaderUI();
        if (configStudio) configStudio.render();
        renderOverviewView();
        showToast(t('toastAdminLocked'), 'info');
      } else {
        openLoginModal();
      }
    });
  }

  // Login form
  const loginForm = document.getElementById('login-form');
  const loginCancel = document.getElementById('login-cancel-btn');
  const loginDismiss = document.getElementById('login-dismiss-btn');
  if (loginCancel) loginCancel.addEventListener('click', () => closeDialogModal('login-dialog'));
  if (loginDismiss) loginDismiss.addEventListener('click', () => closeDialogModal('login-dialog'));
  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const pwdInput = document.getElementById('login-password-input');
      const errBox = document.getElementById('login-error-box');
      const res = await apiFetch('/api/admin/login', {
        method: 'POST',
        body: { password: pwdInput ? pwdInput.value : '' },
      });
      if (!res.ok || !res.data || !res.data.authenticated) {
        if (errBox) {
          errBox.textContent = (res.data && res.data.error) || t('loginInvalidPwd');
          errBox.classList.remove('hidden');
        }
        return;
      }
      state.adminAuthenticated = true;
      state.csrfToken = res.data.csrfToken || '';
      state.defaultPasswordWarning = Boolean(res.data.defaultPasswordWarning);
      if (pwdInput) pwdInput.value = '';
      closeDialogModal('login-dialog');
      updateAuthHeaderUI();
      await Promise.all([loadAdminStatus(), loadAdminConfig()]);
      if (configStudio) configStudio.render();
      renderOverviewView();
      showToast(t('toastAdminUnlocked'), 'success');
    });
  }

  // Dialog close buttons
  const closeBindings = [
    ['new-run-close-btn', 'new-run-dialog'],
    ['new-run-cancel-btn', 'new-run-dialog'],
    ['batch-close-btn', 'batch-dialog'],
    ['batch-cancel-btn', 'batch-dialog'],
    ['model-detail-close-btn', 'model-detail-dialog'],
    ['match-detail-close-btn', 'match-detail-dialog'],
  ];
  for (const [btnId, dlgId] of closeBindings) {
    const b = document.getElementById(btnId);
    if (b) b.addEventListener('click', () => closeDialogModal(dlgId));
  }

  // Reset usage counters
  const resetUsageBtn = document.getElementById('reset-usage-btn');
  if (resetUsageBtn) {
    resetUsageBtn.addEventListener('click', async () => {
      const ok = await confirmAction({
        title: t('confirmResetUsageTitle'),
        message: t('confirmResetUsageMsg'),
        confirmLabel: t('btnResetCounters'),
      });
      if (!ok) return;
      const res = await apiFetch('/api/admin/usage/reset', { method: 'POST' });
      if (res.ok && res.data) {
        state.adminStatus = res.data;
        renderUsageTelemetryTable();
        showToast(t('toastUsageReset'), 'success');
      }
    });
  }

  // Scoreboard filters
  const sbSearch = document.getElementById('scoreboard-search');
  if (sbSearch) {
    sbSearch.addEventListener('input', () => {
      state.scoreboardSearch = sbSearch.value;
      renderScoreboardView();
    });
  }
  const sbProv = document.getElementById('scoreboard-provider-filter');
  if (sbProv) {
    sbProv.addEventListener('change', () => {
      state.scoreboardProvider = sbProv.value;
      renderScoreboardView();
    });
  }
  const sbHideBase = document.getElementById('scoreboard-hide-baselines');
  if (sbHideBase) {
    sbHideBase.addEventListener('change', () => {
      state.scoreboardHideBaselines = sbHideBase.checked;
      renderScoreboardView();
    });
  }

  // Analytics selectors
  const scatterSel = document.getElementById('scatter-mode-select');
  if (scatterSel) {
    scatterSel.addEventListener('change', () => {
      state.scatterMode = scatterSel.value;
      renderAnalyticsView();
    });
  }
  const yakuModelSel = document.getElementById('yaku-model-filter');
  if (yakuModelSel) {
    yakuModelSel.addEventListener('change', () => {
      state.yakuModelFilter = yakuModelSel.value;
      renderAnalyticsView();
    });
  }

  // Matches search & filter
  let matchSearchDebounce = null;
  const mSearch = document.getElementById('matches-search');
  if (mSearch) {
    mSearch.addEventListener('input', () => {
      window.clearTimeout(matchSearchDebounce);
      matchSearchDebounce = window.setTimeout(() => {
        state.matchesSearch = mSearch.value;
        state.matchesPage.page = 1;
        void refreshAllData();
      }, 260);
    });
  }
  const mModelSel = document.getElementById('matches-model-filter');
  if (mModelSel) {
    mModelSel.addEventListener('change', () => {
      state.matchesModelFilter = mModelSel.value;
      state.matchesPage.page = 1;
      void refreshAllData();
    });
  }
}

async function init() {
  applyStaticTranslations();
  syncScoreboardProviderFilter();

  configStudio = new ConfigStudioController({
    rootEl: document.getElementById('config-studio-root'),
    getMetadata: () => state.metadata,
    isAuthenticated: () => state.adminAuthenticated,
    onRequestLogin: () => openLoginModal(),
    apiSaveConfig: async (draft) => {
      const res = await apiFetch('/api/admin/config', { method: 'PUT', body: draft });
      return {
        ok: res.ok && res.data && res.data.saved,
        errors: (res.data && res.data.errors) || [],
      };
    },
    apiTestModel: async (modelDraft) => {
      const res = await apiFetch('/api/admin/models/test', { method: 'POST', body: modelDraft });
      if (!res.data) return { ok: false, message: `HTTP ${res.status}` };
      return res.data;
    },
    onDirtyChange: (dirty) => {
      const badge = document.getElementById('nav-badge-dirty');
      if (badge) badge.classList.toggle('hidden', !dirty);
    },
    onSaved: async () => {
      await checkAdminSession();
      await refreshAllData();
    },
  });

  wireEvents();

  const initialHash = (window.location.hash || '').replace(/^#/, '');
  await refreshAllData(true);
  await checkAdminSession();
  if (initialHash) switchTab(initialHash);
}

void init();
