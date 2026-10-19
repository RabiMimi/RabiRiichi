using RabiRiichi.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public partial class PatternResolver {
    private readonly object planLock = new();
    private StdPattern[] plannedPatterns = [];
    private CompiledScoringEvaluator scoringPlan;

    private CompiledScoringEvaluator GetPlan() {
      lock (planLock) {
        if (scoringPlan == null || !plannedPatterns.SequenceEqual(stdPatterns)) {
          plannedPatterns = stdPatterns.ToArray();
          scoringPlan = new CompiledScoringEvaluator(plannedPatterns);
        }
        return scoringPlan;
      }
    }
    // The plan is shared; each query gets its own scratch state.
    private CompiledScoringEvaluator GetEvaluator() => GetPlan().Fork();
    internal bool CanShareScoring => GetPlan().IsSupported;

    private static bool HasWildcardSearch(BasePattern family) => family is Base33332 or Base72 or Base13_1 or BaseButa;

    /// <summary>
    /// Wildcard scoring has no legacy fallback, so check the pattern set up front.
    /// Custom yaku must be <see cref="RuleYaku"/>s, whose conditions bound the search.
    /// </summary>
    internal void EnsureWildcardSupport() {
      var unsupported = GetPlan().UnsupportedPatterns.Select(p => p.name)
          .Concat(basePatterns.Where(p => !HasWildcardSearch(p)).Select(p => p.GetType().Name)).ToList();
      if (unsupported.Count > 0) {
        throw new NotSupportedException(
            $"Wildcard tiles require built-in scoring rules or RuleYaku; unsupported patterns: {string.Join(", ", unsupported)}");
      }
    }

    internal ScoreStorage ResolveBestValidScoreForFamily(Hand hand, GameTile incoming, BasePattern family,
        PatternMask mask, int minHan) => ResolveScore(hand, incoming, mask, PatternMask.None, minHan, family);

    /// <summary>Maximize over interpretations satisfying the yaku requirement.</summary>
    public ScoreStorage ResolveBestValidScore(Hand hand, GameTile incoming, PatternMask patternMask,
        int minHan, PatternMask excludedMask = PatternMask.None) {
      if (minHan < 0) throw new ArgumentOutOfRangeException(nameof(minHan));
      return ResolveScore(hand, incoming, patternMask, excludedMask, minHan);
    }

    private ScoreStorage ResolveScore(Hand hand, GameTile incoming, PatternMask mask, PatternMask excluded, int minHan,
        BasePattern onlyFamily = null) {
      ArgumentNullException.ThrowIfNull(hand);
      if (incoming == null) throw new ArgumentNullException(nameof(incoming), "Scoring requires the physical winning tile");
      if (hand.Count != Game.HAND_SIZE) return null;
      var evaluator = GetEvaluator();
      if (HandShapeAnalyzer.HasWildcards(hand, incoming)) {
        return new WildcardScoreSearch(this, evaluator, hand, [incoming], mask, excluded, minHan, onlyFamily).Run()[0];
      }
      if (!evaluator.IsSupported) return ResolveMaxScoreLegacy(hand, incoming, mask, excluded, minHan, onlyFamily);
      CompactScore best = default; bool found = false;
      CompactMeld[] bestGroups = null; BasePattern bestFamily = null; int bestRole = 0;
      int index = CompactMeld.Index(incoming.tile);
      var bonus = PhysicalBonuses(hand, incoming);
      foreach (var family in basePatterns) {
        if (onlyFamily != null && family != onlyFamily) continue;
        if (!family.Resolve(hand, incoming, out var decompositions)) continue;
        foreach (var groups in decompositions) {
          var compact = groups.Select(ToCompact).ToArray();
          int role = groups.FindIndex(g => g.Contains(incoming));
          if (role < 0) continue;
          var counts = CountTargets(compact);
          var score = evaluator.Evaluate(family, hand, incoming, compact, counts, role, index,
              mask, excluded, bonus.Aka, bonus.Dora, bonus.Ura);
          if (!score.IsValid(minHan) || found && ScoreMath.Compare(score, best, hand.game.config.scoringOption) <= 0) continue;
          found = true; best = score; bestGroups = compact; bestFamily = family; bestRole = role;
        }
      }
      if (!found) return null;
      evaluator.Evaluate(bestFamily, hand, incoming, bestGroups, CountTargets(bestGroups), bestRole, index,
          mask, excluded, bonus.Aka, bonus.Dora, bonus.Ura);
      return evaluator.Materialize(hand.game.config.scoringOption);
    }

    /// <summary>Share target exploration across all waits and both win sources.</summary>
    public Dictionary<Tile, (ScoreStorage Ron, ScoreStorage Tsumo)> ResolveScorePreviews(
        Hand hand, Tiles winningTiles, PatternMask patternMask, PatternMask excludedMask = PatternMask.None) {
      var result = new Dictionary<Tile, (ScoreStorage Ron, ScoreStorage Tsumo)>();
      // A natural hand scores natural waits on the ordinary path (including the
      // custom-rule fallback); only wildcard holdings or waits need the batch search.
      bool wildHand = hand.freeTiles.Any(t => t.tile.IsWildcard);
      var tiles = winningTiles.Distinct().ToArray();
      var batched = tiles.Where(t => wildHand || t.IsWildcard).ToArray();
      foreach (var tile in tiles.Except(batched)) {
        result[tile] = (ResolveScore(hand, PreviewTile(tile, false), patternMask, excludedMask, 0),
            ResolveScore(hand, PreviewTile(tile, true), patternMask, excludedMask, 0));
      }
      if (batched.Length > 0) {
        var incoming = batched.SelectMany(t => new[] { PreviewTile(t, false), PreviewTile(t, true) }).ToArray();
        var scores = new WildcardScoreSearch(this, GetEvaluator(), hand, incoming, patternMask, excludedMask, 0).Run();
        for (int i = 0; i < batched.Length; i++) result[batched[i]] = (scores[i * 2], scores[i * 2 + 1]);
      }
      return result;
    }

    private static GameTile PreviewTile(Tile tile, bool tsumo) => tsumo
        ? new(tile, -1) { source = Generated.Core.TileSource.Wall }
        : new(tile, -1) { source = Generated.Core.TileSource.Discard,
          discardInfo = new DiscardInfo(null, Generated.Core.DiscardReason.Draw, 0) };

    internal static CompactMeld ToCompact(MenLike group) => new(group switch {
      Shun => CompactMeldKind.Sequence, Kou => CompactMeldKind.Triplet, Kan => CompactMeldKind.Quad,
      Jantou => CompactMeldKind.Pair, _ => CompactMeldKind.Single,
    }, CompactMeld.Index(group.First.tile), group.IsClose);

    internal static byte[] CountTargets(ReadOnlySpan<CompactMeld> groups) {
      var result = new byte[34];
      foreach (var g in groups) for (int pos = 0; pos < g.Count; pos++)
        result[g.TileIndex + (g.Kind == CompactMeldKind.Sequence ? pos : 0)]++;
      return result;
    }

    internal static (int Aka, int Dora, int Ura) PhysicalBonuses(Hand hand, GameTile incoming) {
      int aka = 0, dora = 0, ura = 0;
      foreach (var t in hand.freeTiles.Concat(hand.called.SelectMany(g => g)).Append(incoming)) {
        if (t.tile.IsWildcard) continue;
        if (t.tile.Akadora) aka++;
        dora += hand.game.wall.CountDora(t.tile); ura += hand.game.wall.CountUradora(t.tile);
      }
      foreach (var t in hand.nukiDora) { dora += hand.game.wall.CountDora(t.tile); ura += hand.game.wall.CountUradora(t.tile); }
      return (aka, dora, ura);
    }
  }
}
