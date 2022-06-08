using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Core.Setup;
using RabiRiichi.Util;
using System;
using System.Linq.Expressions;

namespace RabiRiichiTests.Helper {
    public abstract class RabiMock<T> where T : class {
        public Mock<T> mock { get; protected set; }
        public T Object => mock.Object;

        public Moq.Language.Flow.ISetup<T> Setup(Expression<Action<T>> expression) {
            return mock.Setup(expression);
        }

        public Moq.Language.Flow.ISetup<T, TResult> Setup<TResult>(Expression<Func<T, TResult>> expression) {
            return mock.Setup(expression);
        }

        public static implicit operator T(RabiMock<T> mock) => mock.Object;
    }

    public class RabiMockWall : RabiMock<Wall> {
        public RabiMockWall() {
            mock = new Mock<Wall>(new RabiRand(114514), new GameConfig()) {
                CallBase = true
            };
        }

        public void SetIsHaitei() {
            Object.remaining.RemoveRange(0, Object.remaining.Count + Object.rinshan.Count - Wall.NUM_RINSHAN);
        }
    }

    public class RabiMockGame : RabiMock<Game> {
        private class MockSetup : BaseSetup {
            private readonly Wall wall;

            public MockSetup(Wall wall) {
                this.wall = wall;
                wall.Reset();
            }

            public override void Inject(Game game, IServiceCollection collection) {
                base.Inject(game, collection);
                collection.RemoveAll<Wall>();
                collection.AddSingleton(wall);
            }
        }

        public readonly RabiMockWall wall;

        public RabiMockGame() {
            wall = new RabiMockWall();
            mock = new Mock<Game>(new GameConfig {
                actionCenter = new JsonStringActionCenter(null),
                setup = new MockSetup(wall)
            }) {
                CallBase = true
            };
        }
    }
}