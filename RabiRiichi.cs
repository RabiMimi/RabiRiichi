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
        private Task StartGame(HoshinoContext ctx, string scopeKey) {
            var bot = ctx.GetComponent<HBot>();
            var ev = ctx.GetComponent<HEvent>();
            var pmCtx = ctx.GetComponent<HoshinoContext>(key: scopeKey);
            //var game = pmCtx.EnsureComponent<GameComponent>();
            var game = pmCtx.EnsureComponent<RonCalcComponent>();
            return game.OnMessage(ev, bot);
        }

        [OnMessage]
        [GroupFilter(scope = ContextScope.Service)]
        public Task StartGame(HoshinoContext ctx) {
            var ev = ctx.GetComponent<HEvent>();
            return StartGame(ctx, ev.GroupKey);
        }

        // 私聊测试用
        [OnMessage]
        [PMFilter(scope = ContextScope.Service)]
        public Task StartGamePM(HoshinoContext ctx) {
            var ev = ctx.GetComponent<HEvent>();
            return StartGame(ctx, ev.PMKey);
        }
    }
}
