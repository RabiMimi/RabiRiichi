using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmActionMenuTest {
    private static Game BuildGame(System.Action<ScenarioBuilder> configure) {
      var builder = new ScenarioBuilder();
      configure(builder);
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      return game;
    }

    private static SinglePlayerInquiry InquiryWith(int seat, params IPlayerAction[] actions) {
      var inquiry = new SinglePlayerInquiry(seat);
      foreach (var a in actions) {
        inquiry.AddAction(a);
      }
      return inquiry;
    }

    private static List<GameTile> KanTiles(string tile, bool claimed, bool added = false) {
      var tiles = Enumerable.Range(0, 4)
          .Select(i => new GameTile(new Tile(tile), i + 1) { formTime = added && i == 3 ? -1 : 5 })
          .ToList();
      if (claimed) {
        tiles[0].discardInfo = new DiscardInfo(null, DiscardReason.None, 0);
      }
      return tiles;
    }

    [TestMethod]
    public void Build_IncludesSkipAndConfirmActions() {
      var winTile = new GameTile(new Tile("1p"), 1);
      var ron = new RonAction(0, new ScoreStorage(), winTile);
      var inquiry = InquiryWith(0, ron);

      var menu = LlmActionMenu.Build(inquiry);

      // Skip (implicit) + ron.
      Assert.IsTrue(menu.Any(c => c.Kind == "skip"));
      var ronChoice = menu.FirstOrDefault(c => c.Kind == "ron");
      Assert.IsNotNull(ronChoice);
      Assert.AreEqual(-1, ronChoice.OptionIndex);
      // Confirm actions serialize to "{}".
      Assert.AreEqual("{}", ronChoice.ToResponse(0).response);
      Assert.AreEqual(inquiry.actions.IndexOf(ron), ronChoice.ActionIndex);
    }

    [TestMethod]
    public void Build_DiscardOptionsAreEnumeratedWithNotation() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var player = game.GetPlayer(0);
      var hand14 = player.hand.freeTiles.ToList();
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var inquiry = InquiryWith(0, play);

      var menu = LlmActionMenu.Build(inquiry);
      var discards = menu.Where(c => c.Kind == "discard").ToList();
      Assert.AreEqual(hand14.Count, discards.Count);
      // Each discard maps to an option index serialized as a bare int.
      var first = discards[0];
      Assert.AreEqual(first.OptionIndex.ToString(), first.ToResponse(0).response);
      // Description carries tile notation.
      StringAssert.Contains(discards[0].Description, "discard");
    }

    [TestMethod]
    public void Build_ChoiceIdsAreContiguousAndStable() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var player = game.GetPlayer(0);
      var hand14 = player.hand.freeTiles.ToList();
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var menu = LlmActionMenu.Build(InquiryWith(0, play));

      for (int i = 0; i < menu.Count; i++) {
        Assert.AreEqual(i, menu[i].Id);
      }
    }

    [TestMethod]
    public void ToResponse_ChoiceSerializesOptionIndex() {
      var choice = new LlmChoice {
        Id = 0, ActionIndex = 2, OptionIndex = 5, Kind = "discard",
      };
      var resp = choice.ToResponse(3);
      Assert.AreEqual(3, resp.playerId);
      Assert.AreEqual(2, resp.index);
      Assert.AreEqual(JsonSerializer.Serialize(5), resp.response);
    }

    [TestMethod]
    public void DescribeSelected_UsesChoiceSerializationAndReturnsActionDetails() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var player = game.GetPlayer(0);
      var hand14 = player.hand.freeTiles.ToList();
      var inquiry = InquiryWith(0, new PlayTileAction(player, hand14, hand14[0]));
      var choice = LlmActionMenu.Build(inquiry)
          .First(c => c.Kind == "discard" && c.Description.Contains("1m"));

      var description = LlmActionMenu.DescribeSelected(inquiry, choice.ToResponse(0));

      Assert.AreEqual(choice.Description, description);
      StringAssert.Contains(description, "1m");
    }

    [TestMethod]
    public void DescribeAutomaticAction_RecognizesSingleForcedRiichiDiscard() {
      var menu = new List<LlmChoice> {
        new() { Kind = "discard", Description = "discard 7p" },
      };

      var note = LlmActionMenu.DescribeAutomaticAction(
          selfRiichi: true, menu, "discard 7p");

      Assert.AreEqual(
          "You automatically discarded 7p after riichi because that was the only valid action.",
          note);
      menu.Add(new LlmChoice { Kind = "discard", Description = "discard 8p" });
      Assert.IsNull(LlmActionMenu.DescribeAutomaticAction(
          selfRiichi: true, menu, "discard 7p"));
    }

    [TestMethod]
    public void Build_DistinguishesAnkanKakanAndDaiminkan() {
      var kan = new KanAction(0, new List<List<GameTile>> {
        KanTiles("1m", claimed: false),
        KanTiles("2m", claimed: true, added: true),
        KanTiles("3m", claimed: true),
      });

      var menu = LlmActionMenu.Build(InquiryWith(0, kan));

      Assert.IsTrue(menu.Any(choice => choice.Kind == "ankan" &&
          choice.Description.Contains("closed kan")));
      Assert.IsTrue(menu.Any(choice => choice.Kind == "kakan" &&
          choice.Description.Contains("upgrading an existing pon")));
      Assert.IsTrue(menu.Any(choice => choice.Kind == "daiminkan" &&
          choice.Description.Contains("another player's discard")));
    }

    [TestMethod]
    public void OutOfTurnCallsSkipLlmUnlessRonIsAvailable() {
      var daiminkan = new KanAction(0,
          new List<List<GameTile>> { KanTiles("3m", claimed: true) });
      Assert.IsTrue(LlmActionMenu.IsOutOfTurnCallInquiry(InquiryWith(0, daiminkan)));

      var ankan = new KanAction(0,
          new List<List<GameTile>> { KanTiles("1m", claimed: false) });
      Assert.IsFalse(LlmActionMenu.IsOutOfTurnCallInquiry(InquiryWith(0, ankan)));

      var chiiTiles = new List<GameTile> {
        new(new Tile("1m"), 20), new(new Tile("2m"), 21), new(new Tile("3m"), 22),
      };
      var chii = new ChiiAction(0, new List<List<GameTile>> { chiiTiles });
      Assert.IsTrue(LlmActionMenu.IsOutOfTurnCallInquiry(InquiryWith(0, chii)));

      var ron = new RonAction(0, new ScoreStorage(), new GameTile(new Tile("3m"), 23));
      Assert.IsFalse(LlmActionMenu.IsOutOfTurnCallInquiry(InquiryWith(0, chii, ron)));
    }
  }
}
