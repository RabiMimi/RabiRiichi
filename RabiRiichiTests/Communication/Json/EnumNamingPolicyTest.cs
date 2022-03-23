
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication.Json;


namespace RabiRiichiTests.Communication.Json {
    [TestClass]
    public class EnumNamingPolicyTest {
        private static readonly EnumNamingPolicy V = new();

        [TestMethod]
        public void TestRegular() {
            Assert.AreEqual("regular", V.ConvertName("regular"));
            Assert.AreEqual("regular", V.ConvertName("Regular"));
            Assert.AreEqual("regular_message", V.ConvertName("RegularMessage"));
            Assert.AreEqual("regular_message", V.ConvertName("regular_message"));
        }

        [TestMethod]
        public void TestEmpty() {
            Assert.AreEqual("", V.ConvertName(""));
        }
    }
}