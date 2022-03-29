﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichiTests.Helper;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base33332Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base33332();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("122334s5r566777p", null));
            Assert.IsFalse(Resolve("122334s5r5667777p", "7p"));
            Assert.IsFalse(Resolve("1s", "7p"));
        }

        [TestMethod]
        public void TestShun() {
            Assert.IsTrue(Resolve("122334s5r566777p", "7p"));
            Assert.IsTrue(Resolve("122334s5r5667p", "7p", "77p"));
            Assert.IsFalse(Resolve("122334s5677p", "7p", "55p"));
            Assert.IsTrue(Resolve("122334s5r677p", "7p", "567p"));
            Assert.IsFalse(Resolve("122334s5567p", "7p", "567p"));
        }

        [TestMethod]
        public void Test9Lian() {
            Assert.IsTrue(Resolve("1112345678999s", "1s"));
            Assert.IsTrue(Resolve("1112345678999s", "2s"));
            Assert.IsTrue(Resolve("1112345678999s", "3s"));
            Assert.IsTrue(Resolve("1112345678999s", "4s"));
            Assert.IsTrue(Resolve("1112345678999s", "5s"));
            Assert.IsTrue(Resolve("1112345678999s", "6s"));
            Assert.IsTrue(Resolve("1112345678999s", "7s"));
            Assert.IsTrue(Resolve("1112345678999s", "8s"));
            Assert.IsTrue(Resolve("1112345678999s", "9s"));
        }

        [TestMethod]
        public void TestFrenqy() {
            Assert.IsTrue(Resolve("2223344556677s", "2s"));
            Assert.IsTrue(Resolve("1113335557779s", "9s"));
            Assert.IsTrue(Resolve("1113335557799s", "9s"));
            Assert.IsTrue(Resolve("111122334444s1z", "1z"));
            Assert.IsTrue(Resolve("1111222334444s", "2s"));
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsFalse(Resolve("2234556677s123z", "2s"));
        }

        [TestMethod]
        public void TestSpecial() {
            Assert.IsTrue(Resolve("6666666666666z", "6z"));
        }

        [TestMethod]
        public void TestKan() {
            Assert.IsTrue(Resolve("2z", "2z", "1111s", "1111p", "1111m", "1111z"));
        }

        [TestMethod]
        public void TestShanten() {

            Assert.AreEqual(-1, Shanten("2233445566778s", "8s"));
            tiles.AssertEq("");

            Assert.AreEqual(0, Shanten("2233445566778s", null));
            tiles.AssertEq("258s");

            Assert.AreEqual(0, Shanten("1112345678999s", "1p"));
            tiles.AssertEq("258s1p");

            Assert.AreEqual(0, Shanten("1112345678999s", null));
            tiles.AssertEq("123456789s");

            Assert.AreEqual(8, Shanten("159s159p159m1234z", "5z"));
            tiles.AssertEq("159s159p159m12345z");

            Assert.AreEqual(8, Shanten("159s159p159m1234z", null));
            tiles.AssertEq("123456789s123456789p123456789m1234z");

            Assert.AreEqual(5, Shanten("25569m2589p5s357z", "3s"));
            tiles.AssertEq("2569m25p357z");

            Assert.AreEqual(6, Shanten("25569m2589p5s357z", null));
            tiles.AssertEq("123456789m1234567p34567s357z");
        }

        [TestMethod]
        public void TestShantenMax() {
            Assert.AreEqual(5, Shanten("25569m2589p5s357z", "3s", 5));
            tiles.AssertEq("2569m25p357z");

            Assert.AreEqual(int.MaxValue, Shanten("25569m2589p5s357z", null, 5));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("25569m2589p5s357z", "3s", 4));
            Assert.IsNull(tiles);

            Assert.AreEqual(6, Shanten("25569m2589p5s357z", null, 6));
            tiles.AssertEq("123456789m1234567p34567s357z");
        }

        [TestMethod]
        public void TestShantenGroups() {
            Assert.AreEqual(int.MaxValue, Shanten("1s", "1s", 8,
                "222s", "345s", "11p", "11z", "22z"));
            Assert.IsNull(tiles);

            Assert.AreEqual(-1, Shanten("1s", "1s", 8,
                "222s", "345s", "111p", "111z"));
            tiles.AssertEq("");

            Assert.AreEqual(0, Shanten("1s", "2s", 8,
                "222s", "345s", "111p", "111z"));
            tiles.AssertEq("12s");

            Assert.AreEqual(0, Shanten("1s", null, 8,
                "222s", "345s", "111p", "111z"));
            tiles.AssertEq("1s");

            Assert.AreEqual(0, Shanten("45s", "2s", 8,
                "222s", "345s", "111p", "11z"));
            tiles.AssertEq("25s");

            Assert.AreEqual(0, Shanten("45s", null, 8,
                "222s", "345s", "111p", "11z"));
            tiles.AssertEq("36s");
        }

        [TestMethod]
        public void TestShantenSpecial() {
            Assert.AreEqual(0, Shanten("6666666666666z", "7z"));
            tiles.AssertEq("67z");

            Assert.AreEqual(0, Shanten("6666666666666z", null));
            tiles.AssertEq("6z");
        }

        [TestMethod]
        public void TestShantenFrenqy() {
            Assert.AreEqual(-1, Shanten("1113335557779s", "9s"));
            tiles.AssertEq("");

            Assert.AreEqual(0, Shanten("1113335557799s", null));
            tiles.AssertEq("79s");

            Assert.AreEqual(5, Shanten("1246m1p2688s1257z", "2m"));
            tiles.AssertEq("126m268s1p1257z");

            Assert.AreEqual(1, Shanten("1224r5p56668889p", "7p"));
            tiles.AssertEq("12456789p");

            Assert.AreEqual(6, Shanten("348m247p159s1234z", "5z"));
            tiles.AssertEq("12345z159s8m7p");
        }
    }
}
