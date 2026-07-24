// Dependency-free public page for the RabiRiichi Arena (ARENA_DESIGN.md §12a).
// Plain fetch + DOM: a run selector (multiple runs are stored & browsable),
// then run status, next match, leaderboard, and the paginated match list with
// replay links, all scoped to the selected run. The admin page is separate.

"use strict";

const API = "/api/arena";
const PAGE_SIZE = 20;
let page = 1;

// The run currently being viewed. Empty string means "the newest run" (the
// server resolves the default), until the selector is populated.
let currentRunId = "";

function el(id) {
  return document.getElementById(id);
}

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  })[c]);
}

async function getJson(path) {
  const res = await fetch(path);
  if (!res.ok) {
    throw new Error(`${path}: ${res.status}`);
  }
  return res.json();
}

// Appends ?runId=... (or &runId=...) to a path when a run is selected.
function withRun(path) {
  if (!currentRunId) {
    return path;
  }
  const sep = path.includes("?") ? "&" : "?";
  return `${path}${sep}runId=${encodeURIComponent(currentRunId)}`;
}

function fmtDelta(d) {
  const n = Math.round(d);
  if (n > 0) return `<span class="delta-up">+${n}</span>`;
  if (n < 0) return `<span class="delta-down">${n}</span>`;
  return `<span class="muted">0</span>`;
}

function fmtTime(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  return isNaN(d) ? esc(iso) : d.toLocaleString();
}

async function loadRuns() {
  try {
    const runs = await getJson(`${API}/runs`);
    const select = el("run-select");
    if (!runs.length) {
      select.innerHTML = `<option value="">(no runs yet)</option>`;
      el("run-meta").textContent = "";
      currentRunId = "";
      return;
    }
    // Default to the active run if none picked yet, else the newest (first).
    if (!currentRunId) {
      const active = runs.find((r) => r.active);
      currentRunId = (active || runs[0]).runId;
    } else if (!runs.some((r) => r.runId === currentRunId)) {
      currentRunId = runs[0].runId;
    }
    select.innerHTML = runs.map((r) => {
      const label = `${r.active ? "\u25CF " : ""}${labelForRun(r)}`;
      const sel = r.runId === currentRunId ? " selected" : "";
      return `<option value="${esc(r.runId)}"${sel}>${esc(label)}</option>`;
    }).join("");
    const cur = runs.find((r) => r.runId === currentRunId);
    el("run-meta").textContent = cur
      ? `${cur.status} \u2014 ${cur.completedMatches} matches, ${cur.modelCount} models`
      : "";
  } catch (e) {
    el("run-meta").textContent = "Failed to load runs.";
  }
}

function labelForRun(r) {
  const when = fmtTime(r.createdAt) || r.runId;
  return `${when} (${r.status})`;
}

async function loadStatus() {
  try {
    const s = await getJson(withRun(`${API}/next`));
    const bits = [
      `<span class="pill">${esc(s.status)}</span>`,
      s.runId ? `<span class="muted">run ${esc(s.runId)}</span>` : "",
      s.totalSwissRounds
        ? `<span>Round ${s.currentSwissRound}/${s.totalSwissRounds}</span>` : "",
      s.totalTablesInRound
        ? `<span>Table ${s.completedTablesInRound}/${s.totalTablesInRound}</span>` : "",
      `<span>${s.completedMatches} matches played</span>`,
      s.secondsToNextMatch != null
        ? `<span>Next in ${Math.ceil(s.secondsToNextMatch)}s</span>` : "",
    ].filter(Boolean);
    el("status").innerHTML = bits.join("");
    renderNext(s.nextMatch);
  } catch (e) {
    el("status").innerHTML = `<span class="err">Failed to load status.</span>`;
  }
}

function renderNext(nm) {
  if (!nm || !nm.seats || nm.seats.length === 0) {
    el("next").innerHTML = `<span class="muted">No match scheduled.</span>`;
    return;
  }
  const rows = nm.seats.map((s) =>
    `<tr><td class="num">${s.seat}</td><td>${esc(s.displayName || s.modelId)}</td></tr>`
  ).join("");
  el("next").innerHTML =
    `<div class="muted">Swiss round ${nm.swissRound}` +
    `${nm.padded ? " (padded with baselines)" : ""}</div>` +
    `<table><thead><tr><th class="num">Seat</th><th>Player</th></tr></thead>` +
    `<tbody>${rows}</tbody></table>`;
}

async function loadLeaderboard() {
  try {
    const rows = await getJson(withRun(`${API}/leaderboard`));
    if (!rows.length) {
      el("leaderboard").innerHTML = `<span class="muted">No ratings yet.</span>`;
      return;
    }
    const body = rows.map((r) => `<tr>
      <td class="num">${r.rank}</td>
      <td>${esc(r.displayName)}</td>
      <td class="num">${Math.round(r.elo)}</td>
      <td class="num">${r.games}</td>
      <td class="num">${r.wins}</td>
      <td class="num">${r.avgPlacement ? r.avgPlacement.toFixed(2) : "&ndash;"}</td>
      <td class="num">${r.place1}/${r.place2}/${r.place3}/${r.place4}</td>
      <td class="num">${r.penalties}</td>
    </tr>`).join("");
    el("leaderboard").innerHTML = `<table><thead><tr>
      <th class="num">#</th><th>Model</th><th class="num">Elo</th>
      <th class="num">Games</th><th class="num">Wins</th>
      <th class="num">Avg</th><th class="num">1/2/3/4</th>
      <th class="num">Pen</th></tr></thead><tbody>${body}</tbody></table>`;
  } catch (e) {
    el("leaderboard").innerHTML = `<span class="err">Failed to load leaderboard.</span>`;
  }
}

async function loadMatches() {
  try {
    const data = await getJson(withRun(`${API}/matches?page=${page}&pageSize=${PAGE_SIZE}`));
    const items = data.items || [];
    if (!items.length) {
      el("matches").innerHTML = `<span class="muted">No matches yet.</span>`;
    } else {
      const body = items.map((m) => {
        const players = m.players.map((p) =>
          `<div class="seat">#${p.placement} ${esc(p.displayName)} ` +
          `&mdash; ${p.finalPoints} (${Math.round(p.eloAfter)} ${fmtDelta(p.eloDelta)})</div>`
        ).join("");
        const link = m.replayLink
          ? `<a href="${esc(m.replayLink)}" target="_blank" rel="noopener">Replay</a>` : "";
        return `<tr>
          <td>${fmtTime(m.finishedAt)}</td>
          <td><div class="match-players">${players}</div></td>
          <td>${link}</td>
        </tr>`;
      }).join("");
      el("matches").innerHTML = `<table><thead><tr>
        <th>Finished</th><th>Players (rank &mdash; score (elo &Delta;))</th>
        <th>Link</th></tr></thead><tbody>${body}</tbody></table>`;
    }
    const totalPages = Math.max(1, Math.ceil((data.totalCount || 0) / PAGE_SIZE));
    el("pageinfo").textContent = `Page ${data.page} / ${totalPages} (${data.totalCount || 0} total)`;
    el("prev").disabled = data.page <= 1;
    el("next-page").disabled = data.page >= totalPages;
  } catch (e) {
    el("matches").innerHTML = `<span class="err">Failed to load matches.</span>`;
  }
}

// Reloads everything scoped to the currently-selected run.
function loadRunData() {
  page = 1;
  loadStatus();
  loadLeaderboard();
  loadMatches();
}

el("prev").addEventListener("click", () => {
  if (page > 1) { page--; loadMatches(); }
});
el("next-page").addEventListener("click", () => {
  page++; loadMatches();
});
el("run-select").addEventListener("change", (e) => {
  currentRunId = e.target.value;
  loadRuns();
  loadRunData();
});

// Initial load: discover runs, then load the default run's data.
(async () => {
  await loadRuns();
  loadRunData();
})();

// Refresh the run list + live status/next-match panel periodically (the
// leaderboard/matches only change when a match finishes, which the status poll
// reflects; a manual run switch reloads them immediately).
setInterval(() => {
  loadRuns();
  loadStatus();
}, 5000);
