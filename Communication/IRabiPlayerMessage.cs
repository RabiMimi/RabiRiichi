namespace RabiRiichi.Communication {
    public enum RabiMessageType {
        Unnecessary,
        Action,
        Inquiry,
        Event,
        Sync
    }

    public interface IRabiMessage {
        RabiMessageType msgType { get; }
    }

    public interface IRabiPlayerMessage {
        int playerId { get; }
    }
}