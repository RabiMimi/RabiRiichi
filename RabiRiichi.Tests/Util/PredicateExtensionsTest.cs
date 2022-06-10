using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Patterns;
using RabiRiichi.Util;

namespace RabiRiichi.Tests.Util {
    [TestClass]
    public class PredicateExtensionsTest {
        private static readonly Tanyao tanyao = new(null);
        private static readonly Riichi riichi = new(null);

        [TestMethod]
        public void TestGetPredicate() {
            var list = new StdPattern[] { tanyao };
            Assert.IsTrue(tanyao.GetPredicate()(list));
            Assert.IsFalse(riichi.GetPredicate()(list));
        }

        [TestMethod]
        public void TestOr() {
            var list = new StdPattern[] { tanyao };
            Assert.IsTrue(tanyao.GetPredicate().Or(riichi.GetPredicate())(list));
            Assert.IsTrue(tanyao.Or(tanyao.GetPredicate())(list));
            Assert.IsFalse(riichi.GetPredicate().Or(riichi)(list));
            Assert.IsTrue(riichi.Or(tanyao)(list));
        }

        [TestMethod]
        public void TestAnd() {
            var all = new StdPattern[] { tanyao, riichi };
            var tanyaoOnly = new StdPattern[] { tanyao };
            Assert.IsTrue(tanyao.GetPredicate().And(riichi.GetPredicate())(all));
            Assert.IsFalse(tanyao.And(riichi.GetPredicate())(tanyaoOnly));
            Assert.IsFalse(riichi.GetPredicate().And(tanyao)(tanyaoOnly));
            Assert.IsTrue(riichi.And(tanyao)(all));
        }

        [TestMethod]
        public void TestNot() {
            var list = new StdPattern[] { tanyao };
            Assert.IsFalse(tanyao.GetPredicate().Not()(list));
            Assert.IsTrue(riichi.GetPredicate().Not()(list));
            Assert.IsFalse(tanyao.Not()(list));
            Assert.IsTrue(riichi.Not()(list));
        }
    }
}