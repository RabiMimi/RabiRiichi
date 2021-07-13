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

        [OnFullMatch("有无")]
        [GroupFilter(scope = ContextScope.Service)]
        public Task StartGame(HoshinoContext ctx) {
            var bot = ctx.GetComponent<HBot>();
            var ev = ctx.GetComponent<HEvent>();
            var pmCtx = ctx.GetComponent<HoshinoContext>(key: ev.GroupKey);
            var game = pmCtx.EnsureComponent<GameComponent>();
            return game.AddPlayer(ev, bot);
        }

        // 私聊测试用
        [OnFullMatch("有无")]
        [PMFilter(scope = ContextScope.Service)]
        public Task StartGamePM(HoshinoContext ctx) {
            var bot = ctx.GetComponent<HBot>();
            var ev = ctx.GetComponent<HEvent>();
            var pmCtx = ctx.GetComponent<HoshinoContext>(key: ev.PMKey);
            var game = pmCtx.EnsureComponent<GameComponent>();
            return game.AddPlayer(ev, bot);
        }
    }
}
