using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public class MessageSender : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.MessageSender;

        public override async Task<bool> Handle(EventBase ev) {
            var bot = ev.game.hoshino.bot;
            var msgev = ev.game.hoshino.ev;
            if (ev is DealHandEvent e) {
                var user = e.game.GetUser(e.player);
                using var image = HaisImage.V.Generate(e.hand);
                MessageSegmentImage hand = new MessageSegmentImage(image);
                await bot.SendPrivate(msgev.selfId, user.userId, HMessage.From(new MessageSegment[] { hand }));
            }
            return false;
        }
    }
}