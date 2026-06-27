using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Json;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario {
  public class ScenarioInquiryMatcher(MultiPlayerInquiry inquiry, Action onFinish) {
    public class ScenarioPlayerInquiryMatcher(ScenarioInquiryMatcher parent, int playerId) {
      private readonly ScenarioInquiryMatcher parent = parent;
      private readonly int playerId = playerId;

      public ScenarioPlayerInquiryMatcher AssertAction<T>(Predicate<T> matcher) where T : IPlayerAction {
        parent.AssertAction(playerId, matcher);
        return this;
      }

      public ScenarioPlayerInquiryMatcher AssertAction<T>(Action<T> action = null) where T : IPlayerAction {
        return AssertAction<T>(x => {
          action?.Invoke(x);
          return true;
        });
      }

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

      public ScenarioPlayerInquiryMatcher ApplyAction<T, R>(R response, Action<T> action = null) where T : PlayerAction<R> {
        return ApplyAction<T, R>(response, x => {
          action?.Invoke(x);
          return true;
        });
      }

      public ScenarioPlayerInquiryMatcher ApplyAction<T>(Predicate<T> matcher) where T : PlayerAction<Empty> {
        return ApplyAction(Empty.Instance, matcher);
      }

      public ScenarioPlayerInquiryMatcher ApplyAction<T>(Action<T> action = null) where T : PlayerAction<Empty> {
        return ApplyAction(Empty.Instance, action);
      }

      public ScenarioPlayerInquiryMatcher ApplyAction<T>(int response, Predicate<T> matcher) where T : PlayerAction<int> {
        return ApplyAction<T, int>(response, matcher);
      }

      public ScenarioPlayerInquiryMatcher ApplyAction<T>(int response, Action<T> action = null) where T : PlayerAction<int> {
        return ApplyAction<T, int>(response, action);
      }

      public ScenarioPlayerInquiryMatcher ChooseTile<T>(string response, Predicate<T> matcher) where T : ChooseTileAction {
        var tile = new Tile(response);
        var action = parent.FindAction(playerId, matcher);
        Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
        int index = action.options.FindIndex(x => x.tile.tile == tile);
        Assert.IsTrue(index >= 0, $"tile {tile} not found in {action.options.Select(x => x.tile.tile)}");
        return ApplyAction(index, matcher);
      }

      public ScenarioPlayerInquiryMatcher ChooseTile<T>(string response, Action<T> action = null) where T : ChooseTileAction {
        return ChooseTile<T>(response, x => {
          action?.Invoke(x);
          return true;
        });
      }

      public ScenarioPlayerInquiryMatcher ChooseTiles<T>(string response, Predicate<T> matcher) where T : ChooseTilesAction {
        var tiles = new Tiles(response);
        var action = parent.FindAction(playerId, matcher);
        Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
        int index = action.options.FindIndex(x =>
            x.tiles
                .Select(t => t.tile)
                .SequenceEqualAfterSort(tiles));
        Assert.IsTrue(index >= 0, $"{tiles} not found in {action.options.Select(x => x.tiles)}");
        return ApplyAction(index, matcher);
      }

      public ScenarioPlayerInquiryMatcher ChooseTiles<T>(string response, Action<T> action = null) where T : ChooseTilesAction {
        return ChooseTiles<T>(response, x => {
          action?.Invoke(x);
          return true;
        });
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

    public readonly MultiPlayerInquiry inquiry = inquiry;
    private readonly Action onFinish = onFinish;
    private readonly HashSet<IPlayerAction> foundActions = [];

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
        return includeSubtypes ? a is not T : a.GetType() != typeof(T) ? false : matcher == null || matcher((T)a);
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

    public ScenarioInquiryMatcher AssertSkip(int playerId, bool canSkip = true) {
      return canSkip ? AssertAction<SkipAction>(playerId) : AssertNoAction<SkipAction>(playerId);
    }

    public ScenarioInquiryMatcher ApplySkip(int playerId) {
      return ApplyAction<SkipAction>(playerId);
    }

    public ScenarioInquiryMatcher ApplyAction<T, R>(int playerId, R response, Predicate<T> matcher = null) where T : PlayerAction<R> {
      var action = FindAction(playerId, matcher);
      var index = inquiry.GetByPlayerId(playerId).actions.IndexOf(action);
      Assert.IsNotNull(action, $"action {typeof(T).Name} not found");
      Assert.IsFalse(inquiry.hasExecuted, $"inquiry has already been executed");
      foundActions.Add(action);
      inquiry.OnResponse(new InquiryResponse(playerId, index, JsonSerializer.Serialize(response)));
      return this;
    }

    public ScenarioInquiryMatcher ApplyAction<T>(int playerId, Predicate<T> matcher = null) where T : PlayerAction<Empty> {
      return ApplyAction(playerId, Empty.Instance, matcher);
    }

    public ScenarioInquiryMatcher AssertAutoFinish(bool autoFinish = true) {
      onFinish();
      if (autoFinish) {
        Assert.IsTrue(inquiry.hasExecuted, "Inquiry not executed");
      } else {
        Assert.IsFalse(inquiry.hasExecuted, "Inquiry auto executed");
        inquiry.Finish();
      }
      return this;
    }

    public ScenarioInquiryMatcher AssertNoMoreActions(int playerId) {
      var notFound = inquiry.GetByPlayerId(playerId).actions.Where(a => !foundActions.Contains(a)).ToArray();
      Assert.AreEqual(0, notFound.Length, $"Unexpected actions {string.Join(',', notFound.Select(a => a.ToString()))}");
      return this;
    }

    public ScenarioInquiryMatcher Finish() {
      onFinish();
      inquiry.Finish();
      return this;
    }
  }

  public class ScenarioActionCenter : IActionCenter {
    private readonly int playerCount;

    private TaskCompletionSource<ScenarioInquiryMatcher> nextInquirySource = new();
    private string lastMsg;
    private readonly List<int> lastMsgSentTo = [];
    public Task<ScenarioInquiryMatcher> NextInquiry => nextInquirySource.Task;
    private TaskCompletionSource currentInquirySource = null;
    public Task CurrentInquiry => currentInquirySource.Task;
    public readonly GameLogMsg gameLog = new();

    public ScenarioActionCenter(int playerCount) {
      this.playerCount = playerCount;
      gameLog.PlayerLogs.AddRange(Enumerable.Range(0, playerCount).Select(_ => new PlayerLogMsg()));
    }

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
      // Test proto graph
      var proto = ev.game.SerializeProto<EventMsg>(ev, playerId);
      if (proto == null) {
        return;
      }
      if (ev.GetType().GetCustomAttribute<RabiIgnoreAttribute>() == null) {
        Assert.IsNotNull(proto, $"{ev.GetType().Name} not serialized");
        gameLog.PlayerLogs[playerId].Logs.Add(new SingleLogMsg {
          Event = proto,
        });
      } else {
        Assert.IsNull(proto, $"{ev.GetType().Name} serialized");
      }
      OnMessage(playerId, ev);
    }

    public void OnInquiry(MultiPlayerInquiry inquiry) {
      if (inquiry.IsEmpty) {
        return;
      }
      foreach (var playerInquiry in inquiry.playerInquiries) {
        // Test proto graph
        var proto = inquiry.game.SerializeProto<SinglePlayerInquiryMsg>(
            playerInquiry, playerInquiry.playerId);
        Assert.IsNotNull(proto, "Inquiry not serialized");
        gameLog.PlayerLogs[playerInquiry.playerId].Logs.Add(new SingleLogMsg {
          Inquiry = proto,
        });
        OnMessage(playerInquiry.playerId, playerInquiry);
      }
      SendImmediately();
      currentInquirySource = new();
      nextInquirySource.SetResult(new ScenarioInquiryMatcher(inquiry, () => {
        nextInquirySource = new();
        currentInquirySource.SetResult();
      }));
    }

    private void OnMessage(int playerId, object msg) {
      var str = RabiJson.Stringify(msg, playerId);
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