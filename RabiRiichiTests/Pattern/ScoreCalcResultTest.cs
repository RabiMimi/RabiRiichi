using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core.Config;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class ScoreCalcResultTest {
        #region Regular
        private static ScoreCalcResult Create(int han = 0, int fu = 20, int yakuman = 0, ScoringOption option = ScoringOption.Default) {
            return new ScoreCalcResult(option) {
                han = han,
                yaku = han,
                fu = fu,
                yakuman = yakuman
            };
        }

        [TestMethod]
        public void SuccessScoreCalculation() {
            Assert.AreEqual(160, Create(han: 1, fu: 20).BaseScore);
            Assert.AreEqual(240, Create(han: 1, fu: 30).BaseScore);
            Assert.AreEqual(320, Create(han: 1, fu: 40).BaseScore);
            Assert.AreEqual(400, Create(han: 1, fu: 50).BaseScore);
            Assert.AreEqual(480, Create(han: 1, fu: 60).BaseScore);
            Assert.AreEqual(560, Create(han: 1, fu: 70).BaseScore);
            Assert.AreEqual(640, Create(han: 1, fu: 80).BaseScore);
            Assert.AreEqual(720, Create(han: 1, fu: 90).BaseScore);
            Assert.AreEqual(800, Create(han: 1, fu: 100).BaseScore);
            Assert.AreEqual(320, Create(han: 2, fu: 20).BaseScore);
            Assert.AreEqual(480, Create(han: 2, fu: 30).BaseScore);
            Assert.AreEqual(640, Create(han: 2, fu: 40).BaseScore);
            Assert.AreEqual(800, Create(han: 2, fu: 50).BaseScore);
            Assert.AreEqual(960, Create(han: 2, fu: 60).BaseScore);
            Assert.AreEqual(1120, Create(han: 2, fu: 70).BaseScore);
            Assert.AreEqual(1280, Create(han: 2, fu: 80).BaseScore);
            Assert.AreEqual(1440, Create(han: 2, fu: 90).BaseScore);
            Assert.AreEqual(1600, Create(han: 2, fu: 100).BaseScore);
            Assert.AreEqual(640, Create(han: 3, fu: 20).BaseScore);
            Assert.AreEqual(960, Create(han: 3, fu: 30).BaseScore);
            Assert.AreEqual(1280, Create(han: 3, fu: 40).BaseScore);
            Assert.AreEqual(1600, Create(han: 3, fu: 50).BaseScore);
            Assert.AreEqual(1920, Create(han: 3, fu: 60).BaseScore);
            Assert.AreEqual(1280, Create(han: 4, fu: 20).BaseScore);
            Assert.AreEqual(1920, Create(han: 4, fu: 30).BaseScore);
            Assert.AreEqual(0, Create(han: 4, fu: 30).KazoeYakuman);
            Assert.AreEqual(0, Create(han: 4, fu: 30).FinalYakuman);
        }

        [TestMethod]
        public void SuccessMangan() {
            Assert.AreEqual(2000, Create(han: 4, fu: 40).BaseScore);
            Assert.AreEqual(2000, Create(han: 5, fu: 20).BaseScore);
        }

        [TestMethod]
        public void SuccessKiriageMangan() {
            Assert.AreEqual(1920, Create(han: 4, fu: 30).BaseScore);
            Assert.AreEqual(2000, Create(han: 4, fu: 30,
                option: ScoringOption.Default | ScoringOption.KiriageMangan).BaseScore);
        }

        [TestMethod]
        public void SuccessHaneman() {
            Assert.AreEqual(3000, Create(han: 6, fu: 20).BaseScore);
            Assert.AreEqual(3000, Create(han: 7, fu: 20).BaseScore);
        }

        [TestMethod]
        public void SuccessBaiman() {
            Assert.AreEqual(4000, Create(han: 8, fu: 20).BaseScore);
            Assert.AreEqual(4000, Create(han: 9, fu: 20).BaseScore);
            Assert.AreEqual(4000, Create(han: 10, fu: 20).BaseScore);
        }

        [TestMethod]
        public void SuccessSanbaiman() {
            Assert.AreEqual(6000, Create(han: 11, fu: 20).BaseScore);
            Assert.AreEqual(6000, Create(han: 12, fu: 20).BaseScore);
        }
        #endregion

        #region Yakuman
        [TestMethod]
        public void SuccessYakuman() {
            var result = Create(yakuman: 1);
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
            result.yakuman = 2;
            Assert.AreEqual(16000, result.BaseScore);
            Assert.AreEqual(2, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
        }

        [TestMethod]
        public void SuccessYakuman_NoMultipleYakuman() {
            var result = Create(yakuman: 1,
                option: ScoringOption.Default & ~ScoringOption.MultipleYakuman);
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
            result.yakuman = 2;
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
        }

        [TestMethod]
        public void SuccessKazoeYakuman() {
            var result = Create(han: 13, fu: 20);
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(1, result.KazoeYakuman);
            result.han = 26;
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(1, result.KazoeYakuman);
        }

        [TestMethod]
        public void DisabledKazoeYakuman() {
            var result = Create(han: 13, fu: 20,
                option: ScoringOption.Default & ~ScoringOption.KazoeYakuman);
            Assert.AreEqual(6000, result.BaseScore);
            Assert.AreEqual(0, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
            result.han = 26;
            Assert.AreEqual(6000, result.BaseScore);
            Assert.AreEqual(0, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
        }

        [TestMethod]
        public void SuccessYakuman_WithKazoeYakuman() {
            var result = Create(han: 13, fu: 20, yakuman: 1);
            Assert.AreEqual(8000, result.BaseScore);
            Assert.AreEqual(1, result.FinalYakuman);
            Assert.AreEqual(1, result.KazoeYakuman);
            result.yakuman = 2;
            Assert.AreEqual(16000, result.BaseScore);
            Assert.AreEqual(2, result.FinalYakuman);
            Assert.AreEqual(1, result.KazoeYakuman);
        }
        #endregion

        #region Aotenjou
        [TestMethod]
        public void SuccessAotenjou() {
            var option = ScoringOption.Aotenjou;
            Assert.AreEqual(1920, Create(han: 4, fu: 30, option: option).BaseScore);
            Assert.AreEqual(1310720, Create(han: 13, fu: 40, option: option).BaseScore);
            var result = Create(han: 26, fu: 40, yakuman: 2, option: option);
            Assert.AreEqual(720575940379279360L, result.BaseScore);
            Assert.AreEqual(0, result.FinalYakuman);
            Assert.AreEqual(0, result.KazoeYakuman);
        }
        #endregion
    }
}