using RabiRiichi.Core.Setup;
using RabiRiichi.Event;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario {
    public class ScenarioSetup : RiichiSetup {
        public readonly ScenarioActionCenter actionCenter;
        private readonly List<Type> extraStdPatterns = new();

        public ScenarioSetup(ScenarioActionCenter actionCenter) {
            this.actionCenter = actionCenter;
        }

        private Task DelayFinishPlayerAction(WaitPlayerActionEvent ev) {
            return actionCenter.CurrentInquiry;
        }

        public void AddExtraStdPattern<T>() where T : StdPattern {
            extraStdPatterns.Add(typeof(T));
        }

        protected override void InitPatterns() {
            base.InitPatterns();
            stdPatterns.AddRange(extraStdPatterns);
        }

        protected override void RegisterEvents(EventBus eventBus) {
            base.RegisterEvents(eventBus);
            eventBus.Subscribe<WaitPlayerActionEvent>(DelayFinishPlayerAction, EventPriority.After);
        }
    }
}