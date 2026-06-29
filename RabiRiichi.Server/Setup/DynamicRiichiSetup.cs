using RabiRiichi.Core.Setup;

namespace RabiRiichi.Server.Setup {
  public class DynamicRiichiSetup(string[] allowedYakus) : RiichiSetup {

    protected override void InitPatterns() {
      base.InitPatterns();
      if (allowedYakus != null) {
        var allowed = new HashSet<string>(allowedYakus);
        var toRemove = stdPatterns
          .Where(type => IsYaku(type) && !allowed.Contains(type.Name))
          .ToList();
        foreach (var type in toRemove) {
          RemoveStdPattern(type);
        }
      }
    }
  }
}
