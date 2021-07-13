using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public class MessageSender : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.MessageSender;

        public override async Task<bool> Handle(EventBase ev) {
            var bot = ev.game.hoshino.bot;
            var msgev = ev.game.hoshino.ev;
            if (ev is DealHandEvent e) {
                var unicode = e.hand.ToUnicode();
                var user = e.game.GetUser(e.player);
                await bot.SendPrivate(msgev.selfId, user.userId, unicode
                    + "\n" + e.hand.ToString());
            }
            return false;
        }
    }
}