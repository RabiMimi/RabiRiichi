namespace RabiRiichi.Actions {
  public class NextRoundAction : ConfirmAction {
    public override string name => "next_round";

    public NextRoundAction(int playerId) : base(playerId) {
      priority = 0;
    }
  }
}
