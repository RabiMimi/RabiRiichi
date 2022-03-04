using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public class MessageSender {
        public uint CanListen(EventBase ev) => EventPriority.Broadcast;

        public static string ToString(TileSource source) {
            switch (source) {
                case TileSource.Chi:
                    return "吃";
                case TileSource.Pon:
                    return "碰";
                // case TileSource.Kan: return "杠";
                default:
                    return "";
            }
        }

        public Task<bool> Handle(EventBase ev) {
            /*
            var game = ev.game;
            if (ev is DealHandEvent dhe) {
                using var image = TilesImage.V.Generate(dhe.tiles);
                MessageSegmentImage hand = new MessageSegmentImage(image);
                await game.SendPrivate(dhe.player, HMessage.From(hand));
            } else if (ev is PlayTileEvent pte) {
                var player = game.GetPlayer(pte.player);
                var str = $"{player.nickname}打出了{pte.tile}";
                if (pte.riichi) {
                    str += "并立直";
                }
                await game.SendPublic(str);
            } else if (ev is GetTileEvent gte) {
                var player = game.GetPlayer(gte.player);
                var fromPlayer = game.GetPlayer(gte.incoming.fromPlayer);
                var str = $"{player.nickname}{ToString(gte.source)}了";
                if (player != fromPlayer) {
                    str += $"{fromPlayer.nickname}的{gte.incoming}：";
                }
                str += gte.group.ToString();
                await game.SendPublic(str);
            }*/
            return Task.FromResult(true);
        }
    }
}