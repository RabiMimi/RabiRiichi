using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using System.Threading.Tasks;


namespace RabiRiichi.Events {
  [RabiIgnore]
  public class InitGameEvent(Game game) : EventBase(game) {
    public override string name => "init";

    public static Task OnGameInit(InitGameEvent ev) {
      ev.Q.Queue(new BeginGameEvent(ev, 0, 0, 0, 0));
      return Task.CompletedTask;
    }

    public static void Register(EventBus bus) {
      bus.Subscribe<InitGameEvent>(OnGameInit, EventPriority.Execute);
    }
  }
}