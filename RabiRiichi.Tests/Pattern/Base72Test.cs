using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Tests.Helper;

namespace RabiRiichi.Tests.Pattern {
    [TestClass]
    public class Base72Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base72();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("1112233557799s", "1s"));
            Assert.IsFalse(Resolve("12233557799s", "1s", "11s"));
            Assert.IsFalse(Resolve("11335577s1133557p", "7p"));
            Assert.IsFalse(Resolve("1s", "1s"));
            Assert.IsFalse(Resolve("1133445p", "5p", "567s", "567s"));
            Assert.IsFalse(Resolve("113344p1s2233z", "1s", "5555s"));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Resolve("1122334455667p", "7p"));
            Assert.IsTrue(Resolve("11335r577p11335r5z", null));
            Assert.IsTrue(Resolve("119p1199m1199s22z", "9p"));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsTrue(Resolve("1123344556677z", "2z"));
        }

        [TestMethod]
        public void TestShanten() {
            Assert.AreEqual(-1, Shanten("2233445566778s", "8s"));
            tiles.AssertEquals("");

            Assert.AreEqual(0, Shanten("2233445566778s", null));
            tiles.AssertEquals("8s");

            Assert.AreEqual(5, Shanten("2222333344445s", "5s"));
            tiles.AssertEquals("234s");

            Assert.AreEqual(6, Shanten("2222333344445s", null));
            tiles.AssertEquals("156789s123456789p123456789m1234567z");

            Assert.AreEqual(5, Shanten("2223333444455s", "5s"));
            tiles.AssertEquals("2345s");

            Assert.AreEqual(5, Shanten("2223333444455s", null));
            tiles.AssertEquals("16789s123456789p123456789m1234567z");

            Assert.AreEqual(6, Shanten("123456789s1234p", "5p"));
            tiles.AssertEquals("123456789s12345p");

            Assert.AreEqual(5, Shanten("123456789s1234p", "4p"));
            tiles.AssertEquals("123456789s123p");

            Assert.AreEqual(6, Shanten("123456789s1234p", null));
            tiles.AssertEquals("123456789s1234p");
        }

        [TestMethod]
        public void TestShantenMax() {
            Assert.AreEqual(5, Shanten("2222333344445s", "5s", 5));
            tiles.AssertEquals("234s");

            Assert.AreEqual(6, Shanten("2222333344445s", null, 6));
            tiles.AssertEquals("156789s123456789p123456789m1234567z");

            Assert.AreEqual(int.MaxValue, Shanten("2222333344445s", "5s", 4));
            Assert.IsNull(tiles);

            Assert.AreEqual(int.MaxValue, Shanten("2222333344445s", null, 5));
            Assert.IsNull(tiles);
        }


        [TestMethod]
        public void TestShantenGroups() {
            Assert.AreEqual(int.MaxValue, Shanten("3333444455s", "5s", 7, "222s"));
            Assert.IsNull(tiles);

            Assert.AreEqual(5, Shanten("33334444555s", "5s", 7, "22s"));
            tiles.AssertEquals("345s");

            Assert.AreEqual(5, Shanten("33334444555s", null, 7, "22s"));
            tiles.AssertEquals("16789s123456789m123456789p1234567z");

            Assert.AreEqual(5, Shanten("22333344445s", "5s", 7, "22s"));
            tiles.AssertEquals("234s");

            Assert.AreEqual(6, Shanten("22333344445s", null, 7, "22s"));
            tiles.AssertEquals("156789s123456789m123456789p1234567z");

            Assert.AreEqual(1, Shanten("2s", "2s", 7,
                "22s", "33s", "44s", "55s", "66s", "77s"));
            tiles.AssertEquals("2s");

            Assert.AreEqual(1, Shanten("2s", null, 7,
                "22s", "33s", "44s", "55s", "66s", "77s"));
            tiles.AssertEquals("189s123456789m123456789p1234567z");

            Assert.AreEqual(0, Shanten("1s", "2s", 7,
                "22s", "33s", "44s", "55s", "66s", "77s"));
            tiles.AssertEquals("2s");

            Assert.AreEqual(0, Shanten("1s", null, 7,
                "22s", "33s", "44s", "55s", "66s", "77s"));
            tiles.AssertEquals("1s");
        }

        [TestMethod]
        public void TestShantenSpecial() {
            Assert.AreEqual(11, Shanten("6666666666666z", "6z"));
            tiles.AssertEquals("6z");

            Assert.AreEqual(11, Shanten("6666666666666z", null));
            tiles.AssertEquals("123456789m123456789p123456789s123457z");
        }
    }
}
