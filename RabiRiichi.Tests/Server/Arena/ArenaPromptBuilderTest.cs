using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Tests.Scenario;
using System;
using System.Collections.Generic;
using TileSource = RabiRiichi.Generated.Core.TileSource;
using DiscardReason = RabiRiichi.Generated.Core.DiscardReason;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Unit tests for the Arena prompt/tool-context builder, wildcard &amp; non-standard
  /// deck rules, classical yaku (koyaku) instructions, chronological discard rivers,
  /// genbutsu safety hints, seat labeler anonymity, and JSON response parser.
  /// </summary>
  [TestClass]
  public class ArenaPromptBuilderTest {
    private static Game DiscardGame(ArenaConfig.GameRulesConfig rules = null) {
      if (rules != null) {
        var rulesCfg = rules.BuildGameConfig();
        var rulesSetup = rulesCfg.setup;
        var sb = new ScenarioBuilder(rules.PlayerCount);
        sb.WithConfig(cb => cb.OverwriteConfig(rulesCfg));
        sb.WithPlayer(0, p => p.SetFreeTiles("19m123456p123s56z"));
        Game g = null;
        sb.Build(0).WithGame(game => {
          g = game;
          g.config.setup = rulesSetup;
        });
        var p0 = g.GetPlayer(0);
        p0.hand.pendingTile = new GameTile(new Tile("7z"), 999) { player = p0 };
        return g;
      }

      var builder = new ScenarioBuilder();
      builder.WithPlayer(0, p => p.SetFreeTiles("123m456p789s1234s"));
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      var player = game.GetPlayer(0);
      player.hand.pendingTile = new GameTile(new Tile("7z"), 999) { player = player };
      return game;
    }

    private static (PublicGameView view, SinglePlayerInquiry pi, IReadOnlyList<LlmChoice> menu)
        DiscardSetup(Game game) {
      var player = game.GetPlayer(0);
      var hand14 = new List<GameTile>(player.hand.freeTiles);
      if (player.hand.pendingTile != null) {
        hand14.Add(player.hand.pendingTile);
      }
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(play);
      pi.DisableSkip();
      var menu = LlmActionMenu.Build(pi, annotateTenpai: false);
      return (new PublicGameView(game, 0), pi, menu);
    }

    // ----- ArenaSeatLabeler ------------------------------------------------

    [TestMethod]
    public void Labeler_Anonymized_UsesNeutralSeatLabelsOnly() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: false, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: _ => "REAL-NAME");
      Assert.AreEqual("You", labeler.Label(0));
      var l1 = labeler.Label(1);
      StringAssert.Contains(l1, "Player 2");
      Assert.IsFalse(l1.Contains("REAL-NAME"));
    }

    [TestMethod]
    public void Labeler_Revealed_AppendsDisplayName() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: true, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: s => $"Rival-{s}");
      StringAssert.Contains(labeler.Label(1), "Rival-1");
    }

    [TestMethod]
    public void Labeler_Revealed_FallsBackToNeutralWhenNoName() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: true, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: _ => "");
      StringAssert.Contains(labeler.Label(1), "Player 2");
    }

    // ----- System Prompt, Wildcards, Non-Standard Decks & Koyaku -----------

    [TestMethod]
    public void SystemPrompt_DescribesJsonContractAndCarriesNoOpponentInfo() {
      var labeler = new ArenaSeatLabeler(false, 0, s => (Wind)s);
      var builder = new ArenaPromptBuilder(labeler);
      var (view, _, _) = DiscardSetup(DiscardGame());
      var sys = builder.BuildSystemPrompt(2, view);

      StringAssert.Contains(sys, "\"action\"");
      StringAssert.Contains(sys, "\"rationale\"");
      StringAssert.Contains(sys, "Seat 2");
      StringAssert.Contains(sys, "Manzu");
      StringAssert.Contains(sys, "WILDCARD");
      Assert.IsFalse(sys.Contains("{{"), "template placeholders must be substituted");
    }

    [TestMethod]
    public void SystemPrompt_IncludesMatchFormatMinHanAndEnabledYaku() {
      var labeler = new ArenaSeatLabeler(false, 0, s => (Wind)s);
      var builder = new ArenaPromptBuilder(labeler);
      var (view, _, _) = DiscardSetup(DiscardGame());
      var sys = builder.BuildSystemPrompt(0, view);

      StringAssert.Contains(sys, "Match format:");
      StringAssert.Contains(sys, "Minimum yaku requirement");
      StringAssert.Contains(sys, "Enabled standard yaku");
    }

    [TestMethod]
    public void SystemPrompt_WildcardSanmaKoyakuAotenjou_RendersAllSpecialRules() {
      var rules = new ArenaConfig.GameRulesConfig {
        PlayerCount = 3,
        TotalRound = 1,
        MinHan = 2,
        ScoringOption = (int)(ScoringOption.Default & ~ScoringOption.Yakuman), // Aotenjou!
        DoraOption = (int)(DoraOption.Default | DoraOption.Nukidora),
      };
      rules.TileSet.Preset = "Sanma";
      rules.TileSet.ExtraWildcards = 4;
      rules.AllowedYakus.Add("TsubameGaeshi");
      rules.AllowedYakus.Add("Uumensai");

      var game = DiscardGame(rules);
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind),
          new ArenaConfig.PromptConfig {
            ToolContextMode = "extended",
            CustomSystemAppendix = "Test custom appendix rule.",
          });

      var sys = builder.BuildSystemPrompt(0, view);
      StringAssert.Contains(sys, "3-player");
      StringAssert.Contains(sys, "OMITTED tile kinds");
      StringAssert.Contains(sys, "2m, 3m, 4m, 5m, 6m, 7m, 8m");
      StringAssert.Contains(sys, "WILDCARD TILES ENABLED: 4x `*`");
      StringAssert.Contains(sys, "Wildcard Furiten trap");
      StringAssert.Contains(sys, "AOTENJOU");
      StringAssert.Contains(sys, "Nukidora (`4z` North pull): Enabled");
      StringAssert.Contains(sys, "Enabled Classical Yaku / Koyaku");
      StringAssert.Contains(sys, "TsubameGaeshi");
      StringAssert.Contains(sys, "Uumensai");
      StringAssert.Contains(sys, "Test custom appendix rule.");

      var turn = builder.BuildTurnMessage(view, menu);
      StringAssert.Contains(turn, "Wildcards `*` unseen:");
      StringAssert.Contains(turn, "Unseen tile counts by suit:");
      // Sanma Manzu unseen summary only lists 1m and 9m (not omitted 2m-8m).
      StringAssert.Contains(turn, "1m:");
      StringAssert.Contains(turn, "9m:");
      Assert.IsFalse(turn.Contains("2m:"));
    }

    // ----- Turn Message & Tool Context Modes -------------------------------

    [TestMethod]
    public void TurnMessage_StandardMode_IncludesToolContextWithoutPerDiscardSolverLeak() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(view, menu);

      StringAssert.Contains(msg, "== Legal Actions");
      StringAssert.Contains(msg, "== Pre-computed Public Analysis ==");
      Assert.IsFalse(msg.Contains("Per-discard"), "per-discard block must be omitted");
      Assert.IsFalse(msg.Contains("ukeire"), "per-discard ukeire must be omitted");
      StringAssert.Contains(msg, "[0]");
      Assert.IsFalse(msg.Contains("-> TENPAI"), "menu must not leak per-discard tenpai/waits/han");
    }

    [TestMethod]
    public void TurnMessage_MinimalMode_OmitsPrecomputedAnalysisBlock() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind),
          new ArenaConfig.PromptConfig { ToolContextMode = "minimal" });

      var msg = builder.BuildTurnMessage(view, menu);
      Assert.IsFalse(msg.Contains("Pre-computed Public Analysis"));
      StringAssert.Contains(msg, "== Legal Actions");
    }

    [TestMethod]
    public void TurnMessage_Anonymized_HasNoOpponentNames() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind, _ => "LEAK"));

      var msg = builder.BuildTurnMessage(view, menu);

      Assert.IsFalse(msg.Contains("LEAK"));
      StringAssert.Contains(msg, "Player 2");
    }

    [TestMethod]
    public void TurnMessage_IncludesValidationErrorOnRetry() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(
          view, menu, validationError: "action id 99 is not in the legal menu");

      StringAssert.Contains(msg, "REJECTED");
      StringAssert.Contains(msg, "99");
    }

    [TestMethod]
    public void TurnMessage_FoldsInChatWhenProvided() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var chats = new List<ArenaChatLine> {
        new("South (Player 2)", "watch out for my hand"),
      };
      var msg = builder.BuildTurnMessage(view, menu, chats: chats);
      StringAssert.Contains(msg, "watch out for my hand");
    }

    // ----- Chronological Rivers, Riichi Marker & Genbutsu ------------------

    private static GameTile GT(
        string tile, Player owner, TileSource src, Player from = null, int trace = 0, int time = -1) {
      var gt = new GameTile(new Tile(tile), trace) { player = owner, source = src };
      if (from != null) {
        gt.discardInfo = new DiscardInfo(from, DiscardReason.Draw, time >= 0 ? time : trace, 1);
      } else if (time >= 0) {
        gt.discardInfo = new DiscardInfo(owner, DiscardReason.Draw, time, 1);
      }
      return gt;
    }

    [TestMethod]
    public void TurnMessage_CalledAndRiichiTiles_AnnotatedChronologicallyWithoutWildcardCollision() {
      var game = DiscardGame();
      var p0 = game.GetPlayer(0);
      var p2 = game.GetPlayer(2);

      var d1 = GT("1z", p0, TileSource.Discard, trace: 11);
      var claimed = GT("3p", p2, TileSource.Pon, from: p0, trace: 12);
      var riichiDecl = GT("7s", p0, TileSource.Discard, trace: 13);
      p0.hand.discarded.Add(d1);
      p0.hand.discarded.Add(claimed);
      p0.hand.discarded.Add(riichiDecl);
      p0.hand.riichiTile = riichiDecl;

      p2.hand.called.Add(new Kou(new[] {
        GT("3p", p2, TileSource.Pon, trace: 14),
        GT("3p", p2, TileSource.Pon, trace: 15),
        claimed,
      }));

      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));
      var msg = builder.BuildTurnMessage(view, menu);

      StringAssert.Contains(msg, "pon");
      StringAssert.Contains(msg, "3p from you");

      var river = Array.Find(msg.Split('\n'), l => l.Contains("Your discard river"));
      Assert.IsNotNull(river);
      StringAssert.Contains(river, "1z(East wind) 3p(called) 7s[riichi]");
      // Riichi must NEVER use '*' suffix so '*' unambiguously means a wildcard tile!
      Assert.IsFalse(river.Contains("7s*"));
    }

    [TestMethod]
    public void TurnMessage_Chii_NamesTheSpecificClaimedTile() {
      var game = DiscardGame();
      var p1 = game.GetPlayer(1); // South — discarder
      var p3 = game.GetPlayer(3); // North — caller

      var claimed = GT("5m", p3, TileSource.Chii, from: p1, trace: 21);
      p1.hand.discarded.Add(claimed);
      p3.hand.called.Add(new Shun(new[] {
        GT("4m", p3, TileSource.Chii, trace: 22),
        claimed,
        GT("6m", p3, TileSource.Chii, trace: 23),
      }));

      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));
      var msg = builder.BuildTurnMessage(view, menu);

      StringAssert.Contains(msg, "chii");
      StringAssert.Contains(msg, "5m from South (Player 2)");
    }

    [TestMethod]
    public void TurnMessage_ListsGenbutsuSafeTilesAgainstRiichiOpponent() {
      var game = DiscardGame();
      var p0 = game.GetPlayer(0);
      var p1 = game.GetPlayer(1);

      // Seat 0 holds 1m..3m, 4p..6p, 7s..9s, 1s..4s, + pending 7z.
      // Seat 1 declared Riichi on 4p at time=5; seat 2 later passed 7z at time=6.
      var p1Riichi = GT("4p", p1, TileSource.Discard, trace: 30, time: 5);
      p1.hand.discarded.Add(p1Riichi);
      p1.hand.riichiTile = p1Riichi;

      var p2 = game.GetPlayer(2);
      p2.hand.discarded.Add(GT("7z", p2, TileSource.Discard, trace: 31, time: 6));

      // Replace one concealed tile with a wildcard (keeping a legal 14-tile hand) to prove
      // wildcards are never listed as genbutsu.
      p0.hand.freeTiles[0] = new GameTile(Tile.Wildcard, 998) { player = p0 };

      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));
      var msg = builder.BuildTurnMessage(view, menu);

      var genbutsuLine = Array.Find(msg.Split('\n'), l => l.Contains("100% safe (genbutsu)"));
      Assert.IsNotNull(genbutsuLine);
      StringAssert.Contains(genbutsuLine, "4p");
      StringAssert.Contains(genbutsuLine, "7z");
      Assert.IsFalse(genbutsuLine.Contains("*"));
    }

    // ----- ArenaLlmResponse ------------------------------------------------

    [TestMethod]
    public void Response_ParsesActionRationaleAndOptionalTableChat() {
      var r = ArenaLlmResponse.Parse(
          "{\"action\": 3, \"rationale\": \"safe tile\", \"chat\": \"Riichi incoming!\"}");
      Assert.IsTrue(r.HasAction);
      Assert.AreEqual(3, r.ActionId);
      Assert.AreEqual("safe tile", r.Rationale);
      Assert.AreEqual("Riichi incoming!", r.Chat);
    }

    [TestMethod]
    public void Response_ToleratesCodeFencesAndThinkingTags() {
      var raw = "<think>Hmm 2p is suji</think>\nHere is my move:\n```json\n{\"action\": 2, \"rationale\": \"push\"}\n```";
      var r = ArenaLlmResponse.Parse(raw);
      Assert.AreEqual(2, r.ActionId);
      Assert.AreEqual("push", r.Rationale);
    }

    [TestMethod]
    public void Response_AcceptsNumericStringAction() {
      var r = ArenaLlmResponse.Parse("{\"action\": \"5\", \"rationale\": \"ok\"}");
      Assert.AreEqual(5, r.ActionId);
    }

    [TestMethod]
    public void Response_MissingActionHasNoAction() {
      var r = ArenaLlmResponse.Parse("{\"rationale\": \"thinking...\"}");
      Assert.IsFalse(r.HasAction);
      Assert.AreEqual("thinking...", r.Rationale);
    }

    [TestMethod]
    public void Response_GarbageYieldsEmpty() {
      var r = ArenaLlmResponse.Parse("no json here at all");
      Assert.IsFalse(r.HasAction);
      Assert.IsNull(r.Rationale);
    }

    // ----- 4-Locale Localization (en, zhs, zht, ja) ------------------------

    [TestMethod]
    public void NormalizeArenaLanguage_MapsSimplifiedTraditionalJapaneseAndEnglish() {
      Assert.AreEqual("en", ArenaPromptBuilder.NormalizeArenaLanguage(null));
      Assert.AreEqual("en", ArenaPromptBuilder.NormalizeArenaLanguage(""));
      Assert.AreEqual("en", ArenaPromptBuilder.NormalizeArenaLanguage("en-US"));
      Assert.AreEqual("zhs", ArenaPromptBuilder.NormalizeArenaLanguage("zhs"));
      Assert.AreEqual("zhs", ArenaPromptBuilder.NormalizeArenaLanguage("zh"));
      Assert.AreEqual("zhs", ArenaPromptBuilder.NormalizeArenaLanguage("zh-CN"));
      Assert.AreEqual("zht", ArenaPromptBuilder.NormalizeArenaLanguage("zht"));
      Assert.AreEqual("zht", ArenaPromptBuilder.NormalizeArenaLanguage("zh-TW"));
      Assert.AreEqual("zht", ArenaPromptBuilder.NormalizeArenaLanguage("zh-HK"));
      Assert.AreEqual("zht", ArenaPromptBuilder.NormalizeArenaLanguage("zh-Hant"));
      Assert.AreEqual("ja", ArenaPromptBuilder.NormalizeArenaLanguage("ja"));
      Assert.AreEqual("ja", ArenaPromptBuilder.NormalizeArenaLanguage("ja-JP"));
    }

    [TestMethod]
    public void TileAndKanNotation_DistinguishesSimplifiedTraditionalJapaneseAndEnglish() {
      var south = new Tile("2z");
      var green = new Tile("6z");
      Assert.AreEqual("2z(South wind)", TileNotation.One(south, "en"));
      Assert.AreEqual("2z(南风牌)", TileNotation.One(south, "zhs"));
      Assert.AreEqual("2z(南風牌)", TileNotation.One(south, "zht"));
      Assert.AreEqual("2z(南・風牌)", TileNotation.One(south, "ja"));

      Assert.AreEqual("6z(发，三元牌)", TileNotation.One(green, "zhs"));
      Assert.AreEqual("6z(發，三元牌)", TileNotation.One(green, "zht"));
      Assert.AreEqual("6z(發・三元牌)", TileNotation.One(green, "ja"));

      StringAssert.Contains(LlmKanNotation.Describe(TileSource.Ankan, "zhs"), "暗杠");
      StringAssert.Contains(LlmKanNotation.Describe(TileSource.Ankan, "zht"), "暗槓");
      StringAssert.Contains(LlmKanNotation.Describe(TileSource.Ankan, "ja"), "暗槓");
    }

    [TestMethod]
    public void Labeler_LocalizesSelfAndNeutralWindSeatsAcrossAllLocales() {
      var zhs = new ArenaSeatLabeler(false, 0, s => (Wind)s, language: "zhs");
      Assert.AreEqual("你", zhs.Label(0));
      Assert.AreEqual("东家 (玩家 1)", zhs.NeutralLabel(0));
      Assert.AreEqual("南家 (玩家 2)", zhs.Label(1));

      var zht = new ArenaSeatLabeler(false, 0, s => (Wind)s, language: "zht");
      Assert.AreEqual("你", zht.Label(0));
      Assert.AreEqual("東家 (玩家 1)", zht.NeutralLabel(0));
      Assert.AreEqual("南家 (玩家 2)", zht.Label(1));

      var ja = new ArenaSeatLabeler(false, 0, s => (Wind)s, language: "ja");
      Assert.AreEqual("あなた", ja.Label(0));
      Assert.AreEqual("東家 (プレイヤー 1)", ja.NeutralLabel(0));
      Assert.AreEqual("南家 (プレイヤー 2)", ja.Label(1));
    }

    [TestMethod]
    public void LocalizedPrompts_ZhsZhtJa_RenderWildcardSanmaKoyakuAotenjouAndJsonContract() {
      var rules = new ArenaConfig.GameRulesConfig {
        PlayerCount = 3,
        TotalRound = 1,
        MinHan = 2,
        ScoringOption = (int)(ScoringOption.Default & ~ScoringOption.Yakuman),
        DoraOption = (int)(DoraOption.Default | DoraOption.Nukidora),
      };
      rules.TileSet.Preset = "Sanma";
      rules.TileSet.ExtraWildcards = 4;
      rules.AllowedYakus.Add("TsubameGaeshi");
      rules.AllowedYakus.Add("Uumensai");

      var game = DiscardGame(rules);
      var p1 = game.GetPlayer(1);
      var p1Riichi = GT("4p", p1, TileSource.Discard, trace: 30, time: 5);
      p1.hand.discarded.Add(p1Riichi);
      p1.hand.riichiTile = p1Riichi;

      var cases = new (string Lang, string WildcardMarker, string Koyaku1, string Koyaku2, string Aotenjou)[] {
        ("zhs", "万能牌", "燕返", "五门齐", "青天井"),
        ("zht", "萬能牌", "燕返", "五門齊", "青天井"),
        ("ja", "万能牌", "燕返し", "五門斉", "青天井"),
      };

      foreach (var tc in cases) {
        var player = game.GetPlayer(0);
        var hand14 = new List<GameTile>(player.hand.freeTiles);
        if (player.hand.pendingTile != null) hand14.Add(player.hand.pendingTile);
        var pi = new SinglePlayerInquiry(0);
        pi.AddAction(new PlayTileAction(player, hand14, hand14[0]));
        pi.DisableSkip();
        var menu = LlmActionMenu.Build(pi, annotateTenpai: false, language: tc.Lang);
        var view = new PublicGameView(game, 0);

        var builder = new ArenaPromptBuilder(
            new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind, language: tc.Lang),
            new ArenaConfig.PromptConfig {
              Language = tc.Lang,
              ToolContextMode = "extended",
              EnableTableTalk = true,
              CustomSystemAppendix = "Custom appendix check.",
            });

        var sys = builder.BuildSystemPrompt(0, view);
        Assert.IsFalse(sys.Contains("{{"), $"Unreplaced placeholder in {tc.Lang} system prompt");
        StringAssert.Contains(sys, "\"action\"");
        StringAssert.Contains(sys, "\"rationale\"");
        StringAssert.Contains(sys, "\"chat\"");
        StringAssert.Contains(sys, tc.WildcardMarker);
        StringAssert.Contains(sys, tc.Koyaku1);
        StringAssert.Contains(sys, tc.Koyaku2);
        StringAssert.Contains(sys, tc.Aotenjou);
        StringAssert.Contains(sys, "2m, 3m, 4m, 5m, 6m, 7m, 8m");
        StringAssert.Contains(sys, "Custom appendix check.");

        var turn = builder.BuildTurnMessage(view, menu, validationError: "bad id");
        StringAssert.Contains(turn, "[0]");
        StringAssert.Contains(turn, "4p");
        StringAssert.Contains(turn, "bad id");
        StringAssert.Contains(turn, "1m:");
        Assert.IsFalse(turn.Contains("2m:"));

        var summary = builder.BuildCondensedTurnSummary(view, menu[0].Description);
        StringAssert.Contains(summary, menu[0].Description);
      }
    }
  }
}
