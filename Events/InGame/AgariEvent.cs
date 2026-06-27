using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Patterns;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Events.InGame {
  [RabiMessage]
  public class AgariInfo(int playerId, ScoreStorage scores) : IRabiPlayerMessage {
    [RabiBroadcast] public int playerId { get; init; } = playerId;
    [RabiBroadcast] public readonly ScoreStorage scores = scores;

    public AgariInfoMsg ToProto(IEnumerable<GameTileMsg> freeTiles) {
      var ret = new AgariInfoMsg {
        PlayerId = playerId,
        Scores = scores.ToProto(),
      };
      ret.FreeTiles.AddRange(freeTiles);
      return ret;
    }
  }

  [RabiMessage]
  public class AgariInfoList(int fromPlayer, GameTile incoming, params AgariInfo[] agariInfos) : IEnumerable<AgariInfo> {
    [RabiBroadcast] private readonly List<AgariInfo> agariInfos = [.. agariInfos];
    [RabiBroadcast] public readonly int fromPlayer = fromPlayer;
    [RabiBroadcast] public readonly GameTile incoming = incoming;

    public void Add(AgariInfo info) {
      agariInfos.Add(info);
    }

    public IEnumerator<AgariInfo> GetEnumerator() {
      return agariInfos.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return agariInfos.GetEnumerator();
    }

    public int Count => agariInfos.Count;
    public AgariInfo this[int index] => agariInfos[index];
  }

  [RabiMessage]
  public class AgariEvent(EventBase parent, AgariInfoList info) : EventBase(parent) {
    public class Builder(EventBase parent, int fromPlayer, GameTile incoming) : IEventBuilder {
      public readonly EventBase parent = parent;
      public readonly AgariInfoList agariInfos = new AgariInfoList(fromPlayer, incoming);

      public Builder Add(AgariInfo agariInfo) {
        agariInfos.Add(agariInfo);
        return this;
      }
      public EventBase Build() {
        return agariInfos.Count == 0 ? null : (EventBase)new AgariEvent(parent, agariInfos);
      }
    }
    public override string name => "agari";
    #region Request
    public bool isTsumo => agariInfos.incoming.IsTsumo;
    [RabiBroadcast] public readonly AgariInfoList agariInfos = info;

    #endregion
  }
}