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
      
      // Remove all classical yaku (Koyaku) by default in scenario tests to preserve standard game expectations
      var classicalYakuTypes = new HashSet<Type> {
        typeof(TsubameGaeshi), typeof(Kanburi), typeof(Ryanankan), typeof(Chaopaikou), typeof(Chinpaikou),
        typeof(Shousanfon), typeof(Sanrenkou), typeof(Sanfonkou), typeof(Sanankan), typeof(Uumensai),
        typeof(IsshokuSandoujun), typeof(Chinpeikou),
        typeof(Suurenkou), typeof(IsshokuYondoujun), typeof(Renhou), typeof(Daisharin), typeof(Daichikurin), typeof(Daisuurin),
        typeof(Shiisanputa), typeof(Shiisuuputa), typeof(Paarenchan), typeof(Benikujaku), typeof(Heiiisou), typeof(Daichiishin),
        typeof(IshiNoUeNiMoSannen), typeof(Katengecchi), typeof(ShiiaruRaotai)
      };
      
      stdPatterns.RemoveAll(t => classicalYakuTypes.Contains(t) && !extraStdPatterns.Contains(t));
      stdPatterns.AddRange(extraStdPatterns);
    }

    protected override void RegisterEvents(EventBus eventBus) {
      base.RegisterEvents(eventBus);
      eventBus.Subscribe<WaitPlayerActionEvent>(DelayFinishPlayerAction, EventPriority.After);
    }
  }
}