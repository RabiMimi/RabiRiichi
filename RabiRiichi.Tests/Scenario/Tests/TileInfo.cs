using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Tests.Helper;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
  /// <summary>
  /// Verifies the per-tile turn (jun) metadata the server stamps onto tiles:
  /// <see cref="GameTile.drawnJun"/> and <see cref="DiscardInfo.jun"/>. These
  /// power the client tile tooltip and let it infer 手切/摸切 (tedashi/tsumogiri)
  /// authoritatively as "the discarded tile's drawnJun equals the discard jun".
  ///
  /// Each test asserts that inference matches the engine's own authoritative
  /// <see cref="DiscardTileEvent.fromHand"/> signal, which is what the client
  /// relies on being equivalent.
  /// </summary>
  [TestClass]
  public class ScenarioTileInfo {
    [TestMethod]
    public async Task Tsumogiri_DrawnJunEqualsDiscardJun() {
      // Dealer (seat 1) draws 9m and discards it immediately (摸切). A scattered
      // noten hand keeps the inquiry to a plain discard (no riichi offered).
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, p => p.SetFreeTiles("147m258p369s1234z"))
          .WithWall(wall => wall.Reserve("9m"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, inq => inq
          .ChooseTile<PlayTileAction>("9m")
          .AssertNoMoreActions()
      ).AssertAutoFinish();

      scenario.AssertEvent<DiscardTileEvent>(ev => {
        Assert.AreEqual(1, ev.playerId);
        ev.discarded.tile.AssertEquals("9m");
        Assert.IsFalse(ev.fromHand, "discarding the drawn tile is tsumogiri");
        // The client infers tsumogiri from drawnJun == discardInfo.jun.
        Assert.AreEqual(ev.discarded.drawnJun, ev.discarded.discardInfo.jun);
        AssertInferenceMatches(ev);
      });
    }

    [TestMethod]
    public async Task Tedashi_DrawnJunDiffersFromDiscardJun() {
      // Dealer (seat 1) draws 9m but discards a hand tile 4z (手切). The drawn 9m
      // stays in the hand carrying its own drawnJun.
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, p => p.SetFreeTiles("147m258p369s1234z"))
          .WithWall(wall => wall.Reserve("9m"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, inq => inq
          .ChooseTile<PlayTileAction>("4z")
          .AssertNoMoreActions()
      ).AssertAutoFinish();

      scenario.AssertEvent<DiscardTileEvent>(ev => {
        Assert.AreEqual(1, ev.playerId);
        ev.discarded.tile.AssertEquals("4z");
        Assert.IsTrue(ev.fromHand, "discarding a hand tile is tedashi");
        // A tile from the hand was drawn on an earlier jun (or dealt, drawnJun 0),
        // so it never equals the current discard jun.
        Assert.AreNotEqual(ev.discarded.drawnJun, ev.discarded.discardInfo.jun);
        AssertInferenceMatches(ev);
      });

      // The drawn 9m is retained (pending or folded into free tiles) and
      // remembers the jun it was drawn on.
      scenario.WithPlayer(1, player => {
        var drawn = AllHeldTiles(player).First(t => t.tile.IsSame(new Tile("9m")));
        Assert.AreEqual(player.hand.jun, drawn.drawnJun);
      });
    }

    [TestMethod]
    public async Task PostPonDiscard_IsAlwaysTedashi() {
      // Seat 2 holds two 5m and pons seat 1's discarded 5m, then discards a hand
      // tile. A post-claim discard is always 手切: the discarded tile was never
      // drawn (drawnJun 0), so it cannot equal the discard jun.
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, p => p.SetFreeTiles("147m369p258s1234z"))
          .WithPlayer(2, p => p.SetFreeTiles("55m147p258p369p13z"))
          .WithWall(wall => wall.Reserve("5m"))
          .Start(1);

      // Seat 1 draws and discards 5m (tsumogiri) so seat 2 can pon it.
      (await scenario.WaitInquiry()).ForPlayer(1, inq => inq
          .ChooseTile<PlayTileAction>("5m")
          .AssertNoMoreActions()
      ).AssertAutoFinish();

      // Seat 2 pons the 5m.
      (await scenario.WaitInquiry()).ForPlayer(2, inq => inq
          .AssertSkip()
          .ChooseTiles<PonAction>("555m")
          .AssertNoMoreActions()
      ).AssertAutoFinish();

      var postPonInquiry = await scenario.WaitInquiry();
      scenario.WithPlayer(2, player => {
        var ponTiles = player.hand.called.Single().ToArray();
        var ownTiles = ponTiles.Where(tile => tile.discardInfo == null).ToArray();
        var claimedTile = ponTiles.Single(tile => tile.discardInfo != null);

        Assert.AreEqual(2, ownTiles.Length);
        Assert.IsTrue(ownTiles.All(tile => tile.formJun == player.hand.jun));
        Assert.AreEqual(0, claimedTile.formJun,
            "the claimed discard keeps its discard jun instead of a form jun");
      });

      // Seat 2 must now discard from hand.
      postPonInquiry.ForPlayer(2, inq => inq
          .ChooseTile<PlayTileAction>("3z")
          .AssertNoMoreActions()
      ).AssertAutoFinish();

      scenario.AssertEvent<DiscardTileEvent>(ev => {
        Assert.AreEqual(2, ev.playerId);
        ev.discarded.tile.AssertEquals("3z");
        Assert.IsTrue(ev.fromHand, "a discard after a pon is always tedashi");
        Assert.AreEqual(0, ev.discarded.drawnJun, "a hand/dealt tile was never drawn");
        Assert.AreNotEqual(ev.discarded.drawnJun, ev.discarded.discardInfo.jun);
        AssertInferenceMatches(ev);
      });
    }

    /// <summary>
    /// The client's tedashi/tsumogiri inference (drawnJun != discardJun ⇒ tedashi)
    /// must equal the engine's authoritative fromHand signal.
    /// </summary>
    private static void AssertInferenceMatches(DiscardTileEvent ev) {
      bool inferredFromHand =
          ev.discarded.drawnJun != ev.discarded.discardInfo.jun;
      Assert.AreEqual(ev.fromHand, inferredFromHand,
          "inferred tedashi/tsumogiri must match the engine's fromHand");
    }

    private static System.Collections.Generic.IEnumerable<GameTile> AllHeldTiles(
        Player player) {
      foreach (var t in player.hand.freeTiles) {
        yield return t;
      }
      if (player.hand.pendingTile != null) {
        yield return player.hand.pendingTile;
      }
    }
  }
}
