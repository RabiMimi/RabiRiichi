using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Util;
using System.Collections.Generic;

namespace RabiRiichiTests.Util {
    [TestClass]
    public class RabiRandTest {
        [TestMethod]
        public void TestRabiRandDistribution() {
            int randCount = 1000000;
            int bucketCount = 100;
            float errRate = 0.1f;
            var rand = new RabiRand(114514);
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
            ulong seed = 114514;
            HashSet<ulong> seedSet = new() { seed };
            var rand = new RabiRand(seed);
            for (var i = 0; i < randCount; i++) {
                seed = rand.Next();
                Assert.IsFalse(seedSet.Contains(seed), $"seed {seed} is duplicated");
                seedSet.Add(seed);
            }
        }
    }
}