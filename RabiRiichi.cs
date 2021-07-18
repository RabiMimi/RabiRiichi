using HoshinoSharp.Attributes;
using HoshinoSharp.Attributes.Filters;
using HoshinoSharp.Attributes.Triggers;
using HoshinoSharp.Hoshino;
using HoshinoSharp.Runtime;
using RabiRiichi.Bot;
using RabiRiichi.Util;
using System.Threading.Tasks;

[assembly:HoshinoBot]
namespace RabiRiichi {
    [Service(Constants.SERVICE_NAME)]
    public class RabiRiichi : HoshinoService {

        [OnMessage]
        [GroupFilter(scope = ContextScope.Service)]
        public Task StartGame(HoshinoContext ctx) {
            var bot = ctx.GetComponent<HBot>();
            var ev = ctx.GetComponent<HEvent>();
            var pmCtx = ctx.GetComponent<HoshinoContext>(key: ev.GroupKey);
            var game = pmCtx.EnsureComponent<GameComponent>();
            return game.OnMessage(ev, bot);
        }

        // 私聊测试用
        [OnMessage]
        [PMFilter(scope = ContextScope.Service)]
        public Task StartGamePM(HoshinoContext ctx) {
            var bot = ctx.GetComponent<HBot>();
            var ev = ctx.GetComponent<HEvent>();
            var pmCtx = ctx.GetComponent<HoshinoContext>(key: ev.PMKey);
            var game = pmCtx.EnsureComponent<GameComponent>();
            return game.OnMessage(ev, bot);
        }
    }
}
