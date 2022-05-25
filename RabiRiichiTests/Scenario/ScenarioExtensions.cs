using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Linq;


namespace RabiRiichiTests.Scenario {
    public static class Extensions {
        public static AgariInfo AssertTsumo(this AgariInfoList infos, int playerId) {
            Assert.AreEqual(playerId, infos.fromPlayer, $"expected tsumo from player {playerId}, got {infos.fromPlayer}");
            Assert.AreEqual(1, infos.Count, "More than one tsumo");
            Assert.IsTrue(infos.All(info => info.playerId == infos.fromPlayer), "tsumo from other player");
            Assert.IsTrue(infos.incoming.IsTsumo, "incoming tile is not tsumo");
            return infos[0];
        }

        public static AgariInfo AssertRon(this AgariInfoList infos, int fromPlayer, int ronPlayer) {
            Assert.AreEqual(fromPlayer, infos.fromPlayer, $"expected ron from player {fromPlayer}, got {infos.fromPlayer}");
            Assert.AreEqual(fromPlayer, infos.incoming.playerId, $"expected agari tile from player {fromPlayer}, got {infos.incoming.playerId}");
            var info = infos.First((i) => i.playerId == ronPlayer);
            Assert.IsNotNull(info, $"ron player {ronPlayer} not found");
            return info;
        }

        public static AgariInfo AssertKazoeYakuman(this AgariInfo info) {
            Assert.IsTrue(info.scores.result.IsKazoeYakuman, "not kazoe yakuman");
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
            Assert.IsNotNull(yaku, $"yaku {typeof(T).Name} not found");
            if (han != null) {
                Assert.IsTrue(yaku.Type == ScoringType.Han || yaku.Type == ScoringType.BonusHan);
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