using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Util.Graphs;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Util.Graphs {

    [TestClass]
    public class ProducerGraphTest {
        private class TestGraph1 {
            private readonly int two = 2;

            [Produces("a")]
            public static int Add1([Input("a")] int a) {
                return a + 1;
            }

            [Produces("b", Cost = 114514)]
            private static int Triple([Input("a")] int a) {
                Assert.Fail("Should not be called");
                return a * 3;
            }

            [Produces("b")]
            private int Double([Consumes("a")] int a) {
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
                .Execute<int>("b"));
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