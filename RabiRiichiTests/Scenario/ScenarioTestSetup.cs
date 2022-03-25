using RabiRiichi.Core.Setup;
using RabiRiichi.Event;
using RabiRiichi.Event.InGame;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario {
    public class ScenarioSetup : RiichiSetup {
        public readonly ScenarioActionCenter actionCenter;

        public ScenarioSetup(ScenarioActionCenter actionCenter) {
            this.actionCenter = actionCenter;
        }

        private Task DelayFinishPlayerAction(WaitPlayerActionEvent ev) {
            return actionCenter.CurrentInquiry;
        }

        protected override void RegisterEvents(EventBus eventBus) {
            base.RegisterEvents(eventBus);
            eventBus.Subscribe<WaitPlayerActionEvent>(DelayFinishPlayerAction, EventPriority.After);
        }
    }
}