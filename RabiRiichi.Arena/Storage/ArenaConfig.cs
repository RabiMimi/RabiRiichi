using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RabiRiichi.Arena.Config;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Setup;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Complete Arena configuration covering model roster, game rules, custom/wildcard
  /// tile sets, allowed yaku/koyaku, prompt &amp; tool-context controls, decision
  /// limits, tournament/batch scheduling, Elo rating parameters, and URLs.
  /// </summary>
  public sealed class ArenaConfig {
    private static readonly Regex SafeIdRegex = new(
        "^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new() {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string WorkspaceDir { get; set; } = "./workspace";
    public string AdminPassword { get; set; } = "changeme";
    public string ClientUrl { get; set; } = "http://localhost:5173";
    /// <summary> Optional explicit ws(s):// base URL; auto-derived from HTTP host when empty. </summary>
    public string WsUrl { get; set; } = "";
    public bool ExposeReasoningPublicly { get; set; } = true;

    public List<ModelConfig> Models { get; set; } = [];
    public GameRulesConfig GameRules { get; set; } = new();
    public PromptConfig Prompts { get; set; } = new();
    public DecisionConfig Decision { get; set; } = new();
    public RunConfig Run { get; set; } = new();
    public RatingConfig Rating { get; set; } = new();

    public sealed class ModelConfig {
      public string Id { get; set; } = "";
      public string DisplayName { get; set; } = "";
      /// <summary> "openai" | "gemini" | "grok" | "anthropic" | "baseline". </summary>
      public string Provider { get; set; } = "openai";
      public string Model { get; set; }
      public string BaseUrl { get; set; }
      public string ApiKey { get; set; }
      /// <summary> Populated only on redacted responses so the UI knows whether a key is stored. </summary>
      public bool? HasApiKey { get; set; }
      /// <summary> Transient UI flag to explicitly clear a stored API key. </summary>
      public bool? ClearApiKey { get; set; }
      /// <summary> "minimal" | "low" | "medium" | "high". </summary>
      public string ThinkingLevel { get; set; } = "minimal";
      /// <summary> Optional per-model prompt language override: "" (inherit global) | "en" | "zhs" | "zht" | "ja". </summary>
      public string PromptLanguage { get; set; } = "";
      public double? Temperature { get; set; }
      public int? MaxOutputTokens { get; set; }
      /// <summary> Baseline profile: "balanced" | "aggressive" | "defensive" | "fast_open" | "tsumogiri". </summary>
      public string Variant { get; set; } = "balanced";
      public double? FrozenElo { get; set; }
      public bool Enabled { get; set; } = true;
      public string Notes { get; set; }

      [JsonIgnore]
      public bool IsBaseline =>
          string.Equals(Provider, "baseline", StringComparison.OrdinalIgnoreCase);

      public ModelConfig Clone() => new() {
        Id = Id,
        DisplayName = DisplayName,
        Provider = Provider,
        Model = Model,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        ThinkingLevel = ThinkingLevel,
        PromptLanguage = PromptLanguage,
        Temperature = Temperature,
        MaxOutputTokens = MaxOutputTokens,
        Variant = Variant,
        FrozenElo = FrozenElo,
        Enabled = Enabled,
        Notes = Notes,
      };
    }

    public sealed class TileSetConfig {
      /// <summary>
      /// Preset id from <see cref="ArenaMetadata.TilePresets"/> (e.g. "Regular",
      /// "Sanma", "RegularWildcards", "TwoSets", "OnlySZ", "Custom").
      /// </summary>
      public string Preset { get; set; } = "Regular";
      /// <summary> Extra wildcard (*) tiles added on top of the selected preset (0..64). </summary>
      public int ExtraWildcards { get; set; } = 0;
      /// <summary> Used when Preset == "Custom": map of canonical tile code -> count. </summary>
      public Dictionary<string, int> CustomCounts { get; set; } = new();

      public Tiles BuildTiles() =>
          ArenaMetadata.BuildTiles(Preset, ExtraWildcards, CustomCounts);

      public TileSetConfig Clone() => new() {
        Preset = Preset,
        ExtraWildcards = ExtraWildcards,
        CustomCounts = CustomCounts != null
            ? new Dictionary<string, int>(CustomCounts, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal),
      };
    }

    public sealed class GameRulesConfig {
      public int PlayerCount { get; set; } = 4;
      /// <summary> 1 = East Only (Tonpuusen), 2 = East+South (Hanchan). </summary>
      public int TotalRound { get; set; } = 1;
      public int MinHan { get; set; } = 1;

      public long InitialPoints { get; set; } = 25000;
      public long FinishPoints { get; set; } = 30000;
      public long LowerPoints { get; set; } = 0;
      public long UpperPoints { get; set; } = 1000000;
      public long RiichiPoints { get; set; } = 1000;
      public long HonbaPoints { get; set; } = 300;
      public long RyuukyokuPoints0 { get; set; } = 1000;
      public long RyuukyokuPoints1 { get; set; } = 1500;
      public int PointsDeductionPolicy { get; set; } = (int)global::RabiRiichi.Core.Config.PointsDeductionPolicy.SufficientPoints;

      public int RenchanPolicy { get; set; } = (int)global::RabiRiichi.Core.Config.RenchanPolicy.Default;
      public int EndGamePolicy { get; set; } = (int)global::RabiRiichi.Core.Config.EndGamePolicy.Default;
      public int KuikaePolicy { get; set; } = (int)global::RabiRiichi.Core.Config.KuikaePolicy.Default;
      public int RiichiPolicy { get; set; } = (int)global::RabiRiichi.Core.Config.RiichiPolicy.Default;
      public int DoraOption { get; set; } = (int)global::RabiRiichi.Core.Config.DoraOption.Default;
      public int AgariOption { get; set; } = (int)global::RabiRiichi.Core.Config.AgariOption.Default;
      public int ScoringOption { get; set; } = (int)(global::RabiRiichi.Core.Config.ScoringOption.Default | global::RabiRiichi.Core.Config.ScoringOption.KiriageMangan);
      public int RyuukyokuTrigger { get; set; } = (int)global::RabiRiichi.Core.Config.RyuukyokuTrigger.Default;

      public TileSetConfig TileSet { get; set; } = new();
      public List<string> AllowedYakus { get; set; } = [.. ArenaMetadata.DefaultAllowedYakus()];

      public GameConfig BuildGameConfig(double gameplayTimeoutSeconds = 60.0) {
        var tiles = (TileSet ?? new TileSetConfig()).BuildTiles();
        var allowed = AllowedYakus is { Count: > 0 }
            ? AllowedYakus.ToArray()
            : ArenaMetadata.DefaultAllowedYakus().ToArray();

        var cfg = new GameConfig {
          playerCount = PlayerCount,
          totalRound = TotalRound,
          minHan = MinHan,
          initialTiles = tiles,
          pointThreshold = new PointThreshold {
            initialPoints = InitialPoints,
            finishPoints = FinishPoints,
            riichiPoints = RiichiPoints,
            honbaPoints = HonbaPoints,
            ryuukyokuPoints = [RyuukyokuPoints0, RyuukyokuPoints1],
            validPointsRange = [LowerPoints, UpperPoints],
          },
          renchanPolicy = (RenchanPolicy)RenchanPolicy,
          endGamePolicy = (EndGamePolicy)EndGamePolicy,
          kuikaePolicy = (KuikaePolicy)KuikaePolicy,
          riichiPolicy = (RiichiPolicy)RiichiPolicy,
          doraOption = (DoraOption)DoraOption,
          agariOption = (AgariOption)AgariOption,
          scoringOption = (ScoringOption)ScoringOption,
          ryuukyokuTrigger = (RyuukyokuTrigger)RyuukyokuTrigger,
          pointsDeductionPolicy = (PointsDeductionPolicy)PointsDeductionPolicy,
          gameplayActionTimeout = Math.Clamp(gameplayTimeoutSeconds, 5.0, 3600.0),
          nextRoundAckTimeout = 30.0,
          seed = null, // Engine derives fresh hidden RNG seed per match.
          setup = new DynamicRiichiSetup(allowed),
        };
        cfg.Validate();
        return cfg;
      }

      public GameRulesConfig Clone() => new() {
        PlayerCount = PlayerCount,
        TotalRound = TotalRound,
        MinHan = MinHan,
        InitialPoints = InitialPoints,
        FinishPoints = FinishPoints,
        LowerPoints = LowerPoints,
        UpperPoints = UpperPoints,
        RiichiPoints = RiichiPoints,
        HonbaPoints = HonbaPoints,
        RyuukyokuPoints0 = RyuukyokuPoints0,
        RyuukyokuPoints1 = RyuukyokuPoints1,
        PointsDeductionPolicy = PointsDeductionPolicy,
        RenchanPolicy = RenchanPolicy,
        EndGamePolicy = EndGamePolicy,
        KuikaePolicy = KuikaePolicy,
        RiichiPolicy = RiichiPolicy,
        DoraOption = DoraOption,
        AgariOption = AgariOption,
        ScoringOption = ScoringOption,
        RyuukyokuTrigger = RyuukyokuTrigger,
        TileSet = (TileSet ?? new TileSetConfig()).Clone(),
        AllowedYakus = AllowedYakus != null ? [.. AllowedYakus] : [.. ArenaMetadata.DefaultAllowedYakus()],
      };
    }

    public sealed class PromptConfig {
      /// <summary> Default prompt language: "en" | "zhs" | "zht" | "ja". </summary>
      public string Language { get; set; } = "en";
      /// <summary> "minimal" | "standard" | "extended". </summary>
      public string ToolContextMode { get; set; } = "standard";
      /// <summary> When true, LLMs decide out-of-turn chii/pon/daiminkan opportunities. </summary>
      public bool AskLlmForCalls { get; set; } = true;
      /// <summary> Allow optional "chat" table-talk string in JSON response. </summary>
      public bool EnableTableTalk { get; set; } = false;
      /// <summary> Maximum prior condensed turns retained in seat conversation history. </summary>
      public int MaxHistoryTurns { get; set; } = 24;
      /// <summary> Reveal real opponent DisplayNames in prompts instead of blind seat labels. </summary>
      public bool RevealOpponentIdentity { get; set; } = false;
      /// <summary> Optional custom instruction block appended to the system prompt. </summary>
      public string CustomSystemAppendix { get; set; } = "";

      public PromptConfig Clone() => new() {
        Language = Language,
        ToolContextMode = ToolContextMode,
        AskLlmForCalls = AskLlmForCalls,
        EnableTableTalk = EnableTableTalk,
        MaxHistoryTurns = MaxHistoryTurns,
        RevealOpponentIdentity = RevealOpponentIdentity,
        CustomSystemAppendix = CustomSystemAppendix,
      };
    }

    public sealed class DecisionConfig {
      public int TimeoutSeconds { get; set; } = 60;
      public int MaxRetries { get; set; } = 2;
      public int MaxOutputTokens { get; set; } = 1024;

      public DecisionConfig Clone() => new() {
        TimeoutSeconds = TimeoutSeconds,
        MaxRetries = MaxRetries,
        MaxOutputTokens = MaxOutputTokens,
      };
    }

    public sealed class RunConfig {
      /// <summary> "swiss" | "balanced". </summary>
      public string ScheduleMode { get; set; } = "swiss";
      public int SwissRounds { get; set; } = 4;
      public int TargetMatches { get; set; } = 8;
      public int MatchConcurrency { get; set; } = 2;

      public RunConfig Clone() => new() {
        ScheduleMode = ScheduleMode,
        SwissRounds = SwissRounds,
        TargetMatches = TargetMatches,
        MatchConcurrency = MatchConcurrency,
      };
    }

    public sealed class RatingConfig {
      public double InitialElo { get; set; } = 1500.0;
      public double KFactor { get; set; } = 24.0;
      public double Scale { get; set; } = 400.0;

      public RatingConfig Clone() => new() {
        InitialElo = InitialElo,
        KFactor = KFactor,
        Scale = Scale,
      };
    }

    public ArenaConfig Clone() => new() {
      WorkspaceDir = WorkspaceDir,
      AdminPassword = AdminPassword,
      ClientUrl = ClientUrl,
      WsUrl = WsUrl,
      ExposeReasoningPublicly = ExposeReasoningPublicly,
      Models = Models?.Select(m => m.Clone()).ToList() ?? [],
      GameRules = (GameRules ?? new GameRulesConfig()).Clone(),
      Prompts = (Prompts ?? new PromptConfig()).Clone(),
      Decision = (Decision ?? new DecisionConfig()).Clone(),
      Run = (Run ?? new RunConfig()).Clone(),
      Rating = (Rating ?? new RatingConfig()).Clone(),
    };

    /// <summary>
    /// Returns a deep copy with all secrets (AdminPassword and per-model ApiKeys)
    /// stripped so it is safe to return to admin/browser clients.
    /// </summary>
    public ArenaConfig Redacted() {
      var copy = Clone();
      copy.AdminPassword = "";
      foreach (var m in copy.Models) {
        m.HasApiKey = !string.IsNullOrEmpty(m.ApiKey);
        m.ApiKey = null;
        m.ClearApiKey = null;
      }
      return copy;
    }

    /// <summary>
    /// Carries forward existing secrets (AdminPassword and per-model ApiKeys)
    /// when a Web UI update omits or leaves them blank.
    /// </summary>
    public void MergeSecretsFrom(ArenaConfig previous) {
      if (previous == null) return;
      if (string.IsNullOrWhiteSpace(AdminPassword)) {
        AdminPassword = previous.AdminPassword;
      }
      var oldKeys = (previous.Models ?? [])
          .Where(m => !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrEmpty(m.ApiKey))
          .GroupBy(m => m.Id, StringComparer.Ordinal)
          .ToDictionary(g => g.Key, g => g.First().ApiKey, StringComparer.Ordinal);
      foreach (var m in Models ?? []) {
        m.HasApiKey = null;
        if (m.ClearApiKey == true) {
          m.ApiKey = null;
          m.ClearApiKey = null;
          continue;
        }
        m.ClearApiKey = null;
        if (string.IsNullOrWhiteSpace(m.ApiKey) && !string.IsNullOrWhiteSpace(m.Id) &&
            oldKeys.TryGetValue(m.Id, out var existingKey)) {
          m.ApiKey = existingKey;
        }
      }
    }

    /// <summary> Validates all settings; returns human-readable errors (empty = valid). </summary>
    public List<string> Validate() {
      var errors = new List<string>();

      if (string.IsNullOrWhiteSpace(WorkspaceDir)) {
        errors.Add("workspaceDir must not be empty.");
      }
      if (string.IsNullOrEmpty(AdminPassword)) {
        errors.Add("adminPassword must be set.");
      }

      if (!string.IsNullOrWhiteSpace(ClientUrl) && !IsSafeHttpOrRelativeUrl(ClientUrl)) {
        errors.Add("clientUrl must be an http:// or https:// URL.");
      }
      if (!string.IsNullOrWhiteSpace(WsUrl) && !IsSafeWsOrHttpUrl(WsUrl)) {
        errors.Add("wsUrl must be a ws://, wss://, http://, or https:// URL.");
      }

      GameRules ??= new GameRulesConfig();
      GameRules.TileSet ??= new TileSetConfig();
      Prompts ??= new PromptConfig();
      Decision ??= new DecisionConfig();
      Run ??= new RunConfig();
      Rating ??= new RatingConfig();

      if (GameRules.TileSet.ExtraWildcards is < 0 or > 64) {
        errors.Add("gameRules.tileSet.extraWildcards must be in [0, 64].");
      }
      if (GameRules.LowerPoints > GameRules.InitialPoints || GameRules.InitialPoints > GameRules.UpperPoints) {
        errors.Add("gameRules.initialPoints must lie within [lowerPoints, upperPoints].");
      }
      if (GameRules.AllowedYakus == null || GameRules.AllowedYakus.Count == 0) {
        errors.Add("At least one yaku must be enabled in gameRules.allowedYakus.");
      } else {
        var invalidYaku = GameRules.AllowedYakus
            .Where(y => !ArenaMetadata.YakuByName.ContainsKey(y))
            .ToList();
        if (invalidYaku.Count > 0) {
          errors.Add($"Unknown yaku name(s): {string.Join(", ", invalidYaku)}.");
        }
      }

      try {
        GameRules.BuildGameConfig(Math.Clamp(Decision.TimeoutSeconds, 5, 3600));
      } catch (InvalidGameConfigException ex) {
        errors.Add($"Invalid game rules ({ex.ErrorType}): {ex.Message}");
      } catch (Exception ex) {
        errors.Add($"Invalid game rules: {ex.Message}");
      }

      var validPromptLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "en", "zhs", "zht", "ja",
      };
      if (string.IsNullOrWhiteSpace(Prompts.Language) ||
          !validPromptLanguages.Contains(Prompts.Language.Trim())) {
        errors.Add("prompts.language must be 'en', 'zhs', 'zht', or 'ja'.");
      }

      var validToolModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "minimal", "standard", "extended",
      };
      if (!validToolModes.Contains(Prompts.ToolContextMode ?? "")) {
        errors.Add("prompts.toolContextMode must be 'minimal', 'standard', or 'extended'.");
      }
      if (Prompts.MaxHistoryTurns is < 0 or > 100) {
        errors.Add("prompts.maxHistoryTurns must be in [0, 100].");
      }
      if ((Prompts.CustomSystemAppendix ?? "").Length > 8000) {
        errors.Add("prompts.customSystemAppendix must be <= 8000 characters.");
      }

      if (Decision.TimeoutSeconds is < 5 or > 600) {
        errors.Add("decision.timeoutSeconds must be in [5, 600].");
      }
      if (Decision.MaxRetries is < 0 or > 5) {
        errors.Add("decision.maxRetries must be in [0, 5].");
      }
      if (Decision.MaxOutputTokens is < 64 or > 16384) {
        errors.Add("decision.maxOutputTokens must be in [64, 16384].");
      }

      var validScheduleModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "swiss", "balanced",
      };
      if (!validScheduleModes.Contains(Run.ScheduleMode ?? "")) {
        errors.Add("run.scheduleMode must be 'swiss' or 'balanced'.");
      }
      if (Run.SwissRounds is < 1 or > 200) {
        errors.Add("run.swissRounds must be in [1, 200].");
      }
      if (Run.TargetMatches is < 1 or > 1000) {
        errors.Add("run.targetMatches must be in [1, 1000].");
      }
      if (Run.MatchConcurrency is < 1 or > 16) {
        errors.Add("run.matchConcurrency must be in [1, 16].");
      }

      if (Rating.KFactor <= 0 || Rating.KFactor > 200) {
        errors.Add("rating.kFactor must be in (0, 200].");
      }
      if (Rating.Scale <= 0 || Rating.Scale > 5000) {
        errors.Add("rating.scale must be in (0, 5000].");
      }

      Models ??= [];
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var validProviders = ArenaMetadata.Providers.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
      var validThinking = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "minimal", "low", "medium", "high",
      };
      var validVariants = ArenaMetadata.BaselineVariants.Select(v => v.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

      foreach (var m in Models) {
        if (string.IsNullOrWhiteSpace(m.Id) || !SafeIdRegex.IsMatch(m.Id)) {
          errors.Add($"Model id '{m.Id}' must match [A-Za-z0-9_-]{{1,64}}.");
          continue;
        }
        if (!seen.Add(m.Id)) {
          errors.Add($"Duplicate model id '{m.Id}'.");
        }
        if (string.IsNullOrWhiteSpace(m.DisplayName)) {
          errors.Add($"Model '{m.Id}' is missing displayName.");
        }
        if (string.IsNullOrWhiteSpace(m.Provider) || !validProviders.Contains(m.Provider)) {
          errors.Add($"Model '{m.Id}' has unknown provider '{m.Provider}' (expected openai, gemini, grok, anthropic, or baseline).");
          continue;
        }
        if (m.IsBaseline) {
          var v = string.IsNullOrWhiteSpace(m.Variant) ? "balanced" : m.Variant;
          if (!validVariants.Contains(v) && !string.Equals(v, "default", StringComparison.OrdinalIgnoreCase)) {
            errors.Add($"Baseline model '{m.Id}' has unknown variant '{m.Variant}'.");
          }
        } else {
          if (string.IsNullOrWhiteSpace(m.Model)) {
            errors.Add($"Model '{m.Id}' (provider '{m.Provider}') requires model name.");
          }
          if (!string.IsNullOrWhiteSpace(m.ThinkingLevel) && !validThinking.Contains(m.ThinkingLevel)) {
            errors.Add($"Model '{m.Id}' has invalid thinkingLevel '{m.ThinkingLevel}'.");
          }
          if (!string.IsNullOrWhiteSpace(m.PromptLanguage) &&
              !validPromptLanguages.Contains(m.PromptLanguage.Trim())) {
            errors.Add($"Model '{m.Id}' has invalid promptLanguage '{m.PromptLanguage}' (expected 'en', 'zhs', 'zht', or 'ja').");
          }
          if (!string.IsNullOrWhiteSpace(m.BaseUrl) && !IsSafeHttpOrRelativeUrl(m.BaseUrl, allowRelative: false)) {
            errors.Add($"Model '{m.Id}' baseUrl must be an http:// or https:// URL.");
          }
          if (m.MaxOutputTokens is < 64 or > 32768) {
            errors.Add($"Model '{m.Id}' maxOutputTokens must be in [64, 32768] when set.");
          }
          if (m.Temperature is < 0.0 or > 2.0) {
            errors.Add($"Model '{m.Id}' temperature must be in [0.0, 2.0] when set.");
          }
        }
      }

      return errors;
    }

    public static bool IsSafeHttpOrRelativeUrl(string raw, bool allowRelative = false) {
      if (string.IsNullOrWhiteSpace(raw)) return false;
      var s = raw.Trim();
      if (allowRelative && s.StartsWith('/') && !s.StartsWith("//")) return true;
      return Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static bool IsSafeWsOrHttpUrl(string raw) {
      if (string.IsNullOrWhiteSpace(raw)) return false;
      return Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) &&
             (uri.Scheme == "ws" || uri.Scheme == "wss" ||
              uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static ArenaConfig Load(string path) {
      var json = File.ReadAllText(path);
      return Parse(json);
    }

    public static ArenaConfig Parse(string json) =>
        JsonSerializer.Deserialize<ArenaConfig>(json, JsonOpts) ?? new ArenaConfig();

    public void Save(string path) {
      var clean = Clone();
      foreach (var m in clean.Models) {
        m.HasApiKey = null;
        m.ClearApiKey = null;
      }
      AtomicFile.WriteAllText(path, JsonSerializer.Serialize(clean, JsonOpts));
    }
  }
}
