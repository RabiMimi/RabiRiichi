using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
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
  }
}
