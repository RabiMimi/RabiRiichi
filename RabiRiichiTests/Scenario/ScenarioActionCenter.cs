using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario {
    public class ScenarioInquiryMatcher {
        public class ScenarioPlayerInquiryMatcher {
            private readonly ScenarioInquiryMatcher parent;
            private readonly int playerId;

            public ScenarioPlayerInquiryMatcher(ScenarioInquiryMatcher parent, int playerId) {
                this.parent = parent;
                this.playerId = playerId;
            }

            public ScenarioPlayerInquiryMatcher AssertAction<T>(Predicate<T> matcher = null) where T : IPlayerAction {
                parent.AssertAction(playerId, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher AssertNoAction<T>(Predicate<T> matcher = null) where T : IPlayerAction {
                parent.AssertNoAction(playerId, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher ApplyAction<T, R>(R response, Predicate<T> matcher = null) where T : IPlayerAction {
                parent.ApplyAction(playerId, response, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher ApplyAction<T>(Predicate<T> matcher = null) where T : IPlayerAction
                => ApplyAction(true, matcher);

            public ScenarioPlayerInquiryMatcher AssertAutoFinish(bool autoFinish = true) {
                parent.AssertAutoFinish(autoFinish);
                return this;
            }

            public ScenarioPlayerInquiryMatcher AssertNoMoreActions() {
                parent.AssertNoMoreActions(playerId);
                return this;
            }

            public ScenarioPlayerInquiryMatcher Finish() {
                parent.Finish();
                return this;
            }
        }

        public readonly MultiPlayerInquiry inquiry;
        private readonly Action onFinish;
        private readonly HashSet<IPlayerAction> foundActions = new();

        public ScenarioInquiryMatcher(MultiPlayerInquiry inquiry, Action onFinish) {
            this.inquiry = inquiry;
            this.onFinish = onFinish;
        }

        public ScenarioPlayerInquiryMatcher ForPlayer(int playerId) {
            return new ScenarioPlayerInquiryMatcher(this, playerId);
        }

        private IPlayerAction FindAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction
            => inquiry.GetByPlayerId(playerId).actions.Find(a => a is T t && (matcher == null || matcher(t)));

        public ScenarioInquiryMatcher AssertAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            var action = FindAction(playerId, matcher);
            Assert.IsNotNull(action);
            foundActions.Add(action);
            return this;
        }

        public ScenarioInquiryMatcher AssertNoAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            Assert.IsNull(FindAction(playerId, matcher));
            return this;
        }

        public ScenarioInquiryMatcher ApplyAction<T, R>(int playerId, R response, Predicate<T> matcher = null) where T : IPlayerAction {
            var action = FindAction(playerId, matcher);
            var index = inquiry.GetByPlayerId(playerId).actions.IndexOf(action);
            Assert.IsNotNull(action);
            foundActions.Add(action);
            inquiry.OnResponse(new InquiryResponse(playerId, index, JsonSerializer.Serialize(response)));
            return this;
        }

        public ScenarioInquiryMatcher ApplyAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction
            => ApplyAction(playerId, true, matcher);

        public ScenarioInquiryMatcher AssertAutoFinish(bool autoFinish = true) {
            if (autoFinish) {
                Assert.IsTrue(inquiry.hasExecuted);
            } else {
                Assert.IsFalse(inquiry.hasExecuted);
                inquiry.Finish();
            }
            onFinish();
            return this;
        }

        public ScenarioInquiryMatcher AssertNoMoreActions(int playerId) {
            Assert.IsTrue(inquiry.GetByPlayerId(playerId).actions.All(action => foundActions.Contains(action)));
            return this;
        }

        public ScenarioInquiryMatcher Finish() {
            inquiry.Finish();
            onFinish();
            return this;
        }
    }

    public class ScenarioActionCenter : IActionCenter {
        private TaskCompletionSource<ScenarioInquiryMatcher> nextInquirySource = new();
        public Task<ScenarioInquiryMatcher> NextInquiry => nextInquirySource.Task;
        private TaskCompletionSource currentInquirySource = null;
        public Task CurrentInquiry => currentInquirySource.Task;

        public void OnEvent(int playerId, EventBase ev) {
            OnMessage(ev.game, playerId, ev);
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            currentInquirySource = new();
            nextInquirySource.SetResult(new ScenarioInquiryMatcher(inquiry, () => {
                nextInquirySource = new();
                currentInquirySource.SetResult();
            }));
        }

        public void OnMessage(Game game, int playerId, IRabiMessage msg) {
            if (msg.IsRabiIgnore()) {
                return;
            }
            Console.WriteLine($"P{playerId} < {game.json.Stringify(msg, playerId)}");
        }
    }
}