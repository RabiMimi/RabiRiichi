using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
      internal CompactScore BoundOffset;
      internal BasePattern Family;
      internal CompactMeld[] Groups;
      internal int Role, EffectiveIndex;
    }
    private static readonly CompactMeld[] melds = Enumerable.Range(0, 34)
        .Select(i => new CompactMeld(CompactMeldKind.Triplet, i, true))
        .Concat(Enumerable.Range(0, 3).SelectMany(s => Enumerable.Range(0, 7)
            .Select(r => new CompactMeld(CompactMeldKind.Sequence, s * 9 + r, true)))).ToArray();
    /// <summary>For each tile, the ascending <see cref="melds"/> indices of the groups containing it.</summary>
    private static readonly int[][] meldsContaining = Enumerable.Range(0, 34)
        .Select(tile => Enumerable.Range(0, melds.Length).Where(m => melds[m].Kind == CompactMeldKind.Triplet
            ? melds[m].TileIndex == tile
            : tile / 9 == melds[m].TileIndex / 9 && tile - melds[m].TileIndex is >= 0 and <= 2).ToArray()).ToArray();
    private readonly PatternResolver resolver;
    private readonly DiscardScoreCache shared;
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
    private readonly byte[] pairAndWaitFu = new byte[34];
    private readonly ulong mandatoryMask;
    private readonly UInt128 lockedQuadCopies;
    private readonly Partial[] partials = new Partial[15];
    private StdPattern[] boundPatterns;
    private YakuRule[] boundRules;
    private RuleYaku[] boundCustom;
    private readonly PartialView partialView = new();
    private int akaFactor, doraFactor, uraFactor, nukiFactor;
    private int count;
    private long visitedTargets, evaluatedInterpretations, visitedNodes, boundCalculations;

    internal WildcardScoreSearch(PatternResolver resolver, CompiledScoringEvaluator evaluator, Hand hand,
        GameTile[] incoming, PatternMask mask, PatternMask excluded, int minHan, BasePattern onlyFamily = null, DiscardScoreCache shared = null) {
      this.resolver = resolver; this.evaluator = evaluator; this.hand = hand;
      this.mask = mask; this.excluded = excluded; this.minHan = minHan;
      this.onlyFamily = onlyFamily; this.shared = shared;
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
      for (int i = 0; i < 34; i++) {
        if (required[i] != 0 || lockedCounts[i] != 0) mandatoryMask |= 1UL << i;
        var tile = CompactMeld.TileAt(i);
        pairAndWaitFu[i] = (byte)(2 + (hand.game.IsYaku(tile) ? 2 : 0)
            + (tile.IsSame(Tile.From(hand.player.Wind)) ? 2 : 0));
      }
      foreach (var g in fixedGroups) if (g.Kind == CompactMeldKind.Quad)
        lockedQuadCopies += (UInt128)1 << (g.TileIndex * 3);
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
        // Most single queries are ron checks on hands that cannot win at all. The
        // shape solver settles those at once. Butsu has no shanten to ask about.
        if (queries.Length == 1 && family is Base33332 or Base72 or Base13_1
            && family.Shanten(hand, queries[0].Incoming, out _, -1) != -1) continue;
        boundPatterns = resolver.stdPatterns.Where(p => reachable.Contains(p) && p.basePatterns.Contains(family) && (p.type & excluded) == 0).ToArray();
        boundRules = boundPatterns.Select(p => p is RuleYaku ? YakuRule.Unknown : Enum.Parse<YakuRule>(p.GetType().Name)).ToArray();
        boundCustom = boundPatterns.OfType<RuleYaku>().ToArray();
        akaFactor = boundRules.Count(r => r == YakuRule.Akadora);
        doraFactor = boundRules.Count(r => r == YakuRule.Dora);
        uraFactor = hand.riichi ? boundRules.Count(r => r == YakuRule.Uradora) : 0;
        nukiFactor = boundRules.Count(r => r == YakuRule.NukiDora);
        PrepareBoundContexts();
        Reset();
        SeedShared(family);
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
        shared?.Remember(q.Family, q.Groups);
        if (shared?.ReuseScores == true)
          shared.Add(CacheKey(q.Family, DiscardScoreCache.Key(q.Groups, q.Role), q.EffectiveIndex, q), q.Best);
        evaluator.Evaluate(q.Family, hand, q.Incoming, q.Groups, PatternResolver.CountTargets(q.Groups),
            q.Role, q.EffectiveIndex, mask, excluded, q.Aka, q.Dora, q.Ura);
        var scores = evaluator.Materialize(hand.game.config.scoringOption);
        scores.resolution = new HandResolution(hand, q.Incoming, q.Groups, q.Role, q.EffectiveIndex) {
          VisitedTargets = visitedTargets, EvaluatedInterpretations = evaluatedInterpretations,
          VisitedNodes = visitedNodes, BoundCalculations = boundCalculations,
        };
        result[i] = scores;
      }
      if (shared != null) {
        shared.VisitedNodes += visitedNodes; shared.BoundCalculations += boundCalculations;
        shared.VisitedTargets += visitedTargets; shared.EvaluatedInterpretations += evaluatedInterpretations;
      }
      return result;
    }

    private void SeedShared(BasePattern family) {
      if (shared == null) return;
      foreach (var witness in shared.Witnesses) {
        if (witness.Family != family || witness.Groups.Length < fixedGroups.Length) continue;
        bool sameCalls = true;
        for (int i = 0; i < fixedGroups.Length; i++) sameCalls &= witness.Groups[i] == fixedGroups[i];
        if (!sameCalls) continue;
        for (int i = fixedGroups.Length; i < witness.Groups.Length; i++) Push(witness.Groups[i]);
        Visit(family, seed: true);
        while (count > fixedGroups.Length) Pop();
      }
    }

    private void Reset() {
      count = 0; Array.Clear(targets); Array.Clear(freeTargets);
      partials[0] = new Partial { Pair = -1, EveryGroup19 = true, AllSequenceOrPair = true };
      for (int i = 0; i < required.Length; i++) if (required[i] > 0) partials[0].Present |= 1UL << i;
      Array.Fill(activeQueries, true);
      foreach (var g in fixedGroups) {
        groups[count++] = g; Change(g, 1, false);
        partials[count] = ExtendPartial(partials[count - 1], g);
      }
    }

    private void Change(CompactMeld g, int amount, bool free = true) {
      for (int pos = 0; pos < g.Count; pos++) {
        int i = g.TileIndex + (g.Kind == CompactMeldKind.Sequence ? pos : 0);
        targets[i] = (byte)(targets[i] + amount);
        if (free) freeTargets[i] = (byte)(freeTargets[i] + amount);
      }
    }
    private void Push(CompactMeld g) {
      groups[count++] = g; Change(g, 1);
      partials[count] = ExtendPartial(partials[count - 1], g);
    }
    private void Pop() { var g = groups[--count]; Change(g, -1); }

    private bool Covered() {
      for (int i = 0; i < 34; i++) if (freeTargets[i] < required[i]) return false;
      return true;
    }

    /// <summary>
    /// Whether the natural tiles not yet placed can fit in the remaining melds. A
    /// meld holds three tiles and spans three ranks of one suit, so each suit needs
    /// enough melds for both its tile count and how far apart its tiles lie.
    /// </summary>
    private bool CanCover(int remainingMelds, out int lowestMissing) {
      lowestMissing = -1;
      // Fully wild holdings have no mandatory identities to place.
      if (mandatoryMask == 0) return true;
      int needed = 0;
      for (int suit = 0; suit < 3; suit++) {
        int missing = 0; uint ranks = 0;
        for (int rank = 0; rank < 9; rank++) {
          int n = required[suit * 9 + rank] - freeTargets[suit * 9 + rank];
          if (n <= 0) continue;
          if (lowestMissing < 0) lowestMissing = suit * 9 + rank;
          missing += n; ranks |= 1u << rank;
        }
        int spans = 0;
        for (; ranks != 0; spans++) ranks &= ~(7u << BitOperations.TrailingZeroCount(ranks));
        needed += Math.Max(spans, (missing + 2) / 3);
      }
      for (int i = 27; i < 34; i++) {
        int n = required[i] - freeTargets[i];
        if (n <= 0) continue;
        if (lowestMissing < 0) lowestMissing = i;
        needed += (n + 2) / 3;
      }
      return needed <= remainingMelds;
    }

    private void Regular(BasePattern family) {
      if (fixedGroups.Any(g => g.Kind is CompactMeldKind.Single)) return;
      int pairs = fixedGroups.Count(g => g.Kind == CompactMeldKind.Pair);
      int needed = 4 - fixedGroups.Count(g => g.Kind != CompactMeldKind.Pair);
      if (pairs > 1 || needed < 0) return;
      Seed(family);
      if (pairs == 1) SearchMelds(family, needed, 0, 0);
      else for (int pair = 0; pair < 34; pair++) {
        Push(new(CompactMeldKind.Pair, pair, true));
        SearchMelds(family, needed, 0, 0); Pop();
      }
    }

    /// <summary>
    /// Visits each target once. While a natural tile is unplaced the next meld must
    /// contain the lowest such tile, which leaves at most four choices: the choice is
    /// the target's lowest-numbered meld containing that tile, so lower-numbered ones
    /// are banned below it. Melds that hold no required tile come last, in order.
    /// </summary>
    private void SearchMelds(BasePattern family, int remaining, int start, ulong banned) {
      Span<bool> saved = stackalloc bool[activeQueries.Length];
      activeQueries.CopyTo(saved);
      try {
        if (!CanCover(remaining, out int lowestMissing) || CanPrune(family, remaining)) return;
        if (remaining == 0) { Visit(family); return; }
        if (lowestMissing >= 0) {
          foreach (int i in meldsContaining[lowestMissing]) {
            if ((banned >> i & 1) == 0) { Push(melds[i]); SearchMelds(family, remaining - 1, 0, banned); Pop(); }
            banned |= 1UL << i;
          }
          return;
        }
        for (int i = start; i < melds.Length; i++) {
          if ((banned >> i & 1) != 0) continue;
          Push(melds[i]); SearchMelds(family, remaining - 1, i, banned); Pop();
        }
      } finally {
        saved.CopyTo(activeQueries);
      }
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
      if (Covered()) Visit(family, seed: true);
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

    private static DiscardScoreCache.ScoreKey CacheKey(BasePattern family, DiscardScoreCache.GroupKey groups,
        int index, Query q) => new(family, groups, index, q.Incoming.source, q.Incoming.discardInfo?.reason,
            q.Incoming.discardInfo?.isRiichiDeclaration, q.Incoming.IsTsumo, q.Aka, q.Dora, q.Ura);

    private void Visit(BasePattern family, bool seed = false) {
      if (!Covered()) return;
      visitedTargets++;
      evaluator.PrepareTarget(groups.AsSpan(0, count), targets);
      // The ordinary tree mostly visits targets once. Cache the starting targets
      // reused across holdings, rather than hashing every speculative leaf.
      bool reuse = seed && shared?.ReuseScores == true;
      Span<DiscardScoreCache.GroupKey> roleKeys = stackalloc DiscardScoreCache.GroupKey[reuse ? count : 0];
      uint keyedRoles = 0;
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
            CompactScore score;
            if (reuse) {
              if ((keyedRoles & (1u << role)) == 0) {
                roleKeys[role] = DiscardScoreCache.Key(groups.AsSpan(0, count), role);
                keyedRoles |= 1u << role;
              }
              var key = CacheKey(family, roleKeys[role], index, q);
              if (!shared.TryGet(key, out score)) {
                evaluatedInterpretations++;
                score = evaluator.EvaluatePrepared(family, hand, q.Incoming, groups.AsSpan(0, count), targets, role, index,
                    mask, excluded, q.Aka, q.Dora, q.Ura);
                shared.Add(key, score);
              }
            } else {
              evaluatedInterpretations++;
              score = evaluator.EvaluatePrepared(family, hand, q.Incoming, groups.AsSpan(0, count), targets, role, index,
                  mask, excluded, q.Aka, q.Dora, q.Ura);
            }
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
