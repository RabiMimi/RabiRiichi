using RabiRiichi.Core;
using RabiRiichi.Patterns;


namespace RabiRiichi.Actions.Resolver {
  public class ChanAnkanResolver(PatternResolver patternResolver, Base13_1 base13_1) : ChankanResolver(patternResolver) {
    private readonly Base13_1 base13_1 = base13_1;

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      // 暗杠，判定国士
      return !base13_1.Resolve(player.hand, incoming, out _) ? false : base.ResolveAction(player, incoming, output);
    }
  }
}