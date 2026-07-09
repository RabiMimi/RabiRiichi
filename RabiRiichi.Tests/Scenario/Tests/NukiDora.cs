using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using System.Linq;
using System.Threading.Tasks;


namespace RabiRiichi.Tests.Scenario.Tests {
  [TestClass]
  public class ScenarioNukiDora {
    private static ScenarioBuilder Builder(DoraOption dora = DoraOption.Nukidora) {
      return new ScenarioBuilder().WithConfig(config => config.SetDoraOption(dora));
    }

    #region Basic pull

    [TestMethod]
    public async Task SuccessNukiFromDraw() {
      // P1 draws a North and pulls it.
      var scenario = Builder()
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.AssertAction<PlayTileAction>()
          .ChooseTiles<NukiDoraAction>("4z", action => {
            Assert.AreEqual(1, action.options.Count);
          });
      }).AssertAutoFinish();

      scenario
          .AssertEvent<NukiDoraEvent>(ev => Assert.AreEqual(1, ev.playerId))
          .AssertEvent<AddNukiDoraEvent>(ev => Assert.AreEqual(1, ev.playerId))
          // Replacement is drawn from the dead wall.
          .AssertEvent<DrawTileEvent>(ev => Assert.AreEqual(TileSource.Wanpai, ev.source))
          // No new dora indicator is revealed on a nuki.
          .AssertNoEvent<RevealDoraEvent>();

      // Wait for P1's follow-up turn (after the replacement draw) so all queued
      // events are processed, then verify state.
      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.AssertAction<PlayTileAction>();
      });

      // North is set aside, hand size preserved, replacement drawn.
      scenario.WithPlayer(1, p => {
        var hand = p.hand;
        Assert.AreEqual(1, hand.nukiDora.Count);
        Assert.IsTrue(hand.nukiDora[0].tile.IsSame(Tile.North));
        Assert.AreEqual(TileSource.Nuki, hand.nukiDora[0].source);
        Assert.AreEqual(Game.HAND_SIZE, hand.Count);
        Assert.IsFalse(hand.freeTiles.Any(t => t.tile.IsSame(Tile.North)));
      });
    }

    [TestMethod]
    public async Task SuccessNukiFromHand() {
      // P1 already holds a North in hand and draws a non-North tile.
      var scenario = Builder()
          .WithPlayer(1, pb => pb.SetFreeTiles("12345678m123p1s4z"))
          .WithWall(wall => wall.Reserve("9m").AddRinshan("9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      scenario.AssertEvent<AddNukiDoraEvent>().AssertNoEvent<RevealDoraEvent>();
      (await scenario.WaitInquiry()).ForPlayer(1, pi => pi.AssertAction<PlayTileAction>());
      scenario.WithPlayer(1, p => {
        Assert.AreEqual(1, p.hand.nukiDora.Count);
        Assert.AreEqual(Game.HAND_SIZE, p.hand.Count);
      });
    }

    [TestMethod]
    public async Task SuccessMultipleNuki() {
      // P1 draws North, pulls, draws another North from rinshan area, pulls again.
      var scenario = Builder()
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("4z9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      // After the first nuki the replacement is another North, which can be pulled again.
      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      (await scenario.WaitInquiry()).ForPlayer(1, pi => pi.AssertAction<PlayTileAction>());
      scenario.WithPlayer(1, p => {
        Assert.AreEqual(2, p.hand.nukiDora.Count);
        Assert.AreEqual(Game.HAND_SIZE, p.hand.Count);
      });
    }

    #endregion

    #region Gating

    [TestMethod]
    public async Task FailNukiWhenDisabled() {
      // Nukidora flag off: no nuki action even holding North.
      var scenario = Builder(DoraOption.Default)
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.AssertAction<PlayTileAction>()
          .AssertNoAction<NukiDoraAction>();
      }).AssertAutoFinish(false);
    }

    [TestMethod]
    public async Task FailNukiWithoutNorth() {
      // Holding other winds is not pullable.
      var scenario = Builder()
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1z"))
          .WithWall(wall => wall.Reserve("2z"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.AssertAction<PlayTileAction>()
          .AssertNoAction<NukiDoraAction>();
      }).AssertAutoFinish(false);
    }

    #endregion

    #region Scoring

    [TestMethod]
    public async Task NukiCountsAsDora() {
      // P1 is tenpai on 1z. Draw North, pull it, then the rinshan replacement
      // 1z completes the hand: tsumo scores RinshanKaihou (yaku) + NukiDora.
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithPlayer(1, pb => pb.SetFreeTiles("111s123456789m1z"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("1z"))
          .Start(1);

      // Pull the drawn North.
      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      // Rinshan replacement 1z completes the hand.
      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(1)
          .AssertYaku<RinshanKaihou>(han: 1)
          .AssertYaku<NukiDora>(han: 1)
      ).Resolve();
    }

    [TestMethod]
    public async Task NukiDoraDoesNotSatisfyMinHan() {
      // minHan=3. After pull, the rinshan replacement completes a plain hand
      // whose only yaku are MenzenTsumo (1) + RinshanKaihou (1) = 2 yaku han,
      // plus 1 nukidora (bonus). Since nukidora is a bonus (not yaku), yaku han
      // stays 2 < 3, so tsumo is refused — proving nukidora doesn't count.
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithConfig(config => config.SetMinHan(3))
          .WithPlayer(1, pb => pb.SetFreeTiles("234m567m234p678s1z"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("1z"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.AssertNoAction<TsumoAction>();
      }).AssertAutoFinish(false);
    }

    [TestMethod]
    public async Task NorthInWinningHandDoesNotCountAsKita() {
      // North kept in the hand (as a triplet) is NOT nukidora — only pulled
      // North are. Shanpon tenpai on 9p/4z; tsumo the third North to form 444z.
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithPlayer(1, pb => pb.SetFreeTiles("123m456m789m99p44z"))
          .WithWall(wall => wall.Reserve("4z"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        // Draw the third North completing the 444z triplet: tsumo.
        pi.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => {
        var info = ev.agariInfos.AssertTsumo(1);
        Assert.IsFalse(info.scores.Any(s => s.Source is NukiDora),
            "North kept in hand must not score nukidora.");
      }).Resolve();
    }

    [TestMethod]
    public async Task WestIndicatorNorthInHandIsPlainDoraOnly() {
      // Indicator West (3z) -> North (4z) is a regular dora. North kept in hand
      // scores Dora (+3) but NOT nukidora (they were never pulled).
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithPlayer(1, pb => pb.SetFreeTiles("123m456m789m99p44z"))
          .WithWall(wall => wall.Reserve("4z").AddDoras("3z").SetRevealedDoraCount(1))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => {
        var info = ev.agariInfos.AssertTsumo(1);
        info.AssertYaku<Dora>(han: 3);
        Assert.IsFalse(info.scores.Any(s => s.Source is NukiDora),
            "North kept in hand must not score nukidora.");
      }).Resolve();
    }

    [TestMethod]
    public async Task PulledNorthCountsAsDoraWhenIndicatorWest() {
      // A pulled-aside North is set apart from the winning hand, but with a West
      // (3z) indicator it must still earn a regular Dora on top of its NukiDora.
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithPlayer(1, pb => pb.SetFreeTiles("111s123456789m1z"))
          .WithWall(wall => wall
              .Reserve("4z")
              .AddRinshan("1z")
              .AddDoras("3z")
              .SetRevealedDoraCount(1))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(1)
          .AssertYaku<NukiDora>(han: 1)
          .AssertYaku<Dora>(han: 1)
      ).Resolve();
    }

    [TestMethod]
    public async Task PulledNorthCountsAsUradoraWhenIndicatorWest() {
      // In riichi, a pulled-aside North must also earn uradora when the ura
      // indicator is West (3z).
      var scenario = Builder(DoraOption.Default | DoraOption.Nukidora)
          .WithPlayer(1, pb => pb
              .SetFreeTiles("111s123456789m1z")
              .SetRiichiTile("1z"))
          .WithWall(wall => wall
              .Reserve("4z")
              .AddRinshan("1z")
              .AddUradoras("3z")
              .SetRevealedUradoraCount(1))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(1)
          .AssertYaku<NukiDora>(han: 1)
          .AssertYaku<Uradora>(han: 1)
      ).Resolve();
    }

    #endregion

    #region Chankan (搶拔北)

    [TestMethod]
    public async Task SuccessRobNukiByTenpai() {
      // P0 is tenpai (tanki on 4z) with honitsu (manzu + honors), so robbing the
      // pulled North gives a valid yaku.
      var scenario = Builder()
          .WithPlayer(0, pb => pb.SetFreeTiles("123456789m111z4z"))
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      scenario.AssertEvent<NukiDoraEvent>();

      (await scenario.WaitInquiry()).ForPlayer(0, pi => {
        pi.AssertSkip()
          .ApplyAction<RonAction>()
          .AssertNoMoreActions();
      }).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos.AssertRon(1, 0)).Resolve();

      // The robbed North must NOT have been added to P1's nukidora area.
      scenario.WithPlayer(1, p => Assert.AreEqual(0, p.hand.nukiDora.Count));
    }

    [TestMethod]
    public async Task FailRobNukiWithoutYaku() {
      // P0 is tenpai (tanki on 4z) but the shape has no yaku, so winning on 4z
      // is impossible -> cannot rob (無役不能搶).
      var scenario = Builder()
          .WithPlayer(0, pb => pb.SetFreeTiles("234m567m234p567p4z"))
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      // No ron offered to P0; the nuki proceeds normally.
      (await scenario.WaitInquiry()).AssertNoActionForPlayer(0);
      scenario.AssertEvent<AddNukiDoraEvent>();
    }

    #endregion

    #region Flags

    [TestMethod]
    public async Task NukiPreservesMenzen() {
      var scenario = Builder()
          .WithPlayer(1, pb => pb.SetFreeTiles("123456789m123p1s"))
          .WithWall(wall => wall.Reserve("4z").AddRinshan("9p"))
          .Start(1);

      (await scenario.WaitInquiry()).ForPlayer(1, pi => {
        pi.ChooseTiles<NukiDoraAction>("4z");
      }).AssertAutoFinish();

      scenario.WithPlayer(1, p => Assert.IsTrue(p.hand.menzen));
    }

    #endregion
  }
}
