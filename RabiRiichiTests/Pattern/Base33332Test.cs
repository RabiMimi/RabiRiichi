using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base33332Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base33332();
        private Tiles tiles;

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("122334s5r566777p", null, out _));
            Assert.IsFalse(Resolve("122334s5r5667777p", "7p", out _));
            Assert.IsFalse(Resolve("1s", "7p", out _));
        }

        [TestMethod]
        public void TestShun() {
            Assert.IsTrue(Resolve("122334s5r566777p", "7p", out _));
            Assert.IsTrue(Resolve("122334s5r5667p", "7p", out _, "77p"));
            Assert.IsFalse(Resolve("122334s5677p", "7p", out _, "55p"));
            Assert.IsTrue(Resolve("122334s5r677p", "7p", out _, "567p"));
            Assert.IsFalse(Resolve("122334s5567p", "7p", out _, "567p"));
        }

        [TestMethod]
        public void Test9Lian() {
            Assert.IsTrue(Resolve("1112345678999s", "1s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "2s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "3s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "4s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "5s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "6s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "7s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "8s", out _));
            Assert.IsTrue(Resolve("1112345678999s", "9s", out _));
        }

        [TestMethod]
        public void TestFrenqy() {
            Assert.IsTrue(Resolve("2223344556677s", "2s", out _));
            Assert.IsTrue(Resolve("1113335557779s", "9s", out _));
            Assert.IsTrue(Resolve("1113335557799s", "9s", out _));
            Assert.IsTrue(Resolve("111122334444s1z", "1z", out _));
            Assert.IsTrue(Resolve("1111222334444s", "2s", out _));
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsFalse(Resolve("2234556677s123z", "2s", out _));
        }

        [TestMethod]
        public void TestSpecial() {
            Assert.IsTrue(Resolve("6666666666666z", "6z", out _));
        }

        [TestMethod]
        public void TestKan() {
            Assert.IsTrue(Resolve("2z", "2z", out _, "1111s", "1111p", "1111m", "1111z"));
        }

        [TestMethod]
        public void TestShanten() {

            Assert.AreEqual(-1, Shanten("2233445566778s", "8s", out tiles));
            tiles.AssertEq("");

            Assert.AreEqual(0, Shanten("2233445566778s", null, out tiles));
            tiles.AssertEq("258s");

            Assert.AreEqual(0, Shanten("1112345678999s", "1p", out tiles));
            tiles.AssertEq("258s1p");

            Assert.AreEqual(0, Shanten("1112345678999s", null, out tiles));
            tiles.AssertEq("123456789s");

            Assert.AreEqual(8, Shanten("159s159p159m1234z", "5z", out tiles));
            tiles.AssertEq("159s159p159m12345z");

            Assert.AreEqual(8, Shanten("159s159p159m1234z", null, out tiles));
            tiles.AssertEq("123456789s123456789p123456789m1234z");

            Assert.AreEqual(5, Shanten("25569m2589p5s357z", "3s", out tiles));
            tiles.AssertEq("2569m25p357z");

            Assert.AreEqual(6, Shanten("25569m2589p5s357z", null, out tiles));
            tiles.AssertEq("123456789m1234567p34567s357z");
        }

        [TestMethod]
        public void TestShantenSpecial() {
            Assert.AreEqual(0, Shanten("6666666666666z", "7z", out tiles));
            tiles.AssertEq("67z");

            Assert.AreEqual(0, Shanten("6666666666666z", null, out tiles));
            tiles.AssertEq("6z");
        }

        [TestMethod]
        public void TestShantenFrenqy() {
        }
    }
}
