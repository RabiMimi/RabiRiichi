using RabiRiichi.Core;
using RabiRiichi.Patterns;


namespace RabiRiichi.Actions.Resolver {
  public class ChanAnkanResolver(PatternResolver patternResolver, Base13_1 base13_1) : ChankanResolver(patternResolver) {
    private readonly Base13_1 base13_1 = base13_1;

    // A wildcard holding may also fit an ordinary shape. Robbing a concealed
    // kan is scored specifically as kokushi, rather than its best other shape.
    protected override ScoreStorage CalculateScore(Player player, GameTile incoming) =>
        patternResolver.ResolveBestValidScoreForFamily(player.hand, incoming, base13_1,
            PatternMask.All, player.game.config.minHan);
  }
}
