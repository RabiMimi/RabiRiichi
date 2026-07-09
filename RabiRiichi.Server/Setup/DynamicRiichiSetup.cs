using RabiRiichi.Core.Setup;

namespace RabiRiichi.Server.Setup {
  public class DynamicRiichiSetup(string[] allowedYakus, ILogger logger = null) : RiichiSetup {

    protected override void InitPatterns() {
      base.InitPatterns();
      if (allowedYakus != null) {
        var allowed = new HashSet<string>(allowedYakus);
        logger?.LogInformation("Allowed yakus from client: {Allowed}", string.Join(", ", allowedYakus));

        var serverYakus = stdPatterns.Where(IsYaku).Select(t => t.Name).ToHashSet();
        var invalidYakus = allowed.Where(name => !serverYakus.Contains(name)).ToList();
        if (invalidYakus.Count > 0) {
          logger?.LogWarning("Invalid allowed yakus from client (not found on server): {Invalid}", string.Join(", ", invalidYakus));
        }

        var toRemove = stdPatterns
          .Where(type => IsYaku(type) && !allowed.Contains(type.Name))
          .ToList();
        foreach (var type in toRemove) {
          RemoveStdPattern(type);
        }

        var preserved = stdPatterns.Where(IsYaku).Select(t => t.Name).ToList();
        logger?.LogInformation("Preserved yakus on server: {Preserved}", string.Join(", ", preserved));
      } else {
        logger?.LogInformation("No allowed yakus list provided. All standard yakus preserved.");
      }
    }
  }
}
