using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Multi-profile rule-based baseline agent for Arena benchmarking.
  /// Supports calibration anchors across distinct playstyles:
  /// <list type="bullet">
  ///   <item><c>balanced</c> / <c>default</c>: Standard expected-value <see cref="RuleBasedStrategy"/>.</item>
  ///   <item><c>aggressive</c>: Riichi-eager, efficiency-maximizing attacker.</item>
  ///   <item><c>defensive</c>: Cautious betaori folder against opponent riichi or open pressure.</item>
  ///   <item><c>fast_open</c>: Eager open-meld caller when legal yaku routes exist.</item>
  ///   <item><c>tsumogiri</c>: Passive legal floor baseline (claims wins, else discards drawn tile).</item>
  /// </list>
  /// </summary>
  public sealed class ArenaBaselineAI(
      int id,
      Room room,
      string variant = "balanced",
      string tableNickname = null,
      UserStatus status = UserStatus.InRoom) : AIAgent(id, room, status) {
    private readonly string customNickname = tableNickname;

    public override AiType aiType => AiType.RuleBased;

    public override string nickname =>
        !string.IsNullOrWhiteSpace(customNickname)
            ? customNickname
            : $"Player {Seat + 1}";

    public string Variant { get; } = NormalizeVariant(variant);

    public static string NormalizeVariant(string raw) {
      var v = (raw ?? "balanced").Trim().ToLowerInvariant();
      return v switch {
        "" or "default" or "balanced" => "balanced",
        "aggressive" => "aggressive",
        "defensive" => "defensive",
        "fast_open" => "fast_open",
        "tsumogiri" => "tsumogiri",
        _ => "balanced",
      };
    }

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      var game = room?.game;
      if (game == null || playerInquiry == null) {
        return InquiryResponse.Default(Seat);
      }

      var view = new PublicGameView(game, playerInquiry.playerId);
      return DecideVariant(Variant, view, playerInquiry);
    }

    public static InquiryResponse DecideVariant(
        string variant, PublicGameView view, SinglePlayerInquiry inquiry) {
      return NormalizeVariant(variant) switch {
        "tsumogiri" => DecideTsumogiri(view, inquiry),
        "aggressive" => DecideAggressive(view, inquiry),
        "defensive" => DecideDefensive(view, inquiry),
        "fast_open" => DecideFastOpen(view, inquiry),
        _ => RuleBasedStrategy.Decide(view, inquiry),
      };
    }

    private static InquiryResponse Confirm(int seat, int actionIdx) =>
        new(seat, actionIdx, "{}");

    private static InquiryResponse Choose(int seat, int actionIdx, int optionIdx) =>
        new(seat, actionIdx, JsonSerializer.Serialize(optionIdx));

    private static InquiryResponse DecideTsumogiri(
        PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;
      int agariIdx = FindActionIndex(actions, a => a is AgariAction);
      if (agariIdx >= 0) return Confirm(view.Seat, agariIdx);

      int nextIdx = FindActionIndex(actions, a => a is NextRoundAction);
      if (nextIdx >= 0) return Confirm(view.Seat, nextIdx);

      int playIdx = FindActionIndex(actions, a => a is PlayTileAction and not RiichiAction);
      if (playIdx >= 0 && actions[playIdx] is PlayTileAction play && play.options.Count > 0) {
        var pending = view.SelfHand.pendingTile;
        if (pending != null) {
          for (int i = 0; i < play.options.Count; i++) {
            if (play.options[i].tile.traceId == pending.traceId) {
              return Choose(view.Seat, playIdx, i);
            }
          }
        }
        return Choose(view.Seat, playIdx, play.options.Count - 1);
      }

      return InquiryResponse.Default(view.Seat);
    }

    private static InquiryResponse DecideAggressive(
        PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;
      int agariIdx = FindActionIndex(actions, a => a is AgariAction);
      if (agariIdx >= 0) return Confirm(view.Seat, agariIdx);

      // Declare riichi aggressively whenever any live wait can satisfy minHan.
      int riichiIdx = FindActionIndex(actions, a => a is RiichiAction);
      if (riichiIdx >= 0 && actions[riichiIdx] is RiichiAction riichi &&
          riichi.options.Count > 0 && view.WallRemaining >= 4) {
        int bestOpt = ChooseBestRiichiOption(view, riichi);
        if (bestOpt >= 0) {
          return Choose(view.Seat, riichiIdx, bestOpt);
        }
      }

      int nukiIdx = FindActionIndex(actions, a => a is NukiDoraAction);
      if (nukiIdx >= 0 && actions[nukiIdx] is NukiDoraAction nuki && nuki.options.Count > 0) {
        return Choose(view.Seat, nukiIdx, 0);
      }

      // Pure tile efficiency on discard unless far from tenpai against riichi.
      bool anyEnemyRiichi = AnyOpponentRiichi(view);
      int playIdx = FindActionIndex(actions, a => a is PlayTileAction and not RiichiAction);
      if (playIdx >= 0 && actions[playIdx] is PlayTileAction play && play.options.Count > 0) {
        var hand = FullHand(view);
        int shanten = view.ShantenOf(hand);
        if (!anyEnemyRiichi || shanten <= 2) {
          var threats = DefenseModel.Threats(view);
          int bestIdx = 0;
          double bestScore = double.NegativeInfinity;
          int cap = shanten == int.MaxValue ? int.MaxValue : shanten + 1;
          for (int i = 0; i < play.options.Count; i++) {
            var optTile = play.options[i].tile;
            var ev = view.EvaluateDiscard(optTile, hand, cap);
            double dealInRisk = EstimateDealInRisk(view, threats, optTile.tile);
            double s = ev.Shanten == int.MaxValue
                ? -1000.0
                : -100.0 * ev.Shanten + ev.Ukeire * 2.5
                  - 1.5 * (view.CountDora(optTile.tile) + (optTile.tile.Akadora ? 1 : 0))
                  - (optTile.tile.IsWildcard ? 40.0 : 0.0)
                  - 2.0 * dealInRisk;
            if (s > bestScore) {
              bestScore = s;
              bestIdx = i;
            }
          }
          if (bestScore > -900.0) {
            return Choose(view.Seat, playIdx, bestIdx);
          }
        }
      }

      return RuleBasedStrategy.Decide(view, inquiry);
    }

    private static InquiryResponse DecideDefensive(
        PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;
      int agariIdx = FindActionIndex(actions, a => a is AgariAction);
      if (agariIdx >= 0) return Confirm(view.Seat, agariIdx);

      int nextIdx = FindActionIndex(actions, a => a is NextRoundAction);
      if (nextIdx >= 0) return Confirm(view.Seat, nextIdx);

      bool enemyRiichi = AnyOpponentRiichi(view);
      bool heavyMelds = view.OpponentSeats
          .Any(s => view.CalledOf(s).Count >= 2 && view.JunOf(s) >= 7);

      if ((enemyRiichi || heavyMelds) && !view.SelfRiichi) {
        var hand = FullHand(view);
        int shanten = view.ShantenOf(hand);

        // Decline reactive calls when under threat and not reaching immediate tenpai.
        int playIdx = FindActionIndex(actions, a => a is PlayTileAction and not RiichiAction);
        if (playIdx < 0 && shanten > 1) {
          return InquiryResponse.Default(view.Seat);
        }

        // Fold hard to safest tile unless we are already tenpai (shanten <= 0).
        if (playIdx >= 0 && actions[playIdx] is PlayTileAction play &&
            play.options.Count > 0 && shanten >= 1) {
          var threats = DefenseModel.Threats(view);
          int safestIdx = 0;
          double bestSafety = double.NegativeInfinity;
          for (int i = 0; i < play.options.Count; i++) {
            var t = play.options[i].tile.tile;
            double risk = EstimateDealInRisk(view, threats, t);
            double safety = -100.0 * risk;
            if (t.IsZ) safety += 4.0 - view.UnseenCount(t);
            else if (t.Num is 1 or 9) safety += 2.0;
            if (t.IsWildcard) safety -= 25.0; // Keep wildcards rather than throwing them early.
            if (safety > bestSafety) {
              bestSafety = safety;
              safestIdx = i;
            }
          }
          return Choose(view.Seat, playIdx, safestIdx);
        }
      }

      return RuleBasedStrategy.Decide(view, inquiry);
    }

    private static InquiryResponse DecideFastOpen(
        PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;
      int agariIdx = FindActionIndex(actions, a => a is AgariAction);
      if (agariIdx >= 0) return Confirm(view.Seat, agariIdx);

      // Eagerly claim pon/chii on reactive turns if it advances a viable open-yaku hand.
      int playIdx = FindActionIndex(actions, a => a is PlayTileAction);
      if (playIdx < 0 && !AnyOpponentRiichi(view)) {
        int before = view.ShantenOf(view.SelfHand.freeTiles);
        int ponIdx = FindActionIndex(actions, a => a is PonAction);
        if (ponIdx >= 0 && actions[ponIdx] is PonAction pon && pon.options.Count > 0) {
          var firstTile = pon.options[0].tiles.FirstOrDefault()?.tile ?? default;
          bool yakuhai = IsValueHonor(view, firstTile);
          bool tanyaoOk = view.AllowsOpenTanyao && view.MinHan == 1 && !firstTile.Is19Z &&
              view.SelfHand.freeTiles.Count(t => !t.tile.Is19Z || t.tile.IsWildcard) >= 8;
          if (yakuhai || tanyaoOk) {
            return Choose(view.Seat, ponIdx, 0);
          }
        }
        int chiiIdx = FindActionIndex(actions, a => a is ChiiAction);
        if (chiiIdx >= 0 && actions[chiiIdx] is ChiiAction chii && chii.options.Count > 0 && before >= 1) {
          for (int i = 0; i < chii.options.Count; i++) {
            var optTiles = chii.options[i].tiles;
            if (view.AllowsOpenTanyao && view.MinHan == 1 &&
                optTiles.All(t => !t.tile.Is19Z || t.tile.IsWildcard) &&
                view.SelfHand.freeTiles.All(t => !t.tile.Is19Z || t.tile.IsWildcard) &&
                view.SelfCalled.SelectMany(m => m).All(t => !t.tile.Is19Z || t.tile.IsWildcard)) {
              return Choose(view.Seat, chiiIdx, i);
            }
          }
        }
      }

      return RuleBasedStrategy.Decide(view, inquiry);
    }

    private static int ChooseBestRiichiOption(PublicGameView view, RiichiAction riichi) {
      var hand = FullHand(view);
      var threats = DefenseModel.Threats(view);
      int bestIdx = -1;
      double bestScore = double.NegativeInfinity;
      for (int i = 0; i < riichi.options.Count; i++) {
        var optTile = riichi.options[i].tile;
        var cand = riichi.candidates?.Find(c => c.tile.tile.IsSame(optTile.tile));
        bool legal = cand == null || cand.tenpaiInfos.Count == 0 ||
            cand.tenpaiInfos.Any(t => view.UnseenCount(t.winningTile) > 0 &&
                (t.yakuman > 0 || t.yaku + 1 >= view.MinHan));
        if (!legal) continue;

        int liveWaits = cand?.tenpaiInfos?.Sum(t => view.UnseenCount(t.winningTile))
            ?? view.EvaluateDiscard(optTile, hand, 0).Ukeire;
        double risk = EstimateDealInRisk(view, threats, optTile.tile);
        double score = liveWaits * 4.0 - risk * 5.0 - (optTile.tile.IsWildcard ? 30.0 : 0.0);
        if (score > bestScore) {
          bestScore = score;
          bestIdx = i;
        }
      }
      return bestIdx;
    }

    private static double EstimateDealInRisk(
        PublicGameView view, IReadOnlyList<SeatThreat> threats, Tile tile) {
      if (threats == null || threats.Count == 0) return 0.0;
      double total = 0.0;
      foreach (var threat in threats) {
        total += threat.TenpaiOdds * DefenseModel.DealInChance(view, threat, tile);
      }
      return total;
    }

    private static bool IsValueHonor(PublicGameView view, Tile tile) {
      if (!tile.IsZ) return false;
      if (tile.Num is 5 or 6 or 7) return true;
      int roundWindNum = (int)view.RoundWind + 1;
      int selfWindNum = (int)view.SelfWind + 1;
      return tile.Num == roundWindNum || tile.Num == selfWindNum;
    }

    private static bool AnyOpponentRiichi(PublicGameView view) =>
        view.OpponentSeats.Any(view.IsRiichi);

    private static List<GameTile> FullHand(PublicGameView view) {
      var list = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) list.Add(view.SelfHand.pendingTile);
      return list;
    }

    private static int FindActionIndex(
        IReadOnlyList<IPlayerAction> actions, Func<IPlayerAction, bool> pred) {
      for (int i = 0; i < actions.Count; i++) {
        if (pred(actions[i])) return i;
      }
      return -1;
    }
  }
}
