using System;
using System.Collections.Generic;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Config {
  /// <summary>
  /// Thread-safe holder around the live <see cref="ArenaConfig"/> singleton and its
  /// on-disk path (<c>arena.config.json</c>). Validates updates, preserves redacted
  /// secrets unless explicitly replaced/cleared, writes atomically to disk, and
  /// hot-reloads the singleton in place.
  /// </summary>
  public sealed class ArenaConfigProvider {
    private readonly object gate = new();
    private readonly string configPath;
    private readonly ArenaConfig config;

    public static readonly IReadOnlyList<string> RestartRequiredFields = [
      "workspaceDir",
    ];

    public ArenaConfigProvider(ArenaConfig config, string configPath) {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      this.configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    }

    public ArenaConfig Current {
      get {
        lock (gate) {
          return config;
        }
      }
    }

    public string ConfigPath => configPath;

    public sealed class UpdateResult {
      public bool Success { get; init; }
      public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }

    public UpdateResult Update(ArenaConfig incoming) {
      if (incoming == null) {
        throw new ArgumentNullException(nameof(incoming));
      }
      lock (gate) {
        var merged = incoming.Clone();
        merged.MergeSecretsFrom(config);
        var errors = merged.Validate();
        if (errors.Count > 0) {
          return new UpdateResult { Success = false, Errors = errors };
        }
        merged.Save(configPath);
        CopyInto(config, merged);
        return new UpdateResult { Success = true };
      }
    }

    private static void CopyInto(ArenaConfig dst, ArenaConfig src) {
      dst.WorkspaceDir = src.WorkspaceDir;
      dst.AdminPassword = src.AdminPassword;
      dst.ClientUrl = src.ClientUrl;
      dst.WsUrl = src.WsUrl;
      dst.ExposeReasoningPublicly = src.ExposeReasoningPublicly;
      dst.Models = src.Models;
      dst.GameRules = src.GameRules;
      dst.Prompts = src.Prompts;
      dst.Decision = src.Decision;
      dst.Run = src.Run;
      dst.Rating = src.Rating;
    }
  }
}
