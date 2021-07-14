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
            HaisImage haisImage = new HaisImage();
            if (ev is DealHandEvent e) {
                var user = e.game.GetUser(e.player);
                MessageSegmentImage hand = new MessageSegmentImage(haisImage.Generate(e.hand));
                await bot.SendPrivate(msgev.selfId, user.userId, HMessage.From(new MessageSegment[] { hand }));
            }
            return false;
        }
    }
}