using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 抢拔北：听北风的玩家可以像抢槓一样抢和拔出的北。规则与抢槓相同（无役不能抢），
  /// 不限于国士无双。
  /// </summary>
  public class NukiChankanResolver(PatternResolver patternResolver) : RonResolver(patternResolver) {
  }
}
