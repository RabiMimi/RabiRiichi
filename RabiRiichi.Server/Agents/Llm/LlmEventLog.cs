using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// A compact, append-only log of publicly-visible game events, translated to
  /// short lines for the LLM. It is fed one event at a time (from the agent's
  /// <c>OnEvent</c>) and drained at each decision so the model receives only the
  /// delta since its last message. Token frugality is a priority: we record
  /// meaningful public facts (who discarded what, from hand or draw; calls;
  /// riichi; dora; wins/draws) but omit noise (jun counters, internal ids).
  ///
  /// This holds NO hidden information — it only reacts to broadcast fields.
  /// </summary>
  public sealed class LlmEventLog(Func<int, string> nameOf, string language) {
    private readonly Func<int, string> nameOf = nameOf;
    private readonly string language = language;
    private readonly List<string> lines = [];
    private readonly Lock gate = new();

    /// <summary> True once a new round has begun since the last drain. </summary>
    public bool NewRoundPending { get; private set; }

    private string Name(int seat) => nameOf(seat) ?? $"P{seat}";

    /// <summary> Records an event as a compact line, if it is worth sending. </summary>
    public void Record(EventBase ev) {
      var line = Translate(ev);
      if (line == null) {
        return;
      }
      lock (gate) {
        if (ev is BeginGameEvent) {
          NewRoundPending = true;
          lines.Clear();
          return;
        }
        if (lines.Count >= LlmLimits.MaxTranscriptLines) {
          lines.RemoveAt(0);
        }
        lines.Add(line);
      }
    }

    /// <summary>
    /// Returns and clears the buffered lines. Also clears the new-round flag;
    /// pass out whether a new round had begun so the caller can prepend the
    /// round header.
    /// </summary>
    public IReadOnlyList<string> Drain(out bool newRound) {
      lock (gate) {
        newRound = NewRoundPending;
        NewRoundPending = false;
        var copy = lines.ToList();
        lines.Clear();
        return copy;
      }
    }

    /// <summary> Translates a broadcast event into a compact line, or null. </summary>
    private string Translate(EventBase ev) {
      switch (ev) {
        case BeginGameEvent:
          return ""; // handled specially in Record (resets buffer)

        case RiichiEvent r:
          return $"{Name(r.playerId)} declares RIICHI, discards " +
              TileNotation.One(r.discarded, language);

        case DiscardTileEvent d:
          // fromHand = tedashi (chose from hand); else tsumogiri (drew & tossed).
          var how = d.fromHand ? "from hand" : "tsumogiri";
          return $"{Name(d.playerId)} discards " +
              $"{TileNotation.One(d.discarded, language)} ({how})";

        case ClaimTileEvent c:
          return $"{Name(c.playerId)} calls {ClaimKind(c.reason)} " +
              TileNotation.Group(c.group, language);

        case KanEvent k:
          return $"{Name(k.playerId)} declares " +
              $"{LlmKanNotation.Describe(k.kanSource, language)} " +
              TileNotation.Group(k.kan, language);

        case NukiDoraEvent n:
          return $"{Name(n.playerId)} pulls " +
              $"{TileNotation.One(n.incoming, language)} (nukidora)";

        case RevealDoraEvent dora when dora.dora != null:
          return $"New dora indicator: {TileNotation.One(dora.dora, language)} " +
              $"(indicates dora {TileNotation.One(dora.dora.tile.NextDora, language)})";

        case AgariEvent a:
          return TranslateAgari(a);

        case EndGameRyuukyokuEvent:
          return "Exhaustive draw (ryuukyoku)";

        case MidGameRyuukyokuEvent m:
          return $"Abortive draw ({m.name})";

        default:
          return null;
      }
    }

    private string TranslateAgari(AgariEvent a) {
      var winners = a.agariInfos.Select(i => Name(i.playerId)).Distinct().ToList();
      var who = string.Join(", ", winners);
      var mode = a.agariInfos.isTsumo
          ? "tsumo"
          : $"ron on {Name(a.agariInfos.fromPlayer)}'s " +
              TileNotation.One(a.agariInfos.incoming, language);
      return $"{who} wins by {mode}";
    }

    private static string ClaimKind(DiscardReason reason) => reason switch {
      DiscardReason.Chii => "chii",
      DiscardReason.Pon => "pon",
      _ => "meld",
    };

  }
}
