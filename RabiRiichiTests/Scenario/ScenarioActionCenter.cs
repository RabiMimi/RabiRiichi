using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Event;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario {
    public class ScenarioInquiryMatcher {
        public readonly MultiPlayerInquiry inquiry;
        private readonly Action onFinish;

        public ScenarioInquiryMatcher(MultiPlayerInquiry inquiry, Action onFinish) {
            this.inquiry = inquiry;
            this.onFinish = onFinish;
        }

        private IPlayerAction FindAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction
            => inquiry.GetByPlayerId(playerId).actions.Find(a => a is T t && (matcher == null || matcher(t)));

        public ScenarioInquiryMatcher AssertAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            Assert.IsNotNull(FindAction(playerId, matcher));
            return this;
        }

        public ScenarioInquiryMatcher AssertNoAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            Assert.IsNull(FindAction(playerId, matcher));
            return this;
        }

        public ScenarioInquiryMatcher ApplyAction<T, R>(R response, int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            var action = FindAction(playerId, matcher);
            var index = inquiry.GetByPlayerId(playerId).actions.IndexOf(action);
            Assert.IsNotNull(action);
            inquiry.OnResponse(new InquiryResponse(playerId, index, JsonSerializer.Serialize(response)));
            return this;
        }

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
            Console.WriteLine($"{playerId} < {game.json.Stringify(msg, playerId)}");
        }
    }
}