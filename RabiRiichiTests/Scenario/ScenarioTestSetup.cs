using RabiRiichi.Core.Setup;
using RabiRiichi.Event;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario {
    public class ScenarioSetup : RiichiSetup {
        private static Task CancelInitGameEvent(InitGameEvent ev) {
            ev.Cancel();
            return Task.CompletedTask;
        }

        protected override void RegisterEvents(EventBus eventBus) {
            base.RegisterEvents(eventBus);
            eventBus.Subscribe<InitGameEvent>(CancelInitGameEvent, EventPriority.Prepare);
        }
    }
}