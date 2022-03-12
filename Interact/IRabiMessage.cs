namespace RabiRiichi.Interact {
    public interface IRabiMessage {
        string msgType { get; }
    }

    public interface IRabiPlayerMessage : IRabiMessage {
        int playerId { get; }
    }
}