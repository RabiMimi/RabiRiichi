using RabiRiichi.Core.Setup;

namespace RabiRiichi.Server.Setup {
  public class DynamicRiichiSetup(string[] allowedYakus, ILogger logger = null) : RiichiSetup {

    protected override void InitPatterns() {
      base.InitPatterns();
      if (allowedYakus == null) {
        logger?.LogInformation("No allowed yakus list provided. All standard yakus preserved, no koyaku enabled.");
        return;
      }

      var allowed = new HashSet<string>(allowedYakus);
      logger?.LogInformation("Allowed yakus from client: {Allowed}", string.Join(", ", allowedYakus));

      // 古役不在RiichiSetup中注册，这里先全部加入，再与标准役一起按allowedYakus过滤
      foreach (var type in KoyakuPatterns.ALL) {
        AddStdPattern(type);
      }

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
    }
  }
}
