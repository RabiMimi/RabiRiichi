using RabiRiichi.Core.Setup;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario {
  public class ScenarioSetup(ScenarioActionCenter actionCenter) : RiichiSetup {
    public readonly ScenarioActionCenter actionCenter = actionCenter;
    private readonly List<Type> extraStdPatterns = [];

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