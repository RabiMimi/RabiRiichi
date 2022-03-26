using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Linq;


namespace RabiRiichiTests.Scenario {
    public static class Extensions {
        public static AgariInfo AssertTsumo(this AgariInfoList infos, int playerId) {
            Assert.AreEqual(playerId, infos.fromPlayer);
            Assert.AreEqual(1, infos.Count);
            Assert.IsTrue(infos.All(info => info.playerId == infos.fromPlayer));
            Assert.IsTrue(infos.incoming.IsTsumo);
            return infos[0];
        }

        public static AgariInfo AssertRon(this AgariInfoList infos, int fromPlayer, int ronPlayer) {
            Assert.AreEqual(fromPlayer, infos.fromPlayer);
            Assert.AreEqual(fromPlayer, infos.incoming.playerId);
            var info = infos.First((i) => i.playerId == ronPlayer);
            Assert.IsNotNull(info);
            return info;
        }

        public static AgariInfo AssertKazoeYakuman(this AgariInfo info) {
            Assert.IsTrue(info.scores.result.IsKazoeYakuman);
            return info;
        }

        public static AgariInfo AssertScore(this AgariInfo info, int? han = null, int? fu = null, int yakuman = 0) {
            if (han != null) {
                Assert.AreEqual(han, info.scores.result.han);
            }
            if (fu != null) {
                Assert.AreEqual(fu, info.scores.result.fu);
            }
            Assert.AreEqual(yakuman, info.scores.result.yakuman);
            return info;
        }

        public static AgariInfo AssertYakuNum(this AgariInfo info, int yakuNum) {
            Assert.AreEqual(yakuNum, info.scores.Count(s => s.Type != ScoringType.Fu));
            return info;
        }

        public static AgariInfo AssertYaku<T>(this AgariInfo info, int? han = null, int? fu = null, int? yakuman = null) where T : StdPattern {
            var yaku = info.scores.Find((score) => score.Source is T);
            Assert.IsNotNull(yaku);
            if (han != null) {
                Assert.AreEqual(ScoringType.Han, yaku.Type);
                Assert.AreEqual(han.Value, yaku.Val);
            }
            if (fu != null) {
                Assert.AreEqual(ScoringType.Fu, yaku.Type);
                Assert.AreEqual(fu.Value, yaku.Val);
            }
            if (yakuman != null) {
                Assert.AreEqual(ScoringType.Yakuman, yaku.Type);
                Assert.AreEqual(yakuman.Value, yaku.Val);
            }
            return info;
        }
    }
}