using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace RabiRiichiTests.Util {
    [TestClass]
    public class RabiEnumerableTest {
        public readonly int[] source = new int[] { 1, 1, 4, 5, 1, 4 };

        [TestMethod]
        public void TestAll() {
            Assert.IsTrue(source.All((x, idx) => x == 1 || idx > 1));
            Assert.IsFalse(source.All((x, idx) => x == 1 || idx > 2));
        }

        [TestMethod]
        public void TestSmallSubset() {
            var groups = source.Skip(1).Subset(3).ToArray();
            Assert.AreEqual((source.Length - 1) * (source.Length - 2) / 2, groups.Length);
            CollectionAssert.AreEqual(new int[] { 1, 4, 5 }, groups[0].ToArray());
        }

        [TestMethod]
        public void TestLargeSubset() {
            var groups = source.Concat(source).Subset(2).ToArray();
            Assert.AreEqual(source.Length * (source.Length * 2 - 1), groups.Length);
            CollectionAssert.AreEqual(new int[] { 1, 1 }, groups[0].ToArray());
        }

        [TestMethod]
        public void TestSpecialSubset() {
            var noElement = source.Subset(0).ToArray();
            Assert.AreEqual(1, noElement.Length);
            Assert.AreEqual(0, noElement[0].Count());

            var allElements = source.Subset(source.Length).ToArray();
            Assert.AreEqual(1, allElements.Length);
            CollectionAssert.AreEqual(source, allElements[0].ToArray());
        }

        [TestMethod]
        public void TestInvalidSubset() {
            Assert.AreEqual(0, source.Subset(-1).Count());
            Assert.AreEqual(0, source.Subset(source.Length + 1).Count());
        }

        [TestMethod]
        public void TestMaxBy() {
            Assert.AreEqual(1, source.MaxBy(x => -x));
            Assert.ThrowsException<InvalidOperationException>(() => Enumerable.Empty<int>().MaxBy(x => x));
        }
    }
}