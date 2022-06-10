using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Json;
using RabiRiichi.Core;
using RabiRiichi.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario {
    public class ScenarioInquiryMatcher {
        public class ScenarioPlayerInquiryMatcher {
            private readonly ScenarioInquiryMatcher parent;
            private readonly int playerId;

            public ScenarioPlayerInquiryMatcher(ScenarioInquiryMatcher parent, int playerId) {
                this.parent = parent;
                this.playerId = playerId;
            }

            public ScenarioPlayerInquiryMatcher AssertAction<T>(Predicate<T> matcher) where T : IPlayerAction {
                parent.AssertAction(playerId, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher AssertAction<T>(Action<T> action = null) where T : IPlayerAction
                => AssertAction<T>(x => {
                    action?.Invoke(x);
                    return true;
                });

            public ScenarioPlayerInquiryMatcher AssertNoAction<T>(Predicate<T> matcher = null) where T : IPlayerAction {
                parent.AssertNoAction(playerId, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher AssertSkip(bool canSkip = true) {
                parent.AssertSkip(playerId, canSkip);
                return this;
            }

            public ScenarioPlayerInquiryMatcher ApplySkip() {
                parent.ApplySkip(playerId);
                return this;
            }

            public ScenarioPlayerInquiryMatcher ApplyAction<T, R>(R response, Predicate<T> matcher) where T : PlayerAction<R> {
                parent.ApplyAction(playerId, response, matcher);
                return this;
            }

            public ScenarioPlayerInquiryMatcher ApplyAction<T, R>(R response, Action<T> action = null) where T : PlayerAction<R>
                => ApplyAction<T, R>(response, x => {
                    action?.Invoke(x);
                    return true;
                });

            public ScenarioPlayerInquiryMatcher ApplyAction<T>(Predicate<T> matcher) where T : PlayerAction<bool>
                => ApplyAction(true, matcher);

            public ScenarioPlayerInquiryMatcher ApplyAction<T>(Action<T> action = null) where T : PlayerAction<bool>
                => ApplyAction(true, action);

            public ScenarioPlayerInquiryMatcher ApplyAction<T>(int response, Predicate<T> matcher) where T : PlayerAction<int>
                => ApplyAction<T, int>(response, matcher);

            public ScenarioPlayerInquiryMatcher ApplyAction<T>(int response, Action<T> action = null) where T : PlayerAction<int>
                => ApplyAction<T, int>(response, action);

            public ScenarioPlayerInquiryMatcher ChooseTile<T>(string response, Predicate<T> matcher) where T : ChooseTileAction {
                var tile = new Tile(response);
                var action = parent.FindAction(playerId, matcher);
                Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
                int index = action.options.FindIndex(x => (x as ChooseTileActionOption).tile.tile == tile);
                Assert.IsTrue(index >= 0, $"tile {tile} not found in {action.options.Select(x => (x as ChooseTileActionOption).tile.tile)}");
                return ApplyAction(index, matcher);
            }

            public ScenarioPlayerInquiryMatcher ChooseTile<T>(string response, Action<T> action = null) where T : ChooseTileAction
                => ChooseTile<T>(response, x => {
                    action?.Invoke(x);
                    return true;
                });

            public ScenarioPlayerInquiryMatcher ChooseTiles<T>(string response, Predicate<T> matcher) where T : ChooseTilesAction {
                var tiles = new Tiles(response);
                var action = parent.FindAction(playerId, matcher);
                Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
                int index = action.options.FindIndex(x =>
                    (x as ChooseTilesActionOption).tiles
                        .Select(t => t.tile)
                        .SequenceEqualAfterSort(tiles));
                Assert.IsTrue(index >= 0, $"{tiles} not found in {action.options.Select(x => (x as ChooseTilesActionOption).tiles)}");
                return ApplyAction(index, matcher);
            }

            public ScenarioPlayerInquiryMatcher ChooseTiles<T>(string response, Action<T> action = null) where T : ChooseTilesAction
                => ChooseTiles<T>(response, x => {
                    action?.Invoke(x);
                    return true;
                });

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
        private readonly System.Action onFinish;
        private readonly HashSet<IPlayerAction> foundActions = new();

        public ScenarioInquiryMatcher(MultiPlayerInquiry inquiry, System.Action onFinish) {
            this.inquiry = inquiry;
            this.onFinish = onFinish;
        }

        public ScenarioInquiryMatcher ForPlayer(int playerId, Action<ScenarioPlayerInquiryMatcher> action) {
            action(new ScenarioPlayerInquiryMatcher(this, playerId));
            return this;
        }

        public ScenarioInquiryMatcher AssertNoActionForPlayer(int playerId) {
            var playerInquiry = inquiry.GetByPlayerId(playerId);
            if (playerInquiry != null) {
                var errorStr = string.Join(", ", playerInquiry.actions.Select(action => action.ToString()));
                Assert.Fail($"Expect no inquiry for player {playerId} but found {errorStr}");
            }
            return this;
        }

        private T FindAction<T>(int playerId, Predicate<T> matcher = null, bool includeSubtypes = false) where T : IPlayerAction {
            var playerInquiry = inquiry.GetByPlayerId(playerId);
            if (playerInquiry == null) {
                Assert.Fail($"No inquiry for player {playerId}.");
            }
            return (T)playerInquiry.actions.Find(a => {
                if (includeSubtypes ? a is not T : a.GetType() != typeof(T)) {
                    return false;
                }
                return matcher == null || matcher((T)a);
            });
        }

        public ScenarioInquiryMatcher AssertAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            var action = FindAction(playerId, matcher);
            Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
            foundActions.Add(action);
            return this;
        }

        public ScenarioInquiryMatcher AssertNoAction<T>(int playerId, Predicate<T> matcher = null) where T : IPlayerAction {
            Assert.IsNull(FindAction(playerId, matcher), $"action {typeof(T).Name} found");
            return this;
        }

        public ScenarioInquiryMatcher AssertSkip(int playerId, bool canSkip = true)
            => canSkip ? AssertAction<SkipAction>(playerId) : AssertNoAction<SkipAction>(playerId);

        public ScenarioInquiryMatcher ApplySkip(int playerId)
            => ApplyAction<SkipAction>(playerId);

        public ScenarioInquiryMatcher ApplyAction<T, R>(int playerId, R response, Predicate<T> matcher = null) where T : PlayerAction<R> {
            var action = FindAction(playerId, matcher);
            var index = inquiry.GetByPlayerId(playerId).actions.IndexOf(action);
            Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
            Assert.IsFalse(inquiry.hasExecuted, $"inquiry has already been executed");
            foundActions.Add(action);
            inquiry.OnResponse(new InquiryResponse(playerId, index, JsonSerializer.Serialize(response)));
            return this;
        }

        public ScenarioInquiryMatcher ApplyAction<T>(int playerId, Predicate<T> matcher = null) where T : PlayerAction<bool>
            => ApplyAction(playerId, true, matcher);

        public ScenarioInquiryMatcher AssertAutoFinish(bool autoFinish = true) {
            if (autoFinish) {
                Assert.IsTrue(inquiry.hasExecuted, "Inquiry not executed");
            } else {
                Assert.IsFalse(inquiry.hasExecuted, "Inquiry auto executed");
                inquiry.Finish();
            }
            onFinish();
            return this;
        }

        public ScenarioInquiryMatcher AssertNoMoreActions(int playerId) {
            var notFound = inquiry.GetByPlayerId(playerId).actions.Where(a => !foundActions.Contains(a)).ToArray();
            Assert.AreEqual(0, notFound.Length, $"Unexpected actions {string.Join(',', notFound.Select(a => a.ToString()))}");
            return this;
        }

        public ScenarioInquiryMatcher Finish() {
            inquiry.Finish();
            onFinish();
            return this;
        }
    }

    public class ScenarioActionCenter : IActionCenter {
        private int playerCount = -1;
        private TaskCompletionSource<ScenarioInquiryMatcher> nextInquirySource = new();
        private string lastMsg;
        private readonly List<int> lastMsgSentTo = new();
        public Task<ScenarioInquiryMatcher> NextInquiry => nextInquirySource.Task;
        private TaskCompletionSource currentInquirySource = null;
        public Task CurrentInquiry => currentInquirySource.Task;

        ~ScenarioActionCenter() {
            SendImmediately();
        }

        private void SendImmediately() {
            if (string.IsNullOrWhiteSpace(lastMsg)) {
                return;
            }
            if (lastMsgSentTo.Count == playerCount) {
                Console.WriteLine($"ALL < {lastMsg}");
            } else {
                foreach (var playerId in lastMsgSentTo) {
                    Console.WriteLine($"P{playerId}  < {lastMsg}");
                }
            }
            lastMsg = null;
            lastMsgSentTo.Clear();
        }

        public void ForceFail(Exception e) {
            currentInquirySource?.TrySetException(e);
            nextInquirySource?.TrySetException(e);
        }

        public void ForceCancel() {
            currentInquirySource?.TrySetCanceled();
            nextInquirySource?.TrySetCanceled();
        }

        public void OnEvent(int playerId, EventBase ev) {
            OnMessage(ev.game, playerId, ev);
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            if (inquiry.IsEmpty) {
                return;
            }
            foreach (var playerInquiry in inquiry.playerInquiries) {
                OnMessage(inquiry.game, playerInquiry.playerId, playerInquiry);
            }
            SendImmediately();
            currentInquirySource = new();
            nextInquirySource.SetResult(new ScenarioInquiryMatcher(inquiry, () => {
                nextInquirySource = new();
                currentInquirySource.SetResult();
            }));
        }

        public void OnMessage(Game game, int playerId, IRabiMessage msg) {
            if (playerCount == -1) {
                playerCount = game.config.playerCount;
            }
            if (msg.IsRabiIgnore()) {
                return;
            }
            var str = JsonStringify.Stringify(msg, playerId);
            if (str != lastMsg) {
                SendImmediately();
            }
            lastMsg = str;
            if (!lastMsgSentTo.Contains(playerId)) {
                lastMsgSentTo.Add(playerId);
            }
        }
    }
}