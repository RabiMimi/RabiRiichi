using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class Base13_1Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base13_1();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("1357s1357p1357m1z", "7z"));
            Assert.IsFalse(Resolve("19s19m19p1234567z", "2s"));
            Assert.IsFalse(Resolve("19s19m19p1234567z", null));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Resolve("19s19m19p1234567z", "7z"));
            Assert.IsTrue(Resolve("19s19m19p1234577z", "6z"));
            Assert.IsTrue(Resolve("9s19m19p234567z", "1z", "11s"));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }

        [TestMethod]
        public void TestShanten() {
            Assert.AreEqual(-1, Shanten("19m19p19s1234567z", "1z"));
            tiles.AssertEquals("");

            Assert.AreEqual(0, Shanten("19m19p19s1234567z", null));
            tiles.AssertEquals("19m19p19s1234567z");

            Assert.AreEqual(0, Shanten("19m19p129s124567z", "1z"));
            tiles.AssertEquals("2s");

            Assert.AreEqual(1, Shanten("19m19p129s124567z", null));
            tiles.AssertEquals("19m19p19s1234567z");

            Assert.AreEqual(1, Shanten("19m19p129s124567z", "3s"));
            tiles.AssertEquals("23s");

            Assert.AreEqual(0, Shanten("19m19p119s124567z", null));
            tiles.AssertEquals("3z");

            Assert.AreEqual(1, Shanten("19m19p1119s12456z", "6s"));
            tiles.AssertEquals("16s");

            Assert.AreEqual(1, Shanten("19m19p1119s12456z", "9s"));
            tiles.AssertEquals("19s");

            Assert.AreEqual(1, Shanten("19m19p1119s12456z", null));
            tiles.AssertEquals("37z");

            Assert.AreEqual(6, Shanten("1199m11999s1199p", "2s"));
            tiles.AssertEquals("19m129s19p");

            Assert.AreEqual(5, Shanten("1199m11999s1199p", "1z"));
            tiles.AssertEquals("19m19s19p");

            Assert.AreEqual(6, Shanten("1199m11999s1199p", null));
            tiles.AssertEquals("1234567z");

            Assert.AreEqual(13, Shanten("2233445566778s", "8s"));
            tiles.AssertEquals("2345678s");

            Assert.AreEqual(13, Shanten("2233445566778s", null));
            tiles.AssertEquals("19s19m19p1234567z");
        }

        [TestMethod]
        public void TestShantenMax() {
            Assert.AreEqual(5, Shanten("1199m11999s1199p", "1z", 5));
            tiles.AssertEquals("19m19s19p");

            Assert.AreEqual(6, Shanten("1199m11999s1199p", null, 6));
            tiles.AssertEquals("1234567z");

            Assert.AreEqual(int.MaxValue, Shanten("1199m11999s1199p", "1z", 4));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("1199m11999s1199p", null, 5));
            Assert.IsNull(tiles);

            Assert.AreEqual(-1, Shanten("19m19p19s1234567z", "1z", -1));
            tiles.AssertEquals("");
        }

        [TestMethod]
        public void TestShantenGroup() {
            Assert.AreEqual(int.MaxValue, Shanten("1199m11s1199p", "3s", 13, "999s"));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("1199m11s1199p", null, 13, "999s"));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("1199m11s999p", "1z", 13, "11p", "99s"));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("1199m11s999p", null, 13, "11p", "99s"));
            Assert.IsNull(tiles);

            Assert.AreEqual(0, Shanten("19m19s19p12345z", "6z", 13, "11p"));
            tiles.AssertEquals("1p");

            Assert.AreEqual(1, Shanten("19m19s19p12345z", null, 13, "11p"));
            tiles.AssertEquals("67z");

            Assert.AreEqual(0, Shanten("19m119s9p12345z", "6z", 13, "11p"));
            tiles.AssertEquals("1s");

            Assert.AreEqual(1, Shanten("19m119s9p12345z", null, 13, "11p"));
            tiles.AssertEquals("67z");
        }

        [TestMethod]
        public void TestShantenSpecial() {
            Assert.AreEqual(11, Shanten("6666666666666z", "6z"));
            tiles.AssertEquals("6z");

            Assert.AreEqual(11, Shanten("6666666666666z", null));
            tiles.AssertEquals("19m19s19p123457z");
        }
    }
}
