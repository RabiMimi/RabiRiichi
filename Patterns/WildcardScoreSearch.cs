using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// Searches canonical target groups, not wildcard assignments. Only the best
  /// witness is retained. All state belongs to a single query or preview batch.
  /// </summary>
  internal sealed partial class WildcardScoreSearch {
    private sealed class Query {
      internal GameTile Incoming;
      internal int Index, Aka, Dora, Ura, Context;
      internal bool Found;
      internal CompactScore Best;
      internal BasePattern Family;
      internal CompactMeld[] Groups;
      internal int Role, EffectiveIndex;
    }
    private static readonly CompactMeld[] melds = Enumerable.Range(0, 34)
        .Select(i => new CompactMeld(CompactMeldKind.Triplet, i, true))
        .Concat(Enumerable.Range(0, 3).SelectMany(s => Enumerable.Range(0, 7)
            .Select(r => new CompactMeld(CompactMeldKind.Sequence, s * 9 + r, true)))).ToArray();
    private readonly PatternResolver resolver;
    private readonly CompiledScoringEvaluator evaluator;
    private readonly Hand hand;
    private readonly Query[] queries;
    private readonly bool[] activeQueries;
    private readonly Query[] optimisticQueries;
    private bool floorDirty = true, hasFloor;
    private CompactScore floor;
    private readonly PatternMask mask, excluded;
    private readonly int minHan;
    private readonly BasePattern onlyFamily;
    private readonly int[] natural = new int[34], required = new int[34];
    private readonly byte[] targets = new byte[34], freeTargets = new byte[34];
    private readonly CompactMeld[] groups = new CompactMeld[14];
    private readonly CompactMeld[] fixedGroups;
    private readonly byte[] lockedCounts;
    private StdPattern[] boundPatterns;
    private YakuRule[] boundRules;
    private RuleYaku[] boundCustom;
    private readonly PartialView partialView = new();
    private int akaFactor, doraFactor, uraFactor, nukiFactor;
    private int count;
    private long visitedTargets, evaluatedInterpretations;

    internal WildcardScoreSearch(PatternResolver resolver, CompiledScoringEvaluator evaluator, Hand hand,
        GameTile[] incoming, PatternMask mask, PatternMask excluded, int minHan, BasePattern onlyFamily = null) {
      this.resolver = resolver; this.evaluator = evaluator; this.hand = hand;
      this.mask = mask; this.excluded = excluded; this.minHan = minHan;
      this.onlyFamily = onlyFamily;
      foreach (var tile in hand.freeTiles) if (!tile.tile.IsWildcard) natural[CompactMeld.Index(tile.tile)]++;
      natural.CopyTo(required, 0);
      if (incoming.Select(t => t.tile.WithoutDora).Distinct().Count() == 1 && !incoming[0].tile.IsWildcard)
        required[CompactMeld.Index(incoming[0].tile)]++;
      queries = incoming.Select(tile => {
        var b = PatternResolver.PhysicalBonuses(hand, tile);
        return new Query { Incoming = tile, Index = tile.tile.IsWildcard ? -1 : CompactMeld.Index(tile.tile), Aka = b.Aka, Dora = b.Dora, Ura = b.Ura };
      }).ToArray();
      // A wildcard incoming imposes fewer identity constraints than any natural
      // draw. One optimistic query per source context bounds the whole batch.
      optimisticQueries = incoming.GroupBy(t => (t.IsTsumo, t.source, t.discardInfo?.reason, t.discardInfo?.isRiichiDeclaration))
          .Select(g => new Query {
            Index = -1, Incoming = new GameTile(Tile.Wildcard, -1) { source = g.First().source, discardInfo = g.First().discardInfo },
            Aka = queries.Max(q => q.Aka), Dora = queries.Max(q => q.Dora), Ura = queries.Max(q => q.Ura),
          }).ToArray();
      foreach (var q in queries) q.Context = Array.FindIndex(optimisticQueries, o =>
          o.Incoming.IsTsumo == q.Incoming.IsTsumo && o.Incoming.source == q.Incoming.source
          && o.Incoming.discardInfo?.reason == q.Incoming.discardInfo?.reason
          && o.Incoming.discardInfo?.isRiichiDeclaration == q.Incoming.discardInfo?.isRiichiDeclaration);
      fixedGroups = hand.called.Select(g => PatternResolver.ToCompact(g) with { IsCalled = true }).ToArray();
      activeQueries = new bool[queries.Length];
      lockedCounts = PatternResolver.CountTargets(fixedGroups);
    }

    internal ScoreStorage[] Run() {
      if (hand.Count != Game.HAND_SIZE) return new ScoreStorage[queries.Length];
      if (!evaluator.IsSupported)
        throw new NotSupportedException("Wildcard scoring needs a built-in rule or a RuleYaku for every registered pattern and dependency; unsupported: "
            + string.Join(", ", evaluator.UnsupportedPatterns.Select(p => p.name)));
      if (hand.called.Any(g => g.Any(t => t.tile.IsWildcard)))
        throw new ArgumentException("Wildcards cannot be locked in called melds", nameof(hand));
      var reachable = new HashSet<StdPattern>();
      void Reach(StdPattern p) {
        if ((p.type & excluded) != 0 || !reachable.Add(p)) return;
        foreach (var parent in p.afterPatterns) Reach(parent);
      }
      foreach (var p in resolver.stdPatterns) if ((p.type & mask) != 0) Reach(p);
      foreach (var family in resolver.basePatterns) {
        if (onlyFamily != null && family != onlyFamily) continue;
        boundPatterns = resolver.stdPatterns.Where(p => reachable.Contains(p) && p.basePatterns.Contains(family) && (p.type & excluded) == 0).ToArray();
        boundRules = boundPatterns.Select(p => p is RuleYaku ? YakuRule.Unknown : Enum.Parse<YakuRule>(p.GetType().Name)).ToArray();
        boundCustom = boundPatterns.OfType<RuleYaku>().ToArray();
        akaFactor = boundRules.Count(r => r == YakuRule.Akadora);
        doraFactor = boundRules.Count(r => r == YakuRule.Dora);
        uraFactor = hand.riichi ? boundRules.Count(r => r == YakuRule.Uradora) : 0;
        nukiFactor = boundRules.Count(r => r == YakuRule.NukiDora);
        Reset();
        switch (family) {
          case Base33332: Regular(family); break;
          case Base72: SevenPairs(family); break;
          case Base13_1: Orphans(family); break;
          case BaseButa: Buta(family); break;
          default: throw new NotSupportedException("Custom base patterns require a wildcard target generator");
        }
      }
      var result = new ScoreStorage[queries.Length];
      for (int i = 0; i < queries.Length; i++) {
        var q = queries[i]; if (!q.Found) continue;
        evaluator.Evaluate(q.Family, hand, q.Incoming, q.Groups, PatternResolver.CountTargets(q.Groups),
            q.Role, q.EffectiveIndex, mask, excluded, q.Aka, q.Dora, q.Ura);
        var scores = evaluator.Materialize(hand.game.config.scoringOption);
        scores.resolution = new HandResolution(hand, q.Incoming, q.Groups, q.Role, q.EffectiveIndex) {
          VisitedTargets = visitedTargets, EvaluatedInterpretations = evaluatedInterpretations,
        };
        result[i] = scores;
      }
      return result;
    }

    private void Reset() {
      count = 0; Array.Clear(targets); Array.Clear(freeTargets);
      Array.Fill(activeQueries, true);
      foreach (var g in fixedGroups) { groups[count++] = g; Change(g, 1, false); }
    }

    private void Change(CompactMeld g, int amount, bool free = true) {
      for (int pos = 0; pos < g.Count; pos++) {
        int i = g.TileIndex + (g.Kind == CompactMeldKind.Sequence ? pos : 0);
        targets[i] = (byte)(targets[i] + amount);
        if (free) freeTargets[i] = (byte)(freeTargets[i] + amount);
      }
    }
    private void Push(CompactMeld g) { groups[count++] = g; Change(g, 1); }
    private void Pop() { var g = groups[--count]; Change(g, -1); }

    private bool Covered() {
      for (int i = 0; i < 34; i++) if (freeTargets[i] < required[i]) return false;
      return true;
    }

    private bool CanCover(int remainingMelds, int remainingPairs, int firstMeld) {
      int missing = 0;
      for (int i = 0; i < 34; i++) {
        int n = required[i] - freeTargets[i]; if (n <= 0) continue;
        missing += n;
        int max = firstMeld <= i ? 3 : i < 27 && firstMeld <= 34 + (i / 9) * 7 + Math.Min(6, i % 9) ? 1 : 0;
        if (n > remainingMelds * max + remainingPairs * 2) return false;
      }
      return missing <= remainingMelds * 3 + remainingPairs * 2;
    }

    private void Regular(BasePattern family) {
      if (fixedGroups.Any(g => g.Kind is CompactMeldKind.Single)) return;
      int pairs = fixedGroups.Count(g => g.Kind == CompactMeldKind.Pair);
      int needed = 4 - fixedGroups.Count(g => g.Kind != CompactMeldKind.Pair);
      if (pairs > 1 || needed < 0) return;
      Seed(family);
      if (pairs == 1) SearchMelds(family, needed, 0);
      else for (int pair = 0; pair < 34; pair++) {
        Push(new(CompactMeldKind.Pair, pair, true));
        SearchMelds(family, needed, 0); Pop();
      }
    }

    private void SearchMelds(BasePattern family, int remaining, int start) {
      Span<bool> saved = stackalloc bool[activeQueries.Length];
      activeQueries.CopyTo(saved);
      try {
        if (!CanCover(remaining, 0, start) || CanPrune(family, remaining)) return;
        if (remaining == 0) { Visit(family); return; }
        for (int i = start; i < melds.Length; i++) {
          Push(melds[i]); SearchMelds(family, remaining - 1, i); Pop();
        }
      } finally {
        saved.CopyTo(activeQueries);
      }
    }

    private void Seed(BasePattern family) {
      // Strong early incumbents for many-wildcard hands; every seed is still
      // checked against naturals and every legal incoming-tile role.
      for (int pair = 0; pair < 34; pair++) SeedTarget(family, pair, [27, 28, 29, 30]);
      for (int i = 0; i < 34; i++) SeedTarget(family, i, [i, i, i, i]);
      for (int i = 34; i < 55; i++) SeedTarget(family, melds[i].TileIndex + 1, [i, i, i, i]);
      if ((hand.game.config.scoringOption & (ScoringOption.Yakuman | ScoringOption.MultipleYakuman)) == ScoringOption.Yakuman) {
        // With a single-yakuman cap, han breaks many ties. Repeated value
        // triplets with a suited pair establish a much stronger incumbent than
        // all-honor yakuman targets (whose yaku suppress several han awards).
        int honor = 27, bestWeight = -1;
        for (int i = 27; i < 34; i++) {
          int weight = 0;
          foreach (var rule in boundRules) weight += rule switch {
            YakuRule.YakuhaiBakaze when i == 27 + (int)hand.game.info.wind => 1,
            YakuRule.YakuhaiJikaze when i == 27 + (int)hand.player.Wind => 1,
            YakuRule.YakuhaiHaku when i == 31 => 1,
            YakuRule.YakuhaiHatsu when i == 32 => 1,
            YakuRule.YakuhaiChun when i == 33 => 1,
            _ => 0,
          };
          if (weight > bestWeight) { bestWeight = weight; honor = i; }
        }
        for (int pair = 0; pair < 34; pair++) {
          for (int i = 0; i < 34; i++) SeedTarget(family, pair, [i, i, i, i]);
          for (int other = 0; other < 34; other++) SeedTarget(family, pair, [honor, honor, honor, other]);
        }
      }
    }

    private void SeedTarget(BasePattern family, int pair, int[] ids) {
      int start = count;
      var available = ids.Select(i => melds[i]).ToList();
      bool hasPair = false;
      foreach (var g in fixedGroups) {
        if (g.Kind == CompactMeldKind.Pair) { if (g.TileIndex != pair) return; hasPair = true; }
        else {
          int i = available.FindIndex(m => m.TileIndex == g.TileIndex && (m.Kind == g.Kind || g.Kind == CompactMeldKind.Quad && m.Kind == CompactMeldKind.Triplet));
          if (i < 0) return;
          available.RemoveAt(i);
        }
      }
      if (!hasPair) Push(new(CompactMeldKind.Pair, pair, true));
      foreach (var g in available) Push(g);
      if (Covered()) Visit(family);
      while (count > start) Pop();
    }

    private void SevenPairs(BasePattern family) {
      if (fixedGroups.Any(g => g.Kind != CompactMeldKind.Pair) || fixedGroups.Select(g => g.TileIndex).Distinct().Count() != fixedGroups.Length) return;
      if (required.Any(n => n > 2)) return;
      SeedPairs(family, Enumerable.Range(27, 7).ToArray());
      for (int suit = 0; suit < 3; suit++) for (int start = 0; start < 3; start++)
        SeedPairs(family, Enumerable.Range(suit * 9 + start, 7).ToArray());
      var used = fixedGroups.Select(g => g.TileIndex).ToHashSet();
      var forced = Enumerable.Range(0, 34).Where(i => required[i] > 0).ToArray();
      if (forced.Any(used.Contains) || forced.Length + count > 7) return;
      foreach (int i in forced) { Push(new(CompactMeldKind.Pair, i, true)); used.Add(i); }
      SearchPairs(family, 7 - count, 0, used);
    }

    private void SeedPairs(BasePattern family, int[] indices) {
      if (fixedGroups.Any(g => !indices.Contains(g.TileIndex))) return;
      int start = count;
      foreach (int i in indices) if (!fixedGroups.Any(g => g.TileIndex == i)) Push(new(CompactMeldKind.Pair, i, true));
      if (Covered()) Visit(family);
      while (count > start) Pop();
    }

    private void SearchPairs(BasePattern family, int remaining, int start, HashSet<int> used) {
      Span<bool> saved = stackalloc bool[activeQueries.Length];
      activeQueries.CopyTo(saved);
      try {
        if (CanPrune(family, 0)) return;
        if (remaining == 0) { if (Covered()) Visit(family); return; }
        for (int i = start; i < 34; i++) if (!used.Contains(i)) {
          Push(new(CompactMeldKind.Pair, i, true));
          SearchPairs(family, remaining - 1, i + 1, used); Pop();
        }
      } finally {
        saved.CopyTo(activeQueries);
      }
    }

    private void Orphans(BasePattern family) {
      if (fixedGroups.Length > 1 || fixedGroups.Any(g => g.Kind != CompactMeldKind.Pair || !CompactMeld.TileAt(g.TileIndex).Is19Z)) return;
      for (int duplicate = 0; duplicate < 34; duplicate++) {
        if (!CompactMeld.TileAt(duplicate).Is19Z || fixedGroups.Length == 1 && fixedGroups[0].TileIndex != duplicate) continue;
        int start = count;
        for (int i = 0; i < 34; i++) if (CompactMeld.TileAt(i).Is19Z && !(fixedGroups.Length == 1 && i == duplicate))
          Push(new(i == duplicate ? CompactMeldKind.Pair : CompactMeldKind.Single, i, true));
        if (Covered()) Visit(family);
        while (count > start) Pop();
      }
    }

    private void Buta(BasePattern family) {
      if (fixedGroups.Length != 0 || !hand.game.IsFirstJun || !queries.Any(q => q.Incoming.IsTsumo)) return;
      if (!resolver.stdPatterns.Any(p => p.basePatterns.Contains(family) && (p.type & mask) != 0 && (p.type & excluded) == 0)) return;
      SearchSingles(family, 0, 14, false, -10);
    }
    private void SearchSingles(BasePattern family, int i, int remaining, bool pair, int lastSuited) {
      Span<bool> saved = stackalloc bool[activeQueries.Length];
      activeQueries.CopyTo(saved);
      try {
        if (remaining == 0) { if (Covered()) Visit(family); return; }
        if (i == 34 || remaining > 34 - i + (pair ? 0 : 1) || CanPrune(family, 0)) return;
        bool related = i < 27 && lastSuited >= 0 && lastSuited / 9 == i / 9 && i - lastSuited <= 2;
        if (!related) for (int copies = 1; copies <= (pair ? 1 : 2); copies++) {
          if (copies > remaining) continue;
          Push(new(copies == 2 ? CompactMeldKind.Pair : CompactMeldKind.Single, i, true));
          SearchSingles(family, i + 1, remaining - copies, pair || copies == 2, i < 27 ? i : lastSuited); Pop();
        }
        if (required[i] == 0) SearchSingles(family, i + 1, remaining, pair, lastSuited);
      } finally {
        saved.CopyTo(activeQueries);
      }
    }

    private void Visit(BasePattern family) {
      if (!Covered()) return;
      visitedTargets++;
      for (int queryIndex = 0; queryIndex < queries.Length; queryIndex++) {
        if (!activeQueries[queryIndex]) continue;
        var q = queries[queryIndex];
        if (family is BaseButa && !q.Incoming.IsTsumo) continue;
        if (q.Index >= 0 && freeTargets[q.Index] <= natural[q.Index]) continue;
        for (int role = fixedGroups.Length; role < count; role++) {
          var g = groups[role]; bool repeated = false;
          for (int prev = fixedGroups.Length; prev < role; prev++) if (groups[prev] == g) { repeated = true; break; }
          if (repeated) continue;
          int positions = g.Kind == CompactMeldKind.Sequence ? 3 : 1;
          for (int pos = 0; pos < positions; pos++) {
            int index = g.TileIndex + pos;
            if (q.Index >= 0 ? index != q.Index : freeTargets[index] <= natural[index]) continue;
            evaluatedInterpretations++;
            var score = evaluator.Evaluate(family, hand, q.Incoming, groups.AsSpan(0, count), targets, role, index,
                mask, excluded, q.Aka, q.Dora, q.Ura);
            if (!score.IsValid(minHan) || q.Found && ScoreMath.Compare(score, q.Best, hand.game.config.scoringOption) <= 0) continue;
            q.Found = true; q.Best = score; q.Family = family;
            floorDirty = true;
            q.Groups = groups.AsSpan(0, count).ToArray(); q.Role = role; q.EffectiveIndex = index;
          }
        }
      }
    }

  }
}
