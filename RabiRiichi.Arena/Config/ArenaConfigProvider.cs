using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Config {
  /// <summary>
  /// A thin, mutable holder around the single <see cref="ArenaConfig"/> singleton
  /// plus the on-disk config path. It lets the admin config editor (§12b/§13)
  /// overwrite <c>arena.config.json</c> AND hot-reload the in-memory config so
  /// subsequent requests (and long-lived collaborators like
  /// <c>PublicController</c> / <c>ArenaService</c>, which capture the singleton by
  /// reference) observe the change.
  ///
  /// Why mutate-in-place: <c>Program.cs</c> registers a single
  /// <see cref="ArenaConfig"/> INSTANCE and hands the same reference to several
  /// long-lived services. Swapping the reference would leave those services
  /// pointing at the old object. To hot-reload without editing <c>Storage/*</c>
  /// (out of scope), <see cref="Update"/> copies the validated new values into the
  /// existing instance field-by-field, so every holder of the reference sees the
  /// update at once. Some fields (e.g. workspace dir, provider wiring) still need
  /// a process restart to take effect — see <see cref="RestartRequiredFields"/>.
  ///
  /// This class lives OUTSIDE <c>Storage/</c> on purpose: it is a hosting-level
  /// coordinator, not a flat-file store.
  /// </summary>
  public sealed class ArenaConfigProvider {
    private readonly object gate = new();
    private readonly string configPath;
    private readonly ArenaConfig config;

    /// <summary>
    /// Config fields whose change only takes effect after a process restart.
    /// Surfaced to the admin UI so operators know a restart is needed.
    /// </summary>
    public static readonly IReadOnlyList<string> RestartRequiredFields = new[] {
      "workspaceDir",
      "models[].apiKey / baseUrl / provider (provider client wiring)",
    };

    /// <param name="config">The live singleton instance shared across services.</param>
    /// <param name="configPath">Absolute path to <c>arena.config.json</c>.</param>
    public ArenaConfigProvider(ArenaConfig config, string configPath) {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      this.configPath = configPath
          ?? throw new ArgumentNullException(nameof(configPath));
    }

    /// <summary>The live config instance (never null).</summary>
    public ArenaConfig Current => config;

    /// <summary>Absolute path of the config file backing this provider.</summary>
    public string ConfigPath => configPath;

    /// <summary>The result of an attempted config <see cref="Update"/>.</summary>
    public sealed class UpdateResult {
      public bool Success { get; init; }
      public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Validates <paramref name="incoming"/>, preserves any secret the caller left
    /// blank (so a redacted round-trip never wipes real secrets — §12b), writes it
    /// to disk atomically, and mutates the live singleton in place. Returns the
    /// validation errors (and does NOT touch disk or memory) when invalid.
    /// </summary>
    public UpdateResult Update(ArenaConfig incoming) {
      if (incoming == null) {
        throw new ArgumentNullException(nameof(incoming));
      }
      lock (gate) {
        // Merge secrets FIRST so validation runs against the effective config
        // (e.g. so a blank incoming adminPassword doesn't spuriously fail while
        // the existing one is kept).
        var merged = MergeSecrets(incoming, config);
        var errors = merged.Validate();
        if (errors.Count > 0) {
          return new UpdateResult { Success = false, Errors = errors };
        }
        merged.Save(configPath);
        CopyInto(config, merged);
        return new UpdateResult { Success = true };
      }
    }

    /// <summary>
    /// Returns a deep copy of <paramref name="incoming"/> with any blank secret
    /// (adminPassword, per-model apiKey) restored from <paramref name="existing"/>.
    /// A "blank" secret is the empty/placeholder value emitted by
    /// <see cref="ArenaConfig.Redacted"/>, so re-saving a redacted GET body keeps
    /// the real secrets. A non-blank secret in the incoming body overwrites.
    /// </summary>
    private static ArenaConfig MergeSecrets(ArenaConfig incoming, ArenaConfig existing) {
      var merged = incoming.Clone();

      if (IsBlankSecret(merged.AdminPassword)) {
        merged.AdminPassword = existing.AdminPassword;
      }

      var existingKeyById = existing.Models
          .Where(m => !string.IsNullOrEmpty(m.Id))
          .GroupBy(m => m.Id)
          .ToDictionary(g => g.Key, g => g.First().ApiKey);
      foreach (var m in merged.Models) {
        if (IsBlankSecret(m.ApiKey)
            && !string.IsNullOrEmpty(m.Id)
            && existingKeyById.TryGetValue(m.Id, out var prior)) {
          m.ApiKey = prior;
        }
      }
      return merged;
    }

    /// <summary>
    /// A secret counts as "not provided" when it is blank/whitespace — matching
    /// what <see cref="ArenaConfig.Redacted"/> emits (empty strings). Such values
    /// mean "keep the stored secret".
    /// </summary>
    private static bool IsBlankSecret(string value) =>
        string.IsNullOrWhiteSpace(value);

    /// <summary>Copies every field of <paramref name="src"/> into <paramref name="dst"/>.</summary>
    private static void CopyInto(ArenaConfig dst, ArenaConfig src) {
      dst.WorkspaceDir = src.WorkspaceDir;
      dst.AdminPassword = src.AdminPassword;
      dst.PublicUrl = src.PublicUrl;
      dst.WsUrl = src.WsUrl;
      dst.ClientUrl = src.ClientUrl;
      dst.Run = src.Run;
      dst.Rating = src.Rating;
      dst.Decision = src.Decision;
      dst.Exposure = src.Exposure;
      dst.Models = src.Models;
    }
  }
}
