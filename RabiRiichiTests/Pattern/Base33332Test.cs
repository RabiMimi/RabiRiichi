using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base33332Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base33332();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Run("122334s5r566777p", null, out _));
            Assert.IsFalse(Run("122334s5r5667777p", "7p", out _));
            Assert.IsFalse(Run("1s", "7p", out _));
        }

        [TestMethod]
        public void TestShun() {
            Assert.IsTrue(Run("122334s5r566777p", "7p", out _));
            Assert.IsTrue(Run("122334s5r5667p", "7p", out _, "77p"));
            Assert.IsFalse(Run("122334s5677p", "7p", out _, "55p"));
            Assert.IsTrue(Run("122334s5r677p", "7p", out _, "567p"));
            Assert.IsFalse(Run("122334s5567p", "7p", out _, "567p"));
        }

        [TestMethod]
        public void Test9Lian() {
            Assert.IsTrue(Run("1112345678999s", "1s", out _));
            Assert.IsTrue(Run("1112345678999s", "2s", out _));
            Assert.IsTrue(Run("1112345678999s", "3s", out _));
            Assert.IsTrue(Run("1112345678999s", "4s", out _));
            Assert.IsTrue(Run("1112345678999s", "5s", out _));
            Assert.IsTrue(Run("1112345678999s", "6s", out _));
            Assert.IsTrue(Run("1112345678999s", "7s", out _));
            Assert.IsTrue(Run("1112345678999s", "8s", out _));
            Assert.IsTrue(Run("1112345678999s", "9s", out _));
        }

        [TestMethod]
        public void TestFrenqy() {
            Assert.IsTrue(Run("2223344556677s", "2s", out _));
            Assert.IsTrue(Run("1113335557779s", "9s", out _));
            Assert.IsTrue(Run("1113335557799s", "9s", out _));
            Assert.IsTrue(Run("111122334444s1z", "1z", out _));
            Assert.IsTrue(Run("1111222334444s", "2s", out _));
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsFalse(Run("2234556677s123z", "2s", out _));
        }

        [TestMethod]
        public void TestSpecial() {
            Assert.IsTrue(Run("6666666666666z", "6z", out _));
        }

        [TestMethod]
        public void TestKan() {
            Assert.IsTrue(Run("2z", "2z", out _, "1111s", "1111p", "1111m", "1111z"));
        }
    }
}
