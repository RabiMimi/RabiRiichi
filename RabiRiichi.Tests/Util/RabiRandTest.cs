using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Tests.Util {
    [TestClass]
    public class RabiRandTest {
        private RabiRand rand;

        [TestInitialize]
        public void Setup() {
            rand = new RabiRand(114514);
        }

        [TestMethod]
        public void TestRabiRandDistribution() {
            int randCount = 1000000;
            int bucketCount = 100;
            float errRate = 0.1f;
            var histogram = new int[bucketCount];
            for (var i = 0; i < randCount; i++) {
                histogram[rand.Next(bucketCount)]++;
            }

            int maxValue = (int)(randCount / bucketCount * (1 + errRate));
            int minValue = (int)(randCount / bucketCount * (1 - errRate));

            for (var i = 0; i < 100; i++) {
                Assert.IsTrue(histogram[i] >= minValue && histogram[i] <= maxValue, $"histogram {i} is {histogram[i]}, out of range [{minValue}, {maxValue}]");
            }
        }

        [TestMethod]
        public void TestRabiRandPeriod() {
            int randCount = 1000000;
            HashSet<ulong> seedSet = new() { rand.seed };
            for (var i = 0; i < randCount; i++) {
                var seed = rand.Next();
                Assert.IsFalse(seedSet.Contains(seed), $"seed {seed} is duplicated");
                seedSet.Add(seed);
            }
        }

        [TestMethod]
        public void TestNext() {
            int randCount = 1000;
            for (int i = 0; i < randCount; i++) {
                Assert.AreEqual(i, rand.Next(i, i + 1));
                var next3 = rand.Next(3);
                Assert.IsTrue(next3 >= 0 && next3 < 3);
                next3 = rand.Next(i, i + 3);
                Assert.IsTrue(next3 >= i && next3 < i + 3);
            }
        }

        [TestMethod]
        public void TestShuffle() {
            var list = new List<int>() { 10, 1, 1, 2, 2, 2, 3, 4, 9, 6, 6 };
            var toShuffle = list.ToList();
            rand.Shuffle(toShuffle);
            CollectionAssert.AreNotEqual(list, toShuffle);
            CollectionAssert.AreEquivalent(list, toShuffle);
        }

        [TestMethod]
        public void TestChoice() {
            var list = new List<int>() { 10, 1, 1, 2, 2, 2, 3, 4, 9, 6, 6 };
            CollectionAssert.Contains(list, rand.Choice(list));
            var choices = rand.Choice(list, 3).ToArray();
            foreach (var choice in choices) {
                CollectionAssert.Contains(list, choice);
            }
            Assert.AreEqual(3, choices.Length);
            choices = rand.Choice(list, list.Count).ToArray();
            CollectionAssert.AreNotEqual(list, choices);
            CollectionAssert.AreEquivalent(list, choices);
        }
    }
}