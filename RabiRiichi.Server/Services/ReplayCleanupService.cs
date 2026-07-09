using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RabiRiichi.Server.Services {
  public class ReplayCleanupService(ReplayOptions options, ILogger<ReplayCleanupService> logger) : BackgroundService {
    private readonly ReplayOptions options = options;
    private readonly ILogger<ReplayCleanupService> logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
      if (!options.IsEnabled || !options.TTL.HasValue) {
        logger.LogInformation("Replay cleanup service is disabled (no save dir or TTL not set).");
        return;
      }

      logger.LogInformation("Replay cleanup service started with TTL {TTL} seconds.", options.TTL.Value);

      using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
      while (!stoppingToken.IsCancellationRequested) {
        try {
          CleanOldReplays();
        } catch (Exception ex) {
          logger.LogError(ex, "Error occurred during replay cleanup.");
        }

        try {
          await timer.WaitForNextTickAsync(stoppingToken);
        } catch (OperationCanceledException) {
          break;
        }
      }
    }

    private void CleanOldReplays() {
      if (!Directory.Exists(options.SaveDir)) {
        return;
      }

      var now = DateTime.UtcNow;
      var ttlDuration = TimeSpan.FromSeconds(options.TTL.Value);
      var files = Directory.GetFiles(options.SaveDir, "*.pb");
      int deletedCount = 0;

      foreach (var file in files) {
        try {
          var fileInfo = new FileInfo(file);
          if (now - fileInfo.LastWriteTimeUtc > ttlDuration) {
            File.Delete(file);
            deletedCount++;
          }
        } catch (Exception ex) {
          logger.LogWarning(ex, "Failed to delete old replay file: {File}", file);
        }
      }

      if (deletedCount > 0) {
        logger.LogInformation("Deleted {Count} expired replay files.", deletedCount);
      }
    }
  }
}
