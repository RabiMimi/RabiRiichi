// Dependency-free admin page for the RabiRiichi Arena (ARENA_DESIGN.md §12b).
// Plain fetch + DOM: a password gate (kept in sessionStorage, sent as the
// X-Admin-Password header), a redacted config editor (load / validate / save),
// run start/stop, and a per-model monitoring table. No secrets are ever
// displayed: GET /api/admin/config returns a redacted config.

"use strict";

const API = "/api/admin";
const HEADER = "X-Admin-Password";
const PW_KEY = "arena.admin.pw";

function el(id) {
  return document.getElementById(id);
}

function esc(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  })[c]);
}

function getPw() {
  return sessionStorage.getItem(PW_KEY) || "";
}

function setPw(pw) {
  if (pw) {
    sessionStorage.setItem(PW_KEY, pw);
  } else {
    sessionStorage.removeItem(PW_KEY);
  }
}

async function api(path, method = "GET", body = undefined) {
  const opts = { method, headers: { [HEADER]: getPw() } };
  if (body !== undefined) {
    opts.headers["Content-Type"] = "application/json";
    opts.body = JSON.stringify(body);
  }
  return fetch(API + path, opts);
}

// ----- Auth -----------------------------------------------------------------

async function connect() {
  setPw(el("password").value.trim());
  el("authmsg").textContent = "Checking\u2026";
  el("authmsg").className = "muted";
  const res = await api("/status");
  if (res.status === 401) {
    el("authmsg").textContent = "Unauthorized \u2014 wrong or empty password.";
    el("authmsg").className = "err";
    el("panel").classList.add("hidden");
    return;
  }
  if (!res.ok) {
    el("authmsg").textContent = `Error: ${res.status}`;
    el("authmsg").className = "err";
    return;
  }
  el("authmsg").textContent = "Connected.";
  el("authmsg").className = "ok";
  el("panel").classList.remove("hidden");
  const status = await res.json();
  renderStatus(status);
  loadConfig();
}

// ----- Run control + monitoring ---------------------------------------------

function renderStatus(s) {
  const r = s.run || {};
  const bits = [
    `<span class="pill">${esc(r.status)}</span>`,
    r.runId ? `<span class="muted">run ${esc(r.runId)}</span>` : "",
    r.totalSwissRounds
      ? `<span>Round ${r.currentSwissRound}/${r.totalSwissRounds}</span>` : "",
    r.totalTablesInRound
      ? `<span>Table ${r.completedTablesInRound}/${r.totalTablesInRound}</span>` : "",
    `<span>${r.completedMatches || 0} matches played</span>`,
    r.secondsToNextMatch != null
      ? `<span>Next in ${Math.ceil(r.secondsToNextMatch)}s</span>` : "",
  ].filter(Boolean);
  el("runstatus").innerHTML = bits.join("");
  renderRuns(s.runs || []);
  renderMonitoring(s.models || []);
}

function renderRuns(runs) {
  if (!runs.length) {
    el("runs").innerHTML = `<span class="muted">No runs yet. Click &ldquo;New run&rdquo;.</span>`;
    return;
  }
  const body = runs.map((r) => `<tr>
    <td>${r.active ? "\u25CF " : ""}${esc(r.runId)}</td>
    <td>${esc(fmtTime(r.createdAt))}</td>
    <td><span class="pill">${esc(r.status)}</span></td>
    <td class="num">${r.currentSwissRound}/${r.swissRounds}</td>
    <td class="num">${r.completedMatches}</td>
    <td class="num">${r.modelCount}</td>
  </tr>`).join("");
  el("runs").innerHTML = `<table><thead><tr>
    <th>Run</th><th>Created</th><th>Status</th>
    <th class="num">Round</th><th class="num">Matches</th><th class="num">Models</th>
  </tr></thead><tbody>${body}</tbody></table>`;
}

function fmtTime(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  return isNaN(d) ? esc(iso) : d.toLocaleString();
}

function renderMonitoring(models) {
  if (!models.length) {
    el("monitoring").innerHTML = `<span class="muted">No usage recorded yet.</span>`;
    return;
  }
  const body = models.map((m) => `<tr>
    <td>${esc(m.displayName || m.modelId)}</td>
    <td class="num">${m.requests}</td>
    <td class="num">${m.successes}</td>
    <td class="num">${m.failures}</td>
    <td class="num">${m.networkErrors}/${m.timeoutErrors}/${m.invalidResponseErrors}/${m.rateLimitedErrors}/${m.authErrors}/${m.otherErrors}</td>
    <td class="num">${m.promptTokens}/${m.completionTokens}/${m.totalTokens}</td>
    <td class="num">${m.retries}</td>
    <td class="num">${m.penalties}</td>
  </tr>`).join("");
  el("monitoring").innerHTML = `<table><thead><tr>
    <th>Model</th><th class="num">Req</th><th class="num">OK</th>
    <th class="num">Fail</th>
    <th class="num" title="network/timeout/invalid/rate/auth/other">Errors by cat</th>
    <th class="num" title="prompt/completion/total">Tokens</th>
    <th class="num">Retries</th><th class="num">Pen</th>
  </tr></thead><tbody>${body}</tbody></table>`;
}

async function refreshStatus() {
  const res = await api("/status");
  if (res.ok) {
    renderStatus(await res.json());
  }
}

async function startRun() {
  const res = await api("/run/start", "POST");
  if (res.ok) {
    renderStatus(await res.json());
  }
}

async function newRun() {
  if (!confirm("Start a NEW run? This freezes the current config into a fresh " +
      "run. Past runs are kept.")) {
    return;
  }
  const res = await api("/run/new", "POST");
  if (res.ok) {
    renderStatus(await res.json());
  }
}

async function stopRun() {
  const res = await api("/run/stop", "POST");
  if (res.ok) {
    renderStatus(await res.json());
  }
}

// ----- Config editor --------------------------------------------------------

async function loadConfig() {
  el("configmsg").textContent = "";
  el("configerrs").innerHTML = "";
  const res = await api("/config");
  if (!res.ok) {
    el("configmsg").textContent = `Failed to load config: ${res.status}`;
    el("configmsg").className = "err";
    return;
  }
  const cfg = await res.json();
  el("config").value = JSON.stringify(cfg, null, 2);
  el("configmsg").textContent = "Loaded (secrets blank).";
  el("configmsg").className = "muted";
}

async function saveConfig() {
  el("configmsg").textContent = "";
  el("configerrs").innerHTML = "";
  el("restartnote").textContent = "";
  let parsed;
  try {
    parsed = JSON.parse(el("config").value);
  } catch (e) {
    el("configmsg").textContent = `Invalid JSON: ${e.message}`;
    el("configmsg").className = "err";
    return;
  }
  const res = await api("/config", "PUT", parsed);
  const data = await res.json().catch(() => ({}));
  if (res.status === 400) {
    el("configmsg").textContent = "Validation failed \u2014 not saved.";
    el("configmsg").className = "err";
    const errs = (data.errors || []).map((e) => `<li>${esc(e)}</li>`).join("");
    el("configerrs").innerHTML = `<ul class="errs">${errs}</ul>`;
    return;
  }
  if (!res.ok) {
    el("configmsg").textContent = `Save failed: ${res.status}`;
    el("configmsg").className = "err";
    return;
  }
  el("configmsg").textContent = "Saved and hot-reloaded.";
  el("configmsg").className = "ok";
  const restart = data.restartRequiredFields || [];
  if (restart.length) {
    el("restartnote").textContent =
      "Restart required for: " + restart.join(", ");
  }
  loadConfig();
}

// ----- Wire up --------------------------------------------------------------

el("connect").addEventListener("click", connect);
el("password").addEventListener("keydown", (e) => {
  if (e.key === "Enter") { connect(); }
});
el("start").addEventListener("click", startRun);
el("new-run").addEventListener("click", newRun);
el("stop").addEventListener("click", stopRun);
el("reload-config").addEventListener("click", loadConfig);
el("save-config").addEventListener("click", saveConfig);

// Auto-connect if a password is already cached this session.
if (getPw()) {
  el("password").value = getPw();
  connect();
}
setInterval(() => {
  if (getPw() && !el("panel").classList.contains("hidden")) {
    refreshStatus();
  }
}, 5000);
