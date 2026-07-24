using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// A thin <see cref="IHostedService"/> that ties the <see cref="ArenaService"/>
  /// run loop to the app lifetime WITHOUT auto-starting a run: start is an admin
  /// action (M6), never automatic (ARENA_DESIGN.md §10). All this wrapper does is
  /// make the service available and cleanly stop the loop on shutdown.
  /// </summary>
  public sealed class ArenaHostedService(ArenaService arenaService) : IHostedService {
    private readonly ArenaService arenaService = arenaService;

    public Task StartAsync(CancellationToken cancellationToken) {
      // Intentionally does NOT call arenaService.Start(): runs are admin-driven.
      Logger.Log("[Arena] Hosted service ready (run loop idle until admin Start).");
      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
      return arenaService.StopAsync();
    }
  }
}
