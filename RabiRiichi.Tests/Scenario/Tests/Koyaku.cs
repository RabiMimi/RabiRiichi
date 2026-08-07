using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
  /// <summary> 依赖引擎状态、单元测试无法覆盖真实链路的古役 </summary>
  [TestClass]
  public class ScenarioKoyaku {
    /// <summary> 123m678m123p678p 听9s单骑，荣和后本身没有任何常规役 </summary>
    private const string YAKULESS_TENPAI = "123m678m123p678p9s";

    #region 燕返し
    [TestMethod]
    public async Task TsubameGaeshi() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<TsubameGaeshi>()))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles("12366s234m34566p"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles(YAKULESS_TENPAI))
          .WithWall(wall => wall.Reserve("9s").SetRevealedDoraCount(0))
          .Start(1);

      // 玩家1立直，打出7s
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ChooseTile<RiichiAction>("9s")
      ).AssertAutoFinish();

      // 玩家2荣和立直宣言牌
      (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
          .AssertSkip()
          .ApplyAction<RonAction>()
      ).AssertAutoFinish();

      // 手牌本身无役，全靠燕返し成立
      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertRon(1, 2)
          .AssertYaku<TsubameGaeshi>(han: 1)
          .AssertYakuNum(1)
      ).Resolve();
    }

    [TestMethod]
    public async Task TsubameGaeshiRequiresRiichiDeclaration() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<TsubameGaeshi>()))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles("12366s234m34566p"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles(YAKULESS_TENPAI))
          .WithWall(wall => wall.Reserve("9s").SetRevealedDoraCount(0))
          .Start(1);

      // 玩家1只是普通打出9s，没有宣言立直
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ChooseTile<PlayTileAction>("9s")
      ).AssertAutoFinish();

      // 玩家2的手牌无役，不能荣和
      scenario.AssertNoEvent<AgariEvent>();
      await scenario.Resolve(2);
    }
    #endregion

    #region 槓振り
    [TestMethod]
    public async Task Kanburi() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Kanburi>()))
          .WithPlayer(1, playerBuilder => playerBuilder
              // 用字牌开杠，避免与玩家2的听牌争抢同一张牌
              .SetFreeTiles("4444z234s567s99p1z"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles(YAKULESS_TENPAI))
          .WithWall(wall => wall.Reserve("1s").AddRinshan("9s").SetRevealedDoraCount(0))
          .Start(1);

      // 玩家1摸牌后暗杠
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ChooseTiles<KanAction>("4444z")
      ).AssertAutoFinish();

      // 补摸岭上牌后打出9s
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ChooseTile<PlayTileAction>("9s")
      ).AssertAutoFinish();

      // 玩家2荣和开杠后打出的牌
      (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
          .AssertSkip()
          .ApplyAction<RonAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertRon(1, 2)
          .AssertYaku<Kanburi>(han: 1)
      ).Resolve();
    }
    #endregion

    #region 人和
    [TestMethod]
    public async Task Renhou() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Renhou>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("2233445566778m9s"))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles(YAKULESS_TENPAI))
          .WithWall(wall => wall.SetRevealedDoraCount(0))
          .SetFirstJun()
          .Start(0);

      // 庄家第一打
      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ChooseTile<PlayTileAction>("9s")
      ).AssertAutoFinish();

      // 闲家在自己摸牌前荣和第一张舍牌
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .AssertSkip()
          .ApplyAction<RonAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertRon(0, 1)
          .AssertScore(yakuman: 1)
          .AssertYaku<Renhou>(yakuman: 1)
      ).Resolve();
    }
    #endregion

    #region 十三不塔 / 十四不塔
    [TestMethod]
    public async Task Shiisanputa() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Shiisanputa>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              // 12张互不关联的单牌 + 一组对子
              .SetFreeTiles("147m147p147s12344z"))
          .WithWall(wall => wall.SetRevealedDoraCount(0))
          .SetFirstJun()
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertScore(yakuman: 1)
          .AssertYaku<Shiisanputa>(yakuman: 1)
      ).Resolve();
    }

    [TestMethod]
    public async Task Shiisuuputa() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Shiisuuputa>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              // 14张全部互不关联，连对子都没有
              .SetFreeTiles("147m147p147s12345z"))
          .WithWall(wall => wall.SetRevealedDoraCount(0))
          .SetFirstJun()
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertScore(yakuman: 1)
          .AssertYaku<Shiisuuputa>(yakuman: 1)
      ).Resolve();
    }

    [TestMethod]
    public async Task ButaCannotWinWithoutKoyaku() {
      // 不塔底和总是注册，但没有启用古役时该牌型无役，不能和牌
      var scenario = new ScenarioBuilder()
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("147m147p147s12344z"))
          .SetFirstJun()
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .AssertNoAction<TsumoAction>()
          .ChooseTile<PlayTileAction>("4z")
      ).AssertAutoFinish();
    }
    #endregion

    #region 八连荘
    [TestMethod]
    public async Task DealerAgariIncrementsStreak() {
      var scenario = new ScenarioBuilder()
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("123m456m789m123s9s"))
          .WithWall(wall => wall.Reserve("9s"))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario
          .AssertEvent<AgariEvent>()
          .AssertEvent<NextGameEvent>(ev => {
            Assert.AreEqual(1, ev.game.GetPlayer(0).agariStreak, "dealer streak");
            foreach (var player in ev.game.players) {
              if (!player.IsDealer) {
                Assert.AreEqual(0, player.agariStreak, $"player {player.id} streak");
              }
            }
          })
          .Resolve();
    }
    #endregion

    #region 清盃口
    [TestMethod]
    public async Task ChinpeikouStacksWithRyanpeikou() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Chinpeikou>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              // 778899m + 778899s + 1p 单骑
              .SetFreeTiles("778899m778899s1p"))
          .WithWall(wall => wall.Reserve("1p").SetRevealedDoraCount(0))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      // 清盃口加计3番，与二盃口的3番复合
      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertYaku<Chinpeikou>(han: 3)
          .AssertYaku<Ryanpeikou>(han: 3)
      ).Resolve();
    }
    #endregion

    #region 大七星
    [TestMethod]
    public async Task DaichiishinRemovesTsuuiisou() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Daichiishin>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("1122334455667z"))
          .WithWall(wall => wall.Reserve("7z").SetRevealedDoraCount(0))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertScore(yakuman: 2)
          .AssertYaku<Daichiishin>(yakuman: 2)
          .AssertNoYaku<Tsuuiisou>()
      ).Resolve();
    }
    #endregion

    #region 小四喜移除下位古役
    [TestMethod]
    public async Task ShousuushiiRemovesWindKoyaku() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(setup => {
            setup.AddExtraStdPattern<Sanfonkou>();
            setup.AddExtraStdPattern<Shousanfon>();
          }))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("111z22233z44z123m"))
          .WithWall(wall => wall.Reserve("3z").SetRevealedDoraCount(0))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertYaku<Shousuushii>(yakuman: 1)
          .AssertNoYaku<Sanfonkou>()
          .AssertNoYaku<Shousanfon>()
      ).Resolve();
    }

    [TestMethod]
    public async Task DaisuushiiRemovesSanfonkou() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.Setup(
              setup => setup.AddExtraStdPattern<Sanfonkou>()))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("111222333444z1p"))
          .WithWall(wall => wall.Reserve("1p").SetRevealedDoraCount(0))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertYaku<Daisuushii>(yakuman: 2)
          .AssertNoYaku<Sanfonkou>()
      ).Resolve();
    }
    #endregion
  }
}
