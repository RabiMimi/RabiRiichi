using RabiRiichi.Patterns;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Core.Setup {
  /// <summary> 古役目录。默认全部关闭，由房间配置逐个开启 </summary>
  public static class KoyakuPatterns {
    /// <summary> 全部古役 </summary>
    public static readonly IReadOnlyList<Type> ALL = [
      // 1番
      typeof(TsubameGaeshi),
      typeof(Kanburi),
      typeof(ShiiaruRaotai),
      typeof(Shousanfon),

      // 2番
      typeof(Sanrenkou),
      typeof(Sanfonkou),
      typeof(Chaopaikou),
      typeof(Chinpaikou),
      typeof(Uumensai),
      typeof(Ryanankan),

      // 3番
      typeof(IsshokuSandoujun),
      typeof(Chinpeikou),

      // 役满
      typeof(Suurenkou),
      typeof(IsshokuYondoujun),
      typeof(Sanankan),
      typeof(Renhou),
      typeof(Katengecchi),
      typeof(IshiNoUeNiMoSannen),
      typeof(Heiiisou),
      typeof(Benikujaku),
      typeof(Daisharin),
      typeof(Daichikurin),
      typeof(Daisuurin),
      typeof(Shiisanputa),
      typeof(Shiisuuputa),
      typeof(Paarenchan),

      // 两倍役满
      typeof(Daichiishin),
    ];
  }
}
