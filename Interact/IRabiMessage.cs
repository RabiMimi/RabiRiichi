namespace RabiRiichi.Interact {
    public enum RabiMessageType {
        Unnecessary,
        Action,
        Inquiry,
        Event,
    }

    public interface IRabiMessage {
        RabiMessageType msgType { get; }
    }

    public interface IRabiPlayerMessage : IRabiMessage {
        int playerId { get; }
    }
}