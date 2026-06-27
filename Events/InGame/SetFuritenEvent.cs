using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public abstract class SetFuritenEvent(EventBase parent, int playerId, bool furiten) : PrivatePlayerEvent(parent, playerId) {
    #region Request
    [RabiBroadcast] public bool furiten = furiten;

    #endregion
  }

  public class SetTempFuritenEvent(EventBase parent, int playerId, bool furiten) : SetFuritenEvent(parent, playerId, furiten) {
    public override string name => "set_temp_furiten";
  }

  public class SetRiichiFuritenEvent(EventBase parent, int playerId, bool furiten) : SetFuritenEvent(parent, playerId, furiten) {
    public override string name => "set_riichi_furiten";
  }

  public class SetDiscardFuritenEvent(EventBase parent, int playerId, bool furiten) : SetFuritenEvent(parent, playerId, furiten) {
    public override string name => "set_discard_furiten";
  }
}