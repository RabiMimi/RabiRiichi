using RabiRiichi.Pattern;
using System;


namespace RabiRiichi.Riichi.Setup {
    public class RiichiSetup : BaseSetup {

        protected override void InitPatterns() {
            basePatterns = new Type[] {
                typeof(Base33332),
                typeof(Base72),
                typeof(Base13_1)
            };
            stdPatterns = new Type[] {
                typeof(一杯口),
                typeof(平和),
                typeof(役牌中),
                typeof(役牌发),
                typeof(役牌白),
                typeof(役牌自风),
                typeof(役牌场风),
                typeof(断幺九),
                typeof(门清自摸和)
            };
            bonusPatterns = new Type[] {
                typeof(赤宝牌),
                typeof(宝牌),
                typeof(里宝牌)
            };
        }
    }
}