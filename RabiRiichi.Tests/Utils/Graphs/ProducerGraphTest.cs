using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Utils.Graphs;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Utils.Graphs {

    [TestClass]
    public class ProducerGraphTest {
        private class TestGraph1 {
            private readonly int two = 2;

            [Produces("b")]
            public static int Add1([Consumes("a")] int a) {
                return a + 1;
            }

            [Produces("b", Cost = 114514)]
            private static int Triple([Consumes("a")] int a) {
                Assert.Fail("Should not be called");
                return a * 3;
            }

            [Produces("c")]
            private int Double([Consumes("b")] int a) {
                return a * two;
            }

            [Produces("async")]
            private static async Task<int> ProduceAsync() {
                await Task.Delay(1);
                return 1;
            }
        }

        private class InvalidGraph {
            [Produces("a")]
            public static int Add1(int a) {
                return a + 1;
            }
        }

        [TestMethod]
        public void TestCost() {
            Assert.AreEqual(6, new ProducerGraph()
                .Register(new TestGraph1())
                .Build()
                .SetInput("a", 2)
                .Execute<int>("c"));
        }

        [TestMethod]
        public void TestCache() {
            var graph = new ProducerGraph()
                .Register(new TestGraph1());
            for (int i = 0; i < 4; i++) {
                Assert.AreEqual((i + 1) * 2, graph
                    .Build()
                    .SetInput("a", i)
                    .Execute<int>("c"));
            }
            Assert.AreEqual(1, graph.routeCache.Count);
        }

        [TestMethod]
        public async Task TestAsync() {
            Assert.AreEqual(1, await new ProducerGraph()
                .Register<TestGraph1>()
                .Build()
                .ExecuteAsync<int>("async"));
        }

        [TestMethod]
        public void TestInvalid() {
            Assert.ThrowsException<ArgumentException>(() => new ProducerGraph()
                .Register(new InvalidGraph())
                .Build()
                .Execute<int>("a"));
        }
    }
}