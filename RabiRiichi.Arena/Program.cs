// Entry point for the RabiRiichi Arena server (LLM mahjong benchmarking).
//
// Milestone 5 wires the public surface: DI for the flat-file stores + the run
// scheduler, the public REST controllers (/api/arena/*), the public replay
// WebSocket (/ws/public, §12d), and the static SPA (UseStaticFiles +
// MapFallbackToFile). Runs are NOT auto-started here — start/stop is an admin
// action added in M6. This file is kept easy for M6 to extend (admin
// controllers slot in via the same AddControllers/MapControllers pipeline).

using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Arena.WebSockets;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Services;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena {
  public class Program {
    public static void Main(string[] args) {
      var builder = WebApplication.CreateBuilder(args);

      // ----- Config: the single source of truth (§13) --------------------
      // Path resolution: RABIRIICHI_ARENA_CONFIG env var if set, else
      // "arena.config.json" under the current working directory. A missing file
      // loads defaults (Load never returns null) so the app still boots (e.g.
      // for /healthz).
      var configPath =
          Environment.GetEnvironmentVariable("RABIRIICHI_ARENA_CONFIG");
      if (string.IsNullOrWhiteSpace(configPath)) {
        configPath = Path.Combine(Directory.GetCurrentDirectory(), "arena.config.json");
      }
      var config = ArenaConfig.Load(configPath);
      Logger.Log($"[Arena] Loaded config from '{configPath}' " +
          $"(workspace='{config.WorkspaceDir}', models={config.Models.Count}).");

      // The workspace holds replays/, matches/, ratings.json, run.json, stats.json.
      var workspaceDir = string.IsNullOrWhiteSpace(config.WorkspaceDir)
          ? Path.Combine(Directory.GetCurrentDirectory(), "workspace")
          : config.WorkspaceDir;
      Directory.CreateDirectory(workspaceDir);

      var services = builder.Services;

      // ----- Flat-file stores (all singletons over the workspace) --------
      services.AddSingleton(config);
      // Mutable holder for the single config instance + its on-disk path, so the
      // admin config editor (§12b) can overwrite arena.config.json AND hot-reload
      // the in-memory config in place (services capture the same instance by
      // reference). Lives outside Storage/ — it is a hosting coordinator.
      services.AddSingleton(new ArenaConfigProvider(config, configPath));
      // Multiple runs: each run is fully isolated under {workspace}/runs/{runId}/
      // (own config snapshot + ratings + matches + run.json). The RunManager owns
      // that layout; ratings/matches/run-state are NOT global singletons anymore.
      services.AddSingleton(new RunManager(workspaceDir));
      // Usage counters + reasoning transcripts stay GLOBAL (keyed by model/gameId).
      services.AddSingleton(new UsageStats(workspaceDir));
      services.AddSingleton(new ReasoningStore(workspaceDir));

      // The Arena replay store REUSES the server ReplayStore, pointing it at the
      // workspace's replays/ dir. ReplayStore resolves its dir purely from the
      // injected ReplayOptions (env only kicks in via the parameterless ctor), so
      // we construct ReplayOptions(saveDir, ttl) explicitly and never touch the
      // RABIRIICHI_GAME_SAVE_DIR env var.
      var replayDir = Path.Combine(workspaceDir, "replays");
      Directory.CreateDirectory(replayDir);
      services.AddSingleton(new ReplayStore(new ReplayOptions(replayDir, ttl: null)));

      // InfoServiceImpl (GetInfo) is reused for the /ws/public GetInfo response so
      // the client's version check passes; it needs an ILogger from DI.
      services.AddSingleton<InfoServiceImpl>();

      // ----- Run scheduler ------------------------------------------------
      // LLM provider plumbing: the factory needs an IHttpClientFactory; the
      // resolver turns a roster ModelConfig into an ILlmProvider for LLM seats
      // (baseline seats never call it).
      services.AddHttpClient();
      services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

      services.AddSingleton(sp => {
        var providerResolver = ArenaProviderResolver.Create(
            sp.GetRequiredService<ILlmProviderFactory>());
        var replayStore = sp.GetRequiredService<ReplayStore>();
        var reasoningStore = sp.GetRequiredService<ReasoningStore>();
        var usageStats = sp.GetRequiredService<UsageStats>();
        var configProvider = sp.GetRequiredService<ArenaConfigProvider>();
        // The scheduler builds a fresh, single-use EvalRoom per match, using the
        // ACTIVE run's frozen config snapshot (not the live editable config).
        Func<ArenaConfig, EvalRoom> evalRoomFactory = runConfig => new EvalRoom(
            runConfig, replayStore, reasoningStore, usageStats, providerResolver);
        return new ArenaService(
            sp.GetRequiredService<RunManager>(),
            currentConfig: () => configProvider.Current,
            evalRoomFactory: evalRoomFactory);
      });
      // Thin lifetime wrapper; does NOT auto-start a run (admin controls that).
      services.AddHostedService<ArenaHostedService>();

      // ----- Public replay WebSocket handler (§12d) ----------------------
      services.AddSingleton<ArenaPublicWebSocket>();

      // ----- Web pipeline -------------------------------------------------
      services.AddCors(options => options.AddPolicy("AllowAll", p =>
          p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
      services.AddControllers()
          .ConfigureApplicationPartManager(apm => {
            // The Arena references RabiRiichi.Server as a LIBRARY (services,
            // agents, WS building blocks), but MVC would otherwise ALSO discover
            // that Web app's controllers — notably a second /ws/public
            // (WebSocketController.ConnectPublic) plus /ws/connect etc. Two
            // handlers on /ws/public cause an AmbiguousMatchException (HTTP 500)
            // on every replay fetch. Keep ONLY the Arena's own controllers.
            foreach (var part in apm.ApplicationParts
                .Where(p => p.Name == "RabiRiichi.Server").ToList()) {
              apm.ApplicationParts.Remove(part);
            }
          });

      var app = builder.Build();

      // Behind a reverse proxy, trust X-Forwarded-Proto/Host so request-derived
      // origins (notably the replay link's "server=ws(s)://host") reflect the
      // PUBLIC scheme + host the browser used — not the proxy's internal http
      // forward (which the browser can't reach, and which is ws:// under an https
      // page). Must run before anything reads Request.Scheme/Host. The Arena is
      // expected to sit behind a trusted proxy, and this only affects link
      // generation (not an auth boundary), so the headers are honored regardless
      // of the proxy's address. If wsUrl is set explicitly in config, that always
      // wins and this is irrelevant.
      var forwarded = new ForwardedHeadersOptions {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
      };
      forwarded.KnownNetworks.Clear();
      forwarded.KnownProxies.Clear();
      app.UseForwardedHeaders(forwarded);

      app.UseCors("AllowAll");
      app.UseWebSockets();
      app.UseStaticFiles();

      app.MapGet("/healthz", () => "ok");
      app.MapControllers();
      // SPA fallback: serve wwwroot/index.html for non-API, non-file routes.
      app.MapFallbackToFile("index.html");

      app.Run();
    }
  }
}
