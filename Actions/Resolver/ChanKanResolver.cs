using RabiRiichi.Core;
using RabiRiichi.Patterns;


namespace RabiRiichi.Actions.Resolver {
  public class ChankanResolver(PatternResolver patternResolver) : RonResolver(patternResolver) {
    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      return base.ResolveAction(player, incoming, output);
    }
  }
}