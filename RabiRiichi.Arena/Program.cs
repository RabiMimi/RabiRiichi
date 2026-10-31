using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Controllers;
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

      var configPath = Environment.GetEnvironmentVariable("RABIRIICHI_ARENA_CONFIG");
      if (string.IsNullOrWhiteSpace(configPath)) {
        configPath = Path.Combine(Directory.GetCurrentDirectory(), "arena.config.json");
      }
      var config = File.Exists(configPath) ? ArenaConfig.Load(configPath) : new ArenaConfig();
      Logger.Log($"[Arena] Loaded config from '{configPath}' " +
          $"(workspace='{config.WorkspaceDir}', models={config.Models.Count}).");

      var workspaceDir = string.IsNullOrWhiteSpace(config.WorkspaceDir)
          ? Path.Combine(Directory.GetCurrentDirectory(), "workspace")
          : config.WorkspaceDir;
      Directory.CreateDirectory(workspaceDir);

      var services = builder.Services;

      services.AddSingleton(config);
      services.AddSingleton(new ArenaConfigProvider(config, configPath));
      services.AddSingleton<AdminSessionService>();
      services.AddSingleton(new RunManager(workspaceDir));
      services.AddSingleton(new UsageStats(workspaceDir));
      services.AddSingleton(new ReasoningStore(workspaceDir));

      var replayDir = Path.Combine(workspaceDir, "replays");
      Directory.CreateDirectory(replayDir);
      services.AddSingleton(new ReplayStore(new ReplayOptions(replayDir, ttl: null)));
      services.AddSingleton<InfoServiceImpl>();

      services.AddHttpClient();
      services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

      services.AddSingleton(sp => {
        var providerResolver = ArenaProviderResolver.Create(
            sp.GetRequiredService<ILlmProviderFactory>(),
            sp.GetRequiredService<IHttpClientFactory>());
        var replayStore = sp.GetRequiredService<ReplayStore>();
        var reasoningStore = sp.GetRequiredService<ReasoningStore>();
        var usageStats = sp.GetRequiredService<UsageStats>();
        var configProvider = sp.GetRequiredService<ArenaConfigProvider>();
        Func<ArenaConfig, EvalRoom> evalRoomFactory = runConfig => new EvalRoom(
            runConfig, replayStore, reasoningStore, usageStats, providerResolver);
        return new ArenaService(
            sp.GetRequiredService<RunManager>(),
            currentConfig: () => configProvider.Current,
            evalRoomFactory: evalRoomFactory);
      });
      services.AddHostedService<ArenaHostedService>();
      services.AddSingleton<ArenaPublicWebSocket>();

      // Scoped read-only CORS for public Arena endpoints (used by RabiRiichi-Web 3D replay viewer).
      services.AddCors(options => {
        options.AddPolicy("PublicReadCors", p =>
            p.AllowAnyOrigin()
             .WithMethods("GET", "HEAD", "OPTIONS")
             .AllowAnyHeader());
      });

      services.AddControllers()
          .ConfigureApplicationPartManager(apm => {
            foreach (var part in apm.ApplicationParts
                .Where(p => p.Name == "RabiRiichi.Server").ToList()) {
              apm.ApplicationParts.Remove(part);
            }
          });

      var app = builder.Build();

      var forwarded = new ForwardedHeadersOptions {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
      };
      forwarded.KnownNetworks.Clear();
      forwarded.KnownProxies.Clear();
      app.UseForwardedHeaders(forwarded);

      // Strict security headers on all Arena responses.
      app.Use(async (ctx, next) => {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; " +
            "img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; " +
            "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        await next();
      });

      app.UseCors();
      app.UseWebSockets();
      app.UseStaticFiles();

      app.MapGet("/healthz", () => "ok");
      app.MapControllers();
      app.MapFallbackToFile("index.html");

      app.Run();
    }
  }
}
