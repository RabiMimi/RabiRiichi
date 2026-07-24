using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Strongly-typed model of the single Arena config file
  /// (<c>arena.config.json</c>). See ARENA_DESIGN.md §13. This is the single
  /// source of truth: provider keys, model roster, run settings, admin
  /// password, and server/client URLs all live here. Admin-editable via the
  /// web UI in a later milestone.
  ///
  /// The model is a mutable POCO (not a record) because the admin config editor
  /// overwrites the whole document; nested blocks default to non-null instances
  /// so partially-specified files load with sane defaults.
  /// </summary>
  public sealed class ArenaConfig {
    /// <summary>Workspace dir holding replays, reasoning, matches, ratings, stats.</summary>
    public string WorkspaceDir { get; set; } = "";

    /// <summary>Gate for admin endpoints (secret; redacted for display).</summary>
    public string AdminPassword { get; set; } = "";

    /// <summary>Arena HTTP base for its own pages/API.</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>
    /// Arena <c>/ws/public</c> base used for replay fetches. Leave EMPTY (the
    /// default) to auto-derive it from each request's own origin — the Arena
    /// serves the WebSocket on the same port that serves its page/API, so this
    /// "just works" regardless of the port. Set an explicit <c>ws(s)://host[:port]</c>
    /// only to override for reverse-proxy deployments.
    /// </summary>
    public string WsUrl { get; set; } = "";

    /// <summary>Web client base used to build replay links.</summary>
    public string ClientUrl { get; set; } = "";

    public RunConfig Run { get; set; } = new();
    public RatingConfig Rating { get; set; } = new();
    public DecisionConfig Decision { get; set; } = new();
    public ExposureConfig Exposure { get; set; } = new();
    public List<ModelConfig> Models { get; set; } = new();

    // ----- Nested config blocks -------------------------------------------

    public sealed class RunConfig {
      /// <summary>Number of Swiss rounds per run.</summary>
      public int SwissRounds { get; set; } = 7;

      /// <summary>1 = 東 (tonpuusen), 2 = 東南 (hanchan). Engine accepts only 1 or 2.</summary>
      public int TotalRound { get; set; } = 2;

      /// <summary>Players per table (mahjong = 4).</summary>
      public int PlayerCount { get; set; } = 4;

      /// <summary>How many matches may run at once.</summary>
      public int MatchConcurrency { get; set; } = 1;

      /// <summary>Configurable inter-match cooldown.</summary>
      public int CooldownSecondsBetweenMatches { get; set; } = 30;
    }

    public sealed class RatingConfig {
      /// <summary>Elo K-factor (default 24).</summary>
      public double KFactor { get; set; } = 24;

      /// <summary>Elo for models with no prior rating (default 1500).</summary>
      public double InitialElo { get; set; } = 1500;
    }

    public sealed class DecisionConfig {
      /// <summary>Long per-decision budget (seconds).</summary>
      public int TimeoutSeconds { get; set; } = 180;

      /// <summary>Invalid answers retried this many times, then penalized with default.</summary>
      public int MaxRetries { get; set; } = 3;

      /// <summary>Cap on model output tokens per decision.</summary>
      public int MaxOutputTokens { get; set; } = 2048;
    }

    /// <summary>
    /// What agents may learn about each other. Affects ONLY what agents see in
    /// prompts/chat; humans always see real identities on the public page and
    /// in the replay. See §9/§9a. Both default to false.
    /// </summary>
    public sealed class ExposureConfig {
      /// <summary>false = opponents fully anonymous (neutral seat labels only).</summary>
      public bool RevealOpponentIdentity { get; set; } = false;

      /// <summary>false = other agents' chat hidden in-game (still kept in replay).</summary>
      public bool ChatToAgents { get; set; } = false;
    }

    /// <summary>
    /// One roster entry — an LLM model or a rule-based baseline
    /// (<c>provider == "baseline"</c>). Baselines use <see cref="FrozenElo"/> as
    /// a fixed anchor rating and take no LLM settings.
    /// </summary>
    public sealed class ModelConfig {
      /// <summary>Stable unique id (used as the key in ratings/stats).</summary>
      public string Id { get; set; } = "";

      /// <summary>Human-facing name shown on the public page.</summary>
      public string DisplayName { get; set; } = "";

      /// <summary>"openai", "gemini", or "baseline".</summary>
      public string Provider { get; set; } = "";

      /// <summary>Provider API base URL (LLM providers only).</summary>
      public string BaseUrl { get; set; } = "";

      /// <summary>Provider-specific model name (LLM providers only).</summary>
      public string Model { get; set; } = "";

      /// <summary>Provider API key (secret; redacted for display).</summary>
      public string ApiKey { get; set; } = "";

      /// <summary>Whether to enable provider "thinking"/reasoning.</summary>
      public bool Thinking { get; set; } = false;

      /// <summary>Optional prompt template override.</summary>
      public string PromptTemplate { get; set; } = "";

      /// <summary>Whether this entry participates in runs.</summary>
      public bool Enabled { get; set; } = true;

      /// <summary>Strategy tier for baselines (e.g. "strong"/"default"/"weak").</summary>
      public string Variant { get; set; } = "";

      /// <summary>Fixed anchor Elo for baselines; null for rated LLMs.</summary>
      public double? FrozenElo { get; set; }
    }

    // ----- Serialization ---------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Loads config from <paramref name="path"/>. Missing files yield a default
    /// config; missing fields fall back to their type defaults. Never returns
    /// null.
    /// </summary>
    public static ArenaConfig Load(string path) {
      if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
        return new ArenaConfig();
      }
      var json = File.ReadAllText(path);
      if (string.IsNullOrWhiteSpace(json)) {
        return new ArenaConfig();
      }
      var cfg = JsonSerializer.Deserialize<ArenaConfig>(json, JsonOptions)
          ?? new ArenaConfig();
      // Guard against explicit nulls in the file.
      cfg.Run ??= new RunConfig();
      cfg.Rating ??= new RatingConfig();
      cfg.Decision ??= new DecisionConfig();
      cfg.Exposure ??= new ExposureConfig();
      cfg.Models ??= new List<ModelConfig>();
      return cfg;
    }

    /// <summary>Serializes this config to a JSON string.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Atomically writes this config to <paramref name="path"/>.</summary>
    public void Save(string path) => AtomicFile.WriteAllText(path, ToJson());

    // ----- Validation ------------------------------------------------------

    /// <summary>
    /// Returns a list of human-readable validation errors. Empty means valid.
    /// </summary>
    public IReadOnlyList<string> Validate() {
      var errors = new List<string>();

      if (string.IsNullOrWhiteSpace(WorkspaceDir)) {
        errors.Add("workspaceDir must not be empty.");
      }
      if (string.IsNullOrWhiteSpace(AdminPassword)) {
        errors.Add("adminPassword must not be empty.");
      }

      if (Run.TotalRound is not (1 or 2)) {
        errors.Add("run.totalRound must be 1 (東) or 2 (東南).");
      }
      if (Run.PlayerCount != 4) {
        errors.Add("run.playerCount must be 4.");
      }
      if (Run.SwissRounds < 1) {
        errors.Add("run.swissRounds must be >= 1.");
      }
      if (Run.MatchConcurrency < 1) {
        errors.Add("run.matchConcurrency must be >= 1.");
      }
      if (Run.CooldownSecondsBetweenMatches < 0) {
        errors.Add("run.cooldownSecondsBetweenMatches must be >= 0.");
      }

      if (Rating.KFactor <= 0) {
        errors.Add("rating.kFactor must be > 0.");
      }
      if (Rating.InitialElo <= 0) {
        errors.Add("rating.initialElo must be > 0.");
      }

      if (Decision.TimeoutSeconds <= 0) {
        errors.Add("decision.timeoutSeconds must be > 0.");
      }
      if (Decision.MaxRetries < 0) {
        errors.Add("decision.maxRetries must be >= 0.");
      }
      if (Decision.MaxOutputTokens <= 0) {
        errors.Add("decision.maxOutputTokens must be > 0.");
      }

      var seenIds = new HashSet<string>();
      for (int i = 0; i < Models.Count; i++) {
        var m = Models[i];
        var where = $"models[{i}]";
        if (string.IsNullOrWhiteSpace(m.Id)) {
          errors.Add($"{where}.id must not be empty.");
        } else if (!seenIds.Add(m.Id)) {
          errors.Add($"{where}.id '{m.Id}' is duplicated.");
        }
        if (string.IsNullOrWhiteSpace(m.Provider)) {
          errors.Add($"{where}.provider must not be empty.");
        } else if (m.Provider is not ("openai" or "gemini" or "baseline")) {
          errors.Add($"{where}.provider '{m.Provider}' must be one of openai, gemini, baseline.");
        }
        if (m.Provider == "baseline") {
          if (m.FrozenElo is null) {
            errors.Add($"{where} (baseline) must set frozenElo.");
          }
        } else if (!string.IsNullOrWhiteSpace(m.Provider)) {
          // LLM providers need a model name to call.
          if (string.IsNullOrWhiteSpace(m.Model)) {
            errors.Add($"{where}.model must not be empty for provider '{m.Provider}'.");
          }
        }
      }

      return errors;
    }

    // ----- Redaction -------------------------------------------------------

    /// <summary>
    /// Returns a deep copy with all secrets (adminPassword, every model apiKey)
    /// blanked, safe to return from the admin config-display endpoint.
    /// </summary>
    public ArenaConfig Redacted() {
      var copy = Clone();
      copy.AdminPassword = "";
      foreach (var m in copy.Models) {
        m.ApiKey = "";
      }
      return copy;
    }

    /// <summary>Deep copy via JSON round-trip.</summary>
    public ArenaConfig Clone() =>
        JsonSerializer.Deserialize<ArenaConfig>(ToJson(), JsonOptions)!;
  }
}
