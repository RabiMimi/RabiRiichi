using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// A yaku defined by a <see cref="YakuCondition"/> instead of code, so it can be
  /// built at runtime. It scores on every table: the compiled evaluator tests the
  /// condition on each interpretation, and wildcard search prunes targets with
  /// its partial tests instead of enumerating wildcard assignments.
  /// Configure it before registering; it is frozen once a scoring plan uses it.
  /// </summary>
  public class RuleYaku : StdPattern {
    private readonly string yakuName;
    private PatternMask mask = PatternMask.Regular;
    private YakuCondition condition = Cond.True;
    private readonly List<StdPattern> replaced = [];
    private int closedHan, openHan, bonusHan, fu, yakuman, bonusYakuman;
    private bool frozen;

    public RuleYaku(string name, params BasePattern[] families) : this(name, (IEnumerable<BasePattern>)families) { }
    public RuleYaku(string name, IEnumerable<BasePattern> families) {
      ArgumentException.ThrowIfNullOrWhiteSpace(name);
      yakuName = name;
      BaseOn(families);
    }

    // Sealed so the legacy Resolve and the compiled condition test cannot diverge.
    public sealed override string name => yakuName;
    public sealed override PatternMask type => mask;
    public YakuCondition Condition => condition;
    /// <summary>Patterns whose scores this yaku removes when it holds.</summary>
    public IReadOnlyList<StdPattern> Replaced => replaced;

    public RuleYaku OfType(PatternMask type) { Mutate(); mask = type; return this; }
    /// <summary>Adds a requirement. Every requirement must hold.</summary>
    public RuleYaku When(YakuCondition requirement) {
      ArgumentNullException.ThrowIfNull(requirement);
      Mutate();
      condition = ReferenceEquals(condition, Cond.True) ? requirement : condition & requirement;
      return this;
    }
    public RuleYaku Han(int han) => Han(han, han);
    public RuleYaku Han(int closed, int open) { Mutate(); closedHan = Value(closed); openHan = Value(open); return this; }
    /// <summary>Han that counts towards the score but not the one-han minimum, like dora.</summary>
    public RuleYaku BonusHan(int han) { Mutate(); bonusHan = Value(han); return this; }
    public RuleYaku Fu(int fu) { Mutate(); this.fu = Value(fu); return this; }
    public RuleYaku Yakuman(int count = 1) { Mutate(); yakuman = Value(count); return this; }
    /// <summary>Yakuman that counts towards the score but not the yaku requirement.</summary>
    public RuleYaku BonusYakuman(int count = 1) { Mutate(); bonusYakuman = Value(count); return this; }
    /// <summary>Scores after these patterns. They are not required to hold.</summary>
    public new RuleYaku After(params StdPattern[] patterns) { Mutate(); base.After(patterns); return this; }
    /// <summary>Scores after these patterns and only when all of them hold.</summary>
    public new RuleYaku DependOn(params StdPattern[] patterns) { Mutate(); base.DependOn(patterns); return this; }
    /// <summary>Scores after these patterns and removes their scores when this yaku holds.</summary>
    public RuleYaku Replaces(params StdPattern[] patterns) {
      Mutate(); base.After(patterns); replaced.AddRange(patterns);
      return this;
    }

    internal void Freeze() => frozen = true;

    private void Mutate() {
      if (frozen) throw new InvalidOperationException($"{yakuName} is already used by a scoring plan; build a new RuleYaku instead");
    }

    // Wildcard bounds add values without tracking suppression, so they must be nonnegative.
    private static int Value(int value) => value >= 0 ? value
        : throw new ArgumentOutOfRangeException(nameof(value), "Yaku values must be nonnegative");

    internal CompactScore Score(Hand hand) {
      int han = hand.menzen ? closedHan : openHan;
      return new(han + bonusHan, han, fu, yakuman, bonusYakuman);
    }

    public sealed override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int role = groups.FindIndex(g => g.Contains(incoming));
      if (role < 0) return false;
      var compact = groups.Select(PatternResolver.ToCompact).ToArray();
      var view = new WinView();
      view.Set(hand, incoming, compact, PatternResolver.CountTargets(compact), role, CompactMeld.Index(incoming.tile));
      if (!condition.Test(view)) return false;
      scores.Remove(replaced);
      var s = Score(hand);
      if (s.Yaku != 0) scores.Add(new(ScoringType.Han, s.Yaku, this));
      if (s.Han != s.Yaku) scores.Add(new(ScoringType.BonusHan, s.Han - s.Yaku, this));
      if (s.Fu != 0) scores.Add(new(ScoringType.Fu, s.Fu, this));
      if (s.Yakuman != 0) scores.Add(new(ScoringType.Yakuman, s.Yakuman, this));
      if (s.BonusYakuman != 0) scores.Add(new(ScoringType.BonusYakuman, s.BonusYakuman, this));
      return true;
    }
  }
}
