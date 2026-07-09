using System;
using System.IO;
using System.Text.RegularExpressions;
using Google.Protobuf;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Server.Services {
  public class ReplayOptions {
    public string SaveDir { get; }
    public int? TTL { get; }

    public ReplayOptions(string saveDir, int? ttl) {
      SaveDir = saveDir;
      TTL = ttl;
    }

    public ReplayOptions() : this(
      Environment.GetEnvironmentVariable("RABIRIICHI_GAME_SAVE_DIR"),
      GetTtlFromEnv()
    ) {}

    private static int? GetTtlFromEnv() {
      var ttlStr = Environment.GetEnvironmentVariable("RABIRIICHI_GAME_SAVE_TTL");
      return int.TryParse(ttlStr, out int ttl) && ttl > 0 ? ttl : null;
    }

    public bool IsEnabled => !string.IsNullOrEmpty(SaveDir);
  }

  public class ReplayStore(ReplayOptions options) {
    private readonly ReplayOptions options = options;

    public bool IsEnabled => options.IsEnabled;
    public string SaveDir => options.SaveDir;

    public void SaveReplay(string gameId, GameLogMsg replay) {
      if (!IsEnabled) return;
      if (!IsValidGameId(gameId)) {
        throw new ArgumentException("Invalid game ID", nameof(gameId));
      }
      Directory.CreateDirectory(options.SaveDir);
      var path = Path.Combine(options.SaveDir, $"{gameId}.pb");
      using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
      replay.WriteTo(stream);
    }

    public GameLogMsg GetReplay(string gameId) {
      if (!IsEnabled) return null;
      if (!IsValidGameId(gameId)) {
        return null;
      }
      var path = Path.Combine(options.SaveDir, $"{gameId}.pb");
      if (!File.Exists(path)) {
        return null;
      }
      using var stream = File.OpenRead(path);
      return GameLogMsg.Parser.ParseFrom(stream);
    }

    private static readonly Regex GameIdRegex = new(@"^[0-9A-Za-z-]+$");
    public static bool IsValidGameId(string gameId) {
      return !string.IsNullOrEmpty(gameId) && GameIdRegex.IsMatch(gameId);
    }
  }
}
