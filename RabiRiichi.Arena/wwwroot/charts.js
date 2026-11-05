// Pure SVG visualizations for RabiRiichi Arena.
// All layout, geometry, colors, and interactive callouts use native SVG elements
// and attributes (zero inline CSS styles for strict CSP compliance).

import {
  clear,
  el,
  svgEl,
  formatInt,
  formatPct,
  formatSigned,
  providerColorHex,
} from './dom.js';
import { t, tYakuName } from './i18n/index.js';

const PLACE_COLORS = ['#fbbf24', '#60a5fa', '#34d399', '#f87171'];

export function renderProgressRingSvg(fraction, mainLabel, subLabel) {
  const clamped = Math.max(0, Math.min(1, Number.isFinite(fraction) ? fraction : 0));
  const size = 124;
  const cx = 62;
  const cy = 62;
  const r = 50;
  const circumference = 2 * Math.PI * r;
  const dashOffset = circumference * (1 - clamped);

  return svgEl('svg', { viewBox: `0 0 ${size} ${size}`, width: size, height: size, 'aria-label': mainLabel }, [
    svgEl('circle', {
      cx,
      cy,
      r,
      fill: 'none',
      stroke: '#1e293b',
      'stroke-width': 10,
    }),
    svgEl('circle', {
      cx,
      cy,
      r,
      fill: 'none',
      stroke: '#f472b6',
      'stroke-width': 10,
      'stroke-linecap': 'round',
      'stroke-dasharray': circumference.toFixed(2),
      'stroke-dashoffset': dashOffset.toFixed(2),
      transform: `rotate(-90 ${cx} ${cy})`,
    }),
    svgEl(
      'text',
      {
        x: cx,
        y: cy - 3,
        'text-anchor': 'middle',
        fill: '#f8fafc',
        'font-size': 18,
        'font-weight': 700,
      },
      [mainLabel]
    ),
    svgEl(
      'text',
      {
        x: cx,
        y: cy + 16,
        'text-anchor': 'middle',
        fill: '#94a3b8',
        'font-size': 10.5,
      },
      [subLabel || '']
    ),
  ]);
}

export function renderPlacementMiniBarSvg(row) {
  const counts = [row.place1 || 0, row.place2 || 0, row.place3 || 0, row.place4 || 0];
  const total = counts.reduce((a, b) => a + b, 0);
  const w = 112;
  const h = 12;
  const svg = svgEl('svg', {
    viewBox: `0 0 ${w} ${h}`,
    width: w,
    height: h,
    'aria-label': t('chartPlaceAria', { p1: counts[0], p2: counts[1], p3: counts[2], p4: counts[3] }),
  });

  svg.appendChild(svgEl('rect', { x: 0, y: 0, width: w, height: h, rx: 4, fill: '#1e293b' }));
  if (total <= 0) return svg;

  let cursor = 0;
  for (let i = 0; i < 4; i++) {
    if (counts[i] <= 0) continue;
    const segW = (counts[i] / total) * w;
    svg.appendChild(
      svgEl('rect', {
        x: cursor.toFixed(2),
        y: 0,
        width: Math.max(1.5, segW).toFixed(2),
        height: h,
        fill: PLACE_COLORS[i],
      })
    );
    cursor += segW;
  }
  return svg;
}

export function renderEloTrajectoryChart(container, eloSeries, hiddenIds, onToggleModel) {
  clear(container);
  const validSeries = (eloSeries || []).filter((s) => Array.isArray(s.points) && s.points.length > 0);
  if (validSeries.length === 0) {
    container.appendChild(el('div', { className: 'empty-state', text: t('chartEloEmpty') }));
    return;
  }

  const legend = el('div', { className: 'chart-legend' });
  validSeries.forEach((s, idx) => {
    const color = providerColorHex(s.provider, idx + 1);
    const isHidden = hiddenIds.has(s.modelId);
    const dot = svgEl('svg', { width: 10, height: 10, viewBox: '0 0 10 10' }, [
      svgEl('circle', { cx: 5, cy: 5, r: 4.5, fill: color }),
    ]);
    const chip = el(
      'button',
      {
        type: 'button',
        className: `legend-chip${isHidden ? ' muted' : ''}`,
        on: { click: () => onToggleModel(s.modelId) },
      },
      [dot, el('span', { text: s.displayName || s.modelId })]
    );
    legend.appendChild(chip);
  });

  const visibleSeries = validSeries.filter((s) => !hiddenIds.has(s.modelId));
  if (visibleSeries.length === 0) {
    container.appendChild(legend);
    container.appendChild(el('div', { className: 'empty-state', text: t('chartEloAllHidden') }));
    return;
  }

  const W = 860;
  const H = 290;
  const padL = 56;
  const padR = 26;
  const padT = 22;
  const padB = 36;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;

  let maxSeq = 1;
  let minElo = 1450;
  let maxElo = 1550;
  for (const s of visibleSeries) {
    for (const pt of s.points) {
      if (pt.matchSeq > maxSeq) maxSeq = pt.matchSeq;
      if (pt.elo < minElo) minElo = pt.elo;
      if (pt.elo > maxElo) maxElo = pt.elo;
    }
  }
  const margin = Math.max(20, (maxElo - minElo) * 0.12);
  minElo = Math.floor((minElo - margin) / 10) * 10;
  maxElo = Math.ceil((maxElo + margin) / 10) * 10;
  if (maxElo <= minElo) maxElo = minElo + 100;

  const xOf = (seq) => padL + (maxSeq <= 0 ? 0 : (seq / maxSeq) * plotW);
  const yOf = (elo) => padT + (1 - (elo - minElo) / (maxElo - minElo)) * plotH;

  const svg = svgEl('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}` });

  // Horizontal grid lines
  const yTicks = 5;
  for (let i = 0; i <= yTicks; i++) {
    const val = minElo + ((maxElo - minElo) * i) / yTicks;
    const y = yOf(val);
    svg.appendChild(
      svgEl('line', {
        x1: padL,
        y1: y.toFixed(1),
        x2: W - padR,
        y2: y.toFixed(1),
        stroke: 'rgba(148,163,184,0.14)',
        'stroke-width': 1,
      })
    );
    svg.appendChild(
      svgEl(
        'text',
        {
          x: padL - 8,
          y: (y + 4).toFixed(1),
          'text-anchor': 'end',
          fill: '#94a3b8',
          'font-size': 11,
        },
        [Math.round(val)]
      )
    );
  }

  // X axis labels
  const xStep = Math.max(1, Math.ceil(maxSeq / 8));
  for (let seq = 0; seq <= maxSeq; seq += xStep) {
    const x = xOf(seq);
    svg.appendChild(
      svgEl(
        'text',
        {
          x: x.toFixed(1),
          y: H - 10,
          'text-anchor': 'middle',
          fill: '#94a3b8',
          'font-size': 11,
        },
        [`#${seq}`]
      )
    );
  }

  const hoverGroup = svgEl('g', { visibility: 'hidden' });
  const hoverLine = svgEl('line', {
    x1: padL,
    y1: padT,
    x2: padL,
    y2: padT + plotH,
    stroke: 'rgba(248,250,252,0.35)',
    'stroke-dasharray': '4 3',
  });
  const calloutBg = svgEl('rect', {
    x: 0,
    y: 0,
    width: 215,
    height: 44,
    rx: 6,
    fill: '#0f172a',
    stroke: '#475569',
  });
  const calloutTitle = svgEl('text', { x: 10, y: 18, fill: '#f8fafc', 'font-size': 11.5, 'font-weight': 700 });
  const calloutSub = svgEl('text', { x: 10, y: 34, fill: '#cbd5e1', 'font-size': 11 });
  const calloutCard = svgEl('g', {}, [calloutBg, calloutTitle, calloutSub]);
  hoverGroup.appendChild(hoverLine);
  hoverGroup.appendChild(calloutCard);

  validSeries.forEach((s, idx) => {
    if (hiddenIds.has(s.modelId)) return;
    const color = providerColorHex(s.provider, idx + 1);
    const d = s.points
      .map((pt, j) => `${j === 0 ? 'M' : 'L'}${xOf(pt.matchSeq).toFixed(1)},${yOf(pt.elo).toFixed(1)}`)
      .join(' ');
    svg.appendChild(
      svgEl('path', {
        d,
        fill: 'none',
        stroke: color,
        'stroke-width': s.isBaseline ? 1.8 : 2.5,
        'stroke-dasharray': s.isBaseline ? '5 3' : undefined,
      })
    );
    for (const pt of s.points) {
      const cx = xOf(pt.matchSeq);
      const cy = yOf(pt.elo);
      const circle = svgEl('circle', {
        cx: cx.toFixed(1),
        cy: cy.toFixed(1),
        r: 3.6,
        fill: color,
        stroke: '#090d16',
        'stroke-width': 1.2,
      });
      circle.addEventListener('mouseenter', () => {
        hoverLine.setAttribute('x1', cx.toFixed(1));
        hoverLine.setAttribute('x2', cx.toFixed(1));
        calloutTitle.textContent = `${s.displayName || s.modelId}`;
        const placePart = pt.placement ? t('chartEloHoverPlace', { place: pt.placement }) : '';
        calloutSub.textContent = t('chartEloHover', {
          seq: pt.matchSeq,
          elo: Math.round(pt.elo),
          placePart,
        });
        const boxX = Math.min(W - 225, Math.max(padL + 6, cx + 10));
        const boxY = Math.max(padT + 4, cy - 26);
        calloutCard.setAttribute('transform', `translate(${boxX.toFixed(1)}, ${boxY.toFixed(1)})`);
        hoverGroup.setAttribute('visibility', 'visible');
      });
      circle.addEventListener('mouseleave', () => {
        hoverGroup.setAttribute('visibility', 'hidden');
      });
      svg.appendChild(circle);
    }
  });

  svg.appendChild(hoverGroup);
  container.appendChild(legend);
  container.appendChild(svg);
}

export function renderPlacementStackedChart(container, leaderboard) {
  clear(container);
  const rows = (leaderboard || []).filter((r) => r.games > 0);
  if (rows.length === 0) {
    container.appendChild(el('div', { className: 'empty-state', text: t('chartPlaceEmpty') }));
    return;
  }

  const rowH = 30;
  const padL = 186;
  const padR = 54;
  const padT = 24;
  const padB = 18;
  const W = 560;
  const H = padT + rows.length * rowH + padB;
  const barW = W - padL - padR;

  const placeLabels = [t('place1st'), t('place2nd'), t('place3rd'), t('place4th')];
  const legend = el('div', { className: 'chart-legend' }, [
    ...placeLabels.map((lbl, i) =>
      el('span', { className: 'badge' }, [
        svgEl('svg', { width: 9, height: 9, viewBox: '0 0 10 10' }, [
          svgEl('rect', { x: 0, y: 0, width: 10, height: 10, rx: 2, fill: PLACE_COLORS[i] }),
        ]),
        lbl,
      ])
    ),
  ]);

  const svg = svgEl('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}` });
  rows.forEach((r, idx) => {
    const y = padT + idx * rowH;
    const label = (r.displayName || r.modelId).slice(0, 26);
    svg.appendChild(
      svgEl('text', { x: padL - 8, y: y + 14, 'text-anchor': 'end', fill: '#e2e8f0', 'font-size': 11.5 }, [label])
    );

    const counts = [r.place1 || 0, r.place2 || 0, r.place3 || 0, r.place4 || 0];
    const total = Math.max(1, counts.reduce((a, b) => a + b, 0));
    let x = padL;
    for (let p = 0; p < 4; p++) {
      if (counts[p] <= 0) continue;
      const segW = (counts[p] / total) * barW;
      svg.appendChild(
        svgEl('rect', {
          x: x.toFixed(1),
          y: y + 3,
          width: segW.toFixed(1),
          height: 16,
          fill: PLACE_COLORS[p],
        }, [svgEl('title', {}, [`${r.displayName || r.modelId}: ${placeLabels[p]} = ${formatInt(counts[p])} (${formatPct(counts[p] / total)})`])])
      );
      if (segW >= 34) {
        svg.appendChild(
          svgEl(
            'text',
            {
              x: (x + segW / 2).toFixed(1),
              y: y + 15,
              'text-anchor': 'middle',
              fill: '#090d16',
              'font-size': 10,
              'font-weight': 700,
            },
            [`${Math.round((counts[p] / total) * 100)}%`]
          )
        );
      }
      x += segW;
    }

    svg.appendChild(
      svgEl('text', { x: W - padR + 8, y: y + 15, fill: '#94a3b8', 'font-size': 11 }, [t('chartGamesShort', { count: formatInt(r.games) })])
    );
  });

  container.appendChild(legend);
  container.appendChild(svg);
}

export function renderPlaystyleScatterChart(container, leaderboard, mode = 'agari_vs_dealin') {
  clear(container);
  const rows = (leaderboard || []).filter((r) => r.games > 0);
  if (rows.length === 0) {
    container.appendChild(el('div', { className: 'empty-state', text: t('chartScatterEmpty') }));
    return;
  }

  const axisSpec = {
    agari_vs_dealin: {
      xLabel: t('axisDealInSafe'),
      yLabel: t('axisAgariRate'),
      getX: (r) => (r.dealInRate || 0) * 100,
      getY: (r) => (r.agariRate || 0) * 100,
      fmtX: (v) => formatPct(v / 100, 1),
      fmtY: (v) => formatPct(v / 100, 1),
    },
    riichi_vs_open: {
      xLabel: t('axisOpenMeld'),
      yLabel: t('axisRiichiRate'),
      getX: (r) => (r.openRate || 0) * 100,
      getY: (r) => (r.riichiRate || 0) * 100,
      fmtX: (v) => formatPct(v / 100, 1),
      fmtY: (v) => formatPct(v / 100, 1),
    },
    elo_vs_latency: {
      xLabel: t('axisAvgLatency'),
      yLabel: t('axisEloRating'),
      getX: (r) => r.avgLatencyMs || 0,
      getY: (r) => r.elo || 1500,
      fmtX: (v) => t('durationMs', { n: Math.round(v) }),
      fmtY: (v) => formatInt(v),
    },
    elo_vs_tokens: {
      xLabel: t('axisTokensDecision'),
      yLabel: t('axisEloRating'),
      getX: (r) => r.tokensPerDecision || 0,
      getY: (r) => r.elo || 1500,
      fmtX: (v) => formatInt(v),
      fmtY: (v) => formatInt(v),
    },
  }[mode] || {
    xLabel: t('axisDealInSafe'),
    yLabel: t('axisAgariRate'),
    getX: (r) => (r.dealInRate || 0) * 100,
    getY: (r) => (r.agariRate || 0) * 100,
    fmtX: (v) => formatPct(v / 100, 1),
    fmtY: (v) => formatPct(v / 100, 1),
  };

  const W = 540;
  const H = 280;
  const padL = 56;
  const padR = 26;
  const padT = 22;
  const padB = 40;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;

  const xs = rows.map(axisSpec.getX);
  const ys = rows.map(axisSpec.getY);
  let minX = Math.min(...xs);
  let maxX = Math.max(...xs);
  let minY = Math.min(...ys);
  let maxY = Math.max(...ys);
  const spanX = Math.max(4, (maxX - minX) * 0.2 || 10);
  const spanY = Math.max(4, (maxY - minY) * 0.2 || 10);
  minX = Math.max(0, minX - spanX);
  maxX += spanX;
  minY = mode.startsWith('elo') ? minY - spanY : Math.max(0, minY - spanY);
  maxY += spanY;

  const xOf = (v) => padL + ((v - minX) / Math.max(1e-6, maxX - minX)) * plotW;
  const yOf = (v) => padT + (1 - (v - minY) / Math.max(1e-6, maxY - minY)) * plotH;

  const svg = svgEl('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}` });

  // Grid & midpoint crosshairs
  for (let i = 0; i <= 4; i++) {
    const gx = padL + (plotW * i) / 4;
    const gy = padT + (plotH * i) / 4;
    svg.appendChild(svgEl('line', { x1: gx, y1: padT, x2: gx, y2: padT + plotH, stroke: 'rgba(148,163,184,0.12)' }));
    svg.appendChild(svgEl('line', { x1: padL, y1: gy, x2: padL + plotW, y2: gy, stroke: 'rgba(148,163,184,0.12)' }));
    const vx = minX + ((maxX - minX) * i) / 4;
    const vy = maxY - ((maxY - minY) * i) / 4;
    svg.appendChild(
      svgEl('text', { x: gx, y: H - 20, 'text-anchor': 'middle', fill: '#94a3b8', 'font-size': 10 }, [axisSpec.fmtX(vx)])
    );
    svg.appendChild(
      svgEl('text', { x: padL - 7, y: gy + 3, 'text-anchor': 'end', fill: '#94a3b8', 'font-size': 10 }, [axisSpec.fmtY(vy)])
    );
  }

  svg.appendChild(
    svgEl('text', { x: padL + plotW / 2, y: H - 4, 'text-anchor': 'middle', fill: '#cbd5e1', 'font-size': 11 }, [axisSpec.xLabel])
  );
  svg.appendChild(
    svgEl(
      'text',
      {
        x: 14,
        y: padT + plotH / 2,
        'text-anchor': 'middle',
        fill: '#cbd5e1',
        'font-size': 11,
        transform: `rotate(-90 14 ${padT + plotH / 2})`,
      },
      [axisSpec.yLabel]
    )
  );

  const placedLabels = [];
  rows.forEach((r, idx) => {
    const cx = xOf(axisSpec.getX(r));
    const cy = yOf(axisSpec.getY(r));
    const color = providerColorHex(r.provider, idx + 1);
    const alignLeft = cx > padL + plotW * 0.62;
    let collideCount = 0;
    for (const prev of placedLabels) {
      if (Math.abs(prev.cx - cx) < 42 && Math.abs(prev.cy - cy) < 15) {
        collideCount++;
      }
    }
    placedLabels.push({ cx, cy });
    const dyOffsets = [4, -10, 16, -22, 28];
    const dy = dyOffsets[collideCount % dyOffsets.length];
    const g = svgEl('g', {}, [
      svgEl('circle', {
        cx: cx.toFixed(1),
        cy: cy.toFixed(1),
        r: 6.5,
        fill: color,
        'fill-opacity': 0.86,
        stroke: '#090d16',
        'stroke-width': 1.5,
      }),
      svgEl(
        'text',
        {
          x: (alignLeft ? cx - 9 : cx + 9).toFixed(1),
          y: (cy + dy).toFixed(1),
          'text-anchor': alignLeft ? 'end' : 'start',
          fill: '#e2e8f0',
          'font-size': 10.5,
        },
        [(r.displayName || r.modelId).slice(0, 22)]
      ),
      svgEl('title', {}, [
        `${r.displayName || r.modelId}: ${axisSpec.yLabel}=${axisSpec.fmtY(axisSpec.getY(r))}, ${axisSpec.xLabel}=${axisSpec.fmtX(axisSpec.getX(r))}`,
      ]),
    ]);
    svg.appendChild(g);
  });

  container.appendChild(svg);
}

export function renderHeadToHeadMatrix(container, leaderboard, headToHead) {
  clear(container);
  const models = (leaderboard || []).filter((r) => r.games > 0).slice(0, 10);
  if (models.length < 2) {
    container.appendChild(el('div', { className: 'empty-state', text: t('chartH2HEmpty') }));
    return;
  }

  const pairMap = new Map();
  for (const c of headToHead || []) {
    pairMap.set(`${c.modelA}::${c.modelB}`, c);
  }

  const n = models.length;
  const cell = 46;
  const padL = 152;
  const padR = 64;
  const padT = 92;
  const W = padL + n * cell + padR;
  const H = padT + n * cell + 16;

  const svg = svgEl('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}` });

  models.forEach((colM, j) => {
    const x = padL + j * cell + cell / 2;
    svg.appendChild(
      svgEl(
        'text',
        {
          x,
          y: padT - 8,
          fill: '#cbd5e1',
          'font-size': 10.5,
          transform: `rotate(-35 ${x} ${padT - 8})`,
        },
        [(colM.displayName || colM.modelId).slice(0, 14)]
      )
    );
  });

  models.forEach((rowM, i) => {
    const y = padT + i * cell;
    svg.appendChild(
      svgEl(
        'text',
        { x: padL - 8, y: y + cell / 2 + 4, 'text-anchor': 'end', fill: '#e2e8f0', 'font-size': 11 },
        [(rowM.displayName || rowM.modelId).slice(0, 16)]
      )
    );

    models.forEach((colM, j) => {
      const x = padL + j * cell;
      if (i === j) {
        svg.appendChild(svgEl('rect', { x: x + 2, y: y + 2, width: cell - 4, height: cell - 4, rx: 5, fill: '#0f172a' }));
        svg.appendChild(
          svgEl('text', { x: x + cell / 2, y: y + cell / 2 + 4, 'text-anchor': 'middle', fill: '#64748b', 'font-size': 11 }, ['—'])
        );
        return;
      }

      const stat = pairMap.get(`${rowM.modelId}::${colM.modelId}`);
      if (!stat || stat.sharedMatches === 0) {
        svg.appendChild(svgEl('rect', { x: x + 2, y: y + 2, width: cell - 4, height: cell - 4, rx: 5, fill: '#162033' }));
        svg.appendChild(
          svgEl('text', { x: x + cell / 2, y: y + cell / 2 + 4, 'text-anchor': 'middle', fill: '#64748b', 'font-size': 10 }, [t('chartGamesShort', { count: 0 })])
        );
        return;
      }

      const wr = stat.winRateA;
      const fill =
        wr >= 0.6
          ? 'rgba(52, 211, 153, 0.34)'
          : wr >= 0.51
            ? 'rgba(52, 211, 153, 0.18)'
            : wr <= 0.4
              ? 'rgba(248, 113, 113, 0.32)'
              : wr <= 0.49
                ? 'rgba(248, 113, 113, 0.16)'
                : 'rgba(148, 163, 184, 0.2)';

      const g = svgEl('g', {}, [
        svgEl('rect', { x: x + 2, y: y + 2, width: cell - 4, height: cell - 4, rx: 5, fill }),
        svgEl(
          'text',
          { x: x + cell / 2, y: y + 20, 'text-anchor': 'middle', fill: '#f8fafc', 'font-size': 10.5, 'font-weight': 700 },
          [`${Math.round(wr * 100)}%`]
        ),
        svgEl(
          'text',
          { x: x + cell / 2, y: y + 34, 'text-anchor': 'middle', fill: '#cbd5e1', 'font-size': 9.2 },
          [`${stat.aBeatB}-${stat.bBeatA}`]
        ),
        svgEl('title', {}, [
          t('chartH2HTooltip', {
            row: rowM.displayName || rowM.modelId,
            col: colM.displayName || colM.modelId,
            w: stat.aBeatB,
            l: stat.bBeatA,
            t: stat.ties,
            shared: stat.sharedMatches,
            diff: formatSigned(stat.avgPointDiffA, 0),
          }),
        ]),
      ]);
      svg.appendChild(g);
    });
  });

  container.appendChild(svg);
}

export function renderYakuBreakdownChart(container, yakuBreakdown, modelFilter = 'all') {
  clear(container);
  const items = (yakuBreakdown || [])
    .map((y) => {
      const cnt = modelFilter === 'all' ? y.count : (y.byModel && y.byModel[modelFilter]) || 0;
      return { ...y, filteredCount: cnt };
    })
    .filter((y) => y.filteredCount > 0)
    .sort((a, b) => b.filteredCount - a.filteredCount)
    .slice(0, 14);

  if (items.length === 0) {
    container.appendChild(el('div', { className: 'empty-state', text: t('chartYakuEmpty') }));
    return;
  }

  const groupLabelMap = {
    '1-han': t('csYakuGroup1Han'),
    '2-han': t('csYakuGroup2Han'),
    '3-han': t('csYakuGroup3Han'),
    '6-han': t('csYakuGroup6Han'),
    yakuman: t('csYakuGroupYakuman'),
    koyaku: t('csYakuGroupKoyaku'),
    other: t('csYakuGroupOther'),
  };

  const maxCount = Math.max(1, ...items.map((i) => i.filteredCount));
  const rowH = 24;
  const padL = 168;
  const padR = 54;
  const padT = 10;
  const W = 540;
  const H = padT + items.length * rowH + 14;
  const barW = W - padL - padR;

  const svg = svgEl('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}` });
  items.forEach((item, idx) => {
    const y = padT + idx * rowH;
    const w = Math.max(4, (item.filteredCount / maxCount) * barW);
    const fill =
      item.group === 'yakuman'
        ? '#fbbf24'
        : item.isKoyaku
          ? '#a78bfa'
          : '#60a5fa';
    const localizedName = tYakuName(item.name, item.displayName);
    const groupText = groupLabelMap[item.group] || item.group;

    svg.appendChild(
      svgEl('text', { x: padL - 8, y: y + 14, 'text-anchor': 'end', fill: '#e2e8f0', 'font-size': 11 }, [
        localizedName.slice(0, 24),
      ])
    );
    svg.appendChild(
      svgEl('rect', { x: padL, y: y + 3, width: w.toFixed(1), height: 14, rx: 4, fill }, [
        svgEl('title', {}, [
          t('chartYakuTooltip', {
            name: localizedName,
            group: groupText,
            count: formatInt(item.filteredCount),
          }),
        ]),
      ])
    );
    svg.appendChild(
      svgEl('text', { x: (padL + w + 7).toFixed(1), y: y + 14, fill: '#cbd5e1', 'font-size': 11 }, [
        formatInt(item.filteredCount),
      ])
    );
  });

  container.appendChild(svg);
}
